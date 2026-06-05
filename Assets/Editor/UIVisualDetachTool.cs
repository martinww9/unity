#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class UIVisualDetachTool
{
    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/MenuPrincipalEscena/MenuPrincipal.unity",
        "Assets/Scenes/JuegoEscena/Juego.unity"
    };

    [MenuItem("Tools/UI/Detach Button Visuals")]
    public static void DetachButtonVisuals()
    {
        foreach (string scenePath in ScenePaths)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"UIVisualDetachTool: no se encontró la escena {scenePath}.");
                continue;
            }

            var scene = EditorSceneManager.OpenScene(scenePath);

            foreach (var button in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                UITheme.EnsureUniqueButtonVisuals(button.transform);
                EditorUtility.SetDirty(button);
            }

            foreach (var tmp in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                UITheme.EnsureUniqueTextVisual(tmp);
                EditorUtility.SetDirty(tmp);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"UIVisualDetachTool: visuales UI desenlazados en {scenePath}.");
        }
    }
}
#endif
