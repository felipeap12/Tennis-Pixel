using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movimentação")]
    public float moveSpeed = 2.5f;

    [Header("Limites da Quadra (Sua Metade)")]
    public float minX = -0.617f;
    public float maxX = 0.617f;
    public float minY = -0.75f;
    public float maxY = -0.15f;

    [Header("Restrições de Saque")]
    [Tooltip("Bloqueia a saída do jogador da área permitida durante a preparação do saque.")]
    public bool restrictServeArea = true;
    [Tooltip("Se verdadeiro, fixa o jogador rigidamente no centro da linha de fundo para sacar.")]
    public bool lockPositionOnServe = true;
    [Tooltip("Limites horizontais permitidos caso lockPositionOnServe seja falso (área central do fundo).")]
    public float serveMinX = -0.18f;
    public float serveMaxX = 0.18f;
    [Tooltip("Limites verticais permitidos caso lockPositionOnServe seja falso.")]
    public float serveMinY = -0.65f;
    public float serveMaxY = -0.65f;

    [Header("Rebatida")]
    public KeyCode hitKey = KeyCode.Space;
    public Transform hitArea;
    public float hitRadius = 0.65f;

    [Header("Janela de Golpe (Timing Window)")]
    [Tooltip("Tempo em segundos durante o qual o movimento de swing permanece ativo para interceptar a bola.")]
    public float swingWindowDuration = 0.28f;
    [Tooltip("Intervalo mínimo entre tentativas de rebatida.")]
    public float swingCooldown = 0.32f;

    [Header("Referências")]
    public Ball ballScript;
    public Transform ballTransform;

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Vector2 movement;

    private Vector3 initialHitAreaLocalPos = new Vector3(0.35f, 0.08f, 0f);
    private float swingWindowTimer = 0f;
    private float swingCooldownTimer = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (hitArea == null)
        {
            Transform foundArea = transform.Find("HitArea");
            if (foundArea != null) hitArea = foundArea;
        }

        if (hitArea != null)
        {
            initialHitAreaLocalPos = hitArea.localPosition;
            if (Mathf.Abs(initialHitAreaLocalPos.x) < 0.1f)
            {
                initialHitAreaLocalPos.x = 0.35f;
            }
        }

        hitRadius = Mathf.Max(hitRadius, 0.65f);
        FindBallReferences();
    }

    void Start()
    {
        FindBallReferences();
    }

    private void FindBallReferences()
    {
        if (ballScript == null)
        {
            ballScript = FindAnyObjectByType<Ball>();
        }
        if (ballTransform == null && ballScript != null)
        {
            ballTransform = ballScript.transform;
        }
    }

    public bool IsPreparingServe
    {
        get
        {
            if (ballScript == null) FindBallReferences();
            return ballScript != null && ballScript.IsServing && ballScript.IsPlayer1Serving;
        }
    }

    public void ApplyLevel(int level)
    {
        // Ajusta atributos do Player proporcionalmente a cada nível
        switch (Mathf.Clamp(level, 1, 5))
        {
            case 1: moveSpeed = 2.5f; hitRadius = 0.65f; break;
            case 2: moveSpeed = 2.8f; hitRadius = 0.70f; break;
            case 3: moveSpeed = 3.1f; hitRadius = 0.75f; break;
            case 4: moveSpeed = 3.4f; hitRadius = 0.80f; break;
            case 5: moveSpeed = 3.7f; hitRadius = 0.85f; break;
        }
    }

    void Update()
    {
        if (GameManager.Instance != null && (GameManager.Instance.IsGameFinished || GameManager.Instance.IsLevelTransitioning || GameManager.Instance.IsGamePaused))
        {
            movement = Vector2.zero;
            if (animator != null) animator.SetBool("IsRunning", false);
            swingWindowTimer = 0f;
            return;
        }

        bool isPreparingServe = IsPreparingServe;
        float inputX = Input.GetAxisRaw("Horizontal");
        float inputY = Input.GetAxisRaw("Vertical");

        // Quando estiver preparando o saque e a restrição estiver ativa
        if (isPreparingServe && restrictServeArea && lockPositionOnServe)
        {
            movement = Vector2.zero;

            if (animator != null)
            {
                animator.SetBool("IsRunning", false);
                animator.SetFloat("MoveX", 0f);
                animator.SetFloat("MoveY", 0f);
            }

            // Permite mirar para a esquerda ou direita (orientando o sprite) antes de sacar
            if (spriteRenderer != null && Mathf.Abs(inputX) > 0.01f)
            {
                spriteRenderer.flipX = inputX < 0f;
            }
        }
        else
        {
            movement.x = inputX;
            movement.y = inputY;

            if (animator != null)
            {
                bool isMoving = movement.sqrMagnitude > 0;
                animator.SetBool("IsRunning", isMoving);
                animator.SetFloat("MoveX", Mathf.Abs(movement.x));
                animator.SetFloat("MoveY", movement.y);
            }

            if (spriteRenderer != null && Mathf.Abs(movement.x) > 0.01f)
            {
                spriteRenderer.flipX = movement.x < 0f;
            }
        }

        // Espelha a HitArea para o lado correto conforme a orientação do personagem
        UpdateHitAreaPosition();

        // Contagem dos timers de golpe e recarga
        if (swingCooldownTimer > 0f)
        {
            swingCooldownTimer -= Time.deltaTime;
        }

        if (swingWindowTimer > 0f)
        {
            swingWindowTimer -= Time.deltaTime;
            TryHitBall();
        }

        if (Input.GetKeyDown(hitKey) && swingCooldownTimer <= 0f)
        {
            // Durante o saque, a execução é orquestrada centralizadamente pela bola
            if (!isPreparingServe)
            {
                StartSwing();
            }
        }
    }

    void FixedUpdate()
    {
        if (GameManager.Instance != null && (GameManager.Instance.IsGameFinished || GameManager.Instance.IsLevelTransitioning || GameManager.Instance.IsGamePaused))
        {
            if (rb != null) rb.linearVelocity = Vector2.zero;
            return;
        }

        bool isPreparingServe = IsPreparingServe;

        // Limita a área de posicionamento no saque para evitar quebrar a mecânica nos cantos
        if (isPreparingServe && restrictServeArea)
        {
            if (lockPositionOnServe)
            {
                Vector2 fixedServePos = ballScript != null ? (Vector2)ballScript.player1ServePosition : new Vector2(0f, -0.65f);
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.MovePosition(fixedServePos);
                }
                else
                {
                    transform.position = fixedServePos;
                }
                return;
            }
            else
            {
                Vector2 serveTarget = rb.position + movement.normalized * moveSpeed * Time.fixedDeltaTime;
                serveTarget.x = Mathf.Clamp(serveTarget.x, serveMinX, serveMaxX);
                serveTarget.y = Mathf.Clamp(serveTarget.y, serveMinY, serveMaxY);

                if (rb != null) rb.MovePosition(serveTarget);
                else transform.position = serveTarget;
                return;
            }
        }

        Vector2 targetPosition = rb.position + movement.normalized * moveSpeed * Time.fixedDeltaTime;

        targetPosition.x = Mathf.Clamp(targetPosition.x, minX, maxX);
        targetPosition.y = Mathf.Clamp(targetPosition.y, minY, maxY);

        if (rb != null) rb.MovePosition(targetPosition);
        else transform.position = targetPosition;
    }

    private void UpdateHitAreaPosition()
    {
        if (hitArea == null) return;

        bool facingLeft = spriteRenderer != null && spriteRenderer.flipX;
        float targetX = facingLeft ? -Mathf.Abs(initialHitAreaLocalPos.x) : Mathf.Abs(initialHitAreaLocalPos.x);

        if (Mathf.Abs(hitArea.localPosition.x - targetX) > 0.001f)
        {
            hitArea.localPosition = new Vector3(targetX, initialHitAreaLocalPos.y, initialHitAreaLocalPos.z);
        }
    }

    public void StartSwing()
    {
        if (animator != null)
        {
            animator.SetTrigger("Hit");
        }

        swingWindowTimer = swingWindowDuration;
        swingCooldownTimer = swingCooldown;

        TryHitBall();
    }

    public void HitBall()
    {
        StartSwing();
    }

    private bool TryHitBall()
    {
        if (ballScript == null || ballTransform == null)
        {
            FindBallReferences();
            if (ballScript == null || ballTransform == null) return false;
        }

        if (!ballScript.IsRallyActive) return false;

        // Não rebate uma bola que o jogador já enviou para cima (em direção ao adversário)
        if (ballScript.TargetDirection.y > 0.05f) return false;

        Vector3 strikePos = GetStrikePosition();
        float dist = Vector2.Distance(strikePos, ballTransform.position);

        // Checagem de alcance frontal e lateral ao redor do jogador
        float dx = Mathf.Abs(ballTransform.position.x - transform.position.x);
        float dy = ballTransform.position.y - transform.position.y;
        bool inFrontalReach = (dx <= hitRadius * 0.95f) && (dy >= -0.30f && dy <= hitRadius * 1.30f);

        // Checagem de colisão física por segurança adicional
        bool physicsHit = false;
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(strikePos, hitRadius);
        foreach (var col in hitColliders)
        {
            if (col.CompareTag("Ball"))
            {
                physicsHit = true;
                break;
            }
        }

        if (dist <= hitRadius || inFrontalReach || physicsHit)
        {
            // Consome a janela de golpe para garantir exatamente 1 rebatida
            swingWindowTimer = 0f;

            // Passa a posição e a direção de movimento para gerar efeito (Spin) na bola
            ballScript.OnHitByPlayer(new object[] { transform.position, movement });
            Physics2D.SyncTransforms();
            return true;
        }

        return false;
    }

    public Vector3 GetStrikePosition()
    {
        if (hitArea != null)
        {
            return hitArea.position;
        }

        bool facingLeft = spriteRenderer != null && spriteRenderer.flipX;
        float offsetX = facingLeft ? -Mathf.Abs(initialHitAreaLocalPos.x) : Mathf.Abs(initialHitAreaLocalPos.x);
        return transform.position + new Vector3(offsetX, initialHitAreaLocalPos.y, 0f);
    }

    /// <summary>
    /// Reposiciona o jogador de forma limpa na posição de saque e reseta velocidades
    /// </summary>
    public void ResetToServePosition(Vector3 servePos)
    {
        transform.position = servePos;
        if (rb != null)
        {
            rb.position = servePos;
            rb.linearVelocity = Vector2.zero;
        }
        movement = Vector2.zero;
        if (animator != null)
        {
            animator.SetBool("IsRunning", false);
            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", 0f);
        }
        Physics2D.SyncTransforms();
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 strikePos = GetStrikePosition();
        Gizmos.color = swingWindowTimer > 0f ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(strikePos, hitRadius);

        // Área de alcance frontal
        Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
        Gizmos.DrawWireCube(transform.position + new Vector3(0f, hitRadius * 0.5f, 0f), new Vector3(hitRadius * 1.9f, hitRadius * 1.6f, 0f));

        // Visualização da Área de Saque
        if (restrictServeArea)
        {
            Gizmos.color = new Color(1f, 0.92f, 0.22f, 0.6f);
            if (lockPositionOnServe)
            {
                Vector3 p = ballScript != null ? ballScript.player1ServePosition : new Vector3(0f, -0.65f, 0f);
                Gizmos.DrawWireSphere(p, 0.08f);
            }
            else
            {
                Vector3 center = new Vector3((serveMinX + serveMaxX) * 0.5f, (serveMinY + serveMaxY) * 0.5f, 0f);
                Vector3 size = new Vector3(serveMaxX - serveMinX, Mathf.Max(serveMaxY - serveMinY, 0.05f), 0f);
                Gizmos.DrawWireCube(center, size);
            }
        }
    }
}
