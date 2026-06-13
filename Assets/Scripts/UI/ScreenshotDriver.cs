#if UNITY_EDITOR
using System.Collections;
using System.IO;
using Riptide.Core;
using Riptide.Game;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// Dev-only (editor-stripped) screenshot driver: drop Temp/riptide_capture_pending.txt
    /// then enter play, and this bot-drives the flow to each hard-to-reach screen
    /// (Results win/lose, Continue offer, Daily results, Pause) and writes a PNG of
    /// each Game-view frame to Temp/riptide_screen_*.png. Lets the agent eyeball
    /// state-dependent screens without blind navigation. Never ships (UNITY_EDITOR).
    /// </summary>
    public static class ScreenshotDriver
    {
        private const string Pending = "Temp/riptide_capture_pending.txt";
        private const string Done = "Temp/riptide_capture_done.txt";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EarlyInit()
        {
            // Suppress the first-run age gate so screens aren't hidden behind it.
            if (Application.isEditor && File.Exists(Pending))
            {
                PlayerPrefs.SetInt("consent.birthYear", 2000);
                PlayerPrefs.Save();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void LateInit()
        {
            if (!Application.isEditor || !File.Exists(Pending))
            {
                return;
            }

            var go = new GameObject("ScreenshotDriver");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Runner>();
        }

        private sealed class Runner : MonoBehaviour
        {
            private IEnumerator Start()
            {
                File.Delete(Pending);

                // Wait for the auto-booted app to exist.
                ScreenManager screens = null!;
                for (int i = 0; i < 240 && screens == null; i++)
                {
                    screens = Object.FindFirstObjectByType<ScreenManager>();
                    yield return null;
                }

                if (screens == null)
                {
                    File.WriteAllText(Done, "FAIL no ScreenManager\n");
                    yield break;
                }

                GameFlow flow = screens.Flow;
                if (screens.Driver != null)
                {
                    screens.Driver.InstantMode = true; // resolve boards fast for capture
                }

                // 1) Home.
                flow.GoTo(FlowScreen.Home);
                yield return Settle();
                yield return Shoot("home");

                // 2) Voyage win — z1-l1 is tutorial-easy for GreedyClear.
                flow.StartVoyageLevel(1, 1);
                yield return DriveToTerminal(flow, 5);
                yield return WaitForScreen(flow, FlowScreen.Results);
                yield return Shoot("results_voyage_win");

                // 3) Endless drown → Continue offer (fresh profile, fake ad available).
                flow.StartEndless();
                yield return DriveToTerminal(flow, 11);
                yield return WaitForOfferOrScreen(flow, FlowScreen.Results);
                yield return Shoot(flow.ContinueOfferPending ? "continue_offer" : "results_endless");
                if (flow.ContinueOfferPending)
                {
                    // Decline the same way the "Let go" button does, so the sheet
                    // dismisses cleanly instead of lingering across later screens.
                    flow.DeclineContinue();
                    var sheet = Object.FindFirstObjectByType<ContinueSheet>();
                    if (sheet != null)
                    {
                        sheet.DismissForDriver();
                    }

                    yield return WaitForScreen(flow, FlowScreen.Results);
                    yield return Shoot("results_endless");
                }

                // 4) Daily results.
                if (flow.StartDaily())
                {
                    yield return DriveToTerminal(flow, 23);
                    yield return WaitForScreen(flow, FlowScreen.DailyResults);
                    yield return Shoot("daily_results");
                }

                // 5) Pause sheet over a live level.
                flow.StartVoyageLevel(1, 2);
                yield return Settle();
                screens.ShowPause();
                yield return Settle();
                yield return Shoot("pause");

                File.WriteAllText(Done, "OK\n");
            }

            private static IEnumerator DriveToTerminal(GameFlow flow, ulong seed)
            {
                var policy = new GreedyClearPolicy();
                DeterministicRng rng = DeterministicRng.FromSeed(seed);
                int guard = 0;
                while (flow.Store != null && !flow.Store.State.Status.IsTerminal() && guard++ < 500)
                {
                    BotDecision decision = policy.Choose(flow.Store.State, rng);
                    rng = decision.Rng;
                    if (decision.Move == null)
                    {
                        break;
                    }

                    flow.Store.TryDispatch(decision.Move);
                    if (guard % 20 == 0)
                    {
                        yield return null;
                    }
                }
            }

            private static IEnumerator WaitForScreen(GameFlow flow, FlowScreen target)
            {
                for (int i = 0; i < 240 && flow.Screen != target; i++)
                {
                    yield return null;
                }

                yield return Settle();
            }

            private static IEnumerator WaitForOfferOrScreen(GameFlow flow, FlowScreen target)
            {
                for (int i = 0; i < 240 && !flow.ContinueOfferPending && flow.Screen != target; i++)
                {
                    yield return null;
                }

                yield return Settle();
            }

            private static IEnumerator Settle()
            {
                // Generous: the screen transition is t.screen (~340ms); at the low
                // fps during capture, 30 frames undershot it and caught cross-fades.
                for (int i = 0; i < 90; i++)
                {
                    yield return null;
                }
            }

            private IEnumerator Shoot(string name)
            {
                string path = $"Temp/riptide_screen_{name}.png";
                ScreenCapture.CaptureScreenshot(path);
                // CaptureScreenshot lands at end of frame — give it time to flush.
                for (int i = 0; i < 20; i++)
                {
                    yield return new WaitForEndOfFrame();
                }
            }
        }
    }
}
#endif
