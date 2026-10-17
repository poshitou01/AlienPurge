#if DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// Only explicit development-player command-line runs create this harness. No saved
// scene components, new gameplay shortcuts or release-build behaviour.
public sealed class HiveTyrantStandaloneProbe : MonoBehaviour
{
    private readonly List<string> report = new List<string>();
    private readonly HashSet<string> captured = new HashSet<string>();
    private readonly List<float> frames = new List<float>(16000);
    private string directory;
    private int errors;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Launch()
    {
        if (!Environment.GetCommandLineArgs().Contains("-hivetyrant-smoke")) return;
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "HiveTyrant_CombatTest") return;
        new GameObject("HiveTyrant explicit standalone verification").AddComponent<HiveTyrantStandaloneProbe>();
    }
    private IEnumerator Start()
    {
        directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Validation")); Directory.CreateDirectory(directory);
        Application.runInBackground = true; Application.logMessageReceived += RecordError;
        yield return new WaitForSeconds(1.5f);
        var boss = FindObjectOfType<HiveTyrantCombat>(); var player = FindObjectOfType<PlayerHealth>();
        int testTier = Environment.GetCommandLineArgs().Contains("-hivetyrant-thunder") ? 3 :
            Environment.GetCommandLineArgs().Contains("-hivetyrant-cluster") ? 2 : Environment.GetCommandLineArgs().Contains("-hivetyrant-upgraded") ? 1 : 0;
        if (testTier > 0) { HiveTyrantTestLoadouts.Apply(player, testTier); boss.RestartEncounter(); boss.CaptureEntryLoadout(); }
        HiveTyrantCombatDebug.RequestedTestTier = testTier;
        if (Environment.GetCommandLineArgs().Contains("-hivetyrant-visual"))
        {
            // A separate visual fixture, never counted as an unaided full battle.
            player.GetComponent<PlayerMovement>().SetCanMove(false);
            Capture("Visual_HUD"); yield return new WaitForSeconds(.15f);
            boss.Health.TakeDamage(boss.Health.CurrentHealth - boss.Health.MaxHealth / 2);
            yield return new WaitForSeconds(boss.Config.phaseTransitionTime * .52f);
            Capture("Visual_Phase2Reveal");
            yield return new WaitForSeconds(boss.Config.phaseTransitionTime * .5f);
            Capture("Visual_Phase2HUD");
            bool ok = errors == 0 && boss.PhaseTransitionCount == 1 && boss.Phase2Entered;
            File.WriteAllLines(Path.Combine(directory, "Standalone_Visual_Result.txt"), new[] {
                ok ? "PASSED" : "FAILED", "Actual Windows runtime camera + existing UI, rendered offscreen at 1280x720.",
                "Hidden windows skip normal backbuffer rendering. Overlay canvases temporarily bind to the camera only for capture, then restore.",
                "Visual-only threshold fixture; not a full-battle performance or difficulty test.", "Errors=" + errors });
            Application.logMessageReceived -= RecordError; Application.Quit(ok ? 0 : 1); yield break;
        }
        var pilot = player.gameObject.AddComponent<HiveTyrantPlaytestPilot>(); pilot.Boss = boss; pilot.Battle = true;
        bool authoredAuto = boss.AutomaticSkills;
        report.Add("Windows standalone actual runtime; " + Application.unityVersion + "; " + SystemInfo.graphicsDeviceName);
        report.Add("Default automatic AI=" + authoredAuto + "; no skill/debug keys or AI override.");
        report.Add("Loadout tier=" + testTier + "; " + HiveTyrantTestLoadouts.Describe(player) + "; native weapon/movement, input automation only; no battle healing.");
        report.Add("Boss starting HP=" + boss.Health.MaxHealth + "; entry power=" + boss.EntryPower);
        float began = Time.time, deadline = Time.realtimeSinceStartup + 240, next = 0;
        int phases = 0, summons = 0, releases = 0;
        while (boss != null && !boss.Health.IsDead && !player.IsDead && Time.realtimeSinceStartup < deadline)
        {
            phases = boss.PhaseTransitionCount; summons = boss.TotalSummoned; releases = boss.ReleaseCount;
            frames.Add(Time.unscaledDeltaTime * 1000);
            string state = (boss.Phase2Entered ? "P2_" : "P1_") + boss.State;
            if (boss.State != HiveTyrantState.Chase && captured.Add(state)) Capture(state);
            if (boss.State == HiveTyrantState.PhaseTransition && boss.Motor.IsPhase2 && captured.Add("Phase2_Reveal")) Capture("Phase2_Reveal");
            if (Time.time >= next)
            { report.Add("t=" + (Time.time - began).ToString("F1") + " Player=" + player.CurrentHealth + " Boss=" + boss.Health.CurrentHealth + " summons=" + summons); next = Time.time + 10; }
            yield return null;
        }
        bool won = !player.IsDead && (boss == null || boss.Health.IsDead);
        if (boss != null && boss.Health.IsDead) { yield return new WaitForSeconds(.4f); Capture("Death"); }
        yield return new WaitForSecondsRealtime(2);
        frames.Sort();
        report.Add("Outcome won=" + won + ", playerHP=" + player.CurrentHealth + ", elapsed=" + (Time.time - began).ToString("F1") + ", phases=" + phases + ", summons=" + summons + ", releases=" + releases + ", shots=" + pilot.Shots + ", jumps=" + pilot.Jumps);
        report.Add("Hidden-window simulation timing median=" + frames[frames.Count / 2].ToString("F2") + "; P95=" + frames[(int)(frames.Count * .95f)].ToString("F2") + " ms. Not rendering FPS; only explicit captures render camera+UI.");
        report.Add("Errors=" + errors + "; encounter FX roots remaining=" + FindObjectsOfType<Transform>().Count(t => t.name == "HiveTyrant_EncounterEffects"));
        bool success = authoredAuto && won && phases == 1 && errors == 0;
        report.Insert(0, success ? "PASSED" : "FAILED"); File.WriteAllLines(Path.Combine(directory, "Standalone_Result.txt"), report);
        Application.logMessageReceived -= RecordError; Application.Quit(success ? 0 : 1);
    }
    private void RecordError(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { errors++; report.Add(message + "\n" + stack); } }
    private void Capture(string name)
    {
        // A hidden development player can skip its backbuffer. Explicitly render the
        // actual camera and existing UI without depending on OS window visibility.
        var camera = Camera.main; var target = new RenderTexture(1280, 720, 24);
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
        var overlays = FindObjectsOfType<Canvas>().Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        var previousCameras = overlays.Select(c => c.worldCamera).ToArray();
        var previousDistances = overlays.Select(c => c.planeDistance).ToArray();
        var previousLayers = overlays.Select(c => c.sortingLayerID).ToArray();
        int topLayer = SortingLayer.layers.OrderBy(layer => layer.value).Last().id;
        try
        {
            camera.targetTexture = target;
            foreach (var canvas in overlays)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera;
                canvas.planeDistance = camera.nearClipPlane + .1f;
                // Overlay canvases normally ignore world sorting. Keep them above the arena.
                canvas.sortingLayerID = topLayer;
            }
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            if (image.GetPixel(640, 360).maxColorComponent < .05f) { errors++; report.Add("Black capture: " + name); }
            var boss = FindObjectOfType<HiveTyrantCombat>();
            if (boss != null && boss.Health.CurrentHealth > 0)
            {
                var fill = boss.GetComponent<HiveTyrantHealthBar>().Panel.Find("HealthFill").GetComponent<RectTransform>();
                Vector3 pixel = camera.WorldToScreenPoint(fill.TransformPoint(fill.rect.center));
                Color sample = image.GetPixel(Mathf.Clamp(Mathf.RoundToInt(pixel.x), 0, 1279), Mathf.Clamp(Mathf.RoundToInt(pixel.y), 0, 719));
                if (sample.r < sample.g * 1.2f || sample.r < sample.b * 2)
                { errors++; report.Add("HUD fill missing from capture: " + name); }
            }
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
            for (int i = 0; i < overlays.Length; i++)
            {
                overlays[i].renderMode = RenderMode.ScreenSpaceOverlay; overlays[i].worldCamera = previousCameras[i];
                overlays[i].planeDistance = previousDistances[i]; overlays[i].sortingLayerID = previousLayers[i];
            }
            Canvas.ForceUpdateCanvases(); target.Release(); Destroy(target); Destroy(image);
        }
    }
}
#endif
