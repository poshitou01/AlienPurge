using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace AlienPurge.Editor
{
    // Build the accepted scenes directly. No scene migration, runtime installer or gameplay changes.
    public static class WindowsDemoBuilder
    {
        public const string Menu = "Assets/_Project/Scenes/MainMenu_IntegratedV01.unity";
        public const string Level = "Assets/_Project/Scenes/ArtTests/Level01_FullWorld_IntegratedV01.unity";
        const string Reports = "Docs/Demo", LastBuild = "Builds/Demo/LastBuild.txt";
        static T[] All<T>(Scene scene) where T : Component => HiveTyrantCombatSetup.SceneComponents<T>(scene);
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static void RequireSaved()
        {
            Require(!Application.isPlaying, "Exit Play Mode before building.");
            for (int i = 0; i < SceneManager.sceneCount; i++) Require(!SceneManager.GetSceneAt(i).isDirty, "Save scene edits first: " + SceneManager.GetSceneAt(i).path);
        }
        [MenuItem("AlienPurge/Demo/Validate")]
        public static void Validate()
        {
            RequireSaved(); Directory.CreateDirectory(Reports);
            Require(Application.unityVersion == "2022.3.62f3c1", "Use the project's Unity 2022.3.62f3c1.");
            Require(BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64), "Windows x64 build support is unavailable.");
            var previous = EditorSceneManager.GetSceneManagerSetup(); var lines = new List<string>();
            try
            {
                foreach (string path in new[] { Menu, Level })
                {
                    Require(File.Exists(path), "Scene missing: " + path);
                    var scene = EditorSceneManager.OpenScene(path);
                    foreach (var t in All<Transform>(scene)) Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0, "Missing script: " + path + " / " + t.name);
                    if (path == Menu)
                    {
                        var manager = All<MainMenuManager>(scene).Single();
                        Require(new SerializedObject(manager).FindProperty("gameSceneName").stringValue == Path.GetFileNameWithoutExtension(Level), "Existing menu points to another scene.");
                    }
                    else
                    {
                        Require(All<GameManager>(scene).Length == 1 && All<EnemyPool>(scene).Length == 1, "Game/pool manager count is not one.");
                        var pause = All<PauseMenuController>(scene).Single();
                        Require(new SerializedObject(pause).FindProperty("mainMenuSceneName").stringValue == Path.GetFileNameWithoutExtension(Menu), "Existing return route points to another menu.");
                    }
                    lines.Add("PASS: " + path + " scripts and native scene routing.");
                    var tests = All<MonoBehaviour>(scene).Where(c => c != null && (c.GetType().Name.Contains("DebugTester") || c.GetType().Name.Contains("TestSeeder")))
                        .Select(c => c.GetType().Name + " active=" + c.isActiveAndEnabled).Distinct();
                    lines.Add("Existing development components, preserved by request: " + string.Join(", ", tests));
                }
            }
            finally
            {
                var available = previous.Where(s => !string.IsNullOrEmpty(s.path) && File.Exists(s.path)).ToArray();
                if (available.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(available);
                else EditorSceneManager.OpenScene(Menu);
            }
            File.WriteAllLines(Reports + "/Validation.txt", lines); Debug.Log("[Demo] Original integrated scenes validated.");
        }
        [MenuItem("AlienPurge/Demo/Build Windows Demo")]
        public static void BuildWindows()
        {
            Validate();
            string parent = Path.GetFullPath("Builds/Demo/" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Require(!Directory.Exists(parent), "Use a new build directory.");
            string folder = Path.Combine(parent, "AlienPurge_Demo_v0.1_Win64"); Directory.CreateDirectory(folder);
            var originals = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
                .Concat(new[] { "ProjectSettings/ProjectSettings.asset", "ProjectSettings/EditorBuildSettings.asset", "Assets/Settings/UniversalRP.asset" })
                .Concat(Directory.GetFiles("Assets", "*.unity", SearchOption.AllDirectories))
                .Where(File.Exists).Distinct().ToDictionary(p => p, File.ReadAllBytes);
            string product = PlayerSettings.productName, version = PlayerSettings.bundleVersion;
            bool development = EditorUserBuildSettings.development, debugging = EditorUserBuildSettings.allowDebugging,
                profiler = EditorUserBuildSettings.connectProfiler, deep = EditorUserBuildSettings.buildWithDeepProfilingSupport;
            int textureSize = EditorUserBuildSettings.overrideMaxTextureSize;
            var compression = EditorUserBuildSettings.overrideTextureCompression;
            try
            {
                // Package metadata only: separate Demo saves from the developer's AlienPurge saves.
                PlayerSettings.productName = "AlienPurge Demo"; PlayerSettings.bundleVersion = "0.1";
                EditorUserBuildSettings.development = EditorUserBuildSettings.allowDebugging = EditorUserBuildSettings.connectProfiler = EditorUserBuildSettings.buildWithDeepProfilingSupport = false;
                EditorUserBuildSettings.overrideMaxTextureSize = 0; EditorUserBuildSettings.overrideTextureCompression = OverrideTextureCompression.NoOverride;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { Menu, Level }, target = BuildTarget.StandaloneWindows64,
                    locationPathName = Path.Combine(folder, "AlienPurge.exe"), options = BuildOptions.StrictMode | BuildOptions.DetailedBuildReport });
                File.WriteAllLines(Reports + "/BuildResult.txt", new[] { report.summary.result.ToString(), "Unity=" + Application.unityVersion,
                    "Target=" + report.summary.platform, "Backend=" + PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone),
                    "Product=" + PlayerSettings.productName, "Company=" + PlayerSettings.companyName, "Version=" + PlayerSettings.bundleVersion,
                    "Development=false Debugging=false Profiler=false DeepProfiling=false ImportOverrides=None",
                    "Scenes=" + Menu + ";" + Level, "Output=" + folder, "Errors=" + report.summary.totalErrors + " Warnings=" + report.summary.totalWarnings,
                    "Bytes=" + report.summary.totalSize + " Duration=" + report.summary.totalTime, "Git=" + Git("rev-parse HEAD"), "Uncommitted=" + Git("status --porcelain") });
                Require(report.summary.result == BuildResult.Succeeded, "Build failed. See Docs/Demo/BuildResult.txt and the Unity log.");
            }
            finally
            {
                PlayerSettings.productName = product; PlayerSettings.bundleVersion = version;
                EditorUserBuildSettings.development = development; EditorUserBuildSettings.allowDebugging = debugging;
                EditorUserBuildSettings.connectProfiler = profiler; EditorUserBuildSettings.buildWithDeepProfilingSupport = deep;
                EditorUserBuildSettings.overrideMaxTextureSize = textureSize; EditorUserBuildSettings.overrideTextureCompression = compression;
                AssetDatabase.SaveAssets();
                // TMP's build processor clears dynamic caches. Restore source bytes without deleting assets.
                foreach (var original in originals) if (!File.ReadAllBytes(original.Key).SequenceEqual(original.Value)) File.WriteAllBytes(original.Key, original.Value);
                AssetDatabase.Refresh();
            }
            Require(File.Exists(Path.Combine(folder, "AlienPurge.exe")) && File.Exists(Path.Combine(folder, "UnityPlayer.dll")) && Directory.Exists(Path.Combine(folder, "MonoBleedingEdge")), "Incomplete Windows Mono runtime.");
            foreach (string symbols in Directory.GetDirectories(folder, "*_BurstDebugInformation_DoNotShip"))
                Directory.Move(symbols, Path.Combine(parent, Path.GetFileName(symbols)));
            File.WriteAllText(LastBuild, folder);
            PackageLastBuild(); Debug.Log("[Demo] Original-scene Windows release package: " + folder);
        }
        [MenuItem("AlienPurge/Demo/Package Last Build")]
        public static void PackageLastBuild()
        {
            Require(File.Exists(LastBuild), "Build the Demo first."); string folder = File.ReadAllText(LastBuild);
            foreach (string name in new[] { "README_试玩说明.md", "Demo_验收报告.md" })
                if (File.Exists(Reports + "/" + name)) File.Copy(Reports + "/" + name, Path.Combine(folder, name), true);
            if (File.Exists("CREDITS.md")) File.Copy("CREDITS.md", Path.Combine(folder, "CREDITS.md"), true);
            string zip = Path.Combine(Path.GetDirectoryName(folder), "AlienPurge_Demo_v0.1_Win64.zip");
            if (File.Exists(zip)) File.Move(zip, zip + ".previous-" + DateTime.Now.ToString("yyyyMMddHHmmss"));
            ZipFile.CreateFromDirectory(folder, zip, System.IO.Compression.CompressionLevel.Optimal, true);
        }
        [MenuItem("AlienPurge/Demo/Open Build Folder")]
        public static void OpenBuildFolder() { Require(File.Exists(LastBuild), "Build the Demo first."); EditorUtility.RevealInFinder(File.ReadAllText(LastBuild)); }
        static string Git(string args)
        {
            var start = new ProcessStartInfo("git", args) { WorkingDirectory = Directory.GetCurrentDirectory(), UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
            using (var p = Process.Start(start)) { string text = p.StandardOutput.ReadToEnd(); p.WaitForExit(); return text.Trim(); }
        }
    }
}
