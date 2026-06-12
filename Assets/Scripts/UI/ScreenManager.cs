using System;
using System.Collections.Generic;
using Riptide.Core;
using Riptide.Game;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// 5-UI-a: GameFlow remains the navigation source of truth; this manager
    /// projects FlowScreen changes onto a ScreenStack (push new / pop back to a
    /// screen already in the stack / refresh the top), owns the Pause sheet, the
    /// first-run age gate, toasts, and routes Android back through BackRouter.
    /// The board rig swaps in for Playing; screens hide behind it.
    /// </summary>
    public sealed class ScreenManager : MonoBehaviour
    {
        private GameFlow flow = null!;
        private Canvas canvas = null!;
        private RectTransform screensRoot = null!;
        private ScreenStack stack = null!;
        private readonly Dictionary<FlowScreen, RectTransform> screens
            = new Dictionary<FlowScreen, RectTransform>();

        private GameObject? boardRig;
        private AnimationDriver? driver;
        private HudOverlay? hud;
        private PauseSheet? pause;
        private ConsentAgeGate? ageGate;
        private ToastManager toasts = null!;
        private AudioDirector? music;
        private bool instantAnimations;

        public GameFlow Flow => flow;
        public AnimationDriver? Driver => driver;
        public ScreenStack Stack => stack;
        public ToastManager Toasts => toasts;
        public bool PauseShown => pause != null && pause.Shown;
        public bool AgeGateOpen => ageGate != null && ageGate.IsOpen;

        public static ScreenManager Create(Transform parent, GameFlow flow, bool instantAnimations)
        {
            var go = new GameObject("ScreenManager");
            go.transform.SetParent(parent, false);
            var manager = go.AddComponent<ScreenManager>();
            manager.flow = flow;
            manager.instantAnimations = instantAnimations;
            manager.canvas = UiKit.CreateCanvas(go.transform, "ScreenCanvas");
            var canvasRoot = manager.canvas.GetComponent<RectTransform>();

            manager.screensRoot = UiKit.Container(canvasRoot, "Screens");
            UiKit.Stretch(manager.screensRoot);

            // Universal-fit pass: one shared full-bleed backdrop behind the stack;
            // screen roots themselves are transparent and safe-area padded.
            ScreenBackdrop.Create(manager.screensRoot);
            manager.stack = ScreenStack.Create(manager.screensRoot);
            manager.toasts = ToastManager.Create(canvasRoot);

            // 8-UI: music lives at the app root so menus keep their ambience.
            manager.music = AudioDirector.Create(go.transform, flow, null);

            // §4.7 first-run age gate sits above everything until answered.
            if (ConsentAgeGate.Required)
            {
                manager.ageGate = ConsentAgeGate.Build(canvasRoot, flow);
            }

            flow.ScreenChanged += manager.OnScreenChanged;
            flow.RunStarted += manager.OnRunStarted;
            manager.OnScreenChanged(flow.Screen);

            // Mid-run resume prompt — but headless test boots never stall on it.
            if (flow.PendingRun != null && flow.Screen == FlowScreen.Home && !instantAnimations)
            {
                ResumeSheet.Build(canvasRoot, flow, manager.toasts).Show();
            }

            return manager;
        }

        private void OnDestroy()
        {
            if (flow != null)
            {
                flow.ScreenChanged -= OnScreenChanged;
                flow.RunStarted -= OnRunStarted;
            }
        }

        private ContinueSheet? continueSheet;

        private void Update()
        {
            bool settled = flow.Screen == FlowScreen.Playing
                && flow.Store != null
                && flow.Store.State.Status.IsTerminal()
                && (driver == null || !driver.IsAnimating);

            // ROADMAP M2: the continue offer intercepts results. Headless test
            // boots (instantAnimations) auto-decline so bot flows never stall.
            if (settled && flow.ContinueOfferPending)
            {
                if (instantAnimations)
                {
                    flow.DeclineContinue();
                }
                else if (continueSheet == null || !continueSheet.Shown)
                {
                    continueSheet ??= ContinueSheet.Build(canvas.GetComponent<RectTransform>(), flow);
                    continueSheet.Show();
                }
            }
            else if (settled && !flow.ContinueOfferPending && flow.LastOutcome != null)
            {
                // Run ended and the animation settled — hand over to results.
                // (LastOutcome may be stale from a previous run while an offer is
                // pending — the pending check above is what makes this safe.)
                flow.ShowOutcomeScreen();
            }

            HandleBackButton();
        }

        private void HandleBackButton()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            BackDecision decision = BackRouter.Decide(flow.Screen, PauseShown, AgeGateOpen, PreviousScreen());
            switch (decision.Action)
            {
                case BackAction.DismissSheet:
                    pause?.Dismiss();
                    break;
                case BackAction.OpenPauseSheet:
                    ShowPause();
                    break;
                case BackAction.GoTo:
                    flow.GoTo(decision.Target);
                    break;
                case BackAction.Blocked:
                case BackAction.BackgroundApp:
                    break; // OS-level or consumed silently.
            }
        }

        /// <summary>The screen beneath the top of the stack, when there is one.</summary>
        public FlowScreen? PreviousScreen()
        {
            IReadOnlyList<string> ids = stack.Ids;
            if (ids.Count < 2)
            {
                return null;
            }

            return Enum.TryParse(ids[ids.Count - 2], out FlowScreen parsed) ? parsed : null;
        }

        private NotificationService? notifications;

        /// <summary>§6.2: resuming mid-run never drops you straight back into the water.
        /// Backgrounding also recomputes the local-notification plan (ROADMAP M8).</summary>
        private void OnApplicationPause(bool paused)
        {
            if (flow == null)
            {
                return;
            }

            if (paused)
            {
#if RIPTIDE_NOTIFICATIONS
                notifications ??= new NotificationService(new MobileNotificationScheduler());
#else
                notifications ??= new NotificationService(new FakeNotificationScheduler());
#endif
                notifications.Refresh(flow.Meta);
            }
            else if (flow.Screen == FlowScreen.Playing)
            {
                ShowPause();
            }
        }

        public void ShowPause()
        {
            if (flow.Screen != FlowScreen.Playing)
            {
                return;
            }

            if (pause == null)
            {
                pause = PauseSheet.Build(canvas.GetComponent<RectTransform>(), flow);
            }

            pause.Show();
        }

        private void OnRunStarted()
        {
            if (boardRig == null)
            {
                BuildBoardRig();
            }
        }

        private void BuildBoardRig()
        {
            GameStore store = flow.Store!;
            boardRig = new GameObject("BoardRig");
            boardRig.transform.SetParent(transform, false);

            GameSceneDressing.Create(boardRig.transform);
            BoardView board = BoardView.Create(boardRig.transform);
            WaterView water = WaterView.Create(boardRig.transform, store.State.WaterLevel);
            TrayView tray = TrayView.Create(boardRig.transform);
            TideMeterRing meter = TideMeterRing.Create(boardRig.transform);
            BoardChromeView chrome = BoardChromeView.Create(boardRig.transform);
            MarineSnow.Create(boardRig.transform);
            driver = AnimationDriver.Create(boardRig.transform, store, board, water, tray, meter, chrome);
            driver.InstantMode = instantAnimations;
            JuiceDirector.Create(boardRig.transform, driver);

            Camera cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            InputController.Create(boardRig.transform, store, tray, driver, cam, InputTuning.CreateDefault());
            hud = HudOverlay.Create(canvas.GetComponent<RectTransform>(), flow, ShowPause);
            music?.SetDriver(driver);
            TutorialDirector.Create(canvas.GetComponent<RectTransform>(), flow);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugOverlay.Create(boardRig.transform, store, flow.CurrentSeed, flow.Analytics);
#endif

            driver.RenderAll(store.State);
        }

        private void OnScreenChanged(FlowScreen screen)
        {
            bool playing = screen == FlowScreen.Playing;
            screensRoot.gameObject.SetActive(!playing);
            if (boardRig != null)
            {
                boardRig.SetActive(playing);
            }

            if (hud != null)
            {
                hud.gameObject.SetActive(playing);
            }

            if (playing)
            {
                return;
            }

            string id = screen.ToString();
            if (stack.TopId == id)
            {
                RefreshTop(screen);
                return;
            }

            if (Contains(id))
            {
                int guard = 0;
                while (stack.TopId != id && guard++ < 16 && stack.Pop())
                {
                }

                RefreshTop(screen);
                return;
            }

            RectTransform root = GetOrBuild(screen);
            Refresh(root);
            stack.Push(id, root);
        }

        private bool Contains(string id)
        {
            foreach (string entry in stack.Ids)
            {
                if (entry == id)
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshTop(FlowScreen screen)
        {
            if (screens.TryGetValue(screen, out RectTransform? root))
            {
                root.gameObject.SetActive(true);

                // A deactivation mid-transition kills the tween before its
                // onComplete re-arms the group (e.g. the age gate diving into L1
                // frames after boot) — a refreshed top is fully present, always.
                var group = root.GetComponent<CanvasGroup>();
                if (group != null)
                {
                    group.alpha = 1f;
                    group.interactable = true;
                }

                Refresh(root);
            }
        }

        private RectTransform GetOrBuild(FlowScreen screen)
        {
            if (screens.TryGetValue(screen, out RectTransform? existing))
            {
                return existing;
            }

            RectTransform root = Build(screen);
            screens[screen] = root;
            return root;
        }

        private RectTransform Build(FlowScreen screen)
        {
            return screen switch
            {
                FlowScreen.Home => HomeScreen.Build(screensRoot, flow),
                FlowScreen.ZoneMap => ZoneMapScreen.Build(screensRoot, flow),
                FlowScreen.Results => ResultsScreen.Build(screensRoot, flow),
                FlowScreen.DailyResults => DailyResultsScreen.Build(screensRoot, flow),
                FlowScreen.DailyIntro => DailyIntroScreen.Build(screensRoot, flow),
                FlowScreen.Settings => SettingsScreen.Build(screensRoot, flow),
                FlowScreen.Shop => ShopScreen.Build(screensRoot, flow, this),
                FlowScreen.Tidepool => TidepoolScreen.Build(screensRoot, flow),
                _ => UiComponents.Card(screensRoot, "unknown", Vector2.zero),
            };
        }

        private static void Refresh(RectTransform root)
        {
            IScreenRefresh? refresh = root.GetComponent<IScreenRefresh>() as IScreenRefresh
                ?? root.GetComponentInChildren<IScreenRefresh>(true);
            refresh?.Refresh();
        }
    }

    /// <summary>Screens implement this to re-read flow state every time they show.</summary>
    public interface IScreenRefresh
    {
        void Refresh();
    }
}
