using System.Collections.Generic;
using Riptide.Core;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// Spec §7: one place maps beat names → (SFX, haptic) tuples from the
    /// ui_theme.json juice table — all values data, none in code. Animations are
    /// the beats themselves (AnimationDriver); this director handles the audible
    /// and tactile halves with the GDD 7.2 pitch modulations. The whole table is
    /// resolved ONCE at create time so a beat allocates nothing (§9 budget,
    /// covered by the play-mode allocation test).
    /// </summary>
    public sealed class JuiceDirector : MonoBehaviour
    {
        private readonly struct ResolvedJuice
        {
            public readonly bool HasSfx;
            public readonly SfxId Sfx;
            public readonly int HapticTier; // 0 none · 1 light · 2 medium · 3 heavy

            public ResolvedJuice(bool hasSfx, SfxId sfx, int hapticTier)
            {
                HasSfx = hasSfx;
                Sfx = sfx;
                HapticTier = hapticTier;
            }
        }

        private readonly Dictionary<string, ResolvedJuice> resolved =
            new Dictionary<string, ResolvedJuice>(System.StringComparer.Ordinal);
        private AnimationDriver driver = null!;
        private AudioSource sfxSource = null!;

        public static JuiceDirector Create(Transform parent, AnimationDriver driver)
        {
            var go = new GameObject("JuiceDirector");
            go.transform.SetParent(parent, false);
            var juice = go.AddComponent<JuiceDirector>();
            juice.driver = driver;
            juice.sfxSource = go.AddComponent<AudioSource>();
            juice.ResolveTable();
            driver.BeatStarted += juice.OnBeat;
            return juice;
        }

        private void ResolveTable()
        {
            foreach (KeyValuePair<string, JuiceEntry> entry in ThemeRuntime.Theme.Juice)
            {
                bool hasSfx = System.Enum.TryParse(entry.Value.Sfx, out SfxId id);
                if (hasSfx)
                {
                    AudioSynth.Sfx(id); // warm the clip cache off the hot path
                }

                resolved[entry.Key] = new ResolvedJuice(hasSfx, id, TierOf(entry.Value.Haptic));
            }
        }

        internal static int TierOf(string haptic)
        {
            switch (haptic)
            {
                case "light": return 1;
                case "medium": return 2;
                case "heavy": return 3;
                default: return 0;
            }
        }

        private void OnDestroy()
        {
            if (driver != null)
            {
                driver.BeatStarted -= OnBeat;
            }
        }

        private static bool SoundOn => PlayerPrefs.GetInt("settings.audio.on", 1) == 1;

        /// <summary>Hot path: dictionary hit + clip lookup only — no allocations (§9, CI-tested).</summary>
        public void OnBeat(string beat, MoveResult result)
        {
            if (!resolved.TryGetValue(beat, out ResolvedJuice juice))
            {
                return;
            }

            if (juice.HasSfx && SoundOn)
            {
                sfxSource.pitch = Pitch(beat, result);
                sfxSource.PlayOneShot(AudioSynth.Sfx(juice.Sfx), 0.85f);
            }

            PlayHaptic(juice.HapticTier);
        }

        internal static void PlayHaptic(int tier)
        {
            switch (tier)
            {
                case 1:
                    Haptics.Light();
                    break;
                case 2:
                    Haptics.Medium();
                    break;
                case 3:
                    Haptics.Heavy();
                    break;
            }
        }

        /// <summary>GDD 7.2 pitch grammar: piece-size thunk, combo chime climb, streak chirp.</summary>
        private static float Pitch(string beat, MoveResult result)
        {
            switch (beat)
            {
                case "place":
                    return 1.25f - 0.05f * result.Events.PlacedCells.Count;
                case "clear":
                    return 0.9f + 0.1f * Mathf.Max(0, result.Events.Scoring.ComboHalves - 2);
                case "rescue":
                    return 1f + 0.06f * result.Next.RescueStreak;
                default:
                    return 1f;
            }
        }
    }

    /// <summary>
    /// Screen-side juice (§7 star/streak rows): same data table, same gates, for
    /// moments that fire from screens rather than board beats.
    /// </summary>
    public static class UiJuice
    {
        private static AudioSource? source;

        public static void Play(string beat)
        {
            if (!ThemeRuntime.Theme.Juice.TryGetValue(beat, out JuiceEntry? entry) || entry == null)
            {
                return;
            }

            if (PlayerPrefs.GetInt("settings.audio.on", 1) == 1
                && System.Enum.TryParse(entry.Sfx, out SfxId id))
            {
                Source().PlayOneShot(AudioSynth.Sfx(id), 0.85f);
            }

            JuiceDirector.PlayHaptic(JuiceDirector.TierOf(entry.Haptic));
        }

        private static AudioSource Source()
        {
            if (source == null)
            {
                var go = new GameObject("UiJuice");
                source = go.AddComponent<AudioSource>();
            }

            return source;
        }
    }
}
