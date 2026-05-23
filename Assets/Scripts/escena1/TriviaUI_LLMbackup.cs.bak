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

    [HideInInspector] public int LastSelectedIndex = -1;

    [Header("Lobby")]
    [SerializeField] private Transform _playerListContainer;
    [SerializeField] private GameObject _playerEntryPrefab;
    [SerializeField] private GameObject _botonStart;
    [SerializeField] private GameObject _botonRestartServer;
    [SerializeField] private GameObject _botonGenerarNuevas;
    [SerializeField] private GameObject _botonUsarLocales;
    
    [Header("Post-Carrera")]
    [SerializeField] private GameObject _panelLlegaste;
    [SerializeField] private GameObject _panelPodio;
    [SerializeField] private TMP_Text _textoPodio;

    [Header("Retroalimentación IA (Final de Carrera)")]
    [SerializeField] private GameObject panelFeedbackFinal; 
    [SerializeField] private TMPro.TMP_Text textoMensajeGeneral;
    [SerializeField] private TMPro.TMP_Text textoFortalezas;
    [SerializeField] private TMPro.TMP_Text textoMejoras;
    [SerializeField] private Button _botonVerFeedback;
    [SerializeField] private TMP_Text _textoBotonFeedback;

    private FeedbackData _cachedFeedbackData;
    private bool _esperandoFeedback = false;

    private void Awake() 
    {
        Instance = this;
        if (_panelPrincipal != null) _panelPrincipal.SetActive(false);
        if (_panelLobby != null) _panelLobby.SetActive(false);
        if (_panelLlegaste != null) _panelLlegaste.SetActive(false);
        if (_panelPodio != null) _panelPodio.SetActive(false);
    }

    private void Start()
    {
        if (_botonStart != null) _botonStart.SetActive(false);
        if (_botonRestartServer != null) _botonRestartServer.SetActive(false);
        if (_botonGenerarNuevas != null) _botonGenerarNuevas.SetActive(false);

        NetworkRunner runner = FindAnyObjectByType<NetworkRunner>();
        if (runner != null)
        {
            UpdateLobbyUI(runner);
        }
    }

    private void Update()
    {
        if (_esperandoFeedback && GameManager.Instance != null && Player.Local != null)
        {
            if (_textoBotonFeedback != null)
            {
                // Si el N° de feedbacks completados es >= a la gente que llegó antes que yo, es mi turno
                if (GameManager.Instance.FeedbacksCompleted >= Player.Local.PlayerRank - 1)
                {
                    _textoBotonFeedback.text = "Generando feedback...";
                }
                else
                {
                    _textoBotonFeedback.text = "Esperando para generar feedback...";
                }
            }
        }
    }

    public void RegistrarFinDeCarreraLocal(int score, int total)
    {
        _cachedFeedbackData = null;
        _esperandoFeedback = true;
        
        if (_botonVerFeedback != null)
        {
            _botonVerFeedback.interactable = false;
            if (_textoBotonFeedback != null) _textoBotonFeedback.text = " Profesor corrigiendo...";
        }

        // Disparamos la corrutina HTTP de tu QuestionManager de forma asíncrona
        if (QuestionManager.Instance != null)
        {
            QuestionManager.Instance.SolicitarFeedbackFinal(score, total);
        }
    }
    
    public void ShowFeedback(FeedbackData data)
    {
        _cachedFeedbackData = data; // Almacenamos el reporte académico en la memoria caché
        _esperandoFeedback = false;

            if (_botonVerFeedback != null) _botonVerFeedback.interactable = true; // ¡Desbloqueado! El jugador ya puede pulsar el botón
            if (_textoBotonFeedback != null) _textoBotonFeedback.text = "Ver Evaluación IA";
            if (GameManager.Instance != null) GameManager.Instance.RPC_FeedbackCompletado();
    }

    public void UI_BotonMostrarPanelFeedbackOculto()
    {
        if (_cachedFeedbackData == null) return;

        if (panelFeedbackFinal != null) panelFeedbackFinal.SetActive(true);
        if (_panelLlegaste != null) _panelLlegaste.SetActive(false);

        if (textoMensajeGeneral != null && !string.IsNullOrEmpty(_cachedFeedbackData.mensaje_general)) 
            textoMensajeGeneral.text = _cachedFeedbackData.mensaje_general;

        // Mapeo bilingüe tolerante a fallos
        string[] fuertes = (_cachedFeedbackData.fortalezas != null && _cachedFeedbackData.fortalezas.Length > 0) ? _cachedFeedbackData.fortalezas : _cachedFeedbackData.strengths;
        string[] mejoras = (_cachedFeedbackData.areas_mejora != null && _cachedFeedbackData.areas_mejora.Length > 0) ? _cachedFeedbackData.areas_mejora : _cachedFeedbackData.weaknesses;

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
        if (_botonGenerarNuevas != null)
        {
            _botonGenerarNuevas.SetActive(true);
            TMP_Text btnText = _botonGenerarNuevas.GetComponentInChildren<TMP_Text>();
            if (btnText != null) btnText.text = "Generar Preguntas (IA)";
        }
        
        if (_botonUsarLocales != null) _botonUsarLocales.SetActive(true); 
        
        if (_botonRestartServer != null) _botonRestartServer.SetActive(true);
    }

    public void UI_BotonGenerarNuevas()
    {
        // Añadimos las validaciones de null para que no haya crasheos
        if (_botonGenerarNuevas != null) _botonGenerarNuevas.SetActive(false);
        
        if (_botonStart != null)
        {
            _botonStart.SetActive(true);
            
            // Bloqueamos el de Start temporalmente mientras la IA piensa
            Button btn = _botonStart.GetComponent<Button>();
            if (btn != null) btn.interactable = false;
            
            TMP_Text btnText = _botonStart.GetComponentInChildren<TMP_Text>();
            if (btnText != null) btnText.text = "IA Pensando...";
        }

        if (QuestionManager.Instance != null) QuestionManager.Instance.RequestNewGeneration();
    }

    public void UI_BotonIniciarPartida()
    {
        // Ocultamos el lobby para que quede limpio
        if (_panelLobby != null) _panelLobby.SetActive(false);

        // Llamamos al GameManager que pasaste en tu código
        if (GameManager.Instance != null)
        {
            GameManager.Instance.UI_BotonIniciarPartida();
        }
        else
        {
            Debug.LogError("Error: GameManager no está listo.");
        }
    }

    public void ShowQuestion(Question q)
    {
        _panelPrincipal.SetActive(true);
        _preguntaText.text = q.question;

        for (int i = 0; i < _opcionesTexts.Length; i++)
        {
            _opcionesTexts[i].text = q.options[i];
        }
    }

    public void Hide()
    {
        _panelPrincipal.SetActive(false);
    }

    public void UpdateTimer(float time)
    {
        if (_timerText == null) return;

        if (time >= 0)
        {
            _timerText.text = time.ToString("F1") + "s";
            _timerText.color = Color.white; // Tiempo normal
        }
        else
        {
            // Si es negativo, lo pasamos a positivo para mostrarlo en pantalla
            float cuentaAtrasSiguiente = 5f + time;
            cuentaAtrasSiguiente = Mathf.Max(0, cuentaAtrasSiguiente);

            _timerText.text = "Siguiente en: " + cuentaAtrasSiguiente.ToString("F1") + "s";
            _timerText.color = Color.yellow; // Cambiamos el color para indicar que es tiempo de espera
        }
    }

    // Asignar los 4 botones en el Inspector (pasando 0, 1, 2, 3)
    public void OnOptionClicked(int index)
    {
        LastSelectedIndex = index;
        Debug.Log("Opción seleccionada localmente: " + index);
    }

    public void UpdateLobbyUI(NetworkRunner runner)
    {
        if (_panelLobby != null) _panelLobby.SetActive(true);
        
        // 1. Solo el Host ve el botón de Start
        if (_botonStart != null)
        {
            _botonStart.SetActive(runner.IsServer || runner.IsSharedModeMasterClient);
        }

        // 2. Limpiar lista actual
        foreach (Transform child in _playerListContainer) Destroy(child.gameObject);

        // 3. Mostrar todos los jugadores conectados
        foreach (var player in runner.ActivePlayers)
        {
            GameObject entry = Instantiate(_playerEntryPrefab, _playerListContainer);
            entry.GetComponent<TMP_Text>().text = $"Jugador {player.PlayerId}";
        }
    }
    
    public void OnConnectionError()
    {
        if (_botonStart != null) _botonStart.SetActive(false);
        if (_botonRestartServer != null) _botonRestartServer.SetActive(true);
    }

    // Función 2: Cuando la IA ya tiene las preguntas listas
    public void OnQuestionsReady()
    {
        if (_botonStart != null)
        {
            _botonStart.SetActive(true);
            Button btn = _botonStart.GetComponent<Button>();
            if (btn != null) btn.interactable = true;
            
            TMP_Text btnText = _botonStart.GetComponentInChildren<TMP_Text>();
            if (btnText != null) btnText.text = "Iniciar Partida";
        }
        // También mostramos el de generar nuevas por si quieren cambiar la trivia actual
        if (_botonGenerarNuevas != null) _botonGenerarNuevas.SetActive(true);
        if (_botonUsarLocales != null) _botonUsarLocales.SetActive(true);
        if (_botonRestartServer != null) _botonRestartServer.SetActive(true);
    }

    // Función 3: Al hacer clic en el botón de reintentar
    public void UI_BotonReiniciarServidor()
    {
        var runner = FindAnyObjectByType<NetworkRunner>();
        if (runner != null)
        {
            runner.Shutdown();
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene("UI"); 
    }
    public void ShowWaiting()
    {
        // Solo lo activamos si no está activo ya (para no spamear)
        if (_panelLlegaste != null && !_panelLlegaste.activeSelf)
        {
            if (_panelPrincipal != null) _panelPrincipal.SetActive(false); // Oculta trivia
            _panelLlegaste.SetActive(true);
        }
    }

    public void ShowPodium()
    {
        // Muestra el podio final completo (se ejecuta de manera segura por compatibilidad)
        if (_panelPodio != null && !_panelPodio.activeSelf)
        {
            _panelPodio.SetActive(true);
        }
        UpdatePodiumLive();
    }

    public void UpdatePodiumLive()
    {
        // Encendemos el Canvas del podio de forma independiente apenas es llamado
        if (_panelPodio != null && !_panelPodio.activeSelf) 
            _panelPodio.SetActive(true);

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
        // Cuando el servidor avisa que empezó la partida, ocultamos el lobby
        if (_panelLobby != null) _panelLobby.SetActive(false);
    }
}