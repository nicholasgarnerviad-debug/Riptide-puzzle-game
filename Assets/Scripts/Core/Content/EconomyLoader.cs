using System;
using System.Collections.Generic;

namespace Riptide.Core
{
    /// <summary>
    /// A content/schema violation in a named source file. The message always
    /// carries the source label plus line/column from the JSON node.
    /// </summary>
    public sealed class ContentException : Exception
    {
        public ContentException(string source, string message)
            : base($"{source}: {message}")
        {
        }
    }

    /// <summary>GDD 3.2 endless mode parameters (mode assembly in ModeFactory).</summary>
    public sealed class EndlessConfig
    {
        public int StartWaterLevel { get; }
        public int WeightBand { get; }
        public int StartTideInterval { get; }
        public int IntervalShrinkEveryTides { get; }
        public int IntervalFloor { get; }
        public int WeightEscalationEveryPlacements { get; }
        public int BigWeightBonusPerStep { get; }
        public int MaxEscalationSteps { get; }
        public int CreatureSpawnIntervalTrays { get; }

        public EndlessConfig(int startWaterLevel, int weightBand, int startTideInterval,
            int intervalShrinkEveryTides, int intervalFloor, int weightEscalationEveryPlacements,
            int bigWeightBonusPerStep, int maxEscalationSteps, int creatureSpawnIntervalTrays)
        {
            StartWaterLevel = startWaterLevel;
            WeightBand = weightBand;
            StartTideInterval = startTideInterval;
            IntervalShrinkEveryTides = intervalShrinkEveryTides;
            IntervalFloor = intervalFloor;
            WeightEscalationEveryPlacements = weightEscalationEveryPlacements;
            BigWeightBonusPerStep = bigWeightBonusPerStep;
            MaxEscalationSteps = maxEscalationSteps;
            CreatureSpawnIntervalTrays = creatureSpawnIntervalTrays;
        }
    }

    /// <summary>GDD 3.3 daily mode parameters — tuned independently of endless (3C).</summary>
    public sealed class DailyTuning
    {
        public int SurviveTides { get; }
        public int WeightBand { get; }
        public int StartWaterLevel { get; }
        public int StartTideInterval { get; }
        public int IntervalShrinkEveryTides { get; }
        public int IntervalFloor { get; }
        public int BigWeightBonusPerStep { get; }
        public int MaxEscalationSteps { get; }
        public int CreatureSpawnIntervalTrays { get; }

        public DailyTuning(int surviveTides, int weightBand, int startWaterLevel, int startTideInterval,
            int intervalShrinkEveryTides, int intervalFloor, int bigWeightBonusPerStep,
            int maxEscalationSteps, int creatureSpawnIntervalTrays)
        {
            SurviveTides = surviveTides;
            WeightBand = weightBand;
            StartWaterLevel = startWaterLevel;
            StartTideInterval = startTideInterval;
            IntervalShrinkEveryTides = intervalShrinkEveryTides;
            IntervalFloor = intervalFloor;
            BigWeightBonusPerStep = bigWeightBonusPerStep;
            MaxEscalationSteps = maxEscalationSteps;
            CreatureSpawnIntervalTrays = creatureSpawnIntervalTrays;
        }
    }

    /// <summary>GDD 4 / master prompt 3A: GreedyHeuristic bot weights — balance data, never C# constants.</summary>
    public sealed class GreedyHeuristicWeights
    {
        public int Clears { get; }
        public int Rescues { get; }
        public int WaterHeadroom { get; }
        public int Bumpiness { get; }
        public int CreatureDanger { get; }
        public int AlmostFullRows { get; }
        public int GameOverPenalty { get; }

        public GreedyHeuristicWeights(int clears, int rescues, int waterHeadroom, int bumpiness,
            int creatureDanger, int almostFullRows, int gameOverPenalty)
        {
            Clears = clears;
            Rescues = rescues;
            WaterHeadroom = waterHeadroom;
            Bumpiness = bumpiness;
            CreatureDanger = creatureDanger;
            AlmostFullRows = almostFullRows;
            GameOverPenalty = gameOverPenalty;
        }
    }

    /// <summary>
    /// Typed view of economy.json (GDD 8.4: all tunables in JSON, rule 7: no balance
    /// numbers in C#). Phase 3 scope: scoring, deal palette, weight bands, endless,
    /// daily, bot weights. Coins/boosters join in Phase 6.
    /// </summary>
    public sealed class EconomyConfig
    {
        private readonly int pointsPerCell;
        private readonly int rowClearBase;
        private readonly int comboStartHalves;
        private readonly int comboStepHalves;
        private readonly int comboCapHalves;
        private readonly int rescuePoints;
        private readonly int creatureLossPenalty;
        private readonly int tideSurvivalBase;
        private readonly int tideSurvivalStep;

        public int DealColorCount { get; }
        public IReadOnlyDictionary<int, IReadOnlyList<int>> PieceWeightBands { get; }
        public EndlessConfig Endless { get; }
        public DailyTuning Daily { get; }
        public GreedyHeuristicWeights GreedyHeuristic { get; }

        public EconomyConfig(
            int pointsPerCell, int rowClearBase, int comboStartHalves, int comboStepHalves, int comboCapHalves,
            int rescuePoints, int creatureLossPenalty, int tideSurvivalBase, int tideSurvivalStep,
            int dealColorCount,
            IReadOnlyDictionary<int, IReadOnlyList<int>> pieceWeightBands,
            EndlessConfig endless,
            DailyTuning daily,
            GreedyHeuristicWeights greedyHeuristic)
        {
            this.pointsPerCell = pointsPerCell;
            this.rowClearBase = rowClearBase;
            this.comboStartHalves = comboStartHalves;
            this.comboStepHalves = comboStepHalves;
            this.comboCapHalves = comboCapHalves;
            this.rescuePoints = rescuePoints;
            this.creatureLossPenalty = creatureLossPenalty;
            this.tideSurvivalBase = tideSurvivalBase;
            this.tideSurvivalStep = tideSurvivalStep;
            DealColorCount = dealColorCount;
            PieceWeightBands = pieceWeightBands ?? throw new ArgumentNullException(nameof(pieceWeightBands));
            Endless = endless ?? throw new ArgumentNullException(nameof(endless));
            Daily = daily ?? throw new ArgumentNullException(nameof(daily));
            GreedyHeuristic = greedyHeuristic ?? throw new ArgumentNullException(nameof(greedyHeuristic));
        }

        /// <summary>The mode decides survival scoring (GDD 10: Endless/Daily only).</summary>
        public ScoringConfig BuildScoring(bool awardTideSurvival) => new ScoringConfig(
            pointsPerCell, rowClearBase, comboStartHalves, comboStepHalves, comboCapHalves,
            rescuePoints, creatureLossPenalty, tideSurvivalBase, tideSurvivalStep, awardTideSurvival);
    }

    public static class EconomyLoader
    {
        /// <summary>Parses economy.json text. <paramref name="sourceLabel"/> names the file in errors.</summary>
        public static EconomyConfig Load(string json, string sourceLabel)
        {
            try
            {
                JsonObject root = JsonParser.Parse(json).AsObject();

                JsonObject scoring = root.Require("scoring").AsObject();
                JsonObject deal = root.Require("deal").AsObject();
                JsonObject bands = root.Require("pieceWeightBands").AsObject();
                JsonObject endless = root.Require("endless").AsObject();
                JsonObject daily = root.Require("daily").AsObject();
                JsonObject bot = root.Require("bot").AsObject();
                JsonObject heuristic = bot.Require("greedyHeuristic").AsObject();

                var bandTable = new Dictionary<int, IReadOnlyList<int>>();
                foreach (string key in bands.MemberNames)
                {
                    if (!int.TryParse(key, out int bandId) || bandId < 1)
                    {
                        JsonValue node = bands.Require(key);
                        throw new JsonParseException($"Band key '{key}' must be a positive integer", node.Line, node.Column);
                    }

                    bandTable[bandId] = ReadWeights(bands.Require(key), $"band '{key}'");
                }

                if (bandTable.Count == 0)
                {
                    throw new JsonParseException("pieceWeightBands must define at least one band", bands.Line, bands.Column);
                }

                var endlessConfig = new EndlessConfig(
                    RequireNonNegative(endless, "startWaterLevel"),
                    RequireBandRef(endless, "weightBand", bandTable),
                    RequirePositive(endless, "startTideInterval"),
                    RequirePositive(endless, "intervalShrinkEveryTides"),
                    RequirePositive(endless, "intervalFloor"),
                    RequirePositive(endless, "weightEscalationEveryPlacements"),
                    RequireNonNegative(endless, "bigWeightBonusPerStep"),
                    RequireNonNegative(endless, "maxEscalationSteps"),
                    RequirePositive(endless, "creatureSpawnIntervalTrays"));

                var dailyTuning = new DailyTuning(
                    RequirePositive(daily, "surviveTides"),
                    RequireBandRef(daily, "weightBand", bandTable),
                    RequireNonNegative(daily, "startWaterLevel"),
                    RequirePositive(daily, "startTideInterval"),
                    RequirePositive(daily, "intervalShrinkEveryTides"),
                    RequirePositive(daily, "intervalFloor"),
                    RequireNonNegative(daily, "bigWeightBonusPerStep"),
                    RequireNonNegative(daily, "maxEscalationSteps"),
                    RequirePositive(daily, "creatureSpawnIntervalTrays"));

                var heuristicWeights = new GreedyHeuristicWeights(
                    RequireNonNegative(heuristic, "clears"),
                    RequireNonNegative(heuristic, "rescues"),
                    RequireNonNegative(heuristic, "waterHeadroom"),
                    RequireNonNegative(heuristic, "bumpiness"),
                    RequireNonNegative(heuristic, "creatureDanger"),
                    RequireNonNegative(heuristic, "almostFullRows"),
                    RequireNonNegative(heuristic, "gameOverPenalty"));

                return new EconomyConfig(
                    RequireNonNegative(scoring, "pointsPerCell"),
                    RequireNonNegative(scoring, "rowClearBase"),
                    RequirePositive(scoring, "comboStartHalves"),
                    RequireNonNegative(scoring, "comboStepHalves"),
                    RequirePositive(scoring, "comboCapHalves"),
                    RequireNonNegative(scoring, "rescuePoints"),
                    RequireNonNegative(scoring, "creatureLossPenalty"),
                    RequireNonNegative(scoring, "tideSurvivalBase"),
                    RequireNonNegative(scoring, "tideSurvivalStep"),
                    RequirePositive(deal, "colorCount"),
                    bandTable,
                    endlessConfig,
                    dailyTuning,
                    heuristicWeights);
            }
            catch (JsonParseException ex)
            {
                throw new ContentException(sourceLabel, ex.Message);
            }
        }

        internal static IReadOnlyList<int> ReadWeights(JsonValue node, string context)
        {
            JsonArray array = node.AsArray();
            if (array.Count != PieceCatalog.PieceCount)
            {
                throw new JsonParseException(
                    $"{context} must list exactly {PieceCatalog.PieceCount} weights (one per piece), got {array.Count}",
                    array.Line, array.Column);
            }

            var weights = new int[PieceCatalog.PieceCount];
            int total = 0;
            for (int i = 0; i < array.Count; i++)
            {
                int weight = array.Items[i].AsInt();
                if (weight < 0)
                {
                    throw new JsonParseException($"{context}: weight for {(PieceId)i} is negative",
                        array.Items[i].Line, array.Items[i].Column);
                }

                weights[i] = weight;
                total += weight;
            }

            if (total < 1)
            {
                throw new JsonParseException($"{context}: weights must sum to at least 1", array.Line, array.Column);
            }

            return weights;
        }

        private static int RequireBandRef(JsonObject obj, string name, Dictionary<int, IReadOnlyList<int>> bands)
        {
            JsonValue node = obj.Require(name);
            int band = node.AsInt();
            if (!bands.ContainsKey(band))
            {
                throw new JsonParseException($"'{name}' references band {band}, which pieceWeightBands does not define",
                    node.Line, node.Column);
            }

            return band;
        }

        private static int RequirePositive(JsonObject obj, string name)
        {
            JsonValue node = obj.Require(name);
            int value = node.AsInt();
            if (value < 1)
            {
                throw new JsonParseException($"'{name}' must be >= 1", node.Line, node.Column);
            }

            return value;
        }

        private static int RequireNonNegative(JsonObject obj, string name)
        {
            JsonValue node = obj.Require(name);
            int value = node.AsInt();
            if (value < 0)
            {
                throw new JsonParseException($"'{name}' must be >= 0", node.Line, node.Column);
            }

            return value;
        }
    }
}
