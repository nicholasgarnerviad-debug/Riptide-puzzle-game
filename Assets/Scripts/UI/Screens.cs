using Riptide.Core;
using Riptide.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// Spec §4.1 Home, hero-ified (ROADMAP M6): animated water band under the
    /// wordmark, Voyage as a hero card with zone progress, the Daily card with
    /// streak + readiness, Endless, and a slim utility row. Sections cascade in
    /// per the §1.4 stagger grammar.
    /// </summary>
    public sealed class HomeScreen : MonoBehaviour, IScreenRefresh
    {
        private GameFlow flow = null!;
        private CoinCounter coins = null!;
        private StreakFlame streak = null!;
        private TextMeshProUGUI voyageProgress = null!;
        private ProgressPips zonePips = null!;
        private TextMeshProUGUI dailyState = null!;
        private RectTransform[] sections = null!;

        public static RectTransform Build(RectTransform parent, GameFlow flow)
        {
            RectTransform root = ScreenChrome.Root(parent, "HomeScreen");
            var screen = root.gameObject.AddComponent<HomeScreen>();
            screen.flow = flow;

            // Hero band: tinted fade + wordmark + bobbing foam strip.
            RectTransform band = UiComponents.Rect(root, "band", Vector2.zero);
            band.anchorMin = new Vector2(0f, 0.80f);
            band.anchorMax = new Vector2(1f, 1f);
            band.offsetMin = Vector2.zero;
            band.offsetMax = Vector2.zero;
            var bandImage = band.gameObject.AddComponent<Image>();
            bandImage.sprite = SpriteFactory.VerticalFade();
            bandImage.raycastTarget = false;
            ThemedElement.Bind(band.gameObject, "water.calm.btm");
            band.gameObject.AddComponent<HomeWaterBand>();

            TextMeshProUGUI wordmark = UiText.Create(root, "wordmark", flow.Strings.Get("app.title"),
                "display", "accent.primary");
            wordmark.characterSpacing = 18f;
            UiComponents.Place(wordmark.rectTransform, new Vector2(0.5f, 0.885f), new Vector2(900f, 130f));

            screen.coins = UiComponents.CoinCounterComponent(root);
            UiComponents.Place((RectTransform)screen.coins.transform, new Vector2(0.80f, 0.955f), new Vector2(300f, 64f));

            // Voyage hero card.
            RectTransform voyageCard = UiComponents.Card(root, "voyage", new Vector2(940f, 300f));
            UiComponents.Place(voyageCard, new Vector2(0.5f, 0.665f), new Vector2(940f, 300f));
            UiComponents.RoundedStrokeImage(voyageCard, "accent.deep", 32f);
            TextMeshProUGUI voyageTitle = UiText.Create(voyageCard, "title",
                flow.Strings.Get("home.voyage"), "title", "accent.primary");
            UiComponents.Place(voyageTitle.rectTransform, new Vector2(0.5f, 0.74f), new Vector2(860f, 80f));
            screen.voyageProgress = UiText.Create(voyageCard, "progress", "", "body", "text.secondary");
            UiComponents.Place(screen.voyageProgress.rectTransform, new Vector2(0.5f, 0.46f), new Vector2(860f, 60f));
            screen.zonePips = UiComponents.ProgressPipsComponent(voyageCard, 20);
            UiComponents.Place((RectTransform)screen.zonePips.transform, new Vector2(0.5f, 0.20f), new Vector2(820f, 36f));
            var voyageButton = voyageCard.gameObject.AddComponent<Button>();
            voyageButton.onClick.AddListener(() => flow.GoTo(FlowScreen.ZoneMap));
            voyageCard.gameObject.AddComponent<PressEffect>();

            // Daily card.
            RectTransform dailyCard = UiComponents.Card(root, "daily", new Vector2(940f, 220f));
            UiComponents.Place(dailyCard, new Vector2(0.5f, 0.475f), new Vector2(940f, 220f));
            TextMeshProUGUI dailyTitle = UiText.Create(dailyCard, "title",
                flow.Strings.Get("home.daily"), "heading", "text.primary");
            UiComponents.Place(dailyTitle.rectTransform, new Vector2(0.40f, 0.66f), new Vector2(620f, 70f));
            screen.dailyState = UiText.Create(dailyCard, "state", "", "caption", "text.secondary");
            UiComponents.Place(screen.dailyState.rectTransform, new Vector2(0.40f, 0.28f), new Vector2(620f, 50f));
            screen.streak = UiComponents.StreakFlameComponent(dailyCard);
            UiComponents.Place((RectTransform)screen.streak.transform, new Vector2(0.85f, 0.5f), new Vector2(200f, 64f));
            var dailyButton = dailyCard.gameObject.AddComponent<Button>();
            dailyButton.onClick.AddListener(() => flow.GoTo(FlowScreen.DailyIntro));
            dailyCard.gameObject.AddComponent<PressEffect>();

            Button endless = UiComponents.ButtonSecondary(root, "endless", flow.Strings.Get("home.endless"),
                flow.StartEndless);
            UiComponents.Place((RectTransform)endless.transform, new Vector2(0.5f, 0.335f), new Vector2(940f, 120f));

            Button tidepool = UiComponents.ButtonSecondary(root, "tidepool", flow.Strings.Get("home.tidepool"),
                () => flow.GoTo(FlowScreen.Tidepool));
            UiComponents.Place((RectTransform)tidepool.transform, new Vector2(0.30f, 0.225f), new Vector2(520f, 110f));

            Button chest = UiComponents.ButtonReward(root, "chest", flow.Strings.Get("home.chest"),
                () => { flow.TryClaimChestViaAd(); screen.Refresh(); });
            UiComponents.Place((RectTransform)chest.transform, new Vector2(0.745f, 0.225f), new Vector2(380f, 110f));

            Button shop = UiComponents.ButtonGhost(root, "shop", flow.Strings.Get("shop.title"),
                () => flow.GoTo(FlowScreen.Shop));
            UiComponents.Place((RectTransform)shop.transform, new Vector2(0.30f, 0.125f), new Vector2(420f, 88f));

            Button settings = UiComponents.ButtonGhost(root, "settings", flow.Strings.Get("home.settings"),
                () => flow.GoTo(FlowScreen.Settings));
            UiComponents.Place((RectTransform)settings.transform, new Vector2(0.70f, 0.125f), new Vector2(420f, 88f));

            screen.sections = new[]
            {
                wordmark.rectTransform, voyageCard, dailyCard,
                (RectTransform)endless.transform, (RectTransform)tidepool.transform,
                (RectTransform)chest.transform, (RectTransform)shop.transform,
                (RectTransform)settings.transform,
            };

            screen.Refresh();
            return root;
        }

        public void Refresh()
        {
            (int zone, int index) = flow.Meta.Voyage.NextLevel();
            voyageProgress.text = flow.Meta.Voyage.TotalStars == 0
                ? flow.Strings.Get("home.voyage")
                : string.Format(flow.Strings.Get("home.zoneProgress"), zone, index);

            int completedInZone = Mathf.Clamp(index - 1, 0, 20);
            zonePips.SetFilled(completedInZone);

            dailyState.text = flow.Meta.CanAttemptDailyToday()
                ? flow.Strings.Get("home.dailyReady")
                : flow.Strings.Get("daily.attempted");

            int current = flow.Meta.Streak.Current;
            streak.gameObject.SetActive(current > 0);
            streak.Set(current, pulse: false);
            coins.SetInstant(flow.Meta.Coins);

            if (isActiveAndEnabled && sections != null)
            {
                UiCascade.Run(this, sections);
            }
        }
    }

    /// <summary>The hero band's bobbing foam strip — pure ambience, parked when reduced.</summary>
    internal sealed class HomeWaterBand : MonoBehaviour
    {
        private RectTransform foam = null!;
        private float baseY;
        private float time;

        private void Start()
        {
            var foamGo = new GameObject("foam", typeof(RectTransform));
            foamGo.transform.SetParent(transform, false);
            foam = (RectTransform)foamGo.transform;
            foam.anchorMin = new Vector2(0f, 0f);
            foam.anchorMax = new Vector2(1f, 0f);
            foam.sizeDelta = new Vector2(0f, 5f);
            foam.anchoredPosition = new Vector2(0f, 8f);
            baseY = 8f;
            var image = foamGo.AddComponent<UnityEngine.UI.Image>();
            image.sprite = SpriteFactory.Solid();
            image.raycastTarget = false;
            ThemedElement.Bind(foamGo, "water.foamLine");
        }

        private void Update()
        {
            if (ThemeRuntime.ReducedMotion || foam == null)
            {
                return;
            }

            time += Time.deltaTime;
            foam.anchoredPosition = new Vector2(0f, baseY + Mathf.Sin(time * 0.9f) * 5f);
        }
    }

    /// <summary>Spec §4.2 zone map: 10 zone cards, 20 nodes each, stars and locks.</summary>
    public sealed class ZoneMapScreen : MonoBehaviour, IScreenRefresh
    {
        private GameFlow flow = null!;
        private readonly System.Collections.Generic.List<(int zone, int index, Button button, TextMeshProUGUI label)> nodes
            = new System.Collections.Generic.List<(int, int, Button, TextMeshProUGUI)>();

        public static RectTransform Build(RectTransform parent, GameFlow flow)
        {
            RectTransform root = ScreenChrome.Root(parent, "ZoneMapScreen");
            var screen = root.gameObject.AddComponent<ZoneMapScreen>();
            screen.flow = flow;

            Button back = UiComponents.ButtonGhost(root, "back", flow.Strings.Get("common.back"),
                () => flow.GoTo(FlowScreen.Home));
            UiComponents.Place((RectTransform)back.transform, new Vector2(0.14f, 0.955f), new Vector2(240f, 80f));

            RectTransform scrollRoot = UiComponents.Rect(root, "scroll", Vector2.zero);
            scrollRoot.anchorMin = new Vector2(0.04f, 0.02f);
            scrollRoot.anchorMax = new Vector2(0.96f, 0.92f);
            scrollRoot.offsetMin = Vector2.zero;
            scrollRoot.offsetMax = Vector2.zero;
            var scrollImage = scrollRoot.gameObject.AddComponent<Image>();
            scrollImage.sprite = SpriteFactory.Solid();
            ThemedElement.Bind(scrollRoot.gameObject, "bg.abyss");
            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scrollRoot.gameObject.AddComponent<RectMask2D>();

            RectTransform content = UiComponents.Rect(scrollRoot, "content", Vector2.zero);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;

            const float headerH = 100f;
            const float cell = 184f;
            const float pad = 16f;
            float y = 0f;
            for (int zone = 1; zone <= 10; zone++)
            {
                TextMeshProUGUI header = UiText.Create(content, $"zone{zone}",
                    string.Format(flow.Strings.Get("zone.title"), zone), "heading", "text.primary");
                UiComponents.Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(600f, headerH));
                header.rectTransform.anchoredPosition = new Vector2(0f, -y - headerH * 0.5f);
                y += headerH;

                for (int index = 1; index <= 20; index++)
                {
                    int col = (index - 1) % 5;
                    int row = (index - 1) / 5;
                    int zoneCopy = zone;
                    int indexCopy = index;
                    Button node = UiComponents.ButtonSecondary(content, $"z{zone}l{index}", index.ToString(),
                        () => screen.OnNode(zoneCopy, indexCopy));
                    var nodeRt = (RectTransform)node.transform;
                    UiComponents.Place(nodeRt, new Vector2(0.5f, 1f), new Vector2(cell - pad, cell - pad));
                    nodeRt.anchoredPosition = new Vector2((col - 2) * cell, -y - row * cell - cell * 0.5f);
                    screen.nodes.Add((zone, index, node, node.GetComponentInChildren<TextMeshProUGUI>()));
                }

                y += 4 * cell + 30f;
            }

            content.sizeDelta = new Vector2(0f, y + 40f);
            screen.Refresh();
            return root;
        }

        private void OnNode(int zone, int index)
        {
            if (flow.Meta.Voyage.IsUnlocked(zone, index))
            {
                flow.StartVoyageLevel(zone, index);
            }
        }

        public void Refresh()
        {
            foreach ((int zone, int index, Button button, TextMeshProUGUI label) in nodes)
            {
                bool unlocked = flow.Meta.Voyage.IsUnlocked(zone, index);
                int stars = flow.Meta.Voyage.StarsFor(VoyageProgress.LevelId(zone, index));
                button.interactable = unlocked;
                // '*' until an icon set lands: LiberationSans SDF has no U+2605 star.
                label.text = unlocked
                    ? (stars > 0 ? $"{index}\n{new string('*', stars)}" : index.ToString())
                    : "—";
                label.color = ThemeRuntime.Color(unlocked ? "text.primary" : "text.muted");
            }
        }
    }
}
