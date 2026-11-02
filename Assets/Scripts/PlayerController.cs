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
        if (GameManager.Instance != null && (GameManager.Instance.IsGameFinished || GameManager.Instance.IsLevelTransitioning))
        {
            movement = Vector2.zero;
            if (animator != null) animator.SetBool("IsRunning", false);
            swingWindowTimer = 0f;
            return;
        }

        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");

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
            StartSwing();
        }
    }

    void FixedUpdate()
    {
        if (GameManager.Instance != null && (GameManager.Instance.IsGameFinished || GameManager.Instance.IsLevelTransitioning))
        {
            if (rb != null) rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 targetPosition = rb.position + movement.normalized * moveSpeed * Time.fixedDeltaTime;

        targetPosition.x = Mathf.Clamp(targetPosition.x, minX, maxX);
        targetPosition.y = Mathf.Clamp(targetPosition.y, minY, maxY);

        rb.MovePosition(targetPosition);
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

    private void OnDrawGizmosSelected()
    {
        Vector3 strikePos = GetStrikePosition();
        Gizmos.color = swingWindowTimer > 0f ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(strikePos, hitRadius);

        // Área de alcance frontal
        Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
        Gizmos.DrawWireCube(transform.position + new Vector3(0f, hitRadius * 0.5f, 0f), new Vector3(hitRadius * 1.9f, hitRadius * 1.6f, 0f));
    }
}
