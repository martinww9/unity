using UnityEngine;
using TMPro;
using Fusion;

public class RoomButton : MonoBehaviour
{
    [SerializeField] private TMP_Text _nombreText;
    private string _sessionName;
    private Spawner _spawner;

    public void Setup(SessionInfo session, Spawner spawner) {
        _sessionName = session.Name;
        _nombreText.text = $"{session.Name} ({session.PlayerCount}/{session.MaxPlayers})";
        _spawner = spawner;
    }

    public void OnClick() {
        _spawner.StartGame(GameMode.Client, _sessionName);
    }
}