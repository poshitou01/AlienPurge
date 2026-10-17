using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using AlienPurge.ArenaArtV03;

public enum HiveTyrantSkill { Sweep, Slam, Summon }
public enum HiveTyrantState { Dormant, Chase, SweepWindup, SweepRelease, SlamWindup, SlamRelease, SummonWindup, Recovery, PhaseTransition, Dead, Disabled }

[DefaultExecutionOrder(100), DisallowMultipleComponent]
[RequireComponent(typeof(HiveTyrantBossMotor), typeof(EnemyHealth))]
public sealed class HiveTyrantCombat : MonoBehaviour, IEnemyDeathPresentation
{
    [SerializeField] private HiveTyrantCombatConfig config;
    [SerializeField] private ArenaV03Boundary arenaBoundary;
    [Tooltip("Fallback local encounter bounds, also used if no authored arena footprint exists.")]
    [SerializeField] private Vector2 fallbackHalfSize = new Vector2(26, 18);
    [SerializeField] private bool automaticSkills = true;
    [SerializeField] private UnityEvent onDefeated = new UnityEvent();
    private HiveTyrantBossMotor motor;
    private EnemyHealth health;
    private Coroutine actionRoutine;
    private bool phase2Entered;
    private float nextActionTime;
    private int lastSkill = -1;
    private readonly float[] readyTimes = new float[3];
    private readonly List<HiveTyrantTelegraph> markers = new List<HiveTyrantTelegraph>();
    private readonly List<PooledEnemy> summons = new List<PooledEnemy>();
    private readonly List<Vector2> pendingSpawns = new List<Vector2>();
    private readonly HashSet<PlayerHealth> damagedPlayers = new HashSet<PlayerHealth>();
    private Vector2 home;
    public HiveTyrantState State { get; private set; } = HiveTyrantState.Dormant;
    public HiveTyrantCombatConfig Config => config;
    public EnemyHealth Health => health;
    public HiveTyrantBossMotor Motor => motor;
    public bool Phase2Entered => phase2Entered;
    public bool TargetInArena => motor != null && motor.Target != null && InsideArena(motor.Target.position) &&
        Vector2.Distance(motor.CombatOrigin, motor.Target.position) <= (config != null ? config.engageDistance : 18);
    public bool AutomaticSkills { get => automaticSkills; set => automaticSkills = value; }
    public int TotalSummoned { get; private set; }
    public int ReleaseCount { get; private set; }
    public int PhaseTransitionCount { get; private set; }
    public int ActiveWarningCount => markers.Count;
    public int PendingSpawnCount => pendingSpawns.Count;
    public int LivingSummons
    {
        get { PruneSummons(); return summons.FindAll(enemy => !enemy.GetComponent<EnemyHealth>().IsDead).Count; }
    }
    public float DeathPresentationDuration => config != null ? Mathf.Max(config.deathTime, LongestDeathClip()) : 1.6f;

    private void Awake()
    {
        motor = GetComponent<HiveTyrantBossMotor>(); health = GetComponent<EnemyHealth>();
        home = transform.position;
        if (config == null) { Debug.LogError("HiveTyrantCombat: missing combat config on " + name, this); enabled = false; return; }
        health.InitializeHealth(config.maxHealth);
    }
    private void OnEnable()
    {
        if (health != null && !health.IsDead && State == HiveTyrantState.Disabled)
        { motor.EndAction(); motor.SetMovementLocked(false); State = HiveTyrantState.Chase; }
    }
    private void Update()
    {
        if (config == null || health.IsDead || motor.IsDead || State == HiveTyrantState.Disabled) return;
        if (!phase2Entered && health.CurrentHealth > 0 && health.CurrentHealth <= health.MaxHealth * config.phase2HealthRatio)
        {
            CancelAction(); phase2Entered = true; PhaseTransitionCount++;
            actionRoutine = StartCoroutine(PhaseTransition()); return;
        }
        if (actionRoutine != null) return;
        if (!CanEngage()) { State = HiveTyrantState.Dormant; return; }
        State = HiveTyrantState.Chase;
        if (!automaticSkills || Time.time < nextActionTime) return;
        // Rotate eligible choices, so a ready nearby skill cannot starve the others.
        for (int n = 1; n <= 3; n++)
        {
            int candidate = (lastSkill + n) % 3;
            if (RequestSkill((HiveTyrantSkill)candidate)) break;
        }
    }
    private bool CanEngage()
    {
        if (!isActiveAndEnabled || health.IsDead || motor.IsDead || motor.IsMovementLocked || motor.Target == null) return false;
        var player = motor.Target.GetComponentInParent<PlayerHealth>();
        if (player != null && player.IsDead) return false;
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return false;
        return TargetInArena;
    }
    public bool RequestSkill(HiveTyrantSkill skill, bool debugIgnoreCooldownAndRange = false)
    {
        if (config == null || actionRoutine != null || !CanEngage()) return false;
        int index = (int)skill;
        if (index < 0 || index >= 3) return false;
        float distance = Vector2.Distance(motor.CombatOrigin, motor.Target.position);
        if (!debugIgnoreCooldownAndRange)
        {
            if (Time.time < nextActionTime || Time.time < readyTimes[index]) return false;
            if (skill == HiveTyrantSkill.Sweep && distance > config.sweepRadius - .3f) return false;
            if (skill == HiveTyrantSkill.Slam && distance > config.slamRadius - .3f) return false;
        }
        if (skill == HiveTyrantSkill.Summon && !CanSummon()) return false;
        lastSkill = index;
        float cooldown = skill == HiveTyrantSkill.Sweep ? config.sweepCooldown : skill == HiveTyrantSkill.Slam ? config.slamCooldown : config.summonInterval;
        readyTimes[index] = Time.time + cooldown;
        actionRoutine = StartCoroutine(ExecuteSkill(skill));
        return true;
    }
    private IEnumerator ExecuteSkill(HiveTyrantSkill skill)
    {
        if (skill == HiveTyrantSkill.Sweep)
        {
            yield return Sweep(config.sweepWindup);
            if (phase2Entered)
            {
                // Distinct recovery and a fresh locked telegraph before the second hit.
                State = HiveTyrantState.Recovery;
                yield return new WaitForSeconds(config.sweepRecovery);
                yield return Sweep(config.phase2SecondSweepWarning);
            }
        }
        else if (skill == HiveTyrantSkill.Slam) yield return Slam();
        else yield return Summon();
        State = HiveTyrantState.Recovery;
        float recovery = skill == HiveTyrantSkill.Sweep ? config.sweepRecovery : skill == HiveTyrantSkill.Slam ? config.slamRecovery : config.summonRecovery;
        yield return new WaitForSeconds(recovery);
        ClearWarnings(); motor.EndAction();
        nextActionTime = Time.time + config.globalRecovery;
        State = HiveTyrantState.Chase; actionRoutine = null;
    }
    private Vector2 Aim()
    {
        Vector2 delta = motor.Target != null ? (Vector2)motor.Target.position - motor.CombatOrigin : Vector2.down;
        return delta.sqrMagnitude > .0001f ? delta.normalized : Vector2.down;
    }
    private IEnumerator Sweep(float warning)
    {
        Vector2 origin = motor.CombatOrigin, direction = Aim();
        motor.BeginAction(6, direction); State = HiveTyrantState.SweepWindup;
        var marker = AddWarning(origin, direction, config.sweepRadius, config.sweepAngle, new Color(1, .45f, .08f, .32f));
        yield return new WaitForSeconds(warning);
        State = HiveTyrantState.SweepRelease; marker.Released();
        DealAreaDamage(origin, direction, config.sweepRadius, config.sweepAngle, config.sweepDamage);
        ReleaseCount++;
        yield return new WaitForSeconds(.18f);
        ClearWarnings();
    }
    private IEnumerator Slam()
    {
        Vector2 origin = motor.CombatOrigin;
        motor.BeginAction(7, Aim()); State = HiveTyrantState.SlamWindup;
        var marker = AddWarning(origin, Vector2.right, config.slamRadius, 360, new Color(.85f, .25f, .1f, .3f));
        yield return new WaitForSeconds(config.slamWindup);
        State = HiveTyrantState.SlamRelease; marker.Released();
        DealAreaDamage(origin, Vector2.right, config.slamRadius, 360, config.slamDamage);
        ReleaseCount++;
        if (CameraFollow.Instance != null) CameraFollow.Instance.PlayMediumShake();
        yield return new WaitForSeconds(.22f);
        ClearWarnings();
    }
    private bool CanSummon()
    {
        return EnemyPool.Instance != null && EnemyPool.Instance.IsInitialized &&
            TotalSummoned < config.summonBudget && LivingSummons < config.summonAliveCap && config.summonWaveSize > 0;
    }
    private IEnumerator Summon()
    {
        motor.BeginAction(8, Aim()); State = HiveTyrantState.SummonWindup;
        pendingSpawns.Clear();
        int count = Mathf.Min(config.summonWaveSize, config.summonAliveCap - LivingSummons, config.summonBudget - TotalSummoned);
        for (int i = 0; i < count; i++)
        {
            if (!TrySpawnPoint(out Vector2 point)) continue;
            pendingSpawns.Add(point);
            AddWarning(point, Vector2.right, config.summonClearance, 360, new Color(.35f, .8f, 1, .5f));
        }
        yield return new WaitForSeconds(config.summonWarning);
        foreach (Vector2 point in pendingSpawns)
        {
            if (LivingSummons >= config.summonAliveCap || TotalSummoned >= config.summonBudget) break;
            // Revalidate after the player had time to move through the warning.
            if (!IsValidSpawnPoint(point, false)) continue;
            if (EnemyPool.Instance == null) break;
            PooledEnemy enemy = EnemyPool.Instance.GetEnemy(EnemyType.Normal, point, Quaternion.identity);
            if (enemy == null) continue;
            HiveTyrantSummonOwner lease = enemy.GetComponent<HiveTyrantSummonOwner>();
            if (lease == null) lease = enemy.gameObject.AddComponent<HiveTyrantSummonOwner>();
            lease.Claim(this); summons.Add(enemy); TotalSummoned++;
            var definition = enemy.GetComponent<EnemyDefinition>();
            if (definition != null && definition.Data != null)
                definition.InitializeFromGlobalDifficulty(config.summonHealth, config.summonSpeed, config.summonDamage);
        }
        ReleaseCount++;
        pendingSpawns.Clear(); ClearWarnings();
    }
    private bool TrySpawnPoint(out Vector2 point)
    {
        for (int attempt = 0; attempt < config.summonAttempts; attempt++)
        {
            float angle = Random.value * Mathf.PI * 2;
            float radius = Random.Range(config.summonMinRadius, Mathf.Max(config.summonMinRadius, config.summonMaxRadius));
            point = motor.CombatOrigin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            if (IsValidSpawnPoint(point, true)) return true;
        }
        point = default; return false;
    }
    public bool IsValidSpawnPoint(Vector2 point, bool checkPending)
    {
        if (motor.Target == null || Vector2.Distance(point, motor.Target.position) < config.summonPlayerClearance) return false;
        if (!InsideArena(point)) return false;
        // Require a clear straight corridor to the boss: conservative but reachable without a new path system.
        if (Physics2D.CircleCast(motor.CombatOrigin, config.summonClearance, point - motor.CombatOrigin,
            Vector2.Distance(point, motor.CombatOrigin), config.environmentLayers).collider != null) return false;
        foreach (Collider2D c in Physics2D.OverlapCircleAll(point, config.summonClearance))
        {
            if (c.isTrigger || c.transform.IsChildOf(transform)) continue;
            if (((1 << c.gameObject.layer) & config.environmentLayers) != 0 || c.GetComponentInParent<EnemyHealth>() != null || c.GetComponentInParent<PlayerHealth>() != null) return false;
        }
        if (checkPending)
            foreach (Vector2 pending in pendingSpawns)
                if (Vector2.Distance(point, pending) < config.summonClearance * 2) return false;
        return true;
    }
    public bool InsideArena(Vector2 point)
    {
        if (arenaBoundary == null || arenaBoundary.footprint == null || arenaBoundary.footprint.Length < 3)
        { Vector2 p = point - home; return Mathf.Abs(p.x) < fallbackHalfSize.x && Mathf.Abs(p.y) < fallbackHalfSize.y; }
        Vector2 local = arenaBoundary.transform.InverseTransformPoint(point);
        Vector2[] polygon = arenaBoundary.footprint;
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            if ((polygon[i].y > local.y) != (polygon[j].y > local.y) &&
                local.x < (polygon[j].x - polygon[i].x) * (local.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x) inside = !inside;
        return inside;
    }
    public static bool ContainsPoint(Vector2 origin, Vector2 direction, Vector2 point, float radius, float angle)
    {
        Vector2 delta = point - origin;
        return delta.sqrMagnitude <= radius * radius && (angle >= 359.9f || delta.sqrMagnitude < .000001f || Vector2.Angle(direction, delta) <= angle * .5f);
    }
    private void DealAreaDamage(Vector2 origin, Vector2 direction, float radius, float angle, int damage)
    {
        damagedPlayers.Clear();
        foreach (Collider2D collider in Physics2D.OverlapCircleAll(origin, radius))
        {
            var player = collider.GetComponentInParent<PlayerHealth>();
            if (player == null || player.IsDead || !ContainsPoint(origin, direction, player.transform.position, radius, angle)) continue;
            // Walls block hits as well as movement. No through-wall surprises.
            if (Physics2D.Linecast(origin, player.transform.position, config.environmentLayers).collider != null) continue;
            if (damagedPlayers.Add(player)) player.TakeDamage(damage);
        }
    }
    private IEnumerator PhaseTransition()
    {
        State = HiveTyrantState.PhaseTransition;
        motor.EnterPhase2(); motor.BeginAction(9, Aim());
        AddWarning(motor.CombatOrigin, Vector2.right, 3.5f, 360, new Color(1, .65f, .2f, .3f));
        yield return new WaitForSeconds(config.phaseTransitionTime);
        ClearWarnings(); motor.EndAction(); nextActionTime = Time.time + config.globalRecovery;
        State = HiveTyrantState.Chase; actionRoutine = null;
    }
    public void BeginDeathPresentation()
    {
        CancelAction(); State = HiveTyrantState.Dead; motor.SetDead(true); CleanupSummons();
        onDefeated.Invoke();
    }
    private float LongestDeathClip()
    {
        float length = 0;
        if (motor != null && motor.BodyAnimator != null && motor.BodyAnimator.runtimeAnimatorController != null)
            foreach (AnimationClip clip in motor.BodyAnimator.runtimeAnimatorController.animationClips)
                if (clip.name.ToLowerInvariant().Contains("death")) length = Mathf.Max(length, clip.length);
        return length;
    }
    private HiveTyrantTelegraph AddWarning(Vector2 origin, Vector2 direction, float radius, float angle, Color color)
    {
        var marker = HiveTyrantTelegraph.Create(transform, origin, direction, radius, angle, color);
        markers.Add(marker); return marker;
    }
    private void ClearWarnings()
    {
        foreach (var marker in markers) if (marker != null) { marker.gameObject.SetActive(false); Destroy(marker.gameObject); }
        markers.Clear();
    }
    private void CancelAction()
    {
        if (actionRoutine != null) StopCoroutine(actionRoutine);
        actionRoutine = null; pendingSpawns.Clear(); ClearWarnings();
    }
    private void PruneSummons()
    {
        summons.RemoveAll(enemy => enemy == null || enemy.IsReturned || !enemy.gameObject.activeInHierarchy ||
            enemy.GetComponent<HiveTyrantSummonOwner>() == null || enemy.GetComponent<HiveTyrantSummonOwner>().Owner != this);
    }
    private void CleanupSummons()
    {
        // Retain dying owned instances until their death routine returns them; do not touch reused instances.
        foreach (PooledEnemy enemy in summons)
        {
            if (enemy == null || enemy.IsReturned) continue;
            var lease = enemy.GetComponent<HiveTyrantSummonOwner>();
            if (lease != null && lease.Owner == this) enemy.ReturnToPool();
        }
        summons.Clear();
    }
    public void RestartEncounter()
    {
        if (health == null || health.IsDead) return; // Dead bosses are recreated by the test-scene restart.
        CancelAction(); CleanupSummons();
        health.PrepareForSpawn(); health.InitializeHealth(config.maxHealth);
        GetComponent<Rigidbody2D>().position = home;
        transform.position = new Vector3(home.x, home.y, transform.position.z);
        Physics2D.SyncTransforms();
        motor.ResetEncounter(); phase2Entered = false; PhaseTransitionCount = 0; TotalSummoned = 0; ReleaseCount = 0; lastSkill = -1;
        for (int i = 0; i < readyTimes.Length; i++) readyTimes[i] = 0;
        nextActionTime = Time.time + config.globalRecovery; State = HiveTyrantState.Chase;
    }
    private void OnDisable()
    {
        CancelAction(); CleanupSummons();
        if (motor != null && !motor.IsDead) { motor.EndAction(); motor.SetMovementLocked(true); State = HiveTyrantState.Disabled; }
    }
}
