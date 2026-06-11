using System;

namespace Riptide.Core
{
    /// <summary>
    /// Derives the effective tide interval and piece weights from GameState
    /// counters (GDD 3.2). Pure functions of (config, counters) — replay-exact.
    /// Without an EscalationConfig both pass the base values through untouched,
    /// which keeps the Phase 2 goldens and every static level bit-identical.
    /// </summary>
    public static class EscalationRules
    {
        /// <summary>"Big" is derived from the catalog, not hardcoded: 5+ cells (Sq3, I5H, I5V).</summary>
        private static readonly bool[] IsBigPiece = BuildBigMask();

        private static bool[] BuildBigMask()
        {
            var mask = new bool[PieceCatalog.PieceCount];
            for (int i = 0; i < mask.Length; i++)
            {
                mask[i] = PieceCatalog.CellCountOf((PieceId)i) >= 5;
            }

            return mask;
        }

        public static int EffectiveTideInterval(LevelConfig config, int tidesSurvived)
        {
            EscalationConfig? esc = config.Escalation;
            if (esc == null)
            {
                return config.TideInterval;
            }

            int shrink = tidesSurvived / esc.IntervalShrinkEveryTides;
            return Math.Max(esc.IntervalFloor, config.TideInterval - shrink);
        }

        /// <summary>
        /// Returns the deal weights for the current move count. Allocation-free when
        /// no escalation applies (base array reference); copied + boosted otherwise.
        /// </summary>
        public static int[] EffectiveWeights(LevelConfig config, int moveCount, out int totalWeight)
        {
            EscalationConfig? esc = config.Escalation;
            if (esc == null || esc.BigWeightBonusPerStep == 0)
            {
                totalWeight = config.TotalPieceWeight;
                return config.PieceWeightsView;
            }

            int steps = Math.Min(moveCount / esc.WeightEscalationEveryPlacements, esc.MaxEscalationSteps);
            if (steps == 0)
            {
                totalWeight = config.TotalPieceWeight;
                return config.PieceWeightsView;
            }

            int[] baseWeights = config.PieceWeightsView;
            var weights = new int[baseWeights.Length];
            int total = 0;
            int bonus = esc.BigWeightBonusPerStep * steps;
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = baseWeights[i] + (IsBigPiece[i] ? bonus : 0);
                total += weights[i];
            }

            totalWeight = total;
            return weights;
        }
    }
}
