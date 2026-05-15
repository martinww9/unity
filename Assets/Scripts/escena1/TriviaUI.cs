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
    [SerializeField] private GameObject _panelEspera;
    [SerializeField] private GameObject _panelPodio;
    [SerializeField] private TMP_Text _textoPodio;

    private void Awake() 
    {
        Instance = this;
        if (_panelPrincipal != null) _panelPrincipal.SetActive(false);
        if (_panelLobby != null) _panelLobby.SetActive(false);
        if (_panelEspera != null) _panelEspera.SetActive(false);
        if (_panelPodio != null) _panelPodio.SetActive(false);
    }

    private void Start()
    {
        if (_botonStart != null) _botonStart.SetActive(false);
        if (_botonRestartServer != null) _botonRestartServer.SetActive(false);
        if (_botonGenerarNuevas != null) _botonGenerarNuevas.SetActive(false);
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
        _preguntaText.text = q.text;

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
        if (_panelEspera != null && !_panelEspera.activeSelf)
        {
            if (_panelPrincipal != null) _panelPrincipal.SetActive(false); // Oculta trivia
            _panelEspera.SetActive(true);
        }
    }

    public void ShowPodium()
    {
        // Solo lo calculamos la primera vez que se activa
        if (_panelPodio != null && !_panelPodio.activeSelf)
        {
            if (_panelEspera != null) _panelEspera.SetActive(false);
            _panelPodio.SetActive(true);

            GenerarTextoPodio();
        }
    }

    private void GenerarTextoPodio()
    {
        if (_textoPodio == null) return;

        // Buscamos a todos los jugadores en la escena
        Player[] todosLosJugadores = Object.FindObjectsByType<Player>(FindObjectsSortMode.None);
        
        // Filtramos solo los que llegaron y los ordenamos por su Rank (1, 2, 3...)
        var podioOrdenado = System.Linq.Enumerable.ToList(
            System.Linq.Enumerable.OrderBy(
                System.Linq.Enumerable.Where(todosLosJugadores, p => p.PlayerRank > 0), 
                p => p.PlayerRank
            )
        );

        string textoFinal = "<b><size=150%>¡CARRERA TERMINADA!</size></b>\n\n";

        foreach (var p in podioOrdenado)
        {
            // Usamos PlayerId para identificar si fuiste tú o los demás
            string etiquetaTu = (p.Object.HasInputAuthority) ? " (TÚ)" : "";

            textoFinal += $"{p.PlayerRank}º Lugar: Jugador {p.Object.InputAuthority.PlayerId}{etiquetaTu}\n";
        }

        _textoPodio.text = textoFinal;
    }

    public void UpdatePodiumLive()
    {
        if (_panelPodio == null) return;

        // Activamos el panel (ahora debe estar a un lateral)
        if (!_panelPodio.activeSelf) _panelPodio.SetActive(true);

        // Buscamos y ordenamos a los jugadores que han terminado
        Player[] todos = Object.FindObjectsByType<Player>(FindObjectsSortMode.None);
        
        // Ordenamos por rango (solo los que ya llegaron)
        var terminaron = System.Linq.Enumerable.ToList(
            System.Linq.Enumerable.OrderBy(
                System.Linq.Enumerable.Where(todos, p => p.PlayerRank > 0), 
                p => p.PlayerRank
            )
        );

        string contenido = "<b>POSICIONES</b>\n";
        foreach (var p in terminaron)
        {
            string tu = p.Object.HasInputAuthority ? " (TÚ)" : "";
            contenido += $"{p.PlayerRank}º - Jugador {p.Object.InputAuthority.PlayerId}{tu}\n";
        }

        if (_textoPodio != null) _textoPodio.text = contenido;
    }
    public void StartGameUI()
    {
        // Cuando el servidor avisa que empezó la partida, ocultamos el lobby
        if (_panelLobby != null) _panelLobby.SetActive(false);
    }
}