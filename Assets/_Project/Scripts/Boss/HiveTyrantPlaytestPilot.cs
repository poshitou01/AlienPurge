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
    public int Shots;
    private PlayerMovement movement;
    private WeaponManager weapon;
    private PlayerHealth health;
    private static readonly FieldInfo InputField = typeof(PlayerMovement).GetField("moveInput", BindingFlags.NonPublic | BindingFlags.Instance);
    private void Awake() { movement = GetComponent<PlayerMovement>(); weapon = GetComponent<WeaponManager>(); health = GetComponent<PlayerHealth>(); Destination = transform.position; }
    private void FixedUpdate()
    {
        if (health.IsDead) return;
        if (Battle && Boss != null && !Boss.Health.IsDead) ChooseDestination();
        Vector2 delta = Destination - (Vector2)transform.position;
        InputField.SetValue(movement, delta.magnitude > .09f ? delta.normalized * Mathf.Min(1, delta.magnitude / (movement.MoveSpeed * Time.fixedDeltaTime)) : Vector2.zero);
    }
    private void LateUpdate()
    {
        if (!Battle || Boss == null || Boss.Health.IsDead || health.IsDead) return;
        Vector2 target = Boss.Motor.CombatOrigin;
        float nearest = 8;
        foreach (var lease in FindObjectsOfType<HiveTyrantSummonOwner>())
        {
            var enemy = lease.GetComponent<EnemyHealth>();
            if (lease.Owner != Boss || enemy.IsDead) continue;
            float distance = Vector2.Distance(transform.position, lease.transform.position);
            if (distance < nearest) { nearest = distance; target = lease.transform.position; }
        }
        weapon.AimAt(target); if (weapon.TryFire()) Shots++;
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
            int gap = Mathf.Abs(x - plan.GapCenter(0)) < Mathf.Abs(x - plan.GapCenter(1)) ? 0 : 1;
            if (!plan.reachableGaps[gap]) gap = 1 - gap;
            Destination = plan.Point(plan.GapCenter(gap), Mathf.Clamp(z, 2, 10)); return;
        }
        Vector2 radial = current - center;
        if (radial.sqrMagnitude < .01f) radial = Vector2.down;
        float radius = Boss.State == HiveTyrantState.SweepWindup || Boss.State == HiveTyrantState.SweepRelease ? Boss.Config.sweepRadius + .9f : 6.2f;
        float angle = Mathf.Atan2(radial.y, radial.x) + .08f;
        Vector2 destination = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        if (!Boss.InsideArena(destination)) destination = center + Vector2.down * radius;
        Destination = destination;
    }
}
#endif
