using System;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>GDD 7.2's twelve SFX, by name.</summary>
    public enum SfxId
    {
        Place,
        Clear,
        ComboShine,
        Drain,
        Rise,
        DangerWarning,
        Rescue,
        CreatureLost,
        Drown,
        Button,
        StarAward,
        Streak,
    }

    /// <summary>
    /// Every clip is synthesized (DECISIONS.md P8: zero third-party audio).
    /// Simple additive sines + filtered noise with exponential envelopes —
    /// placeholder-grade but loudness-safe and license-free.
    /// </summary>
    public static class AudioSynth
    {
        private const int SampleRate = 44100;
        private static readonly AudioClip?[] SfxCache = new AudioClip?[12];
        private static AudioClip? calmLoop;
        private static AudioClip? tenseLoop;

        public static AudioClip Sfx(SfxId id)
        {
            int index = (int)id;
            if (SfxCache[index] == null)
            {
                SfxCache[index] = Build(id);
            }

            return SfxCache[index]!;
        }

        /// <summary>GDD 7.2: one calm ambient loop…</summary>
        public static AudioClip CalmLoop()
        {
            if (calmLoop == null)
            {
                calmLoop = BuildPad("calm", new[] { 110f, 165f, 220f }, 8f, 0.05f);
            }

            return calmLoop;
        }

        /// <summary>…and a tenser variant that crossfades in at water ≥ 7 (GDD 7.2).</summary>
        public static AudioClip TenseLoop()
        {
            if (tenseLoop == null)
            {
                tenseLoop = BuildPad("tense", new[] { 98f, 147f, 208f, 311f }, 6f, 0.06f);
            }

            return tenseLoop;
        }

        private static AudioClip Build(SfxId id) => id switch
        {
            SfxId.Place => Tone("place", 0.09f, 180f, 120f, 0.5f),
            SfxId.Clear => Chime("clear", new[] { 523f, 659f, 784f }, 0.28f),
            SfxId.ComboShine => Chime("combo", new[] { 659f, 880f, 1047f, 1319f }, 0.35f),
            SfxId.Drain => Whoosh("drain", 0.45f, 900f, 200f),
            SfxId.Rise => Whoosh("rise", 0.35f, 150f, 520f),
            SfxId.DangerWarning => Tone("danger", 0.30f, 440f, 440f, 0.35f, tremolo: 9f),
            SfxId.Rescue => Chime("rescue", new[] { 784f, 1047f }, 0.22f),
            SfxId.CreatureLost => Tone("lost", 0.35f, 330f, 220f, 0.3f),
            SfxId.Drown => Whoosh("drown", 0.8f, 400f, 60f),
            SfxId.Button => Tone("button", 0.05f, 660f, 660f, 0.3f),
            SfxId.StarAward => Chime("star", new[] { 880f, 1109f, 1319f }, 0.3f),
            _ => Chime("streak", new[] { 587f, 740f, 880f, 1175f }, 0.4f),
        };

        private static AudioClip Tone(string name, float seconds, float fromHz, float toHz, float gain, float tremolo = 0f)
        {
            int samples = (int)(SampleRate * seconds);
            var data = new float[samples];
            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float hz = Mathf.Lerp(fromHz, toHz, t);
                phase += hz / SampleRate;
                float env = Mathf.Exp(-4f * t);
                float trem = tremolo > 0f ? 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * tremolo * i / SampleRate) : 1f;
                data[i] = Mathf.Sin(2f * Mathf.PI * phase) * env * gain * trem;
            }

            return ToClip(name, data);
        }

        private static AudioClip Chime(string name, float[] notes, float seconds)
        {
            int samples = (int)(SampleRate * seconds);
            var data = new float[samples];
            int perNote = samples / notes.Length;
            for (int n = 0; n < notes.Length; n++)
            {
                float phase = 0f;
                int start = n * perNote;
                for (int i = 0; i < perNote && start + i < samples; i++)
                {
                    float t = (float)i / perNote;
                    phase += notes[n] / SampleRate;
                    float env = Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 1.2f)) * Mathf.Exp(-2.5f * t);
                    data[start + i] += Mathf.Sin(2f * Mathf.PI * phase) * env * 0.4f;
                }
            }

            return ToClip(name, data);
        }

        private static AudioClip Whoosh(string name, float seconds, float fromHz, float toHz)
        {
            int samples = (int)(SampleRate * seconds);
            var data = new float[samples];
            var rng = new System.Random(7);
            float filtered = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float cutoff = Mathf.Lerp(fromHz, toHz, t) / SampleRate;
                float alpha = Mathf.Clamp01(cutoff * 6f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                filtered += alpha * (noise - filtered);
                float env = Mathf.Sin(Mathf.PI * t);
                data[i] = filtered * env * 0.8f;
            }

            return ToClip(name, data);
        }

        private static AudioClip BuildPad(string name, float[] chordHz, float seconds, float gain)
        {
            int samples = (int)(SampleRate * seconds);
            var data = new float[samples];
            for (int v = 0; v < chordHz.Length; v++)
            {
                float phase = 0f;
                float lfoRate = 0.1f + v * 0.07f;
                for (int i = 0; i < samples; i++)
                {
                    float lfo = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * lfoRate * i / SampleRate + v);
                    phase += chordHz[v] / SampleRate;
                    data[i] += Mathf.Sin(2f * Mathf.PI * phase) * gain * lfo;
                }
            }

            // Seamless loop: crossfade tail into head.
            int fade = SampleRate / 2;
            for (int i = 0; i < fade; i++)
            {
                float mix = (float)i / fade;
                data[i] = data[i] * mix + data[samples - fade + i] * (1f - mix);
            }

            return ToClip(name, data);
        }

        private static AudioClip ToClip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }

    /// <summary>GDD 7.3 haptics tiers; v1 maps all tiers to Handheld.Vibrate (DECISIONS.md).</summary>
    public static class Haptics
    {
        public static bool Enabled => PlayerPrefs.GetInt("settings.haptics.on", 1) == 1;

        public static void Light() => Pulse();

        public static void Medium() => Pulse();

        public static void Heavy() => Pulse();

        private static void Pulse()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Enabled)
            {
                Handheld.Vibrate();
            }
#endif
        }
    }
}
