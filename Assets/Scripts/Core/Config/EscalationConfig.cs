using System;

namespace Riptide.Core
{
    /// <summary>
    /// GDD 3.2 endless escalation: the tide interval shrinks as tides pass and
    /// big pieces grow more frequent as placements accumulate. Pure data; the
    /// effective values are derived deterministically from GameState counters
    /// (EscalationRules), so replays stay exact. Null on a LevelConfig = static level.
    /// </summary>
    public sealed class EscalationConfig
    {
        /// <summary>Shrink the tide interval by 1 every N survived tides (GDD 3.2: 4).</summary>
        public int IntervalShrinkEveryTides { get; }

        /// <summary>Never shrink below this interval (GDD 3.2: 3).</summary>
        public int IntervalFloor { get; }

        /// <summary>Every N placements, big pieces gain weight (GDD 3.2: 25).</summary>
        public int WeightEscalationEveryPlacements { get; }

        /// <summary>Added to each big piece's weight per escalation step.</summary>
        public int BigWeightBonusPerStep { get; }

        /// <summary>Escalation steps stop accumulating here.</summary>
        public int MaxEscalationSteps { get; }

        public EscalationConfig(int intervalShrinkEveryTides, int intervalFloor,
            int weightEscalationEveryPlacements, int bigWeightBonusPerStep, int maxEscalationSteps)
        {
            if (intervalShrinkEveryTides < 1) throw new ArgumentOutOfRangeException(nameof(intervalShrinkEveryTides));
            if (intervalFloor < 1) throw new ArgumentOutOfRangeException(nameof(intervalFloor));
            if (weightEscalationEveryPlacements < 1) throw new ArgumentOutOfRangeException(nameof(weightEscalationEveryPlacements));
            if (bigWeightBonusPerStep < 0) throw new ArgumentOutOfRangeException(nameof(bigWeightBonusPerStep));
            if (maxEscalationSteps < 0) throw new ArgumentOutOfRangeException(nameof(maxEscalationSteps));

            IntervalShrinkEveryTides = intervalShrinkEveryTides;
            IntervalFloor = intervalFloor;
            WeightEscalationEveryPlacements = weightEscalationEveryPlacements;
            BigWeightBonusPerStep = bigWeightBonusPerStep;
            MaxEscalationSteps = maxEscalationSteps;
        }
    }
}
