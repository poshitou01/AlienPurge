using UnityEngine;

public sealed partial class CombatImpactFeedback
{
    sealed class DeathEcho
    {
        public SpriteRenderer body, flash;
        public Vector3 position, scale;
        public Vector2 direction;
        public Quaternion rotation;
        public float began = -100, duration, distance, turn;
    }
    readonly DeathEcho[] deathEchoes = new DeathEcho[12];
    int nextDeath;
    public int ActiveDeathEchoCount
    {
        get { int n = 0; foreach (var e in deathEchoes) if (e != null && e.body.enabled) n++; return n; }
    }
    void CreateDeathEchoes()
    {
        for (int i = 0; i < deathEchoes.Length; i++)
        {
            var e = deathEchoes[i] = new DeathEcho();
            e.body = new GameObject("DefeatedBody_" + i).AddComponent<SpriteRenderer>();
            e.body.transform.SetParent(transform, false); e.body.enabled = false;
            e.flash = new GameObject("DefeatedFlash").AddComponent<SpriteRenderer>();
            e.flash.transform.SetParent(e.body.transform, false); e.flash.sharedMaterial = flashMaterial; e.flash.enabled = false;
        }
    }
    internal void EmitDeathEcho(SpriteRenderer source, Vector2 incoming, bool heavy)
    {
        var e = deathEchoes[nextDeath++ % deathEchoes.Length];
        e.position = source.transform.position; e.rotation = source.transform.rotation; e.scale = source.transform.lossyScale;
        // Native simple death already applies a 1.2 pop; avoid magnifying it twice.
        e.scale /= 1.2f;
        e.direction = incoming; e.began = Time.time; e.duration = heavy ? .38f : .29f;
        e.distance = heavy ? .3f : .48f; e.turn = incoming.x >= 0 ? -17 : 17;
        e.body.sprite = e.flash.sprite = source.sprite;
        e.body.flipX = e.flash.flipX = source.flipX; e.body.flipY = e.flash.flipY = source.flipY;
        e.body.sharedMaterial = source.sharedMaterial;
        e.body.sortingLayerID = e.flash.sortingLayerID = source.sortingLayerID;
        e.body.sortingOrder = source.sortingOrder; e.flash.sortingOrder = source.sortingOrder + 1;
        DrawDeathEcho(e);
    }
    void DrawDeathEcho(DeathEcho e)
    {
        float age = Time.time - e.began, t = Mathf.Clamp01(age / e.duration);
        e.body.enabled = age >= 0 && t < 1; e.flash.enabled = e.body.enabled && age < .055f;
        if (!e.body.enabled) return;
        float move = Mathf.Clamp01((age - .035f) / (e.duration - .035f));
        float travel = 1 - (1 - move) * (1 - move);
        e.body.transform.position = e.position + (Vector3)(e.direction * (e.distance * travel)) + Vector3.down * (.13f * move * move);
        e.body.transform.rotation = e.rotation * Quaternion.Euler(0, 0, e.turn * travel);
        var parentScale = transform.lossyScale;
        float shrink = Mathf.Lerp(1.08f, .72f, move * move);
        e.body.transform.localScale = new Vector3(e.scale.x / parentScale.x, e.scale.y / parentScale.y, e.scale.z / parentScale.z) * shrink;
        e.body.color = new Color(1, .9f, .74f, Mathf.Clamp01((1 - t) / .58f));
        e.flash.color = new Color(1, .95f, .79f, Mathf.Clamp01(1 - age / .055f));
    }
    void UpdateDeathEchoes() { foreach (var e in deathEchoes) if (e != null) DrawDeathEcho(e); }
    void ClearDeathEchoes()
    {
        foreach (var e in deathEchoes) if (e != null)
        { e.began = -100; if (e.body != null) e.body.enabled = false; if (e.flash != null) e.flash.enabled = false; }
    }
}
