using System.Collections;
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
    [SerializeField] private TMP_Text _textoPuntajeLlegada;

    [Header("Retroalimentación IA (Final de Carrera)")]
    [SerializeField] private GameObject panelFeedbackFinal;
    [SerializeField] private TMP_Text textoMensajeGeneral;
    [SerializeField] private TMP_Text textoFortalezas;
    [SerializeField] private TMP_Text textoMejoras;
    [SerializeField] private Button _botonVerFeedback;
    [SerializeField] private TMP_Text _textoBotonFeedback;

    private FeedbackData _cachedFeedbackData;
    private bool _generandoPreguntas = false;
    private bool _generandoFeedback = false;
    private int _localScore;
    private int _localTotal;
    private Coroutine _refreshLobbyCoroutine;
    private NetworkRunner _lobbyRunner;
    private float _lastLobbyDiagLogTime = -10f;

    private void Awake()
    {
        Instance = this;
        HideAllLocalPanels();
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
        HideAllLocalPanels();
        if (_panelPrincipal != null) _panelPrincipal.SetActive(true);
    }

    private void ShowOnlyFinishPanel()
    {
        HideAllLocalPanels();
        if (_panelLlegaste != null) _panelLlegaste.SetActive(true);
    }

    private void ShowOnlyPodiumPanel()
    {
        HideAllLocalPanels();
        if (_panelPodio != null) _panelPodio.SetActive(true);
    }

    private void ShowOnlyFeedbackPanel()
    {
        HideAllLocalPanels();
        if (panelFeedbackFinal != null) panelFeedbackFinal.SetActive(true);
    }

    public void HideAllForMenu()
    {
        HideAllLocalPanels();
    }

    public bool IsLobbyVisible() => _panelLobby != null && _panelLobby.activeSelf;

    public bool BlocksMouseLook() =>
        (_panelLobby != null && _panelLobby.activeSelf)
        || (_panelPrincipal != null && _panelPrincipal.activeSelf)
        || (_panelLlegaste != null && _panelLlegaste.activeSelf)
        || (_panelPodio != null && _panelPodio.activeSelf)
        || (panelFeedbackFinal != null && panelFeedbackFinal.activeSelf);

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

        UpdatePlayerList(runner);

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

    private void UpdatePlayerList(NetworkRunner runner)
    {
        if (_playerListContainer == null || _playerEntryPrefab == null) return;

        foreach (Transform child in _playerListContainer) Destroy(child.gameObject);

        foreach (var player in runner.ActivePlayers)
        {
            GameObject entry = Instantiate(_playerEntryPrefab, _playerListContainer);
            TMP_Text text = entry.GetComponent<TMP_Text>();
            if (text != null) text.text = $"Jugador {player.PlayerId}";
        }
    }

    public void UpdateLevelIndicator(int level)
    {
        if (_textoNivel != null)
            _textoNivel.text = LevelTopics.FormatIndicator(level);
    }

    public void RegistrarFinDeCarreraLocal(int score, int total)
    {
        _localScore = score;
        _localTotal = total;
        _cachedFeedbackData = null;
        _generandoFeedback = false;

        if (JuegoUI.Instance != null)
            JuegoUI.Instance.ShowFinCarreraPhase();
        else
        {
            Spawner.SetJuegoCanvasVisible("CanvasLobby", false);
            Spawner.SetJuegoCanvasVisible("CanvasTimer", false);
            Spawner.SetJuegoCanvasVisible("CanvasPreguntas", false);
            Spawner.SetJuegoCanvasVisible("CanvasPodio", false);
            Spawner.SetJuegoCanvasVisible("CanvasFinCarrera", true);
        }

        ShowOnlyFinishPanel();

        if (_textoPuntajeLlegada != null)
            _textoPuntajeLlegada.text = $"Puntaje final: {score}/{total}";

        if (_botonVerFeedback != null)
        {
            _botonVerFeedback.interactable = true;
            if (_textoBotonFeedback != null)
                _textoBotonFeedback.text = "Solicitar evaluación IA";
        }
    }

    public void UI_BotonSolicitarFeedback()
    {
        if (_generandoFeedback || QuestionManager.Instance == null) return;

        _generandoFeedback = true;
        _cachedFeedbackData = null;

        if (_botonVerFeedback != null)
            _botonVerFeedback.interactable = false;
        if (_textoBotonFeedback != null)
            _textoBotonFeedback.text = "Generando...";

        QuestionManager.Instance.SolicitarFeedbackFinal(_localScore, _localTotal, 3);
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
        _generandoFeedback = false;

        if (_botonVerFeedback != null) _botonVerFeedback.interactable = true;
        if (_textoBotonFeedback != null) _textoBotonFeedback.text = "Ver evaluación IA";
        if (GameManager.Instance != null) GameManager.Instance.RPC_FeedbackCompletado();
    }

    public void OnFeedbackError()
    {
        _generandoFeedback = false;
        if (_botonVerFeedback != null) _botonVerFeedback.interactable = true;
        if (_textoBotonFeedback != null) _textoBotonFeedback.text = "Reintentar evaluación IA";
    }

    public void UI_BotonMostrarPanelFeedbackOculto()
    {
        if (_cachedFeedbackData == null) return;

        if (JuegoUI.Instance != null)
            JuegoUI.Instance.ShowFinCarreraPhase();
        else
        {
            Spawner.SetJuegoCanvasVisible("CanvasLobby", false);
            Spawner.SetJuegoCanvasVisible("CanvasTimer", false);
            Spawner.SetJuegoCanvasVisible("CanvasPreguntas", false);
            Spawner.SetJuegoCanvasVisible("CanvasPodio", false);
            Spawner.SetJuegoCanvasVisible("CanvasFinCarrera", true);
        }

        ShowOnlyFeedbackPanel();

        if (textoMensajeGeneral != null && !string.IsNullOrEmpty(_cachedFeedbackData.mensaje_general))
            textoMensajeGeneral.text = _cachedFeedbackData.mensaje_general;

        string[] fuertes = (_cachedFeedbackData.fortalezas != null && _cachedFeedbackData.fortalezas.Length > 0)
            ? _cachedFeedbackData.fortalezas
            : _cachedFeedbackData.strengths;
        string[] mejoras = (_cachedFeedbackData.areas_mejora != null && _cachedFeedbackData.areas_mejora.Length > 0)
            ? _cachedFeedbackData.areas_mejora
            : _cachedFeedbackData.weaknesses;

        if (textoFortalezas != null && fuertes != null && fuertes.Length > 0)
            textoFortalezas.text = "<b>Puntos Fuertes:</b>\n- " + string.Join("\n- ", fuertes);
        else if (textoFortalezas != null)
            textoFortalezas.text = "<b>Puntos Fuertes:</b>\n- Buen rendimiento conceptual general.";

        if (textoMejoras != null && mejoras != null && mejoras.Length > 0)
            textoMejoras.text = "<b>Áreas Recomendadas:</b>\n- " + string.Join("\n- ", mejoras);
        else if (textoMejoras != null)
            textoMejoras.text = "<b>Áreas Recomendadas:</b>\n- No se detectaron debilidades críticas, ¡sigue así!";
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
        if ((panelFeedbackFinal != null && panelFeedbackFinal.activeSelf) ||
            (_panelPodio != null && _panelPodio.activeSelf))
            return;

        if (_panelLlegaste != null && !_panelLlegaste.activeSelf)
        {
            if (JuegoUI.Instance != null)
                JuegoUI.Instance.ShowFinCarreraPhase();
            else
            {
                Spawner.SetJuegoCanvasVisible("CanvasLobby", false);
                Spawner.SetJuegoCanvasVisible("CanvasTimer", false);
                Spawner.SetJuegoCanvasVisible("CanvasPreguntas", false);
                Spawner.SetJuegoCanvasVisible("CanvasPodio", false);
                Spawner.SetJuegoCanvasVisible("CanvasFinCarrera", true);
            }
            ShowOnlyFinishPanel();
        }
    }

    public void ShowPodiumForAll()
    {
        if (JuegoUI.Instance != null)
            JuegoUI.Instance.ShowPodiumPhase();
        else
        {
            Spawner.SetJuegoCanvasVisible("CanvasLobby", false);
            Spawner.SetJuegoCanvasVisible("CanvasTimer", false);
            Spawner.SetJuegoCanvasVisible("CanvasPreguntas", false);
            Spawner.SetJuegoCanvasVisible("CanvasFinCarrera", false);
            Spawner.SetJuegoCanvasVisible("CanvasPodio", true);
        }

        ShowOnlyPodiumPanel();
        UpdatePodiumLive();
    }

    public void UpdatePodiumLive()
    {
        Player[] todos = Object.FindObjectsByType<Player>(FindObjectsSortMode.None);
        var terminaron = System.Linq.Enumerable.ToList(
            System.Linq.Enumerable.OrderBy(
                System.Linq.Enumerable.Where(todos, p => p.PlayerRank > 0),
                p => p.PlayerRank
            )
        );

        string contenido = "";
        foreach (var p in terminaron)
        {
            string tu = (Player.Local != null && p.Object.InputAuthority == Player.Local.Object.InputAuthority) ? " (TÚ)" : "";
            contenido += $"{p.PlayerRank}º Lugar - Jugador {p.Object.InputAuthority.PlayerId}{tu}\n";
        }

        if (_textoPodio != null) _textoPodio.text = contenido;
    }

    public void StartGameUI()
    {
        HideAllLocalPanels();

        if (JuegoUI.Instance != null)
            JuegoUI.Instance.ShowGameplayPhase();
        else
        {
            Spawner.SetJuegoCanvasVisible("CanvasLobby", false);
            Spawner.SetJuegoCanvasVisible("CanvasPodio", false);
            Spawner.SetJuegoCanvasVisible("CanvasFinCarrera", false);
            Spawner.SetJuegoCanvasVisible("CanvasTimer", true);
            Spawner.SetJuegoCanvasVisible("CanvasPreguntas", true);
        }

        if (Player.Local != null)
            UpdateLevelIndicator(Player.Local.CurrentLevel);
    }
}
