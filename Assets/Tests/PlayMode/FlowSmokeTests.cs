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
    /// Phase 5 flow smoke: home → voyage level win → results (stars/coins) and
    /// home → daily → terminal → share card + retry hook + attempt lock.
    /// Bots drive the store; screens follow the flow.
    /// </summary>
    public sealed class FlowSmokeTests
    {
        private static void WipeMeta()
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
        }

        private static IEnumerator PlayToTerminal(GameFlow flow)
        {
            var policy = new GreedyClearPolicy();
            DeterministicRng rng = DeterministicRng.FromSeed(5);
            int guard = 0;
            while (!flow.Store!.State.Status.IsTerminal() && guard < 400)
            {
                BotDecision decision = policy.Choose(flow.Store.State, rng);
                rng = decision.Rng;
                Assert.That(decision.Move, Is.Not.Null, "bot must always have a move in-progress");
                flow.Store.TryDispatch(decision.Move!);
                guard++;
                if (guard % 25 == 0)
                {
                    yield return null;
                }
            }

            Assert.That(flow.Store.State.Status.IsTerminal(), Is.True, "run must end");
            int settle = 0;
            while (flow.Screen == FlowScreen.Playing && settle++ < 60)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Voyage_WinFlow_AwardsStarsAndCoins_AndUnlocksTheNextLevel()
        {
            WipeMeta();
            (GameFlow flow, ScreenManager screens) = GameBootstrap.CreateApp(instantAnimations: true);
            yield return null;

            Assert.That(flow.Screen, Is.EqualTo(FlowScreen.Home));
            flow.GoTo(FlowScreen.ZoneMap);
            flow.StartVoyageLevel(1, 1);
            Assert.That(flow.Screen, Is.EqualTo(FlowScreen.Playing));

            yield return PlayToTerminal(flow);

            Assert.That(flow.Screen, Is.EqualTo(FlowScreen.Results), "win lands on the results screen");
            RunOutcome outcome = flow.LastOutcome!;
            Assert.That(outcome.Won, Is.True, "z1-l1 is tutorial-easy for GreedyClear");
            Assert.That(outcome.Stars, Is.InRange(1, 3));
            Assert.That(outcome.CoinsAwarded, Is.GreaterThanOrEqualTo(20), "GDD 5.2 floor");
            Assert.That(flow.Meta.Voyage.IsCompleted("z1-l1"), Is.True);
            Assert.That(flow.Meta.Voyage.IsUnlocked(1, 2), Is.True, "completion unlocks the next level");

            Object.Destroy(screens.transform.parent != null ? screens.transform.parent.gameObject : screens.gameObject);
        }

        [UnityTest]
        public IEnumerator Daily_Flow_SharesCard_LocksAttempt_AndOffersOneRetry()
        {
            WipeMeta();
            (GameFlow flow, ScreenManager screens) = GameBootstrap.CreateApp(instantAnimations: true);
            long fakeToday = CivilDate.ToEpochDays(2026, 6, 12);
            flow.Meta.TodayEpochDay = () => fakeToday;
            yield return null;

            Assert.That(flow.Meta.CanAttemptDailyToday(), Is.True);
            Assert.That(flow.StartDaily(), Is.True);
            Assert.That(flow.CurrentSeed, Is.EqualTo(DailySeed.For(2026, 6, 12)), "seed comes from the date");

            yield return PlayToTerminal(flow);

            Assert.That(flow.Screen, Is.EqualTo(FlowScreen.DailyResults));
            RunOutcome outcome = flow.LastOutcome!;
            Assert.That(outcome.DailyNumber, Is.EqualTo(2), "2026-06-12 is daily #2");
            Assert.That(outcome.ShareCardText, Does.StartWith("Riptide #2 🌊"));
            Assert.That(outcome.ShareCardText, Does.Contain("riptide.game/d/2"));

            // 5-UI-a §4.5: the on-screen preview is the share string VERBATIM.
            var resultsScreen = Object.FindFirstObjectByType<DailyResultsScreen>(FindObjectsInactive.Include);
            Assert.That(resultsScreen, Is.Not.Null, "daily results screen built");
            Assert.That(resultsScreen!.PreviewText, Is.EqualTo(outcome.ShareCardText),
                "preview renders exactly what will paste (§4.5)");

            Assert.That(flow.Meta.CanAttemptDailyToday(), Is.False, "GDD 3.3: one attempt per day");
            Assert.That(flow.StartDaily(), Is.False, "second fresh attempt refused");

            Assert.That(flow.Meta.DailyRetryAvailable(), Is.True);
            Assert.That(flow.StartDaily(isRetry: true), Is.True, "the (stubbed-ad) retry hook grants one rerun");
            Assert.That(flow.Screen, Is.EqualTo(FlowScreen.Playing));
            Assert.That(flow.Meta.DailyRetryAvailable(), Is.False, "retry is single-use");

            yield return PlayToTerminal(flow);
            Assert.That(flow.StartDaily(isRetry: true), Is.False, "no second retry");

            WipeMeta();
            Object.Destroy(screens.transform.parent != null ? screens.transform.parent.gameObject : screens.gameObject);
        }

        [UnityTest]
        public IEnumerator Economy_BoostersSpendCoins_DailyRefusesThem_AndTheSavePersists()
        {
            WipeMeta();
            (GameFlow flow, ScreenManager screens) = GameBootstrap.CreateApp(instantAnimations: true);
            long fakeToday = CivilDate.ToEpochDays(2026, 6, 13);
            flow.Meta.TodayEpochDay = () => fakeToday;
            yield return null;

            flow.Meta.EarnCoins(500);
            flow.StartEndless();
            yield return null;

            Assert.That(flow.CanUseBooster(BoosterKind.DrainPump), Is.True, "endless allows boosters");
            Assert.That(flow.TryUseBooster(BoosterKind.DrainPump), Is.True);
            Assert.That(flow.Meta.Coins, Is.EqualTo(350), "GDD 5.3: Drain Pump costs 150");
            Assert.That(flow.Store!.State.MoveCount, Is.EqualTo(0), "boosters are not placements");

            Assert.That(flow.TryUseBooster(BoosterKind.NewTide), Is.True);
            Assert.That(flow.Meta.Coins, Is.EqualTo(230), "New Tide costs 120");

            flow.Meta.SaveNow();
            var reloaded = new Riptide.Game.SaveStore();
            reloaded.Load();
            Assert.That(reloaded.Data.Coins, Is.EqualTo(230), "coin persistence through the save file (contract 6D)");

            flow.StartDaily();
            yield return null;
            Assert.That(flow.CanUseBooster(BoosterKind.DrainPump), Is.False, "GDD 5.3: daily refuses boosters");
            Assert.That(flow.TryUseBooster(BoosterKind.DrainPump), Is.False);
            Assert.That(flow.Meta.Coins, Is.EqualTo(230), "no spend on refusal");

            WipeMeta();
            Object.Destroy(screens.transform.parent != null ? screens.transform.parent.gameObject : screens.gameObject);
        }

        [UnityTest]
        public IEnumerator Endless_Flow_RecordsAPersonalBest()
        {
            WipeMeta();
            (GameFlow flow, ScreenManager screens) = GameBootstrap.CreateApp(instantAnimations: true);
            yield return null;

            flow.StartEndless();
            yield return PlayToTerminal(flow);

            Assert.That(flow.Screen, Is.EqualTo(FlowScreen.Results));
            Assert.That(flow.LastOutcome!.Mode, Is.EqualTo(GameMode.Endless));
            Assert.That(flow.LastOutcome.NewEndlessBest, Is.True, "first run is always a best");
            Assert.That(flow.Meta.EndlessBest, Is.EqualTo(flow.LastOutcome.Score));

            WipeMeta();
            Object.Destroy(screens.transform.parent != null ? screens.transform.parent.gameObject : screens.gameObject);
        }
    }
}
