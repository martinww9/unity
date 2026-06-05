using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class UITheme
{
    private const string UniqueMaterialSuffix = " (UI Instance)";

    public static readonly Color BgPrimary = new(0.102f, 0.137f, 0.196f, 1f);
    public static readonly Color PanelSurface = new(0.141f, 0.204f, 0.278f, 1f);
    public static readonly Color Accent = new(0.239f, 0.545f, 0.992f, 1f);
    public static readonly Color AccentPressed = new(0.18f, 0.42f, 0.78f, 1f);
    public static readonly Color TextPrimary = new(0.941f, 0.957f, 0.973f, 1f);
    public static readonly Color TextSecondary = new(0.659f, 0.722f, 0.8f, 1f);
    public static readonly Color InputBg = new(0.176f, 0.243f, 0.322f, 1f);
    public static readonly Color ListBg = new(0.09f, 0.12f, 0.17f, 1f);

    public static readonly Vector2 ReferenceResolution = new(1920, 1080);

    public static void ApplyCanvasScaler(CanvasScaler scaler)
    {
        if (scaler == null) return;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;
    }

    public static void EnsureUniqueTextVisual(TMP_Text tmp)
    {
        if (tmp == null || tmp.fontSharedMaterial == null) return;

        Material shared = tmp.fontSharedMaterial;
        if (shared.name.EndsWith(UniqueMaterialSuffix))
            return;

        Material unique = Object.Instantiate(shared);
        unique.name = tmp.gameObject.name + UniqueMaterialSuffix;
        tmp.fontSharedMaterial = unique;
        tmp.fontMaterial = unique;
        tmp.UpdateMeshPadding();
    }

    public static void EnsureUniqueButtonVisuals(Transform btn)
    {
        if (btn == null) return;

        var button = btn.GetComponent<Button>();
        if (button != null)
        {
            ColorBlock colors = button.colors;
            button.colors = colors;
        }

        foreach (var tmp in btn.GetComponentsInChildren<TMP_Text>(true))
            EnsureUniqueTextVisual(tmp);
    }

    public static void StylePanelCard(RectTransform panel, float width, float height)
    {
        if (panel == null) return;
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(width, height);
        panel.anchoredPosition = Vector2.zero;
        panel.localScale = Vector3.one;
        StylePanelImage(panel);
    }

    public static void StylePanelImage(Transform panel)
    {
        if (panel == null) return;
        var img = panel.GetComponent<Image>();
        if (img == null) img = panel.gameObject.AddComponent<Image>();
        img.color = PanelSurface;
        img.type = Image.Type.Sliced;
        img.raycastTarget = true;
    }

    public static void StyleListSurface(Transform listRoot)
    {
        if (listRoot == null) return;
        var img = listRoot.GetComponent<Image>();
        if (img == null) img = listRoot.gameObject.AddComponent<Image>();
        img.color = ListBg;
        img.raycastTarget = false;
    }

    public static void StyleTitleText(TMP_Text tmp, string text = null)
    {
        if (tmp == null) return;
        EnsureUniqueTextVisual(tmp);
        if (text != null) tmp.text = text;
        tmp.fontSize = 32;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = TextPrimary;
        tmp.alignment = TextAlignmentOptions.Center;
    }

    public static void StyleBodyText(TMP_Text tmp, string text = null)
    {
        if (tmp == null) return;
        EnsureUniqueTextVisual(tmp);
        if (text != null) tmp.text = text;
        tmp.fontSize = 22;
        tmp.color = TextSecondary;
    }

    public static void StyleHudText(TMP_Text tmp)
    {
        if (tmp == null) return;
        EnsureUniqueTextVisual(tmp);
        tmp.fontSize = 28;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = TextPrimary;
        tmp.alignment = TextAlignmentOptions.Center;
    }

    public static void StyleButton(Transform btn, string label, bool primary)
    {
        if (btn == null) return;
        EnsureUniqueButtonVisuals(btn);
        var button = btn.GetComponent<Button>();
        if (button == null) return;

        if (primary)
            StylePrimaryButton(btn, label);
        else
            StyleSecondaryButton(btn, label);
    }

    public static void StylePrimaryButton(Transform btn, string label = null)
    {
        if (btn == null) return;
        EnsureUniqueButtonVisuals(btn);
        var img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.color = Accent;
            RemoveOutline(btn);
        }

        var button = btn.GetComponent<Button>();
        if (button != null)
        {
            var colors = button.colors;
            colors.normalColor = Accent;
            colors.highlightedColor = Accent * 1.1f;
            colors.pressedColor = AccentPressed;
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
        }

        ApplyButtonLabel(btn, label, TextPrimary);
    }

    public static void StyleSecondaryButton(Transform btn, string label = null)
    {
        if (btn == null) return;
        EnsureUniqueButtonVisuals(btn);
        var img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.color = PanelSurface;
            var outline = btn.GetComponent<Outline>();
            if (outline == null) outline = btn.gameObject.AddComponent<Outline>();
            outline.effectColor = Accent;
            outline.effectDistance = new Vector2(2, -2);
        }

        var button = btn.GetComponent<Button>();
        if (button != null)
        {
            var colors = button.colors;
            colors.normalColor = PanelSurface;
            colors.highlightedColor = new Color(0.2f, 0.28f, 0.38f, 1f);
            colors.pressedColor = AccentPressed;
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
        }

        ApplyButtonLabel(btn, label, Accent);
    }

    public static void StyleHudPill(Transform pill)
    {
        if (pill == null) return;
        var rt = pill.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24f, -24f);
            rt.sizeDelta = new Vector2(140f, 56f);
            rt.localScale = Vector3.one;
        }

        StylePanelImage(pill);
        var outline = pill.GetComponent<Outline>();
        if (outline == null) outline = pill.gameObject.AddComponent<Outline>();
        outline.effectColor = Accent;
        outline.effectDistance = new Vector2(2, -2);
    }

    static void RemoveOutline(Transform btn)
    {
        var outline = btn.GetComponent<Outline>();
        if (outline != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                Object.DestroyImmediate(outline);
            else
#endif
                Object.Destroy(outline);
        }
    }

    static void ApplyButtonLabel(Transform btn, string label, Color textColor)
    {
        var tmp = btn.GetComponentInChildren<TMP_Text>(true);
        if (tmp == null) return;
        EnsureUniqueTextVisual(tmp);
        if (label != null) tmp.text = label;
        tmp.color = textColor;
        tmp.fontSize = 22;
        tmp.fontStyle = FontStyles.Bold;
    }

    public static Transform FindDeep(Transform root, string name)
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

    public static void StyleTextsInHierarchy(Transform root, bool skipButtons = true)
    {
        foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (skipButtons && tmp.GetComponentInParent<Button>() != null) continue;
            if (tmp.fontSize >= 28 || tmp.name.Contains("Title") || tmp.name == "pregunta")
                StyleTitleText(tmp);
            else
                StyleBodyText(tmp);
        }
    }
}
