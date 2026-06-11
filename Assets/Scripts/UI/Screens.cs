using Riptide.Core;
using Riptide.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>Home (GDD 9): Voyage continue, Endless, Daily + streak flame, Tidepool, settings gear.</summary>
    public sealed class HomeScreen : MonoBehaviour, IScreenRefresh
    {
        private GameFlow flow = null!;
        private Text voyageLabel = null!;
        private Text streakLabel = null!;

        public static RectTransform Build(RectTransform parent, GameFlow flow)
        {
            RectTransform root = UiKit.Panel(parent, "HomeScreen", UiKit.PanelColor);
            UiKit.Stretch(root);
            var screen = root.gameObject.AddComponent<HomeScreen>();
            screen.flow = flow;

            Text title = UiKit.Label(root, "title", flow.Strings.Get("app.title"), 110, UiKit.TextColor);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 0.86f), new Vector2(900f, 140f), Vector2.zero);

            Button voyage = UiKit.TextButton(root, "voyage", flow.Strings.Get("home.voyage"), 52,
                () => flow.GoTo(FlowScreen.ZoneMap), UiKit.ButtonAccent);
            UiKit.Place((RectTransform)voyage.transform, new Vector2(0.5f, 0.62f), new Vector2(640f, 130f), Vector2.zero);
            screen.voyageLabel = voyage.GetComponentInChildren<Text>();

            Button endless = UiKit.TextButton(root, "endless", flow.Strings.Get("home.endless"), 52,
                flow.StartEndless);
            UiKit.Place((RectTransform)endless.transform, new Vector2(0.5f, 0.50f), new Vector2(640f, 130f), Vector2.zero);

            Button daily = UiKit.TextButton(root, "daily", flow.Strings.Get("home.daily"), 52,
                () => flow.StartDaily());
            UiKit.Place((RectTransform)daily.transform, new Vector2(0.5f, 0.38f), new Vector2(640f, 130f), Vector2.zero);

            screen.streakLabel = UiKit.Label(root, "streak", "", 40, Palette.MeterDanger);
            UiKit.Place(screen.streakLabel.rectTransform, new Vector2(0.5f, 0.315f), new Vector2(640f, 60f), Vector2.zero);

            Button tidepool = UiKit.TextButton(root, "tidepool", flow.Strings.Get("home.tidepool"), 44,
                () => flow.GoTo(FlowScreen.Tidepool));
            UiKit.Place((RectTransform)tidepool.transform, new Vector2(0.5f, 0.22f), new Vector2(640f, 110f), Vector2.zero);

            Button settings = UiKit.TextButton(root, "settings", flow.Strings.Get("home.settings"), 36,
                () => flow.GoTo(FlowScreen.Settings));
            UiKit.Place((RectTransform)settings.transform, new Vector2(0.5f, 0.10f), new Vector2(400f, 90f), Vector2.zero);

            // GDD 6: rewarded coin chest, capped 3/day by the save.
            Button chest = UiKit.TextButton(root, "chest", flow.Strings.Get("home.chest"), 32,
                () => { flow.TryClaimChestViaAd(); screen.Refresh(); });
            UiKit.Place((RectTransform)chest.transform, new Vector2(0.85f, 0.10f), new Vector2(260f, 90f), Vector2.zero);

            screen.Refresh();
            return root;
        }

        public void Refresh()
        {
            (int zone, int index) = flow.Meta.Voyage.NextLevel();
            voyageLabel.text = flow.Meta.Voyage.TotalStars == 0
                ? flow.Strings.Get("home.voyage")
                : string.Format(flow.Strings.Get("home.voyageContinue"), zone, index);

            int streak = flow.Meta.Streak.Current;
            streakLabel.text = streak > 0 ? string.Format(flow.Strings.Get("home.dailyStreak"), streak) : "";
        }
    }

    /// <summary>Zone map (GDD 9): vertical scroll, 10 zones x 20 nodes with stars and locks.</summary>
    public sealed class ZoneMapScreen : MonoBehaviour, IScreenRefresh
    {
        private GameFlow flow = null!;
        private readonly System.Collections.Generic.List<(int zone, int index, Button button, Text label)> nodes
            = new System.Collections.Generic.List<(int, int, Button, Text)>();

        public static RectTransform Build(RectTransform parent, GameFlow flow)
        {
            RectTransform root = UiKit.Panel(parent, "ZoneMapScreen", UiKit.PanelColor);
            UiKit.Stretch(root);
            var screen = root.gameObject.AddComponent<ZoneMapScreen>();
            screen.flow = flow;

            Button back = UiKit.TextButton(root, "back", flow.Strings.Get("common.back"), 36,
                () => flow.GoTo(FlowScreen.Home));
            UiKit.Place((RectTransform)back.transform, new Vector2(0.12f, 0.955f), new Vector2(180f, 70f), Vector2.zero);

            RectTransform scrollRoot = UiKit.Panel(root, "scroll", new Color(0f, 0f, 0f, 0.25f));
            scrollRoot.anchorMin = new Vector2(0.04f, 0.02f);
            scrollRoot.anchorMax = new Vector2(0.96f, 0.92f);
            scrollRoot.offsetMin = Vector2.zero;
            scrollRoot.offsetMax = Vector2.zero;
            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scrollRoot.gameObject.AddComponent<RectMask2D>();

            RectTransform content = UiKit.Container(scrollRoot, "content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;

            const float headerH = 90f;
            const float cell = 180f;
            const float pad = 14f;
            float y = 0f;
            for (int zone = 1; zone <= 10; zone++)
            {
                Text header = UiKit.Label(content, $"zone{zone}",
                    string.Format(flow.Strings.Get("zone.title"), zone), 48, UiKit.TextColor);
                UiKit.Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(600f, headerH),
                    new Vector2(0f, -y - headerH * 0.5f));
                y += headerH;

                for (int index = 1; index <= 20; index++)
                {
                    int col = (index - 1) % 5;
                    int row = (index - 1) / 5;
                    int zoneCopy = zone;
                    int indexCopy = index;
                    Button node = UiKit.TextButton(content, $"z{zone}l{index}", index.ToString(), 40,
                        () => screen.OnNode(zoneCopy, indexCopy));
                    UiKit.Place((RectTransform)node.transform, new Vector2(0.5f, 1f),
                        new Vector2(cell - pad, cell - pad),
                        new Vector2((col - 2) * cell, -y - row * cell - cell * 0.5f));
                    screen.nodes.Add((zone, index, node, node.GetComponentInChildren<Text>()));
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
            foreach ((int zone, int index, Button button, Text label) in nodes)
            {
                bool unlocked = flow.Meta.Voyage.IsUnlocked(zone, index);
                int stars = flow.Meta.Voyage.StarsFor(VoyageProgress.LevelId(zone, index));
                button.interactable = unlocked;
                label.text = unlocked
                    ? (stars > 0 ? $"{index}\n{new string('★', stars)}" : index.ToString())
                    : "🔒";
                label.color = unlocked ? UiKit.TextColor : UiKit.TextDim;
            }
        }
    }

    /// <summary>Settings (GDD 9): audio/haptics toggles; consent, restore, policy links stubbed for P7/P8.</summary>
    public sealed class SettingsScreen : MonoBehaviour, IScreenRefresh
    {
        private GameFlow flow = null!;
        private Text audioLabel = null!;
        private Text hapticsLabel = null!;

        public static RectTransform Build(RectTransform parent, GameFlow flow)
        {
            RectTransform root = UiKit.Panel(parent, "SettingsScreen", UiKit.PanelColor);
            UiKit.Stretch(root);
            var screen = root.gameObject.AddComponent<SettingsScreen>();
            screen.flow = flow;

            Text title = UiKit.Label(root, "title", flow.Strings.Get("settings.title"), 64, UiKit.TextColor);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 0.9f), new Vector2(700f, 100f), Vector2.zero);

            Button audio = UiKit.TextButton(root, "audio", "", 44, () => screen.Toggle("settings.audio.on"));
            UiKit.Place((RectTransform)audio.transform, new Vector2(0.5f, 0.72f), new Vector2(640f, 110f), Vector2.zero);
            screen.audioLabel = audio.GetComponentInChildren<Text>();

            Button haptics = UiKit.TextButton(root, "haptics", "", 44, () => screen.Toggle("settings.haptics.on"));
            UiKit.Place((RectTransform)haptics.transform, new Vector2(0.5f, 0.60f), new Vector2(640f, 110f), Vector2.zero);
            screen.hapticsLabel = haptics.GetComponentInChildren<Text>();

            Button consent = UiKit.TextButton(root, "consent", flow.Strings.Get("settings.consent"), 38,
                () => flow.Consent?.Reopen());
            UiKit.Place((RectTransform)consent.transform, new Vector2(0.5f, 0.48f), new Vector2(640f, 100f), Vector2.zero);

            Button restore = UiKit.TextButton(root, "restore", flow.Strings.Get("settings.restore"), 38,
                () => flow.Iap?.Restore());
            UiKit.Place((RectTransform)restore.transform, new Vector2(0.5f, 0.37f), new Vector2(640f, 100f), Vector2.zero);

            Button privacy = UiKit.TextButton(root, "privacy", flow.Strings.Get("settings.privacy"), 38,
                () => Application.OpenURL("https://riptide.game/privacy"));
            UiKit.Place((RectTransform)privacy.transform, new Vector2(0.5f, 0.26f), new Vector2(640f, 100f), Vector2.zero);

            Button terms = UiKit.TextButton(root, "terms", flow.Strings.Get("settings.terms"), 38,
                () => Application.OpenURL("https://riptide.game/terms"));
            UiKit.Place((RectTransform)terms.transform, new Vector2(0.5f, 0.15f), new Vector2(640f, 100f), Vector2.zero);

            Button back = UiKit.TextButton(root, "back", flow.Strings.Get("common.back"), 40,
                () => flow.GoTo(FlowScreen.Home));
            UiKit.Place((RectTransform)back.transform, new Vector2(0.5f, 0.05f), new Vector2(400f, 90f), Vector2.zero);

            screen.Refresh();
            return root;
        }

        private void Toggle(string key)
        {
            PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key, 1) == 1 ? 0 : 1);
            PlayerPrefs.Save();
            Refresh();
        }

        public void Refresh()
        {
            audioLabel.text = $"{flow.Strings.Get("settings.audio")}: {OnOff("settings.audio.on")}";
            hapticsLabel.text = $"{flow.Strings.Get("settings.haptics")}: {OnOff("settings.haptics.on")}";
        }

        private static string OnOff(string key) => PlayerPrefs.GetInt(key, 1) == 1 ? "ON" : "OFF";
    }

    /// <summary>Shop sheet (GDD 9) — modal stub until Phase 7 wires IAP.</summary>
    public static class ShopSheet
    {
        public static RectTransform Build(RectTransform parent, GameFlow flow)
        {
            RectTransform root = UiKit.Panel(parent, "ShopSheet", new Color(0f, 0f, 0f, 0.6f));
            UiKit.Stretch(root);

            RectTransform sheet = UiKit.Panel(root, "sheet", UiKit.PanelColor);
            UiKit.Place(sheet, new Vector2(0.5f, 0.5f), new Vector2(820f, 700f), Vector2.zero);

            Text title = UiKit.Label(sheet, "title", flow.Strings.Get("shop.title"), 56, UiKit.TextColor);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 0.85f), new Vector2(700f, 90f), Vector2.zero);

            Button removeAds = UiKit.TextButton(sheet, "removeAds", flow.Strings.Get("shop.removeAds"), 42, () => { });
            removeAds.interactable = false;
            UiKit.Place((RectTransform)removeAds.transform, new Vector2(0.5f, 0.6f), new Vector2(680f, 110f), Vector2.zero);

            Text soon = UiKit.Label(sheet, "soon", flow.Strings.Get("shop.comingSoon"), 34, UiKit.TextDim);
            UiKit.Place(soon.rectTransform, new Vector2(0.5f, 0.4f), new Vector2(700f, 120f), Vector2.zero);

            Button close = UiKit.TextButton(sheet, "close", flow.Strings.Get("common.close"), 40,
                () => flow.GoTo(FlowScreen.Home));
            UiKit.Place((RectTransform)close.transform, new Vector2(0.5f, 0.14f), new Vector2(360f, 90f), Vector2.zero);

            return root;
        }
    }

    /// <summary>
    /// GDD 5.1 Tidepool: horizontally scrolling diorama of rescued species with
    /// lifetime counters and tap-for-flavor, plus the 20-item decoration coin sink.
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
