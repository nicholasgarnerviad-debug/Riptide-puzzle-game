using System;

namespace Riptide.Core
{
    /// <summary>
    /// Assembles the sim configs for the three modes from economy.json data
    /// (GDD 3.1–3.3). Pure: the same economy + roster always yields identical
    /// configs, so daily boards match worldwide.
    /// </summary>
    public static class ModeFactory
    {
        /// <summary>GDD 3.2 Endless Tide: no goals, escalation on, survival scoring on.</summary>
        public static LevelConfig Endless(EconomyConfig economy, int creatureSpeciesCount)
        {
            if (economy == null) throw new ArgumentNullException(nameof(economy));
            EndlessConfig e = economy.Endless;
            return new LevelConfig(
                e.StartWaterLevel,
                e.StartWaterLevel,
                e.StartTideInterval,
                e.CreatureSpawnIntervalTrays,
                creatureSpeciesCount,
                economy.DealColorCount,
                economy.PieceWeightBands[e.WeightBand],
                economy.BuildScoring(awardTideSurvival: true),
                GoalSet.None,
                preset: null,
                escalation: new EscalationConfig(
                    e.IntervalShrinkEveryTides,
                    e.IntervalFloor,
                    e.WeightEscalationEveryPlacements,
                    e.BigWeightBonusPerStep,
                    e.MaxEscalationSteps));
        }

        /// <summary>
        /// GDD 3.3 Daily Riptide: endless ruleset + SurviveTides goal, survival
        /// scoring on. Booster rejection is wired in Phase 6.
        /// </summary>
        public static LevelConfig Daily(EconomyConfig economy, int creatureSpeciesCount)
        {
            if (economy == null) throw new ArgumentNullException(nameof(economy));
            DailyTuning d = economy.Daily;
            return new LevelConfig(
                d.StartWaterLevel,
                d.StartWaterLevel,
                d.StartTideInterval,
                d.CreatureSpawnIntervalTrays,
                creatureSpeciesCount,
                economy.DealColorCount,
                economy.PieceWeightBands[d.WeightBand],
                economy.BuildScoring(awardTideSurvival: true),
                new GoalSet(null, null, d.SurviveTides, null),
                preset: null,
                escalation: new EscalationConfig(
                    d.IntervalShrinkEveryTides,
                    d.IntervalFloor,
                    economy.Endless.WeightEscalationEveryPlacements,
                    d.BigWeightBonusPerStep,
                    d.MaxEscalationSteps));
        }
    }
}
