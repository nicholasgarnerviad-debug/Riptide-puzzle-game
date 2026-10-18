using System.Collections.Generic;
using Riptide.Core;

namespace Riptide.Game
{
    /// <summary>One of today's quests as the UI shows it.</summary>
    public readonly struct QuestView
    {
        public int Index { get; }
        public QuestDef Def { get; }
        public int Progress { get; }
        public bool Claimed { get; }
        public bool Complete => Progress >= Def.Target;

        public QuestView(int index, QuestDef def, int progress, bool claimed)
        {
            Index = index;
            Def = def;
            Progress = progress;
            Claimed = claimed;
        }
    }

    /// <summary>
    /// GDD §15 Pets &amp; Tides flow: per-mode power configs, the weekly Event
    /// Tide mode, quest/badge counters fed from moves and run ends, the buddy's
    /// coin bonus, and the pet/quest/badge/event API the screens call.
    /// </summary>
    public sealed partial class GameFlow
    {
        private ExpansionConfig? expansion;
        private int runTides;
        private int runPowers;

        public ExpansionConfig Expansion => expansion ??= RuntimeContent.LoadExpansion(Roster.Count);

        /// <summary>The event being played (set by StartEvent / resume).</summary>
        public EventDef? CurrentEvent { get; private set; }

        // ------------------------------------------------------------ configs

        /// <summary>GDD §15.1: powers per mode — never the Daily (ModeFactory.Daily is used as-is).</summary>
        public LevelConfig VoyageConfig(LevelDef def) =>
            def.ToLevelConfig(Economy, Roster.Count)
                .WithCreaturePowers(Expansion.PowerModes.Voyage ? Expansion.Powers : null);

        public LevelConfig EndlessRunConfig() =>
            ModeFactory.Endless(Economy, Roster.Count)
                .WithCreaturePowers(Expansion.PowerModes.Endless ? Expansion.Powers : null);

        public LevelConfig EventRunConfig(EventDef def) =>
            EventSchedule.BuildConfig(def, Economy, Roster.Count,
                Expansion.PowerModes.Event ? Expansion.Powers : null);

        // -------------------------------------------------------------- event

        public int EventWeek => EventSchedule.WeekOf(Meta.TodayEpochDay());

        /// <summary>This week's Event Tide.</summary>
        public EventDef ThisWeeksEvent => EventSchedule.ForWeek(EventWeek, Expansion.Events);

        public int EventDaysLeft => EventSchedule.DaysLeft(Meta.TodayEpochDay());

        /// <summary>Already won this week (the reward is one-time per week).</summary>
        public bool EventWonThisWeek => Meta.Save.EventRewardWeek == EventWeek;

        /// <summary>Best goal progress across this week's attempts (0 before the first).</summary>
        public int EventBestProgress => Meta.Save.EventProgressWeek == EventWeek ? Meta.Save.EventBestProgress : 0;

        /// <summary>GDD §15.4: unlimited attempts; the week's seed makes the board shared.</summary>
        public void StartEvent()
        {
            int week = EventWeek;
            CurrentEvent = EventSchedule.ForWeek(week, Expansion.Events);
            CurrentLevel = null;
            Mode = GameMode.Event;
            CurrentSeed = EventSchedule.SeedForWeek(week);
            Recorder.Begin("Event", 0, 0, week, CurrentSeed);
            BeginRun(EventRunConfig(CurrentEvent));
        }

        private void FinishEvent(RunOutcome outcome, GameState final)
        {
            EventDef def = CurrentEvent!;
            outcome.EventId = def.Id;
            int progress = def.GoalKind switch
            {
                EventGoalKind.Rescue => final.Goals.Rescued,
                EventGoalKind.Rows => final.Goals.RowsCleared,
                EventGoalKind.Tides => final.Goals.TidesSurvived,
                _ => (int)System.Math.Min(final.Score, int.MaxValue),
            };
            Meta.RecordEventProgress(EventWeek, progress);
            if (outcome.Won && Meta.TryRecordEventWin(EventWeek, def))
            {
                outcome.EventFirstWin = true;
                outcome.EventRewardDecoration = def.RewardDecoration;
                outcome.CoinsAwarded += def.RewardCoins;
            }
        }

        // ---------------------------------------------------------- progress

        private void RecordExpansionProgress(MoveResult result)
        {
            MoveEvents ev = result.Events;
            int tidesNow = result.Next.Goals.TidesSurvived;
            int tideDelta = System.Math.Max(0, tidesNow - runTides);
            runTides = tidesNow;
            runPowers += ev.Powers.Count;
            Meta.RecordPlayStats(ev.RowsCleared.Count, tideDelta, ev.Powers.Count);

            if (ev.RescuedCreatures.Count == 0 && ev.RowsCleared.Count == 0 && tideDelta == 0 && ev.Powers.Count == 0)
            {
                return;
            }

            var signal = new QuestSignal
            {
                Rescues = ev.RescuedCreatures.Count,
                SpeciesRescues = new int[Roster.Count],
                Rows = ev.RowsCleared.Count,
                DeepClears = ev.RowsCleared.Count >= 2 ? 1 : 0,
                Tides = tideDelta,
                Powers = ev.Powers.Count,
            };
            foreach (CreatureEvent rescue in ev.RescuedCreatures)
            {
                if (rescue.CreatureId < signal.SpeciesRescues.Length)
                {
                    signal.SpeciesRescues[rescue.CreatureId]++;
                }
            }

            Meta.AdvanceQuests(Expansion, signal);
        }

        private void FinishExpansion(RunOutcome outcome)
        {
            outcome.PowersFired = runPowers;
            Meta.AdvanceQuests(Expansion, new QuestSignal
            {
                RunScore = outcome.Score,
                LevelWins = outcome.Mode == GameMode.Voyage && outcome.Won ? 1 : 0,
                DailyWins = outcome.Mode == GameMode.Daily && outcome.Won ? 1 : 0,
                EndlessRuns = outcome.Mode == GameMode.Endless ? 1 : 0,
            });

            int buddy = Meta.Save.BuddySpecies;
            if (buddy >= 0 && outcome.CoinsAwarded > 0)
            {
                outcome.BuddyBonus = PetRules.BuddyBonus(outcome.CoinsAwarded, PetLevel(buddy), Expansion.Pets);
                outcome.CoinsAwarded += outcome.BuddyBonus;
            }
        }

        // --------------------------------------------------------- pets / UI

        public int PetLevel(int species) => PetRules.LevelFor(Meta.Save.RescuesFor(species), Expansion.Pets);

        public int? PetNextThreshold(int species) =>
            PetRules.NextThreshold(Meta.Save.RescuesFor(species), Expansion.Pets);

        public int PetGiftCoins => PetRules.GiftCoins(Meta.Save.SpeciesRescues, Expansion.Pets);

        public bool PetGiftAvailable =>
            PetRules.GiftAvailable(Meta.TodayEpochDay(), Meta.Save.PetGiftDay, Meta.Save.SpeciesRescues, Expansion.Pets);

        public int TryClaimPetGift() => Meta.TryClaimPetGift(Expansion.Pets);

        public bool SetBuddy(int species) => Meta.SetBuddy(species);

        public IReadOnlyList<QuestView> TodaysQuests()
        {
            Meta.EnsureQuestDay(Expansion.QuestsPerDay);
            int[] picks = QuestBoard.ForDay(Meta.Save.QuestDay, Expansion.QuestPool.Count, Expansion.QuestsPerDay);
            var views = new List<QuestView>(picks.Length);
            for (int i = 0; i < picks.Length; i++)
            {
                views.Add(new QuestView(i, Expansion.QuestPool[picks[i]], Meta.Save.QuestProgress[i],
                    Meta.Save.QuestClaimed[i]));
            }

            return views;
        }

        public bool TryClaimQuest(int index)
        {
            IReadOnlyList<QuestView> quests = TodaysQuests();
            return index >= 0 && index < quests.Count && Meta.TryClaimQuest(quests[index]);
        }

        public PlayerStats Stats() => Meta.BuildStats(Expansion.Pets, Roster.Count);

        public bool BadgeClaimed(BadgeDef badge) => Meta.Save.BadgesClaimed.Contains(badge.Id);

        public bool TryClaimBadge(BadgeDef badge) => Meta.TryClaimBadge(badge, Stats());

        /// <summary>Claimable rewards right now — the Quests tab's count badge.</summary>
        public int ClaimableCount()
        {
            int count = 0;
            foreach (QuestView quest in TodaysQuests())
            {
                count += quest.Complete && !quest.Claimed ? 1 : 0;
            }

            PlayerStats stats = Stats();
            foreach (BadgeDef badge in Expansion.Badges)
            {
                count += BadgeRules.Earned(badge, stats) && !BadgeClaimed(badge) ? 1 : 0;
            }

            return count;
        }
    }
}
