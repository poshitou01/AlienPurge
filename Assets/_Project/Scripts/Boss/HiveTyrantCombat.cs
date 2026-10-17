using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using AlienPurge.ArenaArtV03;

public enum HiveTyrantSkill { Sweep, Slam, Summon }
public enum HiveTyrantState { Dormant, Chase, SweepWindup, SweepRelease, SlamWindup, SlamRelease, SummonWindup, Recovery, PhaseTransition, Dead, Disabled }

[DefaultExecutionOrder(100), DisallowMultipleComponent]
[RequireComponent(typeof(HiveTyrantBossMotor), typeof(EnemyHealth), typeof(HiveTyrantEffects))]
[RequireComponent(typeof(HiveTyrantGroundWave), typeof(HiveTyrantSummoning), typeof(HiveTyrantPresentation))]
public sealed class HiveTyrantCombat : MonoBehaviour, IEnemyDeathPresentation
{
    [SerializeField] private HiveTyrantCombatConfig config;
    [SerializeField] private ArenaV03Boundary arenaBoundary;
    [SerializeField] private Vector2 fallbackHalfSize = new Vector2(26, 18);
    [SerializeField] private bool automaticSkills = true;
    [SerializeField] private UnityEvent onDefeated = new UnityEvent();
    private HiveTyrantBossMotor motor;
    private EnemyHealth health;
    private HiveTyrantEffects effects;
    private HiveTyrantPresentation presentation;
    private HiveTyrantGroundWave wave;
    private HiveTyrantSummoning summoning;
    private Coroutine actionRoutine;
    private bool phase2Entered;
    private float nextActionTime;
    private float nextDecisionTime;
    private bool engagementStarted;
    private readonly float[] skillScores = new float[3];
    private int lastSkill = -1;
    private readonly float[] readyTimes = new float[3];
    private readonly List<HiveTyrantTelegraph> markers = new List<HiveTyrantTelegraph>(8);
    private readonly HashSet<PlayerHealth> damagedPlayers = new HashSet<PlayerHealth>();
    private readonly Collider2D[] damageCandidates = new Collider2D[64];
    private Vector2 home;
    public HiveTyrantState State { get; private set; } = HiveTyrantState.Dormant;
    public HiveTyrantCombatConfig Config => config;
    public EnemyHealth Health => health;
    public HiveTyrantBossMotor Motor => motor;
    public HiveTyrantGroundWave Wave => wave;
    public HiveTyrantSummoning Summoning => summoning;
    public bool Phase2Entered => phase2Entered;
    public bool TargetInArena => motor != null && motor.Target != null && InsideArena(motor.Target.position) &&
        Vector2.Distance(motor.CombatOrigin, motor.Target.position) <= (config != null ? config.engageDistance : 18);
    public bool AutomaticSkills { get => automaticSkills; set => automaticSkills = value; }
    public int TotalSummoned => summoning != null ? summoning.Total : 0;
    public int ReleaseCount { get; private set; }
    public int PhaseTransitionCount { get; private set; }
    public int ActiveWarningCount => markers.Count + (wave != null && wave.IsWarning ? 3 : 0);
    public int PendingSpawnCount => summoning != null ? summoning.PendingCount : 0;
    public int LivingSummons => summoning != null ? summoning.Living : 0;
    public float DeathPresentationDuration => config != null ? Mathf.Max(config.deathTime, 1.6f) : 1.6f;

    private void Awake()
    {
        motor = GetComponent<HiveTyrantBossMotor>(); health = GetComponent<EnemyHealth>();
        effects = GetComponent<HiveTyrantEffects>(); presentation = GetComponent<HiveTyrantPresentation>();
        wave = GetComponent<HiveTyrantGroundWave>(); summoning = GetComponent<HiveTyrantSummoning>();
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
        if (!engagementStarted)
        {
            engagementStarted = true;
            nextActionTime = Time.time + config.openingDelay;
            readyTimes[(int)HiveTyrantSkill.Summon] = Time.time + config.firstSummonDelay;
        }
        if (!automaticSkills || Time.time < nextActionTime || Time.time < nextDecisionTime) return;
        nextDecisionTime = Time.time + config.decisionInterval;
        ChooseAutomaticSkill();
    }
    private void ChooseAutomaticSkill()
    {
        float distance = Vector2.Distance(motor.CombatOrigin, motor.Target.position);
        int living = summoning.Living;
        // Pressure follows the situation. Neither a fixed rotation nor a roll every frame.
        skillScores[0] = distance <= config.sweepRadius - .3f ? 85 : -1;
        skillScores[1] = distance <= config.waveDistance + config.waveStartOffset && !wave.IsTravelling
            ? (distance > config.sweepRadius - 1 ? 95 : 45) : -1;
        skillScores[2] = living <= 2 && summoning.CanSummon() ? (living == 0 ? 105 : 65) : -1;
        for (int i = 0; i < 3; i++)
        {
            if (Time.time < readyTimes[i]) skillScores[i] = -1;
            else if (i == lastSkill && skillScores[i] >= 0) skillScores[i] -= 20;
        }
        // A blocked wave plan can fall back to another legal skill this decision tick.
        for (int attempt = 0; attempt < 3; attempt++)
        {
            int best = -1;
            for (int i = 0; i < 3; i++) if (skillScores[i] >= 0 && (best < 0 || skillScores[i] > skillScores[best])) best = i;
            if (best < 0) return;
            if (RequestSkill((HiveTyrantSkill)best)) return;
            skillScores[best] = -1;
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
            if (skill == HiveTyrantSkill.Slam && distance > config.waveDistance + config.waveStartOffset) return false;
        }
        if (skill == HiveTyrantSkill.Summon && !summoning.CanSummon()) return false;
        if (skill == HiveTyrantSkill.Slam)
        {
            if (wave.IsTravelling || !HiveTyrantWavePlan.TryCreate(this, out HiveTyrantWavePlan plan)) return false;
            wave.Prepare(plan);
        }
        lastSkill = index;
        readyTimes[index] = Time.time + (skill == HiveTyrantSkill.Sweep ? config.sweepCooldown : skill == HiveTyrantSkill.Slam ? config.slamCooldown : config.summonInterval);
        actionRoutine = StartCoroutine(ExecuteSkill(skill)); return true;
    }
    private IEnumerator ExecuteSkill(HiveTyrantSkill skill)
    {
        if (skill == HiveTyrantSkill.Sweep)
        {
            yield return Sweep(config.sweepWindup);
            if (phase2Entered)
            { State = HiveTyrantState.Recovery; yield return new WaitForSeconds(config.sweepRecovery); yield return Sweep(config.phase2SecondSweepWarning); }
        }
        else if (skill == HiveTyrantSkill.Slam) yield return Slam();
        else yield return Summon();
        State = HiveTyrantState.Recovery;
        float recovery = skill == HiveTyrantSkill.Sweep ? config.sweepRecovery : skill == HiveTyrantSkill.Slam ? config.slamRecovery : config.summonRecovery;
        yield return new WaitForSeconds(recovery);
        ClearWarnings(); presentation.StopAction(); motor.EndAction();
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
        presentation.Play(6, warning, .18f, config.sweepRecovery);
        var marker = AddWarning(origin, direction, config.sweepRadius, config.sweepAngle, new Color(.92f, .58f, .26f, .25f));
        yield return new WaitForSeconds(warning);
        State = HiveTyrantState.SweepRelease; marker.Released();
        effects.Burst(0, origin, direction, config.sweepRadius, config.sweepAngle);
        DealAreaDamage(origin, direction, config.sweepRadius, config.sweepAngle, config.sweepDamage);
        ReleaseCount++; yield return new WaitForSeconds(.18f); ClearWarnings();
    }
    private IEnumerator Slam()
    {
        motor.BeginAction(7, wave.Plan.forward); State = HiveTyrantState.SlamWindup;
        presentation.Play(7, config.slamWindup, .22f, config.slamRecovery);
        yield return new WaitForSeconds(config.slamWindup);
        State = HiveTyrantState.SlamRelease; wave.Launch(); ReleaseCount++;
        if (CameraFollow.Instance != null) CameraFollow.Instance.PlayMediumShake();
        yield return new WaitForSeconds(.22f);
        // Wave owns its complete travel lifetime, independent of this body's recovery.
    }
    private IEnumerator Summon()
    {
        motor.BeginAction(8, Aim()); State = HiveTyrantState.SummonWindup;
        presentation.Play(8, config.summonWarning, .2f, config.summonRecovery);
        summoning.Prepare();
        for (int i = 0; i < summoning.PendingCount; i++)
        {
            var spawn = summoning.Pending(i);
            AddWarning(spawn.point, Vector2.right, spawn.clearance, 360, new Color(.65f, .48f, .8f, .45f));
        }
        yield return new WaitForSeconds(config.summonWarning);
        summoning.Release(); ReleaseCount++; ClearWarnings(); yield return new WaitForSeconds(.2f);
    }
    public bool IsValidSpawnPoint(Vector2 point, bool checkPending) => summoning.IsValid(point, config.summonClearance, checkPending);
    public bool InsideArena(Vector2 point)
    {
        if (arenaBoundary == null || arenaBoundary.footprint == null || arenaBoundary.footprint.Length < 3)
        { Vector2 p = point - home; return Mathf.Abs(p.x) < fallbackHalfSize.x && Mathf.Abs(p.y) < fallbackHalfSize.y; }
        Vector2 local = arenaBoundary.transform.InverseTransformPoint(point);
        Vector2[] polygon = arenaBoundary.footprint; bool inside = false;
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
        int count = Physics2D.OverlapCircleNonAlloc(origin, radius, damageCandidates);
        for (int i = 0; i < count; i++)
        {
            var player = damageCandidates[i].GetComponentInParent<PlayerHealth>();
            if (player == null || player.IsDead || !ContainsPoint(origin, direction, player.transform.position, radius, angle)) continue;
            if (Physics2D.Linecast(origin, player.transform.position, config.environmentLayers).collider != null) continue;
            if (damagedPlayers.Add(player)) player.TakeDamage(damage);
        }
    }
    private IEnumerator PhaseTransition()
    {
        State = HiveTyrantState.PhaseTransition; motor.BeginAction(9, Aim());
        float duration = config.phaseTransitionTime;
        presentation.Play(9, duration * .55f, .2f, duration * .45f - .2f);
        effects.Burst(3, motor.CombatOrigin, Vector2.up, 4.5f);
        // Gather energy before revealing the winged silhouette. No damage or camera cut.
        yield return new WaitForSeconds(duration * .42f);
        motor.EnterPhase2(); presentation.RevealPhase2();
        yield return new WaitForSeconds(duration * .58f);
        presentation.StopAction(); motor.EndAction(); nextActionTime = Time.time + config.globalRecovery;
        State = HiveTyrantState.Chase; actionRoutine = null;
    }
    public void BeginDeathPresentation()
    {
        CancelAction(); State = HiveTyrantState.Dead; motor.SetDead(true); summoning.Cleanup();
        presentation.Play(3, DeathPresentationDuration * .5f, DeathPresentationDuration * .25f, DeathPresentationDuration * .25f);
        onDefeated.Invoke();
    }
    private HiveTyrantTelegraph AddWarning(Vector2 origin, Vector2 direction, float radius, float angle, Color color)
    { var marker = HiveTyrantTelegraph.Create(transform, origin, direction, radius, angle, color); markers.Add(marker); return marker; }
    private void ClearWarnings() { foreach (var marker in markers) if (marker != null) marker.Release(); markers.Clear(); }
    private void CancelAction()
    {
        if (actionRoutine != null) StopCoroutine(actionRoutine);
        actionRoutine = null; if (summoning != null) summoning.CancelPending(); ClearWarnings();
        if (wave != null) wave.Cancel(); if (effects != null) effects.ClearAll(); if (presentation != null) presentation.StopAction();
    }
    public void RestartEncounter()
    {
        if (health == null || health.IsDead) return;
        CancelAction(); summoning.ResetBudget(); health.PrepareForSpawn(); health.InitializeHealth(config.maxHealth);
        GetComponent<Rigidbody2D>().position = home; transform.position = new Vector3(home.x, home.y, transform.position.z); Physics2D.SyncTransforms();
        motor.ResetEncounter(); phase2Entered = false; PhaseTransitionCount = 0; ReleaseCount = 0; lastSkill = -1;
        for (int i = 0; i < readyTimes.Length; i++) readyTimes[i] = 0;
        engagementStarted = false; nextDecisionTime = 0;
        nextActionTime = Time.time + config.openingDelay; State = HiveTyrantState.Chase;
    }
    private void OnDisable()
    {
        CancelAction(); if (summoning != null) summoning.Cleanup();
        if (motor != null && !motor.IsDead) { motor.EndAction(); motor.SetMovementLocked(true); State = HiveTyrantState.Disabled; }
    }
}
