using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Reproduces the reported camera cut and invisible death screen in the actual scene.
[InitializeOnLoad]
public static class HiveTyrantRepairVerification
{
    private const string Key = "HiveTyrant.RepairVerification";
    public const string Folder = "Docs/Boss/RepairValidation";
    private static readonly Stack<IEnumerator> jobs = new Stack<IEnumerator>();
    private static readonly List<string> report = new List<string>();
    private static readonly List<string> errors = new List<string>();
    private static bool finished;
    private static double start;
    private static bool Baseline => SessionState.GetBool(Key + ".Baseline", false);
    static HiveTyrantRepairVerification()
    {
        EditorApplication.playModeStateChanged += Changed;
        EditorApplication.update += Tick;
        Application.logMessageReceived += Log;
    }
    public static void RunBaseline() { Begin(true); }
    public static void RunRepair() { Begin(false); }
    public static void BuildRepairWindows()
    {
        const string output = "D:/AlienPurge/Builds/HiveTyrantRepair/AlienPurge.exe";
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var build = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { HiveTyrantCombatSetup.TestScene }, locationPathName = output,
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development | BuildOptions.StrictMode
        });
        Directory.CreateDirectory(Folder);
        File.WriteAllLines(Folder + "/WindowsBuild_Result.txt", new[] { build.summary.result.ToString(), "Output=" + output,
            "Errors=" + build.summary.totalErrors + " Warnings=" + build.summary.totalWarnings, "Duration=" + build.summary.totalTime });
        if (build.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new InvalidOperationException("Repair build failed");
    }
    private static void Begin(bool baseline)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        Directory.CreateDirectory(Folder);
        EditorSceneManager.OpenScene(HiveTyrantCombatSetup.TestScene);
        SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Baseline", baseline);
        EditorApplication.isPlaying = true;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            finished = false; start = EditorApplication.timeSinceStartup; jobs.Clear(); report.Clear(); errors.Clear();
            Application.runInBackground = true;
            var gameView = EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")); gameView.Show(); gameView.Focus();
            jobs.Push(Verify());
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            if (errors.Count > 0) { File.AppendAllLines(Folder + "/" + (Baseline ? "Before" : "After") + "_Result.txt", errors); SessionState.SetInt(Key + ".Exit", 1); }
            EditorApplication.Exit(SessionState.GetInt(Key + ".Exit", 1));
        }
    }
    private static void Log(string message, string stack, LogType type)
    {
        if (!SessionState.GetBool(Key, false) || (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
        if (stack.Contains("AstarUpdateChecker")) { report.Add("Existing A* Editor update-check diagnostic: " + message); EditorApplication.isPaused = false; }
        else errors.Add(message + "\n" + stack);
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || finished) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (jobs.Count == 0) Changed(PlayModeStateChange.EnteredPlayMode);
        try
        {
            if (EditorApplication.isPaused)
            {
                if (errors.Count > 0 || !report.Any(line => line.StartsWith("Existing A* Editor update-check")))
                    throw new InvalidOperationException("Editor paused during verification.");
                EditorApplication.isPaused = false; return;
            }
            if (EditorApplication.timeSinceStartup - start > 180) throw new TimeoutException("Repair verification timed out.");
            var job = jobs.Peek();
            if (!job.MoveNext()) { jobs.Pop(); if (jobs.Count == 0) Complete(true); }
            else if (job.Current is IEnumerator nested) jobs.Push(nested);
        }
        catch (Exception e) { report.Add("FAIL " + e); Complete(false); }
    }
    private static void Complete(bool success)
    {
        finished = true; success &= errors.Count == 0;
        report.Insert(0, (success ? "PASSED" : "FAILED") + " actual Unity Editor Play Mode " + Application.unityVersion + "; " + SystemInfo.graphicsDeviceType);
        report.AddRange(errors);
        File.WriteAllLines(Folder + "/" + (Baseline ? "Before" : "After") + "_Result.txt", report);
        SessionState.SetInt(Key + ".Exit", success ? 0 : 1);
        Debug.Log("[HiveTyrantRepair] " + report[0]); EditorApplication.isPlaying = false;
    }
    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); report.Add("PASS " + message); }
    private static IEnumerator Wait(float seconds) { float until = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < until) yield return null; }
    private static IEnumerator Capture(string label)
    {
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder + "/" + (Baseline ? "Before_" : "After_") + label + ".png"));
        yield return Wait(.35f);
        Check(File.Exists(Folder + "/" + (Baseline ? "Before_" : "After_") + label + ".png"), "Rendered screenshot saved: " + label);
    }
    private static IEnumerator Verify()
    {
        yield return Wait(2);
        var boss = UnityEngine.Object.FindObjectOfType<HiveTyrantCombat>();
        var player = UnityEngine.Object.FindObjectOfType<PlayerHealth>();
        var camera = Camera.main;
        Check(boss != null && player != null && camera != null, "Original test scene loaded");
        boss.AutomaticSkills = false; player.GetComponent<PlayerMovement>().SetCanMove(false);
        if (!Baseline)
        {
            var debug = UnityEngine.Object.FindObjectOfType<HiveTyrantCombatDebug>();
            Check(!debug.ControlsVisible, "Large debug panel is hidden by default");
            debug.ToggleControls(); Check(debug.ControlsVisible, "F8 control action opens the panel");
            debug.ToggleControls(); Check(!debug.ControlsVisible, "F8 control action closes the panel");
            Check(AstarPath.active.logPathResults == Pathfinding.PathLog.OnlyErrors, "Native pathfinding keeps errors without per-path Console spam");
        }
        // Position only once to arrange the test; measurement starts after the camera settles.
        Vector2 position = boss.Motor.CombatOrigin + Vector2.down * 7;
        player.GetComponent<Rigidbody2D>().position = position; player.transform.position = position;
        boss.Motor.BeginAction(1, Vector2.down); Physics2D.SyncTransforms();
        yield return Wait(2);
        var probe = camera.gameObject.AddComponent<HiveTyrantRepairFrameProbe>(); probe.player = player; probe.boss = boss;
        yield return Wait(.5f);
        yield return Capture("HUD");
        boss.Motor.EndAction();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Check(boss.RequestSkill(HiveTyrantSkill.Slam, true), "F2 accepted from existing world positions");
        watch.Stop(); report.Add("F2 request CPU milliseconds: " + watch.Elapsed.TotalMilliseconds.ToString("F3"));
        yield return Wait(.6f); yield return Capture("WaveWarning");
        yield return Wait(4.5f);
        probe.recording = false;
        report.Add("Frame samples=" + probe.frameMs.Count + "; max camera world step=" + probe.maxCameraStep.ToString("F4") + "; max player world step=" + probe.maxPlayerStep.ToString("F4") + "; max stationary-player viewport step=" + probe.maxViewportStep.ToString("F4") + "; max ortho-size step=" + probe.maxSizeStep.ToString("F4"));
        var times = probe.frameMs.OrderBy(x => x).ToArray();
        report.Add("Frame ms median=" + times[times.Length / 2].ToString("F2") + "; p95=" + times[(int)(times.Length * .95f)].ToString("F2") + "; max=" + times.Last().ToString("F2") + "; screenshot frames included.");
        File.WriteAllLines(Folder + "/" + (Baseline ? "Before" : "After") + "_Frames.csv", probe.rows);
        Check(probe.maxPlayerStep < .05f, "F2 never teleports the stationary player");
        if (!Baseline) Check(probe.maxViewportStep < .02f && probe.maxCameraStep < .3f, "F2 warning/release/end have no camera cut");
        var managerData = new SerializedObject(GameManager.Instance);
        report.Add("GameManager gameOverPanel assigned=" + (managerData.FindProperty("gameOverPanel").objectReferenceValue != null));
        player.TakeDamage(999);
        yield return Wait(.6f);
        Check(player.IsDead && GameManager.Instance.IsGameOver && Time.timeScale == 0, "Death still follows the original GameManager result/pause flow");
        yield return Capture("Death");
        if (!Baseline)
        {
            var retry = GameObject.Find("HiveTyrant_TestResult");
            Check(retry != null && retry.activeInHierarchy, "Dedicated test death overlay visible while timeScale is zero");
            var button = retry.GetComponentInChildren<Button>();
            Check(button != null && button.interactable, "Retry button is available after death");
            var input = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position) + new Vector2(20, -20), button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(input, hits);
            Check(hits.Count > 0 && hits[0].gameObject == button.gameObject, "Visible retry button receives UI raycasts above the death shade");
            ExecuteEvents.Execute(button.gameObject, input, ExecuteEvents.pointerClickHandler);
        }
        else HiveTyrantCombatDebug.RestartScene();
        yield return Wait(2);
        Check(Time.timeScale == 1 && GameManager.Instance.IsPlaying && !UnityEngine.Object.FindObjectOfType<PlayerHealth>().IsDead, "Restart restores time, original HP and gameplay");
        if (!Baseline) yield return AdditionalChecks();
    }
    private static IEnumerator AdditionalChecks()
    {
        var boss = UnityEngine.Object.FindObjectOfType<HiveTyrantCombat>(); var player = UnityEngine.Object.FindObjectOfType<PlayerHealth>();
        Camera.main.gameObject.AddComponent<HiveTyrantRepairFrameProbe>();
        var hud = boss.GetComponent<HiveTyrantHealthBar>();
        Check(UnityEngine.Object.FindObjectsOfType<Canvas>().Count(c => c.name == "HiveTyrant_BossHUD") == 1 &&
              UnityEngine.Object.FindObjectsOfType<Canvas>().Count(c => c.name == "HiveTyrant_TestHUD") == 1 &&
              UnityEngine.Object.FindObjectsOfType<EventSystem>().Length == 1, "Retry leaves one Boss HUD, test HUD and input event system");
        var ui = UnityEngine.Object.FindObjectOfType<HiveTyrantCombatDebug>();
        ui.TriggerPhase2(); yield return Wait(2.2f);
        Check(hud.Panel.Find("Health").GetComponent<Text>().text == boss.Health.CurrentHealth + " / " + boss.Health.MaxHealth &&
            hud.Panel.Find("Phase").GetComponent<Text>().text == "PHASE 2" && hud.Panel.Find("Subtitle").GetComponent<Text>().text == "RIFTWING", "HUD displays authoritative health and Riftwing phase");
        yield return Capture("Phase2HUD");
        ui.ToggleControls(); yield return Capture("Controls"); ui.ToggleControls();
        // Render the actual overlay with different CanvasScaler inputs; no scene/UI asset is rewritten.
        var canvas = hud.Panel.GetComponentInParent<Canvas>(); var scaler = canvas.GetComponent<CanvasScaler>();
        float originalScale = canvas.scaleFactor; scaler.enabled = false;
        foreach (Vector2 resolution in new[] { new Vector2(1280,720), new Vector2(1920,1080), new Vector2(1024,768) })
        {
            float scale = Mathf.Pow(2, (Mathf.Log(resolution.x / 1600, 2) + Mathf.Log(resolution.y / 900, 2)) * .5f);
            Check(680 * scale < resolution.x - 24 && 116 * scale < resolution.y * .22f, "HUD safe margins at " + resolution);
        }
        canvas.scaleFactor = originalScale; scaler.enabled = true;
        EditorSceneManager.LoadSceneInPlayMode(HiveTyrantCombatSetup.WorldScene, new LoadSceneParameters(LoadSceneMode.Single));
        yield return Wait(2);
        Check(UnityEngine.Object.FindObjectOfType<HiveTyrantCombatDebug>() == null && GameObject.Find("HiveTyrant_TestHUD") == null, "Formal Level01 has no debug panel, test hint or test-only result UI");
        Check(new SerializedObject(GameManager.Instance).FindProperty("gameOverPanel").objectReferenceValue != null, "Formal scene retains its original configured game-over interface");
        var world = UnityEngine.Object.FindObjectOfType<AlienPurge.Level01ConnectedV01.L01World>();
        boss = world.boss.GetComponent<HiveTyrantCombat>(); player = world.player.GetComponent<PlayerHealth>();
        boss.AutomaticSkills = false; world.progression.Complete("Research");
        var framing = Camera.main.GetComponent<HiveTyrantArenaCamera>(); framing.enabled = false;
        Vector2 point = world.World(new Vector2(0, -10));
        player.GetComponent<Rigidbody2D>().position = point; player.transform.position = point;
        player.GetComponent<PlayerMovement>().SetCanMove(false); Physics2D.SyncTransforms();
        // The fixture skips the walk across Level01. Let normal follow catch up to that setup teleport.
        yield return Wait(1.2f); boss.AutomaticSkills = false; framing.enabled = true;
        float until = Time.realtimeSinceStartup + 10;
        while (boss.Motor.HasActionOwner && Time.realtimeSinceStartup < until) yield return null;
        var camera = Camera.main; var probe = camera.gameObject.AddComponent<HiveTyrantRepairFrameProbe>(); probe.player = player; probe.boss = boss;
        yield return Wait(2);
        Check(world.arenaBridge.encounterStarted && boss.RequestSkill(HiveTyrantSkill.Slam, true), "Formal entrance activates the same F2 at its existing arena position");
        yield return Wait(.6f); yield return Capture("Level01Wave");
        Bounds bounds = boss.GetComponent<HiveTyrantPresentation>().VisualBounds;
        Vector3 low = camera.WorldToViewportPoint(bounds.min), high = camera.WorldToViewportPoint(bounds.max);
        Check(low.x >= 0 && low.y >= 0 && high.x <= 1 && high.y <= 1, "Formal F2 keeps the full Boss silhouette on screen");
        yield return Wait(3);
        report.Add("Formal F2 max stationary-player viewport step=" + probe.maxViewportStep.ToString("F4") + "; player world step=" + probe.maxPlayerStep.ToString("F4"));
        Check(probe.maxPlayerStep < .05f && probe.maxViewportStep < .02f, "Formal F2 also preserves player position and continuous framing");
    }
}

[DefaultExecutionOrder(30000)]
public sealed class HiveTyrantRepairFrameProbe : MonoBehaviour
{
    public PlayerHealth player;
    public HiveTyrantCombat boss;
    public bool recording = true;
    public float maxCameraStep, maxPlayerStep, maxViewportStep, maxSizeStep;
    public readonly List<float> frameMs = new List<float>();
    public readonly List<string> rows = new List<string> { "frame,state,dt_ms,camera_x,camera_y,size,player_x,player_y,viewport_step" };
    private Vector3 lastCamera, lastPlayer, lastViewport;
    private float lastSize;
    private bool first = true;
    // A batch Editor without IMGUI scripts can skip Game View repainting entirely.
    // This test-only callback requests real rendering without drawing any extra UI.
    private void OnGUI() { }
    private void LateUpdate()
    {
        if (!recording || player == null || boss == null) return;
        var camera = GetComponent<Camera>(); var position = player.transform.position; var viewport = camera.WorldToViewportPoint(position);
        float step = first ? 0 : Vector2.Distance(viewport, lastViewport);
        if (!first)
        {
            maxCameraStep = Mathf.Max(maxCameraStep, Vector3.Distance(transform.position, lastCamera));
            maxPlayerStep = Mathf.Max(maxPlayerStep, Vector3.Distance(position, lastPlayer));
            maxViewportStep = Mathf.Max(maxViewportStep, step);
            maxSizeStep = Mathf.Max(maxSizeStep, Mathf.Abs(camera.orthographicSize - lastSize));
            frameMs.Add(Time.unscaledDeltaTime * 1000);
        }
        rows.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1},{2:F3},{3:F5},{4:F5},{5:F5},{6:F5},{7:F5},{8:F5}", Time.frameCount, boss.State, Time.unscaledDeltaTime * 1000, transform.position.x, transform.position.y, camera.orthographicSize, position.x, position.y, step));
        first = false; lastCamera = transform.position; lastPlayer = position; lastViewport = viewport; lastSize = camera.orthographicSize;
    }
}
