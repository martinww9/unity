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

    private void Awake()
    {
        Instance = this;
        if (_panelLobby != null) _panelLobby.SetActive(false);
        if (_panelPrincipal != null) _panelPrincipal.SetActive(false);
        if (_panelLlegaste != null) _panelLlegaste.SetActive(false);
        if (_panelPodio != null) _panelPodio.SetActive(false);
    }

    public void HideAllForMenu()
    {
        if (_panelLobby != null) _panelLobby.SetActive(false);
        if (_panelPrincipal != null) _panelPrincipal.SetActive(false);
        if (_panelLlegaste != null) _panelLlegaste.SetActive(false);
        if (_panelPodio != null) _panelPodio.SetActive(false);
        if (panelFeedbackFinal != null) panelFeedbackFinal.SetActive(false);
    }

    public bool IsLobbyVisible() => _panelLobby != null && _panelLobby.activeSelf;

    public bool BlocksMouseLook() =>
        (_panelPrincipal != null && _panelPrincipal.activeSelf)
        || (_panelLlegaste != null && _panelLlegaste.activeSelf)
        || (panelFeedbackFinal != null && panelFeedbackFinal.activeSelf);

    public void ShowLobby(NetworkRunner runner)
    {
        if (runner == null) return;

        Spawner.SetEscena1CanvasVisible("CanvasLobby", true);

        if (_panelLobby != null)
            _panelLobby.SetActive(true);

        UpdatePlayerList(runner);

        bool isHost = runner.IsServer || runner.IsSharedModeMasterClient;
        SetHostButtonsVisible(isHost);

        if (isHost)
            RefreshHostButtonStates();
    }

    private void SetHostButtonsVisible(bool visible)
    {
        if (_botonStart != null) _botonStart.SetActive(visible);
        if (_botonGenerarNuevas != null) _botonGenerarNuevas.SetActive(visible);
        if (_botonRestartServer != null) _botonRestartServer.SetActive(visible);
    }

    public void RefreshHostButtonStates()
    {
        NetworkRunner runner = FindAnyObjectByType<NetworkRunner>();
        if (runner == null) return;

        bool isHost = runner.IsServer || runner.IsSharedModeMasterClient;
        if (!isHost) return;

        SetHostButtonsVisible(true);

        bool ready = QuestionManager.Instance != null && QuestionManager.Instance.IsReady;

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
            _textoNivel.text = $"Nivel {level}/3";
    }

    public void RegistrarFinDeCarreraLocal(int score, int total)
    {
        _localScore = score;
        _localTotal = total;
        _cachedFeedbackData = null;
        _generandoFeedback = false;

        Spawner.SetEscena1CanvasVisible("CanvasFinCarrera", true);

        if (_panelPrincipal != null) _panelPrincipal.SetActive(false);
        if (_panelLlegaste != null) _panelLlegaste.SetActive(true);

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

        if (panelFeedbackFinal != null) panelFeedbackFinal.SetActive(true);
        if (_panelLlegaste != null) _panelLlegaste.SetActive(false);

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

    public void UI_BotonIniciarPartida()
    {
        if (_panelLobby != null) _panelLobby.SetActive(false);

        if (GameManager.Instance != null)
            GameManager.Instance.UI_BotonIniciarPartida();
        else
            Debug.LogError("Error: GameManager no está listo.");
    }

    public void ShowQuestion(Question q)
    {
        if (_panelPrincipal != null) _panelPrincipal.SetActive(true);
        if (_preguntaText != null) _preguntaText.text = q.question;

        for (int i = 0; i < _opcionesTexts.Length; i++)
            _opcionesTexts[i].text = q.options[i];
    }

    public void Hide()
    {
        if (_panelPrincipal != null) _panelPrincipal.SetActive(false);
    }

    public void UpdateTimer(float time)
    {
        if (_timerText == null) return;

        if (time >= 0)
        {
            _timerText.text = time.ToString("F1") + "s";
            _timerText.color = Color.white;
        }
        else
        {
            float cuentaAtrasSiguiente = Mathf.Max(0, 5f + time);
            _timerText.text = "Siguiente en: " + cuentaAtrasSiguiente.ToString("F1") + "s";
            _timerText.color = Color.yellow;
        }
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

        UnityEngine.SceneManagement.SceneManager.LoadScene("UI");
    }

    public void UI_BotonRestartServer() => UI_BotonReiniciarServidor();

    public void ShowWaiting()
    {
        if (_panelLlegaste != null && !_panelLlegaste.activeSelf)
        {
            Spawner.SetEscena1CanvasVisible("CanvasFinCarrera", true);
            if (_panelPrincipal != null) _panelPrincipal.SetActive(false);
            _panelLlegaste.SetActive(true);
        }
    }

    public void ShowPodiumForAll()
    {
        Spawner.SetEscena1CanvasVisible("CanvasPodio", true);

        if (_panelPodio != null)
            _panelPodio.SetActive(true);
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
        if (_panelLobby != null) _panelLobby.SetActive(false);

        Spawner.SetEscena1CanvasVisible("CanvasTimer", true);
        Spawner.SetEscena1CanvasVisible("CanvasPreguntas", true);

        if (Player.Local != null)
            UpdateLevelIndicator(Player.Local.CurrentLevel);
    }
}
