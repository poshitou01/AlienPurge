using System.Collections.Generic;
using UnityEngine;

// Encounter-owned simulation; fixed storage, no projectile GameObjects or per-frame searches.
// Graphics and collision consume the same locked geometry. Camera never reads these bounds.
public sealed class HiveTyrantPressureAttacks
{
    public const int ArcSegments = 28;
    public sealed class Crescent
    {
        public bool active;
        public Vector2 origin, forward;
        public float distance;
        public readonly Vector2[] a = new Vector2[ArcSegments], b = new Vector2[ArcSegments];
        public readonly float[] stops = new float[ArcSegments];
        public readonly HashSet<PlayerHealth> hit = new HashSet<PlayerHealth>();
    }
    public sealed class Spore
    {
        public bool active, impacted;
        public Vector2 position;
        public float age;
        public readonly HashSet<PlayerHealth> hit = new HashSet<PlayerHealth>();
    }
    private sealed class Target
    {
        public PlayerHealth health;
        public PlayerAbilityState ability;
        public Collider2D[] bodies;
        public Vector2 previous;
    }
    private readonly HiveTyrantCombat boss;
    private readonly HiveTyrantEffects effects;
    private Target[] targets;
    public readonly Crescent[] Crescents = { new Crescent(), new Crescent() };
    public readonly Spore[] Spores = { new Spore(), new Spore(), new Spore(), new Spore(), new Spore(), new Spore() };
    public int SweepHits { get; private set; }
    public int SporeHits { get; private set; }
    public int JumpAvoidances { get; private set; }
    public bool HasCrescent { get { foreach (var arc in Crescents) if (arc.active) return true; return false; } }
    public bool HasSpores { get { foreach (var spore in Spores) if (spore.active) return true; return false; } }
    public bool Active => HasCrescent || HasSpores;
    public int WarningCount { get { int count = 0; foreach (var spore in Spores) if (spore.active && !spore.impacted) count++; return count; } }
    public HiveTyrantPressureAttacks(HiveTyrantCombat owner, HiveTyrantEffects visuals) { boss = owner; effects = visuals; }
    private void CacheTargets()
    {
        if (targets != null) return;
        var players = Object.FindObjectsOfType<PlayerHealth>(); targets = new Target[players.Length];
        for (int i = 0; i < players.Length; i++) targets[i] = new Target { health = players[i], ability = players[i].GetComponent<PlayerAbilityState>(),
            bodies = players[i].GetComponentsInChildren<Collider2D>(), previous = players[i].transform.position };
    }
    private static bool Airborne(Target target) => target.ability != null && target.ability.IsAirborne;
    public void LaunchCrescent(Vector2 origin, Vector2 forward)
    {
        CacheTargets(); var config = boss.Config;
        Crescent arc = null;
        foreach (var candidate in Crescents) if (!candidate.active) { arc = candidate; break; }
        if (arc == null) return;
        arc.active = true; arc.origin = origin; arc.forward = forward.normalized; arc.distance = 0; arc.hit.Clear();
        float heading = Mathf.Atan2(forward.y, forward.x);
        for (int i = 0; i < ArcSegments; i++)
        {
            float a = heading + Mathf.Lerp(-config.sweepAngle * .5f, config.sweepAngle * .5f, i / (float)ArcSegments) * Mathf.Deg2Rad;
            float b = heading + Mathf.Lerp(-config.sweepAngle * .5f, config.sweepAngle * .5f, (i + 1) / (float)ArcSegments) * Mathf.Deg2Rad;
            arc.a[i] = origin + Unit(a) * config.sweepRadius; arc.b[i] = origin + Unit(b) * config.sweepRadius;
            // Three probes keep both chord ends and the actual strip out of walls/bounds.
            arc.stops[i] = Mathf.Min(TravelLimit(origin, arc.a[i], arc.forward),
                Mathf.Min(TravelLimit(origin, arc.b[i], arc.forward), TravelLimit(origin, (arc.a[i] + arc.b[i]) * .5f, arc.forward)));
        }
        foreach (var target in targets)
        {
            if (target.health == null || target.health.IsDead) continue;
            target.previous = target.health.transform.position;
            // Preserve the original 7m / 120 degree close sweep. The same cast can hit only once.
            if (!HiveTyrantCombat.ContainsPoint(origin, forward, target.previous, config.sweepRadius, config.sweepAngle)
                || Physics2D.Linecast(origin, target.previous, config.environmentLayers).collider != null) continue;
            if (Airborne(target)) { JumpAvoidances++; continue; }
            Hit(target, arc.hit, true);
        }
        effects.DrawCrescents(this);
    }
    private float TravelLimit(Vector2 origin, Vector2 start, Vector2 forward)
    {
        var config = boss.Config;
        if (!boss.InsideArena(start) || Physics2D.Linecast(origin, start, config.environmentLayers).collider != null) return -1;
        float limit = config.sweepTravelDistance;
        var hit = Physics2D.CircleCast(start, config.sweepThickness * .5f, forward, limit, config.environmentLayers);
        if (hit.collider != null) limit = Mathf.Max(0, hit.distance - .04f);
        // Sampling catches concave boundary exits; the front disappears at its first exit.
        for (float d = .25f; d <= limit; d += .25f) if (!boss.InsideArena(start + forward * d)) { limit = Mathf.Max(0, d - .25f); break; }
        return limit;
    }
    public bool MarkSpore(Vector2 position)
    {
        CacheTargets();
        if (!boss.InsideArena(position) || Physics2D.OverlapCircle(position, .15f, boss.Config.environmentLayers) != null) return false;
        foreach (var spore in Spores) if (!spore.active)
        {
            spore.active = true; spore.impacted = false; spore.position = position; spore.age = 0; spore.hit.Clear();
            effects.DrawSpores(this); return true;
        }
        return false;
    }
    public void Advance(float dt)
    {
        if (!Active || dt <= 0) return;
        if (boss.Health.IsDead || boss.Motor.Target == null || !boss.TargetInArena ||
            (GameManager.Instance != null && !GameManager.Instance.IsPlaying)) { Cancel(); return; }
        var config = boss.Config;
        foreach (var arc in Crescents)
        {
            if (!arc.active) continue;
            float from = arc.distance, to = Mathf.Min(config.sweepTravelDistance + config.sweepThickness, from + dt * config.sweepTravelSpeed);
            foreach (var target in targets)
            {
                if (target.health == null || target.health.IsDead || arc.hit.Contains(target.health)) continue;
                Vector2 current = target.health.transform.position, oldOffset = target.previous - current;
                bool touched = false;
                for (int s = 0; s < ArcSegments && !touched; s++)
                {
                    if (arc.stops[s] < from) continue;
                    float stop = Mathf.Min(to, arc.stops[s]);
                    Vector2 tangent = (arc.b[s] - arc.a[s]).normalized, normal = new Vector2(-tangent.y, tangent.x);
                    Vector2 center = (arc.a[s] + arc.b[s]) * .5f;
                    float fraction = to > from ? (stop - from) / (to - from) : 1;
                    foreach (var body in target.bodies)
                    {
                        if (body == null || !body.enabled || body.isTrigger) continue;
                        HiveTyrantGeometry.Project(body, tangent, out float lo, out float hi);
                        HiveTyrantGeometry.Project(body, normal, out float bottom, out float top);
                        Vector2 now = new Vector2((lo + hi) * .5f - Vector2.Dot(center, tangent), (bottom + top) * .5f - Vector2.Dot(center, normal));
                        Vector2 before = now + Project(oldOffset, tangent, normal);
                        Vector2 half = new Vector2((hi - lo + Vector2.Distance(arc.a[s], arc.b[s])) * .5f, (top - bottom + config.sweepThickness) * .5f);
                        if (!HiveTyrantGeometry.SegmentBox(before - Project(arc.forward * from, tangent, normal),
                            Vector2.Lerp(before, now, fraction) - Project(arc.forward * stop, tangent, normal), -half, half, out _)) continue;
                        if (Physics2D.Linecast(center, body.bounds.center, config.environmentLayers).collider != null) continue;
                        touched = true; break;
                    }
                }
                if (touched) { if (Airborne(target)) JumpAvoidances++; else Hit(target, arc.hit, true); }
            }
            arc.distance = to;
            bool any = false; foreach (float stop in arc.stops) if (stop >= to) { any = true; break; }
            if (!any) arc.active = false;
        }
        foreach (var spore in Spores)
        {
            if (!spore.active) continue;
            spore.age += dt;
            if (spore.age < config.sporeWarning) continue;
            if (!spore.impacted) { spore.impacted = true; effects.Burst(2, spore.position, Vector2.up, config.sporeRadius); }
            foreach (var target in targets)
            {
                if (target.health == null || target.health.IsDead || spore.hit.Contains(target.health) || Airborne(target)) continue;
                foreach (var body in target.bodies)
                {
                    if (body == null || !body.enabled || body.isTrigger) continue;
                    if (Vector2.Distance(body.ClosestPoint(spore.position), spore.position) > config.sporeRadius) continue;
                    if (Physics2D.Linecast(spore.position, body.bounds.center, config.environmentLayers).collider != null) continue;
                    Hit(target, spore.hit, false); break;
                }
            }
            if (spore.age >= config.sporeWarning + config.sporeResidueTime) spore.active = false;
        }
        foreach (var target in targets) if (target.health != null) target.previous = target.health.transform.position;
        effects.DrawCrescents(this); effects.DrawSpores(this);
    }
    private void Hit(Target target, HashSet<PlayerHealth> hits, bool sweep)
    {
        if (!hits.Add(target.health)) return;
        int before = target.health.CurrentHealth;
        target.health.TakeDamage(sweep ? boss.Config.sweepDamage : boss.Config.sporeDamage);
        if (target.health.CurrentHealth < before) { if (sweep) SweepHits++; else SporeHits++; }
    }
    public bool SporeDanger(Vector2 point, float extraRadius = 0)
    {
        foreach (var spore in Spores) if (spore.active && Vector2.Distance(point, spore.position) < boss.Config.sporeRadius + extraRadius) return true;
        return false;
    }
    public void Cancel()
    {
        foreach (var arc in Crescents) { arc.active = false; arc.hit.Clear(); }
        foreach (var spore in Spores) { spore.active = false; spore.hit.Clear(); }
        if (effects != null) effects.HidePressure();
    }
    public void Reset() { Cancel(); targets = null; SweepHits = SporeHits = JumpAvoidances = 0; }
    private static Vector2 Unit(float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    private static Vector2 Project(Vector2 p, Vector2 x, Vector2 y) => new Vector2(Vector2.Dot(p, x), Vector2.Dot(p, y));
}
