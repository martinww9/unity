#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class JuegoUIRedesign
{
    const string ScenePath = "Assets/Scenes/JuegoEscena/Juego.unity";

    [MenuItem("Tools/Juego UI/Apply Redesign")]
    public static void ApplyRedesign()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);

        StyleCanvasRoot("CanvasLobby", JuegoUI.SortLobby, StyleCanvasLobby);
        StyleCanvasRoot("CanvasTimer", JuegoUI.SortTimer, StyleCanvasTimer);
        StyleCanvasRoot("CanvasPreguntas", JuegoUI.SortPreguntas, StyleCanvasPreguntas);
        StyleCanvasRoot("CanvasFinCarrera", JuegoUI.SortFinCarrera, StyleCanvasFinCarrera);
        StyleCanvasRoot("CanvasPodio", JuegoUI.SortPodio, StyleCanvasPodio);

        JuegoUICanvasSetup.SetupCanvases();
        JuegoStaticJsonButtonSetup.SetupStaticJsonButton();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("JuegoUIRedesign: aplicado correctamente.");
    }

    static void StyleCanvasRoot(string canvasName, int sortOrder, System.Action<Transform> styleContent)
    {
        var root = GameObject.Find(canvasName)?.transform;
        if (root == null)
        {
            Debug.LogWarning($"JuegoUIRedesign: no se encontró {canvasName}.");
            return;
        }

        var canvas = root.GetComponent<Canvas>();
        if (canvas != null)
            canvas.sortingOrder = sortOrder;

        UITheme.ApplyCanvasScaler(root.GetComponent<CanvasScaler>());

        if (root is RectTransform rt && rt.localScale == Vector3.zero)
            rt.localScale = Vector3.one;

        styleContent(root);
    }

    static void StyleCanvasLobby(Transform canvasRoot)
    {
        var panel = UITheme.FindDeep(canvasRoot, "Panel");
        if (panel == null) return;

        UITheme.StylePanelCard(panel as RectTransform, 640f, 480f);
        EnsureTitle(panel, "LobbyTitle", "Sala de espera", 36);

        var list = UITheme.FindDeep(panel, "PlayerListContent");
        if (list != null)
            UITheme.StyleListSurface(list);

        UITheme.StyleButton(UITheme.FindDeep(panel, "Boton Start"), null, true);
        UITheme.StyleButton(UITheme.FindDeep(panel, "Boton Regen"), null, false);
        UITheme.StyleButton(UITheme.FindDeep(panel, "Boton Cargar JSON"), "Cargar JSON", false);
        UITheme.StyleButton(UITheme.FindDeep(panel, "Boton Restart"), null, false);

        foreach (var tmp in panel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (tmp.GetComponentInParent<Button>() != null) continue;
            if (tmp.transform == list) continue;
            if (tmp.name == "LobbyTitle") continue;
            UITheme.StyleBodyText(tmp);
        }
    }

    static void StyleCanvasTimer(Transform canvasRoot)
    {
        var panel = UITheme.FindDeep(canvasRoot, "Panel");
        if (panel == null) return;

        UITheme.StyleHudPill(panel);
        var timer = UITheme.FindDeep(panel, "timer");
        if (timer != null)
            UITheme.StyleHudText(timer.GetComponent<TMP_Text>());
    }

    static void StyleCanvasPreguntas(Transform canvasRoot)
    {
        var panel = UITheme.FindDeep(canvasRoot, "panelPrincipal");
        if (panel == null) return;

        UITheme.StylePanelCard(panel as RectTransform, 720f, 420f);

        var pregunta = UITheme.FindDeep(panel, "pregunta");
        if (pregunta != null)
            UITheme.StyleTitleText(pregunta.GetComponent<TMP_Text>());

        UITheme.StyleButton(UITheme.FindDeep(panel, "Boton A"), null, false);
        UITheme.StyleButton(UITheme.FindDeep(panel, "Boton B"), null, false);
        UITheme.StyleButton(UITheme.FindDeep(panel, "Boton C"), null, false);
        UITheme.StyleButton(UITheme.FindDeep(panel, "Boton D"), null, false);

        foreach (var tmp in panel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (tmp.GetComponentInParent<Button>() != null) continue;
            if (tmp.name == "pregunta") continue;
            if (tmp.name.StartsWith("Text Boton")) continue;
            UITheme.StyleBodyText(tmp);
        }
    }

    static void StyleCanvasFinCarrera(Transform canvasRoot)
    {
        var llegaste = UITheme.FindDeep(canvasRoot, "PanelLlegaste");
        if (llegaste != null)
        {
            UITheme.StylePanelCard(llegaste as RectTransform, 520f, 360f);
            var titulo = UITheme.FindDeep(llegaste, "Llegaste");
            if (titulo != null)
                UITheme.StyleTitleText(titulo.GetComponent<TMP_Text>(), "¡Meta!");
        }

        var feedbackPanel = UITheme.FindDeep(canvasRoot, "PanelFeedback");
        if (feedbackPanel != null)
        {
            UITheme.StylePanelImage(feedbackPanel);
            var img = feedbackPanel.GetComponent<Image>();
            if (img != null) img.color = UITheme.ListBg;
        }

        var feedbackRoot = UITheme.FindDeep(canvasRoot, "Feedback");
        if (feedbackRoot != null)
            UITheme.StyleListSurface(feedbackRoot);

        var botonFeedback = FindButtonUnder(canvasRoot, "Ver evaluación", "Solicitar", "Feedback");
        if (botonFeedback != null)
            UITheme.StylePrimaryButton(botonFeedback, null);

        foreach (var tmp in canvasRoot.GetComponentsInChildren<TMP_Text>(true))
        {
            if (tmp.GetComponentInParent<Button>() != null) continue;
            if (tmp.fontSize >= 28)
                UITheme.StyleTitleText(tmp);
            else
                UITheme.StyleBodyText(tmp);
        }
    }

    static void StyleCanvasPodio(Transform canvasRoot)
    {
        var panel = UITheme.FindDeep(canvasRoot, "PanelPodio");
        if (panel == null) return;

        UITheme.StylePanelCard(panel as RectTransform, 480f, 320f);
        EnsureTitle(panel, "PodioTitle", "Clasificación", 32);

        var podio = UITheme.FindDeep(panel, "Podio");
        if (podio != null)
        {
            var tmp = podio.GetComponent<TMP_Text>();
            if (tmp != null)
            {
                UITheme.EnsureUniqueTextVisual(tmp);
                tmp.color = UITheme.TextPrimary;
                tmp.fontSize = 24;
                tmp.alignment = TextAlignmentOptions.TopLeft;
            }
        }
    }

    static void EnsureTitle(Transform panel, string goName, string text, int fontSize)
    {
        var title = panel.Find(goName);
        if (title == null)
        {
            var go = new GameObject(goName, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(panel, false);
            title = go.transform;
            var rt = title as RectTransform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -24f);
            rt.sizeDelta = new Vector2(560f, 48f);
        }

        var tmp = title.GetComponent<TMP_Text>();
        UITheme.StyleTitleText(tmp, text);
        tmp.fontSize = fontSize;
    }

    static Transform FindButtonUnder(Transform root, params string[] nameHints)
    {
        foreach (var btn in root.GetComponentsInChildren<Button>(true))
        {
            var label = btn.GetComponentInChildren<TMP_Text>(true);
            if (label == null) continue;
            foreach (var hint in nameHints)
            {
                if (btn.name.Contains(hint, System.StringComparison.OrdinalIgnoreCase) ||
                    label.text.Contains(hint, System.StringComparison.OrdinalIgnoreCase))
                    return btn.transform;
            }
        }
        return null;
    }
}
#endif
