using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AlienPurge.Level01.RegionLoop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AlienPurge.Level01.Editor
{
    // Uses the real copied scene, pool, controller and UI buttons. No production debug bypasses.
    // Combat is deterministic: this is a behavior regression, not a balance/aiming playtest.
    [InitializeOnLoad]
    public static class RegionLoopVerification
    {
        const string Running = "RegionLoop.Verification.Running";
        const string Result = "Docs/Level01/RegionLoop_PlayMode.txt";
        static IEnumerator routine;
        static RegionLoopController controller;
        static double deadline;
        static double nextDiagnostic;
        static readonly List<string> results = new List<string>();
        static readonly List<string> runtimeErrors = new List<string>();
        static bool autoChoose, failed;
        static RegionLoopVerification()
        {
            EditorApplication.playModeStateChanged += ModeChanged;
            EditorApplication.update += Update;
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (routine != null && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                    runtimeErrors.Add(message + "\n" + stack);
            };
        }

        [MenuItem("AlienPurge/Gameplay/Level01/Run Region Loop V01 Regression")]
        public static void Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            results.Clear();
            RegionLoopInstaller.Install();
            string guid = AssetDatabase.AssetPathToGUID(RegionLoopInstaller.Target);
            var scene = SceneManager.GetActiveScene();
            int objects = RegionLoopInstaller.All<Transform>(scene).Length;
            var anchor = RegionLoopInstaller.All<RegionAnchor>(scene).First();
            string anchorId = anchor.stableId;
            Vector3 priorPosition = anchor.transform.position;
            var definition = RegionLoopInstaller.All<RegionLoopController>(scene).Single().definitions[1];
            int priorHealth = definition.enemies[0].health, priorWaveCount = definition.waves[0].groups[0].count;
            try
            {
                anchor.transform.position += new Vector3(.01f, 0, 0);
                definition.enemies[0].health += 7; definition.waves[0].groups[0].count++;
                EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                RegionLoopInstaller.Install();
                scene = SceneManager.GetActiveScene();
                Require(AssetDatabase.AssetPathToGUID(RegionLoopInstaller.Target) == guid, "Repeat install preserves scene GUID");
                Require(RegionLoopInstaller.All<Transform>(scene).Length == objects, "Repeat install creates no duplicate objects");
                Require(RegionLoopInstaller.All<RegionAnchor>(scene).Single(a => a.stableId == anchorId).transform.position == priorPosition + new Vector3(.01f, 0, 0), "Repeat install retains a deliberately edited anchor position");
                Require(definition.enemies[0].health == priorHealth + 7 && definition.waves[0].groups[0].count == priorWaveCount + 1,
                    "Repeat install retains deliberately edited enemy attributes and wave counts");
            }
            finally
            {
                scene = SceneManager.GetActiveScene();
                var restored = RegionLoopInstaller.All<RegionAnchor>(scene).FirstOrDefault(a => a.stableId == anchorId);
                if (restored != null) restored.transform.position = priorPosition;
                definition.enemies[0].health = priorHealth; definition.waves[0].groups[0].count = priorWaveCount;
                EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            RegionLoopInstaller.Validate();
            SessionState.SetString("RegionLoop.Verification.EditorChecks", string.Join("\n", results));
            File.WriteAllText(Result, "RUNNING: Editor install/idempotence passed. Play Mode results follow.\n");
            // The project's bootstrap saves on Play Mode exit. Isolate the regression profile.
            SessionState.SetString("RegionLoop.Verification.ProductName", PlayerSettings.productName);
            File.WriteAllText("Logs/RegionLoop_TestProductName.txt", PlayerSettings.productName);
            PlayerSettings.productName = "AlienPurge_RegionLoopV01_Regression";
            SessionState.SetBool(Running, true);
            SessionState.SetBool("RegionLoop.Verification.Batch", Application.isBatchMode);
            EditorApplication.isPlaying = true;
        }
        static void ModeChanged(PlayModeStateChange mode)
        {
            if (!SessionState.GetBool(Running, false)) return;
            if (mode == PlayModeStateChange.EnteredPlayMode)
            {
                results.Clear(); runtimeErrors.Clear(); failed = false; autoChoose = false;
                results.Add(SessionState.GetString("RegionLoop.Verification.EditorChecks", ""));
                Application.runInBackground = true;
                var view = EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));
                view.Show(); view.Focus();
                deadline = EditorApplication.timeSinceStartup + 300;
                routine = Exercise();
            }
            if (mode == PlayModeStateChange.EnteredEditMode)
            {
                PlayerSettings.productName = SessionState.GetString("RegionLoop.Verification.ProductName", "AlienPurge");
                SessionState.SetBool(Running, false);
                if (SessionState.GetBool("RegionLoop.Verification.Batch", false))
                    EditorApplication.Exit(SessionState.GetBool("RegionLoop.Verification.Failed", false) ? 1 : 0);
            }
        }
        static void Update()
        {
            if (routine == null || !Application.isPlaying) return;
            try
            {
                EditorApplication.QueuePlayerLoopUpdate();
                if (runtimeErrors.Count > 0) throw new InvalidOperationException("Runtime error(s):\n" + string.Join("\n", runtimeErrors));
                if (EditorApplication.isPaused)
                {
                    results.Add("NOTE: Own batch Editor paused without a runtime error; resumed test player loop.");
                    EditorApplication.isPaused = false;
                    return;
                }
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Regression exceeded 300 seconds.");
                if (EditorApplication.timeSinceStartup > nextDiagnostic)
                {
                    nextDiagnostic = EditorApplication.timeSinceStartup + 5;
                    File.WriteAllText("Logs/RegionRegression_State.txt", "timeScale=" + Time.timeScale + " gameTime=" + Time.time +
                        " active=" + (controller != null) + " choosing=" + UpgradeManager.IsChoosingUpgrade + "/" + WeaponModuleSelectionManager.IsChoosingModule +
                        (controller == null ? "" : " queue=" + controller.rewards.PendingCount + "\n" + string.Join("\n", controller.Regions.Select(s => s.config.regionId + " " + s.objective + " remaining=" + s.Remaining(RegionEnemyRole.Resident) + "/" + s.Remaining(RegionEnemyRole.Mission) + " diagnostic=" + s.diagnostic + "\n" + string.Join(" ", s.enemies.Select(t => t.id + ":dead=" + t.dead + ",lease=" + (t.lease != null) + ",hp=" + (t.lease != null ? t.lease.Health.CurrentHealth : t.savedHealth)))))));
                }
                if (controller != null && !controller.ShuttingDown)
                {
                    var health = controller.world.player.GetComponent<PlayerHealth>();
                    if (!health.IsTemporarilyInvulnerable) health.BeginTemporaryInvulnerability(10000);
                    Require(!(UpgradeManager.IsChoosingUpgrade && WeaponModuleSelectionManager.IsChoosingModule), "Panels never overlap", false);
                    if (autoChoose) ChooseVisibleCard();
                    if (!UpgradeManager.IsChoosingUpgrade && !WeaponModuleSelectionManager.IsChoosingModule && Time.timeScale > 0) Time.timeScale = 30;
                }
                if (!routine.MoveNext()) Finish(false);
            }
            catch (Exception exception)
            {
                results.Add("FAIL: " + exception); Debug.LogError("[RegionLoopTest] " + exception); Finish(true);
            }
        }
        static void Finish(bool failure)
        {
            failed = failure; routine = null;
            File.WriteAllText(Result, (failed ? "FAIL\n" : "PASS\n") + "Unity " + Application.unityVersion + " | " + DateTime.UtcNow.ToString("u") + "\n" + string.Join("\n", results) + "\n");
            SessionState.SetBool("RegionLoop.Verification.Failed", failed);
            Time.timeScale = 1; EditorApplication.isPlaying = false;
        }
        static void Require(bool value, string message, bool record = true)
        {
            if (!value) throw new InvalidOperationException(message);
            if (record) { results.Add("PASS: " + message); Debug.Log("[RegionLoopTest] " + message); }
        }
        static IEnumerator Exercise()
        {
            yield return null;
            controller = UnityEngine.Object.FindObjectOfType<RegionLoopController>();
            Require(controller != null, "Copied scene owns explicit region mode");
            var entry = controller.State("ColonyEntry");
            while (!entry.initialized || entry.enemies.All(t => t.lease == null)) yield return null;
            Require(entry.enemies.Where(t => t.lease != null).All(t => t.lease.GetComponent<EnemyDefinition>().HasBeenInitialized), "Native enemy definitions initialized for attack behavior");
            var gates = controller.world.progression.gates;
            Require(gates.Where(g => new[] { "G1", "G2", "G3" }.Contains(g.gateId)).All(g => g.locked), "G1-G3 closed before missions");
            Require(!controller.TryActivate("ColonyEntry"), "Arrival cannot bypass living garrison");
            while (GameManager.Instance.SurvivalTime < 380) yield return null;
            Require(entry.enemies.All(t => t.type == EnemyType.Normal), "380 seconds in entry never unlocks late enemy types");
            Require(!WeaponModuleSelectionManager.IsChoosingModule && controller.rewards.modules.UnlockedModuleCount == 0, "45/150/240 second module/evolution schedule suppressed");
            Require(!UnityEngine.Object.FindObjectOfType<PlanetStormController>().IsStormActive && !UnityEngine.Object.FindObjectOfType<ToxicFogRouteController>().HasStarted &&
                !UnityEngine.Object.FindObjectOfType<ExtractionController>().HasHighRiskCacheSpawned &&
                !UnityEngine.Object.FindObjectOfType<ExtractionController>().HasEmergencyTriggered, "Storm/fog/315-360 second events remain dormant");
            Require(entry.patrolSpent == entry.config.patrolBudget, "Patrol budget is finite across a long stay");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Docs/Level01/RegionLoop_Entry.png"));

            var ticket = entry.enemies.First(t => t.lease != null && t.role == RegionEnemyRole.Resident);
            int remain = entry.Remaining(RegionEnemyRole.Resident), kills = GameManager.Instance.KillCount;
            var leased = ticket.lease;
            leased.Health.TakeDamage(1);
            int originalHealth = leased.Health.CurrentHealth;
            leased.PoolMember.ReturnToPool();
            Require(!ticket.dead && entry.Remaining(RegionEnemyRole.Resident) == remain && GameManager.Instance.KillCount == kills, "Pool return is not a death or kill");
            while (ticket.lease == null) yield return null;
            Require(ticket.lease.RunId == controller.RunId && ticket.lease.Health.CurrentHealth == originalHealth, "Pool reuse restores run ownership and surviving health");
            var suspended = ticket.lease.PoolMember;
            ticket.lease.gameObject.SetActive(false);
            Require(!ticket.dead && entry.Remaining(RegionEnemyRole.Resident) == remain, "Temporary disable does not complete a required guard");
            while (!suspended.IsReturned) yield return null;
            Require(suspended.IsReturned, "External disable safely retires the ActiveSet slot on the next frame");
            while (ticket.lease == null) yield return null;
            int beforeKill = GameManager.Instance.KillCount;
            var dyingHealth = ticket.lease.Health;
            dyingHealth.TakeDamage(int.MaxValue); dyingHealth.TakeDamage(int.MaxValue);
            while (!ticket.dead) yield return null;
            Require(GameManager.Instance.KillCount == beforeKill + 1 && !entry.RecordDeath(ticket), "Repeated damage/death notification counts one actual kill and one guard completion");
            autoChoose = true;
            while (entry.Remaining(RegionEnemyRole.Resident) > 0) { Kill(entry, RegionEnemyRole.Resident); yield return null; }
            while (!entry.encounterRewarded) yield return null;
            Require(entry.encounterRewarded, "Entry first-clear reward delivered");
            int residentCount = entry.enemies.Count(t => t.role == RegionEnemyRole.Resident);
            Require(controller.TryActivate("ColonyEntry"), "Arrival completes after actual required deaths");
            Require(!gates.First(g => g.gateId == "G1").locked && !controller.TryActivate("ColonyEntry"), "G1 opens and duplicate Arrival interaction is rejected");
            while (controller.rewards.HasPending) yield return null;

            Teleport("CargoYard");
            var cargo = controller.State("CargoYard");
            while (!cargo.initialized) yield return null;
            Require(!controller.HasMilestone("Beacon"), "Entering Cargo does not auto-start/complete Beacon");
            Require(controller.TryActivate("CargoYard"), "Player can explicitly start Beacon");
            float pausedProgress = cargo.missionElapsed;
            Time.timeScale = 0;
            yield return null; yield return null; yield return null;
            Require(cargo.missionElapsed == pausedProgress, "Paused simulation does not advance device defense");
            Time.timeScale = 1;
            Vector3 locked = cargo.lockedDevicePosition;
            controller.world.MoveMissionPoints("PowerMaintenance");
            Require(cargo.lockedDevicePosition == locked && controller.Terminal("CargoYard").transform.position == locked, "Active device cannot move across regions");
            // Force one device-loss condition, using the same runtime integrity checked by TickMission.
            cargo.integrity = 0; yield return null; yield return null;
            Require(cargo.objective == RegionObjectiveState.Failed && cargo.Remaining(RegionEnemyRole.Mission) == 0, "Failure retires all old wave tickets without granting completion");
            Require(controller.TryActivate("CargoYard") && cargo.attempt == 2, "Beacon recovery begins a real new defense attempt");
            Require(!controller.HasMilestone("Beacon") && gates.First(g => g.gateId == "G2").locked, "Retry does not bypass guards or open G2");
            autoChoose = false;
            while (cargo.objective != RegionObjectiveState.Completed)
            {
                Kill(cargo, RegionEnemyRole.Mission); Kill(cargo, RegionEnemyRole.Resident);
                if (UpgradeManager.IsChoosingUpgrade) ChooseVisibleCard();
                yield return null;
            }
            Require(cargo.enemies.Any(t => t.role == RegionEnemyRole.Patrol && !t.dead) || cargo.patrolSpent == 0, "Optional patrols are excluded from completion");
            Require(controller.rewards.HasPending && !controller.rewards.WasApplied("mission/Beacon"), "Module entitlement remains pending until a real choice is applied");
            autoChoose = true;
            while (controller.rewards.HasPending) yield return null;
            Require(controller.rewards.modules.UnlockedModuleCount == 1 && controller.rewards.WasApplied("mission/Beacon"), "Beacon grants module slot 1 exactly once");
            Require(!controller.TryActivate("CargoYard") && !gates.First(g => g.gateId == "G2").locked, "Repeated Beacon interaction cannot grant another module");

            Teleport("ColonyEntry"); yield return null; yield return null;
            Require(entry.enemies.Count(t => t.role == RegionEnemyRole.Resident) == residentCount && entry.Remaining(RegionEnemyRole.Resident) == 0 && entry.encounterRewarded,
                "Returning to a cleared region preserves encounter and reward state");
            Teleport("PowerMaintenance");
            var power = controller.State("PowerMaintenance");
            while (!power.initialized) yield return null;
            Require(controller.TryActivate("PowerMaintenance"), "Power device starts finite defense waves");
            bool sawKeyGuard = false;
            while (power.objective != RegionObjectiveState.Completed)
            {
                sawKeyGuard |= power.enemies.Any(t => t.keyGuard && t.lease != null && t.type == EnemyType.Heavy);
                Kill(power, RegionEnemyRole.Resident); Kill(power, RegionEnemyRole.Mission);
                yield return null;
            }
            while (controller.rewards.HasPending) yield return null;
            Require(sawKeyGuard, "Power includes an actually spawned required Tank guard");
            Require(controller.rewards.modules.UnlockedModuleCount == 2 && controller.rewards.WasApplied("mission/Power"), "Power grants module slot 2 exactly once");
            Require(!gates.First(g => g.gateId == "G3").locked && gates.First(g => g.gateId == "G4").locked, "G3 opens and later progression stays locked");
            Require(!controller.world.progression.Complete("Research") && !controller.HasMilestone("Research"), "Legacy terminal completion cannot bypass region authority");
            Require(controller.missionView.CompletedMissionCount == 3, "Existing result service sees three completed regional missions");

            autoChoose = false;
            var xp = controller.rewards.experience;
            int level = xp.CurrentLevel;
            var pause = UnityEngine.Object.FindObjectOfType<PauseMenuController>();
            pause.PauseGame();
            xp.AddExperience(xp.ExperienceToNextLevel * 4);
            int choices = xp.CurrentLevel - level;
            Require(choices > 1 && controller.rewards.PendingCount == choices, "Every level in a multi-level grant queues one choice");
            yield return null; yield return null;
            Require(PauseMenuController.IsPaused && !UpgradeManager.IsChoosingUpgrade, "Pending rewards respect the existing pause menu");
            pause.ResumeGame();
            yield return null;
            Require(UpgradeManager.IsChoosingUpgrade && Time.timeScale == 0, "Choice panel owns pause");
            yield return null;
            Require(!controller.hud.GetComponentInParent<Canvas>().enabled, "Region HUD never covers the open reward cards");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Docs/Level01/RegionLoop_Choice.png"));
            yield return null; yield return null;
            controller.world.player.GetComponent<PlayerHealth>().CancelTemporaryInvulnerability();
            controller.world.player.GetComponent<PlayerHealth>().TakeDamage(int.MaxValue);
            yield return null; yield return null;
            Require(controller.ShuttingDown && controller.rewards.PendingCount == 0 && !UpgradeManager.IsChoosingUpgrade && !WeaponModuleSelectionManager.IsChoosingModule,
                "Death clears pending choices, subscriptions and owned enemies");

            string previousRun = controller.RunId;
            controller = null;
            GameManager.Instance.RestartGame();
            yield return null; yield return null;
            controller = UnityEngine.Object.FindObjectOfType<RegionLoopController>();
            Require(controller != null && controller.RunId != previousRun && !controller.HasMilestone("Arrival") && controller.rewards.modules.UnlockedModuleCount == 0,
                "Restart creates a clean run and clears region/module state");
            autoChoose = false;
            controller = null;
            EditorSceneManager.LoadSceneInPlayMode(RegionLoopInstaller.Source, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
            Require(UnityEngine.Object.FindObjectOfType<RegionLoopController>() == null && !UnityEngine.Object.FindObjectOfType<WeaponModuleSelectionManager>().regionDriven &&
                !UnityEngine.Object.FindObjectOfType<EnemySpawner>().regionDriven, "Loading original scene restores default legacy scheduling; no mode leakage");
            UnityEngine.Object.FindObjectOfType<PlayerHealth>().BeginTemporaryInvulnerability(1000);
            Time.timeScale = 30;
            while (GameManager.Instance.SurvivalTime < 46 && !WeaponModuleSelectionManager.IsChoosingModule) yield return null;
            Require(WeaponModuleSelectionManager.IsChoosingModule, "Original scene still executes timed first-module selection");
            results.Add("Scope: accelerated Play Mode with real pooled deaths and real selection buttons; device-loss injected; player travel teleported. Human aiming, feel and balance are not certified.");
        }
        static void Teleport(string region)
        {
            controller.world.player.transform.position += controller.Terminal(region).transform.position - controller.world.PlayerFeet;
            Physics2D.SyncTransforms();
        }
        static void Kill(RegionRunState state, RegionEnemyRole role)
        {
            foreach (var ticket in state.enemies.ToArray())
                if (ticket.role == role && ticket.lease != null && !ticket.lease.Health.IsDead) ticket.lease.Health.TakeDamage(int.MaxValue);
        }
        static void ChooseVisibleCard()
        {
            UnityEngine.Object manager = UpgradeManager.IsChoosingUpgrade ? (UnityEngine.Object)controller.rewards.upgrades :
                WeaponModuleSelectionManager.IsChoosingModule ? controller.rewards.modules : null;
            if (manager == null) return;
            var so = new SerializedObject(manager);
            var buttons = so.FindProperty(UpgradeManager.IsChoosingUpgrade ? "upgradeButtons" : "moduleButtons");
            for (int i = 0; i < buttons.arraySize; i++)
            {
                var button = buttons.GetArrayElementAtIndex(i).objectReferenceValue as Button;
                if (button != null && button.gameObject.activeInHierarchy && button.interactable) { button.onClick.Invoke(); return; }
            }
            throw new InvalidOperationException("Visible choice panel has no clickable option.");
        }
    }
}
