using System;
using System.Collections.Generic;
using Riptide.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// Spec §4.7 settings, Tidal Pop edition: a "your ocean" stats card with
    /// stickered stat tiles, a preferences card with chunky switches, an about
    /// card with chevron rows. Scrolls on short phones; sits above the tab bar.
    /// </summary>
    public sealed class SettingsScreen : MonoBehaviour, IScreenRefresh
    {
        private const float CardWidth = 980f;
        private const float RowHeight = 124f;

        private readonly List<(string key, PopSurface track, RectTransform knob)> toggles
            = new List<(string, PopSurface, RectTransform)>();
        private GameFlow flow = null!;
        private CoinCounter? coins;

        public static RectTransform Build(RectTransform parent, GameFlow flow)
        {
            RectTransform root = ScreenChrome.Root(parent, "SettingsScreen");
            var screen = root.gameObject.AddComponent<SettingsScreen>();
            screen.flow = flow;
            screen.coins = ScreenChrome.Header(root, flow, flow.Strings.Get("settings.title"), null, coins: true);
            ScreenChrome.Column column = ScreenChrome.ScrollColumn(root, ScreenChrome.TabFloorRefPx - 40f);

            // ROADMAP M5: identity stats.
            RectTransform stats = column.Add(UiComponents.Card(column.Content, "stats", new Vector2(CardWidth, 330f)), 330f);
            TextMeshProUGUI statsHeader = UiText.Create(stats, "header", flow.Strings.Get("stats.title"),
                "micro", "text.inkSoft");
            UiComponents.Place(statsHeader.rectTransform, new Vector2(0.5f, 0.86f), new Vector2(700f, 40f));
            long rescued = 0;
            foreach (int count in flow.Meta.Save.SpeciesRescues)
            {
                rescued += count;
            }

            StatTile(stats, 0, "fish", PopTone.Yellow, Riptide.Core.ShareCard.GroupThousands(rescued),
                flow.Strings.Get("stats.rescued"));
            StatTile(stats, 1, "star", PopTone.Yellow, flow.Meta.Voyage.TotalStars.ToString(),
                flow.Strings.Get("stats.stars"));
            StatTile(stats, 2, "crown", PopTone.Purple,
                Riptide.Core.ShareCard.GroupThousands(flow.Meta.EndlessBest), flow.Strings.Get("stats.best"));
            StatTile(stats, 3, "flame", PopTone.Coral, flow.Meta.Streak.Best.ToString(),
                flow.Strings.Get("stats.streak"));
            StatTile(stats, 4, "chest", PopTone.Green, flow.Meta.Save.DecorationsOwned.Count.ToString(),
                flow.Strings.Get("stats.decor"));

            // Preferences with chunky switches.
            float prefsHeight = 4 * RowHeight + 60f;
            RectTransform prefs = column.Add(UiComponents.Card(column.Content, "prefs",
                new Vector2(CardWidth, prefsHeight)), prefsHeight);
            screen.ToggleRow(prefs, 0, "settings.audio.on", "sound", PopTone.Blue, flow.Strings.Get("settings.audio"));
            screen.ToggleRow(prefs, 1, "settings.music.on", "music", PopTone.Purple, flow.Strings.Get("settings.music"));
            screen.ToggleRow(prefs, 2, "settings.haptics.on", "vibrate", PopTone.Coral,
                flow.Strings.Get("settings.haptics"));
            screen.ToggleRow(prefs, 3, "settings.reducedMotion.on", "motion", PopTone.Green,
                flow.Strings.Get("settings.reducedMotion"));

            float aboutHeight = 4 * RowHeight + 60f;
            RectTransform about = column.Add(UiComponents.Card(column.Content, "about",
                new Vector2(CardWidth, aboutHeight)), aboutHeight);
            LinkRow(about, 0, "shield", flow.Strings.Get("settings.consent"), () => flow.Consent?.Reopen());
            LinkRow(about, 1, "reroll", flow.Strings.Get("settings.restore"), () => flow.Iap?.Restore());
            LinkRow(about, 2, "doc", flow.Strings.Get("settings.privacy"),
                () => Application.OpenURL("https://riptide.game/privacy"));
            LinkRow(about, 3, "doc", flow.Strings.Get("settings.terms"),
                () => Application.OpenURL("https://riptide.game/terms"));

            TextMeshProUGUI version = UiText.CreateOutlined(column.Content, "version",
                string.Format(flow.Strings.Get("settings.version"), Application.version),
                "micro", "text.secondary", "panel.edge", 0.2f);
            column.Add(version.rectTransform, 50f, 20f);
            version.rectTransform.sizeDelta = new Vector2(600f, 50f);

            screen.Refresh();
            return root;
        }

        private static void StatTile(RectTransform card, int index, string icon, PopTone tone, string value,
            string label)
        {
            float x = 0.1f + index * 0.2f;
            RectTransform badge = PopKit.Panel(card, $"statBadge{index}", new Vector2(104f, 104f), tone, 52f, 7f);
            UiComponents.Place(badge, new Vector2(x, 0.56f), new Vector2(104f, 104f));
            PopKit.Icon(badge, "icon", icon, "text.primary", 56f, PopKit.Edge(tone), 7);

            TextMeshProUGUI valueText = UiText.Create(card, $"statValue{index}", value, "heading", "text.ink");
            UiComponents.Place(valueText.rectTransform, new Vector2(x, 0.28f), new Vector2(190f, 56f));
            TextMeshProUGUI labelText = UiText.Create(card, $"statLabel{index}", label, "micro", "text.inkSoft");
            UiComponents.Place(labelText.rectTransform, new Vector2(x, 0.12f), new Vector2(190f, 34f));
        }

        private void ToggleRow(RectTransform card, int index, string key, string icon, PopTone tone, string label)
        {
            RectTransform row = GroupRow(card, index, key, icon, tone, label, () => Toggle(key));

            RectTransform track = UiComponents.Rect(row, "track", new Vector2(150f, 84f));
            track.anchorMin = track.anchorMax = new Vector2(1f, 0.5f);
            track.anchoredPosition = new Vector2(-110f, 0f);
            PopSurface surface = PopKit.Surface(track, PopTone.Green, 42f, 6f, lip: 0f);

            RectTransform knob = UiComponents.Rect(track, "knob", new Vector2(70f, 70f));
            knob.anchorMin = knob.anchorMax = new Vector2(0.5f, 0.5f);
            Image rim = PopKit.Img(knob, "rim", PopSprites.Circle(), "panel.edge");
            UiComponents.Stretch(rim.rectTransform);
            Image face = PopKit.Img(knob, "face", PopSprites.Circle(), "panel.light");
            UiComponents.Stretch(face.rectTransform);
            face.rectTransform.offsetMin = new Vector2(6f, 6f);
            face.rectTransform.offsetMax = new Vector2(-6f, -6f);
            toggles.Add((key, surface, knob));
        }

        private static void LinkRow(RectTransform card, int index, string icon, string label, Action onClick)
        {
            RectTransform row = GroupRow(card, index, label, icon, PopTone.Navy, label, onClick);
            Image chevron = PopKit.Icon(row, "chevron", "chevronRight", "text.inkSoft", 40f);
            var holder = (RectTransform)chevron.transform.parent;
            holder.anchorMin = holder.anchorMax = new Vector2(1f, 0.5f);
            holder.anchoredPosition = new Vector2(-70f, 0f);
        }

        /// <summary>A grouped-card row: full-width tap target, icon badge, label, divider.</summary>
        private static RectTransform GroupRow(RectTransform card, int index, string name, string icon, PopTone tone,
            string label, Action onClick)
        {
            RectTransform row = UiComponents.Rect(card, name, new Vector2(0f, RowHeight));
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(20f, -30f - RowHeight * (index + 1));
            row.offsetMax = new Vector2(-20f, -30f - RowHeight * index);
            var bg = row.gameObject.AddComponent<Image>();
            bg.sprite = SpriteFactory.Solid();
            bg.color = new Color(0f, 0f, 0f, 0f); // tap target only
            UiComponents.PadHitTarget(row); // 120 ref-px touch floor (§3)
            var button = row.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClick());
            row.gameObject.AddComponent<PressEffect>();

            RectTransform badge = PopKit.Panel(row, "badge", new Vector2(80f, 80f), tone, 22f, 6f);
            badge.anchorMin = badge.anchorMax = new Vector2(0f, 0.5f);
            badge.anchoredPosition = new Vector2(66f, 0f);
            PopKit.Icon(badge, "icon", icon, "text.primary", 46f);

            TextMeshProUGUI text = UiText.Create(row, "label", label, "body", "text.ink",
                TextAlignmentOptions.MidlineLeft);
            text.rectTransform.anchorMin = new Vector2(0f, 0f);
            text.rectTransform.anchorMax = new Vector2(1f, 1f);
            text.rectTransform.offsetMin = new Vector2(132f, 0f);
            text.rectTransform.offsetMax = new Vector2(-200f, 0f);

            if (index < 3)
            {
                Image line = PopKit.Well(row, "divider", "panel.shade", 3f);
                line.rectTransform.anchorMin = new Vector2(0f, 0f);
                line.rectTransform.anchorMax = new Vector2(1f, 0f);
                line.rectTransform.offsetMin = new Vector2(24f, -3f);
                line.rectTransform.offsetMax = new Vector2(-24f, 3f);
            }

            return row;
        }

        private void Toggle(string key)
        {
            PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key, key == "settings.reducedMotion.on" ? 0 : 1) == 1 ? 0 : 1);
            PlayerPrefs.Save();
            Refresh();
        }

        public void Refresh()
        {
            foreach ((string key, PopSurface track, RectTransform knob) in toggles)
            {
                int fallback = key == "settings.reducedMotion.on" ? 0 : 1;
                bool on = PlayerPrefs.GetInt(key, fallback) == 1;
                track.SetTone(on ? PopTone.Green : PopTone.Locked);
                knob.anchoredPosition = new Vector2(on ? 34f : -34f, 0f);
            }

            coins?.SetInstant(flow.Meta.Coins);
        }
    }

    /// <summary>
    /// Spec §4.7 shop, Tidal Pop edition: the Remove Ads hero card (badge, benefit
    /// ticks, price on the green button) above three coin pack cards with stacked
    /// coin art and price capsules. Packs stay disabled with a caption until the
    /// IAP SDK lands (§6.2); Remove Ads is fully wired through the seam.
    /// </summary>
    public sealed class ShopScreen : MonoBehaviour, IScreenRefresh
    {
        private GameFlow flow = null!;
        private ScreenManager manager = null!;
        private Button buy = null!;
        private CoinCounter? coinPill;

        public static RectTransform Build(RectTransform parent, GameFlow flow, ScreenManager manager)
        {
            RectTransform root = ScreenChrome.Root(parent, "ShopScreen");
            var screen = root.gameObject.AddComponent<ShopScreen>();
            screen.flow = flow;
            screen.manager = manager;
            screen.coinPill = ScreenChrome.Header(root, flow, flow.Strings.Get("shop.title"), null, coins: true,
                coinPlus: false);
            ScreenChrome.Column column = ScreenChrome.ScrollColumn(root, ScreenChrome.TabFloorRefPx - 40f);

            // HERO: the featured offer up top; the PRICE lives on the button.
            column.Space(70f);
            RectTransform hero = column.Add(UiComponents.Card(column.Content, "hero", new Vector2(980f, 560f)), 560f, 70f);
            RectTransform ribbon = PopKit.Ribbon(hero, "ribbon", flow.Strings.Get("shop.removeAds"), PopTone.Coral,
                new Vector2(600f, 110f), "title");
            ribbon.anchorMin = ribbon.anchorMax = new Vector2(0.5f, 1f);
            ribbon.anchoredPosition = new Vector2(0f, 10f);

            RectTransform badge = PopKit.Panel(hero, "badge", new Vector2(190f, 190f), PopTone.Coral, 95f, 9f);
            badge.anchorMin = badge.anchorMax = new Vector2(0f, 1f);
            badge.anchoredPosition = new Vector2(170f, -210f);
            PopKit.Icon(badge, "noAds", "noAds", "text.primary", 120f, "pop.coral.edge", 9);

            Benefit(hero, flow.Strings.Get("shop.benefit1"), -170f);
            Benefit(hero, flow.Strings.Get("shop.benefit2"), -260f);

            screen.buy = PopKit.Button(hero, "buy", flow.Strings.Get("shop.price.removeAds"), PopTone.Green,
                screen.OnBuyRemoveAds, new Vector2(820f, 170f), "title");
            var buyRt = (RectTransform)screen.buy.transform;
            buyRt.anchorMin = buyRt.anchorMax = new Vector2(0.5f, 0f);
            buyRt.pivot = new Vector2(0.5f, 0f);
            buyRt.anchoredPosition = new Vector2(0f, 56f);

            // COIN PACKS: scaling coin piles, big amounts, price capsules.
            RectTransform packs = column.Add(UiComponents.Rect(column.Content, "packs", new Vector2(1000f, 470f)), 470f, 24f);
            string[] packKeys = { "shop.pack.small", "shop.pack.medium", "shop.pack.large" };
            string[] amountKeys = { "shop.pack.smallAmount", "shop.pack.mediumAmount", "shop.pack.largeAmount" };
            string[] priceKeys = { "shop.price.small", "shop.price.medium", "shop.price.large" };
            string[] iconIds = { "coins1", "coins2", "coins3" };
            for (int i = 0; i < 3; i++)
            {
                RectTransform pack = UiComponents.Card(packs, $"pack{i}", new Vector2(316f, 450f));
                pack.anchorMin = pack.anchorMax = new Vector2((i + 0.5f) / 3f, 1f);
                pack.pivot = new Vector2(0.5f, 1f);
                pack.anchoredPosition = Vector2.zero;

                Image icon = PopKit.Icon(pack, "icon", iconIds[i], "coin", 130f + i * 14f, "pop.yellow.edge", 8);
                UiComponents.Place((RectTransform)icon.transform.parent, new Vector2(0.5f, 0.72f),
                    new Vector2(130f + i * 14f, 130f + i * 14f));
                TextMeshProUGUI amount = UiText.Create(pack, "amount", flow.Strings.Get(amountKeys[i]), "title",
                    "text.ink");
                UiComponents.Place(amount.rectTransform, new Vector2(0.5f, 0.45f), new Vector2(300f, 70f));
                TextMeshProUGUI packName = UiText.Create(pack, "name", flow.Strings.Get(packKeys[i]), "caption",
                    "text.inkSoft");
                UiComponents.Place(packName.rectTransform, new Vector2(0.5f, 0.33f), new Vector2(300f, 44f));

                // Price capsule: disabled until the IAP SDK lands.
                Button price = PopKit.Button(pack, "price", flow.Strings.Get(priceKeys[i]), PopTone.Blue, () => { },
                    new Vector2(250f, 110f), "body");
                UiComponents.Place((RectTransform)price.transform, new Vector2(0.5f, 0.15f), new Vector2(250f, 110f));
                price.interactable = false;

                if (i == 2)
                {
                    RectTransform best = PopKit.Panel(pack, "bestValue", new Vector2(250f, 64f), PopTone.Coral, 20f, 7f);
                    best.anchorMin = best.anchorMax = new Vector2(0.5f, 1f);
                    best.anchoredPosition = new Vector2(0f, 6f);
                    best.localRotation = Quaternion.Euler(0f, 0f, -4f);
                    TextMeshProUGUI bestText = UiText.CreateOutlined(best, "label", flow.Strings.Get("shop.bestValue"),
                        "micro", "text.primary", "pop.coral.edge");
                    UiComponents.Stretch(bestText.rectTransform);
                }
            }

            TextMeshProUGUI soon = UiText.CreateOutlined(column.Content, "soon", flow.Strings.Get("shop.comingSoon"),
                "caption", "text.secondary", "panel.edge", 0.2f);
            column.Add(soon.rectTransform, 50f, 20f);
            soon.rectTransform.sizeDelta = new Vector2(940f, 50f);

            screen.Refresh();
            return root;
        }

        private static void Benefit(RectTransform hero, string text, float y)
        {
            Image tick = PopKit.Icon(hero, "tick", "check", "pop.green.top", 50f, "pop.green.edge", 7);
            var tickRt = (RectTransform)tick.transform.parent;
            tickRt.anchorMin = tickRt.anchorMax = new Vector2(0f, 1f);
            tickRt.anchoredPosition = new Vector2(330f, y);
            // The bullet glyph in the copy is redundant beside the tick.
            TextMeshProUGUI label = UiText.Create(hero, "benefit", text.TrimStart('•', ' '), "body", "text.ink",
                TextAlignmentOptions.MidlineLeft);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0f, 1f);
            label.rectTransform.pivot = new Vector2(0f, 0.5f);
            label.rectTransform.sizeDelta = new Vector2(600f, 60f);
            label.rectTransform.anchoredPosition = new Vector2(372f, y);
        }

        private void OnBuyRemoveAds()
        {
            flow.Iap?.PurchaseRemoveAds(success =>
            {
                manager.Toasts.Show(flow.Strings.Get(success ? "shop.thanks" : "errors.purchase_failed"));
                Refresh();
            });
        }

        public void Refresh()
        {
            bool owned = flow.Iap != null && flow.Iap.RemoveAdsOwned;
            buy.interactable = !owned;
            PopKit.FaceOf(buy).Find("label").GetComponent<TextMeshProUGUI>().text =
                flow.Strings.Get(owned ? "shop.owned" : "shop.price.removeAds");
            coinPill?.SetInstant(flow.Meta.Coins);
        }
    }
}
