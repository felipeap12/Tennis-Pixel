using UnityEngine;
using UnityEngine.UI;

public class LoginManager : MonoBehaviour
{
    [Header("Botões do Menu")]
    public Button btnEntrar;
    public Button btnSair;

    [Header("Painel da Interface")]
    public GameObject loginPanel; // O Canvas ou Panel principal do Menu

    void Awake()
    {
        // Congela o tempo do jogo assim que a cena carrega para a bola e IA não rodarem
        Time.timeScale = 0f;
    }

    void Start()
    {
        // Garante que o menu está visível ao abrir o jogo
        if (loginPanel != null)
        {
            loginPanel.SetActive(true);
        }

        // Associa os botões às funções
        if (btnEntrar != null)
        {
            btnEntrar.onClick.RemoveAllListeners();
            btnEntrar.onClick.AddListener(EntrarNoJogo);
        }

        if (btnSair != null)
        {
            btnSair.onClick.RemoveAllListeners();
            btnSair.onClick.AddListener(SairDoJogo);
        }
    }

    /// <summary>
    /// Esconde a tela de login, restaura o tempo e inicia o jogo
    /// </summary>
    public void EntrarNoJogo()
    {
        Debug.Log("[Menu] Fechando login e iniciando partida...");

        // 1. Esconde a tela de Login/Menu imediatamente
        if (loginPanel != null)
        {
            loginPanel.SetActive(false);
        }

        // 2. Restaura o tempo do jogo para normal
        Time.timeScale = 1f;

        // 3. Notifica o GameManager para dar o primeiro saque
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartGameFromMenu();
        }
    }

    /// <summary>
    /// Encerra a aplicação
    /// </summary>
    public void SairDoJogo()
    {
        Debug.Log("[Menu] Saindo do jogo...");

        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}