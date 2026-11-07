using UnityEngine;

public class Ball : MonoBehaviour
{
    [Header("Configurações de Velocidade Base")]
    public float baseSpeed = 5.0f;           // Velocidade inicial (configurada no Inspector)
    public float maxSpeed = 10.0f;          // Teto máximo de velocidade no rally
    public float speedIncreasePerHit = 0.5f; // Aumento a cada rebatida no rally
    public float currentSpeed;

    private float initialInspectorSpeed = 0f;

    [Header("Efeito de Curva (Spin Suave e Físico)")]
    [Tooltip("Controla a intensidade máxima do desvio lateral.")]
    public float spinForce = 1.8f;
    [Tooltip("Velocidade de transição da curva (quanto maior, mais rápida a resposta da curva).")]
    public float curveSmoothness = 3.0f;

    private float currentSpin = 0f;
    private Vector2 targetDirection; // Direção base sem efeito
    private Vector2 actualDirection; // Direção real com a curva aplicada

    [Header("Limites da Quadra para Ponto")]
    public float courtMinX = -0.617f;
    public float courtMaxX = 0.617f;
    public float courtMinY = -0.91f;
    public float courtMaxY = 0.91f;
    [Tooltip("Posição Y da rede. Se a bola sair pelas laterais após passar da rede, conta como bola vencedora do atacante.")]
    public float netY = 0f;

    [Header("Posições de Saque (Linha de Fundo)")]
    public Vector3 player1ServePosition = new Vector3(0f, -0.65f, 0f);
    public Vector3 player2ServePosition = new Vector3(0f, 0.65f, 0f);

    [Header("Parâmetros de Ângulo e Efeito do Saque")]
    [Tooltip("Desvio lateral máximo que o jogador pode aplicar no saque (mantém a bola jogável e dentro da quadra).")]
    public float maxPlayerServeAngle = 0.20f;
    [Tooltip("Efeito de curva (spin) máximo permitido no saque do jogador para evitar que a bola saia da quadra.")]
    public float maxPlayerServeSpin = 0.40f;

    [Header("Ajuste de Distância da Bola no Saque")]
    public Vector3 p1BallOffset = new Vector3(0.08f, 0.25f, 0f);  // Ligeiramente acima do P1
    public Vector3 p2BallOffset = new Vector3(-0.08f, -0.25f, 0f); // Ligeiramente abaixo da IA

    [Header("Referências dos Jogadores")]
    public Transform player1Transform;
    public Transform player2Transform;

    [Header("Efeitos Sonoros (Batida da Raquete)")]
    public AudioSource hitAudioSource;
    public AudioClip racketHitSound;
    [Range(0f, 1f)] public float hitSoundVolume = 0.85f;
    [Tooltip("Aplica uma elevação sutil e dinâmica de tom (pitch) à medida que o rally fica mais rápido e tenso.")]
    public bool dynamicRallyPitch = true;

    public int currentRally { get; private set; } = 0;

    private bool isBallInPlay = false;
    private bool isServing = false;
    private bool isPlayer1Serving = true;
    private bool isProcessingPoint = false;
    private bool canCheckPoint = false; // Trava para evitar falso ponto ao sacar
    private float serveLockoutUntil = 0f;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;

    public bool IsRallyActive => isBallInPlay && !isServing && !isProcessingPoint;
    public bool IsServing => isServing;
    public bool IsPlayer1Serving => isPlayer1Serving;
    public Vector2 TargetDirection => targetDirection;
    public Vector2 ActualDirection => actualDirection;
    public float CurrentSpeed => currentSpeed;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (hitAudioSource == null)
        {
            hitAudioSource = GetComponent<AudioSource>();
            if (hitAudioSource == null)
            {
                hitAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        hitAudioSource.playOnAwake = false;
        hitAudioSource.spatialBlend = 0f; // Áudio estéreo 2D limpo

        // Salva a velocidade inicial configurada no Inspector
        if (initialInspectorSpeed <= 0f)
        {
            initialInspectorSpeed = baseSpeed;
        }

        // NOTA: O primeiro saque é disparado pelo botão de entrar do Menu (LoginManager)
    }

    /// <summary>
    /// Ajusta a velocidade da bola de acordo com o nível atual, usando como base o valor do Inspector
    /// </summary>
    public void ApplyLevel(int level)
    {
        if (initialInspectorSpeed <= 0f)
        {
            initialInspectorSpeed = baseSpeed;
        }

        int cl = Mathf.Clamp(level, 1, 5);

        // Nível 1 = 100% da velocidade do Inspector (ex: 5.0)
        // Nível 2 = +20% | Nível 3 = +40% | Nível 4 = +60% | Nível 5 = +80%
        float speedMultiplier = 1.0f + ((cl - 1) * 0.20f);

        baseSpeed = initialInspectorSpeed * speedMultiplier;
        maxSpeed = baseSpeed * 1.8f; // O limite máximo no rally sobe proporcionalmente
        currentSpeed = baseSpeed;

        Debug.Log($"[Ball] Nível {cl}: BaseSpeed = {baseSpeed} | MaxSpeed = {maxSpeed}");
    }

    void Update()
    {
        if (GameManager.Instance != null && (GameManager.Instance.IsGameFinished || GameManager.Instance.IsGamePaused)) return;

        if (isServing)
        {
            FollowServerPosition();

            // Durante a transição de nível ou pausa, bloqueia o saque e impede que a bola seja solta
            if (GameManager.Instance != null && (GameManager.Instance.IsLevelTransitioning || GameManager.Instance.IsGamePaused))
            {
                return;
            }

            // Permite sacar com Espaço apenas quando não estiver bloqueado por transição/pause e fora da janela de lockout
            if (isPlayer1Serving && Time.time >= serveLockoutUntil && Input.GetKeyDown(KeyCode.Space))
            {
                ExecuteServe();
            }
            return;
        }

        // Se estiver em transição de nível ou pause, congela a bola
        if (GameManager.Instance != null && (GameManager.Instance.IsLevelTransitioning || GameManager.Instance.IsGamePaused))
        {
            return;
        }

        if (isBallInPlay && !isProcessingPoint)
        {
            // Calcula a direção desejada inclinando o vetor base de acordo com o spin
            Vector2 desiredDirection = targetDirection;
            if (Mathf.Abs(currentSpin) > 0.01f)
            {
                desiredDirection.x += currentSpin * (spinForce * 0.1f);
                desiredDirection.Normalize();
            }

            // Interpola suavemente a direção para criar um arco natural (evita o efeito 'L')
            actualDirection = Vector2.Lerp(actualDirection, desiredDirection, Time.deltaTime * curveSmoothness);

            // Move a bola na direção curvada mantendo a velocidade constante
            transform.Translate(actualDirection * currentSpeed * Time.deltaTime, Space.World);
            CheckOutOfBounds();
        }
    }

    private void FollowServerPosition()
    {
        if (isPlayer1Serving && player1Transform != null)
        {
            transform.position = player1Transform.position + p1BallOffset;
        }
        else if (!isPlayer1Serving && player2Transform != null)
        {
            transform.position = player2Transform.position + p2BallOffset;
        }
    }

    public void PrepareServe(bool serveToPlayer1)
    {
        CancelInvoke(nameof(ExecuteServe));
        CancelInvoke(nameof(EnablePointChecking));

        if (GameManager.Instance != null && GameManager.Instance.IsGameFinished) return;
        if (GameManager.Instance != null && GameManager.Instance.IsLevelTransitioning) return;

        serveLockoutUntil = Time.time + 0.35f; // Janela de proteção para não disparar no mesmo clique que fechou o HUD
        isBallInPlay = false;
        isServing = true;
        canCheckPoint = false; // Desativa a checagem de ponto durante a preparação do saque
        isPlayer1Serving = serveToPlayer1;
        currentSpeed = baseSpeed;
        currentSpin = 0f;
        currentRally = 0;

        if (ScoreboardHUD.Instance != null)
        {
            ScoreboardHUD.Instance.SetServer(serveToPlayer1);
            ScoreboardHUD.Instance.HideRallyImmediate();
        }

        // Garante que a bola fique visível por cima dos personagens no saque
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = 10;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (player1Transform != null)
        {
            PlayerController pc = player1Transform.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.ResetToServePosition(player1ServePosition);
            }
            else
            {
                player1Transform.position = player1ServePosition;
            }
        }

        if (player2Transform != null)
        {
            EnemyAIController ai = player2Transform.GetComponent<EnemyAIController>();
            if (ai != null)
            {
                ai.SetServeMode(!isPlayer1Serving, player2ServePosition);
            }
            else
            {
                player2Transform.position = player2ServePosition;
            }
        }

        if (!isPlayer1Serving)
        {
            Invoke(nameof(ExecuteServe), 1.2f);
        }

        isProcessingPoint = false;
        FollowServerPosition();
    }

    public void ExecuteServe()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameFinished) return;
        if (!isServing) return;

        isServing = false;
        currentSpin = 0f;
        isBallInPlay = true;
        currentSpeed = baseSpeed;

        if (isPlayer1Serving && player1Transform != null)
        {
            player1Transform.SendMessage("HitBall", SendMessageOptions.DontRequireReceiver);

            float serveAngleX = Random.Range(-0.06f, 0.06f);
            float horizontalInput = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(horizontalInput) > 0.1f)
            {
                serveAngleX = Mathf.Clamp(horizontalInput, -1f, 1f) * maxPlayerServeAngle;
                currentSpin = Mathf.Clamp(horizontalInput, -1f, 1f) * maxPlayerServeSpin;
            }
            else
            {
                currentSpin = 0f;
            }

            targetDirection = new Vector2(serveAngleX, 1f).normalized; // Envia a bola para frente na quadra
        }
        else if (!isPlayer1Serving && player2Transform != null)
        {
            EnemyAIController ai = player2Transform.GetComponent<EnemyAIController>();
            if (ai != null)
            {
                ai.HitBall();
            }
            targetDirection = new Vector2(Random.Range(-0.18f, 0.18f), -1f).normalized;
        }

        actualDirection = targetDirection;

        // Dispara o efeito sonoro da batida da raquete no saque (P1 ou IA)
        PlayRacketHitSound(!isPlayer1Serving);

        // Libera a validação de pontos 0.3s depois de a bola já ter entrado em movimento
        Invoke(nameof(EnablePointChecking), 0.3f);
    }

    private void EnablePointChecking()
    {
        canCheckPoint = true;
    }

    public void OnHitByPlayer(object[] data)
    {
        if (isServing || !isBallInPlay) return;
        // Evita rebatida dupla se a bola já estiver subindo
        if (targetDirection.y > 0.1f) return;

        Vector3 playerPos = (Vector3)data[0];
        Vector2 playerVelocity = (Vector2)data[1];

        currentSpin = Mathf.Clamp(playerVelocity.x, -1f, 1f);

        CalculateReturnDirection(transform.position, playerPos, 1f);
    }

    public void OnHitByPlayer(Vector3 playerPosition)
    {
        if (isServing || !isBallInPlay) return;
        if (targetDirection.y > 0.1f) return;

        currentSpin = 0f;
        CalculateReturnDirection(transform.position, playerPosition, 1f);
    }

    public void OnHitByEnemy(Vector3 enemyPosition)
    {
        if (isServing || !isBallInPlay) return;
        // Evita rebatida dupla se a bola já estiver descendo
        if (targetDirection.y < -0.1f) return;

        currentSpin = 0f;
        CalculateReturnDirection(transform.position, enemyPosition, -1f);
    }

    private void CalculateReturnDirection(Vector3 ballPos, Vector3 hitterPos, float targetYDirection)
    {
        // Clampa o desvio lateral para garantir que a bola siga sempre em direção ao campo adversário
        float rawOffset = (ballPos.x - hitterPos.x) * 0.75f;
        float xOffset = Mathf.Clamp(rawOffset, -1.15f, 1.15f);

        targetDirection = new Vector2(xOffset, targetYDirection).normalized;
        actualDirection = targetDirection;
        currentSpeed = Mathf.Min(currentSpeed + speedIncreasePerHit, maxSpeed);

        currentRally++;
        if (ScoreboardHUD.Instance != null)
        {
            ScoreboardHUD.Instance.OnRallyHit(currentRally, currentSpeed);
        }

        // Toca o efeito sonoro da batida da raquete na bola (Player ou Adversário)
        bool isEnemyHit = targetYDirection < 0f;
        PlayRacketHitSound(isEnemyHit);
    }

    /// <summary>
    /// Toca o efeito sonoro de impacto da raquete na bola com variação de tom orgânica e dinâmica de rally
    /// </summary>
    public void PlayRacketHitSound(bool isEnemyHit = false)
    {
        AudioClip clipToPlay = racketHitSound;
        if (clipToPlay == null)
        {
            if (AudioManager.Instance != null)
            {
                clipToPlay = AudioManager.Instance.GetRacketHitClip();
            }
            else
            {
                clipToPlay = AudioManager.GetOrCreateRetroRacketHitSound();
            }
        }

        if (clipToPlay == null) return;

        // Pitch dinâmico: ligeiramente mais agudo para o jogador e sobe sutilmente com a velocidade do rally
        float basePitch = isEnemyHit ? 0.98f : 1.02f;
        float rallyPitchBoost = dynamicRallyPitch ? Mathf.Clamp(currentRally * 0.012f, 0f, 0.20f) : 0f;
        float randomPitch = Random.Range(-0.04f, 0.04f);

        if (hitAudioSource != null)
        {
            hitAudioSource.pitch = Mathf.Clamp(basePitch + rallyPitchBoost + randomPitch, 0.7f, 1.4f);
            hitAudioSource.PlayOneShot(clipToPlay, hitSoundVolume);
        }
        else if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayRacketHit(rallyPitchBoost, hitSoundVolume);
        }
    }

    private void CheckOutOfBounds()
    {
        // Trava de segurança: NUNCA pontua durante o saque, antes de liberar canCheckPoint ou com o jogo finalizado
        if (GameManager.Instance != null && GameManager.Instance.IsGameFinished) return;
        if (isProcessingPoint || isServing || !canCheckPoint) return;

        if (transform.position.y > courtMaxY)
        {
            PointScored(true);
        }
        else if (transform.position.y < courtMinY)
        {
            PointScored(false);
        }
        else if (transform.position.x < courtMinX || transform.position.x > courtMaxX)
        {
            // Opção B: Divisão da quadra pela rede (netY)
            // Se a bola saiu pela lateral no campo adversário (já cruzou a rede),
            // considera-se uma bola angulada que o adversário não alcançou -> ponto do atacante (Winner)!
            // Se a bola saiu pela lateral no próprio campo de quem rebateu (antes da rede),
            // considera-se erro não forçado na rebatida -> ponto do oponente.
            bool isPlayer1Hitter = targetDirection.y > 0;
            bool crossedNet = isPlayer1Hitter ? (transform.position.y >= netY) : (transform.position.y <= netY);

            bool player1Scored = isPlayer1Hitter ? crossedNet : !crossedNet;
            PointScored(player1Scored);
        }
    }

    private void PointScored(bool player1Scored)
    {
        isProcessingPoint = true;
        canCheckPoint = false;

        if (ScoreboardHUD.Instance != null)
        {
            ScoreboardHUD.Instance.OnRallyEnd();
        }

        if (player1Scored)
        {
            Debug.Log("Ponto do Player 1!");

            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddPoint(1);
            }

            // Se o ponto gerou fim de jogo ou transição de nível, não reinicia saque imediatamente
            if (GameManager.Instance != null && (GameManager.Instance.IsGameFinished || GameManager.Instance.IsLevelTransitioning))
            {
                isServing = false;
                isBallInPlay = false;
                if (rb != null) rb.linearVelocity = Vector2.zero;
                return;
            }

            PrepareServe(true);
        }
        else
        {
            Debug.Log("Ponto da IA!");

            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddPoint(2);
            }

            // Se o ponto gerou fim de jogo ou transição de nível, não reinicia saque imediatamente
            if (GameManager.Instance != null && (GameManager.Instance.IsGameFinished || GameManager.Instance.IsLevelTransitioning))
            {
                isServing = false;
                isBallInPlay = false;
                if (rb != null) rb.linearVelocity = Vector2.zero;
                return;
            }

            PrepareServe(false);
        }
    }
}