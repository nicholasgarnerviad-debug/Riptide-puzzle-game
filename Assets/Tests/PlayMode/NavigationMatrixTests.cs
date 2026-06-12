using NUnit.Framework;
using Riptide.Game;
using Riptide.UI;

namespace Riptide.PlayMode.Tests
{
    /// <summary>
    /// UI spec 5-UI-a ✅: the back-button matrix as a pure decision table.
    /// Priority order: first-run age gate → open sheet → per-screen rule.
    /// </summary>
    public sealed class NavigationMatrixTests
    {
        [Test]
        public void AgeGate_BlocksBack_AboveEverythingElse()
        {
            foreach (FlowScreen screen in System.Enum.GetValues(typeof(FlowScreen)))
            {
                BackDecision d = BackRouter.Decide(screen, sheetOpen: true, consentGateOpen: true);
                Assert.That(d.Action, Is.EqualTo(BackAction.Blocked), $"gate blocks back on {screen}");
            }
        }

        [Test]
        public void OpenSheet_DismissesFirst_EvenDuringPlay()
        {
            Assert.That(BackRouter.Decide(FlowScreen.Playing, true, false).Action,
                Is.EqualTo(BackAction.DismissSheet));
            Assert.That(BackRouter.Decide(FlowScreen.Home, true, false).Action,
                Is.EqualTo(BackAction.DismissSheet));
            Assert.That(BackRouter.Decide(FlowScreen.Settings, true, false).Action,
                Is.EqualTo(BackAction.DismissSheet));
        }

        [Test]
        public void Playing_RaisesThePauseSheet_NeverQuitsSilently()
        {
            Assert.That(BackRouter.Decide(FlowScreen.Playing, false, false).Action,
                Is.EqualTo(BackAction.OpenPauseSheet));
        }

        [Test]
        public void HomeRoot_HandsBackToTheOs()
        {
            Assert.That(BackRouter.Decide(FlowScreen.Home, false, false).Action,
                Is.EqualTo(BackAction.BackgroundApp));
        }

        [Test]
        public void MenuScreens_PopToThePreviousScreen_OrHome()
        {
            foreach (FlowScreen screen in new[]
            {
                FlowScreen.ZoneMap, FlowScreen.Settings, FlowScreen.Shop,
                FlowScreen.Tidepool, FlowScreen.DailyIntro,
            })
            {
                BackDecision d = BackRouter.Decide(screen, false, false);
                Assert.That(d.Action, Is.EqualTo(BackAction.GoTo), screen.ToString());
                Assert.That(d.Target, Is.EqualTo(FlowScreen.Home), $"{screen} defaults to Home");
            }
        }

        [Test]
        public void Results_BackOutToTheirAncestor_NeverIntoTheDeadRun()
        {
            BackDecision voyage = BackRouter.Decide(FlowScreen.Results, false, false, FlowScreen.ZoneMap);
            Assert.That(voyage.Action, Is.EqualTo(BackAction.GoTo));
            Assert.That(voyage.Target, Is.EqualTo(FlowScreen.ZoneMap), "voyage results back out to the map");

            BackDecision endless = BackRouter.Decide(FlowScreen.Results, false, false, FlowScreen.Home);
            Assert.That(endless.Target, Is.EqualTo(FlowScreen.Home), "endless results back out home");

            BackDecision daily = BackRouter.Decide(FlowScreen.DailyResults, false, false, FlowScreen.DailyIntro);
            Assert.That(daily.Target, Is.EqualTo(FlowScreen.DailyIntro),
                "daily results back to the (now locked) intro");
        }
    }
}
