#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class JuegoStaticJsonButtonSetup
{
    const string ScenePath = "Assets/Scenes/JuegoEscena/Juego.unity";
    const string ButtonName = "Boton Cargar JSON";

    [MenuItem("Tools/Juego UI/Setup Static JSON Button")]
    public static void SetupStaticJsonButtonMenu() => SetupStaticJsonButton();

    public static void SetupStaticJsonButton()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);

        var panel = UITheme.FindDeep(GameObject.Find("CanvasLobby")?.transform, "Panel");
        if (panel == null)
        {
            Debug.LogError("JuegoStaticJsonButtonSetup: No se encontró CanvasLobby > Panel.");
            return;
        }

        var triviaUi = Object.FindFirstObjectByType<TriviaUI>();
        if (triviaUi == null)
        {
            Debug.LogError("JuegoStaticJsonButtonSetup: No se encontró TriviaUI en la escena.");
            return;
        }

        var buttonTransform = UITheme.FindDeep(panel, ButtonName);
        if (buttonTransform == null)
            buttonTransform = CreateButton(panel);

        ConfigureButton(buttonTransform, triviaUi);
        AssignButtonReference(triviaUi, buttonTransform.gameObject);

        EditorUtility.SetDirty(triviaUi);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("JuegoStaticJsonButtonSetup: Boton Cargar JSON creado y enlazado.");
    }

    static Transform CreateButton(Transform panel)
    {
        var source = UITheme.FindDeep(panel, "Boton Regen") ?? UITheme.FindDeep(panel, "Boton Restart");
        GameObject buttonObject;

        if (source != null)
        {
            buttonObject = Object.Instantiate(source.gameObject, panel);
            buttonObject.name = ButtonName;
            UITheme.EnsureUniqueButtonVisuals(buttonObject.transform);

            if (buttonObject.transform is RectTransform cloneRect && source is RectTransform sourceRect)
            {
                cloneRect.anchorMin = sourceRect.anchorMin;
                cloneRect.anchorMax = sourceRect.anchorMax;
                cloneRect.pivot = sourceRect.pivot;
                cloneRect.sizeDelta = sourceRect.sizeDelta;
                cloneRect.anchoredPosition = sourceRect.anchoredPosition + new Vector2(0f, -56f);
            }

            buttonObject.transform.SetSiblingIndex(source.GetSiblingIndex() + 1);
        }
        else
        {
            buttonObject = new GameObject(ButtonName, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(panel, false);

            var rt = buttonObject.transform as RectTransform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(260f, 44f);
            rt.anchoredPosition = new Vector2(0f, 72f);

            var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(buttonObject.transform, false);
            var textRect = textObject.transform as RectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        return buttonObject.transform;
    }

    static void ConfigureButton(Transform buttonTransform, TriviaUI triviaUi)
    {
        UITheme.EnsureUniqueButtonVisuals(buttonTransform);

        var label = buttonTransform.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.name = "Text Boton Cargar JSON";
            UITheme.EnsureUniqueTextVisual(label);
            label.text = "Cargar JSON";
        }

        var button = buttonTransform.GetComponent<Button>();
        if (button == null)
            button = buttonTransform.gameObject.AddComponent<Button>();

        button.onClick = new Button.ButtonClickedEvent();
        UnityEventTools.AddPersistentListener(button.onClick, triviaUi.UI_BotonCargarPreguntasJson);

        UITheme.StyleButton(buttonTransform, "Cargar JSON", false);
        EditorUtility.SetDirty(buttonTransform.gameObject);
    }

    static void AssignButtonReference(TriviaUI triviaUi, GameObject buttonObject)
    {
        var serialized = new SerializedObject(triviaUi);
        var prop = serialized.FindProperty("_botonCargarJson");
        if (prop == null)
        {
            Debug.LogError("JuegoStaticJsonButtonSetup: No existe el campo _botonCargarJson en TriviaUI.");
            return;
        }

        prop.objectReferenceValue = buttonObject;
        serialized.ApplyModifiedProperties();
    }
}
#endif
