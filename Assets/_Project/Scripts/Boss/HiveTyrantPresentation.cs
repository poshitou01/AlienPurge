using UnityEngine;

// Eight authored camera views, each with local rigid forearm joints and soft shoulder weights.
// There is one textured surface: moving an arm removes its old pixels, avoiding double limbs.
[DefaultExecutionOrder(500), DisallowMultipleComponent]
public sealed class HiveTyrantPresentation : MonoBehaviour
{
    [SerializeField] private HiveTyrantPoseLibrary library;
    [SerializeField] private SpriteRenderer body;
    [SerializeField] private Material materialTemplate;
    private HiveTyrantCombat boss;
    private Mesh mesh;
    private MeshRenderer view;
    private Material material;
    private GameObject surface;
    // Four corners per rigid cell. The previous six repeated two identical deformations.
    private const int Resolution = 96, Count = Resolution * Resolution * 4;
    private readonly Vector3[] rest = new Vector3[Count], vertices = new Vector3[Count];
    private readonly Color[] colors = new Color[Count];
    private readonly Vector2[] pixels = new Vector2[Count];
    private readonly float[][] scytheWeights = new float[16][], fistWeights = new float[16][];
    private readonly float[][] torsoWeights = new float[16][], wingWeights = new float[16][], coreWeights = new float[16][];
    private float started, windup, release, recovery;
    private int action = -1, profile;
    public bool IsArticulated => action >= 0 && view != null && view.enabled;
    public int CurrentAction => action;
    public int CurrentProfile => profile;
    public float MaximumDisplacement { get; private set; }
    public Bounds VisualBounds => IsArticulated ? view.bounds : body != null ? body.bounds : new Bounds(transform.position, Vector3.one);
    // Use the fixed source canvas, not an animated mesh/trimmed sprite, for camera composition.
    public Bounds FramingBounds
    {
        get
        {
            if (body == null || body.sprite == null) return new Bounds(transform.position, Vector3.one * 12);
            var sprite = body.sprite; Vector2 size = sprite.rect.size / sprite.pixelsPerUnit;
            Vector2 center = (sprite.rect.size * .5f - sprite.pivot) / sprite.pixelsPerUnit;
            Vector3 x = body.transform.TransformVector(new Vector3(size.x, 0, 0));
            Vector3 y = body.transform.TransformVector(new Vector3(0, size.y, 0));
            return new Bounds(body.transform.TransformPoint(center), new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x), Mathf.Abs(x.y) + Mathf.Abs(y.y), .1f));
        }
    }
    private void Awake()
    {
        boss = GetComponent<HiveTyrantCombat>();
        if (body == null) body = GetComponentInChildren<Animator>().GetComponent<SpriteRenderer>();
        if (library == null || library.directions.Length != 16) { Debug.LogError("HiveTyrant missing authored directional pose library", this); enabled = false; return; }
        surface = new GameObject("HiveTyrant_ArticulatedBody"); surface.transform.SetParent(body.transform, false);
        mesh = new Mesh { name = "HiveTyrant local joint surface" }; mesh.MarkDynamic();
        surface.AddComponent<MeshFilter>().sharedMesh = mesh; view = surface.AddComponent<MeshRenderer>();
        material = materialTemplate != null ? new Material(materialTemplate) : new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")); material.name = "HiveTyrant_ArticulatedBody";
        view.sharedMaterial = material; view.sortingLayerID = body.sortingLayerID; view.sortingOrder = body.sortingOrder;
        var uv = new Vector2[Count]; var triangles = new int[Resolution * Resolution * 6];
        int vertex = 0;
        for (int y = 0; y < Resolution; y++) for (int x = 0; x < Resolution; x++)
        {
            int cell = y * Resolution + x, first = vertex;
            int ti = cell * 6;
            triangles[ti] = first; triangles[ti + 1] = first + 1; triangles[ti + 2] = first + 2;
            triangles[ti + 3] = first + 2; triangles[ti + 4] = first + 1; triangles[ti + 5] = first + 3;
            for (int k = 0; k < 4; k++)
            {
                int vx = x + (k >= 2 ? 1 : 0), vy = y + (k % 2);
                pixels[vertex] = new Vector2(vx * 1024f / Resolution, vy * 1024f / Resolution);
                rest[vertex] = PixelToLocal(pixels[vertex]); uv[vertex] = new Vector2(vx / (float)Resolution, 1 - vy / (float)Resolution);
                colors[vertex] = Color.white; vertex++;
            }
        }
        mesh.vertices = rest; mesh.uv = uv; mesh.triangles = triangles; mesh.colors = colors;
        for (int p = 0; p < 16; p++)
        {
            var rig = library.directions[p]; scytheWeights[p] = new float[Count]; fistWeights[p] = new float[Count];
            torsoWeights[p] = new float[Count]; wingWeights[p] = new float[Count]; coreWeights[p] = new float[Count];
            if (rig.skinCells == null || rig.skinCells.Length != Resolution * Resolution) throw new System.InvalidOperationException("HiveTyrant source topology needs authoring bake");
            for (int i = 0; i < Count; i++)
            {
                float sw = rig.skinCells[i / 4] == 1 ? AlongJoint(pixels[i], rig.scytheJoint, rig.scytheTip) : 0;
                float fw = rig.skinCells[i / 4] == 2 ? AlongJoint(pixels[i], rig.fistJoint, rig.fistTip) * (1 - sw) : 0;
                scytheWeights[p][i] = sw; fistWeights[p][i] = fw;
                float upper = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(rig.torsoAnchorY, rig.chestPivot.y - 70, pixels[i].y));
                torsoWeights[p][i] = Mathf.Max(upper, Mathf.Max(sw, fw) * .65f);
                wingWeights[p][i] = p < 8 ? 0 : Mathf.SmoothStep(0, 1, Mathf.InverseLerp(520, 350, pixels[i].y)) * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(55, 180, Mathf.Abs(pixels[i].x - rig.wingHinge.x)));
                coreWeights[p][i] = Mathf.Clamp01(1 - Vector2.Distance(pixels[i], new Vector2(rig.chestPivot.x, rig.chestPivot.y - 180)) / 95);
            }
        }
        // Prewarm the upload/deformation path at spawn, not at the first player-triggered attack.
        Evaluate(0);
        view.enabled = false;
    }
    private static Vector3 PixelToLocal(Vector2 p) => new Vector3((p.x - 512) / 256f, (512 - p.y) / 256f, 0);
    private static float Weight(Vector2 p, Vector2 joint, Vector2 tip, Vector2[] polygon)
    {
        if (polygon == null || polygon.Length < 3) return 0;
        bool inside = false; float edge = float.MaxValue;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            Vector2 line = polygon[i] - polygon[j];
            edge = Mathf.Min(edge, Vector2.Distance(p, polygon[j] + line * Mathf.Clamp01(Vector2.Dot(p - polygon[j], line) / Mathf.Max(1, line.sqrMagnitude))));
            if ((polygon[i].y > p.y) != (polygon[j].y > p.y) && p.x < (polygon[j].x - polygon[i].x) * (p.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x) inside = !inside;
        }
        if (!inside) return 0;
        return AlongJoint(p, joint, tip) * Mathf.SmoothStep(0, 1, edge / 6);
    }
    private static float AlongJoint(Vector2 p, Vector2 joint, Vector2 tip)
    {
        Vector2 axis = tip - joint;
        float along = Vector2.Dot(p - joint, axis) / Mathf.Max(1, axis.sqrMagnitude);
        return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.06f, .32f, along));
    }
    public void Play(int cue, float anticipation, float strike, float settle)
    {
        if (!enabled || library == null) return;
        action = cue; started = Time.time; windup = Mathf.Max(.01f, anticipation); release = Mathf.Max(.01f, strike); recovery = Mathf.Max(.01f, settle);
        // Transformation starts in the old silhouette; wings reveal at the combat cue.
        profile = ((cue == 9 ? boss.Motor.IsPhase2 : boss.Phase2Entered) ? 8 : 0) + boss.Motor.GetCurrentDirection();
        material.mainTexture = library.directions[profile].source.texture;
        // LateUpdate evaluates once before rendering; no duplicate full mesh pass in RequestSkill.
        body.enabled = false; view.enabled = true;
    }
    public void StopAction()
    {
        if (action == 3 && boss != null && boss.Health != null && boss.Health.IsDead && gameObject.activeInHierarchy) return;
        action = -1; if (body != null) body.enabled = true; if (view != null) view.enabled = false;
    }
    public void RevealPhase2()
    {
        if (action != 9 || profile >= 8) return;
        profile += 8; material.mainTexture = library.directions[profile].source.texture;
    }
    private void LateUpdate()
    {
        if (boss.Health == null) return;
        // Ordinary hits use EnemyHealth's short tint and the projectile's pooled sparks.
        // Do not replace a moving sprite with the neutral joint mesh on every bullet.
        if (action < 0) return;
        float elapsed = Time.time - started;
        float t = elapsed < windup ? elapsed / windup :
            elapsed < windup + release ? 1 + (elapsed - windup) / release * .22f :
            1.22f + (elapsed - windup - release) / recovery * .78f;
        Evaluate(Mathf.Clamp(t, 0, 2));
        if (t >= 2 && action != 3) StopAction();
    }
    private static float Ease(float t) => t * t * (3 - 2 * t);
    // Local joint key poses. 1.0 is always the combat scheduler's release moment.
    private void Evaluate(float t)
    {
        var rig = library.directions[profile];
        // Contact pose is reached at exactly t=1, when the combat scheduler releases damage/FX.
        float prepare = Ease(Mathf.Clamp01(t / .85f)), snap = Ease(Mathf.InverseLerp(.85f, 1, t)), settle = Ease(Mathf.InverseLerp(1.22f, 2, t));
        float a = 0, b = 0, chest = 0, lift = 0, fold = 0, opacity = 1;
        if (action == 6)
        {
            a = Mathf.Lerp(-30 * prepare, 32, snap) * (1 - settle);
            b = Mathf.Lerp(12 * prepare, -18, snap) * (1 - settle);
            chest = Mathf.Lerp(-5 * prepare, 5, snap) * (1 - settle); lift = -.018f * prepare * (1 - snap);
        }
        else if (action == 7)
        {
            a = Mathf.Lerp(-30 * prepare, 24, snap) * (1 - settle);
            b = Mathf.Lerp(28 * prepare, -26, snap) * (1 - settle);
            chest = Mathf.Lerp(-3 * prepare, 5, snap) * (1 - settle); lift = Mathf.Lerp(.04f * prepare, -.05f, snap) * (1 - settle);
        }
        else if (action == 8)
        {
            a = -24 * prepare * (1 - settle); b = 25 * prepare * (1 - settle);
            lift = .025f * prepare * (1 - settle); fold = -6 * prepare * (1 - settle);
        }
        else if (action == 10)
        {
            // Raise the crown and alternate the two arms as each spore is cast.
            float pulse = Mathf.Sin(t * Mathf.PI * 4) * .5f + .5f;
            a = (-20 - 5 * pulse) * prepare * (1 - settle);
            b = (20 + 5 * (1 - pulse)) * prepare * (1 - settle);
            fold = -9 * prepare * (1 - settle); lift = .025f * prepare * (1 - settle);
        }
        else if (action == 9)
        {
            a = -18 * Mathf.Sin(t * Mathf.PI * .5f); b = -a; lift = .035f * Mathf.Sin(t * Mathf.PI * .5f);
            fold = 12 * (1 - Ease(Mathf.Clamp01(t / 1.3f)));
        }
        else if (action == 3)
        {
            float fall = Ease(Mathf.Clamp01(t / 1.4f));
            a = 32 * fall; b = -25 * fall; chest = 5 * fall; lift = -.045f * fall; fold = 14 * fall;
            opacity = 1 - Ease(Mathf.InverseLerp(1.65f, 2, t));
        }
        else if (action == 2)
        { chest = -.6f * Mathf.Sin(t * Mathf.PI * .5f); a = -1.2f * Mathf.Sin(t * Mathf.PI * .5f); b = .8f * Mathf.Sin(t * Mathf.PI * .5f); }
        a *= rig.swingSign * rig.jointAmplitude; b *= rig.swingSign * rig.jointAmplitude; chest *= rig.swingSign;
        Vector3 scytheJoint = PixelToLocal(rig.scytheJoint), fistJoint = PixelToLocal(rig.fistJoint), chestPivot = PixelToLocal(rig.chestPivot);
        Quaternion scytheRotation = Quaternion.Euler(0, 0, a), fistRotation = Quaternion.Euler(0, 0, b), chestRotation = Quaternion.Euler(0, 0, chest);
        Quaternion leftWing = Quaternion.Euler(0, 0, -fold), rightWing = Quaternion.Euler(0, 0, fold);
        Vector3 hinge = PixelToLocal(rig.wingHinge);
        Color bodyColor = body.color;
        float coreGlow = (action == 8 || action == 9 || action == 10) ? .3f * Mathf.Sin(Mathf.Min(t, 1) * Mathf.PI * .5f) : 0;
        float armGlow = action == 6 ? .45f * prepare * (1 - snap) : 0;
        float maxDisplacementSquared = 0;
        for (int i = 0; i < Count; i++)
        {
            Vector3 p = rest[i];
            float sw = scytheWeights[profile][i], fw = fistWeights[profile][i];
            if (sw > 0) p += (scytheJoint + scytheRotation * (rest[i] - scytheJoint) - rest[i]) * sw;
            if (fw > 0) p += (fistJoint + fistRotation * (rest[i] - fistJoint) - rest[i]) * fw;
            // Forearms inherit torso power; supporting feet remain pinned in their original coordinates.
            float torso = torsoWeights[profile][i];
            p = Vector3.Lerp(p, chestPivot + chestRotation * (p - chestPivot) + Vector3.up * lift * 4, torso);
            if (profile >= 8 && fold != 0)
            {
                float wing = wingWeights[profile][i];
                p = Vector3.Lerp(p, hinge + (pixels[i].x < rig.wingHinge.x ? leftWing : rightWing) * (p - hinge), wing);
            }
            vertices[i] = p; maxDisplacementSquared = Mathf.Max(maxDisplacementSquared, (p - rest[i]).sqrMagnitude);
            float glow = coreGlow * coreWeights[profile][i] + armGlow * sw;
            colors[i] = new Color(1 + glow, 1 + glow * .6f, 1, opacity) * bodyColor;
        }
        MaximumDisplacement = Mathf.Sqrt(maxDisplacementSquared);
        mesh.vertices = vertices; mesh.colors = colors; mesh.RecalculateBounds();
        view.sortingLayerID = body.sortingLayerID; view.sortingOrder = body.sortingOrder;
    }
    public void SampleForReview(int cue, int direction, bool phase2, float time)
    {
        action = cue; profile = direction + (phase2 ? 8 : 0); material.mainTexture = library.directions[profile].source.texture;
        body.enabled = false; view.enabled = true; Evaluate(time);
    }
    private void OnDisable() { action = -1; if (body != null) body.enabled = true; if (view != null) view.enabled = false; }
    private void OnDestroy() { if (mesh != null) Destroy(mesh); if (material != null) Destroy(material); if (surface != null) Destroy(surface); }
}
