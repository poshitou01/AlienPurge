using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Real Play Mode probes: native weapon hits, authored scene defaults and full fights.
[InitializeOnLoad]
public static class HiveTyrantPressureVerification
{
    const string Key = "HiveTyrant.PressureVerification";
    public const string Folder = "Docs/Boss/PressureValidation";
    static readonly Stack<IEnumerator> jobs = new Stack<IEnumerator>();
    static readonly List<string> report = new List<string>();
    static readonly List<string> errors = new List<string>();
    static bool finished;
    static double started;
    static int hitLogs;
    static bool Baseline => SessionState.GetBool(Key + ".Baseline", false);
    static bool VisualOnly => SessionState.GetBool(Key + ".Visual", false);
    static string Prefix => Baseline ? "Before" : VisualOnly ? "FinalVisual" : "After";
    static HiveTyrantPressureVerification()
    {
        EditorApplication.playModeStateChanged += Changed;
        EditorApplication.update += Tick;
        Application.logMessageReceived += Log;
    }
    public static void RunBaseline() { Begin(true); }
    public static void RunPressure() { Begin(false); }
    public static void RunFinalVisual() { Begin(false, true); }
    public static void BuildPressureWindows()
    {
        const string output = "D:/AlienPurge/Builds/HiveTyrantPressure/AlienPurge.exe";
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var build = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { HiveTyrantCombatSetup.TestScene }, locationPathName = output,
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development | BuildOptions.StrictMode
        });
        File.WriteAllLines(Folder + "/WindowsBuild_Result.txt", new[] { build.summary.result.ToString(), "Output=" + output,
            "Errors=" + build.summary.totalErrors + " Warnings=" + build.summary.totalWarnings, "Duration=" + build.summary.totalTime });
        if (build.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new InvalidOperationException("Polish build failed");
    }
    static void Begin(bool baseline, bool visual = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        Directory.CreateDirectory(Folder);
        EditorSceneManager.OpenScene(HiveTyrantCombatSetup.TestScene);
        SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Baseline", baseline);
        SessionState.SetBool(Key + ".Visual", visual);
        EditorApplication.isPlaying = true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            jobs.Clear(); report.Clear(); errors.Clear(); finished = false;
            started = EditorApplication.timeSinceStartup; Application.runInBackground = true;
            var gameView = EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")); gameView.Show(); gameView.Focus();
            jobs.Push(Verify());
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(SessionState.GetInt(Key + ".Exit", 1));
        }
    }
    static void Log(string message, string stack, LogType type)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack);
        if (type == LogType.Log && stack.Contains("EnemyHealth:TakeDamage")) hitLogs++;
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || finished) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (jobs.Count == 0) Changed(PlayModeStateChange.EnteredPlayMode);
        try
        {
            if (EditorApplication.isPaused) throw new InvalidOperationException("Unexpected Editor pause.");
            if (EditorApplication.timeSinceStartup - started > 900) throw new TimeoutException("Polish checks timed out.");
            var job = jobs.Peek();
            if (!job.MoveNext()) { jobs.Pop(); if (jobs.Count == 0) Complete(true); }
            else if (job.Current is IEnumerator nested) jobs.Push(nested);
        }
        catch (Exception e) { report.Add("FAIL " + e); Complete(false); }
    }
    static void Complete(bool success)
    {
        finished = true; success &= errors.Count == 0;
        report.Insert(0, (success ? "PASSED" : "FAILED") + " actual Unity Play Mode " + Application.unityVersion + "; " + SystemInfo.graphicsDeviceType);
        report.AddRange(errors); File.WriteAllLines(Folder + "/" + Prefix + "_Result.txt", report);
        SessionState.SetInt(Key + ".Exit", success ? 0 : 1);
        Debug.Log("[HiveTyrantPressure] " + report[0]); EditorApplication.isPlaying = false;
    }
    static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); report.Add("PASS " + message); }
    static IEnumerator Wait(float duration) { float end = Time.realtimeSinceStartup + duration; while (Time.realtimeSinceStartup < end) yield return null; }
    static void Place(PlayerHealth player, Vector2 point)
    {
        player.transform.position = point; var rb = player.GetComponent<Rigidbody2D>(); rb.position = point; rb.velocity = Vector2.zero; Physics2D.SyncTransforms();
    }
    static IEnumerator Capture(string name)
    {
        string path = Path.GetFullPath(Folder + "/" + Prefix + "_" + name + ".png");
        ScreenCapture.CaptureScreenshot(path); yield return Wait(.35f);
        Check(File.Exists(path), "Actual Game View capture " + name);
    }
    static IEnumerator Fresh()
    {
        HiveTyrantCombatDebug.RequestedTestTier = 0;
        Time.timeScale = 1; HiveTyrantCombatDebug.RestartScene(); yield return Wait(.7f);
    }
    static void Jump(PlayerHealth player)
    {
        typeof(PlayerJetJump).GetMethod("TryStartJump", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(player.GetComponent<PlayerJetJump>(), null);
    }
    static IEnumerator Verify()
    {
        yield return Wait(.7f);
        UnityEngine.Random.InitState(1710);
        var boss = UnityEngine.Object.FindObjectOfType<HiveTyrantCombat>();
        var player = UnityEngine.Object.FindObjectOfType<PlayerHealth>();
        Check(boss.AutomaticSkills, "Saved scene starts autonomous AI without debug input");
        boss.AutomaticSkills = false; boss.Motor.enabled = false;
        player.GetComponent<PlayerMovement>().SetCanMove(false);
        player.IncreaseMaxHealth(25); // Isolated collision fixtures only, not used in full fights below.
        var c = boss.Config;
        Check(c.sweepRadius == 7 && c.sweepAngle == 120, "Original sweep radius and angle preserved");
        Check(c.phase2SweepSpacing > player.GetComponent<PlayerJetJump>().CooldownDuration, "Second sweep spacing exceeds native jump cooldown");
        Check(HiveTyrantEncounterBalance.Health(c, 4) > c.maxHealth && HiveTyrantEncounterBalance.Health(c, 4) < c.maxHealth * 4,
            "Entry scaling is sublinear: upgrades retain an offensive advantage");
        Check(HiveTyrantEncounterBalance.Health(c, 10000) <= c.maxHealth * c.maximumHealthScale, "Endurance has a hard upper cap");
        boss.RestartEncounter(); boss.Motor.enabled = false; Place(player, boss.Motor.CombatOrigin + Vector2.down * 10);
        boss.CaptureEntryLoadout(); int initial = boss.Health.MaxHealth;
        player.GetComponent<WeaponManager>().AddBulletDamage(1); yield return Wait(.1f);
        Check(boss.Health.MaxHealth == initial, "Mid-fight upgrades never resize or heal the Boss");
        var origin = boss.Motor.CombatOrigin;
        int hp = player.CurrentHealth;
        boss.Pressure.LaunchCrescent(origin, Vector2.down);
        Check(player.CurrentHealth == hp, "Outside original 7m sector: no instant remote hit on release");
        boss.Pressure.Advance(.7f);
        Check(player.CurrentHealth == hp - c.sweepDamage, "Travelling crescent hits a grounded player beyond the original range, including a large simulation step");
        boss.Pressure.Advance(.2f);
        Check(player.CurrentHealth == hp - c.sweepDamage, "Multiple arc segments and body colliders cannot multiply one cast's damage");
        boss.Pressure.Cancel();
        Jump(player); Check(player.GetComponent<PlayerJetJump>().IsActive, "Native jump input enters Airborne without editing invulnerability");
        hp = player.CurrentHealth;
        boss.Pressure.LaunchCrescent(origin, Vector2.down); boss.Pressure.Advance(.7f);
        Check(player.CurrentHealth == hp && boss.Pressure.JumpAvoidances > 0, "Native jump clears the moving crescent");
        boss.Pressure.Cancel(); yield return Wait(.9f);
        Place(player, origin + Vector2.down * 5); hp = player.CurrentHealth;
        boss.Pressure.LaunchCrescent(origin, Vector2.down);
        Check(player.CurrentHealth == hp - c.sweepDamage, "Original close-range sector still deals its release hit"); boss.Pressure.Cancel();
        Place(player, origin + new Vector2(10, 0)); hp = player.CurrentHealth;
        boss.Pressure.LaunchCrescent(origin, Vector2.down); boss.Pressure.Advance(1.7f);
        Check(player.CurrentHealth == hp, "Flanking the travelling crescent remains a valid escape"); boss.Pressure.Cancel();
        var wall = new GameObject("Pressure test wall") { layer = 7 }; wall.transform.position = origin + Vector2.down * 8.5f;
        wall.AddComponent<BoxCollider2D>().size = new Vector2(18, .4f); Physics2D.SyncTransforms();
        Place(player, origin + Vector2.down * 11); hp = player.CurrentHealth;
        boss.Pressure.LaunchCrescent(origin, Vector2.down); boss.Pressure.Advance(1.5f);
        Check(player.CurrentHealth == hp, "Crescent clips against Environment walls and cannot hit through cover");
        boss.Pressure.Cancel(); UnityEngine.Object.Destroy(wall); yield return Wait(.1f);
        Place(player, origin + Vector2.down * 10);
        boss.Pressure.LaunchCrescent(origin, Vector2.down); boss.Pressure.Advance(.12f);
        yield return Capture("Advancing_Crescent"); boss.Pressure.Cancel();
        hp = player.CurrentHealth; Vector2 mark = player.transform.position;
        Check(boss.Pressure.MarkSpore(mark), "Spore mark accepts a legal arena point");
        boss.Pressure.Advance(c.sporeWarning - .1f);
        Check(player.CurrentHealth == hp, "Spore warning does no damage");
        Place(player, mark + Vector2.right * 4);
        boss.Pressure.Advance(.2f);
        Check(player.CurrentHealth == hp && boss.Pressure.Spores[0].position == mark, "Spore locks once; walking out escapes rather than dragging the mark");
        boss.Pressure.Cancel(); Place(player, mark); hp = player.CurrentHealth;
        boss.Pressure.MarkSpore(mark); boss.Pressure.Advance(c.sporeWarning + .05f);
        Check(player.CurrentHealth == hp - c.sporeDamage, "Standing on the expired warning takes a spore hit");
        boss.Pressure.Advance(.25f);
        Check(player.CurrentHealth == hp - c.sporeDamage, "Lingering spore residue has at most one hit per player per patch");
        Place(player, mark + Vector2.right * 4); boss.Pressure.MarkSpore(player.transform.position);
        yield return Capture("Spore_Barrage"); boss.Pressure.Cancel();
        boss.Pressure.MarkSpore(mark); boss.enabled = false;
        Check(!boss.Pressure.Active && boss.LivingSummons == 0, "Disable cancels pressure hazards and owned summons");
        boss.enabled = true; boss.Motor.enabled = false; Place(player, origin + Vector2.down * 10);
        boss.RestartEncounter(); boss.Motor.enabled = false;
        Check(boss.RequestSkill(HiveTyrantSkill.Summon, true), "P1 summon request accepted");
        int planned = boss.PendingSpawnCount;
        Check(planned >= 6 && planned <= c.summonWaveSize, "P1 summons a larger legal wave: " + planned);
        yield return Wait(c.summonWarning + 1.2f);
        Check(boss.LivingSummons >= 6 && boss.LivingSummons <= c.summonAliveCap, "P1 native enemies released within population cap: " + boss.LivingSummons);
        boss.Summoning.Cleanup(); boss.RestartEncounter(); boss.Motor.enabled = false;
        boss.CaptureEntryLoadout();
        boss.Health.TakeDamage(boss.Health.CurrentHealth - Mathf.FloorToInt(boss.Health.MaxHealth * c.phase2HealthRatio));
        yield return Wait(c.phaseTransitionTime + .2f);
        Place(player, boss.Motor.CombatOrigin + Vector2.down * 10);
        Check(boss.Phase2Entered && boss.PhaseTransitionCount == 1, "Phase transition remains a single readable reveal");
        Check(boss.RequestSkill(HiveTyrantSkill.Summon, true), "P2 summon request accepted");
        Check(boss.PendingSpawnCount >= 8 && boss.PendingSpawnCount <= c.phase2SummonWaveSize, "P2 wave grows to 8-10 legal spawns: " + boss.PendingSpawnCount);
        yield return Wait(c.summonWarning + 1.2f); boss.Summoning.Cleanup();
        Place(player, boss.Motor.CombatOrigin + Vector2.right * 10);
        Check(boss.RequestSkill(HiveTyrantSkill.Sweep, true), "P2 paired sweep accepted");
        float firstRelease = -1, secondRelease = -1, until = Time.time + 8; int startReleases = boss.SkillReleases[0];
        while (Time.time < until && secondRelease < 0)
        {
            if (boss.SkillReleases[0] > startReleases && firstRelease < 0) firstRelease = Time.time;
            if (boss.SkillReleases[0] > startReleases + 1) secondRelease = Time.time;
            yield return null;
        }
        Check(firstRelease >= 0 && secondRelease - firstRelease >= c.phase2SweepSpacing - .06f, "Actual P2 releases leave a full jump cooldown: " + (secondRelease - firstRelease).ToString("F2") + "s");
        boss.RestartEncounter(); boss.Motor.enabled = false;
        boss.Pressure.MarkSpore(player.transform.position);
        Check(!boss.RequestSkill(HiveTyrantSkill.Slam, true), "AI cannot overlap barrage with a ground-wave pattern and block its safe lanes");
        boss.RestartEncounter();
        Check(!boss.Pressure.Active && boss.Pressure.SweepHits == 0, "Restart removes all hazards and resets pressure counters");
        int meshCount = Resources.FindObjectsOfTypeAll<Mesh>().Count(m => m.name.StartsWith("Crescent_") || m.name == "SporeBarrage" || m.name == "CrescentRoute");
        for (int i = 0; i < 12; i++) { boss.Pressure.LaunchCrescent(boss.Motor.CombatOrigin, Vector2.down); boss.Pressure.MarkSpore(player.transform.position); boss.RestartEncounter(); }
        Check(Resources.FindObjectsOfTypeAll<Mesh>().Count(m => m.name.StartsWith("Crescent_") || m.name == "SporeBarrage" || m.name == "CrescentRoute") == meshCount,
            "Repeated casts/restarts reuse the same four pressure meshes");
        Place(player, boss.Motor.CombatOrigin + Vector2.down * 10); boss.CaptureEntryLoadout();
        boss.Pressure.MarkSpore(player.transform.position);
        boss.Health.TakeDamage(boss.Health.CurrentHealth - Mathf.FloorToInt(boss.Health.MaxHealth * c.phase2HealthRatio));
        yield return Wait(.1f);
        Check(boss.State == HiveTyrantState.PhaseTransition && !boss.Pressure.Active, "Phase reveal cancels active spore hazards before they can detonate");
        boss.Health.TakeDamage(boss.Health.CurrentHealth); yield return Wait(.1f);
        Check(boss.Health.IsDead && !boss.Pressure.Active && boss.ActiveWarningCount == 0, "Lethal damage during phase reveal cancels every pending hazard");
        yield return Battle(1, false);
        yield return Battle(1, true);
        yield return Battle(2, true);
        yield return Battle(3, true);
        HiveTyrantCombatDebug.RequestedTestTier = 2;
        HiveTyrantCombatDebug.RestartScene(); yield return Wait(.8f);
        var presetPlayer = UnityEngine.Object.FindObjectOfType<PlayerHealth>();
        Check(presetPlayer.MaxHealth == 10 && presetPlayer.GetComponent<WeaponEvolutionController>().CurrentEvolution == WeaponEvolutionType.ClusterBurst,
            "F10 test-loadout restart applies a legal evolved build exactly once");
        HiveTyrantCombatDebug.RequestedTestTier = 0;
        yield return FormalEntry();
        Check(errors.Count == 0, "No Unity errors or exceptions across fixtures and full fights");
    }
    static IEnumerator Battle(int tier, bool dodge)
    {
        yield return Fresh();
        var boss = UnityEngine.Object.FindObjectOfType<HiveTyrantCombat>();
        var player = UnityEngine.Object.FindObjectOfType<PlayerHealth>();
        HiveTyrantTestLoadouts.Apply(player, tier); boss.RestartEncounter(); boss.CaptureEntryLoadout();
        HiveTyrantCombatDebug.RequestedTestTier = tier; // Label this explicit test preset accurately in captures.
        UnityEngine.Random.InitState(1710 + tier);
        var pilot = player.gameObject.AddComponent<HiveTyrantPlaytestPilot>(); pilot.Boss = boss; pilot.Battle = true; pilot.Dodge = dodge;
        var probe = Camera.main.gameObject.AddComponent<HiveTyrantRepairFrameProbe>(); probe.recording = false;
        string label = "Tier" + tier + (dodge ? "_Reactive" : "_Stationary");
        report.Add("BATTLE " + label + " " + HiveTyrantTestLoadouts.Describe(player) + "; BossHP=" + boss.Health.MaxHealth + "; entryPower=" + boss.EntryPower + "; summonHealthScale=" + boss.SummonHealthScale);
        Check(boss.AutomaticSkills && !player.IsTemporarilyInvulnerable, label + " authored AI, native weapon/movement, no healing, no direct Boss damage");
        float began = Time.time, next = began, timeout = Time.realtimeSinceStartup + 240;
        int peak = 0, phases = 0, total = 0, sweepHits = 0, sporeHits = 0, maximum = boss.Health.MaxHealth;
        int[] releases = new int[4]; var frames = new List<float>(); bool capturedBarrage = false, capturedWave = false, capturedP2 = false;
        while (boss != null && !boss.Health.IsDead && !player.IsDead && Time.realtimeSinceStartup < timeout)
        {
            peak = Mathf.Max(peak, boss.LivingSummons); phases = boss.PhaseTransitionCount; total = boss.TotalSummoned;
            sweepHits = boss.Pressure.SweepHits; sporeHits = boss.Pressure.SporeHits; Array.Copy(boss.SkillReleases, releases, 4);
            frames.Add(Time.unscaledDeltaTime * 1000);
            if (Time.time >= next)
            {
                report.Add(label + " t=" + (Time.time - began).ToString("F1") + " Player=" + player.CurrentHealth + " Boss=" + boss.Health.CurrentHealth + " living=" + boss.LivingSummons);
                next = Time.time + 10;
            }
            if (dodge && tier == 1)
            {
                if (!capturedBarrage && boss.Pressure.HasSpores) { capturedBarrage = true; yield return Capture("Battle_Spores"); }
                if (!capturedWave && boss.Pressure.HasCrescent) { capturedWave = true; yield return Capture("Battle_Crescent"); }
                if (!capturedP2 && boss.State == HiveTyrantState.PhaseTransition && boss.Motor.IsPhase2) { capturedP2 = true; yield return Capture("Battle_Phase2"); }
            }
            yield return null;
        }
        bool won = !player.IsDead && (boss == null || boss.Health.IsDead);
        frames.Sort();
        report.Add("OUTCOME " + label + " won=" + won + " playerHP=" + player.CurrentHealth + " elapsed=" + (Time.time - began).ToString("F1") + " BossStartHP=" + maximum +
            " shots=" + pilot.Shots + " jumps=" + pilot.Jumps + " phases=" + phases + " totalSummons=" + total + " peakAlive=" + peak +
            " sweepHits=" + sweepHits + " sporeHits=" + sporeHits + " releases=" + string.Join(",", releases));
        if (frames.Count > 0) report.Add("FRAME " + label + " median=" + frames[frames.Count / 2].ToString("F2") + " P95=" + frames[(int)(frames.Count * .95f)].ToString("F2") + " ms; actual Editor Game View, captures included");
        Check(!dodge ? player.IsDead : won && phases == 1, label + (!dodge ? " upgraded stand-and-fire strategy is punished" : " wins both phases using normal play"));
        if (dodge) Check(releases.All(n => n > 0) && pilot.Jumps > 0, label + " all four skills and native jump exercised");
        yield return Wait(.2f);
    }
    static IEnumerator FormalEntry()
    {
        Time.timeScale = 1;
        EditorSceneManager.LoadSceneInPlayMode(HiveTyrantCombatSetup.WorldScene, new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        yield return Wait(1);
        var boss = UnityEngine.Object.FindObjectOfType<HiveTyrantCombat>();
        var player = UnityEngine.Object.FindObjectOfType<PlayerHealth>();
        Check(boss != null && !boss.EntryCaptured && boss.State == HiveTyrantState.Dormant, "Formal level leaves Boss dormant without taking a premature loadout snapshot");
        HiveTyrantTestLoadouts.Apply(player, 1);
        var world = UnityEngine.Object.FindObjectOfType<AlienPurge.Level01ConnectedV01.L01World>();
        world.progression.previewAllDoorsOpen = true;
        Place(player, world.transform.TransformPoint(new Vector3(0, -10, 0)));
        yield return Wait(.5f);
        Check(world.arenaBridge.encounterStarted && boss.EntryCaptured && boss.Health.MaxHealth > boss.Config.maxHealth,
            "Existing arena bridge activates the encounter and captures the upgraded loadout on entry");
        boss.Health.TakeDamage(boss.Health.CurrentHealth); yield return Wait(.2f);
        Check(world.arenaBridge.encounterFinished && !boss.Pressure.Active, "Formal Boss defeat still clears hazards and completes the existing bridge");
    }
}
