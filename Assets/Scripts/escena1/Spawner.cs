using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class Spawner : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkPrefabRef _playerPrefab;
    [SerializeField] private GameObject _menuCanvas;
    [SerializeField] private GameObject _menuCamera;
    private Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();
    private NetworkRunner _runner;
    private bool _mouseButton0;

    public void UI_StartHost()
    {
        StartGame(GameMode.Host);
    }

    public void UI_StartClient()
    {
        StartGame(GameMode.Client);
    }

    private void Update()
    {
        if (Mouse.current != null)
            _mouseButton0 = _mouseButton0 | Mouse.current.leftButton.isPressed;
    }
    public async void StartGame(GameMode mode)
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
            SessionName = "TestRoom",
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
    /*
    private void OnGUI()
    {
        if (_runner == null)
        {
            if (GUI.Button(new Rect(0, 0, 200, 40), "Host"))
            {
                StartGame(GameMode.Host);
            }

            if (GUI.Button(new Rect(0, 40, 200, 40), "Join"))
            {
                StartGame(GameMode.Client);
            }
        }
    }
    */
    void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            // Create a unique position for the player
            Vector3 spawnPosition = new Vector3((player.RawEncoded % runner.Config.Simulation.PlayerCount) * 3, 1, 0);
            NetworkObject networkPlayerObject = runner.Spawn(_playerPrefab, spawnPosition, Quaternion.identity, player);
            // Keep track of the player avatars for easy access
            _spawnedCharacters.Add(player, networkPlayerObject);
        }
    }
    void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (_spawnedCharacters.TryGetValue(player, out NetworkObject networkObject))
        {
            runner.Despawn(networkObject);
            _spawnedCharacters.Remove(player);
        }
    }
    void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input)
    {
        var data = new NetworkInputData();
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed) data.Direction += Vector3.forward;
            if (keyboard.sKey.isPressed) data.Direction += Vector3.back;
            if (keyboard.aKey.isPressed) data.Direction += Vector3.left;
            if (keyboard.dKey.isPressed) data.Direction += Vector3.right;
            
            data.Buttons.Set(NetworkInputData.MouseButton0, _mouseButton0);
            _mouseButton0 = false;
        }

        if (TriviaUI.Instance != null)
            {
        data.SelectedAnswerIndex = TriviaUI.Instance.LastSelectedIndex;
        // Resetear después de enviarlo para no enviar la misma respuesta 60 veces por segundo*****
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
    void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner) {
        Debug.Log("Escena cargada completamente. Limpiando interfaz de menú.");
        if (_menuCanvas != null) _menuCanvas.SetActive(false);
        if (_menuCamera != null) _menuCamera.SetActive(false);
        
        // Opcional: Liberar el cursor para que el jugador pueda moverse
       /* if (runner.IsPlayer) 
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        */
    }
    void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner) { }
    void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
}



