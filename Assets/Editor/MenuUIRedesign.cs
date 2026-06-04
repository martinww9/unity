#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class MenuUIRedesign
{
    const string ScenePath = "Assets/Scenes/UI/UI.unity";

    static readonly Color BgPrimary = new(0.102f, 0.137f, 0.196f, 1f);
    static readonly Color PanelSurface = new(0.141f, 0.204f, 0.278f, 1f);
    static readonly Color Accent = new(0.239f, 0.545f, 0.992f, 1f);
    static readonly Color AccentPressed = new(0.18f, 0.42f, 0.78f, 1f);
    static readonly Color TextPrimary = new(0.941f, 0.957f, 0.973f, 1f);
    static readonly Color TextSecondary = new(0.659f, 0.722f, 0.8f, 1f);
    static readonly Color InputBg = new(0.176f, 0.243f, 0.322f, 1f);

    [MenuItem("Tools/Menu UI/Apply Redesign")]
    public static void ApplyRedesign()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var canvasMenu = GameObject.Find("CanvasMenu") ?? GameObject.Find("CanvasInicio");
        if (canvasMenu == null)
        {
            Debug.LogError("MenuUIRedesign: No se encontró CanvasMenu/CanvasInicio.");
            return;
        }

        canvasMenu.name = "CanvasMenu";
        var menuCanvas = canvasMenu.GetComponent<Canvas>();
        if (menuCanvas != null)
            menuCanvas.sortingOrder = 100;

        var scaler = canvasMenu.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }

        EnsureBackground(canvasMenu.transform);

        var panelInicio = FindChild(canvasMenu.transform, "PanelInicio");
        var panelBrowser = FindChild(canvasMenu.transform, "PanelBrowser");
        var panelCrear = FindChild(canvasMenu.transform, "PanelCrearSala");

        if (panelBrowser != null && panelBrowser.parent != canvasMenu.transform)
            panelBrowser.SetParent(canvasMenu.transform, false);
        if (panelCrear != null && panelCrear.parent != canvasMenu.transform)
            panelCrear.SetParent(canvasMenu.transform, false);

        DestroyIfExists("CanvasBrowser");
        DestroyIfExists("CanvasCrearSala");

        if (panelInicio != null)
        {
            panelInicio.gameObject.SetActive(true);
            StylePanelCard(panelInicio, 520f, 400f);
            StylePanelInicioContent(panelInicio);
        }

        if (panelBrowser != null)
        {
            panelBrowser.gameObject.SetActive(false);
            StylePanelCard(panelBrowser, 720f, 520f);
            StylePanelBrowser(panelBrowser);
        }

        if (panelCrear != null)
        {
            panelCrear.gameObject.SetActive(false);
            StylePanelCard(panelCrear, 520f, 400f);
            StylePanelCrearSala(panelCrear);
        }

        WireSpawner(canvasMenu, panelInicio, panelBrowser, panelCrear);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("MenuUIRedesign: aplicado correctamente.");
    }

    static void EnsureBackground(Transform canvasMenu)
    {
        var bg = canvasMenu.Find("MenuBackground");
        if (bg == null)
        {
            var go = new GameObject("MenuBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(canvasMenu, false);
            go.transform.SetAsFirstSibling();
            bg = go.transform;
        }

        var rt = bg as RectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        var img = bg.GetComponent<Image>();
        img.color = BgPrimary;
        img.raycastTarget = true;
    }

    static Transform FindChild(Transform parent, string name)
    {
        foreach (Transform c in parent.GetComponentsInChildren<Transform>(true))
            if (c.name == name) return c;
        return null;
    }

    static void DestroyIfExists(string name)
    {
        var go = GameObject.Find(name);
        if (go != null) Object.DestroyImmediate(go);
    }

    static void StylePanelCard(Transform panel, float width, float height)
    {
        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;

        var img = panel.GetComponent<Image>();
        if (img == null) img = panel.gameObject.AddComponent<Image>();
        img.color = PanelSurface;
        img.type = Image.Type.Sliced;
    }

    static void StylePanelInicioContent(Transform panel)
    {
        foreach (var tmp in panel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (tmp.name.Contains("Title") || tmp.fontSize >= 32)
            {
                tmp.text = "Trivia Estructuras de Datos";
                tmp.fontSize = 42;
                tmp.fontStyle = FontStyles.Bold;
                tmp.color = TextPrimary;
                tmp.alignment = TextAlignmentOptions.Center;
            }
            else if (tmp.GetComponentInParent<Button>() == null)
            {
                tmp.color = TextSecondary;
                tmp.fontSize = 22;
            }
        }

        StyleButton(FindDeep(panel, "Join_Button"), "Unirse a sala", true);
        StyleButton(FindDeep(panel, "Host_Button"), "Crear sala", false);
    }

    static void StylePanelBrowser(Transform panel)
    {
        foreach (var tmp in panel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (tmp.GetComponentInParent<Button>() == null && tmp.fontSize >= 28)
            {
                tmp.text = "Salas disponibles";
                tmp.fontSize = 36;
                tmp.fontStyle = FontStyles.Bold;
                tmp.color = TextPrimary;
            }
            else if (tmp.GetComponentInParent<Button>() == null)
                tmp.color = TextSecondary;
        }

        StyleButton(FindDeep(panel, "ButtonRefrescar"), "Refrescar", false);
        StyleButton(FindDeep(panel, "ButtonVolver"), "Volver", false);

        var lobbys = FindDeep(panel, "Lobbys");
        if (lobbys != null)
        {
            var img = lobbys.GetComponent<Image>();
            if (img == null) img = lobbys.gameObject.AddComponent<Image>();
            img.color = new Color(0.09f, 0.12f, 0.17f, 1f);
        }

        var empty = panel.Find("EmptyStateText");
        if (empty == null)
        {
            var go = new GameObject("EmptyStateText", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(panel, false);
            empty = go.transform;
            go.SetActive(false);
        }
        var emptyTmp = empty.GetComponent<TMP_Text>();
        emptyTmp.text = "No hay salas. Refresca o crea una.";
        emptyTmp.fontSize = 20;
        emptyTmp.color = TextSecondary;
        emptyTmp.alignment = TextAlignmentOptions.Center;
    }

    static void StylePanelCrearSala(Transform panel)
    {
        foreach (var tmp in panel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (tmp.GetComponentInParent<Button>() == null && tmp.fontSize >= 28)
            {
                tmp.text = "Crear sala";
                tmp.fontSize = 36;
                tmp.fontStyle = FontStyles.Bold;
                tmp.color = TextPrimary;
            }
        }

        foreach (var input in panel.GetComponentsInChildren<TMP_InputField>(true))
        {
            var bg = input.transform.Find("Text Area")?.GetComponent<Image>();
            if (bg != null) bg.color = InputBg;
            var ph = input.placeholder as TMP_Text;
            if (ph != null) { ph.text = "Nombre de la sala"; ph.color = TextSecondary; }
        }

        StyleButton(FindDeep(panel, "ButtonConfirmarHost"), "Crear partida", true);
        StyleButton(FindDeep(panel, "ButtonVolver"), "Volver", false);
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        foreach (Transform c in root)
        {
            var f = FindDeep(c, name);
            if (f != null) return f;
        }
        return null;
    }

    static void StyleButton(Transform btn, string label, bool primary)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.color = primary ? Accent : PanelSurface;
            if (!primary)
            {
                var outline = btn.GetComponent<Outline>();
                if (outline == null) outline = btn.gameObject.AddComponent<Outline>();
                outline.effectColor = Accent;
                outline.effectDistance = new Vector2(2, -2);
            }
        }

        var colors = btn.GetComponent<Button>().colors;
        colors.normalColor = primary ? Accent : PanelSurface;
        colors.highlightedColor = Accent * 1.1f;
        colors.pressedColor = AccentPressed;
        colors.selectedColor = colors.highlightedColor;
        btn.GetComponent<Button>().colors = colors;

        var tmp = btn.GetComponentInChildren<TMP_Text>();
        if (tmp != null)
        {
            if (!string.IsNullOrEmpty(label)) tmp.text = label;
            tmp.color = primary ? TextPrimary : Accent;
            tmp.fontSize = 22;
            tmp.fontStyle = FontStyles.Bold;
        }
    }

    static void WireSpawner(GameObject canvasMenu, Transform panelInicio, Transform panelBrowser, Transform panelCrear)
    {
        var spawner = Object.FindFirstObjectByType<Spawner>();
        if (spawner == null) return;

        var so = new SerializedObject(spawner);
        so.FindProperty("_canvasMenu").objectReferenceValue = canvasMenu;
        so.FindProperty("_panelInicio").objectReferenceValue = panelInicio != null ? panelInicio.gameObject : null;
        so.FindProperty("_panelBrowser").objectReferenceValue = panelBrowser != null ? panelBrowser.gameObject : null;
        so.FindProperty("_panelCrearSala").objectReferenceValue = panelCrear != null ? panelCrear.gameObject : null;

        Transform content = null;
        if (panelBrowser != null)
            content = panelBrowser.GetComponentInChildren<Transform>(true);
        foreach (var t in panelBrowser != null ? panelBrowser.GetComponentsInChildren<Transform>(true) : System.Array.Empty<Transform>())
            if (t.name == "Content") content = t;

        if (content != null)
            so.FindProperty("_roomListContent").objectReferenceValue = content;

        var lobbys = panelBrowser != null ? FindDeep(panelBrowser, "Lobbys") : null;
        if (lobbys != null)
            so.FindProperty("_roomListPanel").objectReferenceValue = lobbys.gameObject;

        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
