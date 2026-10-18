using UnityEngine;
using UnityEngine.UI;

// One bounded, short-lived confirmation at the hit location. Never acts as an aiming cursor.
[DefaultExecutionOrder(10100), DisallowMultipleComponent]
public sealed class CombatHitMarker : MaskableGraphic
{
    float began = -100, duration;
    CombatImpactTier tier;
    bool animating;
    Camera worldCamera;
    Vector2 worldPoint;
    public bool IsVisible => Time.time < began + duration;
    public CombatImpactTier Tier => tier;
    public void Show(Camera world, Vector2 position, CombatImpactTier incoming)
    {
        if (world == null || canvas == null) return;
        if (IsVisible && tier > incoming && Time.time - began < .16f) return;
        tier = incoming; began = Time.time; animating = true;
        duration = tier >= CombatImpactTier.Kill ? .30f : .13f;
        worldCamera = world; worldPoint = position;
        PlaceAtHit(); raycastTarget = false; SetVerticesDirty();
    }
    void PlaceAtHit()
    {
        if (worldCamera == null || canvas == null) return;
        var root = (RectTransform)transform.parent;
        Vector2 point;
        var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, worldCamera.WorldToScreenPoint(worldPoint), uiCamera, out point);
        rectTransform.localPosition = new Vector3(Mathf.Clamp(point.x, root.rect.xMin + 35, root.rect.xMax - 35),
            Mathf.Clamp(point.y, root.rect.yMin + 35, root.rect.yMax - 35), 0);
    }
    void LateUpdate()
    {
        if (!animating) return;
        PlaceAtHit(); SetVerticesDirty(); if (!IsVisible) animating = false;
    }
    public void Clear() { began = -100; animating = false; SetVerticesDirty(); }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); if (!IsVisible) return;
        float t = Mathf.Clamp01((Time.time - began) / duration);
        bool kill = tier >= CombatImpactTier.Kill;
        float gap = kill ? 11 : 7, length = kill ? 11 : 7, width = kill ? 3.2f : 2.3f;
        gap += (kill ? 5 : 2) * (1 - Mathf.Pow(1 - t, 3));
        float alpha = Mathf.Clamp01((1 - t) / .6f);
        Color tint = kill ? new Color(1, .79f, .38f, alpha) : new Color(.97f, 1, .95f, alpha);
        for (int pass = 0; pass < 2; pass++) for (int i = 0; i < 4; i++)
        {
            float angle = (45 + 90 * i) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Stroke(vh, direction * gap, direction * (gap + length), width + (pass == 0 ? 3 : 0),
                pass == 0 ? new Color(.035f, .055f, .045f, alpha * .85f) : tint);
        }
    }
    static void Stroke(VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint)
    {
        Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x) * width * .5f;
        int start = vh.currentVertCount;
        vh.AddVert(a - n, tint, Vector2.zero); vh.AddVert(a + n, tint, Vector2.zero);
        vh.AddVert(b + n, tint, Vector2.zero); vh.AddVert(b - n, tint, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
    }
}
