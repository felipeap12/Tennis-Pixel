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
    public int player1Points = 0;
    public int player2Points = 0;

    [Header("Placar Atual - Sets Vencidos")]
    public int player1Sets = 0;
    public int player2Sets = 0;

    [Header("Interface de Usuário (Compatibilidade TMP)")]
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

        EnsureScoreboardHUD();
    }

    void Start()
    {
        // Configura o nível base e zera a UI
        ApplyGlobalLevel(currentLevel);
        ResetMatch();
        // NOTA: O primeiro saque é disparado pelo botão de entrar do Menu (LoginManager)
    }

    /// <summary>
    /// Garante que o ScoreboardHUD moderno esteja ativo e anexado ao Canvas da partida
    /// </summary>
    private void EnsureScoreboardHUD()
    {
        if (ScoreboardHUD.Instance == null)
        {
            Canvas[] canvases = FindObjectsByType<Canvas>();
            foreach (var c in canvases)
            {
                if (c.name == "Canvas")
                {
                    if (c.GetComponent<ScoreboardHUD>() == null)
                    {
                        c.gameObject.AddComponent<ScoreboardHUD>();
                    }
                    break;
                }
            }
        }
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

        NotifyPointAnnouncements(winner);
        CheckSetWinner();
        UpdateUI();
    }

    private void NotifyPointAnnouncements(int winner)
    {
        if (ScoreboardHUD.Instance == null) return;

        if (winner == 1)
        {
            // Checa Match Point
            if (player1Sets == 1 && player1Points >= 4 && player1Points > player2Points)
            {
                ScoreboardHUD.Instance.ShowAnnouncement("🔥 MATCH POINT (VOCÊ)!", new Color(1f, 0.45f, 0.2f, 1f), 1.5f);
            }
            // Checa Set Point
            else if (player1Points >= 4 && player1Points > player2Points)
            {
                ScoreboardHUD.Instance.ShowAnnouncement("⚠️ SET POINT (VOCÊ)!", ScoreboardHUD.Instance.goldColor, 1.3f);
            }
            // Checa Deuce / Vantagem
            else if (player1Points >= 3 && player2Points >= 3 && player1Points == player2Points)
            {
                ScoreboardHUD.Instance.ShowAnnouncement("DEUCE (IGUAIS)!", ScoreboardHUD.Instance.goldColor, 1.2f);
            }
            else if (player1Points >= 4 && player1Points == player2Points + 1)
            {
                ScoreboardHUD.Instance.ShowAnnouncement("VANTAGEM VOCÊ!", ScoreboardHUD.Instance.p1Color, 1.2f);
            }
            else
            {
                ScoreboardHUD.Instance.ShowAnnouncement("PONTO DO PLAYER 1!", ScoreboardHUD.Instance.p1Color, 1.0f);
            }
        }
        else if (winner == 2)
        {
            // Checa Match Point da IA
            if (player2Sets == 1 && player2Points >= 4 && player2Points > player1Points)
            {
                ScoreboardHUD.Instance.ShowAnnouncement("🔥 MATCH POINT (I.A.)!", new Color(1f, 0.35f, 0.35f, 1f), 1.5f);
            }
            // Checa Set Point da IA
            else if (player2Points >= 4 && player2Points > player1Points)
            {
                ScoreboardHUD.Instance.ShowAnnouncement("⚠️ SET POINT (I.A.)!", new Color(0.98f, 0.5f, 0.2f, 1f), 1.3f);
            }
            // Checa Deuce / Vantagem
            else if (player1Points >= 3 && player2Points >= 3 && player1Points == player2Points)
            {
                ScoreboardHUD.Instance.ShowAnnouncement("DEUCE (IGUAIS)!", ScoreboardHUD.Instance.goldColor, 1.2f);
            }
            else if (player2Points >= 4 && player2Points == player1Points + 1)
            {
                ScoreboardHUD.Instance.ShowAnnouncement("VANTAGEM I.A.!", ScoreboardHUD.Instance.p2Color, 1.2f);
            }
            else
            {
                ScoreboardHUD.Instance.ShowAnnouncement("PONTO DA I.A.!", ScoreboardHUD.Instance.p2Color, 1.0f);
            }
        }
    }

    private void CheckSetWinner()
    {
        if (player1Points >= 5 && player1Points - player2Points >= 2)
        {
            player1Sets++;
            if (ScoreboardHUD.Instance != null)
            {
                ScoreboardHUD.Instance.ShowAnnouncement("🎉 SET PARA VOCÊ!", ScoreboardHUD.Instance.goldColor, 2.2f);
            }

            ResetSetPoints();
            CheckMatchWinner();
        }
        else if (player2Points >= 5 && player2Points - player1Points >= 2)
        {
            player2Sets++;
            if (ScoreboardHUD.Instance != null)
            {
                ScoreboardHUD.Instance.ShowAnnouncement("SET PARA A I.A.!", ScoreboardHUD.Instance.p2Color, 2.2f);
            }

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

            if (ScoreboardHUD.Instance != null)
            {
                ScoreboardHUD.Instance.ShowAnnouncement("🏆 VOCÊ VENCEU A PARTIDA!", ScoreboardHUD.Instance.goldColor, 3.5f);
            }

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

            if (ScoreboardHUD.Instance != null)
            {
                ScoreboardHUD.Instance.ShowAnnouncement("💔 A I.A. VENCEU A PARTIDA!", ScoreboardHUD.Instance.p2Color, 3.5f);
            }

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

        if (ScoreboardHUD.Instance != null)
        {
            ScoreboardHUD.Instance.SetLevel(currentLevel, levelGraduations[currentLevel - 1]);
        }

        UpdateUI();
        Debug.Log($"[GameManager] Novo Nível Global: {currentLevel} ({levelGraduations[currentLevel - 1]})");
    }

    private void UpdateUI()
    {
        // Atualiza o novo HUD moderno com animação e formatação de tênis
        if (ScoreboardHUD.Instance != null)
        {
            ScoreboardHUD.Instance.UpdateScores(player1Points, player2Points, player1Sets, player2Sets);
        }

        // Mantém referências legadas sincronizadas para total compatibilidade
        if (txtPlayer1Points != null) txtPlayer1Points.text = $"P1: {player1Points}";
        if (txtPlayer2Points != null) txtPlayer2Points.text = $"IA: {player2Points}";
        if (txtPlayer1Sets != null) txtPlayer1Sets.text = $"Sets P1: {player1Sets}";
        if (txtPlayer2Sets != null) txtPlayer2Sets.text = $"Sets IA: {player2Sets}";
        if (txtGraduation != null) txtGraduation.text = $"Graduação: {levelGraduations[currentLevel - 1]} (Nível {currentLevel})";
    }
}
