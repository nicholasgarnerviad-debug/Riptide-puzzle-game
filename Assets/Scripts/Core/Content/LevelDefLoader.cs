using System;
using System.Collections.Generic;

namespace Riptide.Core
{
    /// <summary>
    /// One level definition from a zone file (GDD 3.1): identity, water params,
    /// tide interval, resolved piece weights, goals, optional par, grid preset.
    /// </summary>
    public sealed class LevelDef
    {
        public string Id { get; }
        public int Zone { get; }
        public int StartWaterLevel { get; }
        public int MinWaterLevel { get; }
        public int TideInterval { get; }

        /// <summary>GDD 3.1: par feeds star ratings only; running out of moves never fails a level.</summary>
        public int? ParMoves { get; }

        public GoalSet Goals { get; }
        public IReadOnlyList<int> PieceWeights { get; }
        public IReadOnlyList<PresetCell> Preset { get; }

        public LevelDef(string id, int zone, int startWaterLevel, int minWaterLevel, int tideInterval,
            int? parMoves, GoalSet goals, IReadOnlyList<int> pieceWeights, IReadOnlyList<PresetCell> preset)
        {
            Id = id;
            Zone = zone;
            StartWaterLevel = startWaterLevel;
            MinWaterLevel = minWaterLevel;
            TideInterval = tideInterval;
            ParMoves = parMoves;
            Goals = goals;
            PieceWeights = pieceWeights;
            Preset = preset;
        }

        /// <summary>Builds the sim config. Voyage levels never auto-spawn creatures (GDD 2.5: Endless only).</summary>
        public LevelConfig ToLevelConfig(EconomyConfig economy, int creatureSpeciesCount, bool awardTideSurvival = false)
        {
            return new LevelConfig(
                StartWaterLevel,
                MinWaterLevel,
                TideInterval,
                creatureSpawnIntervalTrays: 0,
                creatureSpeciesCount,
                economy.DealColorCount,
                PieceWeights,
                economy.BuildScoring(awardTideSurvival),
                Goals,
                Preset);
        }
    }

    /// <summary>
    /// Loader + validator for zone level files (master prompt 2C). Every violation
    /// throws a ContentException naming the source and the JSON line/column.
    /// </summary>
    public static class LevelDefLoader
    {
        public static IReadOnlyList<LevelDef> LoadZone(string json, string sourceLabel, EconomyConfig economy)
        {
            if (economy == null) throw new ArgumentNullException(nameof(economy));
            try
            {
                JsonArray root = JsonParser.Parse(json).AsArray();
                if (root.Count == 0)
                {
                    throw new JsonParseException("Zone file must contain at least one level", root.Line, root.Column);
                }

                var ids = new HashSet<string>(StringComparer.Ordinal);
                var levels = new List<LevelDef>(root.Count);
                foreach (JsonValue item in root.Items)
                {
                    LevelDef level = ParseLevel(item.AsObject(), economy);
                    if (!ids.Add(level.Id))
                    {
                        throw new JsonParseException($"Duplicate level id '{level.Id}'", item.Line, item.Column);
                    }

                    levels.Add(level);
                }

                return levels;
            }
            catch (JsonParseException ex)
            {
                throw new ContentException(sourceLabel, ex.Message);
            }
        }

        private static LevelDef ParseLevel(JsonObject obj, EconomyConfig economy)
        {
            JsonValue idNode = obj.Require("id");
            string id = idNode.AsString();
            if (id.Length == 0)
            {
                throw new JsonParseException("Level id must not be empty", idNode.Line, idNode.Column);
            }

            int zone = RangeInt(obj, "zone", 1, 10);
            int startWater = RangeInt(obj, "startWaterLevel", 0, BoardSpec.DrownWaterLevel - 1);
            int minWater = RangeInt(obj, "minWaterLevel", 0, startWater);
            int tideInterval = RangeInt(obj, "tideInterval", 1, int.MaxValue);

            int? parMoves = null;
            JsonValue? parNode = obj.Optional("parMoves");
            if (parNode != null)
            {
                int par = parNode.AsInt();
                if (par < 1)
                {
                    throw new JsonParseException("'parMoves' must be >= 1", parNode.Line, parNode.Column);
                }

                parMoves = par;
            }

            GoalSet goals = ParseGoals(obj.Require("goals").AsObject());
            IReadOnlyList<int> weights = ResolveWeights(obj, economy);
            IReadOnlyList<PresetCell> preset = ParsePreset(obj, startWater, economy.DealColorCount);

            return new LevelDef(id, zone, startWater, minWater, tideInterval, parMoves, goals, weights, preset);
        }

        private static GoalSet ParseGoals(JsonObject goals)
        {
            int? rescueAll = OptionalPositive(goals, "rescueAll");
            int? clearRows = OptionalPositive(goals, "clearRows");
            int? surviveTides = OptionalPositive(goals, "surviveTides");
            long? score = null;
            JsonValue? scoreNode = goals.Optional("score");
            if (scoreNode != null)
            {
                long value = scoreNode.AsLong();
                if (value < 1)
                {
                    throw new JsonParseException("'score' goal must be >= 1", scoreNode.Line, scoreNode.Column);
                }

                score = value;
            }

            if (rescueAll == null && clearRows == null && surviveTides == null && score == null)
            {
                throw new JsonParseException("A level needs at least one goal (GDD 3.1)", goals.Line, goals.Column);
            }

            return new GoalSet(rescueAll, clearRows, surviveTides, score);
        }

        private static IReadOnlyList<int> ResolveWeights(JsonObject obj, EconomyConfig economy)
        {
            JsonValue? bandNode = obj.Optional("weightBand");
            JsonValue? inlineNode = obj.Optional("pieceWeights");
            if (bandNode != null && inlineNode != null)
            {
                throw new JsonParseException("Use 'weightBand' or 'pieceWeights', not both", bandNode.Line, bandNode.Column);
            }

            if (bandNode != null)
            {
                int band = bandNode.AsInt();
                if (!economy.PieceWeightBands.TryGetValue(band, out IReadOnlyList<int>? weights))
                {
                    throw new JsonParseException($"weightBand {band} is not defined in economy.json", bandNode.Line, bandNode.Column);
                }

                return weights;
            }

            if (inlineNode != null)
            {
                return EconomyLoader.ReadWeights(inlineNode, "'pieceWeights'");
            }

            throw new JsonParseException("Level needs 'weightBand' or 'pieceWeights'", obj.Line, obj.Column);
        }

        private static IReadOnlyList<PresetCell> ParsePreset(JsonObject obj, int startWater, int colorCount)
        {
            JsonValue? presetNode = obj.Optional("preset");
            if (presetNode == null)
            {
                return Array.Empty<PresetCell>();
            }

            JsonArray array = presetNode.AsArray();
            var seen = new HashSet<int>();
            var cells = new List<PresetCell>(array.Count);
            foreach (JsonValue item in array.Items)
            {
                JsonObject cellObj = item.AsObject();
                int col = RangeInt(cellObj, "col", 0, BoardSpec.Width - 1);
                int row = RangeInt(cellObj, "row", 0, BoardSpec.Height - 1);
                var pos = new GridPos(col, row);
                if (!seen.Add(pos.Index))
                {
                    throw new JsonParseException($"Preset places ({col},{row}) twice", item.Line, item.Column);
                }

                JsonValue kindNode = cellObj.Require("cell");
                string kind = kindNode.AsString();
                Cell content;
                switch (kind)
                {
                    case "block":
                    {
                        int colorId = OptionalNonNegative(cellObj, "id") ?? 0;
                        if (colorId >= colorCount)
                        {
                            throw new JsonParseException($"Block color id {colorId} exceeds the {colorCount}-color palette",
                                kindNode.Line, kindNode.Column);
                        }

                        content = Cell.Block((byte)colorId);
                        break;
                    }

                    case "coral":
                        content = Cell.Coral;
                        break;

                    case "creature":
                    {
                        JsonValue idNode = cellObj.Require("id");
                        int creatureId = idNode.AsInt();
                        if (creatureId < 0 || creatureId > byte.MaxValue)
                        {
                            throw new JsonParseException($"Creature id {creatureId} is out of range", idNode.Line, idNode.Column);
                        }

                        content = Cell.Creature((byte)creatureId);
                        break;
                    }

                    default:
                        throw new JsonParseException($"Unknown cell kind '{kind}' (block|coral|creature)",
                            kindNode.Line, kindNode.Column);
                }

                bool live = content.Kind == CellKind.Block || content.Kind == CellKind.Creature;
                if (live && row < startWater)
                {
                    throw new JsonParseException(
                        $"Live {kind} at ({col},{row}) sits below startWaterLevel {startWater} (GDD 2.2)",
                        item.Line, item.Column);
                }

                cells.Add(new PresetCell(pos, content));
            }

            return cells;
        }

        private static int RangeInt(JsonObject obj, string name, int min, int max)
        {
            JsonValue node = obj.Require(name);
            int value = node.AsInt();
            if (value < min || value > max)
            {
                string bound = max == int.MaxValue ? $">= {min}" : $"in {min}..{max}";
                throw new JsonParseException($"'{name}' must be {bound}, got {value}", node.Line, node.Column);
            }

            return value;
        }

        private static int? OptionalPositive(JsonObject obj, string name)
        {
            JsonValue? node = obj.Optional(name);
            if (node == null)
            {
                return null;
            }

            int value = node.AsInt();
            if (value < 1)
            {
                throw new JsonParseException($"'{name}' must be >= 1", node.Line, node.Column);
            }

            return value;
        }

        private static int? OptionalNonNegative(JsonObject obj, string name)
        {
            JsonValue? node = obj.Optional(name);
            if (node == null)
            {
                return null;
            }

            int value = node.AsInt();
            if (value < 0)
            {
                throw new JsonParseException($"'{name}' must be >= 0", node.Line, node.Column);
            }

            return value;
        }
    }
}
