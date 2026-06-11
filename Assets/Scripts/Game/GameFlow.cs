using System;
using System.Collections.Generic;
using Riptide.Core;
using UnityEngine;

namespace Riptide.Game
{
    public enum FlowScreen
    {
        Home,
        ZoneMap,
        Playing,
        Results,
        DailyResults,
        Settings,
        Shop,
        Tidepool,
    }

    public enum GameMode
    {
        Voyage,
        Endless,
        Daily,
    }

    /// <summary>Everything the results screens need, computed once when a run ends.</summary>
    public sealed class RunOutcome
    {
        public GameMode Mode;
        public bool Won;
        public GameStatus Status;
        public long Score;
        public int Moves;
        public int Stars;
        public int CoinsAwarded;
        public int Zone;
        public int LevelIndex;
        public string LevelId = "";
        public int? ParMoves;
        public int TidesSurvived;
        public int FinalWaterLevel;
        public bool NewEndlessBest;
        public int DailyNumber;
        public string ShareCardText = "";
        public int StreakAfter;
    }

    /// <summary>
    /// GDD 9 flow: one orchestrator owning content, mode sessions, and screen
    /// transitions. UI screens render from this and call back into it; they never
    /// touch the sim directly (GDD 8.2 discipline, one layer up).
    /// </summary>
    public sealed class GameFlow
    {
        public EconomyConfig Economy { get; }
        public CreatureRoster Roster { get; }
        public StringTable Strings { get; }
        public MetaServices Meta { get; }

        public FlowScreen Screen { get; private set; } = FlowScreen.Home;
        public GameMode Mode { get; private set; }
        public GameStore? Store { get; private set; }
        public RunOutcome? LastOutcome { get; private set; }
        public LevelDef? CurrentLevel { get; private set; }
        public ulong CurrentSeed { get; private set; }

        public event Action<FlowScreen>? ScreenChanged;
        public event Action? RunStarted;

        private readonly Dictionary<int, IReadOnlyList<LevelDef>> zoneCache = new Dictionary<int, IReadOnlyList<LevelDef>>();
        private readonly List<byte> dailyRescuedSpecies = new List<byte>();
        private readonly System.Random casualSeeds = new System.Random();

        public GameFlow(EconomyConfig economy, CreatureRoster roster, StringTable strings, MetaServices meta)
        {
            Economy = economy;
            Roster = roster;
            Strings = strings;
            Meta = meta;
        }

        public IReadOnlyList<LevelDef> ZoneLevels(int zone)
        {
            if (!zoneCache.TryGetValue(zone, out IReadOnlyList<LevelDef>? levels))
            {
                levels = RuntimeContent.LoadZone(zone, Economy);
                zoneCache[zone] = levels;
            }

            return levels;
        }

        public void GoTo(FlowScreen screen)
        {
            Screen = screen;
            ScreenChanged?.Invoke(screen);
        }

        public void StartVoyageLevel(int zone, int index)
        {
            LevelDef def = ZoneLevels(zone)[index - 1];
            CurrentLevel = def;
            Mode = GameMode.Voyage;
            CurrentSeed = (ulong)casualSeeds.Next(1, int.MaxValue);
            BeginRun(def.ToLevelConfig(Economy, Roster.Count));
        }

        public void StartEndless()
        {
            CurrentLevel = null;
            Mode = GameMode.Endless;
            CurrentSeed = (ulong)casualSeeds.Next(1, int.MaxValue);
            BeginRun(ModeFactory.Endless(Economy, Roster.Count));
        }

        /// <summary>GDD 3.3: one attempt per day; seed from the date; retry consumes the hook.</summary>
        public bool StartDaily(bool isRetry = false)
        {
            if (isRetry)
            {
                if (!Meta.DailyRetryAvailable())
                {
                    return false;
                }

                Meta.ConsumeDailyRetry();
            }
            else
            {
                if (!Meta.CanAttemptDailyToday())
                {
                    return false;
                }

                Meta.RecordDailyAttempt();
            }

            long today = Meta.TodayEpochDay();
            Mode = GameMode.Daily;
            CurrentLevel = null;
            dailyRescuedSpecies.Clear();
            CurrentSeed = SeedForEpochDay(today);
            BeginRun(ModeFactory.Daily(Economy, Roster.Count));
            return true;
        }

        private ulong SeedForEpochDay(long epochDay)
        {
            // Reverse epoch-day to civil date for DailySeed (which hashes yyyy-MM-dd).
            DateTime date = new DateTime(1970, 1, 1).AddDays(epochDay);
            return DailySeed.For(date.Year, date.Month, date.Day);
        }

        private void BeginRun(LevelConfig config)
        {
            if (Store == null)
            {
                Store = new GameStore(config, CurrentSeed);
                Store.MoveApplied += OnMoveApplied;
            }
            else
            {
                Store.Reset(config, CurrentSeed);
            }

            GoTo(FlowScreen.Playing);
            RunStarted?.Invoke();
        }

        private void OnMoveApplied(Move move, MoveResult result)
        {
            if (Mode == GameMode.Daily)
            {
                foreach (CreatureEvent rescue in result.Events.RescuedCreatures)
                {
                    if (!dailyRescuedSpecies.Contains(rescue.CreatureId))
                    {
                        dailyRescuedSpecies.Add(rescue.CreatureId);
                    }
                }
            }

            if (result.Next.Status.IsTerminal())
            {
                FinishRun(result.Next);
            }
        }

        private void FinishRun(GameState final)
        {
            var outcome = new RunOutcome
            {
                Mode = Mode,
                Won = final.Status == GameStatus.Won,
                Status = final.Status,
                Score = final.Score,
                Moves = final.MoveCount,
                TidesSurvived = final.Goals.TidesSurvived,
                FinalWaterLevel = final.WaterLevel,
            };

            switch (Mode)
            {
                case GameMode.Voyage:
                    FinishVoyage(outcome);
                    break;
                case GameMode.Endless:
                    outcome.NewEndlessBest = Meta.RecordEndlessScore(final.Score);
                    if (outcome.NewEndlessBest)
                    {
                        outcome.CoinsAwarded = Economy.Coins.EndlessPersonalBest;
                    }

                    break;
                case GameMode.Daily:
                    FinishDaily(outcome);
                    break;
            }

            LastOutcome = outcome;
        }

        private void FinishVoyage(RunOutcome outcome)
        {
            LevelDef def = CurrentLevel!;
            outcome.Zone = def.Zone;
            outcome.LevelId = def.Id;
            outcome.LevelIndex = ParseIndex(def.Id);
            outcome.ParMoves = def.ParMoves;
            if (outcome.Won)
            {
                outcome.Stars = def.ParMoves.HasValue ? StarRating.For(outcome.Moves, def.ParMoves.Value) : 1;
                outcome.CoinsAwarded = CoinRules.LevelCompleteAward(Economy.Coins, def.Zone, outcome.Stars);
                Meta.RecordLevelResult(def.Id, outcome.Stars);
            }
        }

        private void FinishDaily(RunOutcome outcome)
        {
            long today = Meta.TodayEpochDay();
            outcome.DailyNumber = Economy.Daily.DailyNumber(today);
            int milestone = 0;
            if (outcome.Won)
            {
                milestone = Meta.RecordDailyCompletion(Economy.Coins);
                outcome.CoinsAwarded = Economy.Coins.DailyComplete + milestone;
            }

            outcome.StreakAfter = Meta.Streak.Current;

            var emojis = new List<string>(dailyRescuedSpecies.Count);
            foreach (byte speciesId in dailyRescuedSpecies)
            {
                if (speciesId < Roster.Count)
                {
                    emojis.Add(Roster.Species[speciesId].Emoji);
                }
            }

            outcome.ShareCardText = ShareCard.Compose(
                outcome.DailyNumber,
                outcome.FinalWaterLevel,
                emojis,
                outcome.TidesSurvived,
                Economy.Daily.SurviveTides,
                outcome.Score,
                outcome.StreakAfter);
        }

        /// <summary>The driver calls this once end-of-run animations settle.</summary>
        public void ShowOutcomeScreen()
        {
            if (LastOutcome == null)
            {
                return;
            }

            GoTo(LastOutcome.Mode == GameMode.Daily ? FlowScreen.DailyResults : FlowScreen.Results);
        }

        private static int ParseIndex(string levelId)
        {
            int dash = levelId.LastIndexOf('l');
            return dash >= 0 && int.TryParse(levelId.Substring(dash + 1), out int index) ? index : 1;
        }
    }
}
