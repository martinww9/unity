using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerNameTag : MonoBehaviour
{
    [SerializeField] private Transform _anchor;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private float _heightOffset = 4.2f;

    private Player _player;
    private string _lastDisplayedName;

    private void Awake()
    {
        _player = GetComponentInParent<Player>();
        if (_anchor == null)
            _anchor = transform;

        EnsureVisuals();
    }

    private void LateUpdate()
    {
        RefreshVisibility();
        RefreshLabel();
    }

    private void RefreshVisibility()
    {
        if (_player == null || _player.Object == null)
            return;

        bool visible = !_player.Object.HasInputAuthority;
        if (gameObject.activeSelf != visible)
            gameObject.SetActive(visible);
    }

    private void RefreshLabel()
    {
        if (_nameText == null || _player == null || _player.Object == null)
            return;

        string displayName = _player.GetDisplayName();
        if (displayName == _lastDisplayedName)
            return;

        _lastDisplayedName = displayName;
        _nameText.text = displayName;
    }

    private void EnsureVisuals()
    {
        if (_nameText != null)
            return;

        if (_anchor == null)
            _anchor = transform;

        _anchor.localPosition = new Vector3(0f, _heightOffset, 0f);

        var canvasGo = new GameObject("NameTagCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(_anchor, false);
        canvasGo.transform.localPosition = Vector3.zero;
        canvasGo.transform.localRotation = Quaternion.identity;
        canvasGo.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        if (canvasGo.GetComponent<Fusion.FusionBasicBillboard>() == null)
            canvasGo.AddComponent<Fusion.FusionBasicBillboard>();

        var bgGo = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgGo.transform.SetParent(canvasGo.transform, false);
        var bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(260f, 48f);
        var bgImage = bgGo.GetComponent<Image>();
        bgImage.color = new Color(0.09f, 0.12f, 0.17f, 0.75f);
        bgImage.raycastTarget = false;

        var textGo = new GameObject("NameText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(canvasGo.transform, false);
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(260f, 48f);

        _nameText = textGo.GetComponent<TextMeshProUGUI>();
        _nameText.fontSize = 26f;
        _nameText.alignment = TextAlignmentOptions.Center;
        _nameText.color = UITheme.TextPrimary;
        _nameText.raycastTarget = false;
        _nameText.text = "Jugador";
    }
}
