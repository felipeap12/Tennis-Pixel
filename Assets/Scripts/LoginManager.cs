using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class LoginManager : MonoBehaviour
{
    public static LoginManager Instance { get; private set; }

    [Header("Botões do Menu (Opcionais - gerados automaticamente se nulos)")]
    public Button btnEntrar;
    public Button btnSair;

    [Header("Painel da Interface")]
    public GameObject loginPanel;
    public TextMeshProUGUI txtTitulo;
    public TMP_FontAsset fontAsset;

    [Header("Estilo Visual Retrô")]
    public bool autoFormatLayout = true;
    public Color backdropColor = new Color(0.04f, 0.07f, 0.12f, 0.90f);
    public Color cardBgColor = new Color(0.08f, 0.12f, 0.18f, 0.98f);
    public Color borderLimeColor = new Color(0.72f, 0.96f, 0.15f, 1f); // #B8F526
    public Color titleYellow = new Color(1f, 0.98f, 0.35f, 1f);
    public Color titleGreen = new Color(0.55f, 0.92f, 0.12f, 1f);

    [Header("Animação de Transição")]
    public float cardFadeDuration = 0.35f;
    public float bannerHoldDuration = 0.45f;
    public float bannerFadeDuration = 0.25f;
    public string matchStartText = "PREPARAR... SAQUE!";

    [Header("Áudio (Opcional)")]
    public AudioSource audioSource;
    public AudioClip clickSound;
    public AudioClip transitionSound;

    // Componentes internos de animação
    private Canvas rootCanvas;
    private RectTransform cardContainerRect;
    private CanvasGroup cardCanvasGroup;
    private RectTransform titleRect;
    private Vector2 titleOriginalPos;
    private Image btnEntrarImg;
    private Color btnEntrarOriginalColor = Color.white;

    // Overlay de transição
    private GameObject transitionOverlayObj;
    private CanvasGroup transitionCanvasGroup;
    private TextMeshProUGUI transitionBannerTmp;
    private RectTransform transitionBannerRect;

    private bool isStartingGame = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Congela o tempo do jogo enquanto o menu estiver ativo
        Time.timeScale = 0f;

        // Auto-detecta ou atribui o painel principal
        if (loginPanel == null)
            loginPanel = gameObject;

        rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null)
            rootCanvas = GetComponent<Canvas>();

        if (rootCanvas != null)
        {
            // Garante que o menu sempre renderize no topo de qualquer outro elemento
            rootCanvas.sortingOrder = 10;
        }

        // Tenta encontrar elementos legados caso existam na cena
        if (btnEntrar == null)
        {
            Transform tEntrar = transform.Find("Button_Entrar") ?? transform.Find("Button_Jogar");
            if (tEntrar != null) btnEntrar = tEntrar.GetComponent<Button>();
        }

        if (btnSair == null)
        {
            Transform tSair = transform.Find("Button_Sair");
            if (tSair != null) btnSair = tSair.GetComponent<Button>();
        }

        if (txtTitulo == null)
        {
            Transform tTitulo = transform.Find("Txt_Titulo");
            if (tTitulo != null) txtTitulo = tTitulo.GetComponent<TextMeshProUGUI>();
            else txtTitulo = GetComponentInChildren<TextMeshProUGUI>();
        }

        if (autoFormatLayout)
        {
            BuildModernRetroUI();
        }
    }

    void Start()
    {
        if (loginPanel != null)
        {
            loginPanel.SetActive(true);
        }

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

    void Update()
    {
        if (isStartingGame) return;

        // Efeito de flutuação suave do título (bobbing)
        if (titleRect != null)
        {
            float yOffset = Mathf.Sin(Time.unscaledTime * 3f) * 6f;
            titleRect.anchoredPosition = titleOriginalPos + new Vector2(0f, yOffset);
        }

        // Pulso sutil no botão JOGAR para chamar atenção
        if (btnEntrarImg != null)
        {
            float pulse = 0.95f + (Mathf.Sin(Time.unscaledTime * 4f) * 0.05f);
            Color c = btnEntrarOriginalColor * pulse;
            c.a = 1f;
            btnEntrarImg.color = c;
        }

        // Atalhos de teclado para iniciar a partida diretamente
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            EntrarNoJogo();
        }
    }

    /// <summary>
    /// Formata a interface com design retrô arcade, bordas neon e guia de controles 100% autônomo
    /// </summary>
    private void BuildModernRetroUI()
    {
        // 1. Fundo com escurecimento cinematográfico (Backdrop)
        Transform backdropTr = transform.Find("MenuBackdrop");
        GameObject backdropObj;
        if (backdropTr != null)
        {
            backdropObj = backdropTr.gameObject;
        }
        else
        {
            backdropObj = new GameObject("MenuBackdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            backdropObj.transform.SetParent(transform, false);
            backdropObj.transform.SetSiblingIndex(0);
        }

        RectTransform panelRect = backdropObj.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;
        panelRect.anchoredPosition = Vector2.zero;

        Image panelImage = backdropObj.GetComponent<Image>();
        panelImage.color = backdropColor;

        // 2. Cria ou localiza o Card Central (MenuCard)
        Transform existingCard = backdropObj.transform.Find("MenuCard");
        GameObject cardObj;
        if (existingCard != null)
        {
            cardObj = existingCard.gameObject;
        }
        else
        {
            cardObj = new GameObject("MenuCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            cardObj.transform.SetParent(backdropObj.transform, false);
        }

        cardContainerRect = cardObj.GetComponent<RectTransform>();
        cardContainerRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardContainerRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardContainerRect.pivot = new Vector2(0.5f, 0.5f);
        cardContainerRect.anchoredPosition = Vector2.zero;
        cardContainerRect.sizeDelta = new Vector2(680f, 560f);

        Image cardImg = cardObj.GetComponent<Image>();
        cardImg.sprite = CreateRoundedRectSprite(64, 64, 16, cardBgColor, borderLimeColor, 3);
        cardImg.type = Image.Type.Sliced;
        cardImg.color = Color.white;

        cardCanvasGroup = cardObj.GetComponent<CanvasGroup>();
        if (cardCanvasGroup == null) cardCanvasGroup = cardObj.AddComponent<CanvasGroup>();
        cardCanvasGroup.alpha = 1f;

        TMP_FontAsset baseFont = fontAsset != null ? fontAsset : (txtTitulo != null ? txtTitulo.font : TMP_Settings.defaultFontAsset);

        // 3. Estiliza / Cria o Título Principal
        if (txtTitulo == null)
        {
            Transform existingTitle = cardObj.transform.Find("Txt_Titulo");
            if (existingTitle != null)
            {
                txtTitulo = existingTitle.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                GameObject titleObj = new GameObject("Txt_Titulo", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                titleObj.transform.SetParent(cardObj.transform, false);
                txtTitulo = titleObj.GetComponent<TextMeshProUGUI>();
            }
        }
        else
        {
            txtTitulo.transform.SetParent(cardObj.transform, false);
        }

        titleRect = txtTitulo.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.localScale = Vector3.one;
        titleRect.sizeDelta = new Vector2(640f, 85f);
        titleOriginalPos = new Vector2(0f, 185f);
        titleRect.anchoredPosition = titleOriginalPos;

        if (baseFont != null) txtTitulo.font = baseFont;
        txtTitulo.text = "PIXEL TENNIS";
        txtTitulo.fontSize = 62;
        txtTitulo.fontStyle = FontStyles.Bold;
        txtTitulo.alignment = TextAlignmentOptions.Center;
        txtTitulo.enableVertexGradient = true;
        txtTitulo.colorGradient = new VertexGradient(titleYellow, titleYellow, titleGreen, titleGreen);

        // 4. Subtítulo estilo Arcade
        Transform subTr = cardObj.transform.Find("Txt_Subtitulo");
        TextMeshProUGUI txtSub;
        if (subTr == null)
        {
            GameObject subObj = new GameObject("Txt_Subtitulo", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            subObj.transform.SetParent(cardObj.transform, false);
            txtSub = subObj.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            txtSub = subTr.GetComponent<TextMeshProUGUI>();
        }

        RectTransform subRect = txtSub.rectTransform;
        subRect.anchorMin = new Vector2(0.5f, 0.5f);
        subRect.anchorMax = new Vector2(0.5f, 0.5f);
        subRect.pivot = new Vector2(0.5f, 0.5f);
        subRect.anchoredPosition = new Vector2(0f, 132f);
        subRect.sizeDelta = new Vector2(500f, 35f);
        subRect.localScale = Vector3.one;

        if (baseFont != null) txtSub.font = baseFont;
        txtSub.text = "★ VIRTUAL COURT CHAMPIONSHIP ★";
        txtSub.fontSize = 17;
        txtSub.characterSpacing = 3;
        txtSub.fontStyle = FontStyles.Bold;
        txtSub.alignment = TextAlignmentOptions.Center;
        txtSub.color = new Color(0.85f, 0.90f, 0.95f, 0.9f);

        // 5. Botão "JOGAR"
        if (btnEntrar == null)
        {
            Transform existingBtn = cardObj.transform.Find("Button_Jogar");
            if (existingBtn != null)
            {
                btnEntrar = existingBtn.GetComponent<Button>();
            }
            else
            {
                GameObject btnObj = new GameObject("Button_Jogar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                btnObj.transform.SetParent(cardObj.transform, false);
                btnEntrar = btnObj.GetComponent<Button>();
            }
        }
        else
        {
            btnEntrar.transform.SetParent(cardObj.transform, false);
        }

        RectTransform rEntrar = btnEntrar.GetComponent<RectTransform>();
        rEntrar.anchorMin = new Vector2(0.5f, 0.5f);
        rEntrar.anchorMax = new Vector2(0.5f, 0.5f);
        rEntrar.pivot = new Vector2(0.5f, 0.5f);
        rEntrar.localScale = Vector3.one;
        rEntrar.sizeDelta = new Vector2(340f, 64f);
        rEntrar.anchoredPosition = new Vector2(0f, 32f);

        btnEntrarImg = btnEntrar.GetComponent<Image>();
        if (btnEntrarImg != null)
        {
            btnEntrarImg.sprite = CreateRoundedRectSprite(48, 48, 12, borderLimeColor, Color.white, 2);
            btnEntrarImg.type = Image.Type.Sliced;
            btnEntrarOriginalColor = Color.white;
            btnEntrarImg.color = Color.white;
        }

        TextMeshProUGUI txtBtnEntrar = btnEntrar.GetComponentInChildren<TextMeshProUGUI>();
        if (txtBtnEntrar == null)
        {
            GameObject txtObj = new GameObject("Text (TMP)", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            txtObj.transform.SetParent(btnEntrar.transform, false);
            txtBtnEntrar = txtObj.GetComponent<TextMeshProUGUI>();
            RectTransform rTxt = txtBtnEntrar.rectTransform;
            rTxt.anchorMin = Vector2.zero;
            rTxt.anchorMax = Vector2.one;
            rTxt.sizeDelta = Vector2.zero;
            rTxt.anchoredPosition = Vector2.zero;
        }

        if (baseFont != null) txtBtnEntrar.font = baseFont;
        txtBtnEntrar.text = "JOGAR";
        txtBtnEntrar.fontSize = 28;
        txtBtnEntrar.fontStyle = FontStyles.Bold;
        txtBtnEntrar.color = new Color(0.06f, 0.14f, 0.04f, 1f);
        txtBtnEntrar.alignment = TextAlignmentOptions.Center;

        if (btnEntrar.GetComponent<MenuButtonHover>() == null)
        {
            btnEntrar.gameObject.AddComponent<MenuButtonHover>();
        }

        // 6. Botão "SAIR"
        if (btnSair == null)
        {
            Transform existingSair = cardObj.transform.Find("Button_Sair");
            if (existingSair != null)
            {
                btnSair = existingSair.GetComponent<Button>();
            }
            else
            {
                GameObject btnSairObj = new GameObject("Button_Sair", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                btnSairObj.transform.SetParent(cardObj.transform, false);
                btnSair = btnSairObj.GetComponent<Button>();
            }
        }
        else
        {
            btnSair.transform.SetParent(cardObj.transform, false);
        }

        RectTransform rSair = btnSair.GetComponent<RectTransform>();
        rSair.anchorMin = new Vector2(0.5f, 0.5f);
        rSair.anchorMax = new Vector2(0.5f, 0.5f);
        rSair.pivot = new Vector2(0.5f, 0.5f);
        rSair.localScale = Vector3.one;
        rSair.sizeDelta = new Vector2(240f, 46f);
        rSair.anchoredPosition = new Vector2(0f, -48f);

        Image imgSair = btnSair.GetComponent<Image>();
        if (imgSair != null)
        {
            imgSair.sprite = CreateRoundedRectSprite(48, 48, 10, new Color(0.12f, 0.17f, 0.24f, 0.95f), new Color(0.25f, 0.35f, 0.48f, 1f), 1);
            imgSair.type = Image.Type.Sliced;
            imgSair.color = Color.white;
        }

        TextMeshProUGUI txtBtnSair = btnSair.GetComponentInChildren<TextMeshProUGUI>();
        if (txtBtnSair == null)
        {
            GameObject txtObj = new GameObject("Text (TMP)", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            txtObj.transform.SetParent(btnSair.transform, false);
            txtBtnSair = txtObj.GetComponent<TextMeshProUGUI>();
            RectTransform rTxt = txtBtnSair.rectTransform;
            rTxt.anchorMin = Vector2.zero;
            rTxt.anchorMax = Vector2.one;
            rTxt.sizeDelta = Vector2.zero;
            rTxt.anchoredPosition = Vector2.zero;
        }

        if (baseFont != null) txtBtnSair.font = baseFont;
        txtBtnSair.text = "SAIR";
        txtBtnSair.fontSize = 20;
        txtBtnSair.color = new Color(0.75f, 0.82f, 0.90f, 1f);
        txtBtnSair.alignment = TextAlignmentOptions.Center;

        if (btnSair.GetComponent<MenuButtonHover>() == null)
        {
            btnSair.gameObject.AddComponent<MenuButtonHover>();
        }

        // 7. Painel de Instruções & Controles
        Transform ctrTr = cardObj.transform.Find("ControlsPanel");
        GameObject ctrObj;
        if (ctrTr == null)
        {
            ctrObj = new GameObject("ControlsPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ctrObj.transform.SetParent(cardObj.transform, false);
        }
        else
        {
            ctrObj = ctrTr.gameObject;
        }

        RectTransform ctrRect = ctrObj.GetComponent<RectTransform>();
        ctrRect.anchorMin = new Vector2(0.5f, 0.5f);
        ctrRect.anchorMax = new Vector2(0.5f, 0.5f);
        ctrRect.pivot = new Vector2(0.5f, 0.5f);
        ctrRect.anchoredPosition = new Vector2(0f, -165f);
        ctrRect.sizeDelta = new Vector2(580f, 96f);

        Image ctrImg = ctrObj.GetComponent<Image>();
        ctrImg.sprite = CreateRoundedRectSprite(48, 48, 12, new Color(0.05f, 0.08f, 0.13f, 0.85f), new Color(0.2f, 0.28f, 0.38f, 0.8f), 1);
        ctrImg.type = Image.Type.Sliced;
        ctrImg.color = Color.white;

        Transform ctrTextTr = ctrObj.transform.Find("Txt_Controls");
        TextMeshProUGUI txtControls;
        if (ctrTextTr == null)
        {
            GameObject txtObj = new GameObject("Txt_Controls", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            txtObj.transform.SetParent(ctrObj.transform, false);
            txtControls = txtObj.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            txtControls = ctrTextTr.GetComponent<TextMeshProUGUI>();
        }

        RectTransform rControlsText = txtControls.rectTransform;
        rControlsText.anchorMin = Vector2.zero;
        rControlsText.anchorMax = Vector2.one;
        rControlsText.sizeDelta = new Vector2(-24f, -14f);
        rControlsText.anchoredPosition = Vector2.zero;

        if (baseFont != null) txtControls.font = baseFont;
        txtControls.text = "<b><color=#72F526>CONTROLES</color></b>\n<color=#E2E8F0>[W][A][S][D] / [SETAS]</color> Mover   •   <color=#E2E8F0>[ESPAÇO]</color> Sacar & Rebater\n<color=#FACC15>⚡ Dica: Movimente-se ao rebater para aplicar EFEITO (Spin)!</color>";
        txtControls.fontSize = 15;
        txtControls.lineSpacing = 10;
        txtControls.alignment = TextAlignmentOptions.Center;

        // 8. Cria o Overlay de Transição com o banner "PREPARAR... SAQUE!"
        BuildTransitionOverlay(backdropObj.transform, baseFont);
    }

    /// <summary>
    /// Constrói o overlay de tela cheia responsável pela animação de transição quando o jogo inicia
    /// </summary>
    private void BuildTransitionOverlay(Transform parent, TMP_FontAsset baseFont)
    {
        Transform existingOverlay = parent.Find("TransitionOverlay");
        if (existingOverlay != null)
        {
            transitionOverlayObj = existingOverlay.gameObject;
        }
        else
        {
            transitionOverlayObj = new GameObject("TransitionOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            transitionOverlayObj.transform.SetParent(parent, false);
        }

        RectTransform rOverlay = transitionOverlayObj.GetComponent<RectTransform>();
        rOverlay.anchorMin = Vector2.zero;
        rOverlay.anchorMax = Vector2.one;
        rOverlay.sizeDelta = Vector2.zero;
        rOverlay.anchoredPosition = Vector2.zero;

        Image imgOverlay = transitionOverlayObj.GetComponent<Image>();
        imgOverlay.color = new Color(0.03f, 0.05f, 0.09f, 0.95f);
        imgOverlay.raycastTarget = false;

        transitionCanvasGroup = transitionOverlayObj.GetComponent<CanvasGroup>();
        if (transitionCanvasGroup == null) transitionCanvasGroup = transitionOverlayObj.AddComponent<CanvasGroup>();
        transitionCanvasGroup.alpha = 0f;
        transitionCanvasGroup.blocksRaycasts = false;

        Transform bannerTr = transitionOverlayObj.transform.Find("MatchStartBanner");
        if (bannerTr != null)
        {
            transitionBannerTmp = bannerTr.GetComponent<TextMeshProUGUI>();
            transitionBannerRect = bannerTr.GetComponent<RectTransform>();
        }
        else
        {
            GameObject bannerObj = new GameObject("MatchStartBanner", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            bannerObj.transform.SetParent(transitionOverlayObj.transform, false);
            transitionBannerTmp = bannerObj.GetComponent<TextMeshProUGUI>();
            transitionBannerRect = bannerObj.GetComponent<RectTransform>();
        }

        transitionBannerRect.anchorMin = new Vector2(0.5f, 0.5f);
        transitionBannerRect.anchorMax = new Vector2(0.5f, 0.5f);
        transitionBannerRect.pivot = new Vector2(0.5f, 0.5f);
        transitionBannerRect.anchoredPosition = Vector2.zero;
        transitionBannerRect.sizeDelta = new Vector2(800f, 120f);

        if (baseFont != null) transitionBannerTmp.font = baseFont;
        transitionBannerTmp.text = matchStartText;
        transitionBannerTmp.fontSize = 58;
        transitionBannerTmp.fontStyle = FontStyles.Bold;
        transitionBannerTmp.alignment = TextAlignmentOptions.Center;
        transitionBannerTmp.enableVertexGradient = true;
        transitionBannerTmp.colorGradient = new VertexGradient(titleYellow, titleYellow, titleGreen, titleGreen);

        transitionOverlayObj.SetActive(false);
    }

    /// <summary>
    /// Dispara a transição animada e inicia a partida oficialmente
    /// </summary>
    public void EntrarNoJogo()
    {
        if (isStartingGame) return;
        isStartingGame = true;

        Debug.Log("[Menu] Iniciando transição para a partida...");

        // Desativa a interação dos botões para evitar múltiplos cliques
        if (btnEntrar != null) btnEntrar.interactable = false;
        if (btnSair != null) btnSair.interactable = false;

        if (audioSource != null && clickSound != null)
        {
            audioSource.PlayOneShot(clickSound);
        }

        StartCoroutine(StartGameTransitionRoutine());
    }

    /// <summary>
    /// Corrotina que executa a animação de saída do menu e o aviso de início de partida
    /// </summary>
    private IEnumerator StartGameTransitionRoutine()
    {
        // Fase 1: Suavemente escala e desaparece o card central do menu
        if (cardCanvasGroup != null)
        {
            float elapsed = 0f;
            Vector3 startScale = cardContainerRect != null ? cardContainerRect.localScale : Vector3.one;
            Vector3 targetScale = startScale * 1.08f;

            while (elapsed < cardFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / cardFadeDuration);
                float easeOut = Mathf.Sin(t * Mathf.PI * 0.5f);

                cardCanvasGroup.alpha = Mathf.Lerp(1f, 0f, easeOut);
                if (cardContainerRect != null)
                {
                    cardContainerRect.localScale = Vector3.Lerp(startScale, targetScale, easeOut);
                }
                yield return null;
            }
            cardCanvasGroup.alpha = 0f;
        }

        // Fase 2: Exibe o overlay cinematográfico com o banner 'PREPARAR... SAQUE!'
        if (transitionOverlayObj != null && transitionCanvasGroup != null)
        {
            transitionOverlayObj.SetActive(true);
            transitionCanvasGroup.blocksRaycasts = true;

            if (audioSource != null && transitionSound != null)
            {
                audioSource.PlayOneShot(transitionSound);
            }

            // Animação de entrada do banner com impacto retrô (punch in)
            float elapsed = 0f;
            float punchInDuration = 0.25f;
            while (elapsed < punchInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / punchInDuration);

                // Efeito elástico (bounce) de escala: 0.4x -> 1.15x -> 1.0x
                float scaleValue = Mathf.Lerp(0.4f, 1.12f, Mathf.Sin(t * Mathf.PI * 0.5f));
                if (t >= 0.8f)
                {
                    scaleValue = Mathf.Lerp(1.12f, 1.0f, (t - 0.8f) / 0.2f);
                }

                transitionCanvasGroup.alpha = Mathf.Clamp01(t * 1.5f);
                if (transitionBannerRect != null)
                {
                    transitionBannerRect.localScale = new Vector3(scaleValue, scaleValue, 1f);
                }
                yield return null;
            }

            if (transitionBannerRect != null) transitionBannerRect.localScale = Vector3.one;
            transitionCanvasGroup.alpha = 1f;

            // Mantém o banner visível momentaneamente
            yield return new WaitForSecondsRealtime(bannerHoldDuration);

            // Fase 3: Fade-out do overlay revelando a quadra em ação
            elapsed = 0f;
            while (elapsed < bannerFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / bannerFadeDuration);
                transitionCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
                yield return null;
            }

            transitionCanvasGroup.alpha = 0f;
            transitionOverlayObj.SetActive(false);
        }

        // Fase 4: Restaura a velocidade normal do jogo e inicia a partida
        Time.timeScale = 1f;

        if (loginPanel != null)
        {
            loginPanel.SetActive(false);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartGameFromMenu();
        }
    }

    /// <summary>
    /// Fecha o jogo (executável) ou encerra o modo Play no Unity Editor
    /// </summary>
    public void SairDoJogo()
    {
        Debug.Log("[Menu] Saindo do jogo...");

        if (audioSource != null && clickSound != null)
        {
            audioSource.PlayOneShot(clickSound);
        }

        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    /// <summary>
    /// Reabre o menu principal (usado após a tela final de vitória ou retorno manual)
    /// </summary>
    public void ShowMenu()
    {
        isStartingGame = false;

        if (loginPanel != null)
        {
            loginPanel.SetActive(true);
        }

        if (cardCanvasGroup != null)
        {
            cardCanvasGroup.alpha = 1f;
        }

        if (cardContainerRect != null)
        {
            cardContainerRect.localScale = Vector3.one;
        }

        if (transitionOverlayObj != null)
        {
            transitionOverlayObj.SetActive(false);
            if (transitionCanvasGroup != null)
            {
                transitionCanvasGroup.alpha = 0f;
                transitionCanvasGroup.blocksRaycasts = false;
            }
        }

        if (btnEntrar != null) btnEntrar.interactable = true;
        if (btnSair != null) btnSair.interactable = true;

        Time.timeScale = 0f;
    }

    /// <summary>
    /// Gera um Sprite procedural 9-slice com cantos arredondados e borda estilizada
    /// </summary>
    public static Sprite CreateRoundedRectSprite(int width, int height, int cornerRadius, Color fillColor, Color borderColor, int borderWidth)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color transparent = new Color(0, 0, 0, 0);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = Mathf.Max(0, cornerRadius - x, x - (width - 1 - cornerRadius));
                float dy = Mathf.Max(0, cornerRadius - y, y - (height - 1 - cornerRadius));
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist > cornerRadius)
                {
                    tex.SetPixel(x, y, transparent);
                }
                else if (borderWidth > 0 && (dist > cornerRadius - borderWidth || x < borderWidth || x >= width - borderWidth || y < borderWidth || y >= height - borderWidth))
                {
                    tex.SetPixel(x, y, borderColor);
                }
                else
                {
                    tex.SetPixel(x, y, fillColor);
                }
            }
        }
        tex.Apply();

        return Sprite.Create(
            tex,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius)
        );
    }
}

/// <summary>
/// Efeito de escala e feedback suave ao passar o mouse ou clicar nos botões do menu
/// </summary>
public class MenuButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public Vector3 normalScale = Vector3.one;
    public Vector3 hoverScale = new Vector3(1.05f, 1.05f, 1f);
    public Vector3 pressScale = new Vector3(0.95f, 0.95f, 1f);
    public float transitionSpeed = 14f;

    private Vector3 targetScale;

    void Awake()
    {
        targetScale = normalScale;
    }

    void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * transitionSpeed);
    }

    public void OnPointerEnter(PointerEventData eventData) => targetScale = hoverScale;
    public void OnPointerExit(PointerEventData eventData) => targetScale = normalScale;
    public void OnPointerDown(PointerEventData eventData) => targetScale = pressScale;
    public void OnPointerUp(PointerEventData eventData) => targetScale = hoverScale;

    void OnDisable()
    {
        targetScale = normalScale;
        transform.localScale = normalScale;
    }
}
