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
        var pilot = player.gameObject.AddComponent<HiveTyrantPlaytestPilot>(); pilot.Boss = boss; pilot.Battle = true;
        boss.AutomaticSkills = true;
        report.Add("Windows standalone actual runtime; " + Application.unityVersion + "; " + SystemInfo.graphicsDeviceName);
        report.Add("Initial player HP=" + player.CurrentHealth + "/" + player.MaxHealth + ", speed=" + player.GetComponent<PlayerMovement>().MoveSpeed + "; original weapon and native movement; input automation only.");
        float began = Time.time, deadline = Time.realtimeSinceStartup + 240, next = 0;
        int phases = 0, summons = 0, releases = 0;
        while (boss != null && !boss.Health.IsDead && !player.IsDead && Time.realtimeSinceStartup < deadline)
        {
            phases = boss.PhaseTransitionCount; summons = boss.TotalSummoned; releases = boss.ReleaseCount;
            frames.Add(Time.unscaledDeltaTime * 1000);
            string state = (boss.Phase2Entered ? "P2_" : "P1_") + boss.State;
            if (boss.State != HiveTyrantState.Chase && captured.Add(state)) Capture(state);
            if (Time.time >= next)
            { report.Add("t=" + (Time.time - began).ToString("F1") + " Player=" + player.CurrentHealth + " Boss=" + boss.Health.CurrentHealth + " summons=" + summons); next = Time.time + 10; }
            yield return null;
        }
        bool won = !player.IsDead && (boss == null || boss.Health.IsDead);
        if (boss != null && boss.Health.IsDead) { yield return new WaitForSeconds(.4f); Capture("Death"); }
        yield return new WaitForSecondsRealtime(2);
        frames.Sort();
        report.Add("Outcome won=" + won + ", playerHP=" + player.CurrentHealth + ", elapsed=" + (Time.time - began).ToString("F1") + ", phases=" + phases + ", summons=" + summons + ", releases=" + releases + ", shots=" + pilot.Shots);
        report.Add("Median frame ms=" + frames[frames.Count / 2].ToString("F2") + "; P95=" + frames[(int)(frames.Count * .95f)].ToString("F2") + "; screenshot frames included; no hardware-wide performance claim.");
        report.Add("Errors=" + errors + "; encounter FX roots remaining=" + FindObjectsOfType<Transform>().Count(t => t.name == "HiveTyrant_EncounterEffects"));
        bool success = won && phases == 1 && errors == 0;
        report.Insert(0, success ? "PASSED" : "FAILED"); File.WriteAllLines(Path.Combine(directory, "Standalone_Result.txt"), report);
        Application.logMessageReceived -= RecordError; Application.Quit(success ? 0 : 1);
    }
    private void RecordError(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { errors++; report.Add(message + "\n" + stack); } }
    private void Capture(string name)
    {
        var camera = Camera.main; var target = new RenderTexture(1280, 720, 24); var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        var old = camera.targetTexture; var active = RenderTexture.active; camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply(); File.WriteAllBytes(Path.Combine(directory, name + ".png"), image.EncodeToPNG());
        camera.targetTexture = old; RenderTexture.active = active; target.Release(); Destroy(target); Destroy(image);
    }
}
#endif
