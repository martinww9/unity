#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class FeedbackPanelSetup
{
    const string MenuRoot = "Tools/Juego/";
    const string ScenePath = "Assets/Scenes/JuegoEscena/Juego.unity";

    [MenuItem(MenuRoot + "Setup Feedback Panel")]
    public static void SetupFeedbackPanel()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var panelFeedback = GameObject.Find("PanelFeedback");
        if (panelFeedback == null)
        {
            Debug.LogError("FeedbackPanelSetup: No se encontró 'PanelFeedback' en la escena.");
            return;
        }

        var triviaUi = Object.FindFirstObjectByType<TriviaUI>();
        if (triviaUi == null)
        {
            Debug.LogError("FeedbackPanelSetup: No se encontró TriviaUI en la escena.");
            return;
        }

        RemoveFeedbackPanelLayoutGroup(panelFeedback);
        HideLegacyFeedbackTexts(panelFeedback.transform);

        Transform scrollRoot = panelFeedback.transform.Find("FeedbackScroll");
        if (scrollRoot == null)
        {
            var scrollGo = new GameObject("FeedbackScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollRoot = scrollGo.transform;
            Undo.RegisterCreatedObjectUndo(scrollGo, "Create FeedbackScroll");
            Undo.SetTransformParent(scrollRoot, panelFeedback.transform, "Parent FeedbackScroll");
        }

        var scrollRect = scrollRoot.GetComponent<ScrollRect>();
        var scrollRt = scrollRoot.GetComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0.05f, 0.12f);
        scrollRt.anchorMax = new Vector2(0.95f, 0.82f);
        scrollRt.offsetMin = Vector2.zero;
        scrollRt.offsetMax = Vector2.zero;
        scrollRt.localScale = Vector3.one;

        var scrollImage = scrollRoot.GetComponent<Image>();
        scrollImage.color = UITheme.ListBg;
        scrollImage.raycastTarget = true;

        Transform viewport = scrollRoot.Find("Viewport");
        if (viewport == null)
        {
            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport = viewportGo.transform;
            Undo.RegisterCreatedObjectUndo(viewportGo, "Create Feedback Viewport");
            viewport.SetParent(scrollRoot, false);
        }

        var viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        var mask = viewport.GetComponent<Mask>();
        mask.showMaskGraphic = false;

        var viewportRt = viewport.GetComponent<RectTransform>();
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = Vector2.zero;
        viewportRt.localScale = Vector3.one;

        Transform content = viewport.Find("Content");
        if (content == null)
        {
            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content = contentGo.transform;
            Undo.RegisterCreatedObjectUndo(contentGo, "Create Feedback Content");
            content.SetParent(viewport, false);
        }

        var contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0f, 0f);
        contentRt.localScale = Vector3.one;

        var layout = content.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = content.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = contentRt;
        scrollRect.viewport = viewportRt;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRoot.SetSiblingIndex(0);

        Transform paginationRoot = panelFeedback.transform.Find("FeedbackPagination");
        TMP_Text pageTitle = null;
        Button pageButton = null;
        TMP_Text pageButtonText = null;

        if (paginationRoot == null)
        {
            var paginationGo = new GameObject("FeedbackPagination", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            paginationRoot = paginationGo.transform;
            Undo.RegisterCreatedObjectUndo(paginationGo, "Create FeedbackPagination");
            paginationRoot.SetParent(panelFeedback.transform, false);

            var paginationRt = paginationGo.GetComponent<RectTransform>();
            paginationRt.anchorMin = new Vector2(0.05f, 0.02f);
            paginationRt.anchorMax = new Vector2(0.95f, 0.1f);
            paginationRt.offsetMin = Vector2.zero;
            paginationRt.offsetMax = Vector2.zero;
            paginationRt.localScale = Vector3.one;

            var paginationLayout = paginationGo.GetComponent<HorizontalLayoutGroup>();
            paginationLayout.padding = new RectOffset(8, 8, 4, 4);
            paginationLayout.spacing = 12f;
            paginationLayout.childAlignment = TextAnchor.MiddleCenter;
            paginationLayout.childControlWidth = true;
            paginationLayout.childControlHeight = true;
            paginationLayout.childForceExpandWidth = true;
            paginationLayout.childForceExpandHeight = true;
        }

        Transform titleTr = paginationRoot.Find("PageTitle");
        if (titleTr == null)
        {
            var titleGo = new GameObject("PageTitle", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            titleTr = titleGo.transform;
            Undo.RegisterCreatedObjectUndo(titleGo, "Create Feedback PageTitle");
            titleTr.SetParent(paginationRoot, false);
            pageTitle = titleGo.GetComponent<TextMeshProUGUI>();
            var titleLayout = titleGo.GetComponent<LayoutElement>();
            titleLayout.flexibleWidth = 1f;
            titleLayout.minWidth = 200f;
            UITheme.StyleHudText(pageTitle);
            pageTitle.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
            pageTitle.text = "Nivel 1 · Explicaciones";
        }
        else
        {
            pageTitle = titleTr.GetComponent<TMP_Text>();
        }

        paginationRoot.SetAsLastSibling();

        Transform buttonTr = paginationRoot.Find("PageButton");
        if (buttonTr == null)
        {
            var buttonGo = new GameObject("PageButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonTr = buttonGo.transform;
            Undo.RegisterCreatedObjectUndo(buttonGo, "Create Feedback PageButton");
            buttonTr.SetParent(paginationRoot, false);

            var buttonRt = buttonGo.GetComponent<RectTransform>();
            buttonRt.sizeDelta = new Vector2(260f, 44f);

            var buttonLayout = buttonGo.GetComponent<LayoutElement>();
            buttonLayout.minWidth = 220f;
            buttonLayout.preferredWidth = 260f;
            buttonLayout.minHeight = 44f;
            buttonLayout.preferredHeight = 44f;

            pageButton = buttonGo.GetComponent<Button>();
            pageButtonText = EnsurePageButtonLabel(buttonTr, "Siguiente nivel");
        }
        else
        {
            pageButton = buttonTr.GetComponent<Button>();
            pageButtonText = EnsurePageButtonLabel(buttonTr, "Siguiente nivel");
        }

        if (pageButton != null)
        {
            pageButton.onClick.RemoveAllListeners();
            pageButton.onClick.AddListener(triviaUi.UI_BotonFeedbackSiguienteNivel);
        }

        Undo.RecordObject(triviaUi, "Assign feedback scroll content");
        var so = new SerializedObject(triviaUi);
        so.FindProperty("panelFeedbackFinal").objectReferenceValue = panelFeedback;
        so.FindProperty("_feedbackScrollContent").objectReferenceValue = content;
        so.FindProperty("_feedbackPageTitle").objectReferenceValue = pageTitle;
        so.FindProperty("_feedbackPageButton").objectReferenceValue = pageButton;
        so.FindProperty("_feedbackPageButtonText").objectReferenceValue = pageButtonText;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(panelFeedback);
        EditorUtility.SetDirty(triviaUi);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Selection.activeGameObject = panelFeedback;
        Debug.Log("FeedbackPanelSetup: ScrollRect configurado y referencia asignada a TriviaUI.", panelFeedback);
    }

    static void RemoveFeedbackPanelLayoutGroup(GameObject panelFeedback)
    {
        var layoutGroup = panelFeedback.GetComponent<VerticalLayoutGroup>();
        if (layoutGroup == null)
            return;

        Undo.DestroyObjectImmediate(layoutGroup);
    }

    static void HideLegacyFeedbackTexts(Transform panelFeedback)
    {
        foreach (var name in new[] { "Feedback", "Fortalezas", "Mejoras", "MensajeGeneral" })
        {
            Transform child = panelFeedback.Find(name);
            if (child != null)
                child.gameObject.SetActive(false);
        }

        for (int i = 0; i < panelFeedback.childCount; i++)
        {
            Transform child = panelFeedback.GetChild(i);
            if (child.name.StartsWith("Text (TMP)"))
                child.gameObject.SetActive(false);
        }
    }

    static TMP_Text EnsurePageButtonLabel(Transform buttonTr, string label)
    {
        Transform labelTr = buttonTr.Find("Label");
        if (labelTr == null)
        {
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelTr = labelGo.transform;
            Undo.RegisterCreatedObjectUndo(labelGo, "Create Feedback PageButton Label");
            labelTr.SetParent(buttonTr, false);

            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
        }

        TMP_Text labelText = labelTr.GetComponent<TMP_Text>();
        UITheme.StylePrimaryButton(buttonTr, label);
        return labelText;
    }
}
#endif
