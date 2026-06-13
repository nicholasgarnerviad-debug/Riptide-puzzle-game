using NUnit.Framework;
using Riptide.Game;
using UnityEngine;

namespace Riptide.PlayMode.Tests
{
    /// <summary>
    /// Audit B5 (clock hostility): the daily attempt lock must survive deliberate
    /// clock manipulation. GDD §3.3 — ONE attempt per day; a rolled-back clock
    /// must not re-arm it.
    /// </summary>
    public sealed class ClockHostilityTests
    {
        private static void WipeSave()
        {
            string savePath = System.IO.Path.Combine(Application.persistentDataPath, "riptide_save.json");
            if (System.IO.File.Exists(savePath))
            {
                System.IO.File.Delete(savePath);
            }

            foreach (string key in new[] { "riptide.voyage", "riptide.streak", "riptide.endless.best",
                "riptide.daily.attemptDay", "riptide.daily.retryUsed" })
            {
                PlayerPrefs.DeleteKey(key);
            }
        }

        [Test]
        public void Daily_ClockRollback_DoesNotRearmTheAttempt()
        {
            WipeSave();
            var meta = new MetaServices();
            meta.Load();
            long today = meta.TodayEpochDay();
            meta.TodayEpochDay = () => today;

            Assert.That(meta.CanAttemptDailyToday(), Is.True, "fresh profile can attempt");
            meta.RecordDailyAttempt();
            Assert.That(meta.CanAttemptDailyToday(), Is.False, "same day is locked");

            meta.TodayEpochDay = () => today - 1;
            Assert.That(meta.CanAttemptDailyToday(), Is.False,
                "a clock rolled BACKWARD must not re-arm the attempt (B5 exploit)");
            Assert.That(meta.DailyRetryAvailable(), Is.False,
                "the retry hook does not survive a clock rollback either");

            meta.TodayEpochDay = () => today + 1;
            Assert.That(meta.CanAttemptDailyToday(), Is.True, "the next real day re-arms");
        }
    }
}
