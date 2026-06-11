using System;
using Riptide.Core;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// Spec §7: one place maps beat names → (SFX, haptic) tuples from the
    /// ui_theme.json juice table — all values data, none in code. Animations are
    /// the beats themselves (AnimationDriver); this director handles the audible
    /// and tactile halves, with the GDD 7.2 pitch modulations.
    /// </summary>
    public sealed class JuiceDirector : MonoBehaviour
    {
        private AnimationDriver driver = null!;
        private AudioSource sfxSource = null!;

        public static JuiceDirector Create(Transform parent, AnimationDriver driver)
        {
            var go = new GameObject("JuiceDirector");
            go.transform.SetParent(parent, false);
            var juice = go.AddComponent<JuiceDirector>();
            juice.driver = driver;
            juice.sfxSource = go.AddComponent<AudioSource>();
            driver.BeatStarted += juice.OnBeat;
            return juice;
        }

        private void OnDestroy()
        {
            if (driver != null)
            {
                driver.BeatStarted -= OnBeat;
            }
        }

        private static bool SoundOn => PlayerPrefs.GetInt("settings.audio.on", 1) == 1;

        private void OnBeat(string beat, MoveResult result)
        {
            if (!ThemeRuntime.Theme.Juice.TryGetValue(beat, out JuiceEntry? entry) || entry == null)
            {
                return;
            }

            PlaySfx(entry.Sfx, Pitch(beat, result));
            PlayHaptic(entry.Haptic);
        }

        private void PlaySfx(string sfxName, float pitch)
        {
            if (!SoundOn || string.IsNullOrEmpty(sfxName))
            {
                return;
            }

            if (!Enum.TryParse(sfxName, out SfxId id))
            {
                return; // Unknown table entry: silent, never a crash.
            }

            sfxSource.pitch = pitch;
            sfxSource.PlayOneShot(AudioSynth.Sfx(id), 0.85f);
        }

        private static void PlayHaptic(string tier)
        {
            switch (tier)
            {
                case "light":
                    Haptics.Light();
                    break;
                case "medium":
                    Haptics.Medium();
                    break;
                case "heavy":
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
}
