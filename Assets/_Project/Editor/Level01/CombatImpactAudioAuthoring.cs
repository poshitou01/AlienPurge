using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AlienPurge.Level01.Editor
{
    // Original procedural transients, not recordings or samples from another game.
    // Keep the source recipe with the project so the four layers are reproducible.
    public static class CombatImpactAudioAuthoring
    {
        public const string Folder = "Assets/_Project/Audio/SFX/Combat/ImpactV02";
        public static readonly string[] Names = { "Hit", "StrongHit", "Kill", "HeavyKill" };
        public static void EnsureClips()
        {
            Directory.CreateDirectory(Folder);
            for (int kind = 0; kind < Names.Length; kind++)
            {
                string path = Folder + "/SFX_" + Names[kind] + ".wav";
                if (File.Exists(path)) continue;
                WriteClip(path, kind);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                importer.forceToMono = true; importer.loadInBackground = false;
                var settings = importer.defaultSampleSettings;
                settings.preloadAudioData = true;
                settings.loadType = AudioClipLoadType.DecompressOnLoad; settings.compressionFormat = AudioCompressionFormat.PCM;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings = settings; importer.SaveAndReimport();
            }
        }
        static double Sin(double phase) => Math.Sin(phase * Math.PI * 2);
        static void WriteClip(string path, int kind)
        {
            const int rate = 48000;
            double duration = new[] { .105, .17, .255, .43 }[kind];
            var samples = new double[(int)(duration * rate)];
            var random = new System.Random(7193 + kind * 257);
            double low = 0, smooth = 0, sum = 0, peak = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                double t = (double)i / rate, noise = random.NextDouble() * 2 - 1;
                low += (noise - low) * .13; smooth += (noise - smooth) * .4;
                double crack = (smooth - low) * Math.Exp(-t / (kind < 2 ? .015 : .024));
                double body = Sin(72 * t + (kind == 3 ? 2.5 : 1.3) * (1 - Math.Exp(-t * 75))) * Math.Exp(-t / (kind == 3 ? .095 : .029));
                double snap = Sin(1350 * t - 2200 * t * t) * Math.Exp(-t / .009);
                double value = crack * 2 + body * .4 + snap * .12;
                if (kind >= 1)
                    value += (Sin(610 * t) + .4 * Sin(977 * t)) * Math.Exp(-t / .027) * .12;
                if (kind >= 2)
                {
                    // Several short ceramic/metal fragments follow the initial contact. The
                    // descending body gives weight; a quiet resolved overtone confirms the kill.
                    for (int n = 0; n < (kind == 3 ? 7 : 4); n++)
                    {
                        double s = t - (.006 + n * .0125);
                        if (s < 0) continue;
                        value += (Sin((1700 + n * 271) * s - 3600 * s * s) + .28 * noise) * Math.Exp(-s / .018) * (.095 - n * .008);
                    }
                    double chime = Sin(784 * t) * .07 + Sin(1176 * t) * .032;
                    value += chime * Math.Exp(-t / .095) * Math.Min(1, t / .018);
                }
                if (kind == 3) value += low * Math.Exp(-t / .07) * .65;
                double fade = Math.Min(1, t / .0015) * Math.Min(1, (duration - t) / .014);
                samples[i] = value * fade; sum += samples[i];
            }
            double mean = sum / samples.Length;
            for (int i = 0; i < samples.Length; i++)
            {
                // Remove DC with a zero-ended taper; never add a click at the file boundary.
                double edge = Math.Min(1, Math.Min(i, samples.Length - 1 - i) / 240.0);
                samples[i] = (samples[i] - mean) * edge; peak = Math.Max(peak, Math.Abs(samples[i]));
            }
            using (var output = new BinaryWriter(File.Create(path)))
            {
                output.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); output.Write(36 + samples.Length * 2);
                output.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); output.Write(16); output.Write((short)1); output.Write((short)1);
                output.Write(rate); output.Write(rate * 2); output.Write((short)2); output.Write((short)16);
                output.Write(System.Text.Encoding.ASCII.GetBytes("data")); output.Write(samples.Length * 2);
                foreach (double value in samples) output.Write((short)Math.Round(value / peak * .82 * short.MaxValue));
            }
        }
    }
}
