using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Real Editor Play Mode integration checks; no alternate damage or fake combat implementations.</summary>
[InitializeOnLoad]
public static class HiveTyrantCombatVerification
{
    private const string RunningKey = "HiveTyrant.VerificationRunning";
    private const string BatchKey = "HiveTyrant.VerificationBatch";
    private static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
    private static readonly List<string> results = new List<string>();
    private static readonly List<string> runtimeErrors = new List<string>();
    private static readonly List<string> knownEditorDiagnostics = new List<string>();
    private static double startTime;
    private static bool finished;
    private static int exitCode;
    static HiveTyrantCombatVerification()
    {
        EditorApplication.playModeStateChanged += StateChanged;
        EditorApplication.update += Tick;
        Application.logMessageReceived += Log;
    }
    [MenuItem("AlienPurge/Hive Tyrant/Run Play Mode Verification")]
    public static void RunInteractive() { Begin(false); }
    public static void RunBatch() { Begin(true); }
    public static void RunRepairRegression() { SessionState.SetString("HiveTyrant.VerificationMode", "RepairRegression"); Begin(true); }
    public static void RunPolishRegression() { SessionState.SetString("HiveTyrant.VerificationMode", "PolishRegression"); Begin(true); }
    private static string OutputFolder(string mode) => mode.StartsWith("Polish") ? "Docs/Boss/CombatPolishValidation/Regression" : mode.StartsWith("Repair") ? "Docs/Boss/RepairValidation/Regression" : "Docs/Boss/CompletionValidation";
    public static void RunVisualSample() { SessionState.SetString("HiveTyrant.VerificationMode", "VisualSample"); Begin(true); }
    public static void ConfigureAndRunVisualSample() { HiveTyrantCombatSetup.ConfigureAndValidate(); RunVisualSample(); }
    public static void RunPoseAtlas() { SessionState.SetString("HiveTyrant.VerificationMode", "PoseAtlas"); Begin(true); }
    public static void RunBattle() { SessionState.SetString("HiveTyrant.VerificationMode", "Battle"); Begin(true); }
    public static void RunNativeEnemies() { SessionState.SetString("HiveTyrant.VerificationMode", "NativeEnemies"); Begin(true); }
    public static void RunPolicyAndStress() { SessionState.SetString("HiveTyrant.VerificationMode", "PolicyAndStress"); Begin(true); }
    public static void ConfigureAndRunPoseAtlas() { HiveTyrantCombatSetup.ConfigureAndValidate(); RunPoseAtlas(); }
    public static void RunCompletionPass() { HiveTyrantCombatSetup.ConfigureAndValidate(); SessionState.SetString("HiveTyrant.VerificationMode", "CompletionPass"); Begin(true); }
    public static void RunFinalStages() { SessionState.SetString("HiveTyrant.VerificationMode", "FinalStages"); Begin(true); }
    public static void ConfigureAndRunFinalStages() { HiveTyrantCombatSetup.ConfigureAndValidate(); RunFinalStages(); }
    public static void RunSummonBaseline() { SessionState.SetBool("HiveTyrant.SummonBaseline", true); Begin(true); }
    private static void Begin(bool batch)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        EditorSceneManager.OpenScene(HiveTyrantCombatSetup.TestScene);
        SessionState.SetBool(RunningKey, true); SessionState.SetBool(BatchKey, batch);
        EditorApplication.isPlaying = true;
    }
    private static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(RunningKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            results.Clear(); routines.Clear(); finished = false;
            startTime = EditorApplication.timeSinceStartup;
            Application.runInBackground = true;
            Debug.Log("[HiveTyrantVerification] Entered actual Play Mode, starting integration checks.");
            routines.Push(Verify());
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            if (runtimeErrors.Count > 0)
            {
                SessionState.SetInt("HiveTyrant.VerificationExitCode", 1);
                string mode = SessionState.GetString("HiveTyrant.CompletedMode", "PlayMode");
                File.AppendAllText(OutputFolder(mode) + "/" + mode + "_Result.txt", "\nFAILED: runtime/teardown errors:\n" + string.Join("\n", runtimeErrors));
            }
            SessionState.SetBool(RunningKey, false);
            if (SessionState.GetBool(BatchKey, false)) EditorApplication.Exit(SessionState.GetInt("HiveTyrant.VerificationExitCode", 1));
        }
    }
    private static void Tick()
    {
        if (SessionState.GetBool(RunningKey, false) && EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
        // Batch-mode domain reload can deliver EnteredPlayMode before static hooks are restored.
        if (SessionState.GetBool(RunningKey, false) && EditorApplication.isPlaying && routines.Count == 0 && !finished)
            StateChanged(PlayModeStateChange.EnteredPlayMode);
        if (!EditorApplication.isPlaying || !SessionState.GetBool(RunningKey, false) || finished || routines.Count == 0) return;
        try
        {
            if (EditorApplication.isPaused)
            {
                if (runtimeErrors.Count > 0 || knownEditorDiagnostics.Count == 0)
                    throw new InvalidOperationException("Editor paused during verification; inspect startup/runtime errors.");
                // The pre-existing A* HTTP update checker trips Console Error Pause.
                // Resume only that known Editor diagnostic; all gameplay errors remain fatal.
                results.Add("RESUMED existing A* Editor update-check pause; diagnostic retained below.");
                EditorApplication.isPaused = false;
                return;
            }
            if (EditorApplication.timeSinceStartup - startTime > 600) throw new TimeoutException("Play Mode verification exceeded 600 seconds.");
            var current = routines.Peek();
            if (!current.MoveNext())
            {
                routines.Pop();
                if (routines.Count == 0) Complete(true);
                return;
            }
            if (current.Current is IEnumerator nested) routines.Push(nested);
        }
        catch (Exception exception) { results.Add("FAIL " + exception); Complete(false); }
    }
    private static void Log(string message, string stack, LogType type)
    {
        if (SessionState.GetBool(RunningKey, false) && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
        {
            if (stack.Contains("AstarUpdateChecker")) knownEditorDiagnostics.Add(message);
            else runtimeErrors.Add(message + "\n" + stack);
        }
    }
    private static void Complete(bool success)
    {
        finished = true;
        if (runtimeErrors.Count > 0) { success = false; results.Add("RUNTIME ERRORS:\n" + string.Join("\n", runtimeErrors)); }
        results.Insert(0, (success ? "PASSED" : "FAILED") + " - Unity " + Application.unityVersion + " actual Editor Play Mode; " + SystemInfo.graphicsDeviceType);
        results.Add("Automated checks are distinct from visual review and long-term balance acceptance.");
        if (knownEditorDiagnostics.Count > 0) results.Add("Existing unrelated A* Editor update checker diagnostics (also present before changes): " + string.Join(" | ", knownEditorDiagnostics));
        Directory.CreateDirectory("Docs/Boss/CompletionValidation");
        string mode = SessionState.GetString("HiveTyrant.VerificationMode", "PlayMode");
        SessionState.SetString("HiveTyrant.CompletedMode", mode);
        Directory.CreateDirectory(OutputFolder(mode));
        File.WriteAllLines(OutputFolder(mode) + "/" + mode + "_Result.txt", results);
        SessionState.SetString("HiveTyrant.VerificationMode", "PlayMode");
        exitCode = success ? 0 : 1;
        SessionState.SetInt("HiveTyrant.VerificationExitCode", exitCode);
        Debug.Log("[HiveTyrantVerification] " + results[0]);
        EditorApplication.isPlaying = false;
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        results.Add("PASS " + message); Debug.Log("[HiveTyrantVerification] PASS " + message);
    }
    private static IEnumerator Delay(float seconds)
    { float until = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < until) yield return null; }
    private static IEnumerator Idle(HiveTyrantCombat boss)
    { float until = Time.realtimeSinceStartup + 15; while (boss != null && boss.Motor.HasActionOwner && Time.realtimeSinceStartup < until) yield return null; Check(boss != null && !boss.Motor.HasActionOwner, "Action releases ownership after recovery"); }
    private static void Place(PlayerHealth player, Vector2 position)
    {
        player.transform.position = position;
        player.GetComponent<Rigidbody2D>().position = position;
        player.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
        Physics2D.SyncTransforms();
    }
    private static IEnumerator Verify()
    {
        yield return Delay(1.5f);
        if (SessionState.GetString("HiveTyrant.VerificationMode", "PlayMode") == "PolicyAndStress")
        { yield return SummonPolicies(); yield return RepeatStress(); yield break; }
        if (SessionState.GetString("HiveTyrant.VerificationMode", "PlayMode") == "FinalStages")
        { yield return FinalStages(); yield break; }
        if (SessionState.GetString("HiveTyrant.VerificationMode", "PlayMode") == "VisualSample")
        { yield return VisualSample(); yield break; }
        if (SessionState.GetString("HiveTyrant.VerificationMode", "PlayMode") == "PoseAtlas")
        { yield return PoseAtlas(); HiveTyrantCombatDebug.RestartScene(); yield return Delay(1.5f); yield return VisualSample(); yield break; }
        if (SessionState.GetString("HiveTyrant.VerificationMode", "PlayMode") == "Battle")
        { yield return Battle(); yield break; }
        if (SessionState.GetString("HiveTyrant.VerificationMode", "PlayMode") == "NativeEnemies")
        { yield return NativeEnemyReuse(UnityEngine.Object.FindObjectOfType<HiveTyrantCombat>(), UnityEngine.Object.FindObjectOfType<PlayerHealth>()); yield break; }
        if (SessionState.GetBool("HiveTyrant.SummonBaseline", false))
        {
            yield return SummonBaseline();
            SessionState.SetBool("HiveTyrant.SummonBaseline", false);
            yield break;
        }
        HiveTyrantCombat boss = UnityEngine.Object.FindObjectOfType<HiveTyrantCombat>();
        PlayerHealth player = UnityEngine.Object.FindObjectOfType<PlayerHealth>();
        Check(boss != null && player != null && boss.Config != null, "Dedicated scene has usable Boss, Player and config");
        boss.AutomaticSkills = false;
        player.GetComponent<PlayerMovement>().SetCanMove(false);
        Vector2 origin = boss.Motor.CombatOrigin;
        Place(player, origin + Vector2.down * 5);
        Check(boss.Health.CurrentHealth == boss.Config.maxHealth, "Boss uses EnemyHealth at configured initial HP");
        Check(boss.Config.sweepRadius == 7 && boss.Config.sweepAngle == 120 &&
              HiveTyrantCombat.ContainsPoint(origin, Vector2.down, origin + Vector2.down * 6.9f, 7, 120) &&
              !HiveTyrantCombat.ContainsPoint(origin, Vector2.down, origin + Vector2.down * 7.1f, 7, 120) &&
              !HiveTyrantCombat.ContainsPoint(origin, Vector2.down, origin + Vector2.up * 5, 7, 120), "Enlarged 7 / 120 sector includes new range, excludes outside/rear");

        boss.Motor.BeginAction(1, Vector2.down);
        yield return Delay(.2f);
        for (int d = 0; d < 8; d++)
        {
            float angle = (-90 + d * 45) * Mathf.Deg2Rad;
            float before = Mathf.Repeat(boss.Motor.BodyAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1);
            boss.Motor.Face(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
            boss.Motor.BodyAnimator.Update(0);
            float after = Mathf.Repeat(boss.Motor.BodyAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1);
            Check(boss.Motor.GetCurrentDirection() == d && Mathf.Abs(Mathf.DeltaAngle(before * 360, after * 360)) < 3,
                "Eight-direction mapping/gait preserved: " + d);
            yield return Delay(.04f);
        }
        boss.Motor.EndAction();

        var extraCollider = player.gameObject.AddComponent<BoxCollider2D>(); extraCollider.isTrigger = true; extraCollider.size = Vector2.one * .2f;
        int hp = player.CurrentHealth;
        Place(player, origin + Vector2.down * 5);
        Check(boss.RequestSkill(HiveTyrantSkill.Sweep, true), "Sweep starts with explicit action ownership");
        Vector2 lockedPosition = boss.transform.position;
        yield return Delay(boss.Config.sweepWindup * .5f);
        Check(player.CurrentHealth == hp && boss.ActiveWarningCount == 1 && Vector2.Distance(lockedPosition, boss.transform.position) < .01f, "Sweep warning causes no early damage; boss stops moving");
        Capture("Sweep_Warning");
        yield return Delay(boss.Config.sweepWindup * .5f + .3f);
        Check(player.CurrentHealth == hp - boss.Config.sweepDamage, "Sweep hits once despite multiple player colliders");
        yield return Idle(boss);
        UnityEngine.Object.Destroy(extraCollider);
        hp = player.CurrentHealth;
        Place(player, origin + Vector2.down * 5);
        Check(boss.RequestSkill(HiveTyrantSkill.Sweep, true), "Second sweep can start after recovery");
        yield return Delay(.2f);
        Place(player, origin + Vector2.up * 5);
        yield return Delay(boss.Config.sweepWindup + .25f);
        Check(player.CurrentHealth == hp, "Locked sweep can be dodged behind the boss without an untelegraphed rear hit");
        yield return Idle(boss);

        yield return WaveRules(boss, player);
        yield return NativeEnemyReuse(boss, player);
        boss.RestartEncounter(); player.RestoreHealth(player.MaxHealth);
        origin = boss.Motor.CombatOrigin;

        // Existing player weapon and actual pooled Bullet collision path.
        Place(player, origin + Vector2.down * 5); int bossHp = boss.Health.CurrentHealth;
        WeaponManager weapon = player.GetComponent<WeaponManager>();
        weapon.AimAt(origin); Check(weapon.TryFire(), "Existing Player weapon fires using its configured BulletPool");
        yield return Delay(.5f);
        Check(boss.Health.CurrentHealth < bossHp, "Actual weapon projectile damages the original Boss EnemyHealth");
        yield return DamageChains(boss, player);

        // Summon cap, finite budget, ownership and pool reuse.
        Place(player, origin + Vector2.down * 11);
        Check(boss.RequestSkill(HiveTyrantSkill.Summon, true), "Summon starts through the existing EnemyPool");
        Check(boss.PendingSpawnCount > 0 && boss.PendingSpawnCount <= boss.Config.summonWaveSize, "Summon selects finite warned valid positions");
        Capture("Summon_Warning");
        yield return Delay(boss.Config.summonWarning + .15f);
        Check(boss.LivingSummons > 0 && boss.TotalSummoned <= boss.Config.summonBudget, "Summons spawned within wave/budget limits");
        int total = boss.TotalSummoned;
        yield return Idle(boss);
        Check(!boss.RequestSkill(HiveTyrantSkill.Summon), "Natural summon request respects minimum interval");
        Check(boss.RequestSkill(HiveTyrantSkill.Summon, true), "Debug second wave exercises hard alive cap independently of cooldown");
        yield return Delay(boss.Config.summonWarning + .15f);
        Check(boss.LivingSummons == boss.Config.summonAliveCap && !boss.RequestSkill(HiveTyrantSkill.Summon, true), "Two waves fill the alive cap without exceeding it");
        total = boss.TotalSummoned;
        yield return Idle(boss);
        var unrelated = EnemyPool.Instance.GetEnemy(EnemyType.Normal, origin + new Vector2(12, 8), Quaternion.identity);
        Check(unrelated != null, "Unrelated ordinary enemy obtained from existing pool");
        unrelated.GetComponent<EnemyDefinition>().InitializeFromGlobalDifficulty(4, 2, 5);
        boss.enabled = false;
        Check(boss.PendingSpawnCount == 0 && boss.ActiveWarningCount == 0 && boss.LivingSummons == 0 && !unrelated.IsReturned, "Disable cancels only Boss warnings/pending/owned summons; unrelated enemy survives");
        boss.enabled = true;
        // Exercise ordinary EnemyHealth reward and pool death, then re-spawn the same type.
        int kills = new SerializedObject(GameManager.Instance).FindProperty("killCount").intValue;
        int orbs = ExperienceOrbPool.Instance.ActiveCount;
        var ordinaryHealth = unrelated.GetComponent<EnemyHealth>(); ordinaryHealth.TakeDamage(ordinaryHealth.CurrentHealth);
        yield return Delay(.35f);
        Check(unrelated.IsReturned && new SerializedObject(GameManager.Instance).FindProperty("killCount").intValue == kills + 1,
            "Ordinary EnemyHealth death still returns to pool and registers exactly one kill");
        Check(ExperienceOrbPool.Instance.ActiveCount == orbs + 1, "Ordinary death still drops one experience orb through the existing pool");
        var reused = EnemyPool.Instance.GetEnemy(EnemyType.Normal, origin + new Vector2(12, 8), Quaternion.identity);
        Check(!reused.GetComponent<EnemyHealth>().IsDead && reused.GetComponent<Collider2D>().enabled, "Ordinary pooled re-spawn restores health and colliders");
        reused.ReturnToPool();
        // A real temporary obstacle rejects every candidate. Remove the fixture before physics advances.
        // Native enemies can push this stationary test player out of engagement range.
        // Restore the fixture precondition so this tests blocked spawns, not disengagement.
        Place(player, boss.Motor.CombatOrigin + Vector2.down * 10);
        Check(boss.TargetInArena && !player.IsDead && !boss.Motor.HasActionOwner,
            "Blocked-spawn fixture starts with a living player inside engagement range");
        var blocker = new GameObject("Verification_AllSummonPointsBlocked"); blocker.layer = LayerMask.NameToLayer("Environment");
        blocker.transform.position = boss.Motor.CombatOrigin; blocker.AddComponent<CircleCollider2D>().radius = 14;
        Physics2D.SyncTransforms();
        bool blockedRequest = boss.RequestSkill(HiveTyrantSkill.Summon, true);
        results.Add("Blocked-spawn fixture: request=" + blockedRequest + " pending=" + boss.PendingSpawnCount + " state=" + boss.State +
            " owner=" + boss.Motor.HasActionOwner + " locked=" + boss.Motor.IsMovementLocked + " inArena=" + boss.TargetInArena +
            " boss=" + boss.Motor.CombatOrigin + " player=" + player.transform.position + " alive=" + boss.LivingSummons + " budget=" + boss.TotalSummoned + " playerHP=" + player.CurrentHealth);
        Check(blockedRequest && boss.PendingSpawnCount == 0, "Fully blocked spawn search skips every invalid point");
        blocker.SetActive(false); UnityEngine.Object.Destroy(blocker); Physics2D.SyncTransforms();
        yield return Idle(boss);
        Check(boss.TotalSummoned == total, "No-valid-point summon completes without spending budget or stalling");
        while (boss.TotalSummoned < boss.Config.summonBudget)
        {
            Place(player, origin + Vector2.down * 11); player.RestoreHealth(player.MaxHealth);
            Check(boss.RequestSkill(HiveTyrantSkill.Summon, true), "Additional budgeted summon starts");
            yield return Delay(boss.Config.summonWarning + .15f);
            Check(boss.TotalSummoned > total && boss.LivingSummons <= boss.Config.summonAliveCap, "Summon progresses without exceeding alive cap");
            total = boss.TotalSummoned; boss.enabled = false; boss.enabled = true;
        }
        Check(boss.TotalSummoned == boss.Config.summonBudget && !boss.RequestSkill(HiveTyrantSkill.Summon, true), "Exhausted encounter budget blocks further summons");

        // Phase2 cancels a live warning and does not refill HP.
        boss.RestartEncounter(); player.RestoreHealth(player.MaxHealth); Place(player, origin + Vector2.down * 5);
        Check(boss.RequestSkill(HiveTyrantSkill.Sweep, true), "Pre-Phase2 sweep starts");
        int releases = boss.ReleaseCount;
        boss.Health.TakeDamage(boss.Health.CurrentHealth - Mathf.FloorToInt(boss.Health.MaxHealth * boss.Config.phase2HealthRatio));
        int thresholdHealth = boss.Health.CurrentHealth;
        yield return Delay(.1f);
        Check(boss.State == HiveTyrantState.PhaseTransition && boss.PhaseTransitionCount == 1 && boss.PendingSpawnCount == 0, "Threshold enters Phase2 once and interrupts pending skill");
        Capture("Phase2_RiftWing");
        yield return Idle(boss);
        Check(boss.Health.CurrentHealth == thresholdHealth && boss.ReleaseCount == releases, "Phase2 preserves damaged health and cancels old release");
        boss.Health.TakeDamage(1); yield return Delay(.1f);
        Check(boss.PhaseTransitionCount == 1, "Further low-health hits do not repeat Phase2 transition");
        hp = player.CurrentHealth;
        Check(boss.RequestSkill(HiveTyrantSkill.Sweep, true), "Phase2 double sweep starts");
        yield return Delay(boss.Config.sweepWindup + .25f);
        Check(boss.ReleaseCount == releases + 1 && player.CurrentHealth == hp - boss.Config.sweepDamage, "Phase2 first sweep resolves normally");
        yield return Delay(boss.Config.sweepRecovery + .1f);
        Check(boss.ActiveWarningCount == 1 && boss.State == HiveTyrantState.SweepWindup, "Phase2 second sweep has its own visible reaction window");
        yield return Idle(boss);
        Check(boss.ReleaseCount == releases + 2 && player.CurrentHealth == hp - boss.Config.sweepDamage * 2, "Phase2 double sweep hits once per separately warned sweep");

        // Highest priority death interrupts a not-yet-released slam; no reset of phase.
        Check(boss.RequestSkill(HiveTyrantSkill.Slam, true), "Death-interrupt slam starts");
        releases = boss.ReleaseCount; lockedPosition = boss.GetComponent<Rigidbody2D>().position;
        boss.Health.TakeDamage(boss.Health.CurrentHealth);
        Check(boss.Motor.IsDead && boss.State == HiveTyrantState.Dead && boss.ActiveWarningCount == 0, "Lethal damage immediately cancels warnings and locks death action");
        float deathDuration = boss.DeathPresentationDuration;
        yield return Delay(.4f);
        Check(boss != null && boss.ReleaseCount == releases && Vector2.Distance(boss.GetComponent<Rigidbody2D>().position, lockedPosition) < .001f &&
            boss.GetComponent<HiveTyrantPresentation>().IsArticulated && boss.GetComponent<HiveTyrantPresentation>().CurrentProfile >= 8,
            "Full death presentation is retained, without postmortem movement or attack");
        Capture("Death");
        yield return Delay(deathDuration + .2f);
        Check(boss == null, "Boss is cleaned up only after complete death duration");

        // Recreate through the same configured prefab and test lethal Phase1 skip.
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HiveTyrantCombatSetup.BossPath);
        var fresh = UnityEngine.Object.Instantiate(prefab).GetComponent<HiveTyrantCombat>();
        fresh.AutomaticSkills = false; yield return Delay(.1f);
        Check(fresh.Health.CurrentHealth == fresh.Config.maxHealth && !fresh.Phase2Entered && fresh.TotalSummoned == 0, "New encounter starts with clean health, phase and summon budget");
        fresh.Health.TakeDamage(fresh.Health.CurrentHealth); yield return Delay(.1f);
        Check(fresh.Health.IsDead && !fresh.Phase2Entered && fresh.PhaseTransitionCount == 0, "Direct lethal Phase1 damage skips Phase2 conversion");
        yield return MovementAndSceneRestart();
        yield return MainSceneIntegration();
        Check(runtimeErrors.Count == 0, "No runtime errors/exceptions in tested combat paths");
        if (SessionState.GetString("HiveTyrant.VerificationMode", "PlayMode") == "CompletionPass")
        {
            EditorSceneManager.LoadSceneInPlayMode(HiveTyrantCombatSetup.TestScene, new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            yield return Delay(1.5f); yield return PoseAtlas();
            HiveTyrantCombatDebug.RestartScene(); yield return Delay(1.5f); yield return VisualSample();
            HiveTyrantCombatDebug.RestartScene(); yield return Delay(1.5f); yield return Battle();
            yield return RepeatStress();
        }
        if (SessionState.GetString("HiveTyrant.VerificationMode", "PlayMode").EndsWith("Regression"))
        {
            EditorSceneManager.LoadSceneInPlayMode(HiveTyrantCombatSetup.TestScene, new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            yield return Delay(1.5f); yield return Battle(); yield return RepeatStress();
        }
    }
    private static IEnumerator WaveRules(HiveTyrantCombat boss, PlayerHealth player)
    {
        var pilot = player.gameObject.AddComponent<HiveTyrantPlaytestPilot>();
        for (int distanceIndex = 0; distanceIndex < 3; distanceIndex++) for (int gapIndex = 0; gapIndex < 2; gapIndex++)
        {
            boss.RestartEncounter(); player.RestoreHealth(player.MaxHealth);
            float distance = new[] { 5f, 9f, 14f }[distanceIndex];
            Place(player, boss.Motor.CombatOrigin + Vector2.down * distance);
            pilot.Destination = player.transform.position;
            player.GetComponent<PlayerMovement>().SetCanMove(true);
            int hp = player.CurrentHealth;
            Check(boss.RequestSkill(HiveTyrantSkill.Slam, true), "Wave has a reachable route at distance " + distance);
            var plan = boss.Wave.Plan;
            Check(plan.gap >= HiveTyrantGeometry.PlayerWidth(player, plan.right) * 1.8f + .34f && plan.reachableGaps[gapIndex], "Gap " + gapIndex + " accommodates actual collider and clear walking route");
            float z = Vector2.Dot((Vector2)player.transform.position - plan.origin, plan.forward);
            Vector2 start = player.transform.position;
            yield return Delay(.25f); // Human-scale reaction delay; no invulnerability or movement changes.
            pilot.Destination = plan.Point(plan.GapCenter(gapIndex), z);
            if (distanceIndex == 1 && gapIndex == 0) Capture("Wave_Warning");
            yield return Delay(boss.Config.slamWindup + .2f);
            if (distanceIndex == 1 && gapIndex == 0) Capture("Wave_Travelling");
            yield return Idle(boss);
            while (boss.Wave.IsTravelling) yield return null;
            Check(Vector2.Distance(player.transform.position, pilot.Destination) < .2f && player.CurrentHealth == hp && !player.IsTemporarilyInvulnerable,
                "Native walking speed " + player.GetComponent<PlayerMovement>().MoveSpeed + " safely reaches gap " + gapIndex + " from " + distance + " (no damage/invulnerability)");
            Check(Vector2.Distance(start, player.transform.position) > 1, "Gap trial includes real player physics displacement");
        }
        UnityEngine.Object.Destroy(pilot); player.GetComponent<PlayerMovement>().SetCanMove(false);
        boss.RestartEncounter(); player.RestoreHealth(player.MaxHealth);
        Place(player, boss.Motor.CombatOrigin + Vector2.down * 10);
        int health = player.CurrentHealth;
        Check(boss.RequestSkill(HiveTyrantSkill.Slam, true), "Central travelling wave starts at extended targeting range");
        var locked = boss.Wave.Plan;
        yield return Delay(boss.Config.slamWindup + .12f);
        Check(player.CurrentHealth == health && boss.Wave.IsTravelling, "No damage before the moving front arrives");
        yield return Delay(.8f);
        Check(player.CurrentHealth == health - boss.Config.slamDamage && boss.Wave.HitCount == 1, "Moving dangerous segment hits exactly once on arrival");
        yield return Idle(boss);
        Check(boss.Wave.IsTravelling, "Wave survives body release and recovery instead of using old 0.22-second lifetime");
        while (boss.Wave.IsTravelling) yield return null;
        Check(player.CurrentHealth == health - boss.Config.slamDamage, "Passed front leaves no lingering damage");

        boss.RestartEncounter(); boss.Motor.SetMovementLocked(true);
        for (int gap = 0; gap < 2; gap++) for (int edge = -1; edge <= 1; edge += 2) for (int outside = 0; outside < 2; outside++)
        {
            player.RestoreHealth(player.MaxHealth); health = player.CurrentHealth;
            Place(player, boss.Motor.CombatOrigin + Vector2.down * 10);
            Check(HiveTyrantWavePlan.TryCreate(boss, out var plan), "Boundary fixture creates valid shared geometry");
            float radius = HiveTyrantGeometry.PlayerWidth(player, plan.right) * .5f;
            float x = plan.GapCenter(gap) + edge * (plan.gap * .5f - radius + (outside == 0 ? -.04f : .04f));
            Place(player, plan.Point(x, 6));
            boss.Wave.Prepare(plan); boss.Wave.Launch(); boss.Wave.Advance(2);
            Check(player.CurrentHealth == health - (outside == 0 ? 0 : boss.Config.slamDamage),
                "Swept 2-second step: gap " + gap + " edge " + edge + (outside == 0 ? " full collider fits safely" : " exposed collider enters real danger"));
            boss.Wave.Cancel();
        }
        player.RestoreHealth(player.MaxHealth); Place(player, boss.Motor.CombatOrigin + Vector2.down * 10);
        Check(HiveTyrantWavePlan.TryCreate(boss, out var sweepPlan), "Continuous crossing fixture plan");
        Place(player, sweepPlan.Point(-7, 5)); boss.Wave.Prepare(sweepPlan); boss.Wave.Launch();
        Place(player, sweepPlan.Point(7, 5)); health = player.CurrentHealth; boss.Wave.Advance(1.25f);
        Check(player.CurrentHealth == health - boss.Config.slamDamage && boss.Wave.HitCount == 1, "Relative sweep detects player/front crossing between frames without duplicate damage");
        boss.Wave.Cancel();
        player.RestoreHealth(player.MaxHealth); Place(player, boss.Motor.CombatOrigin + Vector2.down * 10);
        Check(HiveTyrantWavePlan.TryCreate(boss, out var airbornePlan), "Existing airborne-rule fixture plan");
        var abilityState = player.GetComponent<PlayerAbilityState>();
        Check(abilityState.TryEnterAbility(PlayerAbilityMode.Airborne), "Native ability state accepts airborne mode");
        health = player.CurrentHealth; boss.Wave.Prepare(airbornePlan); boss.Wave.Launch(); boss.Wave.Advance(2);
        Check(player.CurrentHealth == health && boss.Wave.HitCount == 0, "Ground wave honors the existing airborne exclusion without adding invulnerability");
        abilityState.ExitAbility(PlayerAbilityMode.Airborne); boss.Wave.Cancel();
        Place(player, boss.Motor.CombatOrigin + Vector2.down * 5);
        var wall = new GameObject("Verification_WaveWall"); wall.layer = LayerMask.NameToLayer("Environment");
        wall.transform.position = boss.Motor.CombatOrigin + Vector2.down * 9;
        wall.AddComponent<BoxCollider2D>().size = new Vector2(20, .4f); Physics2D.SyncTransforms();
        Check(HiveTyrantWavePlan.TryCreate(boss, out var clipped), "Wall allows a near safe escape and clips all downstream segments");
        Place(player, boss.Motor.CombatOrigin + Vector2.down * 12); health = player.CurrentHealth;
        boss.Wave.Prepare(clipped); boss.Wave.Launch(); boss.Wave.Advance(2);
        Check(clipped.MaximumLength < 6 && player.CurrentHealth == health, "Wall-clipped geometry cannot damage player behind wall");
        boss.Wave.Cancel(); wall.transform.position = boss.Motor.CombatOrigin + Vector2.down * 3.5f; Physics2D.SyncTransforms();
        Place(player, boss.Motor.CombatOrigin + Vector2.down * 5);
        Check(!HiveTyrantWavePlan.TryCreate(boss, out _), "Blocked launch / all sealed routes cancels wave planning");
        wall.SetActive(false); UnityEngine.Object.Destroy(wall); Physics2D.SyncTransforms();
        boss.Motor.SetMovementLocked(false);
        foreach (bool travel in new[] { false, true })
        {
            boss.RestartEncounter(); Place(player, boss.Motor.CombatOrigin + Vector2.down * 10);
            Check(boss.RequestSkill(HiveTyrantSkill.Slam, true), "Cancellation fixture starts " + (travel ? "travel" : "warning"));
            if (travel) yield return Delay(boss.Config.slamWindup + .1f);
            boss.enabled = false;
            Check(boss.Wave.Plan == null && !boss.Wave.IsTravelling && boss.ActiveWarningCount == 0, "Disable clears warning/travelling geometry and damage");
            boss.enabled = true;
        }
        boss.RestartEncounter(); player.RestoreHealth(player.MaxHealth);
    }
    private static IEnumerator NativeEnemyReuse(HiveTyrantCombat boss, PlayerHealth player)
    {
        boss.RestartEncounter(); boss.Motor.SetMovementLocked(true);
        Check(AstarPath.active != null && AstarPath.active.data.gridGraph != null, "Dedicated scene has scanned native A* navigation");
        for (int type = 0; type < 4; type++)
        {
            Check(EnemyPool.Instance.TryGetConfiguredPrefab((EnemyType)type, out var prefab), "Integrated native candidate " + (EnemyType)type);
            var choice = boss.Config.summonChoices.First(item => item.type == (EnemyType)type);
            PooledEnemy previous = null;
            for (int cycle = 0; cycle < 2; cycle++)
            {
                foreach (var oldProjectile in UnityEngine.Object.FindObjectsOfType<EnemyProjectile>()) oldProjectile.ReturnToPool();
                Place(player, boss.Motor.CombatOrigin + new Vector2(-9, -9)); player.RestoreHealth(player.MaxHealth);
                Vector2 spawn = (Vector2)player.transform.position + Vector2.right * 9;
                var enemy = EnemyPool.Instance.GetEnemy((EnemyType)type, spawn, Quaternion.identity);
                if (previous != null)
                {
                    int limit = EnemyPool.Instance.GetTotalCount((EnemyType)type) + 1;
                    while (enemy != previous && limit-- > 0) { enemy.ReturnToPool(); enemy = EnemyPool.Instance.GetEnemy((EnemyType)type, spawn, Quaternion.identity); }
                    Check(enemy == previous, "Exact pooled instance recycled for " + (EnemyType)type);
                }
                HiveTyrantSummoning.InitializeNative(enemy, prefab, choice);
                var lease = enemy.GetComponent<HiveTyrantSummonOwner>() ?? enemy.gameObject.AddComponent<HiveTyrantSummonOwner>(); lease.Claim(boss);
                var definition = enemy.GetComponent<EnemyDefinition>(); var move = enemy.GetComponent<EnemyMovement>(); var enemyHealth = enemy.GetComponent<EnemyHealth>();
                Check(definition.HasBeenInitialized && enemyHealth.CurrentHealth == definition.FinalMaxHealth && !move.IsMovementLocked && enemy.GetComponent<Rigidbody2D>().simulated,
                    "Clean native stats/locks/physics on " + (EnemyType)type + " cycle " + cycle);
                results.Add("STATS " + (EnemyType)type + " HP=" + definition.FinalMaxHealth + " speed=" + definition.FinalMoveSpeed + " contact=" + definition.FinalContactDamage + " clearance=" + HiveTyrantSummoning.Clearance(prefab));
                int hp = player.CurrentHealth; bool attack = false, projectile = false, hadPath = false; float until = Time.time + 11;
                while (Time.time < until && player.CurrentHealth == hp)
                {
                    Component ability = type == 1 ? (Component)enemy.GetComponent<FastChargeAttack>() : type == 2 ? (Component)enemy.GetComponent<HeavyShockwaveAttack>() : type == 3 ? (Component)enemy.GetComponent<RangedEnemyAttack>() : null;
                    if (ability != null)
                    {
                        object state = ability.GetType().GetField("currentState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(ability);
                        attack |= state.ToString() != "Idle";
                    }
                    projectile |= UnityEngine.Object.FindObjectsOfType<EnemyProjectile>().Any(item => item.gameObject.activeInHierarchy);
                    hadPath |= enemy.GetComponent<EnemyPathNavigator>().HasValidPath;
                    yield return null;
                }
                float moved = Vector2.Distance(spawn, enemy.transform.position);
                Check(moved > .3f && move.NavigationTarget == player.transform && hadPath,
                    "Native navigation moves " + (EnemyType)type + " cycle " + cycle + " by " + moved.ToString("F2") + " units");
                Check(player.CurrentHealth < hp && (type == 0 || attack) && (type != 3 || projectile),
                    "Native " + (EnemyType)type + " cycle " + cycle + " attacks player" + (type == 3 ? " with a real projectile" : ""));
                // Move the reward fixture away after attack evidence so XP remains measurable, without collecting it.
                enemy.transform.position = (Vector2)player.transform.position + Vector2.right * 9; enemy.GetComponent<Rigidbody2D>().position = enemy.transform.position;
                int kills = new SerializedObject(GameManager.Instance).FindProperty("killCount").intValue;
                int orbs = ExperienceOrbPool.Instance.ActiveCount;
                enemyHealth.TakeDamage(enemyHealth.CurrentHealth);
                float returnedBy = Time.time + 3; while (!enemy.IsReturned && Time.time < returnedBy) yield return null;
                Check(enemy.IsReturned && lease.Owner == null && new SerializedObject(GameManager.Instance).FindProperty("killCount").intValue == kills + 1 && ExperienceOrbPool.Instance.ActiveCount >= orbs + 1,
                    "Native " + (EnemyType)type + " death returns pool, clears owner and grants original kill/XP reward");
                previous = enemy;
            }
        }
        boss.RestartEncounter(); player.RestoreHealth(player.MaxHealth);
    }
    private static IEnumerator PoseAtlas()
    {
        var boss = UnityEngine.Object.FindObjectOfType<HiveTyrantCombat>(); var player = UnityEngine.Object.FindObjectOfType<PlayerHealth>();
        boss.AutomaticSkills = false; boss.Motor.SetMovementLocked(true); player.GetComponent<PlayerMovement>().SetCanMove(false);
        Place(player, boss.Motor.CombatOrigin + Vector2.down * 13);
        var camera = Camera.main; camera.GetComponent<CameraFollow>().enabled = false; camera.GetComponent<HiveTyrantArenaCamera>().enabled = false;
        camera.orthographicSize = 6.5f; camera.transform.position = boss.transform.position + new Vector3(0, 1.4f, -10);
        var pose = boss.GetComponent<HiveTyrantPresentation>(); pose.enabled = false;
        Directory.CreateDirectory("Docs/Boss/CompletionValidation/PoseAtlases");
        int[] cues = { 6, 7, 8, 6, 7, 8, 9, 2, 3 };
        for (int row = 0; row < cues.Length; row++) for (int key = 0; key < 3; key++)
        {
            var atlas = new Texture2D(2048, 1024, TextureFormat.RGB24, false);
            var render = new RenderTexture(512, 512, 24); var previous = camera.targetTexture; var active = RenderTexture.active;
            for (int direction = 0; direction < 8; direction++)
            {
                pose.SampleForReview(cues[row], direction, row >= 3, new[] { .9f, 1.22f, 1.8f }[key]);
                camera.targetTexture = render; camera.Render(); RenderTexture.active = render;
                atlas.ReadPixels(new Rect(0, 0, 512, 512), direction % 4 * 512, (1 - direction / 4) * 512);
            }
            atlas.Apply(); File.WriteAllBytes("Docs/Boss/CompletionValidation/PoseAtlases/" + row + "_Cue" + cues[row] + "_Key" + key + ".png", atlas.EncodeToPNG());
            camera.targetTexture = previous; RenderTexture.active = active; render.Release(); UnityEngine.Object.Destroy(render); UnityEngine.Object.Destroy(atlas);
            yield return null;
        }
        results.Add("27 runtime-rendered pose atlases: 9 action/phase rows x 3 keys x 8 authored views. D DR R UR / U UL L DL. Controlled pose/camera review, distinct from real-time cast captures.");
    }
    private static IEnumerator Battle()
    {
        var boss = UnityEngine.Object.FindObjectOfType<HiveTyrantCombat>(); var player = UnityEngine.Object.FindObjectOfType<PlayerHealth>();
        var pilot = player.gameObject.AddComponent<HiveTyrantPlaytestPilot>(); pilot.Boss = boss; pilot.Battle = true;
        boss.AutomaticSkills = true;
        Check(player.MaxHealth == 5 && player.GetComponent<PlayerMovement>().MoveSpeed == 5 && !player.IsTemporarilyInvulnerable, "Battle uses original 5 HP / speed 5 player and weapon, no healing/invulnerability/damage cheats");
        float begin = Time.time, nextRecord = begin; int phaseCount = 0, summons = 0, releases = 0; var frames = new List<float>(15000);
        var states = new HashSet<HiveTyrantState>();
        while (boss != null && !boss.Health.IsDead && !player.IsDead && Time.time < begin + 180)
        {
            states.Add(boss.State); phaseCount = boss.PhaseTransitionCount; summons = boss.TotalSummoned; releases = boss.ReleaseCount;
            frames.Add(Time.unscaledDeltaTime * 1000);
            if (Time.time >= nextRecord)
            {
                results.Add("BATTLE t=" + (Time.time - begin).ToString("F1") + " player=" + player.CurrentHealth + " boss=" + boss.Health.CurrentHealth + " phase=" + (boss.Phase2Entered ? 2 : 1) + " summons=" + summons + " releases=" + releases);
                Capture("Battle/At_" + Mathf.RoundToInt(Time.time - begin).ToString("D3")); nextRecord = Time.time + 10;
            }
            yield return null;
        }
        frames.Sort();
        results.Add("MEASURED " + SystemInfo.processorType + "; " + SystemInfo.graphicsDeviceName + "; batch DX11 1280x720 captures; frames=" + frames.Count + "; median ms=" + frames[frames.Count / 2].ToString("F2") + "; p95 ms=" + frames[(int)(frames.Count * .95f)].ToString("F2") + "; managed bytes=" + GC.GetTotalMemory(false));
        results.Add("BATTLE outcome t=" + (Time.time - begin).ToString("F1") + " playerHP=" + player.CurrentHealth + " shots=" + pilot.Shots + " phases=" + phaseCount + " summons=" + summons + " releases=" + releases);
        Check(!player.IsDead && (boss == null || boss.Health.IsDead), "Normal-parameter automated playthrough defeats Boss without healing or debug damage");
        Check(phaseCount == 1 && states.Contains(HiveTyrantState.SweepWindup) && states.Contains(HiveTyrantState.SlamWindup) && states.Contains(HiveTyrantState.SummonWindup), "Full automatic encounter exercises all skills and both phases");
        yield return Delay(2);
    }
    private static IEnumerator RepeatStress()
    {
        int baselineMeshes = -1, baselineMaterials = -1;
        for (int round = 0; round < 3; round++)
        {
            HiveTyrantCombatDebug.RestartScene(); yield return Delay(1.5f);
            var boss = UnityEngine.Object.FindObjectOfType<HiveTyrantCombat>(); var player = UnityEngine.Object.FindObjectOfType<PlayerHealth>();
            boss.AutomaticSkills = false; player.GetComponent<PlayerMovement>().SetCanMove(false);
            foreach (var skill in new[] { HiveTyrantSkill.Sweep, HiveTyrantSkill.Slam, HiveTyrantSkill.Summon })
            {
                player.RestoreHealth(player.MaxHealth); Place(player, boss.Motor.CombatOrigin + Vector2.down * 10);
                Check(boss.RequestSkill(skill, true), "Repeated round " + round + " casts " + skill);
                yield return Idle(boss); boss.RestartEncounter(); yield return Delay(.55f);
            }
            int meshes = Resources.FindObjectsOfTypeAll<Mesh>().Count(mesh => mesh.name == "HiveTyrant local joint surface" || mesh.name.StartsWith("Release_") || mesh.name.StartsWith("GroundWave_") || mesh.name == "HiveTyrant pooled filled warning");
            int materials = Resources.FindObjectsOfTypeAll<Material>().Count(material => material.name == "HiveTyrant_VertexTint" || material.name == "HiveTyrant_ArticulatedBody");
            if (round == 0) { baselineMeshes = meshes; baselineMaterials = materials; }
            Check(meshes == baselineMeshes && materials == baselineMaterials && boss.ActiveWarningCount == 0 && boss.LivingSummons == 0 && boss.Wave.Plan == null && boss.GetComponent<HiveTyrantEffects>().ActiveCount == 0,
                "Round " + round + " reset leaves stable meshes=" + meshes + " materials=" + materials + ", no summons/warnings/waves/bursts");
            results.Add("MEMORY round=" + round + " managed=" + GC.GetTotalMemory(true) + " totalUnity=" + UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong());
        }
    }
    private static IEnumerator SummonPolicies()
    {
        var boss = UnityEngine.Object.FindObjectOfType<HiveTyrantCombat>(); var player = UnityEngine.Object.FindObjectOfType<PlayerHealth>();
        boss.AutomaticSkills = false; boss.Motor.SetMovementLocked(true); player.GetComponent<PlayerMovement>().SetCanMove(false);
        Place(player, boss.Motor.CombatOrigin + Vector2.down * 11);
        var original = boss.Config; var fixture = UnityEngine.Object.Instantiate(original);
        var configField = typeof(HiveTyrantCombat).GetField("config", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        configField.SetValue(boss, fixture);
        try
        {
            foreach (var choice in original.summonChoices)
            {
                boss.Summoning.ResetBudget(); fixture.summonChoices = new[] { new HiveTyrantSummonChoice { type = choice.type, weight = 1, aliveCap = choice.aliveCap } };
                for (int wave = 0; wave < 2; wave++) { boss.Summoning.Prepare(); boss.Summoning.Release(); }
                int expected = Mathf.Min(choice.aliveCap, fixture.summonAliveCap);
                Check(boss.Summoning.Count(choice.type) == expected && !boss.Summoning.CanSummon(), "Isolated candidate " + choice.type + " reaches configured per-type cap " + expected + " and exits selection");
                var leases = UnityEngine.Object.FindObjectsOfType<HiveTyrantSummonOwner>().Where(lease => lease.Owner == boss).ToArray();
                boss.enabled = false;
                Check(leases.All(lease => lease.Owner == null && lease.GetComponent<PooledEnemy>().IsReturned), "Boss disable clears every owned " + choice.type + " through native pool return");
                boss.enabled = true; boss.AutomaticSkills = false;
            }
            boss.Summoning.ResetBudget(); fixture.summonChoices = new[] { new HiveTyrantSummonChoice { type = EnemyType.Heavy, weight = 1000, aliveCap = 1 } };
            boss.Summoning.Prepare(); boss.Summoning.Release();
            fixture.summonChoices = new[] { fixture.summonChoices[0], new HiveTyrantSummonChoice { type = EnemyType.Normal, weight = 1, aliveCap = 5 } };
            boss.Summoning.Prepare(); boss.Summoning.Release();
            Check(boss.Summoning.Count(EnemyType.Heavy) == 1 && boss.Summoning.Count(EnemyType.Normal) > 0, "Capped high-weight Tank falls back to available Normal candidate without resampling loop");
            boss.Summoning.ResetBudget(); fixture.summonChoices = new[] { new HiveTyrantSummonChoice { type = (EnemyType)999, weight = 1, aliveCap = 1 } };
            Check(!boss.Summoning.CanSummon(), "Unavailable prefab candidate exits without spawning or consuming budget");
            fixture.summonChoices = new[] { new HiveTyrantSummonChoice { type = EnemyType.Normal, weight = 1, aliveCap = 5 } };
            boss.Summoning.Prepare(); int planned = boss.Summoning.PendingCount; var walls = new List<GameObject>();
            for (int i = 0; i < planned; i++)
            { var plan = boss.Summoning.Pending(i); var wall = new GameObject("Release recheck fixture"); wall.layer = LayerMask.NameToLayer("Environment"); wall.transform.position = plan.point; wall.AddComponent<CircleCollider2D>().radius = plan.clearance + .1f; walls.Add(wall); }
            Physics2D.SyncTransforms(); boss.Summoning.Release();
            Check(planned > 0 && boss.Summoning.Total == 0 && boss.Summoning.Living == 0, "Every telegraphed point is rechecked at release; newly blocked points spend no budget");
            foreach (var wall in walls) { wall.SetActive(false); UnityEngine.Object.Destroy(wall); } Physics2D.SyncTransforms();
            boss.Summoning.Prepare(); boss.Summoning.Release();
            var leaseToReuse = UnityEngine.Object.FindObjectsOfType<HiveTyrantSummonOwner>().First(lease => lease.Owner == boss);
            var old = leaseToReuse.GetComponent<PooledEnemy>(); old.ReturnToPool();
            var reused = EnemyPool.Instance.GetEnemy(EnemyType.Normal, boss.Motor.CombatOrigin + Vector2.right * 12, Quaternion.identity);
            int limit = EnemyPool.Instance.GetTotalCount(EnemyType.Normal) + 1;
            while (reused != old && limit-- > 0) { reused.ReturnToPool(); reused = EnemyPool.Instance.GetEnemy(EnemyType.Normal, boss.Motor.CombatOrigin + Vector2.right * 12, Quaternion.identity); }
            Check(reused == old && leaseToReuse.Owner == null, "A different spawner can reuse the exact former Boss-owned instance with its lease cleared");
            boss.Summoning.Cleanup(); Check(!reused.IsReturned, "Boss cleanup cannot reclaim the instance after another spawner reuses it"); reused.ReturnToPool();
            boss.Summoning.Prepare(); boss.Summoning.Release();
            var dyingLeases = UnityEngine.Object.FindObjectsOfType<HiveTyrantSummonOwner>().Where(lease => lease.Owner == boss).ToArray();
            boss.Health.TakeDamage(boss.Health.CurrentHealth);
            Check(dyingLeases.Length > 0 && dyingLeases.All(lease => lease.Owner == null && lease.GetComponent<PooledEnemy>().IsReturned), "Lethal Boss damage immediately returns its live summons");
        }
        finally { if (boss != null) configField.SetValue(boss, original); UnityEngine.Object.Destroy(fixture); }
        yield return Delay(1.8f);
    }
    private static IEnumerator FinalStages()
    {
        var boss = UnityEngine.Object.FindObjectOfType<HiveTyrantCombat>(); var player = UnityEngine.Object.FindObjectOfType<PlayerHealth>();
        boss.AutomaticSkills = false; player.GetComponent<PlayerMovement>().SetCanMove(false);
        Place(player, boss.Motor.CombatOrigin + Vector2.down * 5);
        boss.Health.TakeDamage(boss.Health.MaxHealth / 2); yield return Delay(.1f); yield return Idle(boss);
        Check(boss.RequestSkill(HiveTyrantSkill.Slam, true), "Focused death-interrupt wave starts");
        Vector2 before = boss.GetComponent<Rigidbody2D>().position; int releases = boss.ReleaseCount;
        boss.Health.TakeDamage(boss.Health.CurrentHealth); yield return Delay(.4f);
        var presentation = boss != null ? boss.GetComponent<HiveTyrantPresentation>() : null;
        Check(boss != null && boss.ReleaseCount == releases && Vector2.Distance(before, boss.GetComponent<Rigidbody2D>().position) < .001f && presentation.IsArticulated && presentation.CurrentProfile >= 8,
            "RiftWing death keeps same phase, cancels pending physics movement/wave and retains full body animation");
        Capture("Death"); yield return Delay(1.7f); Check(boss == null, "Death cleanup after full presentation");
        yield return MovementAndSceneRestart(); yield return MainSceneIntegration();
        EditorSceneManager.LoadSceneInPlayMode(HiveTyrantCombatSetup.TestScene, new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        yield return Delay(1.5f); yield return PoseAtlas();
        HiveTyrantCombatDebug.RestartScene(); yield return Delay(1.5f); yield return Battle();
        yield return RepeatStress(); Check(runtimeErrors.Count == 0, "Final stages no runtime errors");
    }
    private static IEnumerator DamageChains(HiveTyrantCombat boss, PlayerHealth player)
    {
        Vector2 center = boss.Motor.CombatOrigin;
        Place(player, center + Vector2.down * 10);
        boss.Motor.SetMovementLocked(true);
        int hp = boss.Health.CurrentHealth;
        FireBullet(center + Vector2.down * 5, Vector2.up, new ProjectileModifierSnapshot(1, false, 0, 0, false, 0, 0, 0, 1, false, 0, 0, 0, 0, 0, .75f, .75f, 0));
        yield return Delay(1.1f);
        Check(boss.Health.CurrentHealth == hp - 2, "Physical piercing bullet damages Boss once while crossing its collider");
        hp = boss.Health.CurrentHealth;
        FireBullet(center + Vector2.down * 5, Vector2.up, new ProjectileModifierSnapshot(0, true, 6, 1, false, 0, 0, 0, 1, false, 0, 0, 0, 0, 0, .75f, .75f, 0));
        yield return Delay(.5f);
        Check(boss.Health.CurrentHealth == hp - 4, "Physical explosive projectile applies original direct and explosion damage to Boss");
        var normal = EnemyPool.Instance.GetEnemy(EnemyType.Normal, center + Vector2.right * 5, Quaternion.identity);
        normal.GetComponent<EnemyDefinition>().InitializeFromGlobalDifficulty(20, .1f, 1);
        hp = boss.Health.CurrentHealth;
        FireBullet(center + Vector2.right * 7, Vector2.left, new ProjectileModifierSnapshot(0, false, 0, 0, true, 1, 8, 1, 1, false, 0, 0, 0, 0, 0, .75f, .75f, 0));
        yield return Delay(.6f);
        Check(boss.Health.CurrentHealth == hp - 2, "Physical hit on ordinary enemy chains through the existing lightning path to Boss");
        hp = boss.Health.CurrentHealth;
        FireBullet(center + Vector2.right * 7, Vector2.left, new ProjectileModifierSnapshot(0, false, 0, 0, false, 0, 0, 0, 1, true, 2, .5f, 1, 1, .9f, .75f, .75f, 0));
        yield return Delay(1.2f);
        Check(boss.Health.CurrentHealth == hp - 2, "Two real split child projectiles damage Boss through original Bullet collision path");
        normal.ReturnToPool(); boss.Motor.SetMovementLocked(false);
    }
    private static IEnumerator SummonBaseline()
    {
        var lines = new List<string>();
        var boss = UnityEngine.Object.FindObjectOfType<HiveTyrantCombat>();
        boss.AutomaticSkills = false;
        var player = UnityEngine.Object.FindObjectOfType<PlayerHealth>();
        lines.Add("AstarPath.active present: " + (AstarPath.active != null));
        for (int i = 0; i < 4; i++)
        {
            var enemy = EnemyPool.Instance.GetEnemy((EnemyType)i, (Vector2)player.transform.position + new Vector2(9, 0), Quaternion.identity);
            enemy.GetComponent<EnemyDefinition>().InitializeFromGlobalDifficulty(4, 2, 5);
            Vector2 initial = enemy.transform.position;
            yield return Delay(2);
            var move = enemy.GetComponent<EnemyMovement>();
            var nav = enemy.GetComponent<EnemyPathNavigator>();
            lines.Add(enemy.Type + ": distance moved=" + Vector2.Distance(initial, enemy.transform.position).ToString("F3") +
                ", target=" + (move.NavigationTarget != null) + ", movement enabled=" + move.enabled + ", locked=" + move.IsMovementLocked +
                ", speed=" + move.MoveSpeed + ", rb.simulated=" + enemy.GetComponent<Rigidbody2D>().simulated +
                ", navigator=" + (nav != null) + ", path=" + (nav != null && nav.HasValidPath));
            enemy.ReturnToPool();
        }
        Directory.CreateDirectory("Docs/Boss/CompletionValidation");
        File.WriteAllLines("Docs/Boss/CompletionValidation/Summon_Before.txt", lines);
    }
    private static IEnumerator VisualSample()
    {
        var boss = UnityEngine.Object.FindObjectOfType<HiveTyrantCombat>();
        var player = UnityEngine.Object.FindObjectOfType<PlayerHealth>();
        boss.AutomaticSkills = false;
        player.GetComponent<PlayerMovement>().SetCanMove(false);
        boss.Motor.SetMovementLocked(true);
        var camera = Camera.main;
        camera.GetComponent<CameraFollow>().enabled = false;
        camera.GetComponent<HiveTyrantArenaCamera>().enabled = false;
        camera.orthographicSize = 7;
        camera.transform.position = boss.transform.position + new Vector3(0, 2, -10);
        Place(player, boss.Motor.CombatOrigin + new Vector2(1, -1).normalized * 6);
        boss.Motor.SetMovementLocked(false);
        Check(boss.RequestSkill(HiveTyrantSkill.Sweep, true), "DR actual sweep starts for visual review");
        float started = Time.time;
        for (int frame = 0; frame < 25; frame++)
        {
            while (Time.time < started + frame * .08f) yield return null;
            Capture("DR_Sweep/Frame_" + frame.ToString("D3"));
        }
        results.Add("Captured 25 actual Play Mode frames at 0.08-second sample spacing; visual acceptance requires image inspection.");
    }
    private static void FireBullet(Vector2 position, Vector2 direction, ProjectileModifierSnapshot snapshot)
    {
        Bullet bullet = UnityEngine.Object.FindObjectOfType<BulletPool>().GetBullet(position, Quaternion.identity);
        if (bullet == null) throw new InvalidOperationException("Test BulletPool returned null");
        bullet.Initialize(direction, 12, 2, 1, 1, snapshot);
    }
    private static IEnumerator MovementAndSceneRestart()
    {
        HiveTyrantCombatDebug.RestartScene();
        yield return Delay(1);
        var boss = UnityEngine.Object.FindObjectOfType<HiveTyrantCombat>();
        var player = UnityEngine.Object.FindObjectOfType<PlayerHealth>();
        Check(boss != null && player != null && !boss.Health.IsDead && !boss.Phase2Entered && boss.TotalSummoned == 0 &&
            boss.ActiveWarningCount == 0 && boss.Health.CurrentHealth == boss.Config.maxHealth,
            "Actual F9 scene-restart path restores the full encounter and pool dependencies");
        boss.AutomaticSkills = false;
        player.GetComponent<PlayerMovement>().SetCanMove(false);
        Vector2 home = boss.transform.position;
        Vector2 center = boss.Motor.CombatOrigin;
        var wall = new GameObject("Verification_PhysicalWall"); wall.layer = LayerMask.NameToLayer("Environment");
        wall.transform.position = center + Vector2.right * 4.3f;
        var obstacle = wall.AddComponent<BoxCollider2D>(); obstacle.size = new Vector2(.5f, 40);
        Place(player, center + Vector2.right * 10);
        Physics2D.SyncTransforms();
        yield return Delay(2.4f);
        var body = boss.GetComponent<Collider2D>();
        Check(Vector2.Distance(home, boss.transform.position) > .05f && !Physics2D.Distance(body, obstacle).isOverlapped &&
            body.bounds.max.x <= obstacle.bounds.min.x + .01f,
            "Actual chasing Rigidbody2D advances but cannot penetrate an Environment wall");
        Place(player, boss.Motor.CombatOrigin + Vector2.right * 5);
        int hp = player.CurrentHealth;
        Check(boss.RequestSkill(HiveTyrantSkill.Sweep, true), "Wall-occluded sweep starts with its real warning");
        yield return Idle(boss);
        Check(player.CurrentHealth == hp, "Environment wall blocks the actual sweep damage linecast");
        wall.SetActive(false); UnityEngine.Object.Destroy(wall);
        boss.RestartEncounter();
        Check(Vector2.Distance(home, boss.transform.position) < .01f && !boss.Motor.HasActionOwner && !boss.Phase2Entered,
            "Encounter reset restores home position and releases movement/action state");
    }
    private static IEnumerator MainSceneIntegration()
    {
        EditorSceneManager.LoadSceneInPlayMode(HiveTyrantCombatSetup.WorldScene, new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        yield return Delay(1);
        var world = UnityEngine.Object.FindObjectOfType<AlienPurge.Level01ConnectedV01.L01World>();
        Check(world != null && world.boss != null && world.arenaBridge != null, "Actual Level01 scene loads its existing Boss and arena bridge");
        var boss = world.boss.GetComponent<HiveTyrantCombat>();
        var player = world.player.GetComponent<PlayerHealth>();
        var camera = Camera.main; var framing = camera.GetComponent<HiveTyrantArenaCamera>(); float normalSize = camera.orthographicSize;
        boss.AutomaticSkills = false; // Isolated entrance/bridge fixture; full auto combat is tested separately.
        Check(boss != null && boss.Motor.IsMovementLocked && !world.arenaBridge.encounterStarted,
            "Level01 Boss remains dormant until existing encounter entrance conditions");
        Check(boss.InsideArena(boss.Motor.CombatOrigin), "Level01 existing Boss position lies inside the authored footprint");
        var solid = boss.GetComponent<Collider2D>();
        bool overlapsWall = Physics2D.OverlapCircleAll(boss.Motor.CombatOrigin, .2f, boss.Config.environmentLayers).Any(c => !c.isTrigger);
        Check(!overlapsWall && solid.enabled && boss.GetComponent<Rigidbody2D>().bodyType == RigidbodyType2D.Kinematic,
            "Level01 Boss starts clear of wall interiors with the original solid kinematic body");
        // Use the existing progression API to unlock only the research prerequisite in this runtime fixture.
        world.progression.Complete("Research");
        framing.enabled = false;
        player.GetComponent<PlayerMovement>().SetCanMove(false);
        Place(player, world.World(new Vector2(0, -10)));
        // Test-only entrance placement skips the approach walk; allow normal follow to settle first.
        yield return Delay(1.2f); framing.enabled = true;
        Check(world.arenaBridge.encounterStarted && !boss.Motor.IsMovementLocked, "Existing Level01 research/entrance bridge activates Boss combat");
        boss.AutomaticSkills = false;
        yield return Idle(boss);
        Place(player, boss.Motor.CombatOrigin + Vector2.down * 5);
        Check(boss.RequestSkill(HiveTyrantSkill.Sweep, true), "Configured combat skill runs on the actual Level01 Boss instance");
        yield return Delay(.2f); Capture("Level01_BossFight");
        yield return Idle(boss);
        player.RestoreHealth(player.MaxHealth); Place(player, boss.Motor.CombatOrigin + Vector2.down * 10);
        Check(boss.RequestSkill(HiveTyrantSkill.Slam, true), "Forward segmented wave finds a reachable route in the actual Level01 arena");
        yield return Delay(1.1f);
        Bounds bounds = boss.GetComponent<HiveTyrantPresentation>().VisualBounds;
        Vector3 low = camera.WorldToViewportPoint(bounds.min), high = camera.WorldToViewportPoint(bounds.max);
        Check(low.x >= 0 && low.y >= 0 && high.x <= 1 && high.y <= 1 && framing.FramingBlend > .99f, "Boss-only framing contains full upper silhouette without changing camera-follow settings");
        Capture("Level01_WaveWarning"); yield return Idle(boss); while (boss.Wave.IsTravelling) yield return null;
        player.RestoreHealth(player.MaxHealth);
        Check(boss.RequestSkill(HiveTyrantSkill.Summon, true), "Native summon placement works on existing Level01 navigation");
        yield return Idle(boss); Check(boss.LivingSummons > 0, "Level01 summons actually spawn through the existing pool");
        boss.Health.TakeDamage(boss.Health.CurrentHealth);
        yield return Delay(.1f);
        Check(world.arenaBridge.encounterFinished && world.progression.Has("BossDefeated"),
            "Original Level01 completion bridge observes death without a new victory/scene switch");
        yield return Delay(2.5f);
        Check(Mathf.Abs(camera.orthographicSize - normalSize) < .01f && framing.FramingBlend == 0, "Boss death restores the original camera size and releases encounter framing");
    }
    private static void Capture(string name)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) { results.Add("UNVERIFIED screenshot " + name + ": null graphics device"); return; }
        Camera camera = Camera.main;
        if (camera == null) return;
        string file = OutputFolder(SessionState.GetString("HiveTyrant.VerificationMode", "PlayMode")) + "/" + name + ".png";
        Directory.CreateDirectory(Path.GetDirectoryName(file));
        var render = new RenderTexture(1280, 720, 24);
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
        camera.targetTexture = render; camera.Render(); RenderTexture.active = render;
        texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
        File.WriteAllBytes(file, texture.EncodeToPNG());
        camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
        UnityEngine.Object.Destroy(texture); render.Release(); UnityEngine.Object.Destroy(render);
    }
}
