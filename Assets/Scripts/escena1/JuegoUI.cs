using UnityEngine;

/// <summary>
/// Raíces de canvas de la escena Juego. Evita GameObject.Find y fija orden de dibujado.
/// </summary>
public class JuegoUI : MonoBehaviour
{
    public static JuegoUI Instance;

    public const int SortLobby = 10;
    public const int SortTimer = 15;
    public const int SortPuntaje = 16;
    public const int SortPreguntas = 20;
    public const int SortStun = 25;
    public const int SortFinCarrera = 30;
    public const int SortPodio = 40;

    [SerializeField] private GameObject _canvasLobby;
    [SerializeField] private GameObject _canvasTimer;
    [SerializeField] private GameObject _canvasPuntaje;
    [SerializeField] private GameObject _canvasPreguntas;
    [SerializeField] private GameObject _canvasStun;
    [SerializeField] private GameObject _canvasPodio;
    [SerializeField] private GameObject _canvasFinCarrera;

    private void Awake()
    {
        Instance = this;
        HideAllCanvases();
        EnsureCanvasStunActive();
    }

    private void EnsureCanvasStunActive()
    {
        if (_canvasStun != null)
            _canvasStun.SetActive(true);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void HideAllCanvases()
    {
        SetCanvasActive(_canvasLobby, false);
        SetCanvasActive(_canvasTimer, false);
        SetCanvasActive(_canvasPuntaje, false);
        SetCanvasActive(_canvasPreguntas, false);
        SetCanvasActive(_canvasPodio, false);
        SetCanvasActive(_canvasFinCarrera, false);
    }

    public void ShowLobbyPhase()
    {
        HideAllCanvases();
        SetCanvasActive(_canvasLobby, true);
    }

    public void ShowGameplayPhase()
    {
        HideAllCanvases();
        SetCanvasActive(_canvasLobby, false);
        SetCanvasActive(_canvasTimer, true);
        SetCanvasActive(_canvasPuntaje, true);
        SetCanvasActive(_canvasPreguntas, true);
    }

    public void ShowFinCarreraPhase()
    {
        HideAllCanvases();
        SetCanvasActive(_canvasFinCarrera, true);
    }

    public void ShowPodiumPhase()
    {
        HideAllCanvases();
        SetCanvasActive(_canvasPodio, true);
    }

    public void ShowPodiumAlongsideGameplay()
    {
        SetCanvasActive(_canvasLobby, false);
        SetCanvasActive(_canvasTimer, true);
        SetCanvasActive(_canvasPreguntas, true);
        SetCanvasActive(_canvasPuntaje, true);
        SetCanvasActive(_canvasPodio, true);
        SetCanvasActive(_canvasFinCarrera, false);
    }

    public void ShowFinCarreraWithPodium()
    {
        SetCanvasActive(_canvasLobby, false);
        SetCanvasActive(_canvasTimer, false);
        SetCanvasActive(_canvasPreguntas, false);
        SetCanvasActive(_canvasPuntaje, false);
        SetCanvasActive(_canvasFinCarrera, true);
        SetCanvasActive(_canvasPodio, true);
    }

    public void SetCanvasByName(string canvasName, bool active)
    {
        switch (canvasName)
        {
            case "CanvasLobby":
                SetCanvasActive(_canvasLobby, active);
                break;
            case "CanvasTimer":
                SetCanvasActive(_canvasTimer, active);
                break;
            case "CanvasPuntaje":
                SetCanvasActive(_canvasPuntaje, active);
                break;
            case "CanvasPreguntas":
                SetCanvasActive(_canvasPreguntas, active);
                break;
            case "CanvasPodio":
                SetCanvasActive(_canvasPodio, active);
                break;
            case "CanvasFinCarrera":
                SetCanvasActive(_canvasFinCarrera, active);
                break;
            default:
                var fallback = GameObject.Find(canvasName);
                if (fallback != null)
                    fallback.SetActive(active);
                break;
        }
    }

    private static void SetCanvasActive(GameObject canvasRoot, bool active)
    {
        if (canvasRoot != null)
            canvasRoot.SetActive(active);
    }

    public void ApplySortingOrders()
    {
        ApplySort(_canvasLobby, SortLobby);
        ApplySort(_canvasTimer, SortTimer);
        ApplySort(_canvasPuntaje, SortPuntaje);
        ApplySort(_canvasPreguntas, SortPreguntas);
        ApplySort(_canvasStun, SortStun);
        ApplySort(_canvasFinCarrera, SortFinCarrera);
        ApplySort(_canvasPodio, SortPodio);
    }

    private static void ApplySort(GameObject canvasRoot, int order)
    {
        if (canvasRoot == null) return;
        var canvas = canvasRoot.GetComponent<Canvas>();
        if (canvas != null)
            canvas.sortingOrder = order;
    }

#if UNITY_EDITOR
    public void EditorAssignCanvasRoots(
        GameObject lobby,
        GameObject timer,
        GameObject preguntas,
        GameObject podio,
        GameObject finCarrera,
        GameObject stun = null,
        GameObject puntaje = null)
    {
        _canvasLobby = lobby;
        _canvasTimer = timer;
        _canvasPreguntas = preguntas;
        _canvasPodio = podio;
        _canvasFinCarrera = finCarrera;
        if (stun != null)
            _canvasStun = stun;
        if (puntaje != null)
            _canvasPuntaje = puntaje;
    }
#endif
}
