using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;


public class Spawner : MonoBehaviour, INetworkRunnerCallbacks
{
    private const int MenuCanvasSortOrder = 100;

    private static readonly string[] JuegoUiCanvasNames =
    {
        "CanvasLobby",
        "CanvasTimer",
        "CanvasPreguntas",
        "CanvasPodio",
        "CanvasFinCarrera"
    };

    [SerializeField] private NetworkPrefabRef _playerPrefab;
    [SerializeField] private GameObject _menuCamera;

    [Header("Menú UI")]
    [SerializeField] private GameObject _canvasMenu;
    [SerializeField] private GameObject _panelInicio;
    [SerializeField] private GameObject _panelCrearSala;
    [SerializeField] private GameObject _panelBrowser;

    [Header("Referencias de Creación")]
    [SerializeField] private TMP_InputField _inputNombreJugador;
    [SerializeField] private TMP_InputField _inputNombreSala;

    [Header("Referencias de Browser")]
    [SerializeField] private GameObject _roomListPanel;
    [SerializeField] private Transform _roomListContent;
    [SerializeField] private GameObject _roomButtonPrefab; // Un botón con un script simple para unirse
    
    [Header("Configuración del Mapa")]
    [SerializeField] private Transform[] _spawnPoints;

    private Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();
    private NetworkRunner _runner;
    private NetworkSceneManagerDefault _sceneManager;
    private bool _mouseButton0;
    private bool _callbacksRegistered;
    private bool _inJuegoSession;
    private bool _suppressReturnToMenuOnShutdown;
    private bool _isStartingGame;

    private static bool IsAltHeld()
    {
        var keyboard = Keyboard.current;
        return keyboard != null &&
               (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed);
    }

    private bool AllowMouseLook()
    {
        if (!_inJuegoSession) return false;
        if (IsAltHeld()) return false;
        if (TriviaUI.Instance != null && TriviaUI.Instance.BlocksMouseLook())
            return false;
        return true;
    }

    private void ApplyCursorState()
    {
        if (!Application.isFocused)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        if (!_inJuegoSession)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        bool uiBlocksMouseLook = TriviaUI.Instance != null && TriviaUI.Instance.BlocksMouseLook();
        if (IsAltHeld() || uiBlocksMouseLook)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void EnterJuegoSession()
    {
        _inJuegoSession = true;

        SetMenuCanvasPriority(false);
        if (_canvasMenu != null) _canvasMenu.SetActive(false);
        if (_menuCamera != null) _menuCamera.SetActive(false);
    }

    private void ReturnToMenuScene()
    {
        _inJuegoSession = false;
        _spawnedCharacters.Clear();

        SetJuegoGameUIVisible(false);
        TryUnloadJuegoScene();

        if (_canvasMenu != null) _canvasMenu.SetActive(true);
        SetMenuCanvasPriority(true);
        if (_menuCamera != null) _menuCamera.SetActive(true);
        OcultarTodosLosPaneles();
        if (_panelInicio != null) _panelInicio.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public static void SetJuegoGameUIVisible(bool visible)
    {
        if (!visible)
        {
            if (JuegoUI.Instance != null)
                JuegoUI.Instance.HideAllCanvases();
            else if (TriviaUI.Instance != null)
                TriviaUI.Instance.HideAllForMenu();

            foreach (string canvasName in JuegoUiCanvasNames)
                SetJuegoCanvasVisible(canvasName, false);
            return;
        }

        foreach (string canvasName in JuegoUiCanvasNames)
            SetJuegoCanvasVisible(canvasName, true);
    }

    public static void SetJuegoCanvasVisible(string canvasName, bool visible)
    {
        if (JuegoUI.Instance != null)
            JuegoUI.Instance.SetCanvasByName(canvasName, visible);
        else
        {
            var canvas = GameObject.Find(canvasName);
            if (canvas != null)
                canvas.SetActive(visible);
        }
    }

    private void SetMenuCanvasPriority(bool menuOnTop)
    {
        if (_canvasMenu == null) return;
        var canvas = _canvasMenu.GetComponent<Canvas>();
        if (canvas != null)
            canvas.sortingOrder = menuOnTop ? MenuCanvasSortOrder : 0;
    }

    private void Awake()
    {
        EnsureSceneManager();
    }

    private void EnsureSceneManager()
    {
        if (_sceneManager != null) return;

        foreach (var sceneManager in GetComponents<NetworkSceneManagerDefault>())
        {
            if (sceneManager != null)
            {
                _sceneManager = sceneManager;
                break;
            }
        }

        if (_sceneManager == null)
            _sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();
    }

    private void PruneDuplicateSceneManagers()
    {
        foreach (var sceneManager in GetComponents<NetworkSceneManagerDefault>())
        {
            if (sceneManager != null && sceneManager != _sceneManager)
                Destroy(sceneManager);
        }
    }

    private void ShutdownSceneManagerOnly()
    {
        EnsureSceneManager();
        if (_sceneManager != null)
            _sceneManager.Shutdown();
    }

    private void DestroyRunnerComponents()
    {
        if (this == null) return;
        foreach (var runner in GetComponents<NetworkRunner>())
        {
            if (runner != null)
                Destroy(runner);
        }

        ShutdownSceneManagerOnly();
        PruneDuplicateSceneManagers();

        _runner = null;
        _callbacksRegistered = false;
    }

    private NetworkRunner GetOrCreateRunner()
    {
        if (_runner == null)
            _runner = GetComponent<NetworkRunner>();

        if (_runner != null && _runner.IsRunning)
            return _runner;

        if (_runner == null)
            _runner = gameObject.AddComponent<NetworkRunner>();

        _runner.ProvideInput = true;
        RegisterRunnerCallbacks(_runner);
        return _runner;
    }

    private NetworkSceneManagerDefault GetOrAddSceneManager()
    {
        EnsureSceneManager();
        PruneDuplicateSceneManagers();
        return _sceneManager;
    }

    private async Task ShutdownRunnerForGameTransitionAsync()
    {
        if (_runner == null)
            _runner = GetComponent<NetworkRunner>();

        if (_runner == null || !_runner.IsRunning)
            return;

        _suppressReturnToMenuOnShutdown = true;
        await _runner.Shutdown();

        for (int i = 0; i < 120; i++)
        {
            if (_runner == null || !_runner.IsRunning)
                break;
            await Task.Delay(50);
        }
    }

    private static void TryUnloadJuegoScene()
    {
        Scene juego = SceneManager.GetSceneByName(SceneNames.Juego);
        if (juego.IsValid() && juego.isLoaded)
            SceneManager.UnloadSceneAsync(juego);
    }

    private void LateUpdate()
    {
        ApplyCursorState();
    }

    private void RegisterRunnerCallbacks(NetworkRunner runner)
    {
        if (runner == null || _callbacksRegistered) return;
        runner.AddCallbacks(this);
        _callbacksRegistered = true;
    }

    private bool TryGetSpawnTransform(int playerIndex, out Vector3 position, out Quaternion rotation)
    {
        if (LevelManager.Instance != null)
        {
            Transform levelSpawn = LevelManager.Instance.GetSpawnPoint(1);
            if (levelSpawn != null)
            {
                position = levelSpawn.position;
                rotation = levelSpawn.rotation;
                return true;
            }
        }

        if (_spawnPoints != null && _spawnPoints.Length > 0)
        {
            int index = playerIndex % _spawnPoints.Length;
            Transform fallback = _spawnPoints[index];
            if (fallback != null)
            {
                position = fallback.position;
                rotation = fallback.rotation;
                return true;
            }
        }

        position = new Vector3((playerIndex % 8) * 3f, 0.001f, 0f);
        rotation = Quaternion.identity;
        return false;
    }

    private void SpawnOrRepositionPlayer(NetworkRunner runner, PlayerRef player)
    {
        TryGetSpawnTransform(player.RawEncoded, out Vector3 spawnPosition, out Quaternion spawnRotation);

        if (_spawnedCharacters.TryGetValue(player, out NetworkObject existing) && existing != null)
        {
            if (existing.TryGetComponent<NetworkCharacterController>(out var cc))
                cc.Teleport(spawnPosition, spawnRotation);
            return;
        }

        NetworkObject networkPlayerObject = runner.Spawn(_playerPrefab, spawnPosition, spawnRotation, player);
        _spawnedCharacters[player] = networkPlayerObject;

        if (QuestionManager.Instance != null && QuestionManager.Instance.IsReady)
            QuestionManager.Instance.SincronizarConNuevoJugador(player);
    }

    private void OcultarTodosLosPaneles() {
        if (_panelInicio != null) _panelInicio.SetActive(false);
        if (_panelCrearSala != null) _panelCrearSala.SetActive(false);
        if (_panelBrowser != null) _panelBrowser.SetActive(false);
    }

    public void UI_CreateRoom(string roomName)
    {
        StartGame(GameMode.Host, roomName);
    }

    public void UI_IrACrearSala() {
        OcultarTodosLosPaneles();
        if (_panelCrearSala != null)
            _panelCrearSala.SetActive(true);
    }

    public async void UI_IrABrowser() {
        OcultarTodosLosPaneles();
        if (_panelBrowser != null) _panelBrowser.SetActive(true);

        var runner = GetOrCreateRunner();

        Debug.Log("Conectando al Lobby de Fusion para buscar salas...");
        await runner.JoinSessionLobby(SessionLobby.ClientServer);
    }

    public void UI_BotonVolver()
    {
        SetJuegoGameUIVisible(false);
        TryUnloadJuegoScene();

        if (_runner != null && _runner.IsRunning)
            _runner.Shutdown();
        else
        {
            DestroyRunnerComponents();
            ReturnToMenuScene();
        }
    }

    public async void UI_BotonRefrescarSalas()
    {
        Debug.Log("Refrescando lista de salas...");
        
        // Vaciamos la lista visual al instante para que el jugador note que se refrescó
        foreach (Transform child in _roomListContent) Destroy(child.gameObject);

        if (_runner != null)
        {
            // Volvemos a pedirle a Photon que nos meta al lobby. 
            // Esto forzará que nos envíe la lista más actualizada en un par de segundos.
            await _runner.JoinSessionLobby(SessionLobby.ClientServer);
        }
    }

    public void UI_ConfirmarHost() {
        string nombre = string.IsNullOrEmpty(_inputNombreSala.text) ? "SalaTest" : _inputNombreSala.text;
         StartGame(GameMode.Host, nombre);
    }

    public void UI_VolverAlMenu() {
        SetJuegoGameUIVisible(false);
        TryUnloadJuegoScene();

        if (_runner != null && _runner.IsRunning)
            _runner.Shutdown();
        else
            ReturnToMenuScene();
    }

    public void UI_StartHost()
    {
        StartGame(GameMode.Host);
    }

    public void UI_StartClient()
    {
        StartGame(GameMode.Client);
    }


    private void Start()
    {
        _inJuegoSession = false;
        SetJuegoGameUIVisible(false);
        OcultarTodosLosPaneles();
        ResolvePlayerNameInput();

        if (_panelInicio != null)
            _panelInicio.SetActive(true);

        PlayerNameStorage.Clear();
        if (_inputNombreJugador != null)
            _inputNombreJugador.text = "";

        SetMenuCanvasPriority(true);
    }

    private void ResolvePlayerNameInput()
    {
        if (_inputNombreJugador != null || _panelInicio == null)
            return;

        foreach (var input in _panelInicio.GetComponentsInChildren<TMP_InputField>(true))
        {
            if (input.gameObject.name == "InputFieldNombreJugador")
            {
                _inputNombreJugador = input;
                return;
            }
        }
    }

    private void Update()
    {
        if (Mouse.current != null)
            _mouseButton0 = _mouseButton0 | Mouse.current.leftButton.isPressed;
    }
    
    public async void StartGame(GameMode mode, string roomName = "TestRoom")
    {
        if (_isStartingGame)
            return;

        _isStartingGame = true;
        try
        {
            ResolvePlayerNameInput();
            if (_inputNombreJugador != null)
                PlayerNameStorage.Set(_inputNombreJugador.text);

            await ShutdownRunnerForGameTransitionAsync();

            var runner = GetOrCreateRunner();

            DontDestroyOnLoad(gameObject);

            SceneRef? sceneToLoad = null;
            if (mode != GameMode.Client)
                sceneToLoad = SceneRef.FromIndex(1);

            var result = await runner.StartGame(new StartGameArgs()
            {
                GameMode = mode,
                SessionName = roomName,
                Scene = sceneToLoad,
                SceneManager = GetOrAddSceneManager()
            });

            if (!result.Ok)
            {
                Debug.LogError($"Fallo al iniciar el juego: {result.ShutdownReason}");
                DestroyRunnerComponents();
                ReturnToMenuScene();
            }
        }
        finally
        {
            _suppressReturnToMenuOnShutdown = false;
            _isStartingGame = false;
        }
    }

    void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
            SpawnOrRepositionPlayer(runner, player);

        if (_inJuegoSession && TriviaUI.Instance != null)
            TriviaUI.Instance.UpdateLobbyUI(runner);
    }
    void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (_spawnedCharacters.TryGetValue(player, out NetworkObject networkObject))
        {
            runner.Despawn(networkObject);
            _spawnedCharacters.Remove(player);
        }
        if (_inJuegoSession && TriviaUI.Instance != null)
            TriviaUI.Instance.UpdateLobbyUI(runner);
    }
    void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input)
    {
        // 1. Creamos la estructura base y FORZAMOS que la respuesta por defecto sea -1
        var data = new NetworkInputData();
        data.SelectedAnswerIndex = -1;

        // 2. Si la ventana está minimizada o en segundo plano, enviamos los datos limpios (sin auto-clicks)
        if (!Application.isFocused) 
        {
            input.Set(data); 
            return;
        }
    
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;

        if (keyboard != null)
        {
            // Movimiento WASD
            if (keyboard.wKey.isPressed) data.Direction += Vector3.forward;
            if (keyboard.sKey.isPressed) data.Direction += Vector3.back;
            if (keyboard.aKey.isPressed) data.Direction += Vector3.left;
            if (keyboard.dKey.isPressed) data.Direction += Vector3.right;
            
            // Clics y Sprint
            data.Buttons.Set(NetworkInputData.MouseButton0, _mouseButton0);
            _mouseButton0 = false;
            
            data.Buttons.Set(NetworkInputData.SprintButton, keyboard.leftShiftKey.isPressed);
        }

        if (AllowMouseLook() && mouse != null)
        {
            Vector2 mouseDelta = mouse.delta.ReadValue();
            data.lookRotationDeltaX = mouseDelta.x * 0.1f;
            data.lookRotationDeltaY = mouseDelta.y * 0.1f;
        }

        // 3. Lógica de Trivia: Solo sobreescribimos el -1 si el jugador realmente hizo clic
        if (TriviaUI.Instance != null && TriviaUI.Instance.LastSelectedIndex != -1)
        {
            data.SelectedAnswerIndex = TriviaUI.Instance.LastSelectedIndex;
            TriviaUI.Instance.LastSelectedIndex = -1; // Lo consumimos
        }

        input.Set(data);
    }

    void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        if (this == null) return;

        if (!_suppressReturnToMenuOnShutdown)
            DestroyRunnerComponents();

        if (!_suppressReturnToMenuOnShutdown)
            ReturnToMenuScene();
    }
    void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) { }
    void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        Debug.Log($"[Lobby] Servidor reporta {sessionList.Count} salas activas.");
        
        // 1. Limpiamos los botones viejos
        foreach (Transform child in _roomListContent) Destroy(child.gameObject);

        // 2. Creamos los botones nuevos
        foreach (var session in sessionList)
        {
            // Solo mostramos la sala si es visible y tiene espacio
            if (session.IsVisible && session.IsOpen)
            {
                Debug.Log($"-> Sala encontrada: {session.Name} | Jugadores: {session.PlayerCount}/{session.MaxPlayers}");
                
                GameObject btnObj = Instantiate(_roomButtonPrefab, _roomListContent, false);
                btnObj.transform.localScale = Vector3.one;
                btnObj.transform.localPosition = Vector3.zero;
                RoomButton script = btnObj.GetComponent<RoomButton>();
                if (script != null) script.Setup(session, this);
            }
        }
    }
    
    void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner) {
        Debug.Log("Entramos a la sesión. Mostrando Lobby.");

        EnterJuegoSession();
        OcultarTodosLosPaneles();

        if (runner.IsServer)
        {
            foreach (var player in runner.ActivePlayers)
                SpawnOrRepositionPlayer(runner, player);
        }

        if (runner.IsServer && QuestionManager.Instance != null)
            QuestionManager.Instance.EnsureHostQuestionsLoaded();

        if (TriviaUI.Instance != null)
        {
            TriviaUI.Instance.ShowLobby(runner);
            TriviaUI.Instance.RefreshLobbyWhenReady();
        }
    }
    void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner) { }
    void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
}