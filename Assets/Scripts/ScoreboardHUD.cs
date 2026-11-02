using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ScoreboardHUD : MonoBehaviour
{
    public static ScoreboardHUD Instance { get; private set; }

    [Header("Configuração Geral")]
    public bool autoBuildUI = true;
    public bool useTennisNotation = true; // Exibe 00, 15, 30, 40, AD (padrão oficial de tênis)
    public TMP_FontAsset customFont;

    [Header("Dimensões do Placar")]
    public float cardWidth = 450f;
    public float cardHeight = 182f;
    public float pointsBoxWidth = 58f;
    public float pointsBoxHeight = 38f;
    public float setsBoxWidth = 44f;
    public float setsBoxHeight = 38f;

    [Header("Cores e Identidade Visual (Paleta Arcade Retrô)")]
    public Color cardBgColor = new Color(0.04f, 0.07f, 0.12f, 0.92f);      // Dark slate glassmorphism
    public Color cardBorderColor = new Color(0.18f, 0.28f, 0.42f, 0.95f);  // Cyan slate border
    public Color p1Color = new Color(0.52f, 0.95f, 0.15f, 1f);              // Neon Lime (#85F226)
    public Color p1ScoreBg = new Color(0.06f, 0.18f, 0.10f, 0.95f);
    public Color p2Color = new Color(0.96f, 0.28f, 0.38f, 1f);              // Coral Crimson (#F54761)
    public Color p2ScoreBg = new Color(0.20f, 0.06f, 0.08f, 0.95f);
    public Color serveBallColor = new Color(0.98f, 0.92f, 0.22f, 1f);       // Tennis Ball Yellow
    public Color goldColor = new Color(0.98f, 0.82f, 0.25f, 1f);            // Trophy Gold
    public Color textWhite = new Color(0.95f, 0.97f, 1f, 1f);
    public Color textMuted = new Color(0.55f, 0.65f, 0.78f, 1f);

    [Header("Áudio Opcional")]
    public AudioSource audioSource;
    public AudioClip pointSound;
    public AudioClip cheerSound;
    public AudioClip alertSound;

    [Header("Transição de Nível")]
    public float levelTransitionHoldDuration = 1.8f;
    public float levelTransitionFadeDuration = 0.25f;
    public AudioClip levelUpSound;

    // Componentes Visuais
    private Canvas rootCanvas;
    private RectTransform scoreboardCardRect;
    private TextMeshProUGUI txtTournamentTitle;
    private TextMeshProUGUI txtHeaderSets;
    private TextMeshProUGUI txtHeaderPoints;

    // Player 1 UI
    private TextMeshProUGUI txtP1Name;
    private TextMeshProUGUI txtP1Sets;
    private TextMeshProUGUI txtP1Points;
    private RectTransform p1PointsBoxRect;
    private GameObject p1ServeIcon;

    // Player 2 UI (IA)
    private TextMeshProUGUI txtP2Name;
    private TextMeshProUGUI txtP2Sets;
    private TextMeshProUGUI txtP2Points;
    private RectTransform p2PointsBoxRect;
    private GameObject p2ServeIcon;

    // Badge de Nível / Graduação
    private RectTransform levelBadgeRect;
    private TextMeshProUGUI txtLevelBadge;

    // Widget de Rally & Velocidade
    private GameObject rallyWidgetObj;
    private RectTransform rallyWidgetRect;
    private CanvasGroup rallyCanvasGroup;
    private TextMeshProUGUI txtRallyInfo;

    // Banner Central de Anúncios (Point, Deuce, Set Point, Match Point, Vitória)
    private GameObject bannerObj;
    private RectTransform bannerRect;
    private CanvasGroup bannerCanvasGroup;
    private TextMeshProUGUI txtBanner;
    private Image bannerBgImg;
    private Coroutine currentBannerRoutine;

    // Overlay de Transição de Nível
    private GameObject levelTransitionOverlayObj;
    private CanvasGroup levelTransitionCanvasGroup;
    private RectTransform levelTransitionCardRect;
    private Image levelTransitionCardImg;
    private TextMeshProUGUI txtTransitionTag;
    private TextMeshProUGUI txtTransitionTitle;
    private TextMeshProUGUI txtTransitionSubtitle;
    private TextMeshProUGUI txtTransitionPrompt;
    private Coroutine currentLevelTransitionRoutine;
    private bool isTransitioningLevel = false;
    public bool IsTransitioningLevel => isTransitioningLevel;

    // Dica de Controles
    private GameObject controlsHintObj;
    private CanvasGroup controlsHintCanvasGroup;

    // Cache interno de dados
    private int cachedP1Points = 0;
    private int cachedP2Points = 0;
    private int cachedP1Sets = 0;
    private int cachedP2Sets = 0;
    private int cachedLevel = 1;
    private string cachedGraduation = "Iniciante";
    private bool cachedIsP1Serving = true;

    // Sprites procedurais cacheados
    private Sprite roundedCardSprite;
    private Sprite scoreBoxSprite;
    private Sprite pillBadgeSprite;
    private Sprite bannerSprite;
    private Sprite circleSprite;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }

        rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null)
        {
            rootCanvas = GetComponent<Canvas>();
        }

        if (autoBuildUI)
        {
            BuildScoreboardHUD();
        }
    }

    void Start()
    {
        // Garante referência de áudio
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        }

        if (levelUpSound == null)
        {
            LoginManager lm = FindAnyObjectByType<LoginManager>();
            if (lm != null && lm.transitionSound != null)
            {
                levelUpSound = lm.transitionSound;
            }
        }

        // Se o GameManager já estiver pronto, atualiza o placar inicial
        if (GameManager.Instance != null)
        {
            cachedLevel = GameManager.Instance.currentLevel;
            if (GameManager.Instance.levelGraduations != null && cachedLevel <= GameManager.Instance.levelGraduations.Length)
            {
                cachedGraduation = GameManager.Instance.levelGraduations[cachedLevel - 1];
            }
        }

        UpdateScoreboardVisuals();
        SetServer(true);
        HideRallyImmediate();

        // Faz o fade out gradual da dica de controles após 8 segundos
        StartCoroutine(FadeOutControlsHintRoutine());
    }

    /// <summary>
    /// Constrói e configura dinamicamente todo o HUD moderno com escala responsiva e estética de transmissão esportiva
    /// </summary>
    public void BuildScoreboardHUD()
    {
        if (rootCanvas == null)
        {
            rootCanvas = gameObject.GetComponent<Canvas>();
            if (rootCanvas == null) rootCanvas = gameObject.AddComponent<Canvas>();
        }

        // 1. Configura o CanvasScaler para ScaleWithScreenSize (1080p referência)
        CanvasScaler scaler = rootCanvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = rootCanvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // 2. Oculta quaisquer textos antigos espalhados na cena para não conflitar
        HideOldScorePlaceholders();

        // 3. Carrega a melhor fonte disponível
        TMP_FontAsset baseFont = customFont;
        if (baseFont == null)
        {
            baseFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }
        if (baseFont == null)
        {
            baseFont = TMP_Settings.defaultFontAsset;
        }

        // 4. Gera sprites 9-slice procedurais de alta nitidez
        CreateProceduralSprites();

        // 5. Constrói o Card Principal do Placar (Top-Left)
        BuildMainScoreboardCard(rootCanvas.transform, baseFont);

        // 6. Constrói o Badge de Nível e Graduação (Abaixo do Card)
        BuildLevelBadge(rootCanvas.transform, baseFont);

        // 7. Constrói o Widget de Rally (Top-Right)
        BuildRallyWidget(rootCanvas.transform, baseFont);

        // 8. Constrói o Banner Central de Anúncios (Top-Center)
        BuildAnnouncementBanner(rootCanvas.transform, baseFont);

        // 9. Constrói a Barra Discreta de Dica de Controles (Bottom-Right)
        BuildControlsHint(rootCanvas.transform, baseFont);

        // 10. Constrói o Overlay de Transição de Nível (Centro Cinematográfico)
        BuildLevelTransitionOverlay(rootCanvas.transform, baseFont);
    }

    private void HideOldScorePlaceholders()
    {
        string[] oldNames = { "PontosP1", "PontosP2", "SetP1", "SetP2", "txtPlayer1Points", "txtPlayer2Points" };
        foreach (var oldName in oldNames)
        {
            Transform t = rootCanvas.transform.Find(oldName);
            if (t != null)
            {
                t.gameObject.SetActive(false);
            }
        }
    }

    private void CreateProceduralSprites()
    {
        roundedCardSprite = LoginManager.CreateRoundedRectSprite(64, 64, 14, cardBgColor, cardBorderColor, 2);
        scoreBoxSprite = LoginManager.CreateRoundedRectSprite(48, 48, 10, new Color(0.08f, 0.12f, 0.18f, 0.95f), new Color(0.25f, 0.38f, 0.52f, 1f), 2);
        pillBadgeSprite = LoginManager.CreateRoundedRectSprite(32, 32, 10, new Color(0.07f, 0.10f, 0.16f, 0.90f), new Color(0.22f, 0.32f, 0.45f, 0.8f), 1);
        bannerSprite = LoginManager.CreateRoundedRectSprite(64, 64, 16, new Color(0.06f, 0.09f, 0.15f, 0.95f), new Color(0.52f, 0.95f, 0.15f, 1f), 3);
        circleSprite = CreateCircleSprite(32, serveBallColor);
    }

    #region Construção de Elementos Visuais

    private void BuildMainScoreboardCard(Transform parent, TMP_FontAsset font)
    {
        Transform existing = parent.Find("ScoreboardCard");
        GameObject cardObj;
        if (existing != null)
        {
            cardObj = existing.gameObject;
        }
        else
        {
            cardObj = new GameObject("ScoreboardCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            cardObj.transform.SetParent(parent, false);
        }

        scoreboardCardRect = cardObj.GetComponent<RectTransform>();
        scoreboardCardRect.anchorMin = new Vector2(0f, 1f);
        scoreboardCardRect.anchorMax = new Vector2(0f, 1f);
        scoreboardCardRect.pivot = new Vector2(0f, 1f);
        scoreboardCardRect.anchoredPosition = new Vector2(36f, -36f);
        scoreboardCardRect.sizeDelta = new Vector2(cardWidth, cardHeight);

        Image cardImg = cardObj.GetComponent<Image>();
        cardImg.sprite = roundedCardSprite;
        cardImg.type = Image.Type.Sliced;
        cardImg.color = Color.white;

        // Cálculos matemáticos precisos de alinhamento das colunas:
        // A linha do jogador tem padding lateral de 12px em cada lado (largura útil = cardWidth - 24px)
        // Box_Points: pos.x = -12px, largura = pointsBoxWidth (58px).
        // Centro de Pontos em relação à borda direita do card: -(12 + 12 + pointsBoxWidth / 2) = -53px
        float rowInset = 12f;
        float pointsRightEdgeInsideRow = 12f;
        float pointsCenterFromCardRight = -(rowInset + pointsRightEdgeInsideRow + (pointsBoxWidth * 0.5f));

        // Box_Sets: fica à esquerda de Box_Points com 12px de vão
        float gapBetweenBoxes = 12f;
        float setsRightEdgeInsideRow = pointsRightEdgeInsideRow + pointsBoxWidth + gapBetweenBoxes;
        float setsCenterFromCardRight = -(rowInset + setsRightEdgeInsideRow + (setsBoxWidth * 0.5f));

        // 1. Título do Header (lado esquerdo)
        GameObject headerTitleObj = new GameObject("CardHeader_Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        headerTitleObj.transform.SetParent(cardObj.transform, false);
        RectTransform headerTitleRect = headerTitleObj.GetComponent<RectTransform>();
        headerTitleRect.anchorMin = new Vector2(0f, 1f);
        headerTitleRect.anchorMax = new Vector2(0f, 1f);
        headerTitleRect.pivot = new Vector2(0f, 1f);
        headerTitleRect.anchoredPosition = new Vector2(16f, -8f);
        headerTitleRect.sizeDelta = new Vector2(250f, 24f);

        txtTournamentTitle = headerTitleObj.GetComponent<TextMeshProUGUI>();
        if (font != null) txtTournamentTitle.font = font;
        txtTournamentTitle.fontSize = 12.5f;
        txtTournamentTitle.fontStyle = FontStyles.Bold;
        txtTournamentTitle.color = goldColor;
        // Sem emojis incompatíveis com TMP: tipografia retrô limpa e nítida
        txtTournamentTitle.text = "<color=#FACC15>PIXEL TENNIS</color>  <color=#475569>|</color>  <color=#38BDF8>MELHOR DE 3 SETS</color>";
        txtTournamentTitle.alignment = TextAlignmentOptions.Left;

        // 2. Cabeçalho de Coluna: SETS (100% alinhado ao centro da caixa de Sets)
        GameObject colSetsObj = new GameObject("Header_Sets", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        colSetsObj.transform.SetParent(cardObj.transform, false);
        RectTransform colSetsRect = colSetsObj.GetComponent<RectTransform>();
        colSetsRect.anchorMin = new Vector2(1f, 1f);
        colSetsRect.anchorMax = new Vector2(1f, 1f);
        colSetsRect.pivot = new Vector2(0.5f, 1f);
        colSetsRect.anchoredPosition = new Vector2(setsCenterFromCardRight, -9f);
        colSetsRect.sizeDelta = new Vector2(setsBoxWidth + 6f, 22f);

        txtHeaderSets = colSetsObj.GetComponent<TextMeshProUGUI>();
        if (font != null) txtHeaderSets.font = font;
        txtHeaderSets.fontSize = 11f;
        txtHeaderSets.fontStyle = FontStyles.Bold;
        txtHeaderSets.color = textMuted;
        txtHeaderSets.alignment = TextAlignmentOptions.Center;
        txtHeaderSets.text = "SETS";

        // 3. Cabeçalho de Coluna: PONTOS (100% alinhado ao centro da caixa de Pontos)
        GameObject colPointsObj = new GameObject("Header_Points", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        colPointsObj.transform.SetParent(cardObj.transform, false);
        RectTransform colPointsRect = colPointsObj.GetComponent<RectTransform>();
        colPointsRect.anchorMin = new Vector2(1f, 1f);
        colPointsRect.anchorMax = new Vector2(1f, 1f);
        colPointsRect.pivot = new Vector2(0.5f, 1f);
        colPointsRect.anchoredPosition = new Vector2(pointsCenterFromCardRight, -9f);
        colPointsRect.sizeDelta = new Vector2(pointsBoxWidth + 8f, 22f);

        txtHeaderPoints = colPointsObj.GetComponent<TextMeshProUGUI>();
        if (font != null) txtHeaderPoints.font = font;
        txtHeaderPoints.fontSize = 11f;
        txtHeaderPoints.fontStyle = FontStyles.Bold;
        txtHeaderPoints.color = textMuted;
        txtHeaderPoints.alignment = TextAlignmentOptions.Center;
        txtHeaderPoints.text = "PONTOS";

        // Linha divisória do Header
        BuildDividerLine(cardObj.transform, new Vector2(0f, -32f), new Vector2(-24f, 2f), new Color(0.2f, 0.3f, 0.45f, 0.6f));

        // Linha do Player 1 (VOCÊ)
        BuildPlayerRow(cardObj.transform, font, 1, new Vector2(0f, -38f), setsRightEdgeInsideRow, pointsRightEdgeInsideRow,
            out txtP1Name, out txtP1Sets, out txtP1Points, out p1PointsBoxRect, out p1ServeIcon);

        // Linha divisória entre os jogadores
        BuildDividerLine(cardObj.transform, new Vector2(0f, -106f), new Vector2(-28f, 1f), new Color(0.18f, 0.25f, 0.38f, 0.5f));

        // Linha do Player 2 (I.A. RIVAL)
        BuildPlayerRow(cardObj.transform, font, 2, new Vector2(0f, -112f), setsRightEdgeInsideRow, pointsRightEdgeInsideRow,
            out txtP2Name, out txtP2Sets, out txtP2Points, out p2PointsBoxRect, out p2ServeIcon);
    }

    private void BuildPlayerRow(Transform parent, TMP_FontAsset font, int playerIndex, Vector2 anchoredPos, float setsRightEdge, float pointsRightEdge,
        out TextMeshProUGUI txtName, out TextMeshProUGUI txtSets, out TextMeshProUGUI txtPoints, out RectTransform pointsBoxRect, out GameObject serveIcon)
    {
        bool isP1 = playerIndex == 1;
        Color accentColor = isP1 ? p1Color : p2Color;
        Color scoreBoxBg = isP1 ? p1ScoreBg : p2ScoreBg;
        string defaultName = isP1 ? "VOCÊ (P1)" : "I.A. RIVAL";

        GameObject rowObj = new GameObject(isP1 ? "Row_P1" : "Row_P2", typeof(RectTransform));
        rowObj.transform.SetParent(parent, false);
        RectTransform rowRect = rowObj.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.anchoredPosition = anchoredPos;
        rowRect.sizeDelta = new Vector2(-24f, 60f);

        // 1. Tag lateral colorida
        GameObject tagObj = new GameObject("AccentTag", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        tagObj.transform.SetParent(rowObj.transform, false);
        RectTransform tagRect = tagObj.GetComponent<RectTransform>();
        tagRect.anchorMin = new Vector2(0f, 0.5f);
        tagRect.anchorMax = new Vector2(0f, 0.5f);
        tagRect.pivot = new Vector2(0f, 0.5f);
        tagRect.anchoredPosition = new Vector2(4f, 0f);
        tagRect.sizeDelta = new Vector2(4f, 34f);
        Image tagImg = tagObj.GetComponent<Image>();
        tagImg.color = accentColor;

        // 2. Indicador de Saque (Bolinha luminosa)
        serveIcon = new GameObject("ServeIndicator", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        serveIcon.transform.SetParent(rowObj.transform, false);
        RectTransform serveRect = serveIcon.GetComponent<RectTransform>();
        serveRect.anchorMin = new Vector2(0f, 0.5f);
        serveRect.anchorMax = new Vector2(0f, 0.5f);
        serveRect.pivot = new Vector2(0.5f, 0.5f);
        serveRect.anchoredPosition = new Vector2(20f, 0f);
        serveRect.sizeDelta = new Vector2(14f, 14f);
        Image serveImg = serveIcon.GetComponent<Image>();
        serveImg.sprite = circleSprite;
        serveImg.color = serveBallColor;

        // 3. Nome do Jogador
        GameObject nameObj = new GameObject("Txt_Name", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        nameObj.transform.SetParent(rowObj.transform, false);
        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 0.5f);
        nameRect.anchorMax = new Vector2(0f, 0.5f);
        nameRect.pivot = new Vector2(0f, 0.5f);
        nameRect.anchoredPosition = new Vector2(36f, 0f);
        nameRect.sizeDelta = new Vector2(240f, 34f);
        txtName = nameObj.GetComponent<TextMeshProUGUI>();
        if (font != null) txtName.font = font;
        txtName.fontSize = 19f;
        txtName.fontStyle = FontStyles.Bold;
        txtName.color = textWhite;
        txtName.text = defaultName;
        txtName.alignment = TextAlignmentOptions.Left;

        // 4. Caixa de Sets (Mais compacta e perfeitamente alinhada)
        GameObject setsBox = new GameObject("Box_Sets", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        setsBox.transform.SetParent(rowObj.transform, false);
        RectTransform setsRect = setsBox.GetComponent<RectTransform>();
        setsRect.anchorMin = new Vector2(1f, 0.5f);
        setsRect.anchorMax = new Vector2(1f, 0.5f);
        setsRect.pivot = new Vector2(1f, 0.5f);
        setsRect.anchoredPosition = new Vector2(-setsRightEdge, 0f);
        setsRect.sizeDelta = new Vector2(setsBoxWidth, setsBoxHeight);
        Image setsImg = setsBox.GetComponent<Image>();
        setsImg.sprite = pillBadgeSprite;
        setsImg.type = Image.Type.Sliced;
        setsImg.color = Color.white;

        GameObject setsTextObj = new GameObject("Txt_Sets", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        setsTextObj.transform.SetParent(setsBox.transform, false);
        RectTransform setsTextRect = setsTextObj.GetComponent<RectTransform>();
        setsTextRect.anchorMin = Vector2.zero;
        setsTextRect.anchorMax = Vector2.one;
        setsTextRect.sizeDelta = Vector2.zero;
        txtSets = setsTextObj.GetComponent<TextMeshProUGUI>();
        if (font != null) txtSets.font = font;
        txtSets.fontSize = 20f;
        txtSets.fontStyle = FontStyles.Bold;
        txtSets.color = textWhite;
        txtSets.alignment = TextAlignmentOptions.Center;
        txtSets.text = "0";

        // 5. Caixa de Pontos (Tamanho e texto ajustados com elegância)
        GameObject ptsBox = new GameObject("Box_Points", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        ptsBox.transform.SetParent(rowObj.transform, false);
        pointsBoxRect = ptsBox.GetComponent<RectTransform>();
        pointsBoxRect.anchorMin = new Vector2(1f, 0.5f);
        pointsBoxRect.anchorMax = new Vector2(1f, 0.5f);
        pointsBoxRect.pivot = new Vector2(1f, 0.5f);
        pointsBoxRect.anchoredPosition = new Vector2(-pointsRightEdge, 0f);
        pointsBoxRect.sizeDelta = new Vector2(pointsBoxWidth, pointsBoxHeight);
        Image ptsImg = ptsBox.GetComponent<Image>();
        ptsImg.sprite = LoginManager.CreateRoundedRectSprite(48, 48, 10, scoreBoxBg, accentColor, 2);
        ptsImg.type = Image.Type.Sliced;
        ptsImg.color = Color.white;

        GameObject ptsTextObj = new GameObject("Txt_Points", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        ptsTextObj.transform.SetParent(ptsBox.transform, false);
        RectTransform ptsTextRect = ptsTextObj.GetComponent<RectTransform>();
        ptsTextRect.anchorMin = Vector2.zero;
        ptsTextRect.anchorMax = Vector2.one;
        ptsTextRect.sizeDelta = Vector2.zero;
        txtPoints = ptsTextObj.GetComponent<TextMeshProUGUI>();
        if (font != null) txtPoints.font = font;
        txtPoints.fontSize = 20f; // Tamanho reduzido para melhor proporção
        txtPoints.fontStyle = FontStyles.Bold;
        txtPoints.color = accentColor;
        txtPoints.alignment = TextAlignmentOptions.Center;
        txtPoints.text = "00";
    }

    private void BuildLevelBadge(Transform parent, TMP_FontAsset font)
    {
        Transform existing = parent.Find("LevelBadge");
        GameObject badgeObj;
        if (existing != null)
        {
            badgeObj = existing.gameObject;
        }
        else
        {
            badgeObj = new GameObject("LevelBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            badgeObj.transform.SetParent(parent, false);
        }

        levelBadgeRect = badgeObj.GetComponent<RectTransform>();
        levelBadgeRect.anchorMin = new Vector2(0f, 1f);
        levelBadgeRect.anchorMax = new Vector2(0f, 1f);
        levelBadgeRect.pivot = new Vector2(0f, 1f);
        levelBadgeRect.anchoredPosition = new Vector2(36f, -226f);
        // Largura alargada para casar 100% com a largura do ScoreboardCard
        levelBadgeRect.sizeDelta = new Vector2(cardWidth, 38f);

        Image img = badgeObj.GetComponent<Image>();
        img.sprite = LoginManager.CreateRoundedRectSprite(48, 48, 10, new Color(0.04f, 0.07f, 0.12f, 0.90f), new Color(0.95f, 0.75f, 0.20f, 0.85f), 1);
        img.type = Image.Type.Sliced;
        img.color = Color.white;

        GameObject txtObj = new GameObject("Txt_Level", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        txtObj.transform.SetParent(badgeObj.transform, false);
        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.sizeDelta = new Vector2(-28f, 0f);

        txtLevelBadge = txtObj.GetComponent<TextMeshProUGUI>();
        if (font != null) txtLevelBadge.font = font;
        txtLevelBadge.fontSize = 13.5f;
        txtLevelBadge.fontStyle = FontStyles.Bold;
        txtLevelBadge.alignment = TextAlignmentOptions.Left;
        // Tipografia limpa sem emojis incompatíveis
        txtLevelBadge.text = $"<color=#FACC15>NÍVEL {cachedLevel}</color>   <color=#475569>•</color>   <color=#94A3B8>GRADUAÇÃO: </color><color=#F8FAFC>{cachedGraduation.ToUpper()}</color>";
    }

    private void BuildRallyWidget(Transform parent, TMP_FontAsset font)
    {
        Transform existing = parent.Find("RallyWidget");
        if (existing != null)
        {
            rallyWidgetObj = existing.gameObject;
        }
        else
        {
            rallyWidgetObj = new GameObject("RallyWidget", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            rallyWidgetObj.transform.SetParent(parent, false);
        }

        rallyWidgetRect = rallyWidgetObj.GetComponent<RectTransform>();
        rallyWidgetRect.anchorMin = new Vector2(1f, 1f);
        rallyWidgetRect.anchorMax = new Vector2(1f, 1f);
        rallyWidgetRect.pivot = new Vector2(1f, 1f);
        rallyWidgetRect.anchoredPosition = new Vector2(-36f, -36f);
        rallyWidgetRect.sizeDelta = new Vector2(300f, 54f);

        Image img = rallyWidgetObj.GetComponent<Image>();
        img.sprite = LoginManager.CreateRoundedRectSprite(48, 48, 12, new Color(0.05f, 0.08f, 0.14f, 0.92f), new Color(0.98f, 0.55f, 0.15f, 0.9f), 2);
        img.type = Image.Type.Sliced;
        img.color = Color.white;

        rallyCanvasGroup = rallyWidgetObj.GetComponent<CanvasGroup>();
        if (rallyCanvasGroup == null) rallyCanvasGroup = rallyWidgetObj.AddComponent<CanvasGroup>();
        rallyCanvasGroup.alpha = 0f;

        GameObject txtObj = new GameObject("Txt_Rally", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        txtObj.transform.SetParent(rallyWidgetObj.transform, false);
        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.sizeDelta = Vector2.zero;

        txtRallyInfo = txtObj.GetComponent<TextMeshProUGUI>();
        if (font != null) txtRallyInfo.font = font;
        txtRallyInfo.fontSize = 17f;
        txtRallyInfo.fontStyle = FontStyles.Bold;
        txtRallyInfo.color = new Color(1f, 0.85f, 0.35f, 1f);
        txtRallyInfo.alignment = TextAlignmentOptions.Center;
        txtRallyInfo.text = "RALLY: 0   <color=#64748B>•</color>   VELOCIDADE: 5.0 m/s";

        rallyWidgetObj.SetActive(false);
    }

    private void BuildAnnouncementBanner(Transform parent, TMP_FontAsset font)
    {
        Transform existing = parent.Find("AnnouncementBanner");
        if (existing != null)
        {
            bannerObj = existing.gameObject;
        }
        else
        {
            bannerObj = new GameObject("AnnouncementBanner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            bannerObj.transform.SetParent(parent, false);
        }

        bannerRect = bannerObj.GetComponent<RectTransform>();
        bannerRect.anchorMin = new Vector2(0.5f, 1f);
        bannerRect.anchorMax = new Vector2(0.5f, 1f);
        bannerRect.pivot = new Vector2(0.5f, 1f);
        bannerRect.anchoredPosition = new Vector2(0f, -48f);
        bannerRect.sizeDelta = new Vector2(580f, 68f);

        bannerBgImg = bannerObj.GetComponent<Image>();
        bannerBgImg.sprite = bannerSprite;
        bannerBgImg.type = Image.Type.Sliced;
        bannerBgImg.color = Color.white;

        bannerCanvasGroup = bannerObj.GetComponent<CanvasGroup>();
        if (bannerCanvasGroup == null) bannerCanvasGroup = bannerObj.AddComponent<CanvasGroup>();
        bannerCanvasGroup.alpha = 0f;

        GameObject txtObj = new GameObject("Txt_Banner", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        txtObj.transform.SetParent(bannerObj.transform, false);
        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.sizeDelta = Vector2.zero;

        txtBanner = txtObj.GetComponent<TextMeshProUGUI>();
        if (font != null) txtBanner.font = font;
        txtBanner.fontSize = 26f;
        txtBanner.fontStyle = FontStyles.Bold;
        txtBanner.color = Color.white;
        txtBanner.alignment = TextAlignmentOptions.Center;
        txtBanner.text = "PONTO DO PLAYER 1!";

        bannerObj.SetActive(false);
    }

    private void BuildControlsHint(Transform parent, TMP_FontAsset font)
    {
        Transform existing = parent.Find("ControlsHint");
        if (existing != null)
        {
            controlsHintObj = existing.gameObject;
        }
        else
        {
            controlsHintObj = new GameObject("ControlsHint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            controlsHintObj.transform.SetParent(parent, false);
        }

        RectTransform r = controlsHintObj.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(1f, 0f);
        r.anchorMax = new Vector2(1f, 0f);
        r.pivot = new Vector2(1f, 0f);
        r.anchoredPosition = new Vector2(-28f, 24f);
        r.sizeDelta = new Vector2(460f, 36f);

        Image img = controlsHintObj.GetComponent<Image>();
        img.sprite = LoginManager.CreateRoundedRectSprite(32, 32, 8, new Color(0.04f, 0.07f, 0.12f, 0.75f), new Color(0.2f, 0.3f, 0.4f, 0.6f), 1);
        img.type = Image.Type.Sliced;
        img.color = Color.white;

        controlsHintCanvasGroup = controlsHintObj.GetComponent<CanvasGroup>();
        if (controlsHintCanvasGroup == null) controlsHintCanvasGroup = controlsHintObj.AddComponent<CanvasGroup>();
        controlsHintCanvasGroup.alpha = 0.85f;

        GameObject txtObj = new GameObject("Txt_Hint", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        txtObj.transform.SetParent(controlsHintObj.transform, false);
        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI t = txtObj.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = 13f;
        t.fontStyle = FontStyles.Normal;
        t.color = new Color(0.75f, 0.85f, 0.95f, 0.9f);
        t.alignment = TextAlignmentOptions.Center;
        t.text = "<b><color=#72F526>[ESPAÇO]</color></b> Sacar & Rebater   <color=#64748B>•</color>   <b><color=#72F526>[W][A][S][D]</color></b> Mover";
    }

    private void BuildLevelTransitionOverlay(Transform parent, TMP_FontAsset font)
    {
        Transform existingOverlay = parent.Find("LevelTransitionOverlay");
        if (existingOverlay != null)
        {
            levelTransitionOverlayObj = existingOverlay.gameObject;
        }
        else
        {
            levelTransitionOverlayObj = new GameObject("LevelTransitionOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            levelTransitionOverlayObj.transform.SetParent(parent, false);
        }

        RectTransform rOverlay = levelTransitionOverlayObj.GetComponent<RectTransform>();
        rOverlay.anchorMin = Vector2.zero;
        rOverlay.anchorMax = Vector2.one;
        rOverlay.pivot = new Vector2(0.5f, 0.5f);
        rOverlay.sizeDelta = Vector2.zero;
        rOverlay.anchoredPosition = Vector2.zero;

        Image imgOverlay = levelTransitionOverlayObj.GetComponent<Image>();
        imgOverlay.color = new Color(0.02f, 0.04f, 0.08f, 0.94f);
        imgOverlay.raycastTarget = false;

        levelTransitionCanvasGroup = levelTransitionOverlayObj.GetComponent<CanvasGroup>();
        if (levelTransitionCanvasGroup == null) levelTransitionCanvasGroup = levelTransitionOverlayObj.AddComponent<CanvasGroup>();
        levelTransitionCanvasGroup.alpha = 0f;
        levelTransitionCanvasGroup.blocksRaycasts = false;

        // Card Central de Transição
        Transform cardTr = levelTransitionOverlayObj.transform.Find("TransitionCard");
        GameObject cardObj;
        if (cardTr != null)
        {
            cardObj = cardTr.gameObject;
        }
        else
        {
            cardObj = new GameObject("TransitionCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            cardObj.transform.SetParent(levelTransitionOverlayObj.transform, false);
        }

        levelTransitionCardRect = cardObj.GetComponent<RectTransform>();
        levelTransitionCardRect.anchorMin = new Vector2(0.5f, 0.5f);
        levelTransitionCardRect.anchorMax = new Vector2(0.5f, 0.5f);
        levelTransitionCardRect.pivot = new Vector2(0.5f, 0.5f);
        levelTransitionCardRect.anchoredPosition = Vector2.zero;
        levelTransitionCardRect.sizeDelta = new Vector2(660f, 250f);

        levelTransitionCardImg = cardObj.GetComponent<Image>();
        levelTransitionCardImg.sprite = LoginManager.CreateRoundedRectSprite(64, 64, 16, cardBgColor, goldColor, 3);
        levelTransitionCardImg.type = Image.Type.Sliced;
        levelTransitionCardImg.color = Color.white;

        // 1. Tag Superior Pill
        GameObject tagObj = new GameObject("Tag_Pill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        tagObj.transform.SetParent(cardObj.transform, false);
        RectTransform tagRect = tagObj.GetComponent<RectTransform>();
        tagRect.anchorMin = new Vector2(0.5f, 1f);
        tagRect.anchorMax = new Vector2(0.5f, 1f);
        tagRect.pivot = new Vector2(0.5f, 1f);
        tagRect.anchoredPosition = new Vector2(0f, -18f);
        tagRect.sizeDelta = new Vector2(320f, 28f);

        Image tagImg = tagObj.GetComponent<Image>();
        tagImg.sprite = pillBadgeSprite;
        tagImg.type = Image.Type.Sliced;

        GameObject tagTxtObj = new GameObject("Txt_Tag", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        tagTxtObj.transform.SetParent(tagObj.transform, false);
        RectTransform tagTxtRect = tagTxtObj.GetComponent<RectTransform>();
        tagTxtRect.anchorMin = Vector2.zero;
        tagTxtRect.anchorMax = Vector2.one;
        tagTxtRect.sizeDelta = Vector2.zero;
        tagTxtRect.anchoredPosition = Vector2.zero;

        txtTransitionTag = tagTxtObj.GetComponent<TextMeshProUGUI>();
        if (font != null) txtTransitionTag.font = font;
        txtTransitionTag.fontSize = 12.5f;
        txtTransitionTag.fontStyle = FontStyles.Bold;
        txtTransitionTag.alignment = TextAlignmentOptions.Center;
        txtTransitionTag.color = goldColor;
        txtTransitionTag.text = "★ NOVO NÍVEL ALCANÇADO ★";

        // 2. Título do Nível (Em destaque retrô arcade com gradiente neon)
        GameObject titleObj = new GameObject("Txt_Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(cardObj.transform, false);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -52f);
        titleRect.sizeDelta = new Vector2(640f, 58f);

        txtTransitionTitle = titleObj.GetComponent<TextMeshProUGUI>();
        if (font != null) txtTransitionTitle.font = font;
        txtTransitionTitle.fontSize = 50f;
        txtTransitionTitle.fontStyle = FontStyles.Bold;
        txtTransitionTitle.alignment = TextAlignmentOptions.Center;
        txtTransitionTitle.enableVertexGradient = true;
        txtTransitionTitle.colorGradient = new VertexGradient(
            new Color(1f, 0.95f, 0.35f, 1f),
            new Color(1f, 0.95f, 0.35f, 1f),
            new Color(0.52f, 0.95f, 0.15f, 1f),
            new Color(0.52f, 0.95f, 0.15f, 1f)
        );
        txtTransitionTitle.text = "NÍVEL 2";

        // 3. Subtítulo (Nome da Graduação)
        GameObject subObj = new GameObject("Txt_Subtitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        subObj.transform.SetParent(cardObj.transform, false);
        RectTransform subRect = subObj.GetComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0.5f, 1f);
        subRect.anchorMax = new Vector2(0.5f, 1f);
        subRect.pivot = new Vector2(0.5f, 1f);
        subRect.anchoredPosition = new Vector2(0f, -114f);
        subRect.sizeDelta = new Vector2(640f, 32f);

        txtTransitionSubtitle = subObj.GetComponent<TextMeshProUGUI>();
        if (font != null) txtTransitionSubtitle.font = font;
        txtTransitionSubtitle.fontSize = 22f;
        txtTransitionSubtitle.fontStyle = FontStyles.Bold;
        txtTransitionSubtitle.alignment = TextAlignmentOptions.Center;
        txtTransitionSubtitle.color = new Color(0.22f, 0.74f, 0.97f, 1f);
        txtTransitionSubtitle.text = "GRADUAÇÃO: AMADOR";

        // Divisória sutil
        BuildDividerLine(cardObj.transform, new Vector2(0f, -152f), new Vector2(-120f, 1.5f), new Color(0.2f, 0.3f, 0.45f, 0.5f));

        // 4. Chamada Inferior (PREPARAR... SAQUE!)
        GameObject promptObj = new GameObject("Txt_Prompt", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        promptObj.transform.SetParent(cardObj.transform, false);
        RectTransform promptRect = promptObj.GetComponent<RectTransform>();
        promptRect.anchorMin = new Vector2(0.5f, 0f);
        promptRect.anchorMax = new Vector2(0.5f, 0f);
        promptRect.pivot = new Vector2(0.5f, 0f);
        promptRect.anchoredPosition = new Vector2(0f, 16f);
        promptRect.sizeDelta = new Vector2(640f, 54f);

        txtTransitionPrompt = promptObj.GetComponent<TextMeshProUGUI>();
        if (font != null) txtTransitionPrompt.font = font;
        txtTransitionPrompt.fontSize = 21f;
        txtTransitionPrompt.fontStyle = FontStyles.Bold;
        txtTransitionPrompt.alignment = TextAlignmentOptions.Center;
        txtTransitionPrompt.text = "<b><color=#72F526>PREPARAR... SAQUE!</color></b>\n<size=12><color=#94A3B8>Pressione [ESPAÇO] para iniciar ou aguarde...</color></size>";

        levelTransitionOverlayObj.SetActive(false);
    }

    private void BuildDividerLine(Transform parent, Vector2 anchoredPos, Vector2 sizeDelta, Color color)
    {
        GameObject divObj = new GameObject("Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        divObj.transform.SetParent(parent, false);
        RectTransform r = divObj.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, 1f);
        r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.anchoredPosition = anchoredPos;
        r.sizeDelta = sizeDelta;
        Image img = divObj.GetComponent<Image>();
        img.color = color;
    }

    private Sprite CreateCircleSprite(int size, Color color)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        float radius = (size - 1) * 0.5f;
        Vector2 center = new Vector2(radius, radius);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist <= radius - 1f)
                {
                    tex.SetPixel(x, y, color);
                }
                else if (dist <= radius)
                {
                    float alpha = Mathf.Clamp01(radius - dist);
                    tex.SetPixel(x, y, new Color(color.r, color.g, color.b, alpha));
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    #endregion

    #region Atualizações de Estado e Animações

    /// <summary>
    /// Notificação disparada quando os pontos mudam no GameManager
    /// </summary>
    public void UpdateScores(int p1Points, int p2Points, int p1Sets, int p2Sets, int winnerLastPoint = 0)
    {
        bool p1Scored = p1Points > cachedP1Points;
        bool p2Scored = p2Points > cachedP2Points;

        cachedP1Points = p1Points;
        cachedP2Points = p2Points;
        cachedP1Sets = p1Sets;
        cachedP2Sets = p2Sets;

        UpdateScoreboardVisuals();

        // Animação de impacto (punch-scale) na caixa de quem pontuou
        if (p1Scored && p1PointsBoxRect != null)
        {
            StartCoroutine(PunchScaleRoutine(p1PointsBoxRect));
        }
        else if (p2Scored && p2PointsBoxRect != null)
        {
            StartCoroutine(PunchScaleRoutine(p2PointsBoxRect));
        }
    }

    /// <summary>
    /// Notifica quem é o sacador atual da partida
    /// </summary>
    public void SetServer(bool isPlayer1Serving)
    {
        cachedIsP1Serving = isPlayer1Serving;
        if (p1ServeIcon != null) p1ServeIcon.SetActive(isPlayer1Serving);
        if (p2ServeIcon != null) p2ServeIcon.SetActive(!isPlayer1Serving);
    }

    /// <summary>
    /// Atualiza os textos e badges do placar com formatação refinada
    /// </summary>
    private void UpdateScoreboardVisuals()
    {
        if (txtP1Sets != null) txtP1Sets.text = cachedP1Sets.ToString();
        if (txtP2Sets != null) txtP2Sets.text = cachedP2Sets.ToString();

        if (useTennisNotation)
        {
            if (txtP1Points != null) txtP1Points.text = FormatTennisScore(cachedP1Points, cachedP2Points);
            if (txtP2Points != null) txtP2Points.text = FormatTennisScore(cachedP2Points, cachedP1Points);
        }
        else
        {
            if (txtP1Points != null) txtP1Points.text = cachedP1Points.ToString("00");
            if (txtP2Points != null) txtP2Points.text = cachedP2Points.ToString("00");
        }

        if (txtLevelBadge != null)
        {
            txtLevelBadge.text = $"<color=#FACC15>NÍVEL {cachedLevel}</color>   <color=#475569>•</color>   <color=#94A3B8>GRADUAÇÃO: </color><color=#F8FAFC>{cachedGraduation.ToUpper()}</color>";
        }
    }

    /// <summary>
    /// Converte pontos numéricos corridos para o formato clássico oficial de tênis (00, 15, 30, 40, AD)
    /// </summary>
    private string FormatTennisScore(int myPoints, int opponentPoints)
    {
        // 0 -> 00, 1 -> 15, 2 -> 30, 3 -> 40
        if (myPoints == 0) return "00";
        if (myPoints == 1) return "15";
        if (myPoints == 2) return "30";

        // Empate a partir de 3 pontos = DEUCE (ambos com 40)
        if (myPoints >= 3 && opponentPoints >= 3)
        {
            if (myPoints == opponentPoints) return "40";
            if (myPoints == opponentPoints + 1) return "AD"; // Vantagem
            if (myPoints < opponentPoints) return "40";
        }

        if (myPoints >= 3) return "40";
        return myPoints.ToString("00");
    }

    /// <summary>
    /// Atualiza o nível global do jogador e o badge de graduação
    /// </summary>
    public void SetLevel(int level, string graduation)
    {
        cachedLevel = level;
        cachedGraduation = graduation;
        if (txtLevelBadge != null)
        {
            txtLevelBadge.text = $"<color=#FACC15>NÍVEL {cachedLevel}</color>   <color=#475569>•</color>   <color=#94A3B8>GRADUAÇÃO: </color><color=#F8FAFC>{cachedGraduation.ToUpper()}</color>";
            StartCoroutine(PunchScaleRoutine(levelBadgeRect, 1.15f, 0.25f));
        }
    }

    /// <summary>
    /// Exibe o contador de rally ao vivo e velocidade da bola no canto superior direito
    /// </summary>
    public void OnRallyHit(int currentRally, float ballSpeed)
    {
        if (rallyWidgetObj == null || txtRallyInfo == null) return;

        if (!rallyWidgetObj.activeSelf)
        {
            rallyWidgetObj.SetActive(true);
            StartCoroutine(FadeCanvasGroup(rallyCanvasGroup, 0f, 1f, 0.15f));
        }

        txtRallyInfo.text = $"RALLY: {currentRally}   <color=#64748B>•</color>   VELOCIDADE: {ballSpeed:F1} m/s";

        // Efeito de impacto a cada rebatida
        StartCoroutine(PunchScaleRoutine(rallyWidgetRect, 1.15f, 0.12f));
    }

    /// <summary>
    /// Finaliza o rally suavemente quando um ponto é marcado
    /// </summary>
    public void OnRallyEnd()
    {
        if (rallyWidgetObj != null && rallyWidgetObj.activeSelf)
        {
            StartCoroutine(FadeOutAndDisableRoutine(rallyWidgetObj, rallyCanvasGroup, 0.35f));
        }
    }

    public void HideRallyImmediate()
    {
        if (rallyWidgetObj != null)
        {
            rallyWidgetObj.SetActive(false);
            if (rallyCanvasGroup != null) rallyCanvasGroup.alpha = 0f;
        }
    }

    /// <summary>
    /// Dispara uma chamada arcade de impacto no centro superior da tela
    /// </summary>
    public void ShowAnnouncement(string text, Color bannerColor, float holdDuration = 1.2f)
    {
        if (bannerObj == null || txtBanner == null) return;

        if (currentBannerRoutine != null)
        {
            StopCoroutine(currentBannerRoutine);
        }

        currentBannerRoutine = StartCoroutine(ShowBannerRoutine(text, bannerColor, holdDuration));
    }

    private IEnumerator ShowBannerRoutine(string text, Color borderColor, float holdDuration)
    {
        bannerObj.SetActive(true);
        txtBanner.text = text;

        if (bannerBgImg != null)
        {
            bannerBgImg.sprite = LoginManager.CreateRoundedRectSprite(64, 64, 16, cardBgColor, borderColor, 3);
        }

        // Animação de entrada rápida e enérgica (punch-in)
        float elapsed = 0f;
        float inDuration = 0.18f;
        Vector3 startScale = new Vector3(0.5f, 0.5f, 1f);
        Vector3 targetScale = Vector3.one;

        while (elapsed < inDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / inDuration);
            float ease = Mathf.Sin(t * Mathf.PI * 0.5f);

            bannerCanvasGroup.alpha = t;
            bannerRect.localScale = Vector3.Lerp(startScale, targetScale * 1.08f, ease);
            yield return null;
        }

        bannerRect.localScale = targetScale;
        bannerCanvasGroup.alpha = 1f;

        yield return new WaitForSeconds(holdDuration);

        // Fade-out suave
        elapsed = 0f;
        float outDuration = 0.28f;
        while (elapsed < outDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / outDuration);
            bannerCanvasGroup.alpha = 1f - t;
            bannerRect.localScale = Vector3.Lerp(targetScale, targetScale * 0.95f, t);
            yield return null;
        }

        bannerCanvasGroup.alpha = 0f;
        bannerObj.SetActive(false);
        currentBannerRoutine = null;
    }

    private IEnumerator PunchScaleRoutine(RectTransform target, float maxScale = 1.25f, float duration = 0.20f)
    {
        if (target == null) yield break;

        Vector3 originalScale = Vector3.one;
        float elapsed = 0f;
        float halfDuration = duration * 0.5f;

        // Escala para cima
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            target.localScale = Vector3.Lerp(originalScale, Vector3.one * maxScale, Mathf.Sin(t * Mathf.PI * 0.5f));
            yield return null;
        }

        elapsed = 0f;
        // Retorna ao normal
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            target.localScale = Vector3.Lerp(Vector3.one * maxScale, originalScale, t);
            yield return null;
        }

        target.localScale = originalScale;
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
    {
        if (cg == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        cg.alpha = to;
    }

    private IEnumerator FadeOutAndDisableRoutine(GameObject go, CanvasGroup cg, float duration)
    {
        if (cg != null)
        {
            yield return StartCoroutine(FadeCanvasGroup(cg, cg.alpha, 0f, duration));
        }
        if (go != null) go.SetActive(false);
    }

    private IEnumerator FadeOutControlsHintRoutine()
    {
        yield return new WaitForSeconds(8.0f);
        if (controlsHintCanvasGroup != null)
        {
            yield return StartCoroutine(FadeCanvasGroup(controlsHintCanvasGroup, controlsHintCanvasGroup.alpha, 0f, 1.5f));
            if (controlsHintObj != null) controlsHintObj.SetActive(false);
        }
    }

    #endregion

    #region Transição de Níveis Intermediária

    /// <summary>
    /// Dispara a tela cinematográfica de transição entre níveis com detalhes de atributos e anúncio de saque
    /// </summary>
    public void ShowLevelTransition(int fromLevel, int toLevel, string fromGrad, string toGrad, System.Action onComplete = null)
    {
        if (currentLevelTransitionRoutine != null)
        {
            StopCoroutine(currentLevelTransitionRoutine);
        }

        currentLevelTransitionRoutine = StartCoroutine(LevelTransitionRoutine(fromLevel, toLevel, fromGrad, toGrad, onComplete, false, false));
    }

    /// <summary>
    /// Dispara a tela comemorativa de Grande Campeão ao vencer o nível 5
    /// </summary>
    public void ShowChampionTransition(int level, string grad, System.Action onComplete = null)
    {
        if (currentLevelTransitionRoutine != null)
        {
            StopCoroutine(currentLevelTransitionRoutine);
        }

        currentLevelTransitionRoutine = StartCoroutine(LevelTransitionRoutine(level, level, grad, grad, onComplete, true, false));
    }

    /// <summary>
    /// Dispara a tela de revanche / nova tentativa para o nível atual
    /// </summary>
    public void ShowRetryTransition(int level, string grad, System.Action onComplete = null)
    {
        if (currentLevelTransitionRoutine != null)
        {
            StopCoroutine(currentLevelTransitionRoutine);
        }

        currentLevelTransitionRoutine = StartCoroutine(LevelTransitionRoutine(level, level, grad, grad, onComplete, false, true));
    }

    private IEnumerator LevelTransitionRoutine(int fromLevel, int toLevel, string fromGrad, string toGrad, System.Action onComplete, bool isChampion = false, bool isRetry = false)
    {
        isTransitioningLevel = true;

        if (levelTransitionOverlayObj == null)
        {
            TMP_FontAsset baseFont = customFont ?? TMP_Settings.defaultFontAsset;
            BuildLevelTransitionOverlay(rootCanvas.transform, baseFont);
        }

        if (levelTransitionOverlayObj == null)
        {
            isTransitioningLevel = false;
            onComplete?.Invoke();
            yield break;
        }

        // Traz o overlay para o topo visual absoluto do Canvas
        levelTransitionOverlayObj.transform.SetAsLastSibling();
        levelTransitionOverlayObj.SetActive(true);

        // Preenche os textos temáticos de forma direta e limpa
        if (isChampion)
        {
            if (txtTransitionTag != null) txtTransitionTag.text = "★ TORNEIO CONCLUÍDO ★";
            if (txtTransitionTitle != null) txtTransitionTitle.text = "GRANDE CAMPEÃO!";
            if (txtTransitionSubtitle != null) txtTransitionSubtitle.text = $"GRADUAÇÃO MÁXIMA: {toGrad.ToUpper()}";
            if (txtTransitionPrompt != null) txtTransitionPrompt.text = "<b><color=#72F526>PREPARAR... SAQUE! (MODO CAMPEÃO)</color></b>\n<size=12><color=#94A3B8>Pressione [ESPAÇO] para jogar ou aguarde...</color></size>";
        }
        else if (isRetry)
        {
            if (txtTransitionTag != null) txtTransitionTag.text = "★ REVANCHE ★";
            if (txtTransitionTitle != null) txtTransitionTitle.text = $"NÍVEL {toLevel}";
            if (txtTransitionSubtitle != null) txtTransitionSubtitle.text = $"GRADUAÇÃO: {toGrad.ToUpper()}";
            if (txtTransitionPrompt != null) txtTransitionPrompt.text = "<b><color=#72F526>PREPARAR... SAQUE!</color></b>\n<size=12><color=#94A3B8>Pressione [ESPAÇO] para sacar ou aguarde...</color></size>";
        }
        else
        {
            if (txtTransitionTag != null) txtTransitionTag.text = "★ NOVO NÍVEL ALCANÇADO ★";
            if (txtTransitionTitle != null) txtTransitionTitle.text = $"NÍVEL {toLevel}";
            if (txtTransitionSubtitle != null) txtTransitionSubtitle.text = $"GRADUAÇÃO: {toGrad.ToUpper()}";
            if (txtTransitionPrompt != null) txtTransitionPrompt.text = "<b><color=#72F526>PREPARAR... SAQUE!</color></b>\n<size=12><color=#94A3B8>Pressione [ESPAÇO] para iniciar ou aguarde...</color></size>";
        }

        // Toca efeito sonoro retrô
        AudioClip soundToPlay = levelUpSound != null ? levelUpSound : (cheerSound != null ? cheerSound : alertSound);
        if (audioSource != null && soundToPlay != null)
        {
            audioSource.PlayOneShot(soundToPlay);
        }

        // Bloqueia raycasts durante a animação para proteção de input
        if (levelTransitionCanvasGroup != null)
        {
            levelTransitionCanvasGroup.blocksRaycasts = true;
        }

        // 1. Animação de Entrada Cinematográfica (Punch-in elástico retrô)
        float elapsed = 0f;
        float punchInDuration = 0.28f;
        while (elapsed < punchInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / punchInDuration);

            // Escala com bounce suave: 0.4x -> 1.10x -> 1.0x
            float scaleValue = Mathf.Lerp(0.4f, 1.10f, Mathf.Sin(t * Mathf.PI * 0.5f));
            if (t >= 0.75f)
            {
                scaleValue = Mathf.Lerp(1.10f, 1.0f, (t - 0.75f) / 0.25f);
            }

            if (levelTransitionCanvasGroup != null) levelTransitionCanvasGroup.alpha = Mathf.Clamp01(t * 1.4f);
            if (levelTransitionCardRect != null) levelTransitionCardRect.localScale = new Vector3(scaleValue, scaleValue, 1f);
            yield return null;
        }

        if (levelTransitionCardRect != null) levelTransitionCardRect.localScale = Vector3.one;
        if (levelTransitionCanvasGroup != null) levelTransitionCanvasGroup.alpha = 1f;

        // 2. Tempo de Retenção (Hold Duration) com suporte a avanço imediato por [ESPAÇO] ou [ENTER]
        float holdTimer = 0f;
        while (holdTimer < levelTransitionHoldDuration)
        {
            holdTimer += Time.unscaledDeltaTime;

            // Pulso sutil no anúncio 'PREPARAR... SAQUE!'
            if (txtTransitionPrompt != null)
            {
                float pulse = 0.94f + (Mathf.Sin(Time.unscaledTime * 6f) * 0.06f);
                txtTransitionPrompt.transform.localScale = new Vector3(pulse, pulse, 1f);
            }

            // Permite ao jogador pular o tempo de espera após 0.35s
            if (holdTimer > 0.35f && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                break;
            }

            yield return null;
        }

        if (txtTransitionPrompt != null) txtTransitionPrompt.transform.localScale = Vector3.one;

        // 3. Animação de Saída Suave (Fade Out)
        elapsed = 0f;
        while (elapsed < levelTransitionFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / levelTransitionFadeDuration);

            if (levelTransitionCanvasGroup != null) levelTransitionCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
            if (levelTransitionCardRect != null) levelTransitionCardRect.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 1.05f, t);
            yield return null;
        }

        if (levelTransitionCanvasGroup != null)
        {
            levelTransitionCanvasGroup.alpha = 0f;
            levelTransitionCanvasGroup.blocksRaycasts = false;
        }

        levelTransitionOverlayObj.SetActive(false);
        isTransitioningLevel = false;
        currentLevelTransitionRoutine = null;

        // Executa a conclusão (atualiza o nível no GameManager e inicia o saque)
        onComplete?.Invoke();
    }

    #endregion
}
