using System;
using System.Collections.Generic;

namespace Riptide.Core
{
    /// <summary>GDD §16.1 rarity tiers, common → legendary.</summary>
    public enum PalRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary,
    }

    /// <summary>GDD §16.1 Pod perks — one per line, scaling by stage.</summary>
    public enum PalPerk
    {
        Coins,
        StarCoins,
        TideInterval,
        BoosterDiscount,
        StartWater,
        FreePops,
        Incubation,
        FreeContinues,
        GiftBoost,
        QuestBoost,
        DeepLuck,
        BondBoost,
    }

    /// <summary>One Tideling line (internally "pal"): three named stages — Baby, Grown, Grand — a rarity, a perk (§16.1).</summary>
    public sealed class PalLine
    {
        public int Id { get; }
        public string Key { get; }
        public string Name { get; }
        public PalRarity Rarity { get; }

        /// <summary>The board species this line is (0..7), or −1 for an egg-only Deep Tideling.</summary>
        public int BoardSpecies { get; }

        public IReadOnlyList<string> Stages { get; }
        public PalPerk Perk { get; }

        /// <summary>The perk's value at stages I, II, III.</summary>
        public IReadOnlyList<int> PerkValues { get; }

        /// <summary>A second per-stage value (Deep Luck's egg tier-up %), else all zero.</summary>
        public IReadOnlyList<int> PerkValues2 { get; }

        public PalLine(int id, string key, string name, PalRarity rarity, int boardSpecies, IReadOnlyList<string> stages,
            PalPerk perk, IReadOnlyList<int> perkValues, IReadOnlyList<int> perkValues2)
        {
            Id = id;
            Key = key;
            Name = name;
            Rarity = rarity;
            BoardSpecies = boardSpecies;
            Stages = stages;
            Perk = perk;
            PerkValues = perkValues;
            PerkValues2 = perkValues2;
        }

        public int PerkAt(int stage) => stage >= 1 && stage <= PerkValues.Count ? PerkValues[stage - 1] : 0;

        public int Perk2At(int stage) => stage >= 1 && stage <= PerkValues2.Count ? PerkValues2[stage - 1] : 0;
    }

    /// <summary>One egg type (§16.3): warmth to hatch, the drop table, the overflow payout.</summary>
    public sealed class EggType
    {
        public int Index { get; }
        public string Id { get; }
        public int RowsToHatch { get; }
        public int OverflowCoins { get; }

        /// <summary>Weight per pal line id (0 = can't hatch from this egg).</summary>
        public IReadOnlyList<int> Weights { get; }

        public int TotalWeight { get; }

        public EggType(int index, string id, int rowsToHatch, int overflowCoins, IReadOnlyList<int> weights)
        {
            Index = index;
            Id = id;
            RowsToHatch = rowsToHatch;
            OverflowCoins = overflowCoins;
            Weights = weights;
            int total = 0;
            foreach (int w in weights)
            {
                total += w;
            }

            TotalWeight = total;
        }
    }

    /// <summary>Where eggs come from (§16.3); every value an egg index.</summary>
    public sealed class EggSources
    {
        public int Welcome { get; set; }
        public IReadOnlyDictionary<int, int> VoyageMilestones { get; set; } = new Dictionary<int, int>();
        public int DeepZoneFrom { get; set; }
        public int DeepZoneLevel20 { get; set; }
        public int DailyWin { get; set; }
        public int QuestsAllClaimed { get; set; }
        public int EventWin { get; set; }
        public int EndlessEveryTides { get; set; }
        public int EndlessEgg { get; set; }
        public int EndlessMaxPerRun { get; set; }
    }

    /// <summary>All of pals.json (GDD §16 Tidelings).</summary>
    public sealed class PalsConfig
    {
        public IReadOnlyList<PalLine> Lines { get; set; } = Array.Empty<PalLine>();
        public IReadOnlyList<EggType> Eggs { get; set; } = Array.Empty<EggType>();

        /// <summary>[rarity][0] bond to grow to stage II (Grown), [rarity][1] to III (Grand).</summary>
        public int[][] GrowBond { get; set; } = Array.Empty<int[]>();

        public int[] DuplicateBond { get; set; } = Array.Empty<int>();
        public int RescueBond { get; set; }
        public int RunBond { get; set; }
        public int LeaderRunBond { get; set; }
        public int SparkleOneIn { get; set; }
        public int NestCapacity { get; set; }

        /// <summary>Pals owned needed to open Pod slot i (slot 0 is always open).</summary>
        public int[] PodUnlocks { get; set; } = Array.Empty<int>();

        public int GiftCoinsPerStage { get; set; }
        public int GiftCap { get; set; }
        public int LineCompleteCoins { get; set; }
        public EggSources Sources { get; set; } = new EggSources();

        public int PodSize => PodUnlocks.Length;

        /// <summary>The line a board species belongs to (−1 when none).</summary>
        public int LineForSpecies(int species)
        {
            foreach (PalLine line in Lines)
            {
                if (line.BoardSpecies == species)
                {
                    return line.Id;
                }
            }

            return -1;
        }

        public int EggIndex(string id)
        {
            foreach (EggType egg in Eggs)
            {
                if (egg.Id == id)
                {
                    return egg.Index;
                }
            }

            return -1;
        }
    }

    /// <summary>Parses + validates pals.json (errors carry the file label and line/column).</summary>
    public static class PalsLoader
    {
        public const int StageCount = 3;

        public static PalsConfig Load(string json, string sourceLabel, int boardSpeciesCount)
        {
            try
            {
                JsonObject root = JsonParser.Parse(json).AsObject();
                var cfg = new PalsConfig();

                var lines = new List<PalLine>();
                var keys = new Dictionary<string, int>(StringComparer.Ordinal);
                var boardSeen = new HashSet<int>();
                foreach (JsonValue node in root.Require("lines").AsArray().Items)
                {
                    JsonObject o = node.AsObject();
                    int id = o.Require("id").AsInt();
                    if (id != lines.Count)
                    {
                        throw new JsonParseException($"line ids must be 0..n-1 in order (got {id})", o.Line, o.Column);
                    }

                    string key = o.Require("key").AsString();
                    if (key.Length == 0 || keys.ContainsKey(key))
                    {
                        throw new JsonParseException($"duplicate or empty line key '{key}'", o.Line, o.Column);
                    }

                    keys[key] = id;
                    int board = o.Require("boardSpecies").AsInt();
                    if (board < -1 || board >= boardSpeciesCount || (board >= 0 && !boardSeen.Add(board)))
                    {
                        throw new JsonParseException($"line '{key}' boardSpecies {board} invalid or reused", o.Line, o.Column);
                    }

                    lines.Add(new PalLine(id, key, o.Require("name").AsString(), ParseEnum<PalRarity>(o, "rarity"), board,
                        Strings(o, "stages", StageCount), ParseEnum<PalPerk>(o, "perk"), Ints(o, "perkValues", StageCount),
                        o.Optional("perkValues2") != null ? Ints(o, "perkValues2", StageCount) : new int[StageCount]));
                }

                if (lines.Count < 1)
                {
                    throw new JsonParseException("lines must not be empty", root.Line, root.Column);
                }

                if (boardSeen.Count != boardSpeciesCount)
                {
                    throw new JsonParseException($"every board species needs a pal line ({boardSeen.Count}/{boardSpeciesCount})",
                        root.Line, root.Column);
                }

                cfg.Lines = lines;
                int rarities = Enum.GetValues(typeof(PalRarity)).Length;
                JsonObject grow = root.Require("growBond").AsObject();
                JsonObject dupes = root.Require("duplicateBond").AsObject();
                cfg.GrowBond = new int[rarities][];
                cfg.DuplicateBond = new int[rarities];
                for (int r = 0; r < rarities; r++)
                {
                    string name = ((PalRarity)r).ToString().ToLowerInvariant();
                    int[] pair = Ints(grow, name, StageCount - 1);
                    if (pair[0] < 1 || pair[1] <= pair[0])
                    {
                        throw new JsonParseException($"growBond.{name} must be ascending and >= 1", grow.Line, grow.Column);
                    }

                    cfg.GrowBond[r] = pair;
                    cfg.DuplicateBond[r] = NonNegative(dupes, name);
                }

                cfg.RescueBond = NonNegative(root, "rescueBond");
                cfg.RunBond = NonNegative(root, "runBond");
                cfg.LeaderRunBond = NonNegative(root, "leaderRunBond");
                cfg.SparkleOneIn = Positive(root, "sparkleOneIn");
                cfg.NestCapacity = Positive(root, "nestCapacity");
                cfg.PodUnlocks = Ints(root, "podUnlocks", -1);
                if (cfg.PodUnlocks.Length < 1 || cfg.PodUnlocks[0] != 0)
                {
                    throw new JsonParseException("podUnlocks must start at 0 (slot 1 is always open)", root.Line, root.Column);
                }

                for (int i = 1; i < cfg.PodUnlocks.Length; i++)
                {
                    if (cfg.PodUnlocks[i] < cfg.PodUnlocks[i - 1])
                    {
                        throw new JsonParseException("podUnlocks must not descend", root.Line, root.Column);
                    }
                }

                cfg.GiftCoinsPerStage = NonNegative(root, "giftCoinsPerStage");
                cfg.GiftCap = NonNegative(root, "giftCap");
                cfg.LineCompleteCoins = NonNegative(root, "lineCompleteCoins");
                cfg.Eggs = LoadEggs(root, keys, lines.Count);
                cfg.Sources = LoadSources(root.Require("eggSources").AsObject(), cfg);
                return cfg;
            }
            catch (JsonParseException ex)
            {
                throw new ContentException(sourceLabel, ex.Message);
            }
            catch (ArgumentException ex)
            {
                throw new ContentException(sourceLabel, ex.Message);
            }
        }

        private static List<EggType> LoadEggs(JsonObject root, Dictionary<string, int> keys, int lineCount)
        {
            var eggs = new List<EggType>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonValue node in root.Require("eggs").AsArray().Items)
            {
                JsonObject e = node.AsObject();
                string id = e.Require("id").AsString();
                if (id.Length == 0 || !ids.Add(id))
                {
                    throw new JsonParseException($"duplicate or empty egg id '{id}'", e.Line, e.Column);
                }

                var weights = new int[lineCount];
                JsonObject w = e.Require("weights").AsObject();
                foreach (string key in w.MemberNames)
                {
                    if (!keys.TryGetValue(key, out int line))
                    {
                        throw new JsonParseException($"egg '{id}' weights an unknown line '{key}'", w.Line, w.Column);
                    }

                    weights[line] = NonNegative(w, key);
                }

                var egg = new EggType(eggs.Count, id, Positive(e, "rowsToHatch"), NonNegative(e, "overflowCoins"), weights);
                if (egg.TotalWeight < 1)
                {
                    throw new JsonParseException($"egg '{id}' can hatch nothing", e.Line, e.Column);
                }

                eggs.Add(egg);
            }

            if (eggs.Count < 1)
            {
                throw new JsonParseException("eggs must not be empty", root.Line, root.Column);
            }

            return eggs;
        }

        private static EggSources LoadSources(JsonObject s, PalsConfig cfg)
        {
            var milestones = new Dictionary<int, int>();
            JsonObject m = s.Require("voyageMilestones").AsObject();
            foreach (string level in m.MemberNames)
            {
                if (!int.TryParse(level, out int index) || index < 1 || index > 20)
                {
                    throw new JsonParseException($"voyage milestone level '{level}' must be 1..20", m.Line, m.Column);
                }

                milestones[index] = Egg(m, level, cfg);
            }

            return new EggSources
            {
                Welcome = Egg(s, "welcome", cfg),
                VoyageMilestones = milestones,
                DeepZoneFrom = Positive(s, "deepZoneFrom"),
                DeepZoneLevel20 = Egg(s, "deepZoneLevel20", cfg),
                DailyWin = Egg(s, "dailyWin", cfg),
                QuestsAllClaimed = Egg(s, "questsAllClaimed", cfg),
                EventWin = Egg(s, "eventWin", cfg),
                EndlessEveryTides = Positive(s, "endlessEveryTides"),
                EndlessEgg = Egg(s, "endlessEgg", cfg),
                EndlessMaxPerRun = NonNegative(s, "endlessMaxPerRun"),
            };
        }

        private static int Egg(JsonObject obj, string name, PalsConfig cfg)
        {
            JsonValue node = obj.Require(name);
            int index = cfg.EggIndex(node.AsString());
            if (index < 0)
            {
                throw new JsonParseException($"'{name}' names an unknown egg '{node.AsString()}'", node.Line, node.Column);
            }

            return index;
        }

        private static string[] Strings(JsonObject obj, string name, int count)
        {
            JsonArray array = obj.Require(name).AsArray();
            if (array.Count != count)
            {
                throw new JsonParseException($"'{name}' needs exactly {count} entries", array.Line, array.Column);
            }

            var result = new string[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = array.Items[i].AsString();
                if (result[i].Length == 0)
                {
                    throw new JsonParseException($"'{name}' entries must not be empty", array.Line, array.Column);
                }
            }

            return result;
        }

        /// <summary>A non-negative int array; <paramref name="count"/> −1 accepts any length.</summary>
        private static int[] Ints(JsonObject obj, string name, int count)
        {
            JsonArray array = obj.Require(name).AsArray();
            if (count >= 0 && array.Count != count)
            {
                throw new JsonParseException($"'{name}' needs exactly {count} entries", array.Line, array.Column);
            }

            var result = new int[array.Count];
            for (int i = 0; i < array.Count; i++)
            {
                result[i] = array.Items[i].AsInt();
                if (result[i] < 0)
                {
                    throw new JsonParseException($"'{name}' entries must be >= 0", array.Line, array.Column);
                }
            }

            return result;
        }

        private static T ParseEnum<T>(JsonObject obj, string name) where T : struct
        {
            JsonValue node = obj.Require(name);
            string text = node.AsString();
            if (!Enum.TryParse(text, ignoreCase: true, out T value) || int.TryParse(text, out _))
            {
                throw new JsonParseException($"'{name}' value '{text}' is not a known {typeof(T).Name}", node.Line, node.Column);
            }

            return value;
        }

        private static int Positive(JsonObject obj, string name)
        {
            JsonValue node = obj.Require(name);
            int value = node.AsInt();
            if (value < 1)
            {
                throw new JsonParseException($"'{name}' must be >= 1", node.Line, node.Column);
            }

            return value;
        }

        private static int NonNegative(JsonObject obj, string name)
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
