using System.Collections;
using NUnit.Framework;
using Riptide.Core;
using Riptide.Game;
using Riptide.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Riptide.PlayMode.Tests
{
    /// <summary>
    /// GDD §15 Pets &amp; Tides through the real app: the weekly Event Tide (intro →
    /// shared-seed run → results + best progress), the Quests tab (claim pays
    /// once, the tab bubble counts it), the Tidepool pet card (Make Buddy, gift)
    /// and the in-run buddy. Bots drive the store; screens follow the flow.
    /// </summary>
    public sealed class ExpansionFlowTests
    {
        private static readonly long Today = CivilDate.ToEpochDays(2026, 10, 14);

        private static void WipeSave()
        {
            string savePath = System.IO.Path.Combine(Application.persistentDataPath, "riptide_save.json");
            if (System.IO.File.Exists(savePath))
            {
                System.IO.File.Delete(savePath);
            }
        }

        private static IEnumerator Boot(System.Action<GameFlow, ScreenManager> ready)
        {
            WipeSave();
            (GameFlow flow, ScreenManager screens) = GameBootstrap.CreateApp(instantAnimations: true);
            flow.Meta.TodayEpochDay = () => Today;
            yield return null;
            ready(flow, screens);
        }

        private static void Teardown(ScreenManager screens) =>
            Object.Destroy(screens.transform.parent != null ? screens.transform.parent.gameObject : screens.gameObject);

        private static IEnumerator PlayToTerminal(GameFlow flow)
        {
            var policy = new GreedyClearPolicy();
            DeterministicRng rng = DeterministicRng.FromSeed(9);
            int guard = 0;
            while (!flow.Store!.State.Status.IsTerminal() && guard < 600)
            {
                BotDecision decision = policy.Choose(flow.Store.State, rng);
                rng = decision.Rng;
                Assert.That(decision.Move, Is.Not.Null, "bot must always have a move in-progress");
                flow.Store.TryDispatch(decision.Move!);
                if (++guard % 25 == 0)
                {
                    yield return null;
                }
            }

            int settle = 0;
            while (flow.Screen == FlowScreen.Playing && settle++ < 60)
            {
                yield return null;
            }
        }

        private static Button ButtonAt(string objectName, string child)
        {
            GameObject? host = GameObject.Find(objectName);
            Assert.That(host, Is.Not.Null, $"{objectName} is on screen");
            Transform? found = host!.transform.Find(child);
            Assert.That(found, Is.Not.Null, $"{objectName}/{child} exists");
            return found!.GetComponent<Button>();
        }

        [UnityTest]
        public IEnumerator EventTide_PlaysTheWeeksSharedBoard_AndRecordsProgress()
        {
            GameFlow flow = null!;
            ScreenManager screens = null!;
            yield return Boot((f, s) => { flow = f; screens = s; });

            flow.GoTo(FlowScreen.EventIntro);
            yield return null;
            Assert.That(Object.FindFirstObjectByType<EventIntroScreen>(), Is.Not.Null, "event intro built");

            EventDef tide = flow.ThisWeeksEvent;
            flow.StartEvent();
            Assert.That(flow.Mode, Is.EqualTo(GameMode.Event));
            Assert.That(flow.CurrentSeed, Is.EqualTo(EventSchedule.SeedForWeek(flow.EventWeek)),
                "§15.4: everyone plays the week's board");
            Assert.That(flow.Store!.State.Config.FeaturedSpecies, Is.EqualTo(tide.FeaturedSpecies));
            Assert.That(flow.Store.State.Config.Powers, Is.Not.Null, "powers ride along in events");

            yield return PlayToTerminal(flow);
            Assert.That(flow.Screen, Is.EqualTo(FlowScreen.Results), "event runs end on the results screen");
            RunOutcome outcome = flow.LastOutcome!;
            Assert.That(outcome.EventId, Is.EqualTo(tide.Id));
            Assert.That(flow.Meta.Save.EventProgressWeek, Is.EqualTo(flow.EventWeek), "progress is stamped");
            if (outcome.Won)
            {
                Assert.That(outcome.EventFirstWin, Is.True);
                Assert.That(flow.Meta.OwnsDecoration(tide.RewardDecoration), Is.True, "the prize lands in the tidepool");
                Assert.That(flow.EventWonThisWeek, Is.True);
            }

            Teardown(screens);
        }

        [UnityTest]
        public IEnumerator QuestsTab_ClaimsPayOnce_AndTheTabBubbleCountsThem()
        {
            GameFlow flow = null!;
            ScreenManager screens = null!;
            yield return Boot((f, s) => { flow = f; screens = s; });

            // Finish every quest on today's board in one huge signal.
            var signal = new QuestSignal
            {
                Rescues = 99, SpeciesRescues = new[] { 9, 9, 9, 9, 9, 9, 9, 9 }, Rows = 99, DeepClears = 9,
                Tides = 99, RunScore = 99999, LevelWins = 9, DailyWins = 1, Powers = 9, EndlessRuns = 9,
            };
            flow.Meta.AdvanceQuests(flow.Expansion, signal);
            Assert.That(TabBar.Shows(FlowScreen.Quests) && TabBar.IsTabRoot(FlowScreen.Quests), Is.True);

            flow.GoTo(FlowScreen.Quests);
            yield return null;
            int claimable = flow.ClaimableCount();
            Assert.That(claimable, Is.GreaterThanOrEqualTo(flow.Expansion.QuestsPerDay));

            QuestView first = flow.TodaysQuests()[0];
            long before = flow.Meta.Coins;
            ButtonAt("quest0", "claim").onClick.Invoke();
            yield return null;
            Assert.That(flow.Meta.Coins, Is.EqualTo(before + first.Def.Reward), "claim pays the reward");
            Assert.That(flow.TodaysQuests()[0].Claimed, Is.True);
            Assert.That(flow.ClaimableCount(), Is.EqualTo(claimable - 1), "the bubble drops by one");
            Assert.That(GameObject.Find("quest0")!.transform.Find("claim"), Is.Null, "claimed cards lose the button");
            Assert.That(flow.TryClaimQuest(0), Is.False, "never pays twice");

            Teardown(screens);
        }

        [UnityTest]
        public IEnumerator Tidelings_CardSheet_MakesALeader_AndTheGiftPaysOncePerDay()
        {
            GameFlow flow = null!;
            ScreenManager screens = null!;
            yield return Boot((f, s) => { flow = f; screens = s; });

            for (int i = 0; i < 5; i++)
            {
                flow.Meta.Save.RecordRescue(0, flow.Roster.Count);
                PalRules.RecordRescue(flow.Meta.Save.Pals, flow.Pals, 0, PalPerks.None);
            }

            PalRules.RecordRescue(flow.Meta.Save.Pals, flow.Pals, 1, PalPerks.None);
            Assert.That(flow.PetLevel(0), Is.EqualTo(1), "a rescued Common is befriended as a Baby");
            Assert.That(flow.PetBond(0), Is.EqualTo(5), "every rescue adds bond");
            Assert.That(flow.LeaderSpecies, Is.EqualTo(0), "the first friend leads the Pod");
            flow.GoTo(FlowScreen.Tidepool);
            yield return null;

            var tab = Object.FindFirstObjectByType<TidepoolScreen>();
            Assert.That(tab!.PodShown(0), Is.EqualTo(flow.Pals.LineForSpecies(0)), "the leader swims center stage");
            GameObject.Find("card_starfish")!.GetComponent<Button>().onClick.Invoke();
            yield return null;
            var sheet = Object.FindFirstObjectByType<TidelingSheet>();
            Assert.That(sheet != null && sheet.IsOpen && sheet.Line == flow.Pals.LineForSpecies(1), Is.True,
                "a Journal card opens its Tideling's card");
            sheet!.transform.Find("content/info/pod/slot0").GetComponent<Button>().onClick.Invoke();
            Assert.That(flow.LeaderSpecies, Is.EqualTo(1), "the card's Leader slot makes it lead");
            Assert.That(flow.SetBuddy(3), Is.False, "a Tideling you don't have can't lead");
            sheet.Close();
            yield return null;

            long before = flow.Meta.Coins;
            int gift = flow.PetGiftCoins;
            Assert.That(gift, Is.GreaterThan(0));
            GameObject.Find("gift")!.GetComponent<Button>().onClick.Invoke();
            Assert.That(flow.Meta.Coins, Is.EqualTo(before + gift), "the daily pet gift pays");
            Assert.That(flow.PetGiftAvailable, Is.False, "once per day");

            Teardown(screens);
        }

        [UnityTest]
        public IEnumerator Home_BuddyStage_ShowsYourBuddy_TalksOnTap_AndPaysTheGift()
        {
            GameFlow flow = null!;
            ScreenManager screens = null!;
            yield return Boot((f, s) => { flow = f; screens = s; });

            var home = Object.FindFirstObjectByType<HomeScreen>();
            Assert.That(home, Is.Not.Null, "home built at boot");
            Assert.That(home!.Stage.Species, Is.EqualTo(0), "the crab stands in until a buddy is picked");

            flow.Meta.Save.RecordRescue(4, flow.Roster.Count);
            PalRules.RecordRescue(flow.Meta.Save.Pals, flow.Pals, 4, PalPerks.None);
            Assert.That(flow.SetBuddy(4), Is.True);
            flow.GoTo(FlowScreen.Tidepool);
            yield return null;
            flow.GoTo(FlowScreen.Home);
            yield return null;
            Assert.That(home.Stage.Species, Is.EqualTo(4), "the stage shows your buddy");
            Assert.That(home.Stage.Line, Is.EqualTo(flow.Strings.Get("buddy.gift")), "a ready gift is the headline");

            string before = home.Stage.Line;
            home.Stage.transform.Find("buddy").GetComponent<Button>().onClick.Invoke();
            Assert.That(home.Stage.Line, Is.Not.EqualTo(before), "tapping the buddy makes it talk");

            long coins = flow.Meta.Coins;
            int gift = flow.PetGiftCoins;
            home.Stage.transform.Find("petGift").GetComponent<Button>().onClick.Invoke();
            Assert.That(flow.Meta.Coins, Is.EqualTo(coins + gift), "the stage's gift box pays the pet gift");
            Assert.That(flow.PetGiftAvailable, Is.False);

            Teardown(screens);
        }

        [UnityTest]
        public IEnumerator Buddy_RidesAlongInRuns()
        {
            GameFlow flow = null!;
            ScreenManager screens = null!;
            yield return Boot((f, s) => { flow = f; screens = s; });

            flow.Meta.Save.RecordRescue(4, flow.Roster.Count);
            PalRules.RecordRescue(flow.Meta.Save.Pals, flow.Pals, 4, PalPerks.None);
            Assert.That(flow.SetBuddy(4), Is.True);
            flow.StartEndless();
            yield return null;

            var pets = Object.FindFirstObjectByType<HudPets>();
            Assert.That(pets, Is.Not.Null, "the in-run pets layer is built with the HUD");
            Assert.That(pets!.Buddy, Is.EqualTo(4), "the turtle buddy perches on the tray");

            Teardown(screens);
        }
    }
}
