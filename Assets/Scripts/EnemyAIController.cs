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

    [Header("Acompanhamento de Saque")]
    [Tooltip("Fator de cobertura lateral no saque (0.85 = cobre 85% do deslocamento do player, bissetriz ideal).")]
    [Range(0.5f, 1.0f)] public float serveTrackingFactor = 0.85f;
    [Tooltip("Multiplicador da velocidade de deslocamento durante o acompanhamento de saque.")]
    public float serveTrackingSpeedMultiplier = 1.0f;
    [Tooltip("Suavização do movimento de acompanhamento para evitar mudanças bruscas.")]
    public float serveTrackingSmoothness = 6.0f;

    [Header("Comportamento de Posicionamento e Recuo")]
    [Tooltip("Limite máximo de aproximação da rede em ralis (0 = fica estritamente na linha base, 0.25 = avança no máximo 25% da quadra até a rede). Evita que a IA cole na rede.")]
    [Range(0f, 0.5f)] public float maxNetApproachRatio = 0.25f;
    [Tooltip("Multiplicador da velocidade de deslocamento ao recuar/voltar para a quadra.")]
    public float recoverySpeedMultiplier = 1.15f;
    [Tooltip("Tempo em segundos durante o golpe em que o movimento fica travado para a animação da raquete antes de liberar o recuo.")]
    public float swingFreezeDuration = 0.10f;

    [Header("Referências")]
    public Transform ball;
    public Transform playerTransform;
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
    private float currentServeTrackX = 0f;
    private bool isSwinging = false;
    private bool canHit = true;
    private bool isMoving = false;
    private bool lastRunningAnim = false;
    private float lastRunChangeTime = -10f;
    private const float minAnimSwitchTime = 0.1f;

    private float lastTargetDirY = 0f;
    private float currentErrorOffset = 0f;

    public bool isHitting => isSwinging;

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
        FindPlayerReferences();

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
        currentServeTrackX = homePosition.x;

        hitRadius = Mathf.Max(hitRadius, 0.60f);
        ApplyLevel(currentLevel);
    }

    void Start()
    {
        FindBallReferences();
        FindPlayerReferences();
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

    private void FindPlayerReferences()
    {
        if (playerTransform == null)
        {
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                playerTransform = player.transform;
            }
            else if (GameManager.Instance != null && GameManager.Instance.playerController != null)
            {
                playerTransform = GameManager.Instance.playerController.transform;
            }
        }
    }

    public void ApplyLevel(int level)
    {
        currentLevel = Mathf.Clamp(level, 1, 5);
        currentDifficulty = DifficultyPresets[currentLevel - 1];

        // Escala o raio de golpe e a cobertura de saque da IA de acordo com a graduação
        switch (currentLevel)
        {
            case 1: hitRadius = 0.60f; serveTrackingFactor = 0.75f; break;
            case 2: hitRadius = 0.65f; serveTrackingFactor = 0.80f; break;
            case 3: hitRadius = 0.70f; serveTrackingFactor = 0.85f; break;
            case 4: hitRadius = 0.75f; serveTrackingFactor = 0.90f; break;
            case 5: hitRadius = 0.80f; serveTrackingFactor = 0.95f; break;
        }
    }

    void Update()
    {
        if (GameManager.Instance != null && (GameManager.Instance.IsGameFinished || GameManager.Instance.IsLevelTransitioning || GameManager.Instance.IsGamePaused))
        {
            StopMovement();
            return;
        }

        if (ball == null || ballScript == null)
        {
            FindBallReferences();
            if (ball == null || ballScript == null) return;
        }

        if (playerTransform == null)
        {
            FindPlayerReferences();
        }

        if (isServingMode) return;

        UpdateHitAreaPosition();

        // 1. Checa se o Player 1 está na fase de preparação do saque
        bool isPlayerPreparingServe = (ballScript != null && ballScript.IsServing && ballScript.IsPlayer1Serving);

        if (isPlayerPreparingServe)
        {
            reactionTimer = 0f;
            lastTargetDirY = 0f;

            if (playerTransform != null)
            {
                // Acompanha lateralmente a posição do Player com base na bissetriz angular de quadra
                float desiredX = Mathf.Clamp(playerTransform.position.x * serveTrackingFactor, minX, maxX);
                currentServeTrackX = Mathf.Lerp(currentServeTrackX, desiredX, Time.deltaTime * serveTrackingSmoothness);
                targetPosition = new Vector2(currentServeTrackX, homePosition.y);
            }
            else
            {
                targetPosition = homePosition;
            }
            return;
        }

        // 2. Se a bola não estiver em rally ativo, mantém posição base defensiva
        if (!ballScript.IsRallyActive)
        {
            reactionTimer = 0f;
            lastTargetDirY = 0f;
            targetPosition = homePosition;
            return;
        }

        // 3. Monitora mudanças no sentido da bola (novo golpe ou recuperação)
        float currentDirY = ballScript.TargetDirection.y;
        if (currentDirY > 0.05f && lastTargetDirY <= 0.05f)
        {
            // O jogador acabou de rebater a bola em direção à IA: reseta o tempo de reação
            reactionTimer = 0f;
            currentErrorOffset = Random.Range(-currentDifficulty.errorMargin, currentDifficulty.errorMargin);
        }
        else if (currentDirY < -0.05f && lastTargetDirY >= -0.05f)
        {
            // A IA acabou de rebater a bola (ou a bola está descendo): reseta o timer
            reactionTimer = 0f;
        }
        lastTargetDirY = currentDirY;

        reactionTimer += Time.deltaTime;

        // Se ainda estiver na breve trava de impacto da animação do golpe, aguarda
        if (isSwinging) return;

        // 4. Calcula o alvo de movimentação inteligente (recuo ou interceptação)
        CalculateTargetPosition();

        // 5. Checagem de rebatida da IA
        if (canHit && !isSwinging)
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
        if (GameManager.Instance != null && (GameManager.Instance.IsGameFinished || GameManager.Instance.IsLevelTransitioning || GameManager.Instance.IsGamePaused))
        {
            StopMovement();
            return;
        }

        if (isServingMode || isSwinging || ball == null || ballScript == null)
        {
            StopMovement();
            return;
        }

        // Modo de acompanhamento de saque: move-se na linha de base acompanhando o player
        if (ballScript.IsServing && ballScript.IsPlayer1Serving)
        {
            MoveTowardsTarget(currentDifficulty.moveSpeed * serveTrackingSpeedMultiplier);
            return;
        }

        if (!ballScript.IsRallyActive)
        {
            StopMovement();
            return;
        }

        // MODO DE RECUPERAÇÃO DE QUADRA:
        // A bola está indo para o jogador (descendo na quadra).
        // A IA recua ativamente para sua base no fundo da quadra com agilidade.
        if (ballScript.TargetDirection.y < -0.05f)
        {
            MoveTowardsTarget(currentDifficulty.moveSpeed * recoverySpeedMultiplier);
            return;
        }

        // MODO DE INTERCEPTAÇÃO:
        // A bola está subindo em direção à IA.
        bool isServeReturn = (ballScript.TargetDirection.y > 0.05f && ball.position.y < (ballScript != null ? ballScript.netY : 0f));
        float effectiveDelay = isServeReturn 
            ? Mathf.Min(currentDifficulty.reactionDelay, 0.08f) 
            : currentDifficulty.reactionDelay;

        if (reactionTimer < effectiveDelay)
        {
            // Enquanto o tempo de reação ao golpe adversário não passou,
            // a IA continua suavemente seu reposicionamento para a base (não congela sem motivo)
            MoveTowardsTarget(currentDifficulty.moveSpeed * 0.75f);
            return;
        }

        MoveTowardsTarget(currentDifficulty.moveSpeed);
    }

    private void MoveTowardsTarget(float speed)
    {
        float distance = Vector2.Distance(rb.position, targetPosition);
        float threshold = isMoving ? stopDistance : resumeDistance;

        if (distance <= threshold)
        {
            isMoving = false;
            StopMovement();
            return;
        }

        isMoving = true;

        Vector2 newPos = Vector2.MoveTowards(rb.position, targetPosition, speed * Time.fixedDeltaTime);
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
        if (ball == null || ballScript == null) return;

        // =========================================================================
        // CASO 1: A BOLA ESTÁ VOLTANDO PARA O JOGADOR (descendo / TargetDirection.y < -0.05f)
        // =========================================================================
        // A IA recua imediatamente para a quadra (fundo de quadra / linha base) e se
        // centraliza na bissetriz da quadra, preparando-se para o próximo golpe.
        if (ballScript.TargetDirection.y < -0.05f)
        {
            float targetY = homePosition.y;

            // Cobertura inteligente do centro / bissetriz da quadra
            float recoveryX = Mathf.Lerp(homePosition.x, ball.position.x * 0.30f, 0.5f);
            float targetX = Mathf.Clamp(recoveryX, minX, maxX);

            targetPosition = new Vector2(targetX, targetY);
            return;
        }

        // =========================================================================
        // CASO 2: A BOLA ESTÁ VINDO EM DIREÇÃO À IA (subindo / TargetDirection.y > 0.05f)
        // =========================================================================
        // A IA intercepta a bola mantendo sua base defensiva no fundo da quadra.
        // NUNCA avança desnecessariamente para a frente da rede, evitando enviesar o jogo.
        if (ballScript.TargetDirection.y > 0.05f)
        {
            float baseDefenseY = homePosition.y;

            // Limite rigoroso de aproximação da rede:
            // Permite no máximo uma fração segura (ex: 25%) da distância entre a base e minY
            float netBoundary = Mathf.Lerp(homePosition.y, minY, Mathf.Clamp01(maxNetApproachRatio));

            Vector2 dir = ballScript.ActualDirection;
            if (dir.y <= 0.01f) dir = ballScript.TargetDirection;

            // Predição de onde a bola vai cruzar a linha de base da IA (homePosition.y)
            float dy = baseDefenseY - ball.position.y;
            float predictedX = ball.position.x;

            if (dy > 0f && dir.y > 0.02f)
            {
                predictedX = ball.position.x + dy * (dir.x / dir.y);
            }

            // Aplica erro calibrado conforme a dificuldade selecionada
            predictedX += currentErrorOffset;

            float targetX = Mathf.Clamp(predictedX, minX, maxX);
            float targetY = baseDefenseY;

            // Checa se a bola possui ângulo muito fechado que cruzaria as laterais antes da linha de base.
            // Nesse caso específico, a IA corta o ângulo avançando suavemente até o limite netBoundary.
            if ((predictedX < minX || predictedX > maxX) && dir.y > 0.02f && Mathf.Abs(dir.x) > 0.01f)
            {
                float lateralLimit = predictedX < minX ? minX : maxX;
                float deltaX = lateralLimit - ball.position.x;
                float intersectY = ball.position.y + deltaX * (dir.y / dir.x);

                targetY = Mathf.Clamp(intersectY, netBoundary, maxY);
                targetX = lateralLimit;
            }
            else
            {
                // Em ralis normais, a IA mantém a base defensiva no fundo da quadra.
                // Se a bola vier em altíssima velocidade, recua ligeiramente para ter mais espaço.
                float speedRatio = Mathf.Max(0f, (ballScript.CurrentSpeed - ballScript.baseSpeed) / Mathf.Max(ballScript.baseSpeed, 1f));
                float depthAdjustment = Mathf.Clamp(speedRatio * 0.15f * (maxY - homePosition.y), 0f, (maxY - homePosition.y) * 0.6f);

                targetY = Mathf.Clamp(baseDefenseY + depthAdjustment, netBoundary, maxY);
            }

            targetPosition = new Vector2(targetX, targetY);
            return;
        }

        // Caso neutro / bola parada
        targetPosition = homePosition;
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
        CancelInvoke(nameof(UnlockMovementAfterHit));
        CancelInvoke(nameof(ResetHitState));
        isServingMode = active;
        isSwinging = false;
        canHit = true;
        isMoving = false;
        reactionTimer = 0f;
        lastTargetDirY = 0f;
        currentErrorOffset = 0f;

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
        else
        {
            // Player 1 está preparando o saque: posiciona a IA na base pronta para acompanhar e receber
            Vector3 receivingPos = new Vector3(homePosition.x, homePosition.y, transform.position.z);
            transform.position = receivingPos;
            if (rb != null)
            {
                rb.position = receivingPos;
                rb.MovePosition(receivingPos);
            }
            Physics2D.SyncTransforms();
            targetPosition = receivingPos;
            currentServeTrackX = homePosition.x;
        }
    }

    public void HitBall()
    {
        if (isSwinging || !canHit) return;

        isSwinging = true;
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

        // Libera o movimento rapidamente após a batida para permitir o recuo imediato para a quadra
        float freezeTime = Mathf.Min(swingFreezeDuration, hitDuration * 0.35f);
        CancelInvoke(nameof(UnlockMovementAfterHit));
        Invoke(nameof(UnlockMovementAfterHit), freezeTime);

        // Cooldown para poder golpear novamente
        CancelInvoke(nameof(ResetHitState));
        Invoke(nameof(ResetHitState), hitDuration);
    }

    private void UnlockMovementAfterHit()
    {
        isSwinging = false;
    }

    private void ResetHitState()
    {
        isSwinging = false;
        canHit = true;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 strikePos = GetStrikePosition();
        Gizmos.color = isSwinging ? Color.green : Color.red;
        Gizmos.DrawWireSphere(strikePos, hitRadius);

        // Área de alcance frontal
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.4f);
        Gizmos.DrawWireCube(transform.position - new Vector3(0f, hitRadius * 0.5f, 0f), new Vector3(hitRadius * 1.9f, hitRadius * 1.6f, 0f));

        // Limite de segurança contra avanço excessivo para a rede
        float netBoundary = Mathf.Lerp(homePosition.y, minY, Mathf.Clamp01(maxNetApproachRatio));
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(new Vector3(minX, netBoundary, 0f), new Vector3(maxX, netBoundary, 0f));

        // Posição alvo de movimentação
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(targetPosition, 0.08f);
        Gizmos.DrawLine(transform.position, targetPosition);
    }
}
