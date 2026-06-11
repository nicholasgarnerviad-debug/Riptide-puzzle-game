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

    public enum BoosterKind
    {
        DrainPump,
        BubblePop,
        NewTide,
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

        /// <summary>The double-coins rewarded payout fired for this outcome (once only).</summary>
        public bool DoubledClaimed;
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
        public bool DailyWasRetry => dailyWasRetry;
        public LevelDef? CurrentLevel { get; private set; }
        public ulong CurrentSeed { get; private set; }

        public event Action<FlowScreen>? ScreenChanged;
        public event Action? RunStarted;

        public AnalyticsService Analytics { get; private set; } = new AnalyticsService();
        public AdService? Ads { get; private set; }
        public IapService? Iap { get; private set; }
        public ConsentService? Consent { get; private set; }

        private int runMaxWater;
        private int runRescues;
        private bool dailyWasRetry;
        private bool freeDrainUsedThisRun;
        private bool freeNewTideUsedThisRun;

        public void AttachServices(AnalyticsService analytics, ConsentService consent, AdService ads, IapService iap)
        {
            Analytics = analytics;
            Consent = consent;
            Ads = ads;
            Iap = iap;
        }

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

        private IReadOnlyList<Decoration>? decorations;

        public IReadOnlyList<Decoration> Decorations =>
            decorations ??= RuntimeContent.LoadDecorations();

        public int BoosterCost(BoosterKind kind) => kind switch
        {
            BoosterKind.DrainPump => Economy.Boosters.DrainPump,
            BoosterKind.BubblePop => Economy.Boosters.BubblePop,
            _ => Economy.Boosters.NewTide,
        };

        public bool CanUseBooster(BoosterKind kind) =>
            Store != null
            && !Store.State.Status.IsTerminal()
            && Store.State.Config.BoostersAllowed
            && Meta.CanAfford(BoosterCost(kind));

        /// <summary>
        /// GDD 5.3 buy-and-use: the coin spend happens only after the sim accepts
        /// the move (an invalid Bubble Pop target costs nothing).
        /// </summary>
        public bool TryUseBooster(BoosterKind kind, GridPos? target = null)
        {
            if (!CanUseBooster(kind))
            {
                return false;
            }

            if (kind == BoosterKind.BubblePop && !target.HasValue)
            {
                return false;
            }

            Move move = kind switch
            {
                BoosterKind.DrainPump => new DrainPumpMove(),
                BoosterKind.BubblePop => new BubblePopMove(target ?? default),
                _ => new NewTideMove(),
            };

            try
            {
                if (!Store!.TryDispatch(move))
                {
                    return false;
                }
            }
            catch (InvalidMoveException)
            {
                return false;
            }

            Meta.TrySpendCoins(BoosterCost(kind));
            Meta.SaveNow();
            Analytics.LogBoosterUsed(kind.ToString(), "coins");
            return true;
        }

        /// <summary>GDD 5.3: one free Drain Pump and one free New Tide per game via rewarded ad.</summary>
        public bool FreeBoosterAvailable(BoosterKind kind) =>
            Store != null && !Store.State.Status.IsTerminal() && Store.State.Config.BoostersAllowed
            && Ads != null && Ads.RewardedAvailable
            && (kind == BoosterKind.DrainPump ? !freeDrainUsedThisRun
                : kind == BoosterKind.NewTide && !freeNewTideUsedThisRun);

        public bool TryFreeBoosterViaAd(BoosterKind kind)
        {
            if (!FreeBoosterAvailable(kind))
            {
                return false;
            }

            RewardedPlacementId placement = kind == BoosterKind.DrainPump
                ? RewardedPlacementId.FreeDrainPump
                : RewardedPlacementId.FreeNewTide;
            return Ads!.ShowRewarded(placement, onPaid: () =>
            {
                if (kind == BoosterKind.DrainPump)
                {
                    freeDrainUsedThisRun = true;
                }
                else
                {
                    freeNewTideUsedThisRun = true;
                }

                Move move = kind == BoosterKind.DrainPump ? new DrainPumpMove() : (Move)new NewTideMove();
                try
                {
                    Store!.TryDispatch(move);
                    Analytics.LogBoosterUsed(kind.ToString(), "rewarded");
                }
                catch (InvalidMoveException)
                {
                }
            });
        }

        /// <summary>GDD 6 rewarded: double coins on the results screen, once per outcome.</summary>
        public bool TryDoubleCoinsViaAd()
        {
            RunOutcome? outcome = LastOutcome;
            if (outcome == null || !outcome.Won || outcome.DoubledClaimed || outcome.CoinsAwarded <= 0
                || Ads == null || !Ads.RewardedAvailable)
            {
                return false;
            }

            return Ads.ShowRewarded(RewardedPlacementId.DoubleCoins, onPaid: () =>
            {
                outcome.DoubledClaimed = true;
                Meta.EarnCoins(outcome.CoinsAwarded);
                Meta.SaveNow();
            });
        }

        /// <summary>GDD 5.2/6 rewarded coin chest, capped 3/day by the save.</summary>
        public bool TryClaimChestViaAd()
        {
            if (Ads == null || !Ads.RewardedAvailable)
            {
                return false;
            }

            return Ads.ShowRewarded(RewardedPlacementId.CoinChest, onPaid: () => Meta.TryClaimChest(Economy.Coins));
        }

        /// <summary>GDD 3.3 daily retry via rewarded ad (the Phase 5 stub becomes real).</summary>
        public bool TryDailyRetryViaAd()
        {
            if (!Meta.DailyRetryAvailable() || Ads == null || !Ads.RewardedAvailable)
            {
                return false;
            }

            return Ads.ShowRewarded(RewardedPlacementId.DailyRetry, onPaid: () => StartDaily(isRetry: true));
        }

        /// <summary>Tidepool purchases route here so the GDD 8.5 event fires.</summary>
        public bool TryBuyDecoration(Decoration decoration)
        {
            if (!Meta.TryBuyDecoration(decoration))
            {
                return false;
            }

            Analytics.LogTidepoolPurchase(decoration.Id);
            return true;
        }

        /// <summary>GDD 5.2: the daily retry can be paid with coins instead of the (stub) ad.</summary>
        public bool StartDailyRetryWithCoins()
        {
            if (!Meta.DailyRetryAvailable() || !Meta.TrySpendCoins(Economy.Coins.DailyRetryCost))
            {
                return false;
            }

            Meta.SaveNow();
            return StartDaily(isRetry: true);
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
            dailyWasRetry = isRetry;
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

            runMaxWater = config.StartWaterLevel;
            runRescues = 0;
            freeDrainUsedThisRun = false;
            freeNewTideUsedThisRun = false;

            if (Mode == GameMode.Voyage && CurrentLevel != null)
            {
                Analytics.LogLevelStart(CurrentLevel.Zone, ParseIndex(CurrentLevel.Id));
            }

            GoTo(FlowScreen.Playing);
            RunStarted?.Invoke();
        }

        private void OnMoveApplied(Move move, MoveResult result)
        {
            // GDD 5.1: lifetime rescue counters feed the Tidepool, every mode.
            if (result.Events.RescuedCreatures.Count > 0)
            {
                Meta.RecordRescues(result.Events.RescuedCreatures, Roster.Count);
                runRescues += result.Events.RescuedCreatures.Count;
            }

            if (result.Next.WaterLevel > runMaxWater)
            {
                runMaxWater = result.Next.WaterLevel; // GDD 8.5: key balance telemetry
            }

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

            if (outcome.CoinsAwarded > 0)
            {
                Meta.EarnCoins(outcome.CoinsAwarded);
            }

            Meta.SaveNow();
            LastOutcome = outcome;

            string resultName = final.Status switch
            {
                GameStatus.Won => "won",
                GameStatus.LostDrowned => "drown",
                GameStatus.LostStuck => "stuck",
                _ => "creature",
            };
            switch (Mode)
            {
                case GameMode.Voyage:
                    Analytics.LogLevelEnd(outcome.Zone, outcome.LevelIndex, resultName, outcome.Moves,
                        outcome.Stars, runMaxWater, runRescues);
                    break;
                case GameMode.Endless:
                    Analytics.LogEndlessEnd(outcome.Moves, outcome.TidesSurvived, outcome.Score, resultName);
                    break;
                case GameMode.Daily:
                    Analytics.LogDailyAttempt(resultName, outcome.Score, dailyWasRetry);
                    break;
            }
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

            // GDD 6: interstitials only after Voyage level end or Endless run end — never after a daily.
            if (LastOutcome.Mode != GameMode.Daily)
            {
                Ads?.TryShowInterstitial(afterDaily: false, placement: LastOutcome.Mode.ToString());
            }
        }

        private static int ParseIndex(string levelId)
        {
            int dash = levelId.LastIndexOf('l');
            return dash >= 0 && int.TryParse(levelId.Substring(dash + 1), out int index) ? index : 1;
        }
    }
}
