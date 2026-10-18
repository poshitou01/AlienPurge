using UnityEngine;

// Pooled with the enemy. Only the visible body reacts; colliders, navigation and attack timing do not move.
[DefaultExecutionOrder(10000), DisallowMultipleComponent]
public sealed class EnemyImpactReaction : MonoBehaviour
{
    EnemyHealth health;
    CombatImpactFeedback host;
    SpriteRenderer body, bodyEcho, flash, barBack, barTrail, barFill;
    Vector3 appliedOffset;
    Vector2 direction;
    float began = -100, barUntil, amount, trail = 1;
    bool heavy;
    bool hiddenSource;
    public bool FlashVisible => flash != null && flash.enabled;
    public float HealthFraction => health == null ? 0 : (float)health.CurrentHealth / Mathf.Max(1, health.MaxHealth);
    public Vector2 ImpactCenter => body != null ? (Vector2)body.bounds.center : (Vector2)transform.position;
    public Vector3 VisualOffset => appliedOffset;
    public bool HealthBarVisible => barFill != null && barFill.enabled;

    public void Play(CombatImpactFeedback owner, Vector2 incoming, CombatImpactTier tier, bool isHeavy)
    {
        host = owner;
        if (health == null) health = GetComponent<EnemyHealth>();
        if (body == null)
        {
            foreach (var candidate in GetComponentsInChildren<SpriteRenderer>())
                if (candidate.sprite != null && candidate.enabled && !candidate.name.ToLowerInvariant().Contains("shadow") &&
                    (body == null || candidate.bounds.size.sqrMagnitude > body.bounds.size.sqrMagnitude)) body = candidate;
            if (body == null) return;
            bodyEcho = NewSprite("ImpactBody", body.transform);
            flash = NewSprite("ImpactSilhouette", body.transform); flash.sharedMaterial = host.flashMaterial;
            flash.sortingLayerID = body.sortingLayerID; flash.sortingOrder = body.sortingOrder + 1;
            barBack = NewSprite("ImpactHealthBack", transform); barTrail = NewSprite("ImpactHealthLoss", transform); barFill = NewSprite("ImpactHealth", transform);
            foreach (var bar in new[] { barBack, barTrail, barFill })
            { bar.sprite = host.BarSprite; bar.sharedMaterial = host.effectMaterial; bar.sortingLayerName = "VFX"; }
            barBack.sortingOrder = 4; barTrail.sortingOrder = 5; barFill.sortingOrder = 6;
            trail = 1;
        }
        RemoveOffset(); direction = incoming; heavy = isHeavy;
        amount = (tier == CombatImpactTier.Hit ? .045f : .075f) * (heavy ? .45f : 1);
        began = Time.time; barUntil = Time.time + 1.4f;
        // Immediate reaction also works when damage arrives after this component's LateUpdate.
        Render();
    }
    static SpriteRenderer NewSprite(string name, Transform parent)
    {
        var view = new GameObject(name).AddComponent<SpriteRenderer>(); view.transform.SetParent(parent, false); view.enabled = false; return view;
    }
    void Update() { RemoveOffset(); }
    void LateUpdate()
    {
        if (host == null || !host.CanPresent) { Clear(); return; }
        Render();
    }
    void Render()
    {
        if (body == null || health == null) return;
        RemoveOffset();
        float age = Time.time - began;
        bool alive = !health.IsDead;
        // Several existing enemy prefabs put their sprite and physics on the same root.
        // Render a translated copy briefly, hiding only the source renderer for that frame.
        // The native animator keeps updating the original sprite and no transform is moved.
        if (alive && body.enabled && age >= 0 && age < .14f)
        {
            float envelope = (1 - age / .14f) * (1 - age / .14f);
            appliedOffset = direction * (amount * envelope);
            bodyEcho.sprite = body.sprite; bodyEcho.flipX = body.flipX; bodyEcho.flipY = body.flipY;
            bodyEcho.color = body.color; bodyEcho.sharedMaterial = body.sharedMaterial;
            bodyEcho.sortingLayerID = body.sortingLayerID; bodyEcho.sortingOrder = body.sortingOrder;
            bodyEcho.transform.localPosition = body.transform.InverseTransformVector(appliedOffset);
            bodyEcho.enabled = true; body.enabled = false; hiddenSource = true;
        }
        flash.enabled = (body.enabled || hiddenSource) && age >= 0 && age < .075f;
        if (flash.enabled)
        {
            flash.sprite = body.sprite; flash.flipX = body.flipX; flash.flipY = body.flipY;
            flash.sortingLayerID = body.sortingLayerID; flash.sortingOrder = body.sortingOrder + 1;
            flash.transform.localPosition = body.transform.InverseTransformVector(appliedOffset);
            flash.color = new Color(1, .95f, .81f, .82f * (1 - age / .075f));
        }
        bool show = alive && host.showHealthBars && Time.time < barUntil;
        barBack.enabled = barTrail.enabled = barFill.enabled = show;
        if (!show) return;
        if (age > .18f) trail = Mathf.MoveTowards(trail, HealthFraction, Time.deltaTime * 2.4f);
        else trail = Mathf.Max(trail, HealthFraction);
        float width = heavy ? .95f : .75f;
        Vector3 anchor = new Vector3(body.bounds.center.x, body.bounds.max.y + .11f, body.transform.position.z);
        SetBar(barBack, anchor, width + .05f, .10f, new Color(.04f, .07f, .07f, .9f));
        SetBar(barTrail, anchor + Vector3.left * (width * (1 - trail) * .5f), width * trail, .055f, new Color(.95f, .49f, .26f));
        SetBar(barFill, anchor + Vector3.left * (width * (1 - HealthFraction) * .5f), width * HealthFraction, .055f, new Color(.79f, .85f, .68f));
    }
    static void SetBar(SpriteRenderer view, Vector3 point, float width, float height, Color color)
    {
        view.transform.position = point; view.transform.rotation = Quaternion.identity;
        var scale = view.transform.parent.lossyScale;
        view.transform.localScale = new Vector3(width / Mathf.Max(.001f, Mathf.Abs(scale.x)), height / Mathf.Max(.001f, Mathf.Abs(scale.y)), 1);
        view.color = color;
    }
    void RemoveOffset()
    {
        if (body != null && hiddenSource) body.enabled = true;
        if (bodyEcho != null) bodyEcho.enabled = false;
        hiddenSource = false;
        appliedOffset = Vector3.zero;
    }
    public void Clear()
    {
        RemoveOffset(); began = -100; barUntil = 0; trail = 1;
        if (flash != null) flash.enabled = false;
        if (barBack != null) barBack.enabled = barTrail.enabled = barFill.enabled = false;
    }
    void OnDisable() { Clear(); }
    void OnEnable() { Clear(); }
}
