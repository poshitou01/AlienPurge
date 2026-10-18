using UnityEngine;

public sealed partial class CombatImpactFeedback
{
    sealed class Burst
    {
        public readonly LineRenderer[] rays = new LineRenderer[8];
        public Vector2 point, direction;
        public float start = -100, duration, size;
        public Color color;
        public bool kill;
        public CombatImpactTier tier;
        public int frame = -1;
    }
    readonly Burst[] bursts = new Burst[24];
    public int ActiveBurstCount
    {
        get { int count = 0; foreach (var burst in bursts) if (burst != null && Time.time < burst.start + burst.duration) count++; return count; }
    }
    internal Sprite BarSprite { get; private set; }
    Texture2D barTexture;
    void CreateVisuals()
    {
        barTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "Combat health bar pixel" };
        barTexture.SetPixel(0, 0, Color.white); barTexture.Apply();
        BarSprite = Sprite.Create(barTexture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1);
        for (int i = 0; i < bursts.Length; i++)
        {
            var burst = bursts[i] = new Burst();
            for (int j = 0; j < burst.rays.Length; j++)
            {
                var line = new GameObject("Impact_" + i + "_Ray_" + j).AddComponent<LineRenderer>();
                line.transform.SetParent(transform, false); line.sharedMaterial = effectMaterial;
                line.useWorldSpace = true; line.positionCount = 2; line.sortingLayerName = "VFX"; line.sortingOrder = 8;
                line.numCapVertices = 0; line.enabled = false; burst.rays[j] = line;
            }
        }
    }
    void EmitBurst(Vector2 point, Vector2 direction, CombatImpactTier tier, PlayerImpactSource source)
    {
        Burst burst = null;
        foreach (var candidate in bursts)
        {
            // Co-located chain/explosion contacts in one frame should not stack into a bright blob.
            if (candidate.frame == Time.frameCount && candidate.tier >= tier && (candidate.point - point).sqrMagnitude < .025f) return;
        }
        foreach (var candidate in bursts)
        {
            if (Time.time >= candidate.start + candidate.duration) { burst = candidate; break; }
            if (burst == null || candidate.tier < burst.tier || candidate.tier == burst.tier && candidate.start < burst.start) burst = candidate;
        }
        // Preserve an active kill burst when a large chain of ordinary hits fills the pool.
        if (burst == null || Time.time < burst.start + burst.duration && burst.tier > tier) return;
        burst.tier = tier; burst.frame = Time.frameCount;
        burst.point = point; burst.direction = direction; burst.start = Time.time;
        burst.kill = tier >= CombatImpactTier.Kill;
        burst.duration = burst.kill ? .26f : .14f;
        burst.size = tier == CombatImpactTier.HeavyKill ? .65f : burst.kill ? .4f : tier == CombatImpactTier.Strong ? .27f : .18f;
        burst.color = burst.kill ? new Color(1, .78f, .4f) : source == PlayerImpactSource.Chain ? new Color(.58f, .87f, 1) : new Color(.91f, .98f, 1);
        DrawBurst(burst);
    }
    void DrawBurst(Burst burst)
    {
        float t = Mathf.Clamp01((Time.time - burst.start) / burst.duration);
        float angle = Mathf.Atan2(burst.direction.y, burst.direction.x);
        for (int j = 0; j < burst.rays.Length; j++)
        {
            var line = burst.rays[j]; line.enabled = t < 1 && (burst.kill || j < 4);
            if (!line.enabled) continue;
            float a = angle + (j * (burst.kill ? Mathf.PI / 4 : Mathf.PI / 2)) + Mathf.PI / 4;
            Vector2 ray = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            float spread = burst.size * (.28f + t * 1.1f);
            Vector2 origin = burst.point + burst.direction * t * burst.size * .3f;
            line.SetPosition(0, origin + ray * spread);
            line.SetPosition(1, origin + ray * (spread + burst.size * (1 - t) * .55f));
            line.widthMultiplier = (burst.kill ? .045f : .028f) * (1 - t * .75f);
            Color color = burst.color; color.a = (1 - t) * .95f;
            line.startColor = color; line.endColor = color;
        }
    }
    void UpdateVisuals() { foreach (var burst in bursts) if (burst != null) DrawBurst(burst); }
    void ClearVisuals()
    {
        foreach (var burst in bursts) if (burst != null)
        { burst.start = -100; burst.frame = -1; foreach (var ray in burst.rays) if (ray != null) ray.enabled = false; }
    }
    void DestroyVisuals()
    {
        if (BarSprite != null) Destroy(BarSprite);
        if (barTexture != null) Destroy(barTexture);
    }
}
