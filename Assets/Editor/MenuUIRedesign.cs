#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class MenuUIRedesign
{
    const string ScenePath = "Assets/Scenes/MenuPrincipalEscena/MenuPrincipal.unity";

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

        UITheme.ApplyCanvasScaler(canvasMenu.GetComponent<CanvasScaler>());
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
            UITheme.StylePanelCard(panelInicio as RectTransform, 520f, 400f);
            StylePanelInicioContent(panelInicio);
        }

        if (panelBrowser != null)
        {
            panelBrowser.gameObject.SetActive(false);
            UITheme.StylePanelCard(panelBrowser as RectTransform, 720f, 520f);
            StylePanelBrowser(panelBrowser);
        }

        if (panelCrear != null)
        {
            panelCrear.gameObject.SetActive(false);
            UITheme.StylePanelCard(panelCrear as RectTransform, 520f, 400f);
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
        img.color = UITheme.BgPrimary;
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

    static void StylePanelInicioContent(Transform panel)
    {
        foreach (var tmp in panel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (tmp.name.Contains("Title") || tmp.fontSize >= 32)
            {
                UITheme.StyleTitleText(tmp, "Trivia Estructuras de Datos");
                tmp.fontSize = 42;
            }
            else if (tmp.GetComponentInParent<Button>() == null && tmp.name != "LabelNombreJugador")
                UITheme.StyleBodyText(tmp);
        }

        EnsurePlayerNameInput(panel);
        UITheme.StyleButton(UITheme.FindDeep(panel, "Join_Button"), "Unirse a sala", true);
        UITheme.StyleButton(UITheme.FindDeep(panel, "Host_Button"), "Crear sala", false);
    }

    static void EnsurePlayerNameInput(Transform panel)
    {
        var label = panel.Find("LabelNombreJugador");
        if (label == null)
        {
            var labelGo = new GameObject("LabelNombreJugador", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(panel, false);
            label = labelGo.transform;
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.sizeDelta = new Vector2(380f, 28f);
        }
        UITheme.StyleBodyText(label.GetComponent<TMP_Text>(), "Tu nombre");

        var input = panel.Find("InputFieldNombreJugador");
        if (input == null)
        {
            var template = UITheme.FindDeep(panel, "InputField (TMP)");
            if (template == null)
            {
                var crearPanel = FindChild(panel.parent, "PanelCrearSala");
                if (crearPanel != null)
                    template = UITheme.FindDeep(crearPanel, "InputField (TMP)");
            }

            if (template != null)
            {
                var clone = Object.Instantiate(template.gameObject, panel, false);
                clone.name = "InputFieldNombreJugador";
                input = clone.transform;
            }
            else
            {
                var inputGo = new GameObject("InputFieldNombreJugador", typeof(RectTransform), typeof(TMP_InputField));
                inputGo.transform.SetParent(panel, false);
                input = inputGo.transform;
            }
        }

        var inputRt = input as RectTransform;
        if (inputRt != null)
            inputRt.sizeDelta = new Vector2(380f, 48f);

        var inputField = input.GetComponent<TMP_InputField>();
        if (inputField != null)
        {
            inputField.characterLimit = 32;
            var bg = input.Find("Text Area")?.GetComponent<Image>();
            if (bg != null) bg.color = UITheme.InputBg;
            var ph = inputField.placeholder as TMP_Text;
            if (ph != null)
            {
                UITheme.EnsureUniqueTextVisual(ph);
                ph.text = "Escribe tu nombre";
                ph.color = UITheme.TextSecondary;
            }
        }
    }

    static void StylePanelBrowser(Transform panel)
    {
        foreach (var tmp in panel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (tmp.GetComponentInParent<Button>() == null && tmp.fontSize >= 28)
                UITheme.StyleTitleText(tmp, "Salas disponibles");
            else if (tmp.GetComponentInParent<Button>() == null)
            {
                UITheme.EnsureUniqueTextVisual(tmp);
                tmp.color = UITheme.TextSecondary;
            }
        }

        UITheme.StyleButton(UITheme.FindDeep(panel, "ButtonRefrescar"), "Refrescar", false);
        UITheme.StyleButton(UITheme.FindDeep(panel, "ButtonVolver"), "Volver", false);

        var lobbys = UITheme.FindDeep(panel, "Lobbys");
        UITheme.StyleListSurface(lobbys);

        var empty = panel.Find("EmptyStateText");
        if (empty == null)
        {
            var go = new GameObject("EmptyStateText", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(panel, false);
            empty = go.transform;
            go.SetActive(false);
        }
        var emptyTmp = empty.GetComponent<TMP_Text>();
        UITheme.StyleBodyText(emptyTmp, "No hay salas. Refresca o crea una.");
        emptyTmp.alignment = TextAlignmentOptions.Center;
    }

    static void StylePanelCrearSala(Transform panel)
    {
        foreach (var tmp in panel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (tmp.GetComponentInParent<Button>() == null && tmp.fontSize >= 28)
                UITheme.StyleTitleText(tmp, "Crear sala");
        }

        foreach (var input in panel.GetComponentsInChildren<TMP_InputField>(true))
        {
            var bg = input.transform.Find("Text Area")?.GetComponent<Image>();
            if (bg != null) bg.color = UITheme.InputBg;
            var ph = input.placeholder as TMP_Text;
            if (ph != null)
            {
                UITheme.EnsureUniqueTextVisual(ph);
                ph.text = "Nombre de la sala";
                ph.color = UITheme.TextSecondary;
            }
        }

        UITheme.StyleButton(UITheme.FindDeep(panel, "ButtonConfirmarHost"), "Crear partida", true);
        UITheme.StyleButton(UITheme.FindDeep(panel, "ButtonVolver"), "Volver", false);
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
        {
            foreach (var t in panelBrowser.GetComponentsInChildren<Transform>(true))
                if (t.name == "Content") content = t;
        }

        if (content != null)
            so.FindProperty("_roomListContent").objectReferenceValue = content;

        var lobbys = panelBrowser != null ? UITheme.FindDeep(panelBrowser, "Lobbys") : null;
        if (lobbys != null)
            so.FindProperty("_roomListPanel").objectReferenceValue = lobbys.gameObject;

        if (panelInicio != null)
        {
            var playerNameInput = UITheme.FindDeep(panelInicio, "InputFieldNombreJugador");
            if (playerNameInput != null)
                so.FindProperty("_inputNombreJugador").objectReferenceValue = playerNameInput.GetComponent<TMP_InputField>();
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
