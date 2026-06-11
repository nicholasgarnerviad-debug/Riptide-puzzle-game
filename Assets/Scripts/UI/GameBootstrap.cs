using System;
using Riptide.Core;
using Riptide.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Riptide.UI
{
    /// <summary>
    /// Builds the whole runtime scene from code (DECISIONS.md: no authored assets):
    /// camera framing, board/water/tray/meter views, animation driver, input,
    /// debug overlay. Auto-boots an endless game in SampleScene; tests call
    /// CreateGame with their own config and InstantMode.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        public GameStore Store { get; private set; } = null!;
        public BoardView Board { get; private set; } = null!;
        public WaterView Water { get; private set; } = null!;
        public TrayView Tray { get; private set; } = null!;
        public TideMeterView Meter { get; private set; } = null!;
        public AnimationDriver Driver { get; private set; } = null!;

        public static GameBootstrap CreateGame(LevelConfig config, ulong seed, bool instantAnimations)
        {
            var root = new GameObject("Riptide");
            var bootstrap = root.AddComponent<GameBootstrap>();
            bootstrap.Configure(config, seed, instantAnimations);
            return bootstrap;
        }

        /// <summary>The full app (GDD 9): content, meta, flow, screens. Starts at Home.</summary>
        public static (GameFlow flow, ScreenManager screens) CreateApp(bool instantAnimations = false)
        {
            var root = new GameObject("RiptideApp");
            var bootstrap = root.AddComponent<GameBootstrap>();
            bootstrap.SetUpCamera();

            EconomyConfig economy = RuntimeContent.LoadEconomy();
            CreatureRoster roster = RuntimeContent.LoadCreatures();
            StringTable strings = RuntimeContent.LoadStrings();
            var meta = new MetaServices();
            meta.Load();

            var flow = new GameFlow(economy, roster, strings, meta);
            ScreenManager screens = ScreenManager.Create(root.transform, flow, instantAnimations);
            return (flow, screens);
        }

        private void Configure(LevelConfig config, ulong seed, bool instantAnimations)
        {
            SetUpCamera();

            Store = new GameStore(config, seed);
            Board = BoardView.Create(transform);
            Water = WaterView.Create(transform, config.StartWaterLevel);
            Tray = TrayView.Create(transform);
            Meter = TideMeterView.Create(transform);
            Driver = AnimationDriver.Create(transform, Store, Board, Water, Tray, Meter);
            Driver.InstantMode = instantAnimations;

            Camera cam = Camera.main != null ? Camera.main : FindFirstCamera();
            InputController.Create(transform, Store, Tray, Driver, cam, InputTuning.CreateDefault());
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugOverlay.Create(transform, Store, seed);
#endif

            Driver.RenderAll(Store.State);
        }

        internal void SetUpCamera()
        {
            Camera cam = Camera.main != null ? Camera.main : FindFirstCamera();
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
            }

            cam.orthographic = true;
            cam.orthographicSize = 8.7f;
            cam.transform.position = new Vector3(0f, -0.2f, -10f);
            cam.backgroundColor = Palette.Background;
            cam.clearFlags = CameraClearFlags.SolidColor;
        }

        private static Camera FindFirstCamera()
        {
            return FindFirstObjectByType<Camera>();
        }

        /// <summary>Auto-boot an endless run when the shipped scene is played directly.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBoot()
        {
            if (SceneManager.GetActiveScene().name != "SampleScene")
            {
                return;
            }

            if (FindFirstObjectByType<GameBootstrap>() != null)
            {
                return;
            }

            try
            {
                CreateApp(instantAnimations: false);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Riptide auto-boot failed: {ex.Message}");
            }
        }
    }
}
