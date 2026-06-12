using System.Collections;
using NUnit.Framework;
using Riptide.Core;
using Riptide.Game;
using Riptide.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace Riptide.PlayMode.Tests
{
    /// <summary>
    /// Mid-run save &amp; resume, app half (SAVE_RESUME_DESIGN.md): a process death
    /// mid-run leaves a resumable record; resume reproduces the exact state hash;
    /// finishing or quitting clears it; a Daily resume keeps the attempt lock;
    /// corruption discards gracefully.
    /// </summary>
    public sealed class ResumeTests
    {
        private static string RunPath =>
            System.IO.Path.Combine(Application.persistentDataPath, "riptide_run.json");

        private static void Wipe()
        {
            foreach (string key in new[]
            {
                "riptide.voyage", "riptide.streak", "riptide.endless.best",
                "riptide.daily.attemptDay", "riptide.daily.retryUsed",
            })
            {
                PlayerPrefs.DeleteKey(key);
            }

            string savePath = System.IO.Path.Combine(Application.persistentDataPath, "riptide_save.json");
            if (System.IO.File.Exists(savePath))
            {
                System.IO.File.Delete(savePath);
            }

            if (System.IO.File.Exists(RunPath))
            {
                System.IO.File.Delete(RunPath);
            }
        }

        private static void KillApp(ScreenManager screens)
        {
            // Process death stand-in: the app object tree vanishes without any
            // FinishRun/abandon path running.
            Object.DestroyImmediate(screens.transform.parent != null
                ? screens.transform.parent.gameObject
                : screens.gameObject);
        }

        private static IEnumerator PlayMoves(GameFlow flow, int count)
        {
            var policy = new GreedyClearPolicy();
            DeterministicRng rng = DeterministicRng.FromSeed(11);
            for (int i = 0; i < count && !flow.Store!.State.Status.IsTerminal(); i++)
            {
                BotDecision decision = policy.Choose(flow.Store.State, rng);
                rng = decision.Rng;
                Assert.That(decision.Move, Is.Not.Null);
                flow.Store.TryDispatch(decision.Move!);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator KilledVoyageRun_Resumes_ToTheExactState()
        {
            Wipe();
            (GameFlow flow, ScreenManager screens) = GameBootstrap.CreateApp(instantAnimations: true);
            yield return null;

            flow.StartVoyageLevel(1, 2);
            yield return PlayMoves(flow, 6);
            Assert.That(flow.Store!.State.Status.IsTerminal(), Is.False, "fixture: still mid-run");
            ulong hashAtKill = StateHash.Compute(flow.Store.State);
            long scoreAtKill = flow.Store.State.Score;

            KillApp(screens);
            yield return null;

            (GameFlow revived, ScreenManager screens2) = GameBootstrap.CreateApp(instantAnimations: true);
            yield return null;

            Assert.That(revived.PendingRun, Is.Not.Null, "the record survived the kill");
            Assert.That(revived.PendingRun!.Mode, Is.EqualTo("Voyage"));
            Assert.That(revived.ResumeRun(), Is.True);
            Assert.That(revived.Screen, Is.EqualTo(FlowScreen.Playing));
            Assert.That(StateHash.Compute(revived.Store!.State), Is.EqualTo(hashAtKill),
                "resume reproduces the exact state");
            Assert.That(revived.Store.State.Score, Is.EqualTo(scoreAtKill));
            Assert.That(revived.Mode, Is.EqualTo(GameMode.Voyage));

            KillApp(screens2);
        }

        [UnityTest]
        public IEnumerator ResumedRun_KeepsRecording_FurtherMoves()
        {
            Wipe();
            (GameFlow flow, ScreenManager screens) = GameBootstrap.CreateApp(instantAnimations: true);
            yield return null;
            flow.StartEndless();
            yield return PlayMoves(flow, 4);
            KillApp(screens);
            yield return null;

            (GameFlow revived, ScreenManager screens2) = GameBootstrap.CreateApp(instantAnimations: true);
            yield return null;
            Assert.That(revived.ResumeRun(), Is.True);
            yield return PlayMoves(revived, 3);
            ulong hashAfterMore = StateHash.Compute(revived.Store!.State);
            KillApp(screens2);
            yield return null;

            (GameFlow third, ScreenManager screens3) = GameBootstrap.CreateApp(instantAnimations: true);
            yield return null;
            Assert.That(third.PendingRun, Is.Not.Null, "second kill is also resumable");
            Assert.That(third.ResumeRun(), Is.True);
            Assert.That(StateHash.Compute(third.Store!.State), Is.EqualTo(hashAfterMore),
                "the resumed run kept appending to the record");

            KillApp(screens3);
        }

        [UnityTest]
        public IEnumerator FinishedRun_LeavesNoPendingRecord()
        {
            Wipe();
            (GameFlow flow, ScreenManager screens) = GameBootstrap.CreateApp(instantAnimations: true);
            yield return null;
            flow.StartVoyageLevel(1, 1);

            var policy = new GreedyClearPolicy();
            DeterministicRng rng = DeterministicRng.FromSeed(5);
            int guard = 0;
            while (!flow.Store!.State.Status.IsTerminal() && guard++ < 400)
            {
                BotDecision decision = policy.Choose(flow.Store.State, rng);
                rng = decision.Rng;
                flow.Store.TryDispatch(decision.Move!);
            }

            int settle = 0;
            while (flow.Screen == FlowScreen.Playing && settle++ < 60)
            {
                yield return null;
            }

            Assert.That(System.IO.File.Exists(RunPath), Is.False,
                "a concluded run never offers a resume");

            KillApp(screens);
        }

        [UnityTest]
        public IEnumerator QuitToHome_AbandonsTheRecord()
        {
            Wipe();
            (GameFlow flow, ScreenManager screens) = GameBootstrap.CreateApp(instantAnimations: true);
            yield return null;
            flow.StartEndless();
            yield return PlayMoves(flow, 3);
            Assert.That(System.IO.File.Exists(RunPath), Is.True, "mid-run record exists");

            flow.GoTo(FlowScreen.Home);
            Assert.That(System.IO.File.Exists(RunPath), Is.False,
                "quit-to-home keeps abandon semantics (design §4)");

            KillApp(screens);
        }

        [UnityTest]
        public IEnumerator KilledDailyRun_Resumes_WithAttemptStillLocked()
        {
            Wipe();
            (GameFlow flow, ScreenManager screens) = GameBootstrap.CreateApp(instantAnimations: true);
            // Real clock: the revived app's boot-time DetectPendingRun compares
            // against the real today, so the record must carry it.
            yield return null;

            Assert.That(flow.StartDaily(), Is.True);
            ulong dailySeed = flow.CurrentSeed;
            yield return PlayMoves(flow, 4);
            KillApp(screens);
            yield return null;

            (GameFlow revived, ScreenManager screens2) = GameBootstrap.CreateApp(instantAnimations: true);
            yield return null;

            Assert.That(revived.PendingRun, Is.Not.Null);
            Assert.That(revived.ResumeRun(), Is.True);
            Assert.That(revived.CurrentSeed, Is.EqualTo(dailySeed), "same date-derived board");
            Assert.That(revived.Meta.CanAttemptDailyToday(), Is.False,
                "resume never grants a second attempt");

            KillApp(screens2);
        }

        [UnityTest]
        public IEnumerator StaleDailyRecord_IsDiscarded()
        {
            Wipe();
            (GameFlow flow, ScreenManager screens) = GameBootstrap.CreateApp(instantAnimations: true);
            long yesterday = flow.Meta.TodayEpochDay() - 1;
            flow.Meta.TodayEpochDay = () => yesterday;
            yield return null;
            Assert.That(flow.StartDaily(), Is.True);
            yield return PlayMoves(flow, 3);
            KillApp(screens);
            yield return null;

            // The revived app boots on the REAL clock — the record is a day old.
            (GameFlow revived, ScreenManager screens2) = GameBootstrap.CreateApp(instantAnimations: true);
            yield return null;

            Assert.That(revived.PendingRun, Is.Null, "yesterday's daily board no longer exists");
            Assert.That(System.IO.File.Exists(RunPath), Is.False, "stale record deleted");

            KillApp(screens2);
        }

        [UnityTest]
        public IEnumerator CorruptRecord_DiscardsGracefully()
        {
            Wipe();
            System.IO.File.WriteAllText(RunPath, "{ this is not a run record");

            (GameFlow flow, ScreenManager screens) = GameBootstrap.CreateApp(instantAnimations: true);
            yield return null;

            Assert.That(flow.PendingRun, Is.Null, "corruption reports no pending run");
            Assert.That(System.IO.File.Exists(RunPath), Is.False, "corrupt file deleted");
            Assert.That(flow.Screen, Is.EqualTo(FlowScreen.Home), "boot lands on Home, never crashes");

            KillApp(screens);
        }
    }
}
