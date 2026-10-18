using System.Collections.Generic;
using UnityEngine;

// Range/target readability remains separate from authored body clips. The optional
// legacy organ overlay is disabled on both production prefabs.
[DefaultExecutionOrder(200), DisallowMultipleComponent]
public sealed class EnemyAbilityPresentation : MonoBehaviour
{
    [SerializeField] private Material materialTemplate;
    [SerializeField] private bool temporaryArt = true;
    [SerializeField] private Sprite[] explosionFrames;
    private SupportEnemyAbility support;
    private ExploderEnemyAbility exploder;
    private EnemyHealth health;
    private SpriteRenderer body;
    private Material material;
    private Surface organs, ground;
    private GameObject ownedGround;
    private SpriteRenderer burst;
    private SupportExploderAnimation animationDriver;
    public bool TemporaryArt => temporaryArt;
    public bool ExplosionVisible => burst != null && burst.enabled;
    public bool WarningVisible => ground != null && ground.Visible && exploder != null && exploder.State == ExploderState.Warning;
    public float DisplayedRadius { get; private set; }
    private void Awake()
    {
        support = GetComponent<SupportEnemyAbility>(); exploder = GetComponent<ExploderEnemyAbility>();
        health = GetComponent<EnemyHealth>(); body = GetComponent<SpriteRenderer>();
        animationDriver = GetComponent<SupportExploderAnimation>();
        material = new Material(materialTemplate); material.mainTexture = Texture2D.whiteTexture;
        if (temporaryArt) organs = new Surface(transform, "TEMP organ silhouettes", material, "Enemies");
        ownedGround = new GameObject("Enemy ability ground (runtime)");
        ground = new Surface(ownedGround.transform, "Range and links", material, "Pickups");
        if (exploder != null)
        {
            var effect = new GameObject("Exploder rupture (visual only)"); effect.transform.SetParent(ownedGround.transform, false);
            burst = effect.AddComponent<SpriteRenderer>(); burst.sharedMaterial = materialTemplate;
            burst.sortingLayerName = "Effects"; burst.sortingOrder = 0; burst.enabled = false;
        }
    }
    private void LateUpdate()
    {
        if (ground == null) return;
        organs?.Clear(); ground.Clear(); DisplayedRadius = 0;
        if (burst != null) burst.enabled = false;
        bool alive = !health.IsDead;
        float pulse = .5f + .5f * Mathf.Sin(Time.time * 6);
        if (alive && temporaryArt)
        {
            Color shell = new Color(.16f, .12f, .1f), membrane = new Color(.6f, .56f, .49f);
            Color organ = support != null ? new Color(.48f, .2f, .65f) : new Color(.9f, .23f, .065f);
            if (exploder != null && exploder.State == ExploderState.Warning)
                organ = Color.Lerp(organ, new Color(1, .8f, .28f), .5f + .5f * Mathf.Sin(Time.time * 22));
            organs.Line(new Vector2(0, .1f), new Vector2(0, .75f), .12f, membrane);
            if (support != null)
            {
                Lobe(new Vector2(0, .83f), new Vector2(.2f, .38f), organ, shell);
                Lobe(new Vector2(-.25f, .57f), new Vector2(.21f, .31f), organ, shell);
                Lobe(new Vector2(.25f, .57f), new Vector2(.21f, .31f), organ, shell);
            }
            else
            {
                Lobe(new Vector2(.08f, .48f), new Vector2(.38f, .55f) * (1 + pulse * .035f), organ, shell);
                Lobe(new Vector2(-.3f, .25f), new Vector2(.2f, .28f), organ, shell);
            }
            organs.Renderer.sortingOrder = body.sortingOrder + 1;
        }
        if (alive && support != null && support.isActiveAndEnabled && support.Config != null)
        {
            Vector2 origin = transform.position; DisplayedRadius = support.Config.supportRadius;
            ground.Ring(origin, DisplayedRadius, .02f, new Color(.65f, .35f, .95f, .18f));
            for (int i = 0; i < support.Config.maximumTargets; i++)
            {
                var target = support.TargetAt(i); if (target == null || !support.HasValidLease(target)) continue;
                Vector2 endpoint = target.transform.position;
                ground.Line(origin, endpoint, .04f, new Color(.73f, .4f, 1, .38f));
                float travel = Mathf.Repeat(Time.time * .7f + i * .23f, 1);
                ground.Ellipse(Vector2.Lerp(origin, endpoint, travel), Vector2.one * .065f, new Color(.9f, .7f, 1, .85f));
                ground.Ring(endpoint, .52f, .055f, new Color(.82f, .52f, 1, .95f));
                ground.Line(endpoint + new Vector2(-.16f, .05f), endpoint + new Vector2(0, .23f), .06f, Color.white);
                ground.Line(endpoint + new Vector2(0, .23f), endpoint + new Vector2(.16f, .05f), .06f, Color.white);
            }
        }
        if (exploder != null && (exploder.State == ExploderState.Warning || exploder.State == ExploderState.Detonated))
        {
            Vector2 center = exploder.BlastCenter; DisplayedRadius = exploder.BlastRadius;
            bool warning = exploder.State == ExploderState.Warning;
            float elapsed = animationDriver != null ? animationDriver.DeathElapsed : 0;
            Color edge = warning ? Color.Lerp(new Color(1,.46f,.1f), new Color(1,.14f,.045f), exploder.WarningProgress) : new Color(1, .9f, .55f);
            if (!warning) edge.a = .75f * (1 - Mathf.Clamp01(elapsed / .55f));
            ground.Ellipse(center, Vector2.one * DisplayedRadius, new Color(1, .16f, .025f, warning ? .13f : .07f * (1 - Mathf.Clamp01(elapsed / .4f))));
            // Ring is inset: its outside edge is exactly the same world-space circle as OverlapCircle.
            ground.Ring(center, DisplayedRadius - .045f, .09f, edge);
            if (warning)
            {
                float progress = exploder.WarningProgress;
                ground.Ellipse(center, Vector2.one * DisplayedRadius * progress, new Color(1,.3f,.04f,.06f+.09f*progress));
                ground.Ring(center, DisplayedRadius * .88f, .08f, new Color(1,.86f,.3f), progress);
                ground.Ring(center, Mathf.Max(.12f,DisplayedRadius*progress-.045f), .07f, new Color(1,.65f,.18f,.8f));
                ground.Line(center + Vector2.up * .25f, center + Vector2.up * .8f, .11f, edge);
                ground.Ellipse(center + Vector2.up * .08f, Vector2.one * .065f, edge);
            }
            else if (burst != null && explosionFrames != null && explosionFrames.Length == 8 && elapsed < .7f)
            {
                burst.sprite = explosionFrames[Mathf.Min(7, Mathf.FloorToInt(elapsed * 12))];
                burst.transform.position = center;
                // 512px / 256PPU gives a 2-unit canvas; only this visual is scaled.
                burst.transform.localScale = new Vector3(DisplayedRadius, DisplayedRadius, 1);
                burst.color = new Color(1, 1, 1, .72f * (1 - Mathf.InverseLerp(.4f, .7f, elapsed)));
                burst.enabled = true;
            }
        }
        organs?.Apply(); ground.Apply();
    }
    private void Lobe(Vector2 point, Vector2 size, Color tint, Color edge)
    {
        organs.Ellipse(point, size, edge);
        organs.Ellipse(point, size * .88f, tint);
        organs.Ellipse(point + size * new Vector2(-.2f, .18f), size * .47f, Color.Lerp(tint, Color.white, .2f));
        organs.Line(point - Vector2.up * size.y * .75f, point + Vector2.up * size.y * .72f, .018f, new Color(.7f, .6f, .55f, .7f));
    }
    private void OnDisable()
    {
        organs?.Hide(); ground?.Hide(); DisplayedRadius = 0;
        if (burst != null) { burst.enabled = false; burst.sprite = null; burst.color = Color.white; burst.transform.localScale = Vector3.one; }
    }
    private void OnDestroy()
    {
        organs?.Dispose(); ground?.Dispose(); if (ownedGround != null) Destroy(ownedGround); if (material != null) Destroy(material);
    }
    internal sealed class Surface
    {
        private readonly Mesh mesh;
        private readonly List<Vector3> vertices = new List<Vector3>(4096);
        private readonly List<int> triangles = new List<int>(6144);
        private readonly List<Color> colors = new List<Color>(4096);
        public readonly MeshRenderer Renderer;
        public bool Visible => Renderer != null && Renderer.enabled;
        public Surface(Transform parent, string name, Material material, string sorting)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            mesh = new Mesh { name = "EnemyAbility_" + name }; mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh; Renderer = go.AddComponent<MeshRenderer>();
            Renderer.sharedMaterial = material; Renderer.sortingLayerName = sorting; Renderer.sortingOrder = 5;
        }
        public void Clear() { vertices.Clear(); triangles.Clear(); colors.Clear(); }
        private void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            int i = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            for (int n = 0; n < 4; n++) colors.Add(color);
            triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2); triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 3);
        }
        public void Line(Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 p = Vector2.Perpendicular((b - a).normalized) * width * .5f; Quad(a - p, b - p, b + p, a + p, color);
        }
        public void Ellipse(Vector2 center, Vector2 radius, Color color)
        {
            for (int i = 0; i < 64; i++) Quad(center, center + Vector2.Scale(Unit(i), radius), center + Vector2.Scale(Unit(i + 1), radius), center, color);
        }
        public void Ring(Vector2 center, float radius, float width, Color color, float progress = 1)
        {
            for (int i = 0; i < Mathf.CeilToInt(64 * progress); i++)
                Quad(center + Unit(i) * (radius - width * .5f), center + Unit(i + 1) * (radius - width * .5f),
                     center + Unit(i + 1) * (radius + width * .5f), center + Unit(i) * (radius + width * .5f), color);
        }
        private static Vector2 Unit(int i) { float a = i * Mathf.PI * 2 / 64; return new Vector2(Mathf.Cos(a), Mathf.Sin(a)); }
        public void Apply()
        {
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds(); Renderer.enabled = vertices.Count > 0;
        }
        public void Hide() { if (Renderer != null) Renderer.enabled = false; }
        public void Dispose() { Object.Destroy(mesh); }
    }
}
