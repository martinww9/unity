#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class StunEffectSetup
{
    const string MenuRoot = "Tools/Juego/";
    const string ScenePath = "Assets/Scenes/JuegoEscena/Juego.unity";
    const string SpiralPath = "Assets/Resources/spiral.png";

    [MenuItem(MenuRoot + "Setup Espiral Stun")]
    public static void SetupEspiralStun()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var spiralSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpiralPath);
        if (spiralSprite == null)
        {
            Debug.LogError($"StunEffectSetup: No se encontró el sprite en {SpiralPath}");
            return;
        }

        var canvasStun = GameObject.Find("CanvasStun");
        if (canvasStun == null)
        {
            canvasStun = new GameObject("CanvasStun", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasStun.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = JuegoUI.SortStun;

            var scaler = canvasStun.GetComponent<CanvasScaler>();
            UITheme.ApplyCanvasScaler(scaler);

            Undo.RegisterCreatedObjectUndo(canvasStun, "Create CanvasStun");
        }

        var espiral = GameObject.Find("EspiralStun");
        if (espiral == null)
        {
            espiral = new GameObject("EspiralStun", typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(espiral, "Create EspiralStun");
        }

        Undo.SetTransformParent(espiral.transform, canvasStun.transform, "Parent EspiralStun to CanvasStun");

        var rt = espiral.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(260f, 260f);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }

        var image = espiral.GetComponent<Image>();
        if (image == null)
            image = Undo.AddComponent<Image>(espiral);
        image.sprite = spiralSprite;
        image.color = Color.white;
        image.raycastTarget = false;

        var effect = canvasStun.GetComponent<EfectoAturdimiento>();
        if (effect == null)
            effect = Undo.AddComponent<EfectoAturdimiento>(canvasStun);
        if (espiral.GetComponent<EfectoAturdimiento>() != null)
            Undo.DestroyObjectImmediate(espiral.GetComponent<EfectoAturdimiento>());

        var effectSo = new SerializedObject(effect);
        effectSo.FindProperty("imagenEspiral").objectReferenceValue = image;
        effectSo.ApplyModifiedPropertiesWithoutUndo();

        canvasStun.SetActive(true);
        espiral.SetActive(false);

        var juegoUi = Object.FindFirstObjectByType<JuegoUI>();
        if (juegoUi != null)
        {
            Undo.RecordObject(juegoUi, "Assign CanvasStun to JuegoUI");
            var juegoSo = new SerializedObject(juegoUi);
            juegoSo.FindProperty("_canvasStun").objectReferenceValue = canvasStun;
            juegoSo.ApplyModifiedProperties();
            juegoUi.ApplySortingOrders();
        }

        var canvasComponent = canvasStun.GetComponent<Canvas>();
        if (canvasComponent != null)
            canvasComponent.sortingOrder = JuegoUI.SortStun;

        EditorUtility.SetDirty(espiral);
        EditorUtility.SetDirty(canvasStun);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Selection.activeGameObject = espiral;
        Debug.Log("StunEffectSetup: CanvasStun + EspiralStun configurados y escena guardada.", espiral);
    }

    [MenuItem(MenuRoot + "Validar Espiral Stun")]
    public static void ValidateEspiralStun()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var espiral = GameObject.Find("EspiralStun");
        if (espiral == null)
        {
            Debug.LogError("StunEffectSetup: Falta 'EspiralStun' en la escena.");
            return;
        }

        var canvasStun = GameObject.Find("CanvasStun");
        var effect = canvasStun != null ? canvasStun.GetComponent<EfectoAturdimiento>() : null;
        var image = espiral.GetComponent<Image>();

        Debug.Log("=== StunEffectSetup: validación ===", espiral);

        if (canvasStun == null)
            Debug.LogWarning("Falta CanvasStun. Ejecuta 'Setup Espiral Stun'.", espiral);
        else if (espiral.transform.parent != canvasStun.transform)
            Debug.LogWarning("EspiralStun no es hijo de CanvasStun.", espiral);
        else
            Debug.Log("Jerarquía CanvasStun → EspiralStun OK.", espiral);

        if (effect == null)
            Debug.LogWarning("Falta EfectoAturdimiento en CanvasStun.", espiral);
        else
            Debug.Log("EfectoAturdimiento presente en CanvasStun.", espiral);

        if (image == null || image.sprite == null)
            Debug.LogWarning("Image sin sprite asignado.", espiral);
        else
            Debug.Log($"Sprite asignado: {image.sprite.name}", espiral);

        var juegoUi = Object.FindFirstObjectByType<JuegoUI>();
        if (juegoUi == null)
            Debug.LogWarning("No se encontró JuegoUI en la escena.", espiral);
    }
}
#endif
