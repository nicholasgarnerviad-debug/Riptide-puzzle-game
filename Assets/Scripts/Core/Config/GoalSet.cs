using System;

namespace Riptide.Core
{
    /// <summary>
    /// GDD 3.1 goal types, mixable per level: RescueAll(N), ClearRows(n),
    /// SurviveTides(n), Score(n). Null = goal type absent. Goals are AND-ed.
    /// Endless uses <see cref="None"/> (a run never "wins").
    /// </summary>
    public sealed class GoalSet
    {
        public int? RescueAllTarget { get; }
        public int? ClearRowsTarget { get; }
        public int? SurviveTidesTarget { get; }
        public long? ScoreTarget { get; }

        public static readonly GoalSet None = new GoalSet(null, null, null, null);

        public GoalSet(int? rescueAllTarget, int? clearRowsTarget, int? surviveTidesTarget, long? scoreTarget)
        {
            if (rescueAllTarget.HasValue && rescueAllTarget.Value < 1) throw new ArgumentOutOfRangeException(nameof(rescueAllTarget));
            if (clearRowsTarget.HasValue && clearRowsTarget.Value < 1) throw new ArgumentOutOfRangeException(nameof(clearRowsTarget));
            if (surviveTidesTarget.HasValue && surviveTidesTarget.Value < 1) throw new ArgumentOutOfRangeException(nameof(surviveTidesTarget));
            if (scoreTarget.HasValue && scoreTarget.Value < 1) throw new ArgumentOutOfRangeException(nameof(scoreTarget));

            RescueAllTarget = rescueAllTarget;
            ClearRowsTarget = clearRowsTarget;
            SurviveTidesTarget = surviveTidesTarget;
            ScoreTarget = scoreTarget;
        }

        public bool HasAny =>
            RescueAllTarget.HasValue || ClearRowsTarget.HasValue || SurviveTidesTarget.HasValue || ScoreTarget.HasValue;

        /// <summary>GDD 2.2/2.5: creature loss fails the level only when a rescue goal exists.</summary>
        public bool HasRescueGoal => RescueAllTarget.HasValue;
    }
}
