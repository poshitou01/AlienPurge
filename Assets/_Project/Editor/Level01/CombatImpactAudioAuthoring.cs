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
        public const string Folder = "Assets/_Project/Audio/SFX/Combat/ImpactV03";
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
            double duration = new[] { .09, .155, .32, .46 }[kind];
            var samples = new double[(int)(duration * rate)];
            var random = new System.Random(7193 + kind * 257);
            double low = 0, smooth = 0, sum = 0, peak = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                double t = (double)i / rate, noise = random.NextDouble() * 2 - 1;
                low += (noise - low) * .13; smooth += (noise - smooth) * .4;
                double value;
                if (kind < 2)
                {
                    // Dry contact: short noise snap and a low knock, with no confirmation tone.
                    double contact = (smooth - low) * Math.Exp(-t / .012) * 2.8;
                    double knock = Sin(126 * t + .65 * (1 - Math.Exp(-t * 110))) * Math.Exp(-t / .023);
                    value = contact + knock * .43;
                    if (kind == 1) value += (Sin(340 * t) + .37 * Sin(582 * t)) * Math.Exp(-t / .038) * .24;
                }
                else
                {
                    // A separate identity: double fracture, descending body, then a clearly
                    // audible resolved fifth. Not an armor mechanic and not a sampled game sound.
                    double body = Sin((kind == 3 ? 63 : 88) * t + 1.4 * (1 - Math.Exp(-t * 65))) * Math.Exp(-t / .065);
                    value = body * .66 + (smooth - low) * Math.Exp(-t / .026) * 2.8;
                    double split = t - .028;
                    if (split > 0) value += (smooth - low) * Math.Exp(-split / .022) * 1.9;
                    for (int n = 0; n < (kind == 3 ? 7 : 5); n++)
                    {
                        double s = t - (.014 + n * .014);
                        if (s < 0) continue;
                        value += (Sin((1430 + n * 317) * s - 1300 * s * s) + .35 * noise) * Math.Exp(-s / .024) * (.15 - n * .013);
                    }
                    double tail = t - .045;
                    if (tail > 0)
                    {
                        double note = kind == 3 ? 523.25 : 783.99;
                        double resolved = Sin(note * tail) * .28 + Sin(note * 1.5 * tail) * .14 + Sin(note * 2.013 * tail) * .045;
                        value += resolved * Math.Min(1, tail / .009) * Math.Exp(-tail / (kind == 3 ? .12 : .085));
                    }
                    if (kind == 3) value += low * Math.Exp(-t / .10) * .7;
                }
                double fade = Math.Min(1, t / .0008) * Math.Min(1, (duration - t) / .018);
                samples[i] = Math.Tanh(value * 1.35) * fade; sum += samples[i];
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
