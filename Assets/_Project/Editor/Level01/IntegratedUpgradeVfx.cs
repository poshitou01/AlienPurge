using System;
using System.IO;
using System.Linq;
using AlienPurge.Level01.RegionLoop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AlienPurge.Level01.Editor
{
    public static class IntegratedUpgradeVfx
    {
        public const string Folder = IntegratedWorldInstaller.Data + "/SpecialUpgrades";
        public const string ReportFolder = IntegratedWorldInstaller.Reports + "/SpecialUpgrades";
        public const string ProfilePath = Folder + "/UpgradeVfx.asset";

        [MenuItem("AlienPurge/Gameplay/Level01/Install Integrated Upgrade Effects")]
        public static void Install()
        {
            if (!Path.GetFullPath(Application.dataPath).Replace('\\', '/').Equals("D:/AlienPurge_IntegratedV01/Assets", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only install inside D:/AlienPurge_IntegratedV01.");
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before installing.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save the open scene before installing special upgrade effects.");
            IntegratedWorldInstaller.CheckProtectedScenes();
            Directory.CreateDirectory(Folder); Directory.CreateDirectory(ReportFolder); AssetDatabase.Refresh();
            var profile = AssetDatabase.LoadAssetAtPath<UpgradeVfxProfile>(ProfilePath);
            if (profile == null)
            {
                var shader = Shader.Find("AlienPurge/Combat/UpgradeVfx");
                if (shader == null) throw new InvalidOperationException("UpgradeVfx shader is missing.");
                var material = new Material(shader) { name = "UpgradeVfx" }; AssetDatabase.CreateAsset(material, Folder + "/UpgradeVfx.mat");
                profile = ScriptableObject.CreateInstance<UpgradeVfxProfile>(); profile.material = material;
                profile.explosionSound = EnsureSound(false); profile.lightningSound = EnsureSound(true);
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            if (profile.explosionFrames == null || profile.explosionFrames.Length == 0)
            {
                // Reuse the project's completed eight-frame flame art without modifying its import settings or GUIDs.
                profile.explosionFrames = Enumerable.Range(1, 8).Select(i => AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/_Project/Art/Enemies/Animated/Exploder/Explosion/Exploder_Explosion_" + i.ToString("D2") + ".png")).ToArray();
                if (profile.explosionFrames.Any(f => f == null)) throw new InvalidOperationException("Missing formal explosion animation frame.");
                EditorUtility.SetDirty(profile);
            }
            var scene = EditorSceneManager.OpenScene(IntegratedWorldInstaller.Target);
            var explosionPool = HiveTyrantCombatSetup.SceneComponents<ExplosionEffectPool>(scene).Single();
            var chainPool = HiveTyrantCombatSetup.SceneComponents<ChainLightningEffectPool>(scene).Single();
            ConfigurePool(explosionPool, profile, "Explosion_Integrated", 24);
            ConfigurePool(chainPool, profile, "Lightning_Integrated", 64);
            var loop = HiveTyrantCombatSetup.SceneComponents<RegionLoopController>(scene).Single();
            if (loop.GetComponent<UpgradeVfxAudio>() == null) loop.gameObject.AddComponent<UpgradeVfxAudio>();
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            IntegratedWorldInstaller.CheckProtectedScenes();
            Validate();
        }

        static void ConfigurePool(MonoBehaviour pool, UpgradeVfxProfile profile, string name, int capacity)
        {
            var so = new SerializedObject(pool); var field = so.FindProperty("effectPrefab");
            var original = (Component)field.objectReferenceValue;
            if (original == null) throw new InvalidOperationException(pool.name + " has no source effect.");
            string path = Folder + "/" + name + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                var copy = Object.Instantiate(original.gameObject); copy.name = name;
                copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); copy.transform.localScale = Vector3.one;
                copy.SetActive(true);
                var visual = copy.AddComponent<UpgradeEffectVisual>(); visual.profile = profile;
                copy.GetComponent<LineRenderer>().enabled = false;
                prefab = PrefabUtility.SaveAsPrefabAsset(copy, path); Object.DestroyImmediate(copy);
            }
            if (prefab.GetComponent<UpgradeEffectVisual>()?.profile != profile)
                throw new InvalidOperationException("Conflicting special upgrade prefab: " + path);
            bool first = original.GetComponent<UpgradeEffectVisual>() == null;
            field.objectReferenceValue = prefab.GetComponent(original.GetType());
            if (first)
            {
                so.FindProperty("initialPoolSize").intValue = capacity;
                so.FindProperty("allowExpansion").boolValue = false;
                if (!EditorUtility.IsPersistent(original)) original.gameObject.SetActive(false);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("AlienPurge/Gameplay/Level01/Validate Integrated Upgrade Effects")]
        public static void Validate()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != IntegratedWorldInstaller.Target) throw new InvalidOperationException("Open the integrated scene first.");
            foreach (var type in new[] { typeof(ExplosionEffectPool), typeof(ChainLightningEffectPool) })
            {
                var pool = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren(type, true)).Single();
                var so = new SerializedObject(pool); var prefab = (Component)so.FindProperty("effectPrefab").objectReferenceValue;
                var visual = prefab.GetComponent<UpgradeEffectVisual>();
                if (visual == null || !visual.Ready || visual.profile.explosionSound == null || visual.profile.lightningSound == null ||
                    visual.profile.explosionFrames == null || visual.profile.explosionFrames.Length != 8 || visual.profile.explosionFrames.Any(f => f == null) || so.FindProperty("allowExpansion").boolValue)
                    throw new InvalidOperationException("Invalid bounded upgrade effect pool: " + pool.name);
                if (UnityEditor.ShaderUtil.ShaderHasError(visual.profile.material.shader)) throw new InvalidOperationException("UpgradeVfx shader failed to compile.");
            }
            if (HiveTyrantCombatSetup.SceneComponents<UpgradeVfxAudio>(scene).Length != 1) throw new InvalidOperationException("Expected one special upgrade audio host.");
            IntegratedWorldInstaller.CheckProtectedScenes();
            File.WriteAllText(ReportFolder + "/Installation.txt", "PASS\nScene=" + scene.path + "\nExplosion pool=24; lightning pool=64; expansion disabled.\nShared legacy prefabs and source scenes preserved.\n");
        }

        // Original short transients. Generated once as PCM assets; no synthesis or allocation during combat.
        static AudioClip EnsureSound(bool lightning)
        {
            const int rate = 48000;
            string path = Folder + (lightning ? "/SFX_UpgradeLightning.wav" : "/SFX_UpgradeExplosion.wav");
            if (!File.Exists(path))
            {
                double duration = lightning ? .17 : .29;
                var samples = new double[(int)(duration * rate)]; var random = new System.Random(lightning ? 8217 : 4729);
                double low = 0, mid = 0, peak = 0, sum = 0;
                for (int i = 0; i < samples.Length; i++)
                {
                    double t = (double)i / rate, noise = random.NextDouble() * 2 - 1;
                    low += (noise - low) * .08; mid += (noise - mid) * .35;
                    double body = Math.Sin(2 * Math.PI * (70 * t + 1.8 * (1 - Math.Exp(-t * 45))));
                    double value;
                    if (lightning)
                    {
                        double buzz = Math.Sin(2 * Math.PI * (1900 * t - 2400 * t * t));
                        double sparks = 1 + .65 * Math.Cos(2 * Math.PI * t * 81);
                        value = ((noise - mid) * .9 * sparks + buzz * .18) * Math.Exp(-t / .031);
                        value += (mid - low) * .4 * Math.Exp(-Math.Abs(t - .025) / .006);
                    }
                    else value = body * .75 * Math.Exp(-t / .047) + (mid - low) * 1.8 * Math.Exp(-t / .022) + low * 1.1 * Math.Exp(-t / .085);
                    double edge = Math.Min(1, Math.Min(i, samples.Length - 1 - i) / 180.0);
                    samples[i] = Math.Tanh(value * 1.2) * edge; sum += samples[i];
                }
                double mean = sum / samples.Length;
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] -= mean * Math.Min(1, Math.Min(i, samples.Length - 1 - i) / 180.0);
                    peak = Math.Max(peak, Math.Abs(samples[i]));
                }
                using (var output = new BinaryWriter(File.Create(path)))
                {
                    output.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); output.Write(36 + samples.Length * 2);
                    output.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); output.Write(16); output.Write((short)1); output.Write((short)1);
                    output.Write(rate); output.Write(rate * 2); output.Write((short)2); output.Write((short)16);
                    output.Write(System.Text.Encoding.ASCII.GetBytes("data")); output.Write(samples.Length * 2);
                    foreach (double value in samples) output.Write((short)Math.Round(value / peak * .78 * short.MaxValue));
                }
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path); importer.forceToMono = true;
                var settings = importer.defaultSampleSettings; settings.preloadAudioData = true; settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM; settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings = settings; importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
