using UnityEngine;

public class EnemyAIController : MonoBehaviour
{
    [System.Serializable]
    public struct AIDifficulty
    {
        public string levelName;
        public float moveSpeed;
        public float reactionDelay;
        public float errorMargin;
        public float missChancePercent;
    }

    [Header("Seleção de Nível (1 a 5)")]
    [Range(1, 5)] public int currentLevel = 1;

    // Presets calibrados e balanceados para 16 PPU
    public static readonly AIDifficulty[] DifficultyPresets = new AIDifficulty[]
    {
        new AIDifficulty { levelName = "1. Iniciante",     moveSpeed = 2.00f, reactionDelay = 0.35f, errorMargin = 0.30f, missChancePercent = 25f },
        new AIDifficulty { levelName = "2. Amador",        moveSpeed = 2.60f, reactionDelay = 0.25f, errorMargin = 0.20f, missChancePercent = 15f },
        new AIDifficulty { levelName = "3. Intermediário", moveSpeed = 3.20f, reactionDelay = 0.18f, errorMargin = 0.10f, missChancePercent = 5f  },
        new AIDifficulty { levelName = "4. Avançado",      moveSpeed = 3.80f, reactionDelay = 0.10f, errorMargin = 0.05f, missChancePercent = 0f  },
        new AIDifficulty { levelName = "5. Profissional",  moveSpeed = 4.40f, reactionDelay = 0.02f, errorMargin = 0.00f, missChancePercent = 0f  }
    };

    [Header("Dificuldade Ativa")]
    public AIDifficulty currentDifficulty;

    [Header("Referências")]
    public Transform ball;
    public Transform hitArea;
    public float hitRadius = 0.60f;

    [Header("Limites da IA (Quadra)")]
    public float minX = -0.617f;
    public float maxX = 0.617f;
    public float minY = 0.15f;
    public float maxY = 0.75f;
    public Vector2 homePosition = new Vector2(0f, 0.45f);

    [Header("Histerese de Movimento")]
    public float stopDistance = 0.015f;
    public float resumeDistance = 0.04f;

    [Header("Rebatida")]
    public float hitDuration = 0.35f;

    [Header("Animator")]
    public string idleStateName = "Player2_Idle";

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Ball ballScript;

    private Vector2 targetPosition;
    private Vector3 initialHitAreaLocalPos = new Vector3(0f, -0.25f, 0f);
    private float reactionTimer;
    private bool isServingMode = false;
    private bool isHitting = false;
    private bool canHit = true;
    private bool isMoving = false;
    private bool lastRunningAnim = false;
    private float lastRunChangeTime = -10f;
    private const float minAnimSwitchTime = 0.1f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.None;
        }
        if (animator != null) animator.applyRootMotion = false;

        FindBallReferences();

        if (hitArea == null)
        {
            Transform found = transform.Find("HitArea");
            if (found != null) hitArea = found;
        }

        // Corrige offsets corrompidos ou assimétricos na cena
        if (hitArea != null)
        {
            Vector3 locPos = hitArea.localPosition;
            if (Mathf.Abs(locPos.x) > 0.35f || locPos.y < -0.45f || locPos.y > 0.2f)
            {
                hitArea.localPosition = new Vector3(0f, -0.25f, 0f);
            }
            initialHitAreaLocalPos = hitArea.localPosition;
        }

        if (minX > maxX) { float t = minX; minX = maxX; maxX = t; }
        if (minY > maxY) { float t = minY; minY = maxY; maxY = t; }

        homePosition = new Vector2(Mathf.Clamp(homePosition.x, minX, maxX), Mathf.Clamp(homePosition.y, minY, maxY));
        targetPosition = homePosition;

        hitRadius = Mathf.Max(hitRadius, 0.60f);
        ApplyLevel(currentLevel);
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
        if (ball == null && ballScript != null)
        {
            ball = ballScript.transform;
        }
    }

    public void ApplyLevel(int level)
    {
        currentLevel = Mathf.Clamp(level, 1, 5);
        currentDifficulty = DifficultyPresets[currentLevel - 1];

        // Escala o raio de golpe da IA de forma justa de acordo com a graduação
        switch (currentLevel)
        {
            case 1: hitRadius = 0.60f; break;
            case 2: hitRadius = 0.65f; break;
            case 3: hitRadius = 0.70f; break;
            case 4: hitRadius = 0.75f; break;
            case 5: hitRadius = 0.80f; break;
        }
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameFinished)
        {
            StopMovement();
            return;
        }

        if (ball == null || ballScript == null)
        {
            FindBallReferences();
            if (ball == null || ballScript == null) return;
        }

        if (isServingMode || isHitting) return;

        UpdateHitAreaPosition();

        if (!ballScript.IsRallyActive)
        {
            reactionTimer = 0f;
            targetPosition = homePosition;
            return;
        }

        reactionTimer += Time.deltaTime;

        if (reactionTimer >= currentDifficulty.reactionDelay)
        {
            CalculateTargetPosition();
        }

        // Checagem robusta de rebatida da IA
        if (canHit && !isHitting)
        {
            // A IA só deve rebater quando a bola estiver se aproximando dela (subindo na quadra)
            if (ballScript.TargetDirection.y > 0.05f)
            {
                Vector3 strikePos = GetStrikePosition();
                float dist = Vector2.Distance(strikePos, ball.position);

                // Checagem de alcance frontal e lateral ao redor da IA
                float dx = Mathf.Abs(ball.position.x - transform.position.x);
                float dy = transform.position.y - ball.position.y; // positivo quando a bola está à frente da IA

                bool inFrontalReach = (dx <= hitRadius * 0.95f) && (dy >= -0.20f && dy <= hitRadius * 1.30f);

                if (dist <= hitRadius || inFrontalReach)
                {
                    HitBall();
                }
            }
        }
    }

    void FixedUpdate()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameFinished)
        {
            StopMovement();
            return;
        }

        if (isServingMode || isHitting || ball == null || ballScript == null)
        {
            StopMovement();
            return;
        }

        if (!ballScript.IsRallyActive || reactionTimer < currentDifficulty.reactionDelay)
        {
            StopMovement();
            return;
        }

        float distance = Vector2.Distance(rb.position, targetPosition);
        float threshold = isMoving ? stopDistance : resumeDistance;

        if (distance <= threshold)
        {
            isMoving = false;
            StopMovement();
            return;
        }

        isMoving = true;

        Vector2 newPos = Vector2.MoveTowards(rb.position, targetPosition, currentDifficulty.moveSpeed * Time.fixedDeltaTime);
        newPos.x = Mathf.Clamp(newPos.x, minX, maxX);
        newPos.y = Mathf.Clamp(newPos.y, minY, maxY);

        Vector2 dir = (newPos - rb.position).normalized;
        rb.MovePosition(newPos);

        if (spriteRenderer != null && Mathf.Abs(dir.x) > 0.01f)
        {
            spriteRenderer.flipX = dir.x < 0f;
        }

        if (animator != null)
        {
            SetRunning(true);
            float animX = Mathf.Abs(dir.x) > 0.15f ? Mathf.Sign(dir.x) : 0f;
            float animY = Mathf.Abs(dir.y) > 0.15f ? Mathf.Sign(dir.y) : 0f;

            animator.SetFloat("MoveX", animX);
            animator.SetFloat("MoveY", animY);
        }
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

    void CalculateTargetPosition()
    {
        float targetX = Mathf.Clamp(ball.position.x, minX, maxX);
        float targetY = homePosition.y;

        // Se a bola estiver subindo em direção à metade da quadra da IA
        if (ballScript != null && ballScript.TargetDirection.y > 0.05f)
        {
            if (ball.position.y < homePosition.y - 0.4f)
            {
                // Acompanha a aproximação mantendo distância ideal para contato frontal
                targetY = Mathf.Clamp(ball.position.y + 0.35f, minY, maxY);
            }
            else
            {
                targetY = Mathf.Clamp(ball.position.y, minY, maxY);
            }
        }

        targetPosition = new Vector2(targetX, targetY);
    }

    private void SetRunning(bool value, bool force = false)
    {
        if (animator == null || lastRunningAnim == value) return;
        if (!force && Time.time - lastRunChangeTime < minAnimSwitchTime) return;
        lastRunningAnim = value;
        lastRunChangeTime = Time.time;
        animator.SetBool("IsRunning", value);
    }

    private void StopMovement()
    {
        if (rb != null) rb.linearVelocity = Vector2.zero;
        SetRunning(false);
    }

    public void SetServeMode(bool active, Vector3 servePosition)
    {
        CancelInvoke(nameof(ResetHitState));
        isServingMode = active;
        isHitting = false;
        canHit = true;
        isMoving = false;
        reactionTimer = 0f;

        if (rb != null) rb.linearVelocity = Vector2.zero;

        lastRunningAnim = false;
        if (animator != null)
        {
            animator.ResetTrigger("Hit");
            animator.SetBool("IsRunning", false);
            animator.Play(idleStateName, 0, 0f);
            animator.Update(0f);
        }

        if (active)
        {
            transform.position = servePosition;
            if (rb != null)
            {
                rb.position = servePosition;
                rb.MovePosition(servePosition);
            }
            Physics2D.SyncTransforms();
            targetPosition = servePosition;
        }
    }

    public void HitBall()
    {
        if (isHitting || !canHit) return;

        isHitting = true;
        canHit = false;
        isServingMode = false;
        isMoving = false;

        StopMovement();
        SetRunning(false, true);

        // Vira a IA para a direção da bola no instante do impacto
        if (spriteRenderer != null && ball != null)
        {
            float diffX = ball.position.x - transform.position.x;
            if (Mathf.Abs(diffX) > 0.05f)
            {
                spriteRenderer.flipX = diffX < 0f;
            }
        }

        if (animator != null)
        {
            animator.SetTrigger("Hit");
        }

        if (ballScript != null)
        {
            ballScript.OnHitByEnemy(transform.position);
        }
        else if (ball != null)
        {
            ball.SendMessage("OnHitByEnemy", transform.position, SendMessageOptions.DontRequireReceiver);
        }

        Invoke(nameof(ResetHitState), hitDuration);
    }

    private void ResetHitState()
    {
        isHitting = false;
        canHit = true;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 strikePos = GetStrikePosition();
        Gizmos.color = isHitting ? Color.green : Color.red;
        Gizmos.DrawWireSphere(strikePos, hitRadius);

        // Área de alcance frontal
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.4f);
        Gizmos.DrawWireCube(transform.position - new Vector3(0f, hitRadius * 0.5f, 0f), new Vector3(hitRadius * 1.9f, hitRadius * 1.6f, 0f));
    }
}
