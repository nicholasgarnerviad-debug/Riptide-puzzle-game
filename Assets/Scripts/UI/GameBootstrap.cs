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
        public TideMeterRing Meter { get; private set; } = null!;
        public BoardChromeView Chrome { get; private set; } = null!;
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

            // GDD 7: monetization services — fakes until the SDK defines are on (DECISIONS.md).
            MainThreadDispatcher.Ensure();
            var analytics = new AnalyticsService();
#if RIPTIDE_FIREBASE
            analytics.AddSink(new FirebaseAnalyticsSink());
#endif
#if RIPTIDE_ADMOB
            IConsentProvider consentProvider = new UmpConsentProvider();
            // Google's published TEST ad units; swap for prod ids at release (GDD 6).
            IAdSdk adSdk = new GoogleMobileAdsAdapter(
                "ca-app-pub-3940256099942544/1033173712",
                "ca-app-pub-3940256099942544/5224354917");
#else
            IConsentProvider consentProvider = new FakeConsentProvider();
            IAdSdk adSdk = new FakeAdSdk();
#endif
            var consent = new ConsentService(consentProvider);
            var ads = new AdService(adSdk, consent, meta, economy.Ads, analytics);
#if RIPTIDE_IAP
            IIapSdk iapSdk = new UnityIapAdapter();
#else
            IIapSdk iapSdk = new FakeIapSdk();
#endif
            var iap = new IapService(iapSdk, meta, analytics);
            flow.AttachServices(analytics, consent, ads, iap);

            // GDD 7A: consent resolves BEFORE any ad init (AdService listens).
            consent.Request();

            // Mid-run save: a record that survived a process death surfaces as a
            // resume prompt over Home (SAVE_RESUME_DESIGN.md §5).
            flow.DetectPendingRun();

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
            Meter = TideMeterRing.Create(transform);
            Chrome = BoardChromeView.Create(transform);
            MarineSnow.Create(transform);
            Driver = AnimationDriver.Create(transform, Store, Board, Water, Tray, Meter, Chrome);
            Driver.InstantMode = instantAnimations;
            JuiceDirector.Create(transform, Driver);

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
            cam.backgroundColor = Palette.Background;
            cam.clearFlags = CameraClearFlags.SolidColor;

            // Universal screen fit: ortho size + Y from CameraFit.Solve (Core,
            // device-matrix tested); re-applied on resolution/safe-area changes.
            CameraFitter.Attach(cam);
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
