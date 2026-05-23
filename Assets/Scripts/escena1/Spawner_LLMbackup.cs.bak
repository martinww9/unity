using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;


public class Spawner : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkPrefabRef _playerPrefab;
    [SerializeField] private GameObject _menuCanvas;
    [SerializeField] private GameObject _menuCamera;
    
    [Header("Paneles de Navegación")]   
    [SerializeField] private GameObject _panelPrincipal;
    [SerializeField] private GameObject _panelCrearSala;
    [SerializeField] private GameObject _panelBrowser;

    [Header("Referencias de Creación")]
    [SerializeField] private TMP_InputField _inputNombreSala;

    [Header("Referencias de Browser")]
    [SerializeField] private GameObject _roomListPanel;
    [SerializeField] private Transform _roomListContent;
    [SerializeField] private GameObject _roomButtonPrefab; // Un botón con un script simple para unirse
    
    [Header("Configuración del Mapa")]
    [SerializeField] private Transform[] _spawnPoints;

    private Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();
    private NetworkRunner _runner;
    private bool _mouseButton0;

    private void OcultarTodosLosPaneles() {
        _panelPrincipal.SetActive(false);
        _panelCrearSala.SetActive(false);
        _panelBrowser.SetActive(false);
    }

    public void UI_CreateRoom(string roomName)
    {
        StartGame(GameMode.Host, roomName);
    }

    public void UI_IrACrearSala() {
        OcultarTodosLosPaneles();
        _panelCrearSala.SetActive(true);
    }

    public async void UI_IrABrowser() {
        OcultarTodosLosPaneles();
        if (_panelBrowser != null) _panelBrowser.SetActive(true);

        if (_runner == null) 
        {
            _runner = gameObject.AddComponent<NetworkRunner>();
            _runner.ProvideInput = true;
        }

        Debug.Log("Conectando al Lobby de Fusion para buscar salas...");
        // Esto conectará al cliente al servidor maestro para recibir la lista
        await _runner.JoinSessionLobby(SessionLobby.ClientServer);
    }

    public void UI_BotonVolver()
    {
        OcultarTodosLosPaneles();
        
        // Volvemos a mostrar el menú principal
        if (_panelPrincipal != null) _panelPrincipal.SetActive(true);

        // Si estábamos en el buscador de salas, apagamos el Runner para no gastar 
        // recursos en segundo plano escuchando a la red.
        if (_runner != null)
        {
            _runner.Shutdown();
            _runner = null;
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
        if (_runner != null) _runner.Shutdown(); // Detener búsqueda si volvemos
        OcultarTodosLosPaneles();
        _panelPrincipal.SetActive(true);
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
        OcultarTodosLosPaneles();
        
        if (_panelPrincipal != null)
        {
            _panelPrincipal.SetActive(true);
        }
    }

    private void Update()
    {
        if (Mouse.current != null)
            _mouseButton0 = _mouseButton0 | Mouse.current.leftButton.isPressed;
    }
    
    public async void StartGame(GameMode mode, string roomName = "TestRoom")
    {
        if (_runner == null)
        {
            _runner = gameObject.AddComponent<NetworkRunner>();
            _runner.ProvideInput = true;
        }

        DontDestroyOnLoad(gameObject);

        // Create the NetworkSceneInfo from the current scene
        SceneRef? sceneToLoad = null;
        if (mode != GameMode.Client)
            sceneToLoad = SceneRef.FromIndex(1); // Tu Escena2


        // Start or join (depends on gamemode) a session with a specific name
        var result = await _runner.StartGame(new StartGameArgs()
        {
            GameMode = mode,
            SessionName = roomName,
            Scene = sceneToLoad,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });
        
        if (!result.Ok)
        {
            Debug.LogError($"Fallo al iniciar el juego: {result.ShutdownReason}");
            // Limpieza si falla
            Destroy(_runner);
            _runner = null;
        }
    }

    void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            // 1. Calculamos dónde aparecerá el jugador basado en los puntos que crees en Unity
            Vector3 spawnPosition = Vector3.zero;
            Quaternion spawnRotation = Quaternion.identity;

            if (_spawnPoints != null && _spawnPoints.Length > 0)
            {
                int index = player.RawEncoded % _spawnPoints.Length;
                spawnPosition = _spawnPoints[index].position;
                spawnRotation = _spawnPoints[index].rotation;
            }
            else
            {
                // Respaldo por si olvidas asignar los puntos en el Inspector
                spawnPosition = new Vector3((player.RawEncoded % runner.Config.Simulation.PlayerCount) * 3, 0.001f, 0);
            }

            // 2. Hacemos spawn
            NetworkObject networkPlayerObject = runner.Spawn(_playerPrefab, spawnPosition, spawnRotation, player);
            _spawnedCharacters.Add(player, networkPlayerObject);
            
            if (QuestionManager.Instance != null && QuestionManager.Instance.IsReady)
            {
                QuestionManager.Instance.SincronizarConNuevoJugador(player);
            }
        }
        if (TriviaUI.Instance != null) TriviaUI.Instance.UpdateLobbyUI(runner);
    }
    void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (_spawnedCharacters.TryGetValue(player, out NetworkObject networkObject))
        {
            runner.Despawn(networkObject);
            _spawnedCharacters.Remove(player);
        }
        if (TriviaUI.Instance != null) TriviaUI.Instance.UpdateLobbyUI(runner);
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
            
            // Lógica del botón ALT para el cursor
            if (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                data.lookRotationDeltaX = 0f;
                data.lookRotationDeltaY = 0f;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;

                if (mouse != null)
                {
                    Vector2 mouseDelta = mouse.delta.ReadValue();
                    data.lookRotationDeltaX = mouseDelta.x * 0.1f;
                    data.lookRotationDeltaY = mouseDelta.y * 0.1f;
                }
            }
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
    void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
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
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true; 

        OcultarTodosLosPaneles();

        if (_menuCanvas != null) _menuCanvas.SetActive(false);
        if (_menuCamera != null) _menuCamera.SetActive(false);
    }
    void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner) { }
    void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
}