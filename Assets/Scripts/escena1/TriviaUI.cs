using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Fusion;

public class TriviaUI : MonoBehaviour
{
    public static TriviaUI Instance;

    [Header("Referencias de UI")]
    [SerializeField] private GameObject _panelLobby;
    [SerializeField] private GameObject _panelPrincipal;
    [SerializeField] private TMP_Text _preguntaText;
    [SerializeField] private TMP_Text[] _opcionesTexts;
    [SerializeField] private TMP_Text _timerText;
    [SerializeField] private TMP_Text _textoNivel;
    [SerializeField] private TMP_Text _textoPuntajeHud;

    [HideInInspector] public int LastSelectedIndex = -1;

    [Header("Lobby")]
    [SerializeField] private Transform _playerListContainer;
    [SerializeField] private GameObject _playerEntryPrefab;
    [SerializeField] private GameObject _botonStart;
    [SerializeField] private GameObject _botonRestartServer;
    [SerializeField] private GameObject _botonGenerarNuevas;
    [SerializeField] private GameObject _botonCargarJson;

    [Header("Post-Carrera")]
    [SerializeField] private GameObject _panelLlegaste;
    [SerializeField] private GameObject _panelPodio;
    [SerializeField] private TMP_Text _textoPodio;
    [SerializeField] private TMP_Text _textoEsperaLlegada;
    [SerializeField] private TMP_Text _textoPuntajeLlegada;

    [Header("Retroalimentación IA (Final de Carrera)")]
    [SerializeField] private GameObject panelFeedbackFinal;
    [SerializeField] private TMP_Text textoMensajeGeneral;
    [SerializeField] private Transform _feedbackScrollContent;
    [SerializeField] private Button _botonVerFeedback;
    [SerializeField] private TMP_Text _textoBotonFeedback;
    [SerializeField] private TMP_Text _feedbackPageTitle;
    [SerializeField] private Button _feedbackPageButton;
    [SerializeField] private TMP_Text _feedbackPageButtonText;

    private FeedbackData _cachedFeedbackData;
    private readonly List<int> _feedbackLevels = new List<int>();
    private readonly Dictionary<int, List<FeedbackItem>> _feedbackByLevel = new Dictionary<int, List<FeedbackItem>>();
    private int _feedbackPageIndex;
    private Transform _feedbackPaginationRoot;
    private bool _generandoPreguntas = false;
    private int _localScore;
    private int _localTotal;
    private int _localN1Correct;
    private int _localN2Correct;
    private int _localN3Correct;
    private bool _levelBlockedMessageActive;
    private int _indicatorLevel = 1;
    private int _blockedCorrect;
    private int _blockedTotal;
    private int _blockedLevelQuestionIndex;
    private int _blockedLastAnsweredIndex;
    private Coroutine _refreshLobbyCoroutine;
    private NetworkRunner _lobbyRunner;
    private float _lastLobbyDiagLogTime = -10f;

    private void Awake()
    {
        Instance = this;
        HideAllLocalPanels();
        ResolveHudTexts();
    }

    private void Update()
    {
        if (_levelBlockedMessageActive && _blockedTotal > 0)
            RefreshBlockedMessage(_blockedCorrect, _blockedTotal, _blockedLevelQuestionIndex, _blockedLastAnsweredIndex);
    }

    private void ResolveHudTexts()
    {
        if (_textoNivel == null)
            _textoNivel = FindHudText("TextoNivel", "textoNivel", "Nivel");

        if (_textoPuntajeHud == null)
            _textoPuntajeHud = FindHudText("PuntajeHud", "TextoPuntaje", "puntajeHud", "Puntaje");

        var legacyMeta = GameObject.Find("MetaBloqueada");
        if (legacyMeta != null)
            legacyMeta.SetActive(false);
    }

    private TMP_Text FindHudText(params string[] names)
    {
        foreach (string name in names)
        {
            var go = GameObject.Find(name);
            if (go != null)
            {
                var tmp = go.GetComponent<TMP_Text>();
                if (tmp != null)
                    return tmp;
            }
        }

        return null;
    }

    private void HideAllLocalPanels()
    {
        if (_panelLobby != null) _panelLobby.SetActive(false);
        if (_panelPrincipal != null) _panelPrincipal.SetActive(false);
        if (_panelLlegaste != null) _panelLlegaste.SetActive(false);
        if (_panelPodio != null) _panelPodio.SetActive(false);
        if (panelFeedbackFinal != null) panelFeedbackFinal.SetActive(false);
    }

    private void ShowOnlyLobbyPanel()
    {
        HideAllLocalPanels();
        if (_panelLobby != null) _panelLobby.SetActive(true);
    }

    private void ShowOnlyQuestionPanel()
    {
        if (_panelLobby != null) _panelLobby.SetActive(false);
        if (_panelLlegaste != null) _panelLlegaste.SetActive(false);
        if (panelFeedbackFinal != null) panelFeedbackFinal.SetActive(false);
        if (!GameManager.HasFinishers && _panelPodio != null) _panelPodio.SetActive(false);
        if (_panelPrincipal != null) _panelPrincipal.SetActive(true);
    }

    private void ShowOnlyFinishPanel()
    {
        if (_panelLobby != null) _panelLobby.SetActive(false);
        if (_panelPrincipal != null) _panelPrincipal.SetActive(false);
        if (panelFeedbackFinal != null) panelFeedbackFinal.SetActive(false);
        if (!GameManager.HasFinishers && _panelPodio != null) _panelPodio.SetActive(false);
        if (_panelLlegaste != null) _panelLlegaste.SetActive(true);
    }

    private void ShowOnlyPodiumPanel()
    {
        HideAllLocalPanels();
        if (_panelPodio != null) _panelPodio.SetActive(true);
    }

    private void ShowOnlyFeedbackPanel()
    {
        if (_panelLobby != null) _panelLobby.SetActive(false);
        if (_panelPrincipal != null) _panelPrincipal.SetActive(false);
        if (_panelLlegaste != null) _panelLlegaste.SetActive(false);
        if (!GameManager.HasFinishers && _panelPodio != null) _panelPodio.SetActive(false);
        if (panelFeedbackFinal != null)
        {
            panelFeedbackFinal.SetActive(true);
            PrepareFeedbackPanel();
        }
    }

    private void PrepareFeedbackPanel()
    {
        if (panelFeedbackFinal == null)
            return;

        DisableFeedbackPanelLayoutGroup();
        HideLegacyFeedbackTexts();
        TryResolveFeedbackScrollContent();
        EnsureFeedbackScrollUI();
    }

    private void DisableFeedbackPanelLayoutGroup()
    {
        VerticalLayoutGroup layoutGroup = panelFeedbackFinal.GetComponent<VerticalLayoutGroup>();
        if (layoutGroup != null)
            layoutGroup.enabled = false;
    }

    private void HideLegacyFeedbackTexts()
    {
        Transform root = panelFeedbackFinal.transform;
        foreach (string name in new[] { "Feedback", "Fortalezas", "Mejoras", "MensajeGeneral" })
        {
            Transform child = root.Find(name);
            if (child != null)
                child.gameObject.SetActive(false);
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name.StartsWith("Text (TMP)"))
                child.gameObject.SetActive(false);
        }

        if (textoMensajeGeneral != null)
            textoMensajeGeneral.gameObject.SetActive(false);
    }

    private void TryResolveFeedbackScrollContent()
    {
        if (_feedbackScrollContent != null)
            return;

        Transform content = panelFeedbackFinal.transform.Find("FeedbackScroll/Viewport/Content");
        if (content != null)
            _feedbackScrollContent = content;
    }

    private void EnsureFeedbackScrollUI()
    {
        if (_feedbackScrollContent != null || panelFeedbackFinal == null)
            return;

        Transform scrollRoot = panelFeedbackFinal.transform.Find("FeedbackScroll");
        if (scrollRoot == null)
        {
            var scrollGo = new GameObject("FeedbackScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollRoot = scrollGo.transform;
            scrollRoot.SetParent(panelFeedbackFinal.transform, false);

            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0.05f, 0.12f);
            scrollRt.anchorMax = new Vector2(0.95f, 0.82f);
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = Vector2.zero;
            scrollRt.localScale = Vector3.one;

            var scrollImage = scrollGo.GetComponent<Image>();
            scrollImage.color = UITheme.ListBg;
            scrollImage.raycastTarget = true;
        }

        Transform viewport = scrollRoot.Find("Viewport");
        if (viewport == null)
        {
            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport = viewportGo.transform;
            viewport.SetParent(scrollRoot, false);

            var viewportRt = viewport.GetComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;

            var viewportImage = viewport.GetComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;
        }

        Transform content = viewport.Find("Content");
        if (content == null)
        {
            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content = contentGo.transform;
            content.SetParent(viewport, false);

            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = Vector2.zero;

            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        ScrollRect scrollRect = scrollRoot.GetComponent<ScrollRect>();
        scrollRect.content = content.GetComponent<RectTransform>();
        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        _feedbackScrollContent = content;
        scrollRoot.SetSiblingIndex(0);
    }

    public void HideAllForMenu()
    {
        HideAllLocalPanels();
    }

    public bool IsLobbyVisible() => _panelLobby != null && _panelLobby.activeSelf;

    public bool BlocksMouseLook()
    {
        if (_panelLobby != null && _panelLobby.activeSelf)
            return true;
        if (_panelPrincipal != null && _panelPrincipal.activeSelf)
            return true;
        if (panelFeedbackFinal != null && panelFeedbackFinal.activeSelf)
            return true;
        if (_panelLlegaste != null && _panelLlegaste.activeSelf
            && Player.Local != null && Player.Local.State == EPlayerState.Finished)
            return true;
        return false;
    }

    public void ShowLobby(NetworkRunner runner)
    {
        if (runner == null) return;

        if (JuegoUI.Instance != null)
            JuegoUI.Instance.ShowLobbyPhase();
        else
        {
            Spawner.SetJuegoGameUIVisible(false);
            Spawner.SetJuegoCanvasVisible("CanvasLobby", true);
        }

        ShowOnlyLobbyPanel();

        RefreshPlayerList(runner);

        _lobbyRunner = runner;

        bool isHost = runner.IsServer || runner.IsSharedModeMasterClient;
        SetHostButtonsVisible(isHost);
        RefreshHostButtonStates(runner);
    }

    private void SetHostButtonsVisible(bool visible)
    {
        if (_botonStart != null) _botonStart.SetActive(visible);
        if (_botonGenerarNuevas != null) _botonGenerarNuevas.SetActive(visible);
        if (_botonCargarJson != null) _botonCargarJson.SetActive(visible);
        if (_botonRestartServer != null) _botonRestartServer.SetActive(visible);
    }

    public void RefreshHostButtonStates(NetworkRunner runner = null)
    {
        runner ??= _lobbyRunner;
        if (runner == null)
            runner = FindAnyObjectByType<NetworkRunner>();
        if (runner == null) return;

        bool isHost = runner.IsServer || runner.IsSharedModeMasterClient;
        if (!isHost) return;

        SetHostButtonsVisible(true);

        bool ready = QuestionManager.Instance != null && QuestionManager.Instance.IsReady;

        if (!ready && Time.unscaledTime - _lastLobbyDiagLogTime >= 5f)
        {
            _lastLobbyDiagLogTime = Time.unscaledTime;
            Debug.Log(
                $"[Lobby] Iniciar deshabilitado — QM={(QuestionManager.Instance != null)}, " +
                $"IsReady={(QuestionManager.Instance != null && QuestionManager.Instance.IsReady)}, " +
                $"generando={_generandoPreguntas}, isHost={isHost}");
        }
        else if (ready)
        {
            _lastLobbyDiagLogTime = -10f;
        }

        if (_botonStart != null)
        {
            Button btn = _botonStart.GetComponent<Button>();
            if (btn != null) btn.interactable = ready && !_generandoPreguntas;
            SetButtonLabel(_botonStart, _generandoPreguntas ? "IA Pensando..." : (ready ? "Iniciar Partida" : "Esperando preguntas..."));
        }

        if (_botonGenerarNuevas != null)
        {
            Button btn = _botonGenerarNuevas.GetComponent<Button>();
            if (btn != null) btn.interactable = !_generandoPreguntas;
            SetButtonLabel(_botonGenerarNuevas, _generandoPreguntas
                ? "Generando..."
                : (ready ? "Regenerar Preguntas" : "Generar Preguntas (IA)"));
        }

        if (_botonCargarJson != null)
        {
            Button btn = _botonCargarJson.GetComponent<Button>();
            if (btn != null) btn.interactable = !_generandoPreguntas;
            SetButtonLabel(_botonCargarJson, "Cargar JSON");
        }

        if (_botonRestartServer != null)
        {
            Button btn = _botonRestartServer.GetComponent<Button>();
            if (btn != null) btn.interactable = !_generandoPreguntas;
            SetButtonLabel(_botonRestartServer, "Reiniciar Servidor");
        }
    }

    private static void SetButtonLabel(GameObject buttonObject, string label)
    {
        if (buttonObject == null) return;
        TMP_Text btnText = buttonObject.GetComponentInChildren<TMP_Text>();
        if (btnText != null) btnText.text = label;
    }

    public void RefreshPlayerList(NetworkRunner runner)
    {
        if (_playerListContainer == null || _playerEntryPrefab == null) return;

        foreach (Transform child in _playerListContainer) Destroy(child.gameObject);

        foreach (var player in runner.ActivePlayers)
        {
            GameObject entry = Instantiate(_playerEntryPrefab, _playerListContainer);
            TMP_Text text = entry.GetComponent<TMP_Text>();
            if (text != null) text.text = Player.GetDisplayName(runner, player);
        }
    }

    public bool IsLevelBlockedMessageActive => _levelBlockedMessageActive;

    public void UpdateLevelIndicator(int level)
    {
        if (Player.Local != null)
        {
            UpdateLevelHud(
                level,
                Player.Local.GetCurrentLevelCorrectCount(),
                QuestionManager.Instance != null ? QuestionManager.Instance.GetQuestionCount(level) : 0);
            return;
        }

        _indicatorLevel = level;
        if (_levelBlockedMessageActive)
            return;

        ResolveHudTexts();
        if (_textoNivel != null)
            _textoNivel.text = BuildLevelIndicatorText(level);
    }

    public void UpdateLevelHud(int level, int correct, int total)
    {
        _indicatorLevel = level;
        if (_levelBlockedMessageActive)
            return;

        ResolveHudTexts();
        if (_textoNivel == null) return;

        if (total <= 0)
        {
            _textoNivel.text = BuildLevelIndicatorText(level);
            return;
        }

        _textoNivel.text = BuildLevelIndicatorText(level, LevelProgressRules.FormatHudProgress(level, correct, total));
    }

    private static string BuildLevelIndicatorText(int level, string subtitle = null)
    {
        string baseText = LevelTopics.FormatIndicator(level);
        if (string.IsNullOrEmpty(subtitle))
            return baseText;
        return $"{baseText}\n\n{subtitle}";
    }

    public void UpdateLevelProgress(int level, int correct, int total)
    {
        UpdateLevelHud(level, correct, total);
    }

    public void UpdateScoreDisplay(int score, int maxScore)
    {
        ResolveHudTexts();
        if (_textoPuntajeHud == null) return;
        _textoPuntajeHud.text = ScoringRules.FormatScoreHud(score, maxScore);
    }

    public void ShowLevelBlockedMessage(int correct, int total, int levelQuestionIndex, int lastAnsweredIndex = -1)
    {
        _levelBlockedMessageActive = true;
        RefreshBlockedMessage(correct, total, levelQuestionIndex, lastAnsweredIndex);
    }

    public void HideLevelBlockedMessage()
    {
        _levelBlockedMessageActive = false;

        int correct = Player.Local != null ? Player.Local.GetCurrentLevelCorrectCount() : _blockedCorrect;
        int total = QuestionManager.Instance != null && Player.Local != null
            ? QuestionManager.Instance.GetQuestionCount(Player.Local.CurrentLevel)
            : _blockedTotal;
        UpdateLevelHud(_indicatorLevel, correct, total);
    }

    public void RefreshBlockedMessage(int correct, int total, int levelQuestionIndex, int lastAnsweredIndex = -1)
    {
        if (total <= 0)
            return;

        _blockedCorrect = correct;
        _blockedTotal = total;
        _blockedLevelQuestionIndex = levelQuestionIndex;
        _blockedLastAnsweredIndex = lastAnsweredIndex;

        if (Player.Local != null)
            _indicatorLevel = Player.Local.CurrentLevel;

        ResolveHudTexts();
        if (_textoNivel == null) return;

        string blockedMessage = LevelProgressRules.FormatBlockedAtGoalMessage(
            correct, total, levelQuestionIndex, lastAnsweredIndex);
        _textoNivel.text = BuildLevelIndicatorText(_indicatorLevel, blockedMessage);
    }

    public void RegistrarFinDeCarreraLocal(int score, int total, int n1Correct = 0, int n2Correct = 0, int n3Correct = 0)
    {
        _localScore = score;
        _localTotal = total;
        _localN1Correct = n1Correct;
        _localN2Correct = n2Correct;
        _localN3Correct = n3Correct;
        _cachedFeedbackData = null;
        HideLevelBlockedMessage();

        if (JuegoUI.Instance != null)
            JuegoUI.Instance.ShowFinCarreraPhase();
        else
        {
            Spawner.SetJuegoCanvasVisible("CanvasLobby", false);
            Spawner.SetJuegoCanvasVisible("CanvasTimer", false);
            Spawner.SetJuegoCanvasVisible("CanvasPuntaje", false);
            Spawner.SetJuegoCanvasVisible("CanvasPreguntas", false);
            Spawner.SetJuegoCanvasVisible("CanvasPodio", false);
            Spawner.SetJuegoCanvasVisible("CanvasFinCarrera", true);
        }

        ShowOnlyFinishPanel();
        ApplyFinishPanelContent(score, total, n1Correct, n2Correct, n3Correct);

        if (_botonVerFeedback != null)
        {
            _botonVerFeedback.interactable = true;
            if (_textoBotonFeedback != null)
                _textoBotonFeedback.text = "Ver explicaciones";
        }
    }

    public void UI_BotonSolicitarFeedback()
    {
        if (QuestionManager.Instance == null) return;

        SeenQuestion[] seen = PlayerQuestionHistory.GetSeenQuestions(QuestionManager.Instance);
        if (seen.Length == 0)
        {
            ShowFeedbackMessage("No viste preguntas en esta partida, así que no hay explicaciones para mostrar.");
            return;
        }

        _cachedFeedbackData = null;
        QuestionManager.Instance.SolicitarFeedbackFinal(_localScore, _localTotal, seen);

        if (_cachedFeedbackData != null)
            ShowFeedbackPanel(_cachedFeedbackData);
    }

    public void UI_BotonFeedback()
    {
        if (_cachedFeedbackData != null)
            UI_BotonMostrarPanelFeedbackOculto();
        else
            UI_BotonSolicitarFeedback();
    }

    public void ShowFeedback(FeedbackData data)
    {
        _cachedFeedbackData = data;

        if (_botonVerFeedback != null) _botonVerFeedback.interactable = true;
        if (_textoBotonFeedback != null) _textoBotonFeedback.text = "Ver explicaciones";
        if (GameManager.Instance != null) GameManager.Instance.RPC_FeedbackCompletado();
    }

    public void OnFeedbackError()
    {
        if (_botonVerFeedback != null) _botonVerFeedback.interactable = true;
        if (_textoBotonFeedback != null) _textoBotonFeedback.text = "Ver explicaciones";
    }

    public void UI_BotonMostrarPanelFeedbackOculto()
    {
        if (_cachedFeedbackData == null) return;
        ShowFeedbackPanel(_cachedFeedbackData);
    }

    public void UI_BotonFeedbackSiguienteNivel()
    {
        if (_feedbackLevels.Count <= 1) return;

        _feedbackPageIndex = (_feedbackPageIndex + 1) % _feedbackLevels.Count;
        ShowFeedbackPage(_feedbackPageIndex);
    }

    private void ShowFeedbackMessage(string message)
    {
        EnsureFinCarreraCanvasVisible();
        ShowOnlyFeedbackPanel();
        _feedbackLevels.Clear();
        _feedbackByLevel.Clear();
        SetFeedbackPaginationVisible(false);
        ClearFeedbackEntries();

        if (_feedbackScrollContent != null)
        {
            CreateFeedbackEntry(1, message, string.Empty, string.Empty);
            return;
        }

        if (textoMensajeGeneral != null)
        {
            textoMensajeGeneral.gameObject.SetActive(true);
            textoMensajeGeneral.text = message;
        }
    }

    private void ShowFeedbackPanel(FeedbackData data)
    {
        EnsureFinCarreraCanvasVisible();
        ShowOnlyFeedbackPanel();
        RenderFeedbackItems(data);
    }

    private void EnsureFinCarreraCanvasVisible()
    {
        if (JuegoUI.Instance != null)
            JuegoUI.Instance.ShowFinCarreraPhase();
        else
        {
            Spawner.SetJuegoCanvasVisible("CanvasLobby", false);
            Spawner.SetJuegoCanvasVisible("CanvasTimer", false);
            Spawner.SetJuegoCanvasVisible("CanvasPuntaje", false);
            Spawner.SetJuegoCanvasVisible("CanvasPreguntas", false);
            Spawner.SetJuegoCanvasVisible("CanvasPodio", false);
            Spawner.SetJuegoCanvasVisible("CanvasFinCarrera", true);
        }
    }

    private void BuildFeedbackPages(FeedbackData data)
    {
        _feedbackLevels.Clear();
        _feedbackByLevel.Clear();

        if (data?.items == null)
            return;

        foreach (FeedbackItem item in data.items)
        {
            int nivel = ResolveFeedbackNivel(item);
            if (!_feedbackByLevel.TryGetValue(nivel, out List<FeedbackItem> bucket))
            {
                bucket = new List<FeedbackItem>();
                _feedbackByLevel[nivel] = bucket;
            }

            bucket.Add(item);
        }

        foreach (int nivel in _feedbackByLevel.Keys)
            _feedbackLevels.Add(nivel);

        _feedbackLevels.Sort();
        _feedbackPageIndex = 0;
    }

    private static int ResolveFeedbackNivel(FeedbackItem item)
    {
        if (item != null && item.nivel >= 1 && item.nivel <= 3)
            return item.nivel;

        string id = item?.id;
        if (!string.IsNullOrEmpty(id) && id.Length >= 2 && id[0] == 'N' && char.IsDigit(id[1]))
            return id[1] - '0';

        return 1;
    }

    private void ShowFeedbackPage(int pageIndex)
    {
        if (_feedbackLevels.Count == 0)
            return;

        pageIndex = Mathf.Clamp(pageIndex, 0, _feedbackLevels.Count - 1);
        _feedbackPageIndex = pageIndex;

        int nivel = _feedbackLevels[pageIndex];
        if (!_feedbackByLevel.TryGetValue(nivel, out List<FeedbackItem> items) || items.Count == 0)
            return;

        EnsureFeedbackPaginationUI();
        UpdateFeedbackPaginationControls();
        ClearFeedbackEntries();
        ResetFeedbackScrollPosition();

        if (_feedbackScrollContent == null)
        {
            var fallback = new StringBuilder();
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) fallback.Append("\n\n");
                fallback.Append(FormatFeedbackItem(i + 1, items[i]));
            }

            if (textoMensajeGeneral != null)
            {
                textoMensajeGeneral.gameObject.SetActive(true);
                textoMensajeGeneral.text = fallback.ToString();
            }

            return;
        }

        if (textoMensajeGeneral != null)
            textoMensajeGeneral.gameObject.SetActive(false);

        for (int i = 0; i < items.Count; i++)
        {
            FeedbackItem item = items[i];
            CreateFeedbackEntry(
                i + 1,
                item.question,
                item.correct_option,
                item.explanation);
        }
    }

    private void RenderFeedbackItems(FeedbackData data)
    {
        ClearFeedbackEntries();

        if (data?.items == null || data.items.Length == 0)
        {
            ShowFeedbackMessage("No se recibieron explicaciones para las preguntas vistas.");
            return;
        }

        BuildFeedbackPages(data);
        ShowFeedbackPage(0);
    }

    private void EnsureFeedbackPaginationUI()
    {
        if (panelFeedbackFinal == null)
            return;

        DisableFeedbackPanelLayoutGroup();

        if (_feedbackPaginationRoot == null)
        {
            Transform existing = panelFeedbackFinal.transform.Find("FeedbackPagination");
            if (existing != null)
                _feedbackPaginationRoot = existing;
        }

        if (_feedbackPaginationRoot == null)
        {
            var rootGo = new GameObject("FeedbackPagination", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            _feedbackPaginationRoot = rootGo.transform;
            _feedbackPaginationRoot.SetParent(panelFeedbackFinal.transform, false);

            var rootRt = rootGo.GetComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0.05f, 0.02f);
            rootRt.anchorMax = new Vector2(0.95f, 0.1f);
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            rootRt.localScale = Vector3.one;

            var layout = rootGo.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 4, 4);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
        }

        _feedbackPaginationRoot.SetAsLastSibling();

        if (_feedbackPageTitle == null)
        {
            Transform titleTr = _feedbackPaginationRoot.Find("PageTitle");
            if (titleTr != null)
                _feedbackPageTitle = titleTr.GetComponent<TMP_Text>();
        }

        if (_feedbackPageTitle == null)
        {
            var titleGo = new GameObject("PageTitle", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            titleGo.transform.SetParent(_feedbackPaginationRoot, false);
            _feedbackPageTitle = titleGo.GetComponent<TextMeshProUGUI>();
            var titleLayout = titleGo.GetComponent<LayoutElement>();
            titleLayout.flexibleWidth = 1f;
            titleLayout.minWidth = 200f;
            UITheme.StyleHudText(_feedbackPageTitle);
            _feedbackPageTitle.alignment = TextAlignmentOptions.MidlineLeft;
        }

        if (_feedbackPageButton == null)
        {
            Transform buttonTr = _feedbackPaginationRoot.Find("PageButton");
            if (buttonTr != null)
            {
                _feedbackPageButton = buttonTr.GetComponent<Button>();
                _feedbackPageButtonText = EnsurePageButtonLabel(buttonTr, "Siguiente nivel");
            }
        }

        if (_feedbackPageButton == null)
        {
            var buttonGo = new GameObject("PageButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonGo.transform.SetParent(_feedbackPaginationRoot, false);

            var buttonRt = buttonGo.GetComponent<RectTransform>();
            buttonRt.sizeDelta = new Vector2(260f, 44f);

            var buttonLayout = buttonGo.GetComponent<LayoutElement>();
            buttonLayout.minWidth = 220f;
            buttonLayout.preferredWidth = 260f;
            buttonLayout.minHeight = 44f;
            buttonLayout.preferredHeight = 44f;

            _feedbackPageButton = buttonGo.GetComponent<Button>();
            _feedbackPageButtonText = EnsurePageButtonLabel(buttonGo.transform, "Siguiente nivel");
        }
        else if (_feedbackPageButtonText == null)
        {
            _feedbackPageButtonText = EnsurePageButtonLabel(_feedbackPageButton.transform, "Siguiente nivel");
        }

        if (_feedbackPageButton != null)
        {
            _feedbackPageButton.onClick.RemoveListener(UI_BotonFeedbackSiguienteNivel);
            _feedbackPageButton.onClick.AddListener(UI_BotonFeedbackSiguienteNivel);
        }
    }

    private static TMP_Text EnsurePageButtonLabel(Transform buttonTr, string label)
    {
        Transform labelTr = buttonTr.Find("Label");
        if (labelTr == null)
        {
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelTr = labelGo.transform;
            labelTr.SetParent(buttonTr, false);

            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
        }

        TMP_Text labelText = labelTr.GetComponent<TMP_Text>();
        UITheme.StylePrimaryButton(buttonTr, label);
        return labelText;
    }

    private void UpdateFeedbackPaginationControls()
    {
        if (_feedbackLevels.Count == 0)
        {
            SetFeedbackPaginationVisible(false);
            return;
        }

        EnsureFeedbackPaginationUI();
        SetFeedbackPaginationVisible(true);

        int nivel = _feedbackLevels[_feedbackPageIndex];
        if (_feedbackPageTitle != null)
        {
            _feedbackPageTitle.text = _feedbackLevels.Count > 1
                ? $"Nivel {nivel} · Explicaciones ({_feedbackPageIndex + 1}/{_feedbackLevels.Count})"
                : $"Nivel {nivel} · Explicaciones";
        }

        if (_feedbackPageButton != null)
            _feedbackPageButton.gameObject.SetActive(_feedbackLevels.Count > 1);

        if (_feedbackPageButtonText != null && _feedbackLevels.Count > 1)
        {
            int nextIndex = (_feedbackPageIndex + 1) % _feedbackLevels.Count;
            int nextNivel = _feedbackLevels[nextIndex];
            _feedbackPageButtonText.text = nextIndex == 0
                ? $"Volver a nivel {nextNivel}"
                : $"Siguiente: Nivel {nextNivel}";
        }
    }

    private void SetFeedbackPaginationVisible(bool visible)
    {
        if (_feedbackPaginationRoot != null)
            _feedbackPaginationRoot.gameObject.SetActive(visible);
    }

    private void ResetFeedbackScrollPosition()
    {
        if (_feedbackScrollContent == null)
            return;

        ScrollRect scroll = _feedbackScrollContent.GetComponentInParent<ScrollRect>();
        if (scroll != null)
            scroll.verticalNormalizedPosition = 1f;
    }

    private void ClearFeedbackEntries()
    {
        if (_feedbackScrollContent == null)
            return;

        for (int i = _feedbackScrollContent.childCount - 1; i >= 0; i--)
            Destroy(_feedbackScrollContent.GetChild(i).gameObject);
    }

    private void CreateFeedbackEntry(int number, string question, string correctOption, string explanation)
    {
        if (_feedbackScrollContent == null)
            return;

        var entryGo = new GameObject($"FeedbackEntry_{number}", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        entryGo.transform.SetParent(_feedbackScrollContent, false);

        var layout = entryGo.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 10, 10);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var layoutElement = entryGo.GetComponent<LayoutElement>();
        layoutElement.minHeight = 120f;
        layoutElement.preferredWidth = -1f;

        var image = entryGo.GetComponent<Image>();
        image.color = UITheme.ListBg;
        image.raycastTarget = false;

        string questionText = string.IsNullOrWhiteSpace(question)
            ? $"Pregunta {number}"
            : $"<b>{number}. {question}</b>";

        CreateFeedbackText(entryGo.transform, questionText, 22f, FontStyles.Bold);

        if (!string.IsNullOrWhiteSpace(correctOption))
            CreateFeedbackText(entryGo.transform, $"<b>Respuesta correcta:</b> {correctOption}", 20f, FontStyles.Normal);

        if (!string.IsNullOrWhiteSpace(explanation))
            CreateFeedbackText(entryGo.transform, explanation, 18f, FontStyles.Italic);
    }

    private static void CreateFeedbackText(Transform parent, string text, float fontSize, FontStyles style)
    {
        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(parent, false);

        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = UITheme.TextPrimary;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;

        var layoutElement = textGo.AddComponent<LayoutElement>();
        layoutElement.minHeight = fontSize + 12f;
        layoutElement.flexibleWidth = 1f;
    }

    private static string FormatFeedbackItem(int number, FeedbackItem item)
    {
        var sb = new StringBuilder();
        sb.Append($"<b>{number}. {item.question}</b>");
        if (!string.IsNullOrWhiteSpace(item.correct_option))
            sb.Append("\n<b>Respuesta correcta:</b> ").Append(item.correct_option);
        if (!string.IsNullOrWhiteSpace(item.explanation))
            sb.Append("\n").Append(item.explanation);
        return sb.ToString();
    }

    public void ShowGenerateButton()
    {
        _generandoPreguntas = false;
        NetworkRunner runner = FindAnyObjectByType<NetworkRunner>();
        if (runner != null) ShowLobby(runner);
        else RefreshHostButtonStates();
    }

    public void UI_BotonGenerarNuevas()
    {
        _generandoPreguntas = true;
        RefreshHostButtonStates();

        if (GameManager.Instance != null)
            GameManager.Instance.UI_BotonRegenerarPreguntas();
        else if (QuestionManager.Instance != null)
            QuestionManager.Instance.RequestNewGeneration();
    }

    public void UI_BotonCargarPreguntasJson()
    {
        _generandoPreguntas = false;

        if (QuestionManager.Instance != null)
            QuestionManager.Instance.LoadStaticQuestionsFromFile();
        else
            Debug.LogError("Error: QuestionManager no está listo para cargar JSON.");

        RefreshHostButtonStates(_lobbyRunner);
    }

    public void UI_BotonIniciarPartida()
    {
        HideAllLocalPanels();

        if (GameManager.Instance != null)
            GameManager.Instance.UI_BotonIniciarPartida();
        else
            Debug.LogError("Error: GameManager no está listo.");
    }

    public void ShowQuestion(Question q)
    {
        ShowOnlyQuestionPanel();
        if (_preguntaText != null) _preguntaText.text = q.question;

        for (int i = 0; i < _opcionesTexts.Length; i++)
            _opcionesTexts[i].text = q.options[i];
    }

    public void Hide()
    {
        if (_panelPrincipal != null) _panelPrincipal.SetActive(false);
    }

    public void UpdateResponseTimer(float seconds)
    {
        if (_timerText == null) return;

        _timerText.text = "Responder: " + Mathf.Max(0f, seconds).ToString("F1") + "s";
        _timerText.color = Color.white;
    }

    public void UpdateNextQuestionTimer(float seconds)
    {
        if (_timerText == null) return;

        _timerText.text = "Siguiente en: " + Mathf.Max(0f, seconds).ToString("F1") + "s";
        _timerText.color = Color.yellow;
    }

    public void ClearTimer()
    {
        if (_timerText == null) return;

        _timerText.text = "";
        _timerText.color = Color.white;
    }

    public void OnOptionClicked(int index)
    {
        LastSelectedIndex = index;
    }

    public void UpdateLobbyUI(NetworkRunner runner)
    {
        ShowLobby(runner);
    }

    public void OnConnectionError()
    {
        _generandoPreguntas = false;
        RefreshHostButtonStates();
    }

    public void OnQuestionsReady()
    {
        _generandoPreguntas = false;
        RefreshHostButtonStates();
    }

    public void RefreshLobbyWhenReady()
    {
        if (_refreshLobbyCoroutine != null)
            StopCoroutine(_refreshLobbyCoroutine);
        _refreshLobbyCoroutine = StartCoroutine(RefreshLobbyWhenReadyRoutine());
    }

    private IEnumerator RefreshLobbyWhenReadyRoutine()
    {
        const float timeout = 60f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (!IsLobbyVisible())
            {
                _refreshLobbyCoroutine = null;
                yield break;
            }

            if (QuestionManager.Instance != null)
            {
                RefreshHostButtonStates(_lobbyRunner);
                if (QuestionManager.Instance.IsReady)
                {
                    _refreshLobbyCoroutine = null;
                    yield break;
                }
            }

            yield return null;
            elapsed += Time.deltaTime;
        }

        if (IsLobbyVisible())
            RefreshHostButtonStates(_lobbyRunner);

        _refreshLobbyCoroutine = null;
    }

    public void UI_BotonReiniciarServidor()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.UI_BotonReiniciarServidor();
            return;
        }

        var runner = FindAnyObjectByType<NetworkRunner>();
        if (runner != null)
            runner.Shutdown();

        UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.MenuPrincipal);
    }

    public void UI_BotonRestartServer() => UI_BotonReiniciarServidor();

    public void ShowWaiting()
    {
        if (panelFeedbackFinal != null && panelFeedbackFinal.activeSelf)
            return;

        if (Player.Local == null || Player.Local.State != EPlayerState.Finished)
            return;

        if (JuegoUI.Instance != null)
        {
            if (GameManager.HasFinishers)
                JuegoUI.Instance.ShowFinCarreraWithPodium();
            else
                JuegoUI.Instance.ShowFinCarreraPhase();
        }
        else
        {
            Spawner.SetJuegoCanvasVisible("CanvasLobby", false);
            Spawner.SetJuegoCanvasVisible("CanvasTimer", false);
            Spawner.SetJuegoCanvasVisible("CanvasPuntaje", false);
            Spawner.SetJuegoCanvasVisible("CanvasPreguntas", false);
            Spawner.SetJuegoCanvasVisible("CanvasFinCarrera", true);
            Spawner.SetJuegoCanvasVisible("CanvasPodio", GameManager.HasFinishers);
        }

        ShowOnlyFinishPanel();
        ApplyFinishPanelContent(_localScore, _localTotal, _localN1Correct, _localN2Correct, _localN3Correct);

        if (GameManager.HasFinishers)
            ShowPodiumSidebar();
    }

    private void ShowPodiumSidebar()
    {
        if (_panelPodio != null) _panelPodio.SetActive(true);
        UpdatePodiumLive();
    }

    public void ShowPodiumForAll()
    {
        bool localFinished = Player.Local != null && Player.Local.State == EPlayerState.Finished;

        if (localFinished)
        {
            if (JuegoUI.Instance != null)
                JuegoUI.Instance.ShowFinCarreraWithPodium();
            else
            {
                Spawner.SetJuegoCanvasVisible("CanvasLobby", false);
                Spawner.SetJuegoCanvasVisible("CanvasTimer", false);
                Spawner.SetJuegoCanvasVisible("CanvasPuntaje", false);
                Spawner.SetJuegoCanvasVisible("CanvasPreguntas", false);
                Spawner.SetJuegoCanvasVisible("CanvasFinCarrera", true);
                Spawner.SetJuegoCanvasVisible("CanvasPodio", true);
            }

            if (panelFeedbackFinal == null || !panelFeedbackFinal.activeSelf)
            {
                ShowOnlyFinishPanel();
                ApplyFinishPanelContent(_localScore, _localTotal, _localN1Correct, _localN2Correct, _localN3Correct);
            }
        }
        else
        {
            if (JuegoUI.Instance != null)
                JuegoUI.Instance.ShowPodiumAlongsideGameplay();
            else
            {
                Spawner.SetJuegoCanvasVisible("CanvasLobby", false);
                Spawner.SetJuegoCanvasVisible("CanvasTimer", true);
                Spawner.SetJuegoCanvasVisible("CanvasPuntaje", true);
                Spawner.SetJuegoCanvasVisible("CanvasPreguntas", true);
                Spawner.SetJuegoCanvasVisible("CanvasFinCarrera", false);
                Spawner.SetJuegoCanvasVisible("CanvasPodio", true);
            }
        }

        ShowPodiumSidebar();
    }

    public void UpdatePodiumLive()
    {
        Player[] todos = Object.FindObjectsByType<Player>(FindObjectsSortMode.None);
        var terminaron = System.Linq.Enumerable.ToList(
            System.Linq.Enumerable.ThenBy(
                System.Linq.Enumerable.OrderByDescending(
                    System.Linq.Enumerable.Where(todos, p => p.State == EPlayerState.Finished),
                    p => p.PuntajeObtenido
                ),
                p => p.FinishOrder
            )
        );

        string contenido = "";
        for (int i = 0; i < terminaron.Count; i++)
        {
            var p = terminaron[i];
            string tu = (Player.Local != null && p.Object.InputAuthority == Player.Local.Object.InputAuthority) ? " (TÚ)" : "";
            contenido += $"{i + 1}º Lugar - {p.GetDisplayName()} ({p.PuntajeObtenido} pts){tu}\n";
        }

        if (_textoPodio != null) _textoPodio.text = contenido;
    }

    public void StartGameUI()
    {
        PlayerQuestionHistory.Clear();
        HideLevelBlockedMessage();
        HideAllLocalPanels();

        if (JuegoUI.Instance != null)
            JuegoUI.Instance.ShowGameplayPhase();
        else
        {
            Spawner.SetJuegoCanvasVisible("CanvasLobby", false);
            Spawner.SetJuegoCanvasVisible("CanvasPodio", false);
            Spawner.SetJuegoCanvasVisible("CanvasFinCarrera", false);
            Spawner.SetJuegoCanvasVisible("CanvasTimer", true);
            Spawner.SetJuegoCanvasVisible("CanvasPuntaje", true);
            Spawner.SetJuegoCanvasVisible("CanvasPreguntas", true);
        }

        if (Player.Local != null)
        {
            UpdateLevelHud(
                Player.Local.CurrentLevel,
                Player.Local.GetCurrentLevelCorrectCount(),
                QuestionManager.Instance != null ? QuestionManager.Instance.GetQuestionCount(Player.Local.CurrentLevel) : 0);
            UpdateScoreDisplay(
                Player.Local.PuntajeObtenido,
                QuestionManager.Instance != null ? QuestionManager.Instance.GetMaxPossibleScore() : 0);
        }
    }

    private void ResolveFinishPanelTexts()
    {
        if (_panelLlegaste == null)
            return;

        if (_textoEsperaLlegada == null)
        {
            Transform llegaste = _panelLlegaste.transform.Find("Llegaste");
            if (llegaste != null)
                _textoEsperaLlegada = llegaste.GetComponent<TMP_Text>();
        }

        if (_textoPuntajeLlegada == null)
        {
            Transform puntaje = _panelLlegaste.transform.Find("PuntajeLlegada");
            if (puntaje != null)
                _textoPuntajeLlegada = puntaje.GetComponent<TMP_Text>();
        }
    }

    private void ApplyFinishPanelContent(int score, int total, int n1Correct = 0, int n2Correct = 0, int n3Correct = 0)
    {
        ResolveFinishPanelTexts();

        if (_textoEsperaLlegada != null)
            _textoEsperaLlegada.text = "Esperando que termine el resto";

        if (_textoPuntajeLlegada != null)
            _textoPuntajeLlegada.text = BuildFinishPanelText(score, total, n1Correct, n2Correct, n3Correct);
    }

    private string BuildFinishPanelText(int score, int total, int n1Correct, int n2Correct, int n3Correct)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Puntaje final: {score}/{total}");

        int n1Total = QuestionManager.Instance != null ? QuestionManager.Instance.GetQuestionCount(1) : 0;
        int n2Total = QuestionManager.Instance != null ? QuestionManager.Instance.GetQuestionCount(2) : 0;
        int n3Total = QuestionManager.Instance != null ? QuestionManager.Instance.GetQuestionCount(3) : 0;

        if (n1Total > 0 || n2Total > 0 || n3Total > 0)
        {
            sb.Append("Por nivel: ");
            if (n1Total > 0) sb.Append($"N1: {n1Correct}/{n1Total}");
            if (n2Total > 0) sb.Append(n1Total > 0 ? $" · N2: {n2Correct}/{n2Total}" : $"N2: {n2Correct}/{n2Total}");
            if (n3Total > 0) sb.Append((n1Total > 0 || n2Total > 0) ? $" · N3: {n3Correct}/{n3Total}" : $"N3: {n3Correct}/{n3Total}");
        }

        return sb.ToString().TrimEnd();
    }
}
