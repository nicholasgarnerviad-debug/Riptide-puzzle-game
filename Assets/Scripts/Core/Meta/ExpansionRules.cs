using System;
using System.Collections.Generic;

namespace Riptide.Core
{
    /// <summary>GDD §15.3: the day's quests — same for everyone, from the date alone.</summary>
    public static class QuestBoard
    {
        public static int[] ForDay(long epochDay, int poolCount, int perDay)
        {
            if (perDay > poolCount)
            {
                throw new ArgumentOutOfRangeException(nameof(perDay));
            }

            DeterministicRng rng = DeterministicRng.FromSeed(SeedFor(epochDay));
            var indices = new int[poolCount];
            for (int i = 0; i < poolCount; i++)
            {
                indices[i] = i;
            }

            // Partial Fisher–Yates: the first perDay slots are the day's draw.
            for (int i = 0; i < perDay; i++)
            {
                RngIntDraw draw = rng.NextInt(poolCount - i);
                rng = draw.Rng;
                int j = i + draw.Value;
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }

            var picked = new int[perDay];
            Array.Copy(indices, picked, perDay);
            return picked;
        }

        private static ulong SeedFor(long epochDay) => unchecked((ulong)epochDay * 0x9E3779B97F4A7C15UL ^ 0x51F15EEDUL);
    }

    /// <summary>What just happened, as quest fuel (one move or one finished run).</summary>
    public sealed class QuestSignal
    {
        public int Rescues;
        public int[] SpeciesRescues = new int[0];
        public int Rows;
        public int DeepClears;
        public int Tides;
        public long RunScore;
        public int LevelWins;
        public int DailyWins;
        public int Powers;
        public int EndlessRuns;

        public int RescuesOf(int species) =>
            species >= 0 && species < SpeciesRescues.Length ? SpeciesRescues[species] : 0;
    }

    public static class QuestRules
    {
        /// <summary>New progress after a signal, clamped to the target.</summary>
        public static int Advance(QuestDef quest, int progress, QuestSignal signal)
        {
            long next = quest.Kind switch
            {
                QuestKind.Rescue => (long)progress + signal.Rescues,
                QuestKind.RescueSpecies => (long)progress + signal.RescuesOf(quest.Species),
                QuestKind.Rows => (long)progress + signal.Rows,
                QuestKind.DeepClear => (long)progress + signal.DeepClears,
                QuestKind.Tides => (long)progress + signal.Tides,
                QuestKind.Score => Math.Max(progress, signal.RunScore),
                QuestKind.Levels => (long)progress + signal.LevelWins,
                QuestKind.Daily => (long)progress + signal.DailyWins,
                QuestKind.Powers => (long)progress + signal.Powers,
                QuestKind.Endless => (long)progress + signal.EndlessRuns,
                _ => progress,
            };

            return (int)Math.Min(next, quest.Target);
        }

        public static bool Complete(QuestDef quest, int progress) => progress >= quest.Target;
    }

    /// <summary>Lifetime counters a badge can read (GDD §15.3).</summary>
    public readonly struct PlayerStats
    {
        public long Rescues { get; }
        public int Species { get; }
        public long Rows { get; }
        public int BestClear { get; }
        public long Tides { get; }
        public int Stars { get; }
        public int Levels { get; }
        public long EndlessBest { get; }
        public int StreakBest { get; }
        public int Decor { get; }
        public long Powers { get; }
        public int Quests { get; }
        public int Events { get; }
        /// <summary>The highest Tideling stage reached (GDD §16; 0 = none).</summary>
        public int BestStage { get; }

        /// <summary>GDD §16.5: Calls made, Tidelings owned, Legendaries owned, Sparkle forms owned.</summary>
        public long Calls { get; }

        public int Tidelings { get; }

        public int Legendaries { get; }

        public int Sparkles { get; }

        public PlayerStats(long rescues, int species, long rows, int bestClear, long tides, int stars, int levels,
            long endlessBest, int streakBest, int decor, long powers, int quests, int events, int bestStage,
            long calls = 0, int tidelings = 0, int legendaries = 0, int sparkles = 0)
        {
            Rescues = rescues;
            Species = species;
            Rows = rows;
            BestClear = bestClear;
            Tides = tides;
            Stars = stars;
            Levels = levels;
            EndlessBest = endlessBest;
            StreakBest = streakBest;
            Decor = decor;
            Powers = powers;
            Quests = quests;
            Events = events;
            BestStage = bestStage;
            Calls = calls;
            Tidelings = tidelings;
            Legendaries = legendaries;
            Sparkles = sparkles;
        }

        /// <summary>The same stats with the Tideling counts filled in.</summary>
        public PlayerStats WithTidelings(long calls, int tidelings, int legendaries, int sparkles) =>
            new PlayerStats(Rescues, Species, Rows, BestClear, Tides, Stars, Levels, EndlessBest, StreakBest, Decor,
                Powers, Quests, Events, BestStage, calls, tidelings, legendaries, sparkles);

        public long Get(BadgeStat stat) => stat switch
        {
            BadgeStat.Rescues => Rescues,
            BadgeStat.Species => Species,
            BadgeStat.Rows => Rows,
            BadgeStat.BestClear => BestClear,
            BadgeStat.Tides => Tides,
            BadgeStat.Stars => Stars,
            BadgeStat.Levels => Levels,
            BadgeStat.EndlessBest => EndlessBest,
            BadgeStat.StreakBest => StreakBest,
            BadgeStat.Decor => Decor,
            BadgeStat.Powers => Powers,
            BadgeStat.Quests => Quests,
            BadgeStat.Events => Events,
            BadgeStat.BestStage => BestStage,
            BadgeStat.Calls => Calls,
            BadgeStat.Tidelings => Tidelings,
            BadgeStat.Legendaries => Legendaries,
            BadgeStat.Sparkles => Sparkles,
            _ => 0,
        };
    }

    public static class BadgeRules
    {
        public static bool Earned(BadgeDef badge, PlayerStats stats) => stats.Get(badge.Stat) >= badge.Target;

        /// <summary>0..1 toward the badge (for the wall's progress rings).</summary>
        public static float Progress(BadgeDef badge, PlayerStats stats) =>
            badge.Target <= 0 ? 1f : (float)Math.Min(1.0, (double)stats.Get(badge.Stat) / badge.Target);
    }

    /// <summary>GDD §15.4: one event per week, rotating; the week's board is shared via its seed.</summary>
    public static class EventSchedule
    {
        public static int WeekOf(long epochDay) => CivilDate.WeekIndex(epochDay);

        public static EventDef ForWeek(int week, IReadOnlyList<EventDef> rotation)
        {
            int n = rotation.Count;
            return rotation[((week % n) + n) % n];
        }

        /// <summary>Calendar days left in the event week, including today (1..7).</summary>
        public static int DaysLeft(long epochDay) => 7 - (int)((((epochDay + 3) % 7) + 7) % 7);

        public static ulong SeedForWeek(int week) => unchecked(((ulong)week * 0xD1B54A32D192ED03UL) ^ 0x7E7E7E7EUL) | 1UL;

        /// <summary>Endless ruleset + the event's overrides + its goal (+ powers when on).</summary>
        public static LevelConfig BuildConfig(EventDef e, EconomyConfig economy, int speciesCount,
            CreaturePowersConfig? powers)
        {
            EndlessConfig end = economy.Endless;
            GoalSet goals = e.GoalKind switch
            {
                EventGoalKind.Rescue => new GoalSet(e.GoalTarget, null, null, null),
                EventGoalKind.Rows => new GoalSet(null, e.GoalTarget, null, null),
                EventGoalKind.Tides => new GoalSet(null, null, e.GoalTarget, null),
                _ => new GoalSet(null, null, null, e.GoalTarget),
            };

            int interval = e.StartTideInterval;
            return new LevelConfig(
                e.StartWaterLevel,
                e.StartWaterLevel,
                interval,
                e.CreatureSpawnIntervalTrays,
                speciesCount,
                economy.DealColorCount,
                economy.PieceWeightBands[end.WeightBand],
                economy.BuildScoring(awardTideSurvival: true),
                goals,
                preset: null,
                escalation: new EscalationConfig(
                    end.IntervalShrinkEveryTides,
                    Math.Min(end.IntervalFloor, interval),
                    end.WeightEscalationEveryPlacements,
                    end.BigWeightBonusPerStep,
                    end.MaxEscalationSteps),
                boostersAllowed: true,
                powers: powers,
                featuredSpecies: e.FeaturedSpecies);
        }
    }
}
