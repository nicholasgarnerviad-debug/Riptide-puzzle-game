#if UNITY_EDITOR
using System.Collections;
using System.IO;
using Riptide.Core;
using Riptide.Game;
using UnityEngine;
using UnityEngine.UI;

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

                // 1) Home + the level map.
                flow.GoTo(FlowScreen.Home);
                yield return Settle();
                yield return Shoot("home");
                flow.GoTo(FlowScreen.ZoneMap);
                yield return Settle();
                yield return Shoot("zonemap");

                // 2) A live board mid-run (a few bot moves in, animations on).
                if (screens.Driver != null)
                {
                    screens.Driver.InstantMode = false;
                }

                flow.StartVoyageLevel(1, 3);
                yield return DriveMoves(flow, 7, 5);
                yield return Settle();
                yield return Shoot("game");
                if (screens.Driver != null)
                {
                    screens.Driver.InstantMode = true;
                }

                // 3) Voyage win — z1-l1 is tutorial-easy for GreedyClear.
                flow.StartVoyageLevel(1, 1);
                yield return DriveToTerminal(flow, 5);
                yield return WaitForScreen(flow, FlowScreen.Results);
                yield return Shoot("results_voyage_win");

                // 3b) Voyage loss — z1-l3 under a seed GreedyClear drowns in.
                flow.StartVoyageLevel(1, 3);
                yield return DriveToTerminal(flow, 3);
                yield return WaitForOfferOrScreen(flow, FlowScreen.Results);
                if (flow.ContinueOfferPending)
                {
                    flow.DeclineContinue();
                    var offer = Object.FindFirstObjectByType<ContinueSheet>();
                    if (offer != null)
                    {
                        offer.DismissForDriver();
                    }

                    yield return WaitForScreen(flow, FlowScreen.Results);
                }

                yield return Shoot("results_voyage_" + (flow.LastOutcome != null && flow.LastOutcome.Won ? "win2" : "lose"));

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

                // 4) Daily intro, then daily results.
                flow.GoTo(FlowScreen.DailyIntro);
                yield return Settle();
                yield return Shoot("daily_intro");
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
                var pause = Object.FindFirstObjectByType<PauseSheet>();
                if (pause != null)
                {
                    pause.Dismiss();
                }

                // 6) GDD §15: the Event Tide intro and a run of it.
                flow.GoTo(FlowScreen.EventIntro);
                yield return Settle();
                yield return Shoot("event_intro");
                flow.StartEvent();
                yield return DriveToTerminal(flow, 31);
                yield return WaitForOfferOrScreen(flow, FlowScreen.Results);
                if (flow.ContinueOfferPending)
                {
                    flow.DeclineContinue();
                    var eventOffer = Object.FindFirstObjectByType<ContinueSheet>();
                    if (eventOffer != null)
                    {
                        eventOffer.DismissForDriver();
                    }

                    yield return WaitForScreen(flow, FlowScreen.Results);
                }

                yield return Shoot("results_event");

                // 7) The tab screens — with a buddy picked so the pet bits show.
                int buddy = -1;
                for (int i = 0; i < flow.Roster.Count && buddy < 0; i++)
                {
                    buddy = flow.Meta.Save.RescuesFor(i) > 0 ? i : -1;
                }

                flow.SetBuddy(buddy);
                foreach (FlowScreen tab in new[] { FlowScreen.Settings, FlowScreen.Shop, FlowScreen.Quests, FlowScreen.Tidepool })
                {
                    flow.GoTo(tab);
                    yield return Settle();
                    yield return Shoot(tab.ToString().ToLowerInvariant());
                }

                yield return ShootTidelings(flow);

                flow.GoTo(FlowScreen.Home);
                yield return Settle();
                yield return Shoot("home_buddy");

                // 8) The buddy perched on the tray in a live run.
                flow.StartVoyageLevel(1, 3);
                yield return DriveMoves(flow, 7, 3);
                yield return Settle();
                yield return Shoot("game_buddy");
                flow.GoTo(FlowScreen.Home);
                File.WriteAllText(Done, "OK\n");
            }

            /// <summary>GDD §16.6: a Tideling's card, the Conch, both reveals and growing up.</summary>
            private IEnumerator ShootTidelings(GameFlow flow)
            {
                var tab = Object.FindFirstObjectByType<TidepoolScreen>();
                if (tab == null)
                {
                    yield break;
                }

                // A Pod of three and a Tideling ready to grow, so every piece shows.
                PalSave pals = flow.Meta.Save.Pals;
                pals.EnsureLines(flow.Pals.Lines.Count);
                foreach (int line in new[] { 0, 1, 2, 3, 4, 6, 8, 11 })
                {
                    pals.Stage[line] = System.Math.Max(pals.Stage[line], line == 0 ? 3 : 2);
                }

                pals.Pod[0] = 0;
                pals.Pod[1] = 6;
                pals.Pod[2] = 11;
                pals.Eggs = System.Math.Max(pals.Eggs, 62);

                // The dev save carries over between captures: grow whichever Tideling still can.
                int grow = 4;
                for (int line = 0; line < flow.Pals.Lines.Count; line++)
                {
                    TidelingView t = flow.Tideling(line);
                    if (t.Owned && t.NextBond.HasValue)
                    {
                        grow = line;
                        pals.Bond[line] = System.Math.Max(pals.Bond[line], t.NextBond.Value);
                        break;
                    }
                }

                tab.Refresh();
                yield return Settle();
                yield return Shoot("tidelings");

                tab.OpenSheet(grow);
                yield return Settle();
                yield return Shoot("tidelings_card");
                var sheet = Object.FindFirstObjectByType<TidelingSheet>();
                sheet?.transform.Find("content/info/action")?.GetComponent<Button>()?.onClick.Invoke();
                yield return Settle();
                yield return Shoot("grow_up");
                while (Overlay.DismissTop())
                {
                }

                yield return Settle();
                tab.OpenConch();
                yield return Settle();
                yield return Shoot("conch");
                var conch = Object.FindFirstObjectByType<ConchScreen>();
                conch?.transform.Find("content/callOne")?.GetComponent<Button>()?.onClick.Invoke();
                yield return Settle();
                yield return Shoot("reveal_one");
                Overlay.DismissTop();
                yield return Settle();
                conch?.transform.Find("content/callTen")?.GetComponent<Button>()?.onClick.Invoke();
                yield return Settle();
                yield return Shoot("reveal_ten");
                while (Overlay.DismissTop())
                {
                }

                yield return Settle();
            }

            private static IEnumerator DriveMoves(GameFlow flow, ulong seed, int moves)
            {
                var policy = new GreedyClearPolicy();
                DeterministicRng rng = DeterministicRng.FromSeed(seed);
                for (int i = 0; i < moves && flow.Store != null && !flow.Store.State.Status.IsTerminal(); i++)
                {
                    BotDecision decision = policy.Choose(flow.Store.State, rng);
                    rng = decision.Rng;
                    if (decision.Move == null)
                    {
                        break;
                    }

                    flow.Store.TryDispatch(decision.Move);
                    for (int f = 0; f < 30; f++)
                    {
                        yield return null;
                    }
                }
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
                // The dev seed/hash readout is not part of the design under review.
                foreach (DebugOverlay overlay in Object.FindObjectsByType<DebugOverlay>(FindObjectsSortMode.None))
                {
                    overlay.gameObject.SetActive(false);
                }

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
