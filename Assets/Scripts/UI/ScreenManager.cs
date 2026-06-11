using System.Collections.Generic;
using Riptide.Core;
using Riptide.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// Owns the screen canvas, builds GDD 9 screens lazily, follows GameFlow
    /// transitions, and bridges run-end (animations settled) to the results flow.
    /// The Phase 4 board rig is built on the first run and toggled with Playing.
    /// </summary>
    public sealed class ScreenManager : MonoBehaviour
    {
        private GameFlow flow = null!;
        private Canvas canvas = null!;
        private readonly Dictionary<FlowScreen, RectTransform> screens = new Dictionary<FlowScreen, RectTransform>();

        private GameObject? boardRig;
        private AnimationDriver? driver;
        private HudOverlay? hud;
        private bool instantAnimations;

        public GameFlow Flow => flow;
        public AnimationDriver? Driver => driver;

        public static ScreenManager Create(Transform parent, GameFlow flow, bool instantAnimations)
        {
            var go = new GameObject("ScreenManager");
            go.transform.SetParent(parent, false);
            var manager = go.AddComponent<ScreenManager>();
            manager.flow = flow;
            manager.instantAnimations = instantAnimations;
            manager.canvas = UiKit.CreateCanvas(go.transform, "ScreenCanvas");
            flow.ScreenChanged += manager.OnScreenChanged;
            flow.RunStarted += manager.OnRunStarted;
            manager.OnScreenChanged(flow.Screen);
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

        private void Update()
        {
            // Run ended and the animation settled — hand over to results.
            if (flow.Screen == FlowScreen.Playing
                && flow.Store != null
                && flow.Store.State.Status.IsTerminal()
                && (driver == null || !driver.IsAnimating)
                && flow.LastOutcome != null)
            {
                flow.ShowOutcomeScreen();
            }
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

            BoardView board = BoardView.Create(boardRig.transform);
            WaterView water = WaterView.Create(boardRig.transform, store.State.WaterLevel);
            TrayView tray = TrayView.Create(boardRig.transform);
            TideMeterView meter = TideMeterView.Create(boardRig.transform);
            driver = AnimationDriver.Create(boardRig.transform, store, board, water, tray, meter);
            driver.InstantMode = instantAnimations;

            Camera cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            InputController.Create(boardRig.transform, store, tray, driver, cam, InputTuning.CreateDefault());
            hud = HudOverlay.Create(canvas.GetComponent<RectTransform>(), flow);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugOverlay.Create(boardRig.transform, store, flow.CurrentSeed);
#endif

            driver.RenderAll(store.State);
        }

        private void OnScreenChanged(FlowScreen screen)
        {
            foreach (KeyValuePair<FlowScreen, RectTransform> entry in screens)
            {
                entry.Value.gameObject.SetActive(false);
            }

            if (screen != FlowScreen.Playing)
            {
                RectTransform root = GetOrBuild(screen);
                Refresh(screen, root);
                root.gameObject.SetActive(true);
            }

            if (boardRig != null)
            {
                boardRig.SetActive(screen == FlowScreen.Playing);
            }

            if (hud != null)
            {
                hud.gameObject.SetActive(screen == FlowScreen.Playing);
            }
        }

        private RectTransform GetOrBuild(FlowScreen screen)
        {
            if (screens.TryGetValue(screen, out RectTransform? existing))
            {
                return existing;
            }

            var canvasRoot = canvas.GetComponent<RectTransform>();
            RectTransform root = screen switch
            {
                FlowScreen.Home => HomeScreen.Build(canvasRoot, flow),
                FlowScreen.ZoneMap => ZoneMapScreen.Build(canvasRoot, flow),
                FlowScreen.Results => ResultsScreen.Build(canvasRoot, flow),
                FlowScreen.DailyResults => DailyResultsScreen.Build(canvasRoot, flow),
                FlowScreen.Settings => SettingsScreen.Build(canvasRoot, flow),
                FlowScreen.Shop => ShopSheet.Build(canvasRoot, flow),
                FlowScreen.Tidepool => TidepoolStubScreen.Build(canvasRoot, flow),
                _ => UiKit.Panel(canvasRoot, "unknown", UiKit.PanelColor),
            };
            screens[screen] = root;
            return root;
        }

        private void Refresh(FlowScreen screen, RectTransform root)
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
