using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Referências dos Jogadores e Bola")]
    public PlayerController playerController;
    public EnemyAIController aiController;
    public Ball ballScript;

    [Header("Nível e Graduação (1 a 5)")]
    public int currentLevel = 1;
    public string[] levelGraduations = new string[]
    {
        "Iniciante",
        "Amador",
        "Intermediário",
        "Avançado",
        "Profissional"
    };

    [Header("Placar Atual - Pontos do Set")]
    private int player1Points = 0;
    private int player2Points = 0;

    [Header("Placar Atual - Sets Vencidos")]
    private int player1Sets = 0;
    private int player2Sets = 0;

    [Header("Interface de Usuário (UI TextMeshPro)")]
    public TextMeshProUGUI txtPlayer1Points;
    public TextMeshProUGUI txtPlayer2Points;
    public TextMeshProUGUI txtPlayer1Sets;
    public TextMeshProUGUI txtPlayer2Sets;
    public TextMeshProUGUI txtGraduation;
    public TextMeshProUGUI txtMatchStatus;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // Configura o nível base e zera a UI
        ApplyGlobalLevel(currentLevel);
        ResetMatch();
        // NOTA: O saque não é disparado aqui, permitindo que a tela de menu/login trave o jogo antes de iniciar.
    }

    /// <summary>
    /// Método chamado pelo LoginManager para dar início oficial à partida
    /// </summary>
    public void StartGameFromMenu()
    {
        ResetMatch();

        if (ballScript != null)
        {
            ballScript.PrepareServe(true); // Prepara o primeiro saque para o Player 1
        }
    }

    public void AddPoint(int winner)
    {
        if (winner == 1)
            player1Points++;
        else if (winner == 2)
            player2Points++;

        CheckSetWinner();
        UpdateUI();
    }

    private void CheckSetWinner()
    {
        if (player1Points >= 5 && player1Points - player2Points >= 2)
        {
            player1Sets++;
            ResetSetPoints();
            CheckMatchWinner();
        }
        else if (player2Points >= 5 && player2Points - player1Points >= 2)
        {
            player2Sets++;
            ResetSetPoints();
            CheckMatchWinner();
        }
    }

    private void CheckMatchWinner()
    {
        if (player1Sets >= 2)
        {
            if (txtMatchStatus != null)
                txtMatchStatus.text = "VOCÊ VENCEU A PARTIDA!";

            if (currentLevel < 5)
            {
                currentLevel++;
                ApplyGlobalLevel(currentLevel);
            }

            Invoke(nameof(ResetMatchAndServe), 3.5f);
        }
        else if (player2Sets >= 2)
        {
            if (txtMatchStatus != null)
                txtMatchStatus.text = "A I.A. VENCEU A PARTIDA!";

            Invoke(nameof(ResetMatchAndServe), 3.5f);
        }
    }

    private void ResetMatchAndServe()
    {
        ResetMatch();
        if (ballScript != null)
        {
            ballScript.PrepareServe(true);
        }
    }

    private void ResetSetPoints()
    {
        player1Points = 0;
        player2Points = 0;
    }

    private void ResetMatch()
    {
        ResetSetPoints();
        player1Sets = 0;
        player2Sets = 0;
        if (txtMatchStatus != null) txtMatchStatus.text = "";
        UpdateUI();
    }

    /// <summary>
    /// Notifica a IA, o Player e a Bola para atualizarem os seus próprios atributos ao mudar de nível
    /// </summary>
    public void ApplyGlobalLevel(int level)
    {
        currentLevel = Mathf.Clamp(level, 1, 5);

        if (aiController != null)
        {
            aiController.ApplyLevel(currentLevel);
        }

        if (playerController != null)
        {
            playerController.ApplyLevel(currentLevel);
        }

        if (ballScript != null)
        {
            ballScript.ApplyLevel(currentLevel);
        }

        UpdateUI();
        Debug.Log($"[GameManager] Novo Nível Global: {currentLevel} ({levelGraduations[currentLevel - 1]})");
    }

    private void UpdateUI()
    {
        if (txtPlayer1Points != null) txtPlayer1Points.text = $"P1: {player1Points}";
        if (txtPlayer2Points != null) txtPlayer2Points.text = $"IA: {player2Points}";
        if (txtPlayer1Sets != null) txtPlayer1Sets.text = $"Sets P1: {player1Sets}";
        if (txtPlayer2Sets != null) txtPlayer2Sets.text = $"Sets IA: {player2Sets}";
        if (txtGraduation != null) txtGraduation.text = $"Graduação: {levelGraduations[currentLevel - 1]} (Nível {currentLevel})";
    }
}