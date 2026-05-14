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

    public void UI_IrABrowser() {
        OcultarTodosLosPaneles();
        _panelBrowser.SetActive(true);
        // Iniciamos el Runner para buscar salas (Lobby compartido)
        StartGame(GameMode.Client, "");
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
        if (_runner != null) return;

        // Create the Fusion runner and let it know that we will be providing user input
        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;
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
            // Create a unique position for the player
            Vector3 spawnPosition = new Vector3((player.RawEncoded % runner.Config.Simulation.PlayerCount) * 3, 1, 0);
            NetworkObject networkPlayerObject = runner.Spawn(_playerPrefab, spawnPosition, Quaternion.identity, player);
            // Keep track of the player avatars for easy access
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
        if (!Application.isFocused) 
        {
            // Enviamos un input vacío para que el personaje se quede quieto pero la conexión siga viva
            input.Set(new NetworkInputData()); 
            return;
        }
    
        var data = new NetworkInputData();
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;

        if (keyboard != null)
        {
            // 1. Movimiento WASD
            if (keyboard.wKey.isPressed) data.Direction += Vector3.forward;
            if (keyboard.sKey.isPressed) data.Direction += Vector3.back;
            if (keyboard.aKey.isPressed) data.Direction += Vector3.left;
            if (keyboard.dKey.isPressed) data.Direction += Vector3.right;
            
            // Clic (Lo que ya tenías)
            data.Buttons.Set(NetworkInputData.MouseButton0, _mouseButton0);
            _mouseButton0 = false;

            // 2. Lógica del botón ALT para el cursor
            if (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed)
            {
                // Si mantenemos ALT: Mostramos el cursor y frenamos la cámara
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                data.lookRotationDeltaX = 0f;
                data.lookRotationDeltaY = 0f;
            }
            else
            {
                // Si soltamos ALT: Ocultamos el cursor y capturamos el ratón
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;

                if (mouse != null)
                {
                    // Leemos el movimiento (delta) del ratón con el Nuevo Input System
                    Vector2 mouseDelta = mouse.delta.ReadValue();
                    
                    // Nota: Multiplicamos por un valor pequeño (ej. 0.1f) para que la 
                    // sensibilidad inicial no sea extremadamente alta
                    data.lookRotationDeltaX = mouseDelta.x * 0.1f;
                    data.lookRotationDeltaY = mouseDelta.y * 0.1f;
                }
            }
        }

        // 3. Lógica de Trivia (Se mantiene igual)
        if (TriviaUI.Instance != null)
        {
            data.SelectedAnswerIndex = TriviaUI.Instance.LastSelectedIndex;
            // Resetear después de enviarlo para no enviar la misma respuesta 60 veces por segundo
            TriviaUI.Instance.LastSelectedIndex = -1; 
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
        foreach (Transform child in _roomListContent) Destroy(child.gameObject);

        foreach (var session in sessionList)
        {
            GameObject btnObj = Instantiate(_roomButtonPrefab, _roomListContent);
            RoomButton script = btnObj.GetComponent<RoomButton>();
            if(script != null) script.Setup(session, this);
        }
    
    }
    void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner) {
        Debug.Log("Entramos a la sesión. Mostrando Lobby.");
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true; 
        if (_menuCanvas != null) _menuCanvas.SetActive(false);
        if (_menuCamera != null) _menuCamera.SetActive(false);
    }
    void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner) { }
    void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
}