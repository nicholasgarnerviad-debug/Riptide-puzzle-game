using System;
using Riptide.Core;

namespace Riptide.Game
{
    /// <summary>
    /// GDD §15 Pets &amp; Tides persistence (save v4): quest days and progress,
    /// lifetime badge counters, and the weekly event reward. Day guards are
    /// strictly-greater (audit B5). Tidelings (§16) live in MetaServices.Tidelings.
    /// </summary>
    public sealed partial class MetaServices
    {
        // ---------------------------------------------------------------- quests

        /// <summary>Rolls the quest board forward to today (never backwards on clock rollback).</summary>
        public void EnsureQuestDay(int perDay)
        {
            SaveData d = store.Data;
            long today = TodayEpochDay();
            bool stale = today > d.QuestDay;
            bool malformed = d.QuestProgress.Length != perDay || d.QuestClaimed.Length != perDay;
            if (stale || malformed)
            {
                d.QuestDay = stale ? today : Math.Max(d.QuestDay, 0);
                d.QuestProgress = new int[perDay];
                d.QuestClaimed = new bool[perDay];
            }
        }

        public void AdvanceQuests(ExpansionConfig expansion, QuestSignal signal)
        {
            EnsureQuestDay(expansion.QuestsPerDay);
            SaveData d = store.Data;
            int[] picks = QuestBoard.ForDay(d.QuestDay, expansion.QuestPool.Count, expansion.QuestsPerDay);
            for (int i = 0; i < picks.Length; i++)
            {
                d.QuestProgress[i] = QuestRules.Advance(expansion.QuestPool[picks[i]], d.QuestProgress[i], signal);
            }
        }

        public bool TryClaimQuest(QuestView quest, int bonusPercent = 0)
        {
            SaveData d = store.Data;
            if (!quest.Complete || d.QuestClaimed[quest.Index])
            {
                return false;
            }

            d.QuestClaimed[quest.Index] = true;
            d.QuestsCompleted++;
            wallet.Earn(quest.Def.Reward * (100 + bonusPercent) / 100);
            SaveNow();
            return true;
        }

        // ---------------------------------------------------------------- badges

        public void RecordPlayStats(int rowsCleared, int tidesSurvived, int powers)
        {
            SaveData d = store.Data;
            d.TotalRowsCleared += rowsCleared;
            d.BestClearAtOnce = Math.Max(d.BestClearAtOnce, rowsCleared);
            d.TotalTides += tidesSurvived;
            d.PowersTriggered += powers;
        }

        public PlayerStats BuildStats(int speciesCount, int bestStage)
        {
            SaveData d = store.Data;
            long rescues = 0;
            int met = 0;
            for (int i = 0; i < speciesCount; i++)
            {
                int count = d.RescuesFor(i);
                rescues += count;
                met += count > 0 ? 1 : 0;
            }

            return new PlayerStats(rescues, met, d.TotalRowsCleared, d.BestClearAtOnce, d.TotalTides,
                Voyage.TotalStars, Voyage.CompletedCount, d.EndlessBest, d.Streak.Best, d.DecorationsOwned.Count,
                d.PowersTriggered, d.QuestsCompleted, d.EventsCompleted, bestStage);
        }

        public bool TryClaimBadge(BadgeDef badge, PlayerStats stats, int bonusPercent = 0)
        {
            SaveData d = store.Data;
            if (!BadgeRules.Earned(badge, stats) || d.BadgesClaimed.Contains(badge.Id))
            {
                return false;
            }

            d.BadgesClaimed.Add(badge.Id);
            wallet.Earn(badge.Reward * (100 + bonusPercent) / 100);
            SaveNow();
            return true;
        }

        // ----------------------------------------------------------------- event

        public void RecordEventProgress(int week, int progress)
        {
            SaveData d = store.Data;
            if (d.EventProgressWeek != week)
            {
                d.EventProgressWeek = week;
                d.EventBestProgress = 0;
            }

            d.EventBestProgress = Math.Max(d.EventBestProgress, progress);
        }

        /// <summary>First win this week: unlock the event decoration, count it. False on repeats.</summary>
        public bool TryRecordEventWin(int week, EventDef def)
        {
            SaveData d = store.Data;
            if (d.EventRewardWeek >= week)
            {
                return false;
            }

            d.EventRewardWeek = week;
            d.EventsCompleted++;
            if (!d.DecorationsOwned.Contains(def.RewardDecoration))
            {
                d.DecorationsOwned.Add(def.RewardDecoration);
            }

            return true;
        }
    }
}
