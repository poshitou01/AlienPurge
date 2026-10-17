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
public static class HiveTyrantPolishVerification
{
    const string Key = "HiveTyrant.PolishVerification";
    public const string Folder = "Docs/Boss/CombatPolishValidation";
    static readonly Stack<IEnumerator> jobs = new Stack<IEnumerator>();
    static readonly List<string> report = new List<string>();
    static readonly List<string> errors = new List<string>();
    static bool finished;
    static double started;
    static int hitLogs;
    static bool Baseline => SessionState.GetBool(Key + ".Baseline", false);
    static bool VisualOnly => SessionState.GetBool(Key + ".Visual", false);
    static string Prefix => Baseline ? "Before" : VisualOnly ? "FinalVisual" : "After";
    static HiveTyrantPolishVerification()
    {
        EditorApplication.playModeStateChanged += Changed;
        EditorApplication.update += Tick;
        Application.logMessageReceived += Log;
    }
    public static void RunBaseline() { Begin(true); }
    public static void RunPolish() { Begin(false); }
    public static void RunFinalVisual() { Begin(false, true); }
    public static void BuildPolishWindows()
    {
        const string output = "D:/AlienPurge/Builds/HiveTyrantPolish/AlienPurge.exe";
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
            if (EditorApplication.timeSinceStartup - started > 420) throw new TimeoutException("Polish checks timed out.");
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
        Debug.Log("[HiveTyrantPolish] " + report[0]); EditorApplication.isPlaying = false;
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
    static IEnumerator Verify()
    {
        // Intervene before any automatic attack only for the isolated hit probe.
        var boss = UnityEngine.Object.FindObjectOfType<HiveTyrantCombat>();
        bool authoredAuto = boss.AutomaticSkills; boss.AutomaticSkills = false;
        yield return Wait(1);
        var player = UnityEngine.Object.FindObjectOfType<PlayerHealth>();
        player.GetComponent<PlayerMovement>().SetCanMove(false);
        Place(player, boss.Motor.CombatOrigin + Vector2.down * 4.1f);
        yield return Wait(2);
        var view = Camera.main;
        var probe = view.gameObject.AddComponent<HiveTyrantRepairFrameProbe>(); probe.boss = boss; probe.player = player;
        var presentation = boss.GetComponent<HiveTyrantPresentation>();
        var weapon = player.GetComponent<WeaponManager>(); int hp = boss.Health.CurrentHealth, shots = 0, poseRestarts = 0;
        bool previousArticulated = presentation.IsArticulated; hitLogs = 0;
        float until = Time.realtimeSinceStartup + 8;
        while (Time.realtimeSinceStartup < until)
        {
            weapon.AimAt(boss.Motor.CombatOrigin); if (weapon.TryFire()) shots++;
            if (presentation.IsArticulated && !previousArticulated) poseRestarts++;
            previousArticulated = presentation.IsArticulated; yield return null;
        }
        probe.recording = false;
        float[] frames = probe.frameMs.OrderBy(x => x).ToArray();
        report.Add("Authored auto=" + authoredAuto + "; shots=" + shots + "; actual damage=" + (hp - boss.Health.CurrentHealth) + "; full-body hit pose restarts=" + poseRestarts + "; per-hit Console logs=" + hitLogs);
        report.Add("Rendered hit probe: frames=" + frames.Length + "; median_ms=" + frames[frames.Length / 2].ToString("F3") + "; p95_ms=" + frames[(int)(frames.Length * .95f)].ToString("F3") + "; max_ms=" + frames.Last().ToString("F3") + "; camera_step=" + probe.maxCameraStep.ToString("F5") + "; player_step=" + probe.maxPlayerStep.ToString("F5"));
        File.WriteAllLines(Folder + "/" + Prefix + "_HitFrames.csv", probe.rows);
        Check(hp - boss.Health.CurrentHealth >= 30, "Continuous native weapon hits reach original EnemyHealth");
        if (!Baseline) Check(poseRestarts == 0 && hitLogs == 0, "Ordinary bullets never restart full-body poses or spam Console");
        yield return Capture("HUD");
        if (Baseline) yield break;
        Check(authoredAuto, "Saved test scene starts with automatic AI enabled");
        Check(boss.Config.summonWaveSize == 4 && boss.Config.summonAliveCap == 6 && boss.Config.phase2SummonWaveSize == 5 && boss.Config.phase2SummonAliveCap == 8 && boss.Config.summonBudget == 20,
            "Encounter config: 4/5 per wave, 6/8 alive, 20 total; native type caps retained");
        var hud = boss.GetComponent<HiveTyrantHealthBar>();
        var ornament = hud.Panel.Find("OriginalOrnament").GetComponent<UnityEngine.UI.Image>();
        Check(ornament.sprite.texture.name == "HiveTyrant_BossHPFrame_01" && ornament.type == UnityEngine.UI.Image.Type.Sliced,
            "Original frame artwork uses sliced rails and proportional caps");
        Check(boss.RequestSkill(HiveTyrantSkill.Summon, true) && boss.PendingSpawnCount == 4, "Phase 1 reserves four valid native summons");
        int releases = boss.ReleaseCount;
        boss.Health.TakeDamage(boss.Health.CurrentHealth - 300);
        int phaseHp = boss.Health.CurrentHealth; Vector3 playerAtTransition = player.transform.position;
        yield return Wait(.35f);
        Check(boss.State == HiveTyrantState.PhaseTransition && boss.PendingSpawnCount == 0 && boss.ActiveWarningCount == 0 && !boss.Motor.IsPhase2,
            "Transition cancels pending attacks and gathers energy before wings appear");
        Check(hud.PhaseAnnouncementVisible && presentation.CurrentProfile < 8, "Phase II announcement accompanies old-silhouette charge");
        yield return Capture("Phase2_Charge");
        yield return Wait(.75f);
        Check(boss.Motor.IsPhase2 && presentation.CurrentProfile >= 8 && hud.PhaseAnnouncementVisible, "Wing reveal and phase title overlap at the authored cue");
        yield return Capture("Phase2_Reveal");
        yield return Wait(1.5f);
        Check(boss.PhaseTransitionCount == 1 && boss.Health.CurrentHealth == phaseHp && boss.ReleaseCount == releases && !boss.Motor.HasActionOwner && !hud.PhaseAnnouncementVisible,
            "Transition completes once, with no healing, attack release or lingering title");
        Check(Vector3.Distance(playerAtTransition, player.transform.position) < .001f && Time.timeScale == 1,
            "Transition never teleports the player or pauses gameplay time");
        Check(boss.RequestSkill(HiveTyrantSkill.Summon, true) && boss.PendingSpawnCount == 5, "Phase 2 reserves five valid native summons");
        yield return Wait(boss.Config.summonWarning + .12f);
        Check(boss.LivingSummons == 5 && boss.TotalSummoned == 5, "All five warned Phase 2 summons become live native enemies");
        yield return Capture("Phase2_Summons");
        yield return Wait(1);
        Check(boss.RequestSkill(HiveTyrantSkill.Summon, true) && boss.PendingSpawnCount <= 3 && boss.PendingSpawnCount > 0,
            "Second Phase 2 wave reserves only the three remaining alive slots");
        yield return Wait(boss.Config.summonWarning + .12f);
        Check(boss.LivingSummons <= 8 && boss.TotalSummoned <= 8, "Phase 2 live spawns respect the eight-enemy cap at release");
        boss.Summoning.Cleanup();
        yield return Wait(1);
        if (VisualOnly)
        {
            boss.RestartEncounter(); boss.Health.TakeDamage(300); yield return Wait(.4f);
            boss.enabled = false; yield return Wait(.1f);
            Check(!hud.PhaseAnnouncementVisible && boss.GetComponent<HiveTyrantEffects>().ActiveCount == 0, "Disable during transformation removes announcement and effects");
            boss.enabled = true; boss.RestartEncounter(); yield return Wait(.1f);
            Check(!hud.PhaseAnnouncementVisible && !boss.Motor.IsPhase2 && boss.PhaseTransitionCount == 0, "Restart clears phase, caption and wing reveal state");
            boss.Health.TakeDamage(300); yield return Wait(.4f); boss.Health.TakeDamage(300); yield return Wait(.1f);
            Check(boss.Health.IsDead && !hud.PhaseAnnouncementVisible && boss.GetComponent<HiveTyrantEffects>().ActiveCount == 0,
                "Lethal hit during transformation immediately cancels announcement and ascension FX");
            yield break;
        }
        boss.RestartEncounter(); boss.AutomaticSkills = true;
        Place(player, boss.Motor.CombatOrigin + Vector2.down * 4.1f);
        yield return Wait(boss.Config.openingDelay + .1f);
        Check(boss.State == HiveTyrantState.SweepWindup, "Autonomous close-range opening chooses sweep");
        boss.RestartEncounter(); Place(player, boss.Motor.CombatOrigin + Vector2.down * 11);
        yield return Wait(boss.Config.openingDelay + .1f);
        Check(boss.State == HiveTyrantState.SlamWindup, "Autonomous ranged opening chooses ground wave");
        yield return Battle();
    }
    static IEnumerator Battle()
    {
        HiveTyrantCombatDebug.RestartScene(); yield return Wait(.9f);
        var boss = UnityEngine.Object.FindObjectOfType<HiveTyrantCombat>();
        var player = UnityEngine.Object.FindObjectOfType<PlayerHealth>();
        var probe = Camera.main.gameObject.AddComponent<HiveTyrantRepairFrameProbe>(); probe.recording = false;
        var pilot = player.gameObject.AddComponent<HiveTyrantPlaytestPilot>(); pilot.Boss = boss; pilot.Battle = true;
        Check(boss.AutomaticSkills && player.MaxHealth == 5 && player.GetComponent<PlayerMovement>().MoveSpeed == 5 && !player.IsTemporarilyInvulnerable,
            "Fresh scene: default autonomous AI, original 5 HP/speed 5 player, normal weapon; no F keys, healing or boss damage injection");
        var states = new HashSet<HiveTyrantState>(); float begin = Time.time, next = begin;
        int maxAlive = 0, phases = 0, summons = 0, releases = 0; bool announced = false;
        while (boss != null && !boss.Health.IsDead && !player.IsDead && Time.time < begin + 200)
        {
            states.Add(boss.State); maxAlive = Mathf.Max(maxAlive, boss.LivingSummons);
            phases = boss.PhaseTransitionCount; summons = boss.TotalSummoned; releases = boss.ReleaseCount;
            var hud = boss.GetComponent<HiveTyrantHealthBar>();
            if (!announced && hud.PhaseAnnouncementVisible)
            {
                announced = true; yield return Wait(.4f); yield return Capture("Battle_Phase2");
            }
            if (Time.time >= next)
            {
                string row = "BATTLE t=" + (Time.time - begin).ToString("F1") + " player=" + player.CurrentHealth + " boss=" + boss.Health.CurrentHealth + " summons=" + summons + " alive=" + boss.LivingSummons + " state=" + boss.State;
                report.Add(row); Debug.Log("[HiveTyrantPolish] " + row); next = Time.time + 15;
            }
            yield return null;
        }
        report.Add("Battle result: playerHP=" + player.CurrentHealth + "; elapsed=" + (Time.time - begin).ToString("F1") + "; shots=" + pilot.Shots + "; phases=" + phases + "; totalSummons=" + summons + "; maxAlive=" + maxAlive + "; releases=" + releases);
        Check(!player.IsDead && (boss == null || boss.Health.IsDead), "Complete autonomous battle is winnable with the unmodified player and native weapon");
        Check(phases == 1 && announced && states.Contains(HiveTyrantState.SweepWindup) && states.Contains(HiveTyrantState.SlamWindup) && states.Contains(HiveTyrantState.SummonWindup),
            "Full fight uses all three skills, both phases and the phase announcement");
        yield return Wait(2);
        Check(UnityEngine.Object.FindObjectsOfType<HiveTyrantSummonOwner>().Length == 0 && GameObject.Find("HiveTyrant_EncounterEffects") == null,
            "Defeat removes owned summons and transition effects");
        yield return Capture("Battle_Clear");
    }
}
