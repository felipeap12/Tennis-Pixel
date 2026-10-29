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
    public float hitRadius = 0.45f;

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
    public float hitDuration = 0.4f;

    [Header("Animator")]
    public string idleStateName = "Player2_Idle";

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Ball ballScript;

    private Vector2 targetPosition;
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

        if (ball != null) ballScript = ball.GetComponent<Ball>();

        if (hitArea == null)
        {
            Transform found = transform.Find("HitArea");
            if (found != null) hitArea = found;
        }

        if (minX > maxX) { float t = minX; minX = maxX; maxX = t; }
        if (minY > maxY) { float t = minY; minY = maxY; maxY = t; }

        homePosition = new Vector2(Mathf.Clamp(homePosition.x, minX, maxX), Mathf.Clamp(homePosition.y, minY, maxY));
        targetPosition = homePosition;

        ApplyLevel(currentLevel);
    }

    public void ApplyLevel(int level)
    {
        currentLevel = Mathf.Clamp(level, 1, 5);
        currentDifficulty = DifficultyPresets[currentLevel - 1];
    }

    void Update()
    {
        if (ball == null || ballScript == null) return;
        if (isServingMode || isHitting) return;

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

        if (canHit && hitArea != null)
        {
            if (Vector2.Distance(hitArea.position, ball.position) <= hitRadius)
            {
                HitBall();
            }
        }
    }

    void FixedUpdate()
    {
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

    void CalculateTargetPosition()
    {
        float targetX = Mathf.Clamp(ball.position.x, minX, maxX);
        float targetY = homePosition.y;

        if (ball.position.y > 0f && ball.position.y < targetY)
        {
            targetY = Mathf.Max(minY, ball.position.y - 0.03f);
        }
        else if (ball.position.y > 0.55f)
        {
            targetY = Mathf.Clamp(ball.position.y, minY, maxY);
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

        if (animator != null)
            animator.SetTrigger("Hit");

        if (ball != null)
            ball.SendMessage("OnHitByEnemy", transform.position, SendMessageOptions.DontRequireReceiver);

        Invoke(nameof(ResetHitState), hitDuration);
    }

    private void ResetHitState()
    {
        isHitting = false;
        canHit = true;
    }

    private void OnDrawGizmosSelected()
    {
        if (hitArea != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(hitArea.position, hitRadius);
        }
    }
}