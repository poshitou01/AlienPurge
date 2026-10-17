using UnityEngine;
using UnityEngine.UI;

// Owned runtime UI only; never finds or rewrites other HUD canvases.
public static class HiveTyrantHudLayout
{
    public static readonly Color Ink = new Color(.045f, .055f, .06f, .92f);
    public static readonly Color Line = new Color(.44f, .47f, .4f, .5f);
    public static readonly Color Paper = new Color(.93f, .94f, .87f);
    public static readonly Color Muted = new Color(.66f, .7f, .65f);
    public static readonly Color Brood = new Color(.67f, .78f, .42f);
    public static readonly Color Rift = new Color(.94f, .64f, .33f);
    private static Font font;
    public static Canvas Canvas(string name, int order, bool interactive = false)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = order;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
        if (interactive) root.AddComponent<GraphicRaycaster>();
        return canvas;
    }
    public static RectTransform Root(Transform parent, string name, Vector2 size, Vector2 anchor, Vector2 pivot, Vector2 offset)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; rect.sizeDelta = size; rect.anchoredPosition = offset; return rect;
    }
    public static RectTransform Rect(Transform parent, string name, Rect area)
    { return Root(parent, name, area.size, new Vector2(0, 1), new Vector2(0, 1), new Vector2(area.x, -area.y)); }
    public static Image Box(Transform parent, string name, Rect area, Color color, bool raycast = false)
    {
        var image = Rect(parent, name, area).gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = raycast; return image;
    }
    public static Text Label(Transform parent, string name, Rect area, string value, int size, Color color, TextAnchor alignment = TextAnchor.MiddleLeft, bool bold = false)
    {
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var text = Rect(parent, name, area).gameObject.AddComponent<Text>(); text.font = font; text.fontSize = size;
        text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; text.text = value; text.color = color; text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Truncate; text.raycastTarget = false;
        return text;
    }
    public static Button Button(Transform parent, string name, Rect area, string caption, UnityEngine.Events.UnityAction clicked)
    {
        var face = Box(parent, name, area, Brood, true); var button = face.gameObject.AddComponent<Button>(); button.targetGraphic = face;
        var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1.13f, 1.13f, 1.13f);
        colors.selectedColor = colors.highlightedColor; colors.pressedColor = new Color(.75f, .8f, .65f); button.colors = colors;
        Label(face.transform, "Label", new Rect(0, 0, area.width, area.height), caption, 15, Ink, TextAnchor.MiddleCenter, true);
        button.onClick.AddListener(clicked); return button;
    }
}
