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
            if (EditorApplication.timeSinceStartup - startTime > 240) throw new TimeoutException("Play Mode verification exceeded 240 seconds.");
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
        results.Add("Formal eight-direction attack animation and final gameplay balance remain unverified/incomplete.");
        if (knownEditorDiagnostics.Count > 0) results.Add("Existing unrelated A* Editor update checker diagnostics (also present before changes): " + string.Join(" | ", knownEditorDiagnostics));
        Directory.CreateDirectory("Docs/Boss/Validation");
        File.WriteAllLines("Docs/Boss/Validation/PlayMode_Result.txt", results);
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
    { float until = Time.time + seconds; while (Time.time < until) yield return null; }
    private static IEnumerator Idle(HiveTyrantCombat boss)
    { float until = Time.time + 15; while (boss != null && boss.Motor.HasActionOwner && Time.time < until) yield return null; Check(boss != null && !boss.Motor.HasActionOwner, "Action releases ownership after recovery"); }
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
        HiveTyrantCombat boss = UnityEngine.Object.FindObjectOfType<HiveTyrantCombat>();
        PlayerHealth player = UnityEngine.Object.FindObjectOfType<PlayerHealth>();
        Check(boss != null && player != null && boss.Config != null, "Dedicated scene has usable Boss, Player and config");
        boss.AutomaticSkills = false;
        player.GetComponent<PlayerMovement>().SetCanMove(false);
        Vector2 origin = boss.Motor.CombatOrigin;
        Place(player, origin + Vector2.down * 5);
        Check(boss.Health.CurrentHealth == boss.Config.maxHealth, "Boss uses EnemyHealth at configured initial HP");
        Check(HiveTyrantCombat.ContainsPoint(origin, Vector2.down, origin + Vector2.down * 5, 6, 110) &&
              !HiveTyrantCombat.ContainsPoint(origin, Vector2.down, origin + Vector2.up * 5, 6, 110), "Sector front/back geometry agrees with warning");

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

        Place(player, origin + Vector2.down * 6.2f); hp = player.CurrentHealth;
        Check(boss.RequestSkill(HiveTyrantSkill.Slam, true), "Slam starts");
        yield return Delay(boss.Config.slamWindup * .5f);
        Check(player.CurrentHealth == hp, "Filled slam warning causes no early damage"); Capture("Slam_Warning");
        yield return Delay(boss.Config.slamWindup * .5f + .3f);
        Check(player.CurrentHealth == hp - boss.Config.slamDamage, "Slam disk damages an interior point exactly once");
        yield return Idle(boss);
        Place(player, origin + Vector2.down * (boss.Config.slamRadius + .8f)); hp = player.CurrentHealth;
        Check(boss.RequestSkill(HiveTyrantSkill.Slam, true), "Outside-disk slam starts");
        yield return Delay(boss.Config.slamWindup + .3f);
        Check(player.CurrentHealth == hp, "Moving outside the slam disk avoids damage"); yield return Idle(boss);

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
        var blocker = new GameObject("Verification_AllSummonPointsBlocked"); blocker.layer = LayerMask.NameToLayer("Environment");
        blocker.transform.position = boss.Motor.CombatOrigin; blocker.AddComponent<CircleCollider2D>().radius = 14;
        Physics2D.SyncTransforms();
        Check(boss.RequestSkill(HiveTyrantSkill.Summon, true) && boss.PendingSpawnCount == 0, "Fully blocked spawn search skips every invalid point");
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
        releases = boss.ReleaseCount; lockedPosition = boss.transform.position;
        boss.Health.TakeDamage(boss.Health.CurrentHealth);
        Check(boss.Motor.IsDead && boss.State == HiveTyrantState.Dead && boss.ActiveWarningCount == 0, "Lethal damage immediately cancels warnings and locks death action");
        float deathDuration = boss.DeathPresentationDuration;
        yield return Delay(.4f);
        Check(boss != null && boss.ReleaseCount == releases && Vector2.Distance(boss.transform.position, lockedPosition) < .01f,
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
        Check(boss != null && boss.Motor.IsMovementLocked && !world.arenaBridge.encounterStarted,
            "Level01 Boss remains dormant until existing encounter entrance conditions");
        Check(boss.InsideArena(boss.Motor.CombatOrigin), "Level01 existing Boss position lies inside the authored footprint");
        var solid = boss.GetComponent<Collider2D>();
        bool overlapsWall = Physics2D.OverlapCircleAll(boss.Motor.CombatOrigin, .2f, boss.Config.environmentLayers).Any(c => !c.isTrigger);
        Check(!overlapsWall && solid.enabled && boss.GetComponent<Rigidbody2D>().bodyType == RigidbodyType2D.Kinematic,
            "Level01 Boss starts clear of wall interiors with the original solid kinematic body");
        // Use the existing progression API to unlock only the research prerequisite in this runtime fixture.
        world.progression.Complete("Research");
        player.GetComponent<PlayerMovement>().SetCanMove(false);
        Place(player, world.World(new Vector2(0, -10)));
        yield return Delay(.3f);
        Check(world.arenaBridge.encounterStarted && !boss.Motor.IsMovementLocked, "Existing Level01 research/entrance bridge activates Boss combat");
        boss.AutomaticSkills = false;
        yield return Idle(boss);
        Place(player, boss.Motor.CombatOrigin + Vector2.down * 5);
        Check(boss.RequestSkill(HiveTyrantSkill.Sweep, true), "Configured combat skill runs on the actual Level01 Boss instance");
        yield return Delay(.2f); Capture("Level01_BossFight");
        boss.Health.TakeDamage(boss.Health.CurrentHealth);
        yield return Delay(.1f);
        Check(world.arenaBridge.encounterFinished && world.progression.Has("BossDefeated"),
            "Original Level01 completion bridge observes death without a new victory/scene switch");
    }
    private static void Capture(string name)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) { results.Add("UNVERIFIED screenshot " + name + ": null graphics device"); return; }
        Camera camera = Camera.main;
        if (camera == null) return;
        Directory.CreateDirectory("Docs/Boss/Validation");
        var render = new RenderTexture(1280, 720, 24);
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
        camera.targetTexture = render; camera.Render(); RenderTexture.active = render;
        texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
        File.WriteAllBytes("Docs/Boss/Validation/" + name + ".png", texture.EncodeToPNG());
        camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
        UnityEngine.Object.Destroy(texture); render.Release(); UnityEngine.Object.Destroy(render);
    }
}
