using System;

namespace Riptide.Core
{
    /// <summary>
    /// GDD 10 scoring tunables. No defaults: every number is injected (rule 7,
    /// "no balance numbers in C#") — the canonical values live in economy.json
    /// (Phase 2C) and in test fixtures until then. Combo multipliers are stored
    /// in half-steps so scoring stays in integer math (x1=2, x1.5=3, x2=4, x2.5=5).
    /// </summary>
    public sealed class ScoringConfig
    {
        public int PointsPerCell { get; }
        public int RowClearBase { get; }
        public int ComboStartHalves { get; }
        public int ComboStepHalves { get; }
        public int ComboCapHalves { get; }
        public int RescuePoints { get; }
        public int CreatureLossPenalty { get; }
        public int TideSurvivalBase { get; }
        public int TideSurvivalStep { get; }

        /// <summary>GDD 10: tide-survival points apply in Endless/Daily only.</summary>
        public bool AwardTideSurvival { get; }

        public ScoringConfig(
            int pointsPerCell,
            int rowClearBase,
            int comboStartHalves,
            int comboStepHalves,
            int comboCapHalves,
            int rescuePoints,
            int creatureLossPenalty,
            int tideSurvivalBase,
            int tideSurvivalStep,
            bool awardTideSurvival)
        {
            if (pointsPerCell < 0) throw new ArgumentOutOfRangeException(nameof(pointsPerCell));
            if (rowClearBase < 0) throw new ArgumentOutOfRangeException(nameof(rowClearBase));
            if (comboStartHalves < 1) throw new ArgumentOutOfRangeException(nameof(comboStartHalves));
            if (comboStepHalves < 0) throw new ArgumentOutOfRangeException(nameof(comboStepHalves));
            if (comboCapHalves < comboStartHalves) throw new ArgumentOutOfRangeException(nameof(comboCapHalves));
            if (rescuePoints < 0) throw new ArgumentOutOfRangeException(nameof(rescuePoints));
            if (creatureLossPenalty < 0) throw new ArgumentOutOfRangeException(nameof(creatureLossPenalty));
            if (tideSurvivalBase < 0) throw new ArgumentOutOfRangeException(nameof(tideSurvivalBase));
            if (tideSurvivalStep < 0) throw new ArgumentOutOfRangeException(nameof(tideSurvivalStep));

            PointsPerCell = pointsPerCell;
            RowClearBase = rowClearBase;
            ComboStartHalves = comboStartHalves;
            ComboStepHalves = comboStepHalves;
            ComboCapHalves = comboCapHalves;
            RescuePoints = rescuePoints;
            CreatureLossPenalty = creatureLossPenalty;
            TideSurvivalBase = tideSurvivalBase;
            TideSurvivalStep = tideSurvivalStep;
            AwardTideSurvival = awardTideSurvival;
        }
    }
}
