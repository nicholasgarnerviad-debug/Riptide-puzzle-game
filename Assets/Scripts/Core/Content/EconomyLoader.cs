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

    /// <summary>GDD 3.2 endless escalation parameters (consumed by Phase 3 tooling and Phase 5 mode setup).</summary>
    public sealed class EndlessConfig
    {
        public int StartTideInterval { get; }
        public int IntervalShrinkEveryTides { get; }
        public int IntervalFloor { get; }
        public int WeightEscalationEveryPlacements { get; }
        public int CreatureSpawnIntervalTrays { get; }

        public EndlessConfig(int startTideInterval, int intervalShrinkEveryTides, int intervalFloor,
            int weightEscalationEveryPlacements, int creatureSpawnIntervalTrays)
        {
            StartTideInterval = startTideInterval;
            IntervalShrinkEveryTides = intervalShrinkEveryTides;
            IntervalFloor = intervalFloor;
            WeightEscalationEveryPlacements = weightEscalationEveryPlacements;
            CreatureSpawnIntervalTrays = creatureSpawnIntervalTrays;
        }
    }

    /// <summary>
    /// Typed view of economy.json (GDD 8.4: all tunables in JSON, rule 7: no balance
    /// numbers in C#). Phase 2 scope: scoring, deal palette, piece-weight bands,
    /// endless params. Coins/boosters join in Phase 6.
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

        public EconomyConfig(
            int pointsPerCell, int rowClearBase, int comboStartHalves, int comboStepHalves, int comboCapHalves,
            int rescuePoints, int creatureLossPenalty, int tideSurvivalBase, int tideSurvivalStep,
            int dealColorCount,
            IReadOnlyDictionary<int, IReadOnlyList<int>> pieceWeightBands,
            EndlessConfig endless)
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
                    RequirePositive(endless, "startTideInterval"),
                    RequirePositive(endless, "intervalShrinkEveryTides"),
                    RequirePositive(endless, "intervalFloor"),
                    RequirePositive(endless, "weightEscalationEveryPlacements"),
                    RequirePositive(endless, "creatureSpawnIntervalTrays"));

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
                    endlessConfig);
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
