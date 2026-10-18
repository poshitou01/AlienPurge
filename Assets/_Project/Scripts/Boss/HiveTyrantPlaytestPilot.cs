#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Reflection;
using UnityEngine;

// Test-only input source. Never attached to a saved scene/prefab. Native PlayerMovement
// still performs movement at its unmodified speed with its normal physics and locks.
[DefaultExecutionOrder(-100), AddComponentMenu("")]
public sealed class HiveTyrantPlaytestPilot : MonoBehaviour
{
    public Vector2 Destination;
    public bool Battle;
    public HiveTyrantCombat Boss;
    public int Shots, Jumps;
    public bool Dodge = true;
    private PlayerJetJump jump;
    private HiveTyrantSummonOwner[] adds = new HiveTyrantSummonOwner[0];
    private float nextScan;
    private static readonly MethodInfo JumpInput = typeof(PlayerJetJump).GetMethod("TryStartJump", BindingFlags.NonPublic | BindingFlags.Instance);
    private PlayerMovement movement;
    private WeaponManager weapon;
    private PlayerHealth health;
    private static readonly FieldInfo InputField = typeof(PlayerMovement).GetField("moveInput", BindingFlags.NonPublic | BindingFlags.Instance);
    private void Awake() { movement = GetComponent<PlayerMovement>(); weapon = GetComponent<WeaponManager>(); health = GetComponent<PlayerHealth>(); jump = GetComponent<PlayerJetJump>(); Destination = transform.position; }
    private void FixedUpdate()
    {
        if (health.IsDead) return;
        if (Battle && Boss != null && !Boss.Health.IsDead && Dodge)
        {
            TryJump(); ChooseDestination(); AvoidLocalHazards();
        }
        Vector2 delta = Destination - (Vector2)transform.position;
        InputField.SetValue(movement, delta.magnitude > .09f ? delta.normalized * Mathf.Min(1, delta.magnitude / (movement.MoveSpeed * Time.fixedDeltaTime)) : Vector2.zero);
    }
    private void LateUpdate()
    {
        if (!Battle || Boss == null || Boss.Health.IsDead || health.IsDead) return;
        Vector2 target = Boss.Motor.CombatOrigin;
        float nearest = 8;
        if (Time.time >= nextScan) { adds = FindObjectsOfType<HiveTyrantSummonOwner>(); nextScan = Time.time + .2f; }
        foreach (var lease in adds)
        {
            if (lease == null || !lease.isActiveAndEnabled) continue;
            var enemy = lease.GetComponent<EnemyHealth>();
            if (lease.Owner != Boss || enemy.IsDead) continue;
            float distance = Vector2.Distance(transform.position, lease.transform.position);
            if (distance < nearest) { nearest = distance; target = lease.transform.position; }
        }
        weapon.AimAt(target); if (weapon.TryFire()) Shots++;
    }
    private void TryJump()
    {
        if (jump == null || !jump.IsReady) return;
        Vector2 current = transform.position;
        bool danger = Boss.State == HiveTyrantState.SweepWindup && Boss.SweepReleaseAt - Time.time < .18f &&
            HiveTyrantCombat.ContainsPoint(Boss.SweepOrigin, Boss.SweepDirection, current, Boss.Config.sweepRadius + .3f, Boss.Config.sweepAngle);
        foreach (var arc in Boss.Pressure.Crescents)
        {
            if (!arc.active) continue;
            Vector2 relative = current - arc.forward * (arc.distance + arc.speed * .16f);
            for (int i = 0; i < HiveTyrantPressureAttacks.ArcSegments; i++)
            {
                if (arc.stops[i] < arc.distance) continue;
                Vector2 edge = arc.b[i] - arc.a[i];
                Vector2 nearest = arc.a[i] + edge * Mathf.Clamp01(Vector2.Dot(relative - arc.a[i], edge) / edge.sqrMagnitude);
                if (Vector2.Distance(relative, nearest) < .85f) { danger = true; break; }
            }
        }
        if (danger) { JumpInput.Invoke(jump, null); if (jump.IsActive) Jumps++; }
    }
    private void AvoidLocalHazards()
    {
        Vector2 current = transform.position, desired = (Destination - current).normalized;
        float best = float.NegativeInfinity; Vector2 chosen = desired;
        for (int i = 0; i < 20; i++)
        {
            Vector2 direction = new Vector2(Mathf.Cos(i * Mathf.PI * .1f), Mathf.Sin(i * Mathf.PI * .1f));
            Vector2 point = current + direction * 1.25f;
            if (!Boss.InsideArena(point) || Physics2D.CircleCast(current, .35f, direction, 1.4f, Boss.Config.environmentLayers).collider != null) continue;
            float score = Vector2.Dot(direction, desired) * 2;
            if (Boss.Pressure.SporeDanger(point, .55f)) score -= 15;
            if (Boss.Pressure.SporeDanger(current + direction * 2.5f, .35f)) score -= 4;
            foreach (var add in adds)
            {
                if (add == null || !add.isActiveAndEnabled) continue;
                float d = Vector2.Distance(point, add.transform.position);
                score -= Mathf.Max(0, 2 - d) * 5;
            }
            if (Vector2.Distance(point, Boss.Motor.CombatOrigin) < 3.5f) score -= 15;
            if (score > best) { best = score; chosen = direction; }
        }
        Destination = current + chosen * 2;
    }
    private void ChooseDestination()
    {
        Vector2 current = transform.position, center = Boss.Motor.CombatOrigin;
        var wave = Boss.Wave;
        if (wave.Plan != null && (wave.IsWarning || wave.IsTravelling))
        {
            var plan = wave.Plan;
            float z = Vector2.Dot(current - plan.origin, plan.forward);
            float x = Vector2.Dot(current - plan.origin, plan.right);
            int gap = -1; float nearest = float.PositiveInfinity;
            for (int i = 0; i < plan.GapCount; i++)
                if (plan.reachableGaps[i] && Mathf.Abs(x - plan.GapCenter(i)) < nearest)
                { gap = i; nearest = Mathf.Abs(x - plan.GapCenter(i)); }
            if (gap >= 0) { Destination = plan.Point(plan.GapCenter(gap), Mathf.Clamp(z, 2, 10)); return; }
        }
        Vector2 radial = current - center;
        if (radial.sqrMagnitude < .01f) radial = Vector2.down;
        float radius = 8.2f;
        float angle = Mathf.Atan2(radial.y, radial.x) + .08f;
        Vector2 destination = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        if (!Boss.InsideArena(destination)) destination = center + Vector2.down * radius;
        Destination = destination;
    }
}
#endif
