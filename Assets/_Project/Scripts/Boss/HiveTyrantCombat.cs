using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using AlienPurge.ArenaArtV03;

public enum HiveTyrantSkill { Sweep, Slam, Summon, SporeBarrage }
public enum HiveTyrantState { Dormant, Chase, SweepWindup, SweepRelease, SlamWindup, SlamRelease, SummonWindup, Recovery, PhaseTransition, Dead, Disabled, SporeWindup }

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
    private readonly float[] skillScores = new float[4];
    private int lastSkill = -1;
    private readonly float[] readyTimes = new float[4];
    private readonly float[] lastUsed = new float[4];
    private bool entryCaptured;
    private bool phase2RallyPending;
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
    public HiveTyrantPressureAttacks Pressure { get; private set; }
    public float EntryPower { get; private set; } = 1;
    public float SummonHealthScale { get; private set; } = 1;
    public bool EntryCaptured => entryCaptured;
    public Vector2 SweepOrigin { get; private set; }
    public Vector2 SweepDirection { get; private set; }
    public float SweepReleaseAt { get; private set; }
    public readonly int[] SkillReleases = new int[4];
    public bool Phase2Entered => phase2Entered;
    public bool TargetInArena => motor != null && motor.Target != null && InsideArena(motor.Target.position) &&
        (entryCaptured || Vector2.Distance(motor.CombatOrigin, motor.Target.position) <= (config != null ? config.engageDistance : 18));
    public bool AutomaticSkills { get => automaticSkills; set => automaticSkills = value; }
    public int TotalSummoned => summoning != null ? summoning.Total : 0;
    public int ReleaseCount { get; private set; }
    public int PhaseTransitionCount { get; private set; }
    public int ActiveWarningCount => markers.Count + (wave != null && wave.IsWarning ? 3 : 0) + (Pressure != null ? Pressure.WarningCount : 0);
    public int PendingSpawnCount => summoning != null ? summoning.PendingCount : 0;
    public int LivingSummons => summoning != null ? summoning.Living : 0;
    public float DeathPresentationDuration => config != null ? Mathf.Max(config.deathTime, 1.6f) : 1.6f;

    private void Awake()
    {
        motor = GetComponent<HiveTyrantBossMotor>(); health = GetComponent<EnemyHealth>();
        effects = GetComponent<HiveTyrantEffects>(); presentation = GetComponent<HiveTyrantPresentation>();
        wave = GetComponent<HiveTyrantGroundWave>(); summoning = GetComponent<HiveTyrantSummoning>();
        Pressure = new HiveTyrantPressureAttacks(this, effects);
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
        if (!entryCaptured && CanEngage()) CaptureEntryLoadout();
        if (actionRoutine != null && State != HiveTyrantState.PhaseTransition && !CanEngage())
        { CancelAction(); motor.EndAction(); State = HiveTyrantState.Dormant; return; }
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
            readyTimes[(int)HiveTyrantSkill.SporeBarrage] = Time.time + config.openingDelay + 4;
            for (int i = 0; i < lastUsed.Length; i++) lastUsed[i] = Time.time;
        }
        if (!automaticSkills || Time.time < nextActionTime || Time.time < nextDecisionTime) return;
        nextDecisionTime = Time.time + config.decisionInterval;
        ChooseAutomaticSkill();
    }
    private void FixedUpdate() { if (Pressure != null && config != null) Pressure.Advance(Time.fixedDeltaTime); }
    public void CaptureEntryLoadout()
    {
        if (entryCaptured || !CanEngage()) return;
        entryCaptured = true;
        EntryPower = HiveTyrantEncounterBalance.OffensivePower(motor.Target);
        SummonHealthScale = config.scaleHealthWithEntryLoadout ? Mathf.Clamp(Mathf.Pow(EntryPower, .35f), 1, config.maximumSummonHealthScale) : 1;
        int taken = Mathf.Max(0, health.MaxHealth - health.CurrentHealth);
        health.InitializeHealth(HiveTyrantEncounterBalance.Health(config, EntryPower));
        if (taken > 0) health.TakeDamage(taken);
    }
    private void ChooseAutomaticSkill()
    {
        if (phase2RallyPending)
        {
            if (summoning.CanSummon() && RequestSkill(HiveTyrantSkill.Summon)) { phase2RallyPending = false; return; }
            if (!summoning.CanSummon()) phase2RallyPending = false;
        }
        float distance = Vector2.Distance(motor.CombatOrigin, motor.Target.position);
        int living = summoning.Living;
        // Pressure follows the situation. Neither a fixed rotation nor a roll every frame.
        skillScores[0] = distance <= config.sweepRadius + config.sweepTravelDistance - 1 ? (distance < config.sweepRadius ? 90 : 65) : -1;
        skillScores[1] = distance <= config.waveDistance + config.waveStartOffset && !wave.IsTravelling
            ? (distance > config.sweepRadius - 1 ? 95 : 45) : -1;
        skillScores[2] = living <= summoning.AliveCap / 2 && summoning.CanSummon() ? (living == 0 ? 115 : 85) : -1;
        skillScores[3] = distance >= 3 ? 75 : 55;
        for (int i = 0; i < skillScores.Length; i++)
        {
            if (Time.time < readyTimes[i]) skillScores[i] = -1;
            else if (skillScores[i] >= 0)
            {
                skillScores[i] += Mathf.Min(45, (Time.time - lastUsed[i]) * 2);
                if (i == lastSkill) skillScores[i] -= 35;
            }
        }
        // A blocked wave plan can fall back to another legal skill this decision tick.
        for (int attempt = 0; attempt < skillScores.Length; attempt++)
        {
            int best = -1;
            for (int i = 0; i < skillScores.Length; i++) if (skillScores[i] >= 0 && (best < 0 || skillScores[i] > skillScores[best])) best = i;
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
        if (index < 0 || index >= skillScores.Length) return false;
        CaptureEntryLoadout();
        // Adds may overlap a hazard; two ground patterns cannot seal each other's safe route.
        if (skill != HiveTyrantSkill.Summon && (Pressure.Active || wave.IsTravelling)) return false;
        float distance = Vector2.Distance(motor.CombatOrigin, motor.Target.position);
        if (!debugIgnoreCooldownAndRange)
        {
            if (Time.time < nextActionTime || Time.time < readyTimes[index]) return false;
            if (skill == HiveTyrantSkill.Sweep && distance > config.sweepRadius + config.sweepTravelDistance - 1) return false;
            if (skill == HiveTyrantSkill.Slam && distance > config.waveDistance + config.waveStartOffset) return false;
        }
        if (skill == HiveTyrantSkill.Summon && !summoning.CanSummon()) return false;
        if (skill == HiveTyrantSkill.Slam)
        {
            if (wave.IsTravelling || !HiveTyrantWavePlan.TryCreate(this, out HiveTyrantWavePlan plan)) return false;
            wave.Prepare(plan);
        }
        lastSkill = index;
        lastUsed[index] = Time.time;
        float cooldown = skill == HiveTyrantSkill.Sweep ? config.sweepCooldown : skill == HiveTyrantSkill.Slam ? config.slamCooldown :
            skill == HiveTyrantSkill.SporeBarrage ? config.sporeCooldown : (phase2Entered ? config.phase2SummonInterval : config.summonInterval);
        readyTimes[index] = Time.time + cooldown;
        actionRoutine = StartCoroutine(ExecuteSkill(skill)); return true;
    }
    private IEnumerator ExecuteSkill(HiveTyrantSkill skill)
    {
        if (skill == HiveTyrantSkill.Sweep)
        {
            yield return Sweep(config.sweepWindup);
            if (phase2Entered)
            {
                State = HiveTyrantState.Recovery;
                yield return new WaitForSeconds(Mathf.Max(config.sweepRecovery, config.phase2SweepSpacing - config.phase2SecondSweepWarning - .18f));
                yield return Sweep(config.phase2SecondSweepWarning);
            }
        }
        else if (skill == HiveTyrantSkill.Slam) yield return Slam();
        else if (skill == HiveTyrantSkill.Summon) yield return Summon();
        else yield return SporeBarrage();
        State = HiveTyrantState.Recovery;
        float recovery = skill == HiveTyrantSkill.Sweep ? config.sweepRecovery : skill == HiveTyrantSkill.Slam ? config.slamRecovery : config.summonRecovery;
        yield return new WaitForSeconds(recovery);
        ClearWarnings(); presentation.StopAction(); motor.EndAction();
        nextActionTime = Time.time + (phase2Entered ? config.phase2GlobalRecovery : config.globalRecovery);
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
        SweepOrigin = origin; SweepDirection = direction; SweepReleaseAt = Time.time + warning;
        motor.BeginAction(6, direction); State = HiveTyrantState.SweepWindup;
        presentation.Play(6, warning, .18f, config.sweepRecovery);
        var marker = AddWarning(origin, direction, config.sweepRadius, config.sweepAngle, new Color(.92f, .58f, .26f, .25f));
        effects.DrawSweepGuide(origin, direction);
        yield return new WaitForSeconds(warning);
        State = HiveTyrantState.SweepRelease; marker.Released();
        effects.HideSweepGuide(); Pressure.LaunchCrescent(origin, direction);
        ReleaseCount++; SkillReleases[0]++; yield return new WaitForSeconds(.18f); ClearWarnings();
    }
    private IEnumerator Slam()
    {
        motor.BeginAction(7, wave.Plan.forward); State = HiveTyrantState.SlamWindup;
        presentation.Play(7, config.slamWindup, .22f, config.slamRecovery);
        yield return new WaitForSeconds(config.slamWindup);
        State = HiveTyrantState.SlamRelease; wave.Launch(); ReleaseCount++; SkillReleases[1]++;
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
        summoning.Release(); ReleaseCount++; SkillReleases[2]++; ClearWarnings(); yield return new WaitForSeconds(.2f);
    }
    private IEnumerator SporeBarrage()
    {
        State = HiveTyrantState.SporeWindup; motor.BeginAction(10, Aim());
        int count = Mathf.Clamp(phase2Entered ? config.phase2SporeCount : config.sporeCount, 1, Pressure.Spores.Length);
        presentation.Play(10, .5f, Mathf.Max(.2f, (count - 1) * config.sporeInterval), .6f);
        yield return new WaitForSeconds(.5f);
        for (int i = 0; i < count; i++)
        {
            // Sample once per shell. A locked circle never follows the player during its warning.
            if (motor.Target != null) Pressure.MarkSpore(motor.Target.position);
            ReleaseCount++; SkillReleases[3]++;
            if (i + 1 < count) yield return new WaitForSeconds(config.sporeInterval);
        }
        yield return new WaitForSeconds(.25f);
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
        // The new phase answers with a brood rally, even if an interrupted P1 summon spent its cooldown.
        phase2RallyPending = true; readyTimes[(int)HiveTyrantSkill.Summon] = Time.time;
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
        Pressure?.Cancel();
    }
    public void RestartEncounter()
    {
        if (health == null || health.IsDead) return;
        CancelAction(); summoning.ResetBudget(); health.PrepareForSpawn(); health.InitializeHealth(config.maxHealth);
        GetComponent<Rigidbody2D>().position = home; transform.position = new Vector3(home.x, home.y, transform.position.z); Physics2D.SyncTransforms();
        motor.ResetEncounter(); phase2Entered = false; PhaseTransitionCount = 0; ReleaseCount = 0; lastSkill = -1;
        entryCaptured = false; phase2RallyPending = false; EntryPower = SummonHealthScale = 1; Pressure.Reset();
        for (int i = 0; i < SkillReleases.Length; i++) { SkillReleases[i] = 0; lastUsed[i] = 0; }
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
