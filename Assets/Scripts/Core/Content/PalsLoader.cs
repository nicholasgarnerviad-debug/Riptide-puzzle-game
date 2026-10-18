using System;
using System.Collections.Generic;

namespace Riptide.Core
{
    /// <summary>GDD §16.1 tiers. The tier sets both how often a Tideling arrives and how strong its ability is.</summary>
    public enum PalRarity
    {
        Common,
        Rare,
        Epic,
        Legendary,
    }

    /// <summary>
    /// GDD §16.4 Pod abilities, grouped by the tier that carries them: Common = small
    /// coin perks, Rare = run helpers, Epic = strong run perks, Legendary = signatures.
    /// </summary>
    public enum PalPerk
    {
        Coins,
        StarCoins,
        QuestBoost,
        BoosterDiscount,
        GiftBoost,
        RescueCoins,
        EggPace,
        TideInterval,
        FreePops,
        FreeRerolls,
        FreeContinues,
        DeepLuck,
        StartWater,
        BondBoost,
        FreeDrains,
        WhaleSong,
    }

    /// <summary>One Tideling line (internally "pal"): three named stages — Baby, Grown, Grand — a tier, an ability (§16.1).</summary>
    public sealed class PalLine
    {
        public int Id { get; }
        public string Key { get; }
        public string Name { get; }
        public PalRarity Rarity { get; }

        /// <summary>The board species this line is (0..7), or −1 for a Call-only Tideling.</summary>
        public int BoardSpecies { get; }

        public IReadOnlyList<string> Stages { get; }
        public PalPerk Perk { get; }

        /// <summary>The ability's value at stages Baby, Grown, Grand.</summary>
        public IReadOnlyList<int> PerkValues { get; }

        /// <summary>A second per-stage value (Deep Luck's pity cut, Whale Song's water), else all zero.</summary>
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

    /// <summary>The Call (§16.3): eggs in, a Tideling out — disclosed tier odds and two pity counters.</summary>
    public sealed class CallConfig
    {
        public int Cost { get; set; }
        public int TenCost { get; set; }

        /// <summary>Per-tier odds in permille (sum 1000), indexed by <see cref="PalRarity"/>.</summary>
        public int[] TierRatesPermille { get; set; } = Array.Empty<int>();

        /// <summary>An Epic-or-better is guaranteed on this call after the last one.</summary>
        public int EpicPity { get; set; }

        /// <summary>A Legendary is guaranteed on this call after the last one.</summary>
        public int LegendaryPity { get; set; }

        public bool TenCallGuaranteesRare { get; set; }
    }

    /// <summary>Where eggs come from and how many (§16.3); eggs are earned, never sold.</summary>
    public sealed class EggConfig
    {
        /// <summary>Finished boards (any mode, won or lost) per egg (§16.3).</summary>
        public int BoardsPerEgg { get; set; }
        public int Welcome { get; set; }
        public IReadOnlyDictionary<int, int> VoyageMilestones { get; set; } = new Dictionary<int, int>();
        public int DeepZoneFrom { get; set; }
        public int DeepZoneLevel20 { get; set; }
        public int DailyWin { get; set; }
        public int QuestsAllClaimed { get; set; }
        public int EventWin { get; set; }
        public int EndlessEveryTides { get; set; }
        public int EndlessEggs { get; set; }
        public int EndlessMaxPerRun { get; set; }
        public int RareRescue { get; set; }
    }

    /// <summary>All of pals.json (GDD §16 Tidelings).</summary>
    public sealed class PalsConfig
    {
        public IReadOnlyList<PalLine> Lines { get; set; } = Array.Empty<PalLine>();

        /// <summary>[tier][0] bond to grow to Grown, [tier][1] to Grand.</summary>
        public int[][] GrowBond { get; set; } = Array.Empty<int[]>();

        public int[] DuplicateBond { get; set; } = Array.Empty<int>();
        public int RescueBond { get; set; }
        public int RunBond { get; set; }
        public int LeaderRunBond { get; set; }
        public int SparkleOneIn { get; set; }

        /// <summary>Tidelings owned needed to open Pod slot i (slot 0 is always open).</summary>
        public int[] PodUnlocks { get; set; } = Array.Empty<int>();

        public int GiftCoinsPerStage { get; set; }
        public int GiftCap { get; set; }
        public int LineCompleteCoins { get; set; }
        public CallConfig Call { get; set; } = new CallConfig();
        public EggConfig Eggs { get; set; } = new EggConfig();

        public int PodSize => PodUnlocks.Length;

        private IReadOnlyList<int>[]? byTier;

        /// <summary>The line ids of one tier, in content order.</summary>
        public IReadOnlyList<int> LinesOfTier(PalRarity tier)
        {
            if (byTier == null)
            {
                int tiers = Enum.GetValues(typeof(PalRarity)).Length;
                var lists = new List<int>[tiers];
                for (int t = 0; t < tiers; t++)
                {
                    lists[t] = new List<int>();
                }

                foreach (PalLine line in Lines)
                {
                    lists[(int)line.Rarity].Add(line.Id);
                }

                byTier = lists;
            }

            return byTier[(int)tier];
        }

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
                var cfg = new PalsConfig { Lines = LoadLines(root, boardSpeciesCount) };

                int tiers = Enum.GetValues(typeof(PalRarity)).Length;
                JsonObject grow = root.Require("growBond").AsObject();
                JsonObject dupes = root.Require("duplicateBond").AsObject();
                cfg.GrowBond = new int[tiers][];
                cfg.DuplicateBond = new int[tiers];
                for (int t = 0; t < tiers; t++)
                {
                    string name = TierName(t);
                    int[] pair = Ints(grow, name, StageCount - 1);
                    if (pair[0] < 1 || pair[1] <= pair[0])
                    {
                        throw new JsonParseException($"growBond.{name} must be ascending and >= 1", grow.Line, grow.Column);
                    }

                    cfg.GrowBond[t] = pair;
                    cfg.DuplicateBond[t] = NonNegative(dupes, name);
                }

                cfg.RescueBond = NonNegative(root, "rescueBond");
                cfg.RunBond = NonNegative(root, "runBond");
                cfg.LeaderRunBond = NonNegative(root, "leaderRunBond");
                cfg.SparkleOneIn = Positive(root, "sparkleOneIn");
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
                cfg.Call = LoadCall(root.Require("call").AsObject(), cfg);
                cfg.Eggs = LoadEggs(root.Require("eggs").AsObject());
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

        private static List<PalLine> LoadLines(JsonObject root, int boardSpeciesCount)
        {
            var lines = new List<PalLine>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
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
                if (key.Length == 0 || !keys.Add(key))
                {
                    throw new JsonParseException($"duplicate or empty line key '{key}'", o.Line, o.Column);
                }

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
                throw new JsonParseException($"every board species needs a line ({boardSeen.Count}/{boardSpeciesCount})",
                    root.Line, root.Column);
            }

            return lines;
        }

        private static CallConfig LoadCall(JsonObject c, PalsConfig cfg)
        {
            int tiers = Enum.GetValues(typeof(PalRarity)).Length;
            JsonObject rates = c.Require("tierRatesPermille").AsObject();
            var permille = new int[tiers];
            int sum = 0;
            for (int t = 0; t < tiers; t++)
            {
                permille[t] = NonNegative(rates, TierName(t));
                sum += permille[t];
                if (permille[t] > 0 && cfg.LinesOfTier((PalRarity)t).Count == 0)
                {
                    throw new JsonParseException($"tier '{TierName(t)}' has odds but no Tidelings", rates.Line, rates.Column);
                }
            }

            if (sum != 1000)
            {
                throw new JsonParseException($"tierRatesPermille must sum to 1000 (got {sum})", rates.Line, rates.Column);
            }

            var call = new CallConfig
            {
                Cost = Positive(c, "cost"),
                TenCost = Positive(c, "tenCost"),
                TierRatesPermille = permille,
                EpicPity = Positive(c, "epicPity"),
                LegendaryPity = Positive(c, "legendaryPity"),
                TenCallGuaranteesRare = c.Require("tenCallGuaranteesRare").AsBool(),
            };
            if (call.TenCost < call.Cost || call.TenCost > call.Cost * 10 || call.LegendaryPity < call.EpicPity)
            {
                throw new JsonParseException("call: tenCost must be in [cost, 10×cost] and legendaryPity >= epicPity",
                    c.Line, c.Column);
            }

            return call;
        }

        private static EggConfig LoadEggs(JsonObject e)
        {
            var milestones = new Dictionary<int, int>();
            JsonObject m = e.Require("voyageMilestones").AsObject();
            foreach (string level in m.MemberNames)
            {
                if (!int.TryParse(level, out int index) || index < 1 || index > 20)
                {
                    throw new JsonParseException($"voyage milestone level '{level}' must be 1..20", m.Line, m.Column);
                }

                milestones[index] = Positive(m, level);
            }

            return new EggConfig
            {
                BoardsPerEgg = Positive(e, "boardsPerEgg"),
                Welcome = NonNegative(e, "welcome"),
                VoyageMilestones = milestones,
                DeepZoneFrom = Positive(e, "deepZoneFrom"),
                DeepZoneLevel20 = Positive(e, "deepZoneLevel20"),
                DailyWin = NonNegative(e, "dailyWin"),
                QuestsAllClaimed = NonNegative(e, "questsAllClaimed"),
                EventWin = NonNegative(e, "eventWin"),
                EndlessEveryTides = Positive(e, "endlessEveryTides"),
                EndlessEggs = NonNegative(e, "endlessEggs"),
                EndlessMaxPerRun = NonNegative(e, "endlessMaxPerRun"),
                RareRescue = NonNegative(e, "rareRescue"),
            };
        }

        private static string TierName(int tier) => ((PalRarity)tier).ToString().ToLowerInvariant();

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
