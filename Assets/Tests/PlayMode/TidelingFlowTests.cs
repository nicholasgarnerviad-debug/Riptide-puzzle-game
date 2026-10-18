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
    /// GDD §16 Tidelings through the real flow: the Welcome eggs, one egg every five
    /// finished boards, the Call, the Pod's abilities on prices / free boosters / the
    /// run config — and none of it in the Daily.
    /// </summary>
    public sealed class TidelingFlowTests
    {
        private static readonly long Today = CivilDate.ToEpochDays(2026, 10, 14);

        private static IEnumerator Boot(System.Action<GameFlow, ScreenManager> ready)
        {
            string savePath = System.IO.Path.Combine(Application.persistentDataPath, "riptide_save.json");
            if (System.IO.File.Exists(savePath))
            {
                System.IO.File.Delete(savePath);
            }

            (GameFlow flow, ScreenManager screens) = GameBootstrap.CreateApp(instantAnimations: true);
            flow.Meta.TodayEpochDay = () => Today;
            yield return null;
            ready(flow, screens);
        }

        private static void Teardown(ScreenManager screens) =>
            Object.Destroy(screens.transform.parent != null ? screens.transform.parent.gameObject : screens.gameObject);

        private static IEnumerator PlayToTerminal(GameFlow flow, ulong seed)
        {
            var policy = new GreedyClearPolicy();
            DeterministicRng rng = DeterministicRng.FromSeed(seed);
            int guard = 0;
            while (!flow.Store!.State.Status.IsTerminal() && guard < 600)
            {
                BotDecision decision = policy.Choose(flow.Store.State, rng);
                rng = decision.Rng;
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

        /// <summary>Befriends a Tideling as if found (Commons by rescue; others set directly).</summary>
        private static void Own(GameFlow flow, int line)
        {
            PalSave pals = flow.Meta.Save.Pals;
            pals.EnsureLines(flow.Pals.Lines.Count);
            pals.Stage[line] = System.Math.Max(1, pals.Stage[line]);
        }

        [UnityTest]
        public IEnumerator WelcomeEggs_ThenAnEggEveryFiveBoards_AndACall()
        {
            GameFlow flow = null!;
            ScreenManager screens = null!;
            yield return Boot((f, s) => { flow = f; screens = s; });

            Assert.That(flow.Eggs, Is.EqualTo(flow.Pals.Eggs.Welcome), "the Welcome gift");
            int before = flow.Eggs;
            for (int board = 1; board <= 5; board++)
            {
                flow.StartVoyageLevel(1, 1);
                yield return PlayToTerminal(flow, (ulong)board);
                Assert.That(flow.LastOutcome, Is.Not.Null);
                if (board < 5)
                {
                    Assert.That(flow.BoardsTowardEgg, Is.EqualTo(board), $"board {board} of 5");
                }
            }

            Assert.That(flow.BoardsTowardEgg, Is.Zero, "the fifth board lays the egg");
            Assert.That(flow.Eggs, Is.GreaterThanOrEqualTo(before + 1));
            Assert.That(flow.LastOutcome!.EggsEarned, Is.GreaterThanOrEqualTo(1));

            int eggs = flow.Eggs;
            CallResult? call = flow.CallOne();
            Assert.That(call, Is.Not.Null);
            Assert.That(flow.Eggs, Is.EqualTo(eggs - flow.Pals.Call.Cost));
            Assert.That(flow.Tideling(call!.Value.Line).Owned, Is.True, "the Call's Tideling is yours");
            Teardown(screens);
        }

        [UnityTest]
        public IEnumerator PodAbilities_ShapeRuns_ButNeverTheDaily()
        {
            GameFlow flow = null!;
            ScreenManager screens = null!;
            yield return Boot((f, s) => { flow = f; screens = s; });

            int octopus = flow.Pals.LineForSpecies(3);
            int manta = 8;
            int narwhal = 7;
            Assert.That(flow.Pals.Lines[manta].Perk, Is.EqualTo(PalPerk.FreePops));
            Assert.That(flow.Pals.Lines[narwhal].Perk, Is.EqualTo(PalPerk.TideInterval));
            foreach (int line in new[] { 0, 1, octopus, manta, narwhal })
            {
                Own(flow, line);
            }

            Assert.That(flow.TrySetPod(0, manta), Is.True);
            Assert.That(flow.TrySetPod(1, octopus), Is.True, "five owned opens slot 2");
            int baseTide = ModeFactory.Endless(flow.Economy, flow.Roster.Count).TideInterval;

            flow.StartEndless();
            yield return null;
            Assert.That(flow.PodFreeBoostersLeft(BoosterKind.BubblePop), Is.EqualTo(1), "the Manta's free pop");
            Assert.That(flow.BoosterCost(BoosterKind.BubblePop),
                Is.EqualTo(flow.Economy.Boosters.BubblePop * 95 / 100), "the Octopus's discount");
            Assert.That(flow.Store!.State.Config.TideInterval, Is.EqualTo(baseTide), "no Narwhal in the Pod yet");

            Assert.That(flow.TrySetPod(1, narwhal), Is.True);
            flow.StartEndless();
            yield return null;
            Assert.That(flow.Store!.State.Config.TideInterval, Is.EqualTo(baseTide + 1), "Steady Current");

            Assert.That(flow.StartDaily(), Is.True);
            yield return null;
            Assert.That(flow.Store!.State.Config.TideInterval,
                Is.EqualTo(ModeFactory.Daily(flow.Economy, flow.Roster.Count).TideInterval), "the Daily is untouched");
            Assert.That(flow.PodFreeBoostersLeft(BoosterKind.BubblePop), Is.Zero, "no abilities in the Daily");
            Teardown(screens);
        }
    }
}
