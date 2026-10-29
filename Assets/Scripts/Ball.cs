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

    [Header("Posições de Saque (Linha de Fundo)")]
    public Vector3 player1ServePosition = new Vector3(0f, -0.65f, 0f);
    public Vector3 player2ServePosition = new Vector3(0f, 0.65f, 0f);

    [Header("Ajuste de Distância da Bola no Saque")]
    public Vector3 p1BallOffset = new Vector3(0.08f, 0.25f, 0f);  // Ligeiramente acima do P1
    public Vector3 p2BallOffset = new Vector3(-0.08f, -0.25f, 0f); // Ligeiramente abaixo da IA

    [Header("Referências dos Jogadores")]
    public Transform player1Transform;
    public Transform player2Transform;

    private bool isBallInPlay = false;
    private bool isServing = false;
    private bool isPlayer1Serving = true;
    private bool isProcessingPoint = false;
    private bool canCheckPoint = false; // Trava para evitar falso ponto ao sacar

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;

    public bool IsRallyActive => isBallInPlay && !isServing && !isProcessingPoint;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

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
        if (isServing)
        {
            FollowServerPosition();

            if (isPlayer1Serving && Input.GetKeyDown(KeyCode.Space))
            {
                ExecuteServe();
            }
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
            transform.Translate(actualDirection * currentSpeed * Time.deltaTime);
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

        isBallInPlay = false;
        isServing = true;
        canCheckPoint = false; // Desativa a checagem de ponto durante a preparação do saque
        isPlayer1Serving = serveToPlayer1;
        currentSpeed = baseSpeed;
        currentSpin = 0f;

        // Garante que a bola fique visível por cima dos personagens no saque
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = 10;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (isPlayer1Serving && player1Transform != null)
        {
            player1Transform.position = player1ServePosition;
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
        if (!isServing) return;

        isServing = false;
        currentSpin = 0f;

        if (isPlayer1Serving && player1Transform != null)
        {
            player1Transform.SendMessage("HitBall", SendMessageOptions.DontRequireReceiver);
            targetDirection = new Vector2(Random.Range(-0.2f, 0.2f), 1f).normalized; // Envia a bola para frente na quadra
        }
        else if (!isPlayer1Serving && player2Transform != null)
        {
            EnemyAIController ai = player2Transform.GetComponent<EnemyAIController>();
            if (ai != null)
            {
                ai.HitBall();
            }
            targetDirection = new Vector2(Random.Range(-0.2f, 0.2f), -1f).normalized;
        }

        actualDirection = targetDirection;
        currentSpeed = baseSpeed;
        isBallInPlay = true;

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

        Vector3 playerPos = (Vector3)data[0];
        Vector2 playerVelocity = (Vector2)data[1];

        currentSpin = Mathf.Clamp(playerVelocity.x, -1f, 1f);

        CalculateReturnDirection(transform.position, playerPos, 1f);
    }

    public void OnHitByPlayer(Vector3 playerPosition)
    {
        if (isServing || !isBallInPlay) return;
        currentSpin = 0f;
        CalculateReturnDirection(transform.position, playerPosition, 1f);
    }

    public void OnHitByEnemy(Vector3 enemyPosition)
    {
        if (isServing || !isBallInPlay) return;
        currentSpin = 0f;
        CalculateReturnDirection(transform.position, enemyPosition, -1f);
    }

    private void CalculateReturnDirection(Vector3 ballPos, Vector3 hitterPos, float targetYDirection)
    {
        float xOffset = (ballPos.x - hitterPos.x) * 0.8f;
        targetDirection = new Vector2(xOffset, targetYDirection).normalized;
        actualDirection = targetDirection;
        currentSpeed = Mathf.Min(currentSpeed + speedIncreasePerHit, maxSpeed);
    }

    private void CheckOutOfBounds()
    {
        // Trava de segurança: NUNCA pontua durante o saque ou antes de liberar canCheckPoint
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
            bool pointForPlayer1 = targetDirection.y > 0;
            PointScored(pointForPlayer1);
        }
    }

    private void PointScored(bool player1Scored)
    {
        isProcessingPoint = true;
        canCheckPoint = false;

        if (player1Scored)
        {
            Debug.Log("Ponto do Player 1!");

            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddPoint(1);
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

            PrepareServe(false);
        }
    }
}