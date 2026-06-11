using System;

namespace Riptide.Core
{
    /// <summary>
    /// Immutable goal progress (GDD 3.1). Goals are AND-ed; an empty GoalSet
    /// (Endless) is never satisfied.
    /// </summary>
    public sealed class GoalState
    {
        public GoalSet Goals { get; }
        public int Rescued { get; }
        public int RowsCleared { get; }
        public int TidesSurvived { get; }

        public GoalState(GoalSet goals, int rescued, int rowsCleared, int tidesSurvived)
        {
            if (rescued < 0) throw new ArgumentOutOfRangeException(nameof(rescued));
            if (rowsCleared < 0) throw new ArgumentOutOfRangeException(nameof(rowsCleared));
            if (tidesSurvived < 0) throw new ArgumentOutOfRangeException(nameof(tidesSurvived));

            Goals = goals ?? throw new ArgumentNullException(nameof(goals));
            Rescued = rescued;
            RowsCleared = rowsCleared;
            TidesSurvived = tidesSurvived;
        }

        public GoalState AddProgress(int rescues, int rowsCleared, int tidesSurvived) =>
            new GoalState(Goals, Rescued + rescues, RowsCleared + rowsCleared, TidesSurvived + tidesSurvived);

        public bool IsSatisfied(long score)
        {
            if (!Goals.HasAny)
            {
                return false;
            }

            if (Goals.RescueAllTarget.HasValue && Rescued < Goals.RescueAllTarget.Value) return false;
            if (Goals.ClearRowsTarget.HasValue && RowsCleared < Goals.ClearRowsTarget.Value) return false;
            if (Goals.SurviveTidesTarget.HasValue && TidesSurvived < Goals.SurviveTidesTarget.Value) return false;
            if (Goals.ScoreTarget.HasValue && score < Goals.ScoreTarget.Value) return false;
            return true;
        }
    }
}
