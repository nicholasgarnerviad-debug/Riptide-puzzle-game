using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// Spec §6.1: serializes overlapping move events into a beat sequence
    /// (clear → drain → rise plays as 3 beats, total ≤ t.resolveBudget) and is the
    /// single input-lock authority — locked during board resolution only.
    /// Beats are named with the juice-table keys so JuiceDirector maps 1:1.
    /// </summary>
    public sealed class UiEventQueue
    {
        private readonly List<(string Name, Func<IEnumerator>? Play)> beats =
            new List<(string, Func<IEnumerator>?)>();

        public event Action<string>? BeatStarted;

        public bool IsResolving { get; private set; }

        /// <summary>Beat names in queued order (tests assert the §2.6 sequence).</summary>
        public IReadOnlyList<string> PlannedBeats
        {
            get
            {
                var names = new List<string>(beats.Count);
                foreach ((string name, _) in beats)
                {
                    names.Add(name);
                }

                return names;
            }
        }

        /// <summary>A beat with no blocking duration (place thunk, rescue chirps, flourishes).</summary>
        public void AddInstant(string name) => beats.Add((name, null));

        public void Add(string name, Func<IEnumerator> play) => beats.Add((name, play));

        public IEnumerator Play()
        {
            IsResolving = true;
            foreach ((string name, Func<IEnumerator>? play) in beats)
            {
                BeatStarted?.Invoke(name);
                if (play != null)
                {
                    yield return play();
                }
            }

            IsResolving = false;
        }

        // §1.4: the user's reduced-motion toggle halves even the GDD-locked water
        // beats — an explicit accessibility override (DECISIONS.md). Queue and
        // WaterView share these so the input-lock window always matches.
        public static float DrainSeconds(bool multiDrain) =>
            ThemeRuntime.MotionSeconds(multiDrain ? "t.drainMulti" : "t.drain");

        public static float RiseSeconds() => ThemeRuntime.MotionSeconds("t.rise");

        /// <summary>Blocking time left for the clear beat after drain/rise take their cut.</summary>
        public static float ClearBudgetSeconds(bool drains, bool multiDrain, bool rises)
        {
            float budget = ThemeRuntime.Seconds("t.resolveBudget");
            float drain = drains ? DrainSeconds(multiDrain) : 0f;
            float rise = rises ? RiseSeconds() : 0f;
            return Mathf.Max(0f, budget - drain - rise);
        }

        /// <summary>
        /// Budgeted clear stagger: the nominal 30ms/cell pop stagger (§7) compresses
        /// whenever clear+drain+rise would exceed t.resolveBudget — the budget wins
        /// (DECISIONS.md). Returns seconds per cell.
        /// </summary>
        public static float ClearStagger(int cellCount, bool drains, bool multiDrain, bool rises)
        {
            const float popLife = 0.12f;
            const float nominal = 0.03f;
            if (cellCount <= 0 || ThemeRuntime.ReducedMotion)
            {
                return 0f; // §1.4: staggers off under reduced motion.
            }

            float fit = (ClearBudgetSeconds(drains, multiDrain, rises) - popLife) / cellCount;
            return Mathf.Clamp(fit, 0f, nominal);
        }
    }
}
