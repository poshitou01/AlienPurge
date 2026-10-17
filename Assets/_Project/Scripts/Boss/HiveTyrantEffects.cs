using System.Collections.Generic;
using UnityEngine;

// Small fixed pools owned by one encounter. Geometry has no damage callbacks.
[DisallowMultipleComponent]
public sealed class HiveTyrantEffects : MonoBehaviour
{
    [SerializeField] private Material materialTemplate;
    private GameObject ownedRoot;
    private Material material;
    private HiveTyrantCombat boss;
    private readonly Surface[] bursts = new Surface[10];
    private readonly Surface[] wave = new Surface[3];
    private readonly HiveTyrantTelegraph[] warnings = new HiveTyrantTelegraph[12];
    private readonly float[] ages = new float[10];
    private readonly Vector2[] origins = new Vector2[10], directions = new Vector2[10];
    private readonly float[] radii = new float[10], angles = new float[10];
    private readonly int[] kinds = new int[10];
    public int ActiveCount { get; private set; }
    public int OwnedMeshCount => 25;
    public Material SharedMaterial => material;
    private void Awake()
    {
        boss = GetComponent<HiveTyrantCombat>();
        ownedRoot = new GameObject("HiveTyrant_EncounterEffects");
        material = materialTemplate != null ? new Material(materialTemplate) : new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
        material.name = "HiveTyrant_VertexTint"; material.mainTexture = Texture2D.whiteTexture;
        for (int i = 0; i < bursts.Length; i++) { bursts[i] = new Surface(ownedRoot.transform, "Release_" + i, material, "Effects", 20); ages[i] = -1; }
        for (int i = 0; i < wave.Length; i++) wave[i] = new Surface(ownedRoot.transform, "GroundWave_" + i, material, "Enemies", -20);
        for (int i = 0; i < warnings.Length; i++)
        {
            var go = new GameObject("HiveTyrant_Warning_" + i); go.transform.SetParent(ownedRoot.transform);
            warnings[i] = go.AddComponent<HiveTyrantTelegraph>(); warnings[i].Initialize(material); go.SetActive(false);
        }
    }
    public HiveTyrantTelegraph Warning(Vector2 origin, Vector2 direction, float radius, float angle, Color tint)
    {
        foreach (var warning in warnings)
            if (!warning.gameObject.activeSelf) { warning.Show(origin, direction, radius, angle, tint); return warning; }
        Debug.LogError("HiveTyrant warning capacity exceeded", this); return null;
    }
    public void Burst(int kind, Vector2 origin, Vector2 direction, float radius = 1, float angle = 360)
    {
        for (int i = 0; i < bursts.Length; i++)
            if (ages[i] < 0)
            { ages[i] = 0; kinds[i] = kind; origins[i] = origin; directions[i] = direction; radii[i] = radius; angles[i] = angle; return; }
    }
    private void Update()
    {
        ActiveCount = 0;
        float lifetime = boss.Config != null ? boss.Config.effectLifetime : .45f;
        int fragments = boss.Config != null ? boss.Config.effectFragmentCount : 16;
        for (int i = 0; i < bursts.Length; i++)
        {
            if (ages[i] < 0) continue;
            ages[i] += Time.deltaTime;
            if (ages[i] >= lifetime) { ages[i] = -1; bursts[i].Hide(); continue; }
            ActiveCount++;
            float t = ages[i] / lifetime;
            Surface surface = bursts[i]; surface.Clear();
            float heading = Mathf.Atan2(directions[i].y, directions[i].x);
            Color tint = kinds[i] == 2 ? new Color(.72f, .58f, .85f, 1 - t) : new Color(1, .9f, .69f, 1 - t);
            if (kinds[i] == 0)
            {
                for (int n = 0; n < 28; n++)
                {
                    float a = heading + Mathf.Lerp(-angles[i] * .5f, angles[i] * .5f, n / 28f) * Mathf.Deg2Rad;
                    float b = heading + Mathf.Lerp(-angles[i] * .5f, angles[i] * .5f, (n + 1) / 28f) * Mathf.Deg2Rad;
                    float r = radii[i] * (.88f + .1f * t), thickness = .3f * (1 - t);
                    surface.Quad(origins[i] + Unit(a) * (r - thickness), origins[i] + Unit(b) * (r - thickness), origins[i] + Unit(b) * r, origins[i] + Unit(a) * r, tint);
                }
            }
            for (int n = 0; n < fragments; n++)
            {
                float a = heading + (kinds[i] == 0 ? Mathf.Lerp(-angles[i] * .5f, angles[i] * .5f, n / (float)fragments) * Mathf.Deg2Rad : n * 2.39996f);
                Vector2 point = origins[i] + Unit(a) * radii[i] * (.4f + .5f * t);
                point += Vector2.up * Mathf.Sin(t * Mathf.PI) * .25f;
                float size = (.04f + .025f * (n % 3)) * (1 - t);
                surface.Diamond(point, size, tint);
            }
            surface.Apply();
        }
    }
    public void DrawWave(HiveTyrantWavePlan plan, float distance, bool warning)
    {
        for (int s = 0; s < 3; s++)
        {
            Surface surface = wave[s]; surface.Clear();
            float length = plan.lengths[s];
            if (length <= 0 || (!warning && distance > length + plan.thickness * .5f)) { surface.Hide(); continue; }
            float x = plan.SegmentCenter(s), half = plan.width * .5f;
            if (warning)
            {
                Rect(surface, plan, x - half, x + half, 0, length, new Color(.8f, .48f, .21f, .13f));
                Rect(surface, plan, x - half, x - half + .055f, 0, length, new Color(.98f, .82f, .51f, .7f));
                Rect(surface, plan, x + half - .055f, x + half, 0, length, new Color(.98f, .82f, .51f, .7f));
                for (float z = .6f; z < length; z += 1.6f)
                {
                    Vector2 a = plan.Point(x - .35f, z - .28f), b = plan.Point(x, z), c = plan.Point(x + .35f, z - .28f);
                    surface.Line(a, b, .07f, new Color(1, .85f, .55f, .55f)); surface.Line(b, c, .07f, new Color(1, .85f, .55f, .55f));
                }
            }
            else
            {
                float front = Mathf.Min(length, distance + plan.thickness * .5f), back = Mathf.Max(0, distance - plan.thickness * .5f);
                if (front <= back) { surface.Hide(); continue; }
                // Every rock and dust fragment remains strictly within its dangerous segment.
                int fragments = boss.Config != null ? Mathf.Clamp(boss.Config.effectFragmentCount, 8, 24) : 16;
                for (int j = 0; j < fragments; j++)
                {
                    float left = x - half + j * plan.width / fragments, right = left + plan.width / fragments;
                    float crest = front - .09f * (j % 3);
                    float heel = Mathf.Lerp(back, front, .16f + .14f * (j % 3));
                    Vector2 a = plan.Point(left + .025f, heel), b = plan.Point(right - .025f, heel + .04f), c = plan.Point(right, crest), d = plan.Point(left, front);
                    surface.Quad(a, b, c, d, new Color(.38f + .07f * (j % 2), .34f, .29f, .88f));
                    surface.Line(d, c, .085f, new Color(1, .92f, .73f, .95f));
                    surface.Diamond(plan.Point((left + right) * .5f, Mathf.Lerp(back, front, .3f + .12f * (j % 3))), .075f, new Color(.88f, .8f, .66f, .8f));
                    if (j % 3 == 0) surface.Diamond(plan.Point((left + right) * .5f, back + .08f), .11f, new Color(.74f, .7f, .62f, .2f));
                }
            }
            surface.Apply();
        }
    }
    private static void Rect(Surface mesh, HiveTyrantWavePlan plan, float l, float r, float b, float f, Color color)
    { mesh.Quad(plan.Point(l, b), plan.Point(r, b), plan.Point(r, f), plan.Point(l, f), color); }
    public void HideWave() { foreach (var surface in wave) if (surface != null) surface.Hide(); }
    public void ClearAll()
    {
        for (int i = 0; i < bursts.Length; i++) { ages[i] = -1; if (bursts[i] != null) bursts[i].Hide(); }
        foreach (var warning in warnings) if (warning != null) warning.Release();
        HideWave(); ActiveCount = 0;
    }
    private void OnDisable() { ClearAll(); }
    private void OnDestroy()
    {
        foreach (var surface in bursts) surface?.Dispose(); foreach (var surface in wave) surface?.Dispose();
        if (ownedRoot != null) Destroy(ownedRoot); if (material != null) Destroy(material);
    }
    private static Vector2 Unit(float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    public sealed class Surface
    {
        public readonly GameObject go;
        private readonly Mesh mesh;
        private readonly List<Vector3> vertices = new List<Vector3>(1024);
        private readonly List<int> triangles = new List<int>(1536);
        private readonly List<Color> colors = new List<Color>(1024);
        public Surface(Transform root, string name, Material material, string layer, int order)
        {
            go = new GameObject(name); go.transform.SetParent(root, false);
            mesh = new Mesh { name = name }; mesh.MarkDynamic(); go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.sortingLayerName = layer; renderer.sortingOrder = order;
            go.SetActive(false);
        }
        public void Clear() { vertices.Clear(); triangles.Clear(); colors.Clear(); }
        public void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            int n = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            for (int i = 0; i < 4; i++) colors.Add(color);
            triangles.Add(n); triangles.Add(n + 2); triangles.Add(n + 1); triangles.Add(n); triangles.Add(n + 3); triangles.Add(n + 2);
        }
        public void Line(Vector2 a, Vector2 b, float width, Color color)
        { Vector2 along = (b - a).normalized, side = new Vector2(-along.y, along.x) * width * .5f; Quad(a - side, b - side, b + side, a + side, color); }
        public void Diamond(Vector2 center, float size, Color color)
        { Quad(center + Vector2.left * size, center + Vector2.up * size, center + Vector2.right * size, center + Vector2.down * size, color); }
        public void Apply()
        { mesh.Clear(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds(); go.SetActive(vertices.Count > 0); }
        public void Hide() { if (go != null) go.SetActive(false); }
        public void Dispose() { if (mesh != null) Object.Destroy(mesh); }
    }
}
