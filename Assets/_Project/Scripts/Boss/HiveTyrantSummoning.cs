using System.Collections.Generic;
using Pathfinding;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HiveTyrantSummoning : MonoBehaviour
{
    public struct SpawnPlan { public Vector2 point; public float clearance; public HiveTyrantSummonChoice choice; public GameObject prefab; }
    private HiveTyrantCombat boss;
    private readonly List<PooledEnemy> owned = new List<PooledEnemy>(16);
    private readonly List<SpawnPlan> pending = new List<SpawnPlan>(8);
    private readonly Collider2D[] overlaps = new Collider2D[64];
    public int Total { get; private set; }
    public int PendingCount => pending.Count;
    public int Living => Count(null);
    public int WaveSize => boss.Phase2Entered ? boss.Config.phase2SummonWaveSize : boss.Config.summonWaveSize;
    public int AliveCap => boss.Phase2Entered ? boss.Config.phase2SummonAliveCap : boss.Config.summonAliveCap;
    public SpawnPlan Pending(int index) => pending[index];
    private void Awake() { boss = GetComponent<HiveTyrantCombat>(); }
    public int Count(EnemyType? type)
    {
        int count = 0;
        for (int i = owned.Count - 1; i >= 0; i--)
        {
            var enemy = owned[i];
            var lease = enemy != null ? enemy.GetComponent<HiveTyrantSummonOwner>() : null;
            if (enemy == null || enemy.IsReturned || lease == null || lease.Owner != boss) { owned.RemoveAt(i); continue; }
            if (!enemy.GetComponent<EnemyHealth>().IsDead && (!type.HasValue || enemy.Type == type.Value)) count++;
        }
        return count;
    }
    private bool Eligible(HiveTyrantSummonChoice choice, out GameObject prefab)
    {
        prefab = null;
        if (choice == null || choice.weight <= 0 || choice.aliveCap <= 0 || !EnemyPool.Instance.TryGetConfiguredPrefab(choice.type, out prefab)) return false;
        var definition = prefab.GetComponent<EnemyDefinition>();
        if (definition == null || definition.Data == null || definition.Data.Type != choice.type) return false;
        int reserved = 0; foreach (var plan in pending) if (plan.choice.type == choice.type) reserved++;
        return Count(choice.type) + reserved < choice.aliveCap;
    }
    public bool CanSummon()
    {
        var c = boss.Config;
        if (EnemyPool.Instance == null || !EnemyPool.Instance.IsInitialized || WaveSize <= 0 ||
            Total >= c.summonBudget || Living >= AliveCap || c.summonChoices == null) return false;
        foreach (var choice in c.summonChoices) if (Eligible(choice, out _)) return true;
        return false;
    }
    private bool Select(out HiveTyrantSummonChoice selected, out GameObject prefab)
    {
        selected = null; prefab = null; float total = 0;
        foreach (var choice in boss.Config.summonChoices) if (Eligible(choice, out _)) total += choice.weight;
        if (total <= 0) return false;
        float roll = Random.value * total;
        foreach (var choice in boss.Config.summonChoices)
        {
            if (!Eligible(choice, out GameObject candidate)) continue;
            roll -= choice.weight;
            if (roll > 0) continue;
            selected = choice; prefab = candidate; return true;
        }
        return false;
    }
    public void Prepare()
    {
        pending.Clear(); var c = boss.Config;
        int count = Mathf.Min(WaveSize, AliveCap - Living, c.summonBudget - Total);
        for (int i = 0; i < count; i++)
        {
            if (!Select(out HiveTyrantSummonChoice choice, out GameObject prefab)) break;
            float clearance = Clearance(prefab);
            for (int attempt = 0; attempt < c.summonAttempts; attempt++)
            {
                float angle = Random.value * Mathf.PI * 2;
                Vector2 point = boss.Motor.CombatOrigin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(Mathf.Max(c.summonMinRadius, 3.3f + clearance), c.summonMaxRadius);
                if (!IsValid(point, clearance, true)) continue;
                pending.Add(new SpawnPlan { point = point, clearance = clearance, choice = choice, prefab = prefab }); break;
            }
        }
    }
    public void Release()
    {
        foreach (var plan in pending)
        {
            if (Total >= boss.Config.summonBudget || Living >= AliveCap) break;
            if (Count(plan.choice.type) >= plan.choice.aliveCap || !IsValid(plan.point, plan.clearance, false)) continue;
            var enemy = EnemyPool.Instance.GetEnemy(plan.choice.type, plan.point, Quaternion.identity);
            if (enemy == null) continue;
            var definition = enemy.GetComponent<EnemyDefinition>();
            InitializeNative(enemy, plan.prefab, plan.choice);
            if (!definition.HasBeenInitialized) { enemy.ReturnToPool(); continue; }
            var lease = enemy.GetComponent<HiveTyrantSummonOwner>();
            if (lease == null) lease = enemy.gameObject.AddComponent<HiveTyrantSummonOwner>();
            lease.Claim(boss); owned.Add(enemy); Total++;
            boss.GetComponent<HiveTyrantEffects>().Burst(2, plan.point, Vector2.up, plan.clearance * 1.5f);
        }
        pending.Clear();
    }
    public static void InitializeNative(PooledEnemy enemy, GameObject prefab, HiveTyrantSummonChoice choice)
    {
        // Preserve each prefab's base stats, EnemyData multipliers and native attack components.
        // Do not write EnemyData or use the former universal Swarmer override.
        enemy.GetComponent<EnemyDefinition>().InitializeFromGlobalDifficulty(
            Mathf.Max(1, Mathf.RoundToInt(prefab.GetComponent<EnemyHealth>().MaxHealth * choice.healthMultiplier)),
            prefab.GetComponent<EnemyMovement>().MoveSpeed * choice.speedMultiplier,
            Mathf.Max(1, Mathf.RoundToInt(prefab.GetComponent<EnemyContactDamage>().Damage * choice.damageMultiplier)));
    }
    public static float Clearance(GameObject prefab)
    {
        float radius = .2f;
        float scale = prefab.GetComponent<EnemyDefinition>().Data.VisualScale;
        foreach (Collider2D c in prefab.GetComponentsInChildren<Collider2D>())
        {
            if (!c.enabled || c.isTrigger) continue;
            Vector2 offset = c.transform.TransformPoint(c.offset) - prefab.transform.position;
            float localRadius = c is CircleCollider2D circle ? circle.radius : c is BoxCollider2D box ? box.size.magnitude * .5f : .6f;
            radius = Mathf.Max(radius, (offset.magnitude + localRadius * Mathf.Max(Mathf.Abs(c.transform.lossyScale.x), Mathf.Abs(c.transform.lossyScale.y))) * scale);
        }
        return radius + .15f;
    }
    public bool IsValid(Vector2 point, float clearance, bool checkPending)
    {
        var c = boss.Config; var target = boss.Motor.Target;
        if (target == null || Vector2.Distance(point, target.position) < c.summonPlayerClearance + clearance) return false;
        if (!boss.InsideArena(point) || !boss.InsideArena(point + Vector2.right * clearance) || !boss.InsideArena(point - Vector2.right * clearance) ||
            !boss.InsideArena(point + Vector2.up * clearance) || !boss.InsideArena(point - Vector2.up * clearance)) return false;
        Vector2 corridor = point - boss.Motor.CombatOrigin;
        if (Physics2D.CircleCast(boss.Motor.CombatOrigin, clearance, corridor, corridor.magnitude, c.environmentLayers).collider != null) return false;
        int count = Physics2D.OverlapCircleNonAlloc(point, clearance, overlaps);
        if (count == overlaps.Length) return false;
        for (int i = 0; i < count; i++)
        {
            var hit = overlaps[i];
            if (hit.isTrigger || hit.transform.IsChildOf(transform)) continue;
            if (((1 << hit.gameObject.layer) & c.environmentLayers) != 0 || hit.GetComponentInParent<EnemyHealth>() != null || hit.GetComponentInParent<PlayerHealth>() != null) return false;
        }
        if (AstarPath.active == null) return false;
        var spawnNode = AstarPath.active.GetNearest((Vector3)point, NNConstraint.Default);
        var playerNode = AstarPath.active.GetNearest(target.position, NNConstraint.Default);
        if (spawnNode.node == null || playerNode.node == null || Vector2.Distance(point, spawnNode.position) > 1 || !PathUtilities.IsPathPossible(spawnNode.node, playerNode.node)) return false;
        if (checkPending) foreach (var other in pending) if (Vector2.Distance(point, other.point) < clearance + other.clearance) return false;
        return true;
    }
    public void CancelPending() { pending.Clear(); }
    public void Cleanup()
    {
        pending.Clear();
        foreach (var enemy in owned)
        {
            if (enemy == null || enemy.IsReturned) continue;
            var lease = enemy.GetComponent<HiveTyrantSummonOwner>();
            if (lease != null && lease.Owner == boss) enemy.ReturnToPool();
        }
        owned.Clear();
    }
    public void ResetBudget() { Cleanup(); Total = 0; }
    private void OnDisable() { Cleanup(); }
}
