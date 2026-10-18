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
    private readonly Surface[] wave = new Surface[HiveTyrantWavePlan.MaximumSegments];
    private readonly Surface[] crescents = new Surface[2];
    private Surface spores, sweepGuide;
    private readonly HiveTyrantTelegraph[] warnings = new HiveTyrantTelegraph[12];
    private readonly float[] ages = new float[10];
    private readonly Vector2[] origins = new Vector2[10], directions = new Vector2[10];
    private readonly float[] radii = new float[10], angles = new float[10];
    private readonly int[] kinds = new int[10];
    public int ActiveCount { get; private set; }
    public int OwnedMeshCount => bursts.Length + wave.Length + crescents.Length + warnings.Length + 2;
    public Material SharedMaterial => material;
    private void Awake()
    {
        boss = GetComponent<HiveTyrantCombat>();
        ownedRoot = new GameObject("HiveTyrant_EncounterEffects");
        material = materialTemplate != null ? new Material(materialTemplate) : new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
        material.name = "HiveTyrant_VertexTint"; material.mainTexture = Texture2D.whiteTexture;
        for (int i = 0; i < bursts.Length; i++) { bursts[i] = new Surface(ownedRoot.transform, "Release_" + i, material, "Effects", 20); ages[i] = -1; }
        for (int i = 0; i < wave.Length; i++) wave[i] = new Surface(ownedRoot.transform, "GroundWave_" + i, material, "Enemies", -20);
        for (int i = 0; i < crescents.Length; i++) crescents[i] = new Surface(ownedRoot.transform, "Crescent_" + i, material, "Enemies", -18);
        spores = new Surface(ownedRoot.transform, "SporeBarrage", material, "Enemies", -22, 3072);
        sweepGuide = new Surface(ownedRoot.transform, "CrescentRoute", material, "Enemies", -24);
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
        int fragments = boss.Config != null ? boss.Config.effectFragmentCount : 16;
        for (int i = 0; i < bursts.Length; i++)
        {
            if (ages[i] < 0) continue;
            float lifetime = kinds[i] == 3 ? boss.Config.phaseTransitionTime : boss.Config.effectLifetime;
            ages[i] += Time.deltaTime;
            if (ages[i] >= lifetime) { ages[i] = -1; bursts[i].Hide(); continue; }
            ActiveCount++;
            float t = ages[i] / lifetime;
            Surface surface = bursts[i]; surface.Clear();
            if (kinds[i] == 3)
            {
                DrawAscension(surface, origins[i], radii[i], t);
                surface.Apply(); continue;
            }
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
    private static void DrawAscension(Surface surface, Vector2 origin, float radius, float t)
    {
        // Thin violet arcs and inward motes are presentation only, never a damage ring.
        float reveal = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.36f, .68f, t));
        float alpha = Mathf.SmoothStep(0, 1, t / .12f) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.76f, 1, t)));
        Color halo = new Color(.77f, .60f, 1, alpha * .55f);
        float r = Mathf.Lerp(radius * .4f, radius, reveal);
        for (int i = 0; i < 64; i++)
        {
            if (i % 8 == 7) continue;
            float a = i * Mathf.PI * 2 / 64 + t * .5f, b = (i + 1) * Mathf.PI * 2 / 64 + t * .5f;
            surface.Line(origin + Unit(a) * r, origin + Unit(b) * r, .025f + .025f * reveal, halo);
        }
        for (int i = 0; i < 18; i++)
        {
            float a = i * 2.39996f + t * .8f;
            float inward = Mathf.Lerp(radius * .8f, .45f, Mathf.Clamp01(t / .42f));
            float distance = t < .42f ? inward : Mathf.Lerp(.45f, radius * 1.15f, reveal);
            Vector2 point = origin + Unit(a) * distance;
            point.y += Mathf.Sin(t * Mathf.PI) * .7f;
            surface.Diamond(point, .035f + .025f * (i % 3), new Color(1, .71f, .36f, alpha * .8f));
        }
    }
    public void DrawWave(HiveTyrantWavePlan plan, float distance, bool warning)
    {
        for (int s = 0; s < wave.Length; s++)
        {
            Surface surface = wave[s]; surface.Clear();
            if (s >= plan.SegmentCount) { surface.Hide(); continue; }
            float length = plan.lengths[s];
            if (length <= 0) { surface.Hide(); continue; }
            float x = plan.SegmentCenter(s), half = plan.width * .5f;
            if (warning)
            {
                Color edge = plan.enraged ? new Color(.94f, .58f, 1, .8f) : new Color(.98f, .82f, .51f, .7f);
                Rect(surface, plan, x - half, x + half, 0, length, plan.enraged ? new Color(.6f, .22f, .8f, .14f) : new Color(.8f, .48f, .21f, .13f));
                Rect(surface, plan, x - half, x - half + .055f, 0, length, edge);
                Rect(surface, plan, x + half - .055f, x + half, 0, length, edge);
                for (float z = .6f; z < length; z += 1.6f)
                {
                    Vector2 a = plan.Point(x - .35f, z - .28f), b = plan.Point(x, z), c = plan.Point(x + .35f, z - .28f);
                    surface.Line(a, b, .07f, edge); surface.Line(b, c, .07f, edge);
                    // Three notches advertise the cascade before any front is released.
                    if (plan.enraged) for (int n = 1; n < plan.volleyCount; n++)
                    {
                        Vector2 offset = plan.forward * (-.18f * n);
                        surface.Line(a + offset, b + offset, .035f, edge); surface.Line(b + offset, c + offset, .035f, edge);
                    }
                }
            }
            else
            {
                for (int volley = 0; volley < plan.volleyCount; volley++)
                {
                    float travelled = distance - volley * plan.volleyInterval * plan.speed;
                    if (travelled < 0 || travelled > length + plan.thickness * .5f) continue;
                    float front = Mathf.Min(length, travelled + plan.thickness * .5f), back = Mathf.Max(0, travelled - plan.thickness * .5f);
                    if (front <= back) continue;
                    // Every rock and dust fragment remains strictly within its dangerous segment.
                    int fragments = boss.Config != null ? Mathf.Clamp(boss.Config.effectFragmentCount, 8, 24) : 16;
                    for (int j = 0; j < fragments; j++)
                    {
                        float left = x - half + j * plan.width / fragments, right = left + plan.width / fragments;
                        float crest = front - .09f * (j % 3);
                        float heel = Mathf.Lerp(back, front, .16f + .14f * (j % 3));
                        Vector2 a = plan.Point(left + .025f, heel), b = plan.Point(right - .025f, heel + .04f), c = plan.Point(right, crest), d = plan.Point(left, front);
                        surface.Quad(a, b, c, d, new Color(.38f + .07f * (j % 2), .34f, .29f, .88f));
                        surface.Line(d, c, .085f, plan.enraged ? new Color(.97f, .64f, 1, 1) : new Color(1, .92f, .73f, .95f));
                        surface.Diamond(plan.Point((left + right) * .5f, Mathf.Lerp(back, front, .3f + .12f * (j % 3))), .075f, new Color(.88f, .8f, .66f, .8f));
                        if (j % 3 == 0) surface.Diamond(plan.Point((left + right) * .5f, back + .08f), .11f, new Color(.74f, .7f, .62f, .2f));
                    }
                }
            }
            surface.Apply();
        }
    }
    private static void Rect(Surface mesh, HiveTyrantWavePlan plan, float l, float r, float b, float f, Color color)
    { mesh.Quad(plan.Point(l, b), plan.Point(r, b), plan.Point(r, f), plan.Point(l, f), color); }
    public void HideWave() { foreach (var surface in wave) if (surface != null) surface.Hide(); }
    public void DrawSweepGuide(Vector2 origin, Vector2 forward)
    {
        sweepGuide.Clear();
        Vector2 right = new Vector2(-forward.y, forward.x);
        for (float distance = boss.Config.sweepRadius + 1; distance < boss.Config.sweepRadius + boss.Config.sweepTravelDistance; distance += 2)
        {
            Vector2 p = origin + forward * distance;
            if (!boss.InsideArena(p) || Physics2D.Linecast(origin, p, boss.Config.environmentLayers).collider != null) break;
            Color c = new Color(1, .55f, .14f, .58f);
            sweepGuide.Line(p - forward * .35f - right * .35f, p, .045f, c);
            sweepGuide.Line(p - forward * .35f + right * .35f, p, .045f, c);
        }
        sweepGuide.Apply();
    }
    public void HideSweepGuide() { sweepGuide?.Hide(); }
    public void DrawCrescents(HiveTyrantPressureAttacks attacks)
    {
        for (int i = 0; i < crescents.Length; i++)
        {
            var surface = crescents[i]; var arc = attacks.Crescents[i];
            if (!arc.active) { surface.Hide(); continue; }
            surface.Clear();
            for (int s = 0; s < HiveTyrantPressureAttacks.ArcSegments; s++)
            {
                if (arc.stops[s] < arc.distance) continue;
                Vector2 a = arc.a[s] + arc.forward * arc.distance, b = arc.b[s] + arc.forward * arc.distance;
                surface.Line(a, b, boss.Config.sweepThickness, arc.enraged ? new Color(.82f, .24f, 1, .75f) : new Color(1, .38f, .055f, .7f));
                surface.Line(a, b, .12f, new Color(1, .94f, .63f, 1));
                if (s % 3 == 0) surface.Diamond((a + b) * .5f - arc.forward * .25f, .075f, new Color(1, .65f, .18f, .9f));
            }
            surface.Apply();
        }
    }
    public void DrawSpores(HiveTyrantPressureAttacks attacks)
    {
        spores.Clear();
        foreach (var spore in attacks.Spores)
        {
            if (!spore.active) continue;
            float radius = boss.Config.sporeRadius;
            float progress = Mathf.Clamp01(spore.age / spore.warning);
            float fade = spore.impacted ? 1 - Mathf.Clamp01((spore.age - spore.warning) / spore.residue) : 1;
            Color edge = spore.impacted ? new Color(.87f, .22f, 1, .85f * fade) : new Color(.95f, .66f, 1, .95f);
            for (int i = 0; i < 40; i++)
            {
                Vector2 a = Unit(i * Mathf.PI * 2 / 40), b = Unit((i + 1) * Mathf.PI * 2 / 40);
                spores.Quad(spore.position, spore.position + a * radius, spore.position + b * radius, spore.position,
                    new Color(.49f, .12f, .63f, (spore.impacted ? .3f * fade : .12f)));
                spores.Line(spore.position + a * radius, spore.position + b * radius, spore.impacted ? .06f : .035f, edge);
                if (!spore.impacted && i / 40f <= progress)
                    spores.Line(spore.position + a * radius * .83f, spore.position + b * radius * .83f, .07f, edge);
            }
            // A descending diamond and filling clock ring make the release time explicit.
            float height = spore.impacted ? .08f : Mathf.Lerp(1.6f, .08f, progress);
            spores.Diamond(spore.position + Vector2.up * height, spore.impacted ? .14f : .18f, edge);
            if (!spore.impacted) spores.Line(spore.position + Vector2.left * .24f, spore.position + Vector2.right * .24f, .035f, edge);
        }
        spores.Apply();
    }
    public void HidePressure()
    { foreach (var surface in crescents) surface?.Hide(); spores?.Hide(); sweepGuide?.Hide(); }
    public void ClearAll()
    {
        for (int i = 0; i < bursts.Length; i++) { ages[i] = -1; if (bursts[i] != null) bursts[i].Hide(); }
        foreach (var warning in warnings) if (warning != null) warning.Release();
        HideWave(); HidePressure(); ActiveCount = 0;
    }
    private void OnDisable() { ClearAll(); }
    private void OnDestroy()
    {
        foreach (var surface in bursts) surface?.Dispose(); foreach (var surface in wave) surface?.Dispose();
        foreach (var surface in crescents) surface?.Dispose(); spores?.Dispose(); sweepGuide?.Dispose();
        if (ownedRoot != null) Destroy(ownedRoot); if (material != null) Destroy(material);
    }
    private static Vector2 Unit(float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    public sealed class Surface
    {
        public readonly GameObject go;
        private readonly Mesh mesh;
        private readonly List<Vector3> vertices;
        private readonly List<int> triangles;
        private readonly List<Color> colors;
        public Surface(Transform root, string name, Material material, string layer, int order, int capacity = 1024)
        {
            vertices = new List<Vector3>(capacity); triangles = new List<int>(capacity * 3 / 2); colors = new List<Color>(capacity);
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
