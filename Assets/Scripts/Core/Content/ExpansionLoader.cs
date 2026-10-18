using System;
using System.Collections.Generic;

namespace Riptide.Core
{
    /// <summary>GDD §15.1: which modes run creature powers (the Daily never does — not configurable).</summary>
    public sealed class PowerModes
    {
        public bool Voyage { get; }
        public bool Endless { get; }
        public bool Event { get; }

        public PowerModes(bool voyage, bool endless, bool eventTide)
        {
            Voyage = voyage;
            Endless = endless;
            Event = eventTide;
        }
    }

    /// <summary>GDD §15.3 quest kinds — what a quest counts.</summary>
    public enum QuestKind
    {
        Rescue,        // creatures rescued (any species)
        RescueSpecies, // creatures of one species rescued
        Rows,          // rows cleared
        DeepClear,     // clears of 2+ rows at once
        Tides,         // tides survived
        Score,         // one run scoring at least the target (progress = best run)
        Levels,        // voyage levels won
        Daily,         // daily riptide completed
        Powers,        // creature powers fired
        Endless,       // endless runs played
    }

    public sealed class QuestDef
    {
        public string Id { get; }
        public QuestKind Kind { get; }
        public int Target { get; }
        public int Species { get; }
        public int Reward { get; }

        public QuestDef(string id, QuestKind kind, int target, int species, int reward)
        {
            Id = id;
            Kind = kind;
            Target = target;
            Species = species;
            Reward = reward;
        }
    }

    /// <summary>GDD §15.3 badge stats — lifetime counters a badge reads.</summary>
    public enum BadgeStat
    {
        Rescues,
        Species,
        Rows,
        BestClear,
        Tides,
        Stars,
        Levels,
        EndlessBest,
        StreakBest,
        Decor,
        Powers,
        Quests,
        Events,
        BestStage,

        // GDD §16.5 Tideling badges.
        Calls,
        Tidelings,
        Legendaries,
        Sparkles,
    }

    public sealed class BadgeDef
    {
        public string Id { get; }
        public BadgeStat Stat { get; }
        public long Target { get; }
        public int Reward { get; }
        public string Icon { get; }

        public BadgeDef(string id, BadgeStat stat, long target, int reward, string icon)
        {
            Id = id;
            Stat = stat;
            Target = target;
            Reward = reward;
            Icon = icon;
        }
    }

    /// <summary>GDD §15.4 event goal — mapped onto the sim's GoalSet.</summary>
    public enum EventGoalKind
    {
        Rescue,
        Rows,
        Tides,
        Score,
    }

    public sealed class EventDef
    {
        public string Id { get; }
        public int FeaturedSpecies { get; }
        public int StartWaterLevel { get; }
        public int StartTideInterval { get; }
        public int CreatureSpawnIntervalTrays { get; }
        public EventGoalKind GoalKind { get; }
        public int GoalTarget { get; }
        public int RewardCoins { get; }
        public string RewardDecoration { get; }

        public EventDef(string id, int featuredSpecies, int startWaterLevel, int startTideInterval,
            int creatureSpawnIntervalTrays, EventGoalKind goalKind, int goalTarget, int rewardCoins,
            string rewardDecoration)
        {
            Id = id;
            FeaturedSpecies = featuredSpecies;
            StartWaterLevel = startWaterLevel;
            StartTideInterval = startTideInterval;
            CreatureSpawnIntervalTrays = creatureSpawnIntervalTrays;
            GoalKind = goalKind;
            GoalTarget = goalTarget;
            RewardCoins = rewardCoins;
            RewardDecoration = rewardDecoration;
        }
    }

    /// <summary>The whole GDD §15 expansion — expansion.json, immutable after load.</summary>
    public sealed class ExpansionConfig
    {
        public CreaturePowersConfig Powers { get; }
        public PowerModes PowerModes { get; }
        public int QuestsPerDay { get; }
        public IReadOnlyList<QuestDef> QuestPool { get; }
        public IReadOnlyList<BadgeDef> Badges { get; }
        public IReadOnlyList<EventDef> Events { get; }

        public ExpansionConfig(CreaturePowersConfig powers, PowerModes powerModes, int questsPerDay,
            IReadOnlyList<QuestDef> questPool, IReadOnlyList<BadgeDef> badges, IReadOnlyList<EventDef> events)
        {
            Powers = powers;
            PowerModes = powerModes;
            QuestsPerDay = questsPerDay;
            QuestPool = questPool;
            Badges = badges;
            Events = events;
        }
    }

    /// <summary>Parses + validates expansion.json (errors carry the file label and line/column).</summary>
    public static class ExpansionLoader
    {
        public static ExpansionConfig Load(string json, string sourceLabel, int speciesCount)
        {
            try
            {
                JsonObject root = JsonParser.Parse(json).AsObject();

                JsonObject powersObj = root.Require("creaturePowers").AsObject();
                JsonObject modes = powersObj.Require("modes").AsObject();
                var powerModes = new PowerModes(modes.Require("voyage").AsBool(), modes.Require("endless").AsBool(),
                    modes.Require("event").AsBool());
                var powers = new CreaturePowersConfig(
                    NonNegative(powersObj, "crabCoral"),
                    NonNegative(powersObj, "starfishScore"),
                    NonNegative(powersObj, "seahorseTideRelief"),
                    NonNegative(powersObj, "octopusCoral"),
                    NonNegative(powersObj, "turtleDrain"),
                    NonNegative(powersObj, "axolotlDrain"));

                JsonObject questsObj = root.Require("quests").AsObject();
                int perDay = Positive(questsObj, "perDay");
                var pool = new List<QuestDef>();
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (JsonValue node in questsObj.Require("pool").AsArray().Items)
                {
                    JsonObject q = node.AsObject();
                    string id = UniqueId(q, ids);
                    QuestKind kind = ParseEnum<QuestKind>(q, "kind");
                    int species = q.Optional("species") is JsonValue sp ? sp.AsInt() : -1;
                    if (kind == QuestKind.RescueSpecies && (species < 0 || species >= speciesCount))
                    {
                        throw new JsonParseException($"quest '{id}' needs a species in [0, {speciesCount})",
                            q.Line, q.Column);
                    }

                    pool.Add(new QuestDef(id, kind, Positive(q, "target"), species, Positive(q, "reward")));
                }

                if (pool.Count < perDay)
                {
                    throw new JsonParseException($"quest pool ({pool.Count}) is smaller than perDay ({perDay})",
                        questsObj.Line, questsObj.Column);
                }

                var badges = new List<BadgeDef>();
                ids.Clear();
                foreach (JsonValue node in root.Require("badges").AsArray().Items)
                {
                    JsonObject b = node.AsObject();
                    string id = UniqueId(b, ids);
                    badges.Add(new BadgeDef(id, ParseEnum<BadgeStat>(b, "stat"), b.Require("target").AsLong(),
                        Positive(b, "reward"), b.Require("icon").AsString()));
                }

                var events = new List<EventDef>();
                ids.Clear();
                foreach (JsonValue node in root.Require("events").AsArray().Items)
                {
                    JsonObject e = node.AsObject();
                    string id = UniqueId(e, ids);
                    int featured = e.Require("featuredSpecies").AsInt();
                    if (featured < -1 || featured >= speciesCount)
                    {
                        throw new JsonParseException($"event '{id}' featuredSpecies out of range", e.Line, e.Column);
                    }

                    int startWater = NonNegative(e, "startWaterLevel");
                    if (startWater >= BoardSpec.DrownWaterLevel)
                    {
                        throw new JsonParseException($"event '{id}' starts drowned", e.Line, e.Column);
                    }

                    events.Add(new EventDef(id, featured, startWater, Positive(e, "startTideInterval"),
                        NonNegative(e, "creatureSpawnIntervalTrays"), ParseEnum<EventGoalKind>(e, "goal"),
                        Positive(e, "goalTarget"), NonNegative(e, "rewardCoins"),
                        e.Require("rewardDecoration").AsString()));
                }

                if (events.Count < 1)
                {
                    throw new JsonParseException("events rotation is empty", root.Line, root.Column);
                }

                return new ExpansionConfig(powers, powerModes, perDay, pool, badges, events);
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

        private static string UniqueId(JsonObject obj, HashSet<string> seen)
        {
            JsonValue node = obj.Require("id");
            string id = node.AsString();
            if (id.Length == 0 || !seen.Add(id))
            {
                throw new JsonParseException($"duplicate or empty id '{id}'", node.Line, node.Column);
            }

            return id;
        }

        private static T ParseEnum<T>(JsonObject obj, string name) where T : struct
        {
            JsonValue node = obj.Require(name);
            string text = node.AsString();
            if (!Enum.TryParse(text, ignoreCase: true, out T value) || int.TryParse(text, out _))
            {
                throw new JsonParseException($"'{name}' value '{text}' is not a known {typeof(T).Name}",
                    node.Line, node.Column);
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
