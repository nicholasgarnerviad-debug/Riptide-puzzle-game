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
        private Button voyageContinue = null!;
        private TextMeshProUGUI endlessCaption = null!;
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

            // Wordmark: big expressive type (research: bold display type is the
            // 2025 menu signature) — ice→cyan vertex gradient + soft glow echo.
            TextMeshProUGUI wordmarkGlow = UiText.Create(root, "wordmarkGlow",
                flow.Strings.Get("app.title"), "display", "glow.primary");
            wordmarkGlow.characterSpacing = 16f;
            wordmarkGlow.fontSize = 104f;
            UiComponents.Place(wordmarkGlow.rectTransform, new Vector2(0.5f, 0.915f), new Vector2(940f, 150f));
            wordmarkGlow.rectTransform.localScale = Vector3.one * 1.035f;

            TextMeshProUGUI wordmark = UiText.Create(root, "wordmark", flow.Strings.Get("app.title"),
                "display", "accent.primary");
            wordmark.characterSpacing = 16f;
            wordmark.fontSize = 104f;
            wordmark.enableVertexGradient = true;
            wordmark.colorGradient = new VertexGradient(
                ThemeRuntime.Color("block.ice"), ThemeRuntime.Color("block.ice"),
                ThemeRuntime.Color("accent.primary"), ThemeRuntime.Color("accent.primary"));
            UiComponents.Place(wordmark.rectTransform, new Vector2(0.5f, 0.915f), new Vector2(940f, 150f));

            // Coin pill, top-right.
            RectTransform coinPill = UiComponents.Rect(root, "coinPill", new Vector2(250f, 78f));
            UiComponents.Place(coinPill, new Vector2(0.84f, 0.957f), new Vector2(250f, 78f));
            Image pillImage = UiComponents.RoundedImage(coinPill.gameObject, 39f);
            pillImage.raycastTarget = false;
            ThemedElement.Bind(coinPill.gameObject, "bg.surface");
            UiComponents.RoundedStrokeImage(coinPill, "stroke.subtle", 39f);
            screen.coins = UiComponents.CoinCounterComponent(coinPill);
            UiComponents.Place((RectTransform)screen.coins.transform, new Vector2(0.5f, 0.5f), new Vector2(230f, 64f));

            // VOYAGE HERO — the one big thing on the screen (research: a single
            // dominant play action; everything else recedes).
            RectTransform voyageCard = UiComponents.Card(root, "voyage", new Vector2(960f, 520f));
            UiComponents.Place(voyageCard, new Vector2(0.5f, 0.645f), new Vector2(960f, 520f));
            UiComponents.RoundedStrokeImage(voyageCard, "accent.deep", 32f);

            RectTransform badge = UiComponents.Rect(voyageCard, "badge", new Vector2(132f, 132f));
            UiComponents.Place(badge, new Vector2(0.15f, 0.78f), new Vector2(132f, 132f));
            var badgeBg = badge.gameObject.AddComponent<Image>();
            badgeBg.sprite = SpriteFactory.Dot();
            badgeBg.raycastTarget = false;
            ThemedElement.Bind(badge.gameObject, "bg.raised");
            RectTransform badgeIcon = UiComponents.Rect(badge, "icon", new Vector2(84f, 84f));
            UiComponents.Place(badgeIcon, new Vector2(0.5f, 0.5f), new Vector2(84f, 84f));
            var badgeIconImage = badgeIcon.gameObject.AddComponent<Image>();
            badgeIconImage.sprite = MenuSprites.Icon("compass");
            badgeIconImage.raycastTarget = false;
            ThemedElement.Bind(badgeIcon.gameObject, "accent.primary");

            TextMeshProUGUI voyageTitle = UiText.Create(voyageCard, "title",
                flow.Strings.Get("home.voyage"), "title", "accent.primary");
            voyageTitle.alignment = TextAlignmentOptions.Left;
            UiComponents.Place(voyageTitle.rectTransform, new Vector2(0.62f, 0.84f), new Vector2(560f, 80f));
            screen.voyageProgress = UiText.Create(voyageCard, "progress", "", "body", "text.secondary");
            screen.voyageProgress.alignment = TextAlignmentOptions.Left;
            UiComponents.Place(screen.voyageProgress.rectTransform, new Vector2(0.62f, 0.69f), new Vector2(560f, 56f));
            screen.zonePips = UiComponents.ProgressPipsComponent(voyageCard, 20);
            UiComponents.Place((RectTransform)screen.zonePips.transform, new Vector2(0.5f, 0.52f), new Vector2(820f, 36f));
            screen.voyageContinue = UiComponents.ButtonPrimary(voyageCard, "continue", "",
                () =>
                {
                    (int z, int i) = flow.Meta.Voyage.NextLevel();
                    flow.StartVoyageLevel(z, i);
                });
            UiComponents.Place((RectTransform)screen.voyageContinue.transform, new Vector2(0.5f, 0.21f), new Vector2(680f, 150f));
            var voyageButton = voyageCard.gameObject.AddComponent<Button>();
            voyageButton.onClick.AddListener(() => flow.GoTo(FlowScreen.ZoneMap));
            voyageCard.gameObject.AddComponent<PressEffect>();

            // DAILY + ENDLESS, two-up: dense, distinct, each with an icon identity.
            RectTransform dailyCard = UiComponents.Card(root, "daily", new Vector2(462f, 360f));
            UiComponents.Place(dailyCard, new Vector2(0.262f, 0.388f), new Vector2(462f, 360f));
            RectTransform foamBorder = UiComponents.Rect(dailyCard, "foamBorder", Vector2.zero);
            foamBorder.anchorMin = new Vector2(0f, 1f);
            foamBorder.anchorMax = new Vector2(1f, 1f);
            foamBorder.offsetMin = new Vector2(24f, -5f);
            foamBorder.offsetMax = new Vector2(-24f, -1f);
            var foamImage = foamBorder.gameObject.AddComponent<Image>();
            foamImage.sprite = SpriteFactory.Solid();
            foamImage.raycastTarget = false;
            ThemedElement.Bind(foamBorder.gameObject, "water.foamLine");
            CardIcon(dailyCard, "sun", "warning");
            TextMeshProUGUI dailyTitle = UiText.Create(dailyCard, "title",
                flow.Strings.Get("home.daily"), "heading", "text.primary");
            UiComponents.Place(dailyTitle.rectTransform, new Vector2(0.5f, 0.42f), new Vector2(430f, 60f));
            screen.dailyState = UiText.Create(dailyCard, "state", "", "caption", "text.secondary");
            UiComponents.Place(screen.dailyState.rectTransform, new Vector2(0.5f, 0.21f), new Vector2(430f, 50f));
            screen.streak = UiComponents.StreakFlameComponent(dailyCard);
            UiComponents.Place((RectTransform)screen.streak.transform, new Vector2(0.84f, 0.85f), new Vector2(160f, 60f));
            var dailyButton = dailyCard.gameObject.AddComponent<Button>();
            dailyButton.onClick.AddListener(() => flow.GoTo(FlowScreen.DailyIntro));
            dailyCard.gameObject.AddComponent<PressEffect>();

            RectTransform endlessCard = UiComponents.Card(root, "endless", new Vector2(462f, 360f));
            UiComponents.Place(endlessCard, new Vector2(0.738f, 0.388f), new Vector2(462f, 360f));
            UiComponents.RoundedStrokeImage(endlessCard, "block.violet", 32f);
            CardIcon(endlessCard, "waves", "block.violet");
            TextMeshProUGUI endlessTitle = UiText.Create(endlessCard, "title",
                flow.Strings.Get("home.endless"), "heading", "text.primary");
            UiComponents.Place(endlessTitle.rectTransform, new Vector2(0.5f, 0.42f), new Vector2(430f, 60f));
            screen.endlessCaption = UiText.Create(endlessCard, "caption", "", "caption", "text.secondary");
            UiComponents.Place(screen.endlessCaption.rectTransform, new Vector2(0.5f, 0.21f), new Vector2(430f, 50f));
            var endlessButton = endlessCard.gameObject.AddComponent<Button>();
            endlessButton.onClick.AddListener(flow.StartEndless);
            endlessCard.gameObject.AddComponent<PressEffect>();

            // Bottom icon bar: compact, icon-first (research: recognizable icons
            // in a consistent row, not full-width text slabs).
            Button tidepool = BarButton(root, 0.14f, "tidepool", "fish", flow.Strings.Get("home.tidepool"),
                "accent.primary", () => flow.GoTo(FlowScreen.Tidepool));
            Button chest = BarButton(root, 0.38f, "chest", "chest", flow.Strings.Get("home.chest"),
                "coin", () => { flow.TryClaimChestViaAd(); screen.Refresh(); });
            AdBadge(chest);
            Button shop = BarButton(root, 0.62f, "shop", "bag", flow.Strings.Get("shop.title"),
                "positive", () => flow.GoTo(FlowScreen.Shop));
            Button settings = BarButton(root, 0.86f, "settings", "gear", flow.Strings.Get("home.settings"),
                "text.secondary", () => flow.GoTo(FlowScreen.Settings));

            screen.sections = new[]
            {
                wordmark.rectTransform, voyageCard, dailyCard, endlessCard,
                (RectTransform)tidepool.transform, (RectTransform)chest.transform,
                (RectTransform)shop.transform, (RectTransform)settings.transform,
            };

            screen.Refresh();
            return root;
        }

        /// <summary>Icon mark in a card's upper half.</summary>
        private static void CardIcon(RectTransform card, string iconId, string colorToken)
        {
            RectTransform icon = UiComponents.Rect(card, "icon", new Vector2(96f, 96f));
            UiComponents.Place(icon, new Vector2(0.5f, 0.70f), new Vector2(96f, 96f));
            var image = icon.gameObject.AddComponent<Image>();
            image.sprite = MenuSprites.Icon(iconId);
            image.raycastTarget = false;
            ThemedElement.Bind(icon.gameObject, colorToken);
        }

        /// <summary>Square icon button for the bottom bar: glyph above a micro label.</summary>
        private static Button BarButton(RectTransform root, float x, string name, string iconId,
            string label, string iconToken, System.Action onClick)
        {
            RectTransform rt = UiComponents.Rect(root, name, new Vector2(200f, 180f));
            UiComponents.Place(rt, new Vector2(x, 0.145f), new Vector2(200f, 180f));
            Image bg = UiComponents.RoundedImage(rt.gameObject, 24f);
            ThemedElement.Bind(rt.gameObject, "bg.raised");
            UiComponents.RoundedStrokeImage(rt, "stroke.subtle", 24f);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(() => onClick());
            rt.gameObject.AddComponent<PressEffect>();

            RectTransform icon = UiComponents.Rect(rt, "icon", new Vector2(76f, 76f));
            UiComponents.Place(icon, new Vector2(0.5f, 0.64f), new Vector2(76f, 76f));
            var iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.sprite = MenuSprites.Icon(iconId);
            iconImage.raycastTarget = false;
            ThemedElement.Bind(icon.gameObject, iconToken);

            TextMeshProUGUI text = UiText.Create(rt, "label", label, "micro", "text.secondary");
            UiComponents.Place(text.rectTransform, new Vector2(0.5f, 0.16f), new Vector2(190f, 36f));
            return button;
        }

        /// <summary>Small amber AD corner badge (rewarded affordance).</summary>
        private static void AdBadge(Button host)
        {
            RectTransform badge = UiComponents.Rect((RectTransform)host.transform, "adBadge", new Vector2(58f, 58f));
            UiComponents.Place(badge, new Vector2(0.88f, 0.88f), new Vector2(58f, 58f));
            var image = badge.gameObject.AddComponent<Image>();
            image.sprite = SpriteFactory.Dot();
            image.raycastTarget = false;
            ThemedElement.Bind(badge.gameObject, "warning");
            TextMeshProUGUI ad = UiText.Create(badge, "ad", "AD", "micro", "text.onAccent");
            UiComponents.Stretch(ad.rectTransform);
        }

        public void Refresh()
        {
            (int zone, int index) = flow.Meta.Voyage.NextLevel();
            // The zone NAME carries the adventure (spec §4.2 zones.* strings).
            voyageProgress.text = string.Format(flow.Strings.Get("home.zoneNamed"),
                zone, index, flow.Strings.Get($"zones.{zone}"));
            voyageContinue.GetComponentInChildren<TextMeshProUGUI>().text =
                string.Format(flow.Strings.Get("home.voyageContinue"), zone, index);

            // Genre pass: the endless card carries the score to chase.
            long best = flow.Meta.EndlessBest;
            endlessCaption.text = best > 0
                ? string.Format(flow.Strings.Get("endless.best"), Riptide.Core.ShareCard.GroupThousands(best))
                : flow.Strings.Get("home.endlessHint");

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

    /// <summary>Spec §4.2 zone map: 10 zone cards, 20 nodes each, stars and locks.
    /// Modern pass: named zone header cards, circular nodes with state rings,
    /// the current level glowing (§4.2 node states).</summary>
    public sealed class ZoneMapScreen : MonoBehaviour, IScreenRefresh
    {
        private GameFlow flow = null!;
        private readonly System.Collections.Generic.List<(int zone, int index, Button button,
            TextMeshProUGUI label, Image ring, GameObject glow)> nodes
            = new System.Collections.Generic.List<(int, int, Button, TextMeshProUGUI, Image, GameObject)>();
        private readonly System.Collections.Generic.List<(int zone, TextMeshProUGUI stars)> zoneStars
            = new System.Collections.Generic.List<(int, TextMeshProUGUI)>();

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

            const float headerH = 130f;
            const float cell = 184f;
            float y = 20f;
            for (int zone = 1; zone <= 10; zone++)
            {
                // Named zone header card (§4.2; zones.* finally on the map).
                RectTransform headerCard = UiComponents.Rect(content, $"zoneHeader{zone}", new Vector2(940f, 110f));
                UiComponents.Place(headerCard, new Vector2(0.5f, 1f), new Vector2(940f, 110f));
                headerCard.anchoredPosition = new Vector2(0f, -y - 55f);
                var headerImage = headerCard.gameObject.AddComponent<Image>();
                headerImage.sprite = MenuSprites.PanelGradient();
                headerImage.type = Image.Type.Sliced;
                headerImage.pixelsPerUnitMultiplier = 14f * (100f / 64f) / 24f;
                headerImage.raycastTarget = false;
                ThemedElement.Bind(headerCard.gameObject, "bg.surface");
                TextMeshProUGUI header = UiText.Create(headerCard, "name",
                    string.Format(flow.Strings.Get("zone.title"), zone) + " — "
                        + flow.Strings.Get($"zones.{zone}"),
                    "heading", "accent.primary");
                header.alignment = TextAlignmentOptions.Left;
                UiComponents.Place(header.rectTransform, new Vector2(0.34f, 0.5f), new Vector2(560f, 70f));
                TextMeshProUGUI stars = UiText.Create(headerCard, "stars", "", "caption", "text.secondary");
                stars.alignment = TextAlignmentOptions.Right;
                UiComponents.Place(stars.rectTransform, new Vector2(0.83f, 0.5f), new Vector2(260f, 50f));
                screen.zoneStars.Add((zone, stars));
                y += headerH;

                for (int index = 1; index <= 20; index++)
                {
                    int col = (index - 1) % 5;
                    int row = (index - 1) / 5;
                    int zoneCopy = zone;
                    int indexCopy = index;

                    // Circular node: glow (current) under ring under inner disc.
                    RectTransform nodeRt = UiComponents.Rect(content, $"z{zone}l{index}", new Vector2(146f, 146f));
                    UiComponents.Place(nodeRt, new Vector2(0.5f, 1f), new Vector2(146f, 146f));
                    nodeRt.anchoredPosition = new Vector2((col - 2) * cell, -y - row * cell - cell * 0.5f);

                    var glowGo = new GameObject("glow", typeof(RectTransform));
                    glowGo.transform.SetParent(nodeRt, false);
                    ((RectTransform)glowGo.transform).sizeDelta = new Vector2(210f, 210f);
                    var glowImage = glowGo.AddComponent<Image>();
                    glowImage.sprite = MenuSprites.SoftGlow();
                    glowImage.raycastTarget = false;
                    ThemedElement.Bind(glowGo, "glow.primary");
                    glowGo.SetActive(false);

                    var ringImage = nodeRt.gameObject.AddComponent<Image>();
                    ringImage.sprite = SpriteFactory.Dot();

                    RectTransform inner = UiComponents.Rect(nodeRt, "inner", new Vector2(130f, 130f));
                    UiComponents.Place(inner, new Vector2(0.5f, 0.5f), new Vector2(130f, 130f));
                    var innerImage = inner.gameObject.AddComponent<Image>();
                    innerImage.sprite = SpriteFactory.Dot();
                    innerImage.raycastTarget = false;
                    ThemedElement.Bind(inner.gameObject, "bg.deep");

                    TextMeshProUGUI label = UiText.Create(nodeRt, "label", "", "body", "text.primary");
                    UiComponents.Stretch(label.rectTransform);

                    var node = nodeRt.gameObject.AddComponent<Button>();
                    node.targetGraphic = ringImage;
                    node.onClick.AddListener(() => screen.OnNode(zoneCopy, indexCopy));
                    nodeRt.gameObject.AddComponent<PressEffect>();

                    screen.nodes.Add((zone, index, node, label, ringImage, glowGo));
                }

                y += 4 * cell + 40f;
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
            (int currentZone, int currentIndex) = flow.Meta.Voyage.NextLevel();
            foreach ((int zone, int index, Button button, TextMeshProUGUI label, Image ring, GameObject glow) in nodes)
            {
                bool unlocked = flow.Meta.Voyage.IsUnlocked(zone, index);
                bool current = zone == currentZone && index == currentIndex;
                int stars = flow.Meta.Voyage.StarsFor(VoyageProgress.LevelId(zone, index));
                button.interactable = unlocked;
                glow.SetActive(current);
                // §4.2 node states: locked subtle ring · current cyan + glow ·
                // complete bright ring with star count.
                ring.color = ThemeRuntime.Color(current ? "accent.primary"
                    : stars > 0 ? "stroke.bright" : unlocked ? "bg.raised" : "stroke.subtle");
                // '*' until an icon set lands: LiberationSans SDF has no U+2605 star.
                label.text = unlocked
                    ? (stars > 0 ? $"{index}\n{new string('*', stars)}" : index.ToString())
                    : "—";
                label.color = ThemeRuntime.Color(unlocked ? "text.primary" : "text.muted");
            }

            foreach ((int zone, TextMeshProUGUI stars) in zoneStars)
            {
                int total = 0;
                for (int index = 1; index <= 20; index++)
                {
                    total += flow.Meta.Voyage.StarsFor(VoyageProgress.LevelId(zone, index));
                }

                stars.text = string.Format(flow.Strings.Get("zone.stars"), total);
            }
        }
    }
}
