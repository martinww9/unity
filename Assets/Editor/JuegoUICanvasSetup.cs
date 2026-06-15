#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine.UI;

public static class JuegoUICanvasSetup
{
    const string ScenePath = "Assets/Scenes/JuegoEscena/Juego.unity";

    [MenuItem("Tools/Juego UI/Setup Canvases")]
    public static void SetupCanvasesMenu() => SetupCanvases();

    public static void SetupCanvases()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);

        var lobby = GameObject.Find("CanvasLobby");
        var timer = GameObject.Find("CanvasTimer");
        var preguntas = GameObject.Find("CanvasPreguntas");
        var podio = GameObject.Find("CanvasPodio");
        var finCarrera = GameObject.Find("CanvasFinCarrera");
        var stun = GameObject.Find("CanvasStun");
        var puntaje = EnsureCanvasPuntaje();
        EnsureTimerHudTexts(timer);

        if (lobby == null || timer == null || preguntas == null || podio == null || finCarrera == null)
        {
            Debug.LogError("JuegoUICanvasSetup: Faltan uno o más canvases raíz en la escena Juego.");
            return;
        }

        if (stun == null)
            Debug.LogWarning("JuegoUICanvasSetup: Falta CanvasStun. Ejecuta Tools > Juego > Setup Espiral Stun.");

        var triviaUi = Object.FindFirstObjectByType<TriviaUI>();
        if (triviaUi == null)
        {
            Debug.LogError("JuegoUICanvasSetup: No se encontró TriviaUI en la escena.");
            return;
        }

        var juegoUi = triviaUi.GetComponent<JuegoUI>();
        if (juegoUi == null)
            juegoUi = triviaUi.gameObject.AddComponent<JuegoUI>();

        juegoUi.EditorAssignCanvasRoots(lobby, timer, preguntas, podio, finCarrera, stun, puntaje);
        juegoUi.ApplySortingOrders();
        juegoUi.HideAllCanvases();

        EditorUtility.SetDirty(juegoUi);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("Juego UI: canvases enlazados (incl. CanvasPuntaje), sorting aplicado y todos desactivados al inicio.");
    }

    static GameObject EnsureCanvasPuntaje()
    {
        var existing = GameObject.Find("CanvasPuntaje");
        if (existing != null)
        {
            EnsureHudTextChild(existing.transform, "PuntajeHud", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -72f), "Puntaje: 0/0");
            return existing;
        }

        var canvasGo = new GameObject("CanvasPuntaje", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = JuegoUI.SortPuntaje;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        EnsureHudTextChild(canvasGo.transform, "PuntajeHud", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -72f), "Puntaje: 0/0");
        return canvasGo;
    }

    static void EnsureTimerHudTexts(GameObject timerCanvas)
    {
        if (timerCanvas == null)
            return;

        EnsureHudTextChild(timerCanvas.transform, "ProgresoNivel", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -108f), "N1: 0/0 correctas — Necesitas 0 para avanzar (60%)");
    }

    static TMP_Text EnsureHudTextChild(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        string defaultText,
        float fontSize = 22f,
        bool active = true)
    {
        Transform existing = parent.Find(name);
        GameObject textGo;
        if (existing != null)
        {
            textGo = existing.gameObject;
        }
        else
        {
            textGo = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(parent, false);
        }

        var rt = textGo.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = new Vector2(900f, 48f);

        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = defaultText;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        textGo.SetActive(active);
        return tmp;
    }
}
#endif
