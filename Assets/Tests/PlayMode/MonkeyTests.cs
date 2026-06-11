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
    /// Contract 8C: the bot drives the real app stack — flow, screens, rendering
    /// views, store dispatch, random boosters, screen transitions — for 50 runs.
    /// Any exception or error log fails the test (Unity's runner enforces it).
    /// </summary>
    public sealed class MonkeyTests
    {
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator Monkey_FiftyRuns_AcrossAllModes_ZeroExceptions()
        {
            string savePath = System.IO.Path.Combine(Application.temporaryCachePath, "monkey_save.json");
            if (System.IO.File.Exists(savePath))
            {
                System.IO.File.Delete(savePath);
            }

            (GameFlow flow, ScreenManager screens) = GameBootstrap.CreateApp(instantAnimations: true);
            long monkeyDay = CivilDate.ToEpochDays(2026, 7, 1);
            flow.Meta.TodayEpochDay = () => monkeyDay;
            flow.Meta.EarnCoins(5000);
            yield return null;

            var random = new RandomLegalPolicy();
            DeterministicRng rng = DeterministicRng.FromSeed(0xAB5EEDUL);
            int completedRuns = 0;

            for (int run = 0; run < 50; run++)
            {
                // Mode mix: mostly endless chaos, voyage early levels, a daily per "day".
                int modeRoll = run % 5;
                if (modeRoll == 4)
                {
                    monkeyDay++;
                    if (!flow.StartDaily())
                    {
                        flow.StartEndless();
                    }
                }
                else if (modeRoll == 2)
                {
                    flow.StartVoyageLevel(1, 1 + run % 5);
                }
                else
                {
                    flow.StartEndless();
                }

                yield return null;

                int guard = 0;
                while (flow.Store!.State.Status == GameStatus.InProgress && guard < 300)
                {
                    guard++;

                    // Occasionally hammer boosters, including invalid pop targets.
                    if (guard % 9 == 0)
                    {
                        RngIntDraw boosterRoll = rng.NextInt(4);
                        rng = boosterRoll.Rng;
                        switch (boosterRoll.Value)
                        {
                            case 0:
                                flow.TryUseBooster(BoosterKind.DrainPump);
                                break;
                            case 1:
                                RngIntDraw col = rng.NextInt(BoardSpec.Width);
                                rng = col.Rng;
                                RngIntDraw row = rng.NextInt(BoardSpec.Height);
                                rng = row.Rng;
                                flow.TryUseBooster(BoosterKind.BubblePop, new GridPos(col.Value, row.Value));
                                break;
                            case 2:
                                flow.TryUseBooster(BoosterKind.NewTide);
                                break;
                            default:
                                flow.TryDoubleCoinsViaAd(); // invalid mid-run; must no-op safely
                                break;
                        }
                    }

                    if (flow.Store.State.Status != GameStatus.InProgress)
                    {
                        break;
                    }

                    BotDecision decision = random.Choose(flow.Store.State, rng);
                    rng = decision.Rng;
                    if (decision.Move == null)
                    {
                        Assert.Fail($"run {run}: InProgress with no legal move");
                    }

                    flow.Store.TryDispatch(decision.Move!);
                    if (guard % 20 == 0)
                    {
                        yield return null;
                    }
                }

                int settle = 0;
                while (flow.Screen == FlowScreen.Playing && settle++ < 60)
                {
                    yield return null;
                }

                // Poke the outcome screens like a bored thumb.
                flow.TryDoubleCoinsViaAd();
                flow.TryClaimChestViaAd();
                if (run % 7 == 0)
                {
                    flow.GoTo(FlowScreen.Tidepool);
                    yield return null;
                    flow.GoTo(FlowScreen.ZoneMap);
                    yield return null;
                }

                flow.GoTo(FlowScreen.Home);
                yield return null;
                completedRuns++;
            }

            Assert.That(completedRuns, Is.EqualTo(50), "contract 8C: 50 clean runs");
            Object.Destroy(screens.transform.parent != null ? screens.transform.parent.gameObject : screens.gameObject);
        }
    }
}
