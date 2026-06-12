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
        DailyIntro,
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
        PieceSwap,
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
        public int Rescues;
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

        /// <summary>Endless milestone crossed (ROADMAP M4): tides survived, for the HUD pop.</summary>
        public event Action<int>? MilestoneReached;

        /// <summary>
        /// Continue ruling (ROADMAP M2): a drowned board is holding for the offer —
        /// results are deferred until the player resolves or declines it.
        /// </summary>
        public bool ContinueOfferPending { get; private set; }

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

        public GameFlow(EconomyConfig economy, CreatureRoster roster, StringTable strings, MetaServices meta,
            RunRecorder? recorder = null)
        {
            Economy = economy;
            Roster = roster;
            Strings = strings;
            Meta = meta;
            Recorder = recorder ?? new RunRecorder();
        }

        /// <summary>Mid-run persistence (SAVE_RESUME_DESIGN.md).</summary>
        public RunRecorder Recorder { get; }

        /// <summary>A pending mid-run record found at boot (date-checked for Daily); null otherwise.</summary>
        public RunRecord? PendingRun { get; private set; }

        private IReadOnlyList<Decoration>? decorations;

        public IReadOnlyList<Decoration> Decorations =>
            decorations ??= RuntimeContent.LoadDecorations();

        public int BoosterCost(BoosterKind kind) => kind switch
        {
            BoosterKind.DrainPump => Economy.Boosters.DrainPump,
            BoosterKind.BubblePop => Economy.Boosters.BubblePop,
            BoosterKind.PieceSwap => Economy.Boosters.PieceSwap,
            _ => Economy.Boosters.NewTide,
        };

        public bool CanUseBooster(BoosterKind kind) =>
            Store != null
            && !Store.State.Status.IsTerminal()
            && Store.State.Config.BoostersAllowed
            && Meta.CanAfford(BoosterCost(kind));

        /// <summary>GDD 9 L4: one free Drain Pump, granted by the tutorial, no ad, no coins.</summary>
        public bool TutorialDrainPumpPending { get; private set; }

        public void GrantTutorialDrainPump() => TutorialDrainPumpPending = true;

        /// <summary>
        /// GDD 5.3 buy-and-use: the coin spend happens only after the sim accepts
        /// the move (an invalid Bubble Pop target costs nothing). The tutorial's
        /// granted Drain Pump bypasses the wallet once.
        /// </summary>
        public bool TryUseBooster(BoosterKind kind, GridPos? target = null)
        {
            bool tutorialFreebie = kind == BoosterKind.DrainPump && TutorialDrainPumpPending;
            if (!tutorialFreebie && !CanUseBooster(kind))
            {
                return false;
            }

            if (tutorialFreebie && (Store == null || Store.State.Status.IsTerminal()
                || !Store.State.Config.BoostersAllowed))
            {
                return false;
            }

            if ((kind == BoosterKind.BubblePop || kind == BoosterKind.PieceSwap) && !target.HasValue)
            {
                return false;
            }

            Move move = kind switch
            {
                BoosterKind.DrainPump => new DrainPumpMove(),
                BoosterKind.BubblePop => new BubblePopMove(target ?? default),
                BoosterKind.PieceSwap => new PieceSwapMove(target?.Col ?? 0),
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

            if (tutorialFreebie)
            {
                TutorialDrainPumpPending = false;
                Analytics.LogBoosterUsed(kind.ToString(), "tutorial");
                return true;
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
            // Quitting a live run to ANY menu keeps abandon semantics (design §4):
            // home or the level map, a deliberately-left run is over — no
            // resume-after-quit in the GDD. (Terminal states are untouched: the
            // results path and a pending continue offer clear/keep the record.)
            if (Screen == FlowScreen.Playing && screen != FlowScreen.Playing
                && Store != null && !Store.State.Status.IsTerminal())
            {
                Recorder.Finish();
            }

            Screen = screen;
            ScreenChanged?.Invoke(screen);
        }

        public void StartVoyageLevel(int zone, int index)
        {
            LevelDef def = ZoneLevels(zone)[index - 1];
            CurrentLevel = def;
            Mode = GameMode.Voyage;
            CurrentSeed = (ulong)casualSeeds.Next(1, int.MaxValue);
            Recorder.Begin("Voyage", zone, index, 0, CurrentSeed);
            BeginRun(def.ToLevelConfig(Economy, Roster.Count));
        }

        public void StartEndless()
        {
            CurrentLevel = null;
            Mode = GameMode.Endless;
            CurrentSeed = (ulong)casualSeeds.Next(1, int.MaxValue);
            Recorder.Begin("Endless", 0, 0, 0, CurrentSeed);
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
            Recorder.Begin("Daily", 0, 0, today, CurrentSeed);
            BeginRun(ModeFactory.Daily(Economy, Roster.Count));
            return true;
        }

        private ulong SeedForEpochDay(long epochDay)
        {
            // Reverse epoch-day to civil date for DailySeed (which hashes yyyy-MM-dd).
            DateTime date = new DateTime(1970, 1, 1).AddDays(epochDay);
            return DailySeed.For(date.Year, date.Month, date.Day);
        }

        /// <summary>
        /// Boot check (SAVE_RESUME_DESIGN.md §5): surfaces a pending mid-run
        /// record. A Daily record from another day is discarded outright — the
        /// attempt was consumed that day and the board no longer exists.
        /// </summary>
        public void DetectPendingRun()
        {
            RunRecord? record = Recorder.ReadPending();
            if (record != null && record.Mode == "Daily" && record.EpochDay != Meta.TodayEpochDay())
            {
                Recorder.DeleteFile();
                record = null;
            }

            PendingRun = record;
        }

        /// <summary>
        /// Replays the pending record through the engine and re-enters Playing at
        /// the exact recorded state. Any divergence (content drift, corruption,
        /// illegal move) discards gracefully and returns false (design §6).
        /// Side effects of the original moves (rescue counters, milestones,
        /// wallet) are NOT re-fired — they already happened.
        /// </summary>
        public bool ResumeRun()
        {
            RunRecord? record = PendingRun;
            PendingRun = null;
            if (record == null)
            {
                return false;
            }

            LevelConfig config;
            LevelDef? level = null;
            GameMode mode;
            switch (record.Mode)
            {
                case "Voyage":
                    if (record.Zone < 1 || record.Zone > 10 || record.Level < 1 || record.Level > 20)
                    {
                        Recorder.DeleteFile();
                        return false;
                    }

                    level = ZoneLevels(record.Zone)[record.Level - 1];
                    config = level.ToLevelConfig(Economy, Roster.Count);
                    mode = GameMode.Voyage;
                    break;
                case "Endless":
                    config = ModeFactory.Endless(Economy, Roster.Count);
                    mode = GameMode.Endless;
                    break;
                case "Daily":
                    config = ModeFactory.Daily(Economy, Roster.Count);
                    mode = GameMode.Daily;
                    break;
                default:
                    Recorder.DeleteFile();
                    return false;
            }

            RunReplayResult replay = RunReplay.Rebuild(config, record);
            if (replay.Status != RunReplayStatus.Ok)
            {
                Recorder.DeleteFile();
                return false;
            }

            Mode = mode;
            CurrentLevel = level;
            CurrentSeed = record.Seed;
            dailyWasRetry = false;
            dailyRescuedSpecies.Clear();
            if (mode == GameMode.Daily)
            {
                foreach (int id in replay.RescuedSpeciesInOrder)
                {
                    if (!dailyRescuedSpecies.Contains((byte)id))
                    {
                        dailyRescuedSpecies.Add((byte)id);
                    }
                }
            }

            if (Store == null)
            {
                Store = new GameStore(config, record.Seed);
                Store.MoveApplied += OnMoveApplied;
            }

            Store.Restore(replay.State!);

            runMaxWater = replay.MaxWater;
            runRescues = replay.Rescues;
            freeDrainUsedThisRun = false;
            freeNewTideUsedThisRun = false;
            ContinueOfferPending = false;

            Recorder.Resume(record);
            GoTo(FlowScreen.Playing);
            RunStarted?.Invoke();

            // Killed between the terminal move and the outcome (design §7.2):
            // re-raise the continue offer, or conclude the run exactly once.
            if (Store.State.Status.IsTerminal())
            {
                if (CanOfferContinue(Store.State))
                {
                    ContinueOfferPending = true;
                }
                else
                {
                    FinishRun(Store.State);
                }
            }

            return true;
        }

        public void AbandonPendingRun()
        {
            Recorder.DeleteFile();
            PendingRun = null;
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
            ContinueOfferPending = false;

            if (Mode == GameMode.Voyage && CurrentLevel != null)
            {
                Analytics.LogLevelStart(CurrentLevel.Zone, ParseIndex(CurrentLevel.Id));
            }

            GoTo(FlowScreen.Playing);
            RunStarted?.Invoke();
        }

        private void OnMoveApplied(Move move, MoveResult result)
        {
            // Mid-run save: persist the inputs in the same frame as the move
            // (design §4 — OnApplicationPause is unreliable on OS kill).
            Recorder.Append(move, result.Next);

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

            // ROADMAP M4: endless milestone pops as the tide is survived.
            if (Mode == GameMode.Endless && result.Events.TideRose
                && Economy.Coins.EndlessMilestoneEvery > 0
                && result.Next.Goals.TidesSurvived > 0
                && result.Next.Goals.TidesSurvived % Economy.Coins.EndlessMilestoneEvery == 0)
            {
                MilestoneReached?.Invoke(result.Next.Goals.TidesSurvived);
            }

            if (result.Next.Status.IsTerminal())
            {
                if (CanOfferContinue(result.Next))
                {
                    // ROADMAP M2: hold the results — the player gets one offer.
                    ContinueOfferPending = true;
                    Analytics.Log(AnalyticsSchema.ContinueOffered, ("mode", Mode.ToString()));
                }
                else
                {
                    FinishRun(result.Next);
                }
            }
        }

        /// <summary>Continue ruling: drowned, unspent, boosters-allowed mode, and payable.</summary>
        private bool CanOfferContinue(GameState final) =>
            final.Status == GameStatus.LostDrowned
            && !final.ContinueUsed
            && Mode != GameMode.Daily
            && final.Config.BoostersAllowed
            && (ContinueAdAvailable || Meta.CanAfford(Economy.Coins.ContinueCost));

        public bool ContinueAdAvailable => Ads != null && Ads.RewardedAvailable;

        public bool TryContinueViaAd()
        {
            if (!ContinueOfferPending || Ads == null || !Ads.RewardedAvailable)
            {
                return false;
            }

            return Ads.ShowRewarded(RewardedPlacementId.ContinueRun, onPaid: () => DispatchContinue("ad"));
        }

        public bool TryContinueWithCoins()
        {
            if (!ContinueOfferPending || !Meta.TrySpendCoins(Economy.Coins.ContinueCost))
            {
                return false;
            }

            Meta.SaveNow();
            DispatchContinue("coins");
            return true;
        }

        public void DeclineContinue()
        {
            if (!ContinueOfferPending)
            {
                return;
            }

            ContinueOfferPending = false;
            Analytics.Log(AnalyticsSchema.ContinueDeclined, ("mode", Mode.ToString()));
            FinishRun(Store!.State);
        }

        private void DispatchContinue(string method)
        {
            ContinueOfferPending = false;
            try
            {
                if (Store!.TryDispatch(new ContinueMove()))
                {
                    Analytics.Log(AnalyticsSchema.ContinueUsed, ("method", method), ("mode", Mode.ToString()));
                    return;
                }
            }
            catch (InvalidMoveException)
            {
            }

            // The sim refused (already used / not drowned) — fall through to results.
            FinishRun(Store!.State);
        }

        private void FinishRun(GameState final)
        {
            // The run concluded — the outcome path runs exactly once, and the
            // pending record must never offer to replay a finished run.
            Recorder.Finish();

            var outcome = new RunOutcome
            {
                Mode = Mode,
                Won = final.Status == GameStatus.Won,
                Status = final.Status,
                Score = final.Score,
                Moves = final.MoveCount,
                TidesSurvived = final.Goals.TidesSurvived,
                FinalWaterLevel = final.WaterLevel,
                Rescues = runRescues,
            };

            switch (Mode)
            {
                case GameMode.Voyage:
                    FinishVoyage(outcome);
                    break;
                case GameMode.Endless:
                    outcome.NewEndlessBest = Meta.RecordEndlessScore(final.Score);
                    // ROADMAP M4: banked milestones pay out with the run.
                    outcome.CoinsAwarded =
                        CoinRules.EndlessMilestoneAward(Economy.Coins, final.Goals.TidesSurvived)
                        + (outcome.NewEndlessBest ? Economy.Coins.EndlessPersonalBest : 0);
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
