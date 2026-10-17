using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class HiveTyrantBuildVerification
{
    public const string Output = "D:/AlienPurge/Builds/HiveTyrantCompletion/AlienPurge.exe";
    public static void BuildWindows()
    {
        VerifySetupStability();
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            throw new InvalidOperationException("Windows standalone module unavailable");
        Directory.CreateDirectory(Path.GetDirectoryName(Output));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { HiveTyrantCombatSetup.TestScene }, locationPathName = Output,
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development | BuildOptions.StrictMode
        });
        Directory.CreateDirectory("Docs/Boss/CompletionValidation");
        File.WriteAllLines("Docs/Boss/CompletionValidation/WindowsBuild_Result.txt", new[] {
            report.summary.result.ToString(), "Unity " + UnityEngine.Application.unityVersion,
            "Output=" + Output, "Scene=" + HiveTyrantCombatSetup.TestScene,
            "Errors=" + report.summary.totalErrors + " Warnings=" + report.summary.totalWarnings,
            "Bytes=" + report.summary.totalSize + " Duration=" + report.summary.totalTime,
            "Development Windows64 build. Original project build-scene settings unchanged. Runtime smoke has a separate report."
        });
        if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("HiveTyrant Windows build failed");
    }
    private static void VerifySetupStability()
    {
        var before = Directory.GetFiles("Assets", "*", SearchOption.AllDirectories).OrderBy(path => path).ToDictionary(path => path, Hash);
        HiveTyrantCombatSetup.ConfigureAndValidate();
        var after = Directory.GetFiles("Assets", "*", SearchOption.AllDirectories).OrderBy(path => path).ToDictionary(path => path, Hash);
        var changed = before.Keys.Union(after.Keys).Where(path => !before.ContainsKey(path) || !after.ContainsKey(path) || before[path] != after[path]).ToArray();
        Directory.CreateDirectory("Docs/Boss/CompletionValidation");
        File.WriteAllLines("Docs/Boss/CompletionValidation/RepeatSetup_Result.txt", new[] { changed.Length == 0 ? "PASSED" : "FAILED", "All Assets files hashed with SHA256 before and after repeat Setup: " + before.Count, "Changed/added/removed: " + changed.Length }.Concat(changed));
        if (changed.Length > 0) throw new InvalidOperationException("Repeat Setup changed saved Assets; inspect stability report before building.");
    }
    private static string Hash(string path)
    { using (var hash = SHA256.Create()) using (var stream = File.OpenRead(path)) return BitConverter.ToString(hash.ComputeHash(stream)); }
}
