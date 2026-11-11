using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TwoInPot.Editor
{
    // Original short PCM cues. No external samples, generation services or licenses.
    public static class FestivalAudioBuilder
    {
        const string Folder = "Assets/TwoInPot/Audio";
        public static void Apply()
        {
            Directory.CreateDirectory(Folder);
            var sound = Camera.main.GetComponent<FestivalSfx>();
            if (sound == null) sound = Camera.main.gameObject.AddComponent<FestivalSfx>();
            if (Camera.main.GetComponent<AudioListener>() == null) Camera.main.gameObject.AddComponent<AudioListener>();
            sound.clips = new AudioClip[9];
            string[] names = { "Select", "Contact", "Push", "Land", "Jump", "Gate", "Record", "Clear", "Ending" };
            float[] lengths = { .07f, .065f, .16f, .11f, .15f, .25f, .22f, .32f, .48f };
            for (int i = 0; i < names.Length; i++)
            {
                string path = Folder + "/" + names[i] + ".wav";
                WriteClip(path, i, lengths[i]);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad; settings.compressionFormat = AudioCompressionFormat.PCM;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings = settings; importer.forceToMono = false; importer.SaveAndReimport();
                sound.clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            EditorUtility.SetDirty(sound);
            Debug.Log("[POT_AUDIO] Nine original short PCM cues attached; four pooled voices; mute control available.");
        }
        static void WriteClip(string path, int cue, float duration)
        {
            const int rate = 22050;
            int count = (int)(duration * rate);
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2);
                writer.Write((short)2); writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
                double phase = 0; uint random = 9731;
                for (int i = 0; i < count; i++)
                {
                    double t = (double)i / rate, progress = (double)i / Math.Max(1, count - 1);
                    double frequency = cue == 0 ? 720 : cue == 1 ? 240 : cue == 2 ? 150 + progress * 260 : cue == 3 ? 110 - progress * 50 : cue == 4 ? 300 + progress * 420 : cue == 5 ? 160 + progress * 180 : cue == 6 ? (progress < .5 ? 440 : 330) : cue == 7 ? (progress < .5 ? 392 : 523) : (progress < .33 ? 330 : progress < .66 ? 392 : 440);
                    phase += frequency / rate;
                    double triangle = 1 - 4 * Math.Abs(phase - Math.Floor(phase) - .5);
                    random = random * 1664525 + 1013904223;
                    double noise = (random >> 8) / 8388607.5 - 1;
                    double wave = cue == 1 || cue == 3 ? triangle * .35 + noise * .65 : cue == 5 ? triangle * .75 + noise * .25 : triangle * .72 + Math.Sin(phase * Math.PI * 2) * .28;
                    double envelope = Math.Min(1, t / .004) * Math.Pow(1 - progress, cue == 1 || cue == 3 ? 2.8 : 1.7);
                    writer.Write((short)(Math.Clamp(wave * envelope * .42, -.5, .5) * short.MaxValue));
                }
            }
        }
    }
}
