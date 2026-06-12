using Riptide.Core;
using Riptide.Game;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// GDD 7.2 music wiring: calm/tense loops crossfading at the danger read,
    /// the warning tone on a rise into danger, and the drown muffle duck.
    /// Per-event SFX + haptics moved to JuiceDirector (spec §7 juice table).
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        private GameFlow flow = null!;
        private AnimationDriver? driver;
        private AudioSource sfxSource = null!;
        private AudioSource calmSource = null!;
        private AudioSource tenseSource = null!;
        private float duck = 1f;

        public static AudioDirector Create(Transform parent, GameFlow flow, AnimationDriver? driver)
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
            director.SetDriver(driver);
            flow.RunStarted += director.OnRunStarted;
            return director;
        }

        /// <summary>
        /// 8-UI: the director lives at the app root (music must survive menus);
        /// the board-rig driver attaches once a run builds it.
        /// </summary>
        public void SetDriver(AnimationDriver? newDriver)
        {
            if (driver != null)
            {
                driver.BeatStarted -= OnBeat;
            }

            driver = newDriver;
            if (driver != null)
            {
                driver.BeatStarted += OnBeat;
            }
        }

        private void OnDestroy()
        {
            SetDriver(null);
            if (flow != null)
            {
                flow.RunStarted -= OnRunStarted;
            }
        }

        private static bool SoundOn => PlayerPrefs.GetInt("settings.audio.on", 1) == 1;

        private static bool MusicOn => PlayerPrefs.GetInt("settings.music.on", 1) == 1;

        public void PlayButton() => Play(SfxId.Button, 1f);

        private void OnRunStarted()
        {
            duck = 1f;
        }

        private void OnBeat(string beat, MoveResult result)
        {
            if (beat == "rise" && DangerRule.IsDanger(result.Next.WaterLevel))
            {
                Play(SfxId.DangerWarning, 1f); // GDD 7.2: warning tone at danger
            }
            else if (beat == "drown")
            {
                duck = 0.2f; // GDD 7.2: submerge muffle on the music (juice "musicMuffle400")
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
            if (!SoundOn || !MusicOn)
            {
                calmSource.volume = 0f;
                tenseSource.volume = 0f;
                return;
            }

            // GDD 7.2/7.3: the tense variant crossfades in at the danger read.
            int water = flow.Store != null ? flow.Store.State.WaterLevel : 0;
            bool danger = DangerRule.IsDanger(water) && flow.Screen == FlowScreen.Playing;
            float target = danger ? 1f : 0f;
            float fade = Time.deltaTime * 1.5f;
            float tense = Mathf.MoveTowards(tenseSource.volume / 0.55f, target, fade);
            duck = Mathf.MoveTowards(duck, flow.Screen == FlowScreen.Playing ? 1f : 0.85f, Time.deltaTime * 0.5f);
            tenseSource.volume = tense * 0.55f * duck;
            calmSource.volume = (1f - tense) * 0.5f * duck;
        }
    }
}
