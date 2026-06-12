using Riptide.Core;
using Riptide.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// Spec §4.1 Home: type-based wordmark (until branded art), voyage continue as
    /// the primary action, Endless, the Daily card with streak flame, Tidepool and
    /// a settings ghost, coin counter + rewarded chest.
    /// </summary>
    public sealed class HomeScreen : MonoBehaviour, IScreenRefresh
    {
        private GameFlow flow = null!;
        private Button voyage = null!;
        private StreakFlame streak = null!;
        private CoinCounter coins = null!;

        public static RectTransform Build(RectTransform parent, GameFlow flow)
        {
            RectTransform root = ScreenChrome.Root(parent, "HomeScreen");
            var screen = root.gameObject.AddComponent<HomeScreen>();
            screen.flow = flow;

            TextMeshProUGUI wordmark = UiText.Create(root, "wordmark", flow.Strings.Get("app.title"),
                "display", "accent.primary");
            UiComponents.Place(wordmark.rectTransform, new Vector2(0.5f, 0.87f), new Vector2(900f, 140f));

            screen.coins = UiComponents.CoinCounterComponent(root);
            UiComponents.Place((RectTransform)screen.coins.transform, new Vector2(0.5f, 0.795f), new Vector2(300f, 72f));

            screen.voyage = UiComponents.ButtonPrimary(root, "voyage", flow.Strings.Get("home.voyage"),
                () => flow.GoTo(FlowScreen.ZoneMap));
            UiComponents.Place((RectTransform)screen.voyage.transform, new Vector2(0.5f, 0.645f), new Vector2(680f, 140f));

            Button endless = UiComponents.ButtonSecondary(root, "endless", flow.Strings.Get("home.endless"),
                flow.StartEndless);
            UiComponents.Place((RectTransform)endless.transform, new Vector2(0.5f, 0.525f), new Vector2(680f, 130f));

            Button daily = UiComponents.ButtonSecondary(root, "daily", flow.Strings.Get("home.daily"),
                () => flow.GoTo(FlowScreen.DailyIntro));
            UiComponents.Place((RectTransform)daily.transform, new Vector2(0.5f, 0.405f), new Vector2(680f, 130f));

            screen.streak = UiComponents.StreakFlameComponent(root);
            UiComponents.Place((RectTransform)screen.streak.transform, new Vector2(0.5f, 0.325f), new Vector2(240f, 64f));

            Button tidepool = UiComponents.ButtonSecondary(root, "tidepool", flow.Strings.Get("home.tidepool"),
                () => flow.GoTo(FlowScreen.Tidepool));
            UiComponents.Place((RectTransform)tidepool.transform, new Vector2(0.5f, 0.235f), new Vector2(680f, 120f));

            Button settings = UiComponents.ButtonGhost(root, "settings", flow.Strings.Get("home.settings"),
                () => flow.GoTo(FlowScreen.Settings));
            UiComponents.Place((RectTransform)settings.transform, new Vector2(0.32f, 0.115f), new Vector2(420f, 92f));

            // GDD 6: rewarded coin chest, capped 3/day by the save.
            Button chest = UiComponents.ButtonReward(root, "chest", flow.Strings.Get("home.chest"),
                () => { flow.TryClaimChestViaAd(); screen.Refresh(); });
            UiComponents.Place((RectTransform)chest.transform, new Vector2(0.70f, 0.115f), new Vector2(430f, 105f));

            screen.Refresh();
            return root;
        }

        public void Refresh()
        {
            (int zone, int index) = flow.Meta.Voyage.NextLevel();
            voyage.GetComponentInChildren<TextMeshProUGUI>().text = flow.Meta.Voyage.TotalStars == 0
                ? flow.Strings.Get("home.voyage")
                : string.Format(flow.Strings.Get("home.voyageContinue"), zone, index);

            int current = flow.Meta.Streak.Current;
            streak.gameObject.SetActive(current > 0);
            streak.Set(current, pulse: false);
            coins.SetInstant(flow.Meta.Coins);
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

    /// <summary>
    /// GDD 5.1 Tidepool: horizontally scrolling diorama of rescued species with
    /// lifetime counters and tap-for-flavor, plus the 20-item decoration coin sink.
    /// (Visual diorama pass — parallax, idle bobbing, slots — lands in 8-UI.)
    /// </summary>
    public sealed class TidepoolScreen : MonoBehaviour, IScreenRefresh
    {
        private GameFlow flow = null!;
        private Text coins = null!;
        private Text flavorLine = null!;
        private readonly System.Collections.Generic.List<(int id, Text count)> speciesCards
            = new System.Collections.Generic.List<(int, Text)>();
        private readonly System.Collections.Generic.List<(Decoration deco, Button button, Text label)> decoButtons
            = new System.Collections.Generic.List<(Decoration, Button, Text)>();
        private int flavorCursor;

        public static RectTransform Build(RectTransform parent, GameFlow flow)
        {
            RectTransform root = UiKit.Panel(parent, "TidepoolScreen", UiKit.PanelColor);
            UiKit.Stretch(root);
            var screen = root.gameObject.AddComponent<TidepoolScreen>();
            screen.flow = flow;

            Text title = UiKit.Label(root, "title", flow.Strings.Get("tidepool.title"), 64, UiKit.TextColor);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 0.945f), new Vector2(700f, 90f), Vector2.zero);

            screen.coins = UiKit.Label(root, "coins", "", 40, ThemeRuntime.Color("coin"));
            UiKit.Place(screen.coins.rectTransform, new Vector2(0.5f, 0.895f), new Vector2(500f, 60f), Vector2.zero);

            // The diorama: species swim in a horizontal scroll band.
            RectTransform dioramaRoot = UiKit.Panel(root, "diorama", new Color(0.04f, 0.10f, 0.14f, 0.9f));
            dioramaRoot.anchorMin = new Vector2(0.02f, 0.60f);
            dioramaRoot.anchorMax = new Vector2(0.98f, 0.865f);
            dioramaRoot.offsetMin = Vector2.zero;
            dioramaRoot.offsetMax = Vector2.zero;
            var dioramaScroll = dioramaRoot.gameObject.AddComponent<ScrollRect>();
            dioramaRoot.gameObject.AddComponent<RectMask2D>();
            RectTransform dioramaContent = UiKit.Container(dioramaRoot, "content");
            dioramaContent.anchorMin = new Vector2(0f, 0f);
            dioramaContent.anchorMax = new Vector2(0f, 1f);
            dioramaContent.pivot = new Vector2(0f, 0.5f);
            dioramaScroll.content = dioramaContent;
            dioramaScroll.vertical = false;
            dioramaScroll.horizontal = true;

            const float cardW = 290f;
            for (int i = 0; i < flow.Roster.Count; i++)
            {
                CreatureSpecies species = flow.Roster.Species[i];
                int speciesId = species.Id;
                RectTransform card = UiKit.Panel(dioramaContent, $"sp{speciesId}", new Color(1f, 1f, 1f, 0.05f));
                UiKit.Place(card, new Vector2(0f, 0.5f), new Vector2(cardW - 18f, 380f),
                    new Vector2(cardW * i + cardW * 0.5f, 0f));
                var cardButton = card.gameObject.AddComponent<Button>();
                cardButton.onClick.AddListener(() => screen.OnSpeciesTapped(speciesId));

                Text emoji = UiKit.Label(card, "emoji", species.Emoji, 110, Color.white);
                UiKit.Place(emoji.rectTransform, new Vector2(0.5f, 0.66f), new Vector2(200f, 150f), Vector2.zero);
                Text name = UiKit.Label(card, "name", species.Name, 38, UiKit.TextColor);
                UiKit.Place(name.rectTransform, new Vector2(0.5f, 0.33f), new Vector2(260f, 60f), Vector2.zero);
                Text count = UiKit.Label(card, "count", "", 30, UiKit.TextDim);
                UiKit.Place(count.rectTransform, new Vector2(0.5f, 0.15f), new Vector2(260f, 50f), Vector2.zero);
                screen.speciesCards.Add((speciesId, count));
            }

            dioramaContent.sizeDelta = new Vector2(cardW * flow.Roster.Count + 20f, 0f);

            screen.flavorLine = UiKit.Label(root, "flavor", "", 34, Palette.MeterFilled);
            UiKit.Place(screen.flavorLine.rectTransform, new Vector2(0.5f, 0.555f), new Vector2(1000f, 60f), Vector2.zero);

            Text decoTitle = UiKit.Label(root, "decoTitle", flow.Strings.Get("tidepool.decorations"), 44, UiKit.TextColor);
            UiKit.Place(decoTitle.rectTransform, new Vector2(0.5f, 0.50f), new Vector2(600f, 70f), Vector2.zero);

            RectTransform shopRoot = UiKit.Panel(root, "shop", new Color(0f, 0f, 0f, 0.25f));
            shopRoot.anchorMin = new Vector2(0.02f, 0.085f);
            shopRoot.anchorMax = new Vector2(0.98f, 0.475f);
            shopRoot.offsetMin = Vector2.zero;
            shopRoot.offsetMax = Vector2.zero;
            var shopScroll = shopRoot.gameObject.AddComponent<ScrollRect>();
            shopRoot.gameObject.AddComponent<RectMask2D>();
            RectTransform shopContent = UiKit.Container(shopRoot, "content");
            shopContent.anchorMin = new Vector2(0f, 1f);
            shopContent.anchorMax = new Vector2(1f, 1f);
            shopContent.pivot = new Vector2(0.5f, 1f);
            shopScroll.content = shopContent;
            shopScroll.horizontal = false;
            shopScroll.vertical = true;

            const float rowH = 110f;
            var decorations = flow.Decorations;
            for (int i = 0; i < decorations.Count; i++)
            {
                Decoration deco = decorations[i];
                Text label = UiKit.Label(shopContent, $"deco_{deco.Id}", $"{deco.Emoji} {deco.Name}", 34,
                    UiKit.TextColor, TextAnchor.MiddleLeft);
                UiKit.Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(620f, rowH - 14f),
                    new Vector2(330f, -rowH * i - rowH * 0.5f));

                Decoration decoCopy = deco;
                Button buy = UiKit.TextButton(shopContent, $"buy_{deco.Id}", "", 30, () => screen.OnBuy(decoCopy));
                UiKit.Place((RectTransform)buy.transform, new Vector2(1f, 1f), new Vector2(280f, rowH - 26f),
                    new Vector2(-170f, -rowH * i - rowH * 0.5f));
                screen.decoButtons.Add((deco, buy, buy.GetComponentInChildren<Text>()));
            }

            shopContent.sizeDelta = new Vector2(0f, rowH * decorations.Count + 20f);

            Button back = UiKit.TextButton(root, "back", flow.Strings.Get("common.back"), 38,
                () => flow.GoTo(FlowScreen.Home));
            UiKit.Place((RectTransform)back.transform, new Vector2(0.5f, 0.04f), new Vector2(380f, 80f), Vector2.zero);

            screen.Refresh();
            return root;
        }

        private void OnSpeciesTapped(int speciesId)
        {
            CreatureSpecies species = flow.Roster.Species[speciesId];
            if (flow.Meta.Save.RescuesFor(speciesId) <= 0)
            {
                flavorLine.text = flow.Strings.Get("tidepool.never");
                return;
            }

            flavorCursor = (flavorCursor + 1) % species.Flavor.Count;
            flavorLine.text = $"{species.Emoji} {species.Flavor[flavorCursor]}";
        }

        private void OnBuy(Decoration deco)
        {
            if (flow.TryBuyDecoration(deco))
            {
                Refresh();
            }
        }

        public void Refresh()
        {
            coins.text = string.Format(flow.Strings.Get("hud.coins"), ShareCard.GroupThousands(flow.Meta.Coins));
            flavorLine.text = "";

            foreach ((int id, Text count) in speciesCards)
            {
                int rescues = flow.Meta.Save.RescuesFor(id);
                count.text = rescues > 0
                    ? string.Format(flow.Strings.Get("tidepool.rescued"), rescues)
                    : flow.Strings.Get("tidepool.never");
            }

            foreach ((Decoration deco, Button button, Text label) in decoButtons)
            {
                bool owned = flow.Meta.OwnsDecoration(deco.Id);
                label.text = owned
                    ? flow.Strings.Get("tidepool.owned")
                    : string.Format(flow.Strings.Get("tidepool.buy"), deco.Cost);
                button.interactable = !owned && flow.Meta.CanAfford(deco.Cost);
            }
        }
    }
}
