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
}
