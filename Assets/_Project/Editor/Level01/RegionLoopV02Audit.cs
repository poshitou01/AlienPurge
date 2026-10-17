using System;
using System.IO;
using System.Linq;
using AlienPurge.Level01.RegionLoop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AlienPurge.Level01.Editor
{
    [InitializeOnLoad]
    public static class RegionLoopV02Audit
    {
        const string Key = "RegionV02.Audit";
        static int stage;
        static double deadline;
        static float started;
        static int before;
        static ExperienceOrb orb;
        static RegionLoopController loop;
        static string report;
        static RegionLoopV02Audit()
        {
            EditorApplication.playModeStateChanged += mode =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                if (mode == PlayModeStateChange.EnteredPlayMode) { stage = 1; deadline = EditorApplication.timeSinceStartup + 80; report = "V01 real death / pickup baseline audit\n"; }
                if (mode == PlayModeStateChange.EnteredEditMode)
                {
                    PlayerSettings.productName = SessionState.GetString(Key + ".product", "AlienPurge");
                    SessionState.SetBool(Key, false);
                    if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool(Key + ".failed", false) ? 1 : 0);
                }
            };
            EditorApplication.update += Tick;
        }
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play first.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
            SessionState.SetString(Key + ".product", PlayerSettings.productName);
            PlayerSettings.productName = "AlienPurge_RegionLoopV02_Audit";
            var scene = EditorSceneManager.OpenScene(RegionLoopInstaller.Target);
            RegionLoopInstaller.CloneSceneFonts(scene, RegionLoopV02Installer.DataFolder + "/LegacyAuditFonts");
            SessionState.SetBool(Key, true);
            SessionState.SetBool(Key + ".failed", false);
            EditorApplication.isPlaying = true;
        }
        static void Tick()
        {
            if (stage == 0 || !EditorApplication.isPlaying) return;
            EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.isPaused) EditorApplication.isPaused = false;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) { Finish("TIMEOUT"); return; }
                if (stage == 1)
                {
                    if (!Application.persistentDataPath.Contains("AlienPurge_RegionLoopV02_Audit")) throw new InvalidOperationException("Audit profile not isolated");
                    loop = UnityEngine.Object.FindObjectOfType<RegionLoopController>();
                    if (loop == null || loop.Regions.Count == 0) return;
                    var ticket = loop.Regions[0].enemies.FirstOrDefault(t => t.lease != null);
                    if (ticket == null) return;
                    loop.world.player.GetComponent<PlayerHealth>().BeginTemporaryInvulnerability(1000);
                    var xp = loop.rewards.experience;
                    before = xp.CurrentExperience;
                    report += "Profile: " + Application.persistentDataPath + "\nXP curve: " + EditorJsonUtility.ToJson(xp) + "\n";
                    report += "Player colliders: " + string.Join(" | ", loop.world.player.GetComponentsInChildren<Collider2D>().Select(c => c.name + ": " + c.bounds + " tag=" + c.tag)) + "\n";
                    report += "Death XP expected: " + ticket.lease.Health.ExperienceAmount + "\n";
                    ticket.lease.Health.TakeDamage(int.MaxValue);
                    started = Time.time; stage = 2;
                }
                else if (stage == 2 && Time.time > started + .2f)
                {
                    orb = UnityEngine.Object.FindObjectOfType<ExperienceOrb>();
                    if (orb == null) { Finish("FAIL: real death produced no active orb"); return; }
                    report += "Orb: " + orb.transform.position + " exp=" + orb.ExperienceAmount + "\n";
                    loop.world.player.transform.position = orb.transform.position;
                    Physics2D.SyncTransforms(); started = Time.time; stage = 3;
                }
                else if (stage == 3 && Time.time > started + 2)
                {
                    var xp = loop.rewards.experience;
                    report += "After pickup: xp=" + xp.CurrentExperience + " level=" + xp.CurrentLevel + " queue=" + loop.rewards.PendingCount + " orbActive=" + (orb != null && orb.gameObject.activeInHierarchy) + "\n";
                    Finish(xp.CurrentExperience > before || xp.CurrentLevel > 1 ? "PASS: native death/drop/physics pickup/AddExperience chain" : "FAIL: native pickup did not add XP");
                }
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }
        static void Finish(string message)
        {
            stage = 0;
            SessionState.SetBool(Key + ".failed", !message.StartsWith("PASS:"));
            Directory.CreateDirectory("Docs/Level01/V02");
            File.WriteAllText("Docs/Level01/V02/ExperienceBaseline.txt", report + message + "\n");
            EditorApplication.isPlaying = false;
        }
    }
}
