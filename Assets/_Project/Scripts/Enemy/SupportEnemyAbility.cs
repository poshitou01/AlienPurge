using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyMovement), typeof(EnemyHealth), typeof(PooledEnemy))]
public sealed class SupportEnemyAbility : MonoBehaviour, IEnemyDeathPresentation
{
    [SerializeField] private SupportEnemyConfig config;
    private struct Lease { public EnemySupportBuff target; public uint generation; }
    private readonly List<Lease> leases = new List<Lease>(6);
    private readonly List<Collider2D> overlaps = new List<Collider2D>(64);
    private readonly List<PooledEnemy> candidates = new List<PooledEnemy>(64);
    private EnemyMovement movement;
    private EnemyHealth health;
    private PooledEnemy pooled;
    private Transform player, goal;
    private Vector2 desiredGoal;
    private float nextRefresh;
    public SupportEnemyConfig Config => config;
    public float DeathPresentationDuration => .2f;
    public int TargetCount { get { int n = 0; foreach (var lease in leases) if (HasValidLease(lease.target)) n++; return n; } }
    public EnemySupportBuff TargetAt(int index) => index < leases.Count ? leases[index].target : null;
    private bool Alive => config != null && isActiveAndEnabled && health != null && !health.IsDead &&
        pooled != null && !pooled.IsReturned && GameManager.Instance != null && GameManager.Instance.IsPlaying;

    private void Awake()
    {
        movement = GetComponent<EnemyMovement>(); health = GetComponent<EnemyHealth>(); pooled = GetComponent<PooledEnemy>();
    }
    private void OnEnable()
    {
        ReleaseAll(); nextRefresh = 0;
        GetComponent<EnemyContactDamage>().enabled = false;
        if (goal == null) goal = new GameObject("Support navigation goal (runtime)").transform;
        goal.gameObject.SetActive(true); desiredGoal = transform.position; goal.position = desiredGoal;
        movement.SetNavigationTarget(goal);
        var found = GameObject.FindGameObjectWithTag("Player"); player = found != null ? found.transform : null;
    }
    public static bool IsEligible(PooledEnemy enemy)
    {
        if (enemy == null || enemy.IsReturned || !enemy.gameObject.activeInHierarchy) return false;
        if (enemy.Type != EnemyType.Normal && enemy.Type != EnemyType.Fast && enemy.Type != EnemyType.Heavy && enemy.Type != EnemyType.Ranged) return false;
        var h = enemy.GetComponent<EnemyHealth>();
        return h != null && !h.IsDead && enemy.GetComponent<HiveTyrantCombat>() == null;
    }
    public bool HasValidLease(EnemySupportBuff target)
    {
        if (!Alive || target == null || !target.isActiveAndEnabled || !IsEligible(target.GetComponent<PooledEnemy>()) ||
            ((Vector2)target.transform.position - (Vector2)transform.position).sqrMagnitude > config.supportRadius * config.supportRadius) return false;
        foreach (var lease in leases) if (lease.target == target && lease.generation == target.Generation) return true;
        return false;
    }
    private void Update()
    {
        if (!Alive) { ReleaseAll(); return; }
        if (Time.time >= nextRefresh)
        {
            nextRefresh = Time.time + Mathf.Max(.1f, config.refreshInterval);
            RefreshTargetsAndGoal();
        }
        if (goal != null) goal.position = desiredGoal;
    }
    private void RefreshTargetsAndGoal()
    {
        ReleaseAll(); candidates.Clear();
        var filter = new ContactFilter2D { useTriggers = false }; filter.SetLayerMask(config.allyLayers);
        Physics2D.OverlapCircle(transform.position, config.allySearchRadius, filter, overlaps);
        foreach (var hit in overlaps)
        {
            var candidate = hit.GetComponentInParent<PooledEnemy>();
            if (IsEligible(candidate) && !candidates.Contains(candidate)) candidates.Add(candidate);
        }
        candidates.Sort((a, b) => ((a.transform.position - transform.position).sqrMagnitude).CompareTo((b.transform.position - transform.position).sqrMagnitude));
        foreach (var candidate in candidates)
        {
            if (leases.Count >= Mathf.Clamp(config.maximumTargets, 1, 6)) break;
            if (Vector2.Distance(candidate.transform.position, transform.position) > config.supportRadius) continue;
            var buff = candidate.GetComponent<EnemySupportBuff>();
            if (buff == null) buff = candidate.gameObject.AddComponent<EnemySupportBuff>();
            leases.Add(new Lease { target = buff, generation = buff.Generation }); buff.Grant(this);
        }
        Vector2 position = transform.position;
        if (player == null) { desiredGoal = position; return; }
        Vector2 away = position - (Vector2)player.position;
        if (away.sqrMagnitude < .01f) away = Vector2.up;
        if (away.magnitude < config.keepAwayFromPlayer)
            desiredGoal = (Vector2)player.position + away.normalized * (config.keepAwayFromPlayer + 1f);
        else if (candidates.Count > 0)
        {
            Vector2 anchor = candidates[0].transform.position;
            Vector2 behind = anchor - (Vector2)player.position;
            desiredGoal = anchor + (behind.sqrMagnitude < .01f ? away.normalized : behind.normalized) * config.behindAllyDistance;
        }
        else desiredGoal = position; // No allies: hold distance rather than becoming a melee pursuer.
    }
    public void BeginDeathPresentation() { ReleaseAll(); }
    private void ReleaseAll()
    {
        foreach (var lease in leases) if (lease.target != null) lease.target.Revoke(this, lease.generation);
        leases.Clear();
    }
    private void OnDisable()
    {
        ReleaseAll();
        if (goal != null) goal.gameObject.SetActive(false);
        if (movement != null) movement.SetNavigationTarget(null);
    }
    private void OnDestroy() { if (goal != null) Destroy(goal.gameObject); }
}
