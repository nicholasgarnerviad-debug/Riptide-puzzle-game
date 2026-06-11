using System;
using Riptide.Core;
using UnityEngine;

namespace Riptide.Game
{
    /// <summary>
    /// Phase 5 persistence shim over PlayerPrefs (DECISIONS.md: absorbed by the
    /// versioned save file in Phase 6). Also the app's single source of "today" —
    /// injectable so flow tests can travel in time.
    /// </summary>
    public sealed class MetaServices
    {
        private const string VoyageKey = "riptide.voyage";
        private const string StreakKey = "riptide.streak";
        private const string EndlessBestKey = "riptide.endless.best";
        private const string DailyAttemptDayKey = "riptide.daily.attemptDay";
        private const string DailyRetryUsedKey = "riptide.daily.retryUsed";

        /// <summary>Tests override this; production uses the device clock.</summary>
        public Func<long> TodayEpochDay = () =>
        {
            DateTime now = DateTime.Now;
            return CivilDate.ToEpochDays(now.Year, now.Month, now.Day);
        };

        public VoyageProgress Voyage { get; private set; } = new VoyageProgress();
        public StreakState Streak { get; private set; } = StreakState.Empty;

        public long EndlessBest
        {
            get => long.TryParse(PlayerPrefs.GetString(EndlessBestKey, "0"), out long best) ? best : 0;
            private set => PlayerPrefs.SetString(EndlessBestKey, value.ToString());
        }

        public void Load()
        {
            Voyage = VoyageProgress.Deserialize(PlayerPrefs.GetString(VoyageKey, ""));
            Streak = DeserializeStreak(PlayerPrefs.GetString(StreakKey, ""));
        }

        public void RecordLevelResult(string levelId, int stars)
        {
            Voyage.Record(levelId, stars);
            PlayerPrefs.SetString(VoyageKey, Voyage.Serialize());
            PlayerPrefs.Save();
        }

        /// <summary>Returns true (and the new state) when the endless score beats the best.</summary>
        public bool RecordEndlessScore(long score)
        {
            if (score <= EndlessBest)
            {
                return false;
            }

            EndlessBest = score;
            PlayerPrefs.Save();
            return true;
        }

        public bool CanAttemptDailyToday()
        {
            long today = TodayEpochDay();
            return GetDailyAttemptDay() != today;
        }

        public bool DailyRetryAvailable()
        {
            long today = TodayEpochDay();
            return GetDailyAttemptDay() == today && PlayerPrefs.GetInt(DailyRetryUsedKey, 0) == 0;
        }

        public void RecordDailyAttempt()
        {
            PlayerPrefs.SetString(DailyAttemptDayKey, TodayEpochDay().ToString());
            PlayerPrefs.SetInt(DailyRetryUsedKey, 0);
            PlayerPrefs.Save();
        }

        public void ConsumeDailyRetry()
        {
            PlayerPrefs.SetInt(DailyRetryUsedKey, 1);
            PlayerPrefs.Save();
        }

        /// <summary>Completing the daily advances the streak; returns the milestone award (0 = none).</summary>
        public int RecordDailyCompletion(CoinsConfig coins)
        {
            Streak = StreakLogic.CompleteDaily(Streak, TodayEpochDay());
            PlayerPrefs.SetString(StreakKey, SerializeStreak(Streak));
            PlayerPrefs.Save();
            return CoinRules.StreakMilestoneAward(coins, Streak.Current);
        }

        private static long GetDailyAttemptDayStatic(string raw) => long.TryParse(raw, out long day) ? day : -1;

        private long GetDailyAttemptDay() => GetDailyAttemptDayStatic(PlayerPrefs.GetString(DailyAttemptDayKey, ""));

        private static string SerializeStreak(StreakState s) =>
            $"{s.Current}|{s.Best}|{s.FreezesHeld}|{s.LastCompletedEpochDay}|{s.LastFreezePurchaseWeek}";

        private static StreakState DeserializeStreak(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return StreakState.Empty;
            }

            string[] parts = raw.Split('|');
            if (parts.Length != 5
                || !int.TryParse(parts[0], out int current)
                || !int.TryParse(parts[1], out int best)
                || !int.TryParse(parts[2], out int freezes)
                || !long.TryParse(parts[3], out long lastDay)
                || !int.TryParse(parts[4], out int lastWeek))
            {
                return StreakState.Empty;
            }

            return new StreakState(current, best, freezes, lastDay, lastWeek);
        }
    }
}
