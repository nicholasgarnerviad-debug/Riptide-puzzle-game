using System.Collections.Generic;
using Riptide.Core;

namespace Riptide.Game
{
    /// <summary>One Tideling as the screens show it (GDD §16).</summary>
    public readonly struct TidelingView
    {
        public PalLine Line { get; }
        public int Stage { get; }
        public int Bond { get; }

        /// <summary>Bond to grow to the next stage (null at Grand or when not owned).</summary>
        public int? NextBond { get; }

        /// <summary>The Pod slot it sits in, or −1.</summary>
        public int PodSlot { get; }

        public bool Sparkle { get; }
        public bool SparkleOn { get; }
        public bool Owned => Stage > 0;
        public bool CanGrow => NextBond.HasValue && Bond >= NextBond.Value;
        public string FormName => Line.Stages[System.Math.Max(0, Stage - 1)];

        public TidelingView(PalLine line, int stage, int bond, int? nextBond, int podSlot, bool sparkle, bool sparkleOn)
        {
            Line = line;
            Stage = stage;
            Bond = bond;
            NextBond = nextBond;
            PodSlot = podSlot;
            Sparkle = sparkle;
            SparkleOn = sparkleOn;
        }
    }

    /// <summary>
    /// GDD §16 Tidelings in the flow: the Pod's abilities on run configs, booster
    /// prices, free boosters, Second Wind, run coins and bond; eggs from boards and
    /// every source; the Call; growing up; the Pod; the gift; and the API the screens
    /// call. Abilities never reach the Daily (<see cref="RunPerks"/> is neutral there).
    /// </summary>
    public sealed partial class GameFlow
    {
        private PalsConfig? pals;
        private int podPopsUsed;
        private int podRerollsUsed;
        private int podDrainsUsed;
        private int runRescueEggs;

        public PalsConfig Pals => pals ??= RuntimeContent.LoadPals(Roster.Count);

        private PalSave PalState => Meta.Save.Pals;

        /// <summary>The Pod's abilities right now (screens show these).</summary>
        public PalPerks PodPerks => PalRules.Perks(PalState, Pals);

        /// <summary>The abilities the current run gets — none in the Daily.</summary>
        private PalPerks RunPerks => Mode == GameMode.Daily ? PalPerks.None : PodPerks;

        private LevelConfig WithPod(LevelConfig config, bool pod)
        {
            if (!pod)
            {
                return config;
            }

            PalPerks perks = PodPerks;
            return config.WithPalPerks(perks.TideIntervalBonus, perks.StartWaterReduction);
        }

        // ------------------------------------------------------ boosters / continue

        private int PodDiscounted(int cost) =>
            Mode == GameMode.Daily ? cost : cost * (100 - PodPerks.BoosterDiscountPercent) / 100;

        /// <summary>Free uses of a booster the Pod still has this run (Manta pops, Otter rerolls, Squid drains).</summary>
        public int PodFreeBoostersLeft(BoosterKind kind)
        {
            if (Mode == GameMode.Daily)
            {
                return 0;
            }

            PalPerks perks = PodPerks;
            int left = kind switch
            {
                BoosterKind.BubblePop => perks.FreePops - podPopsUsed,
                BoosterKind.NewTide => perks.FreeRerolls - podRerollsUsed,
                BoosterKind.DrainPump => perks.FreeDrains - podDrainsUsed,
                _ => 0,
            };
            return System.Math.Max(0, left);
        }

        private bool UsePodFreeBooster(BoosterKind kind)
        {
            if (PodFreeBoostersLeft(kind) <= 0)
            {
                return false;
            }

            switch (kind)
            {
                case BoosterKind.BubblePop:
                    podPopsUsed++;
                    break;
                case BoosterKind.NewTide:
                    podRerollsUsed++;
                    break;
                default:
                    podDrainsUsed++;
                    break;
            }

            return true;
        }

        /// <summary>The Axolotl's Second Wind has a free continue left today.</summary>
        public bool SecondWindAvailable =>
            Mode != GameMode.Daily && PalRules.FreeContinueLeft(PalState, Meta.TodayEpochDay(), PodPerks);

        private void UseSecondWind()
        {
            PalRules.UseFreeContinue(PalState, Meta.TodayEpochDay());
            Meta.SaveNow();
        }

        private void ResetTidelingRun()
        {
            podPopsUsed = 0;
            podRerollsUsed = 0;
            podDrainsUsed = 0;
            runRescueEggs = 0;
        }

        // ------------------------------------------------------ rescues and run end

        private void RecordTidelingRescues(IReadOnlyList<CreatureEvent> rescued)
        {
            PalPerks perks = RunPerks;
            foreach (CreatureEvent rescue in rescued)
            {
                runRescueEggs += PalRules.RecordRescue(PalState, Pals, rescue.CreatureId, perks).Eggs;
            }
        }

        /// <summary>Run end: the Pod's coins, run bond, the board's egg progress and every egg source.</summary>
        private void FinishTidelings(RunOutcome outcome)
        {
            PalSave save = PalState;
            PalPerks perks = RunPerks;
            int bonus = outcome.CoinsAwarded * perks.CoinsPercent / 100
                + (outcome.Mode == GameMode.Voyage && outcome.Won ? outcome.Stars * perks.StarCoins : 0)
                + outcome.Rescues * perks.RescueCoins;
            outcome.PodBonus = bonus;
            outcome.CoinsAwarded += bonus;

            PalRules.RecordRun(save, Pals, perks);
            int laid = PalRules.AddBoard(save, Pals, perks);
            int earned = 0;
            EggConfig eggs = Pals.Eggs;
            switch (outcome.Mode)
            {
                case GameMode.Voyage:
                    int milestone = PalRules.VoyageMilestoneEggs(Pals, outcome.Zone, outcome.LevelIndex);
                    if (outcome.Won && milestone > 0 && PalRules.ClaimKey(save, "voyage:" + outcome.LevelId))
                    {
                        earned += milestone;
                    }

                    break;
                case GameMode.Daily:
                    long today = Meta.TodayEpochDay();
                    if (outcome.Won && today > save.DailyEggDay)
                    {
                        save.DailyEggDay = today;
                        earned += eggs.DailyWin;
                    }

                    break;
                case GameMode.Event:
                    if (outcome.EventFirstWin && EventWeek > save.EventEggWeek)
                    {
                        save.EventEggWeek = EventWeek;
                        earned += eggs.EventWin;
                    }

                    break;
                case GameMode.Endless:
                    earned += System.Math.Min(outcome.TidesSurvived / eggs.EndlessEveryTides, eggs.EndlessMaxPerRun)
                        * eggs.EndlessEggs;
                    break;
            }

            PalRules.GrantEggs(save, earned);
            outcome.EggsEarned = runRescueEggs + laid + earned;
            outcome.BoardsTowardEgg = PalRules.BoardsTowardEgg(save);
        }

        /// <summary>Claiming the day's last quest lays the quest eggs (once per day).</summary>
        private void GrantQuestEggsIfAllClaimed()
        {
            long today = Meta.TodayEpochDay();
            bool[] claimed = Meta.Save.QuestClaimed;
            if (today <= PalState.QuestEggDay || claimed.Length == 0)
            {
                return;
            }

            foreach (bool done in claimed)
            {
                if (!done)
                {
                    return;
                }
            }

            PalState.QuestEggDay = today;
            PalRules.GrantEggs(PalState, Pals.Eggs.QuestsAllClaimed);
            Meta.SaveNow();
        }

        // ------------------------------------------------------------ screens' API

        public int Eggs => PalState.Eggs;

        public int BoardsTowardEgg => PalRules.BoardsTowardEgg(PalState);

        public int CallsUntilLegendary => PalRules.CallsUntilLegendary(PalState, Pals, PodPerks);

        public int PodSlotsOpen => PalRules.PodSlotsOpen(Pals, PalState.OwnedCount());

        public TidelingView Tideling(int line)
        {
            PalSave save = PalState;
            save.EnsureLines(Pals.Lines.Count);
            int stage = save.StageOf(line);
            return new TidelingView(Pals.Lines[line], stage, save.BondOf(line), PalRules.NextThreshold(Pals, line, stage),
                System.Array.IndexOf(save.Pod, line), save.Sparkle[line], save.SparkleOn[line]);
        }

        public IReadOnlyList<TidelingView> AllTidelings()
        {
            var all = new List<TidelingView>(Pals.Lines.Count);
            for (int i = 0; i < Pals.Lines.Count; i++)
            {
                all.Add(Tideling(i));
            }

            return all;
        }

        /// <summary>One Call (the Conch). Null when the nest is short of eggs.</summary>
        public CallResult? CallOne()
        {
            CallResult? result = PalRules.Call(PalState, Pals, PodPerks);
            if (result.HasValue)
            {
                Meta.SaveNow();
                Analytics.Log("tideling_call", ("count", "1"), ("tier", result.Value.Tier.ToString()));
            }

            return result;
        }

        /// <summary>Ten Calls (one free, a Rare-or-better guaranteed). Null when short.</summary>
        public IReadOnlyList<CallResult>? CallTen()
        {
            IReadOnlyList<CallResult>? results = PalRules.CallTen(PalState, Pals, PodPerks);
            if (results != null)
            {
                Meta.SaveNow();
                Analytics.Log("tideling_call", ("count", "10"));
            }

            return results;
        }

        /// <summary>Grows a Tideling up a stage; finishing a line pays the Journal reward once.</summary>
        public bool TryGrow(int line)
        {
            PalSave save = PalState;
            if (!PalRules.TryGrow(save, Pals, line))
            {
                return false;
            }

            if (PalRules.LineRewardDue(save, line))
            {
                save.LineRewarded[line] = true;
                Meta.EarnCoins(Pals.LineCompleteCoins);
            }

            Meta.SaveNow();
            Analytics.Log("tideling_grow", ("line", Pals.Lines[line].Key), ("stage", save.StageOf(line).ToString()));
            return true;
        }

        public bool TrySetPod(int slot, int line)
        {
            if (!PalRules.TrySetPod(PalState, Pals, slot, line))
            {
                return false;
            }

            Meta.SaveNow();
            return true;
        }

        public bool TrySetSparkle(int line, bool on)
        {
            PalSave save = PalState;
            if (line < 0 || line >= save.Sparkle.Length || !save.Sparkle[line])
            {
                return false;
            }

            save.SparkleOn[line] = on;
            Meta.SaveNow();
            return true;
        }

        /// <summary>The highest stage any Tideling has reached (the "All Grown Up" badge).</summary>
        public int BestStage()
        {
            int best = 0;
            foreach (int stage in PalState.Stage)
            {
                best = System.Math.Max(best, stage);
            }

            return best;
        }

        // ------------------------------------------- species-keyed API (current screens)

        /// <summary>The board species of the Pod leader (−1 when none, or a Call-only leader the board art can't draw yet).</summary>
        public int LeaderSpecies =>
            PalState.Leader >= 0 && PalState.Leader < Pals.Lines.Count ? Pals.Lines[PalState.Leader].BoardSpecies : -1;

        /// <summary>A species' Tideling stage (0 = not owned).</summary>
        public int PetLevel(int species) => PalState.StageOf(Pals.LineForSpecies(species));

        public int PetBond(int species) => PalState.BondOf(Pals.LineForSpecies(species));

        /// <summary>Bond to grow this species' Tideling to its next stage (null at Grand / not owned).</summary>
        public int? PetNextThreshold(int species)
        {
            int line = Pals.LineForSpecies(species);
            return line < 0 ? null : PalRules.NextThreshold(Pals, line, PalState.StageOf(line));
        }

        public int PetGiftCoins => PalRules.GiftCoins(PalState, Pals, PodPerks);

        /// <summary>Strictly-greater day guard (audit B5): clock rollback never re-arms the gift.</summary>
        public bool PetGiftAvailable => Meta.TodayEpochDay() > Meta.Save.PetGiftDay && PetGiftCoins > 0;

        public int TryClaimPetGift() => Meta.TryClaimPalGift(PetGiftCoins);

        /// <summary>Makes a species' (owned) Tideling the Pod leader.</summary>
        public bool SetBuddy(int species) => TrySetPod(0, Pals.LineForSpecies(species));
    }
}
