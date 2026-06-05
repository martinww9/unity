#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

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

        if (lobby == null || timer == null || preguntas == null || podio == null || finCarrera == null)
        {
            Debug.LogError("JuegoUICanvasSetup: Faltan uno o más canvases raíz en la escena Juego.");
            return;
        }

        var triviaUi = Object.FindFirstObjectByType<TriviaUI>();
        if (triviaUi == null)
        {
            Debug.LogError("JuegoUICanvasSetup: No se encontró TriviaUI en la escena.");
            return;
        }

        var juegoUi = triviaUi.GetComponent<JuegoUI>();
        if (juegoUi == null)
            juegoUi = triviaUi.gameObject.AddComponent<JuegoUI>();

        juegoUi.EditorAssignCanvasRoots(lobby, timer, preguntas, podio, finCarrera);
        juegoUi.ApplySortingOrders();
        juegoUi.HideAllCanvases();

        EditorUtility.SetDirty(juegoUi);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("Juego UI: canvases enlazados, sorting aplicado y todos desactivados al inicio.");
    }
}
#endif
