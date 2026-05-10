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

    private void Awake() 
    {
        Instance = this;
        if (_panelPrincipal != null) _panelPrincipal.SetActive(false);
        if (_panelLobby != null) _panelLobby.SetActive(false);
    }

    public void StartGameUI()
    {
        _panelLobby.SetActive(false);
        _panelPrincipal.SetActive(true);
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
        _timerText.text = time.ToString("F1") + "s";
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
    _botonStart.SetActive(runner.IsServer || runner.IsSharedModeMasterClient);

    // 2. Limpiar lista actual
    foreach (Transform child in _playerListContainer) Destroy(child.gameObject);

    // 3. Mostrar todos los jugadores conectados
    foreach (var player in runner.ActivePlayers)
    {
        GameObject entry = Instantiate(_playerEntryPrefab, _playerListContainer);
        entry.GetComponent<TMP_Text>().text = $"Jugador {player.PlayerId}";
    }
}
}