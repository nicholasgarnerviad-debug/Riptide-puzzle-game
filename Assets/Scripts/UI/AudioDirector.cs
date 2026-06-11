using Riptide.Core;
using Riptide.Game;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// GDD 7.2 audio wiring: SFX from MoveEvents (pitch up per combo, piece-size
    /// pitch on place), calm/tense music loops crossfading at water ≥ 7, drown
    /// muffle, plus the GDD 7.3 haptic tiers (light place / medium clear / heavy rise).
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        private GameFlow flow = null!;
        private AudioSource sfxSource = null!;
        private AudioSource calmSource = null!;
        private AudioSource tenseSource = null!;
        private float duck = 1f;

        public static AudioDirector Create(Transform parent, GameFlow flow)
        {
            var go = new GameObject("AudioDirector");
            go.transform.SetParent(parent, false);
            var director = go.AddComponent<AudioDirector>();
            director.flow = flow;
            director.sfxSource = go.AddComponent<AudioSource>();
            director.calmSource = go.AddComponent<AudioSource>();
            director.tenseSource = go.AddComponent<AudioSource>();
            director.calmSource.clip = AudioSynth.CalmLoop();
            director.tenseSource.clip = AudioSynth.TenseLoop();
            director.calmSource.loop = true;
            director.tenseSource.loop = true;
            director.calmSource.volume = 0.5f;
            director.tenseSource.volume = 0f;
            director.calmSource.Play();
            director.tenseSource.Play();

            if (flow.Store != null)
            {
                flow.Store.MoveApplied += director.OnMove;
            }

            flow.RunStarted += director.OnRunStarted;
            return director;
        }

        private void OnDestroy()
        {
            if (flow?.Store != null)
            {
                flow.Store.MoveApplied -= OnMove;
            }

            if (flow != null)
            {
                flow.RunStarted -= OnRunStarted;
            }
        }

        private static bool SoundOn => PlayerPrefs.GetInt("settings.audio.on", 1) == 1;

        public void PlayButton() => Play(SfxId.Button, 1f);

        private void OnRunStarted()
        {
            duck = 1f;
        }

        private void OnMove(Move move, MoveResult result)
        {
            MoveEvents events = result.Events;

            if (events.PlacedCells.Count > 0)
            {
                // GDD 7.2: soft thunk, pitch varies by piece size.
                Play(SfxId.Place, 1.25f - 0.05f * events.PlacedCells.Count);
                Haptics.Light();
            }

            if (events.RowsCleared.Count > 0)
            {
                // GDD 7.2: rising chime, +pitch per combo step.
                Play(SfxId.Clear, 0.9f + 0.1f * Mathf.Max(0, events.Scoring.ComboHalves - 2));
                Haptics.Medium();
                if (events.Scoring.ComboHalves >= 3)
                {
                    Play(SfxId.ComboShine, 1f);
                }
            }

            if (events.RescuedCreatures.Count > 0)
            {
                Play(SfxId.Rescue, 1f + 0.06f * result.Next.RescueStreak);
            }

            if (events.LostCreatures.Count > 0)
            {
                Play(SfxId.CreatureLost, 1f);
            }

            if (events.DrainAmount > 0)
            {
                Play(SfxId.Drain, 1f); // the hero moment (GDD 7.1)
            }

            if (events.TideRose)
            {
                Play(SfxId.Rise, 1f);
                Haptics.Heavy();
                if (result.Next.WaterLevel >= 7)
                {
                    Play(SfxId.DangerWarning, 1f); // GDD 7.2: warning tone at danger
                }
            }

            if (result.Next.Status == GameStatus.LostDrowned)
            {
                Play(SfxId.Drown, 1f);
                duck = 0.2f; // GDD 7.2: submerge muffle on the music
            }
            else if (result.Next.Status == GameStatus.Won)
            {
                Play(SfxId.StarAward, 1f);
            }
        }

        private void Play(SfxId id, float pitch)
        {
            if (!SoundOn)
            {
                return;
            }

            sfxSource.pitch = pitch;
            sfxSource.PlayOneShot(AudioSynth.Sfx(id), 0.85f);
        }

        private void Update()
        {
            if (!SoundOn)
            {
                calmSource.volume = 0f;
                tenseSource.volume = 0f;
                return;
            }

            // GDD 7.2/7.3: the tense variant crossfades in at water >= 7.
            int water = flow.Store != null ? flow.Store.State.WaterLevel : 0;
            bool danger = water >= 7 && flow.Screen == FlowScreen.Playing;
            float target = danger ? 1f : 0f;
            float fade = Time.deltaTime * 1.5f;
            float tense = Mathf.MoveTowards(tenseSource.volume / 0.55f, target, fade);
            duck = Mathf.MoveTowards(duck, flow.Screen == FlowScreen.Playing ? 1f : 0.85f, Time.deltaTime * 0.5f);
            tenseSource.volume = tense * 0.55f * duck;
            calmSource.volume = (1f - tense) * 0.5f * duck;
        }
    }
}
