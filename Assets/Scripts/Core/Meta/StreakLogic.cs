using System;

namespace Riptide.Core
{
    /// <summary>Immutable daily-streak state (GDD 3.3). Dates are epoch days (CivilDate).</summary>
    public readonly struct StreakState : IEquatable<StreakState>
    {
        public int Current { get; }
        public int Best { get; }
        public int FreezesHeld { get; }
        public long LastCompletedEpochDay { get; }
        public int LastFreezePurchaseWeek { get; }

        public StreakState(int current, int best, int freezesHeld, long lastCompletedEpochDay, int lastFreezePurchaseWeek)
        {
            Current = current;
            Best = best;
            FreezesHeld = freezesHeld;
            LastCompletedEpochDay = lastCompletedEpochDay;
            LastFreezePurchaseWeek = lastFreezePurchaseWeek;
        }

        public static readonly StreakState Empty = new StreakState(0, 0, 0, 0, 0);

        public bool Equals(StreakState other) =>
            Current == other.Current && Best == other.Best && FreezesHeld == other.FreezesHeld
            && LastCompletedEpochDay == other.LastCompletedEpochDay
            && LastFreezePurchaseWeek == other.LastFreezePurchaseWeek;

        public override bool Equals(object? obj) => obj is StreakState other && Equals(other);

        public override int GetHashCode() => Current ^ (FreezesHeld << 8) ^ (int)LastCompletedEpochDay;
    }

    /// <summary>
    /// GDD 3.3 streak rules (DECISIONS.md): +1 on consecutive days; a single missed
    /// day consumes a held freeze instead of resetting; otherwise reset to 1.
    /// At most one freeze held; acquisition limited to one per calendar week.
    /// </summary>
    public static class StreakLogic
    {
        public const int MaxFreezesHeld = 1;

        public static StreakState CompleteDaily(StreakState state, long todayEpochDay)
        {
            if (state.LastCompletedEpochDay == todayEpochDay && state.Current > 0)
            {
                return state; // already counted today
            }

            int next;
            int freezes = state.FreezesHeld;
            if (state.Current == 0)
            {
                next = 1;
            }
            else
            {
                long gap = todayEpochDay - state.LastCompletedEpochDay;
                if (gap == 1)
                {
                    next = state.Current + 1;
                }
                else if (gap == 2 && freezes > 0)
                {
                    freezes--;
                    next = state.Current + 1; // the freeze bridged the missed day
                }
                else
                {
                    next = 1;
                }
            }

            return new StreakState(next, Math.Max(state.Best, next), freezes,
                todayEpochDay, state.LastFreezePurchaseWeek);
        }

        public static bool CanAcquireFreeze(StreakState state, long todayEpochDay) =>
            state.FreezesHeld < MaxFreezesHeld
            && CivilDate.WeekIndex(todayEpochDay) != state.LastFreezePurchaseWeek;

        public static StreakState AcquireFreeze(StreakState state, long todayEpochDay)
        {
            if (!CanAcquireFreeze(state, todayEpochDay))
            {
                throw new InvalidOperationException("Freeze acquisition not available (held or already bought this week).");
            }

            return new StreakState(state.Current, state.Best, state.FreezesHeld + 1,
                state.LastCompletedEpochDay, CivilDate.WeekIndex(todayEpochDay));
        }
    }
}
