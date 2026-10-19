using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Opt-in on the integrated effect prefabs; the original effect prefabs keep their legacy visuals.
// One reusable mesh per pooled effect, no particles/GameObjects allocated on a hit or per frame.
[DisallowMultipleComponent]
public sealed class UpgradeEffectVisual : MonoBehaviour
{
    public UpgradeVfxProfile profile;
    public bool Ready => profile != null && profile.material != null;
    public float Duration { get; private set; }
    public float Radius { get; private set; }
    public Vector2 StartPoint { get; private set; }
    public Vector2 EndPoint { get; private set; }
    public int Hop { get; private set; }
    public int VertexCount => vertices.Count;
    public bool Visible => meshRenderer != null && meshRenderer.enabled;
    public float Elapsed { get; private set; }

    Mesh mesh;
    MeshRenderer meshRenderer;
    SpriteRenderer flame;
    readonly List<Vector3> vertices = new List<Vector3>(1024);
    readonly List<Vector3> uv = new List<Vector3>(1024);
    readonly List<Color> colors = new List<Color>(1024);
    readonly List<int> triangles = new List<int>(2048);
    readonly Vector2[] bolt = new Vector2[21];
    bool lightning;
    float delay, seed;
    int generation;

    void Awake()
    {
        if (!Ready) return;
        mesh = new Mesh { name = "Pooled special upgrade mesh" }; mesh.MarkDynamic();
        var filter = gameObject.AddComponent<MeshFilter>(); filter.sharedMesh = mesh;
        meshRenderer = gameObject.AddComponent<MeshRenderer>(); meshRenderer.sharedMaterial = profile.material;
        meshRenderer.sortingLayerName = "VFX"; meshRenderer.sortingOrder = 6;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off; meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off; meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        meshRenderer.enabled = false;
        if (GetComponent<ExplosionEffect>() != null && profile.explosionFrames != null && profile.explosionFrames.Length > 0)
        {
            flame = new GameObject("Pooled explosion animation").AddComponent<SpriteRenderer>();
            flame.transform.SetParent(transform, false); flame.sortingLayerName = "VFX"; flame.sortingOrder = 5;
            flame.enabled = false;
        }
    }

    public void BeginExplosion(Vector3 position, float radius)
    {
        lightning = false; delay = 0; Hop = 0; Radius = radius;
        transform.position = position; transform.rotation = Quaternion.identity; transform.localScale = Vector3.one;
        Duration = profile.explosionDuration; seed = ++generation * 7.13f;
        Draw(0); UpgradeVfxAudio.Request(profile, false, position);
    }

    public void BeginLightning(Vector3 start, Vector3 end, int hop)
    {
        lightning = true; Radius = 0; Hop = Mathf.Max(0, hop);
        StartPoint = start; EndPoint = end;
        transform.position = start; transform.rotation = Quaternion.identity; transform.localScale = Vector3.one;
        delay = Mathf.Min(4, Hop) * profile.hopDelay; Duration = profile.lightningDuration + delay;
        seed = ++generation * 11.29f + start.x * .13f + end.y * .31f;
        Draw(0);
        // One electrical sound per chain, not one per hop.
        if (Hop == 0) UpgradeVfxAudio.Request(profile, true, start);
    }

    public void Draw(float elapsed)
    {
        Elapsed = elapsed;
        if (mesh == null) return;
        if (!UpgradeVfxAudio.GameplayVisible || elapsed < delay || elapsed >= Duration) { Hide(); return; }
        vertices.Clear(); uv.Clear(); colors.Clear(); triangles.Clear();
        float t = Mathf.Clamp01((elapsed - delay) / (Duration - delay));
        if (lightning) DrawLightning(t, elapsed - delay); else DrawExplosion(t);
        mesh.Clear(false); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0, false);
        mesh.RecalculateBounds(); meshRenderer.enabled = true;
    }

    void DrawExplosion(float t)
    {
        float r = Radius, fade = 1 - t;
        Color amber = profile.explosionColor;
        if (flame != null)
        {
            int frame = Mathf.Min(profile.explosionFrames.Length - 1, Mathf.FloorToInt(t * profile.explosionFrames.Length));
            flame.sprite = profile.explosionFrames[frame]; flame.enabled = true;
            float diameter = Mathf.Max(.01f, flame.sprite.bounds.size.x);
            flame.transform.localScale = Vector3.one * (r * 1.8f / diameter);
            flame.color = new Color(1, .9f, .78f, .78f * Mathf.Clamp01((1 - t) / .32f));
        }
        float sweep = 1 - Mathf.Pow(1 - Mathf.Clamp01(t / .57f), 3);
        float ringRadius = r * Mathf.Lerp(.16f, 1, sweep);
        // A faint residual cloud sits behind the short luminous burst, never hiding enemies.
        for (int i = 0; i < 7; i++)
        {
            Vector2 direction = Direction(i * 2.39996f + seed);
            float variation = Noise(i + 2);
            Disc(direction * r * (.13f + t * .44f), r * (.12f + t * .17f),
                Tint(new Color(.15f, .13f, .12f), Mathf.Sin(t * Mathf.PI) * .19f), i);
            float burn = Mathf.Clamp01(1 - t * 2.35f);
            if (flame == null)
                Disc(direction * r * (.08f + t * .56f), r * (.13f + variation * .09f) * (1 - t * .6f),
                    Tint(Color.Lerp(amber, new Color(1, .8f, .3f), burn), burn * .92f), i + 1);
        }
        Disc(Vector2.zero, r * (.48f + t * .25f), Tint(amber, fade * fade * .20f));
        // The outer edge reaches the actual damage radius. It does not imply an additional damage tick.
        Ring(ringRadius, Mathf.Min(.18f, r * .12f) * (1 - t * .65f), Tint(amber, fade * .42f));
        float flash = Mathf.Clamp01(1 - t * 4.2f);
        Disc(Vector2.zero, r * (.24f + t * .45f), Tint(new Color(1, .94f, .73f), flash * .95f));
        for (int i = 0; i < 13; i++)
        {
            float n = Noise(i + 19), angle = i * 2.39996f + seed;
            Vector2 direction = Direction(angle);
            float travel = r * (.15f + .72f * (1 - Mathf.Pow(1 - t, 2))) * (.65f + n * .35f);
            float length = r * (.07f + n * .1f) * fade;
            Vector2 tip = direction * travel;
            Ribbon(tip - direction * length, tip, .025f + .025f * fade,
                Tint(Color.Lerp(amber, new Color(1, .9f, .59f), n), fade));
            Disc(tip, .035f + .025f * fade, Tint(new Color(1, .86f, .46f), fade));
        }
    }

    void DrawLightning(float t, float seconds)
    {
        Vector2 delta = EndPoint - StartPoint;
        float length = delta.magnitude;
        if (length < .001f) return;
        Vector2 direction = delta / length, perpendicular = new Vector2(-direction.y, direction.x);
        int segments = Mathf.Clamp(Mathf.CeilToInt(length / .3f), 5, 20);
        int flicker = Mathf.FloorToInt(seconds / .035f);
        float amplitude = Mathf.Min(.24f, length * .10f);
        float fade = Mathf.Pow(1 - t, .65f) * (flicker % 3 == 1 ? .68f : 1);
        for (int i = 0; i <= segments; i++)
        {
            float f = i == 0 || i == segments ? (float)i / segments : (i + (Noise(i * 11 + flicker * 5) - .5f) * .65f) / segments;
            float offset = (Noise(i * 3 + flicker * 23) * 2 - 1) * amplitude;
            bolt[i] = delta * f + perpendicular * (i == 0 || i == segments ? 0 : offset);
        }
        Color blue = profile.lightningColor;
        // Separate broad glow, blue conductor and thin white core; no post-processing dependency.
        for (int layer = 0; layer < 3; layer++)
        {
            float width = layer == 0 ? .38f : layer == 1 ? .14f : .055f;
            Color color = Tint(layer == 2 ? new Color(.91f, .99f, 1) : blue, fade * (layer == 0 ? .16f : .95f));
            for (int i = 0; i < segments; i++) Ribbon(bolt[i], bolt[i + 1], width, color);
        }
        for (int i = 0; i < 2; i++)
        {
            Vector2 root = bolt[segments * (i + 1) / 3];
            Vector2 spur = root + perpendicular * (i == 0 ? 1 : -1) * Mathf.Min(.42f, length * .17f);
            Vector2 bend = Vector2.Lerp(root, spur, .58f) + direction * .13f;
            Ribbon(root, bend, .045f, Tint(blue, fade * .6f));
            Ribbon(bend, spur, .025f, Tint(blue, fade * .3f));
        }
        Disc(Vector2.zero, .20f, Tint(blue, fade * .55f));
        Disc(delta, .32f, Tint(blue, fade * .7f));
        Disc(delta, .10f, Tint(new Color(.87f, .98f, 1), fade));
        for (int i = 0; i < 5; i++)
        {
            Vector2 ray = Direction(i * 2.39996f + seed);
            Vector2 tip = delta + ray * (.08f + t * .38f);
            Ribbon(tip, tip + ray * .12f * (1 - t), .028f, Tint(blue, fade * .8f));
        }
    }

    float Noise(int index) => Mathf.Repeat(Mathf.Sin(index * 127.1f + seed * 311.7f) * 43758.5453f, 1);
    static Vector2 Direction(float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    static Color Tint(Color color, float alpha) { color.a = alpha; return color; }
    void Disc(Vector2 center, float radius, Color color, int rotation = 0)
    {
        if (color.a <= .001f || radius <= .001f) return;
        Vector2 a = Direction(rotation * 1.71f) * radius, b = new Vector2(-a.y, a.x);
        Quad(center - a - b, center + a - b, center + a + b, center - a + b, color, 0);
    }
    void Ribbon(Vector2 a, Vector2 b, float width, Color color)
    {
        Vector2 delta = b - a;
        if (delta.sqrMagnitude < .000001f || width < .0001f) return;
        Vector2 side = new Vector2(-delta.y, delta.x).normalized * width * .5f;
        Quad(a - side, b - side, b + side, a + side, color, 1);
    }
    void Ring(float radius, float width, Color color)
    {
        const int segments = 48;
        for (int i = 0; i < segments; i++)
        {
            Vector2 a = Direction(i * Mathf.PI * 2 / segments), b = Direction((i + 1) * Mathf.PI * 2 / segments);
            // Width lies inside the damage boundary, including the feathered edge.
            Quad(a * (radius - width), b * (radius - width), b * radius, a * radius, color, 1);
        }
    }
    void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color, float kind)
    {
        int n = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
        uv.Add(new Vector3(-1, -1, kind)); uv.Add(new Vector3(1, -1, kind));
        uv.Add(new Vector3(1, 1, kind)); uv.Add(new Vector3(-1, 1, kind));
        for (int i = 0; i < 4; i++) colors.Add(color);
        triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 2);
        triangles.Add(n); triangles.Add(n + 2); triangles.Add(n + 3);
    }
    public void Hide() { if (meshRenderer != null) meshRenderer.enabled = false; if (flame != null) flame.enabled = false; }
    void OnDisable() { Hide(); }
    void OnDestroy() { if (mesh != null) Destroy(mesh); }
}
