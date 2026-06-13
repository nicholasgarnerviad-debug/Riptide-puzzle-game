using System;
using Riptide.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// Spec §4.7 settings, modern pass: a stats card with icon tiles, grouped
    /// section cards (preferences with REAL switches; about with chevron rows).
    /// </summary>
    public sealed class SettingsScreen : MonoBehaviour, IScreenRefresh
    {
        private readonly System.Collections.Generic.List<(string key, Image track, RectTransform knob)> toggles
            = new System.Collections.Generic.List<(string, Image, RectTransform)>();

        public static RectTransform Build(RectTransform parent, GameFlow flow)
        {
            RectTransform root = ScreenChrome.Root(parent, "SettingsScreen");
            var screen = root.gameObject.AddComponent<SettingsScreen>();

            TextMeshProUGUI title = UiText.Create(root, "title", flow.Strings.Get("settings.title"),
                "title", "text.primary");
            UiComponents.Place(title.rectTransform, new Vector2(0.5f, 0.945f), new Vector2(700f, 90f));

            // ROADMAP M5: identity stats — now an icon-tile card, not microtext.
            RectTransform statsCard = UiComponents.Card(root, "stats", new Vector2(960f, 280f));
            UiComponents.Place(statsCard, new Vector2(0.5f, 0.825f), new Vector2(960f, 280f));
            TextMeshProUGUI statsHeader = UiText.Create(statsCard, "header",
                flow.Strings.Get("stats.title"), "micro", "text.muted");
            UiComponents.Place(statsHeader.rectTransform, new Vector2(0.5f, 0.87f), new Vector2(700f, 38f));
            long rescued = 0;
            foreach (int count in flow.Meta.Save.SpeciesRescues)
            {
                rescued += count;
            }

            StatTile(statsCard, 0, "fish", Riptide.Core.ShareCard.GroupThousands(rescued),
                flow.Strings.Get("stats.rescued"));
            StatTile(statsCard, 1, "compass", flow.Meta.Voyage.TotalStars.ToString(),
                flow.Strings.Get("stats.stars"));
            StatTile(statsCard, 2, "waves", Riptide.Core.ShareCard.GroupThousands(flow.Meta.EndlessBest),
                flow.Strings.Get("stats.best"));
            StatTile(statsCard, 3, "sun", flow.Meta.Streak.Best.ToString(),
                flow.Strings.Get("stats.streak"));
            StatTile(statsCard, 4, "chest", flow.Meta.Save.DecorationsOwned.Count.ToString(),
                flow.Strings.Get("stats.decor"));

            // Preferences: one grouped card, label left, REAL switch right.
            RectTransform prefs = UiComponents.Card(root, "prefs", new Vector2(960f, 480f));
            UiComponents.Place(prefs, new Vector2(0.5f, 0.585f), new Vector2(960f, 480f));
            screen.ToggleRow(prefs, 0, "settings.audio.on", flow.Strings.Get("settings.audio"));
            screen.ToggleRow(prefs, 1, "settings.music.on", flow.Strings.Get("settings.music"));
            screen.ToggleRow(prefs, 2, "settings.haptics.on", flow.Strings.Get("settings.haptics"));
            screen.ToggleRow(prefs, 3, "settings.reducedMotion.on", flow.Strings.Get("settings.reducedMotion"));

            // About: grouped chevron rows.
            RectTransform about = UiComponents.Card(root, "about", new Vector2(960f, 480f));
            UiComponents.Place(about, new Vector2(0.5f, 0.335f), new Vector2(960f, 480f));
            LinkRow(about, 0, flow.Strings.Get("settings.consent"), () => flow.Consent?.Reopen());
            LinkRow(about, 1, flow.Strings.Get("settings.restore"), () => flow.Iap?.Restore());
            LinkRow(about, 2, flow.Strings.Get("settings.privacy"),
                () => Application.OpenURL("https://riptide.game/privacy"));
            LinkRow(about, 3, flow.Strings.Get("settings.terms"),
                () => Application.OpenURL("https://riptide.game/terms"));

            Button back = UiComponents.ButtonGhost(root, "back", flow.Strings.Get("common.back"),
                () => flow.GoTo(FlowScreen.Home));
            UiComponents.Place((RectTransform)back.transform, new Vector2(0.5f, 0.115f), new Vector2(400f, 90f));

            TextMeshProUGUI version = UiText.Create(root, "version",
                string.Format(flow.Strings.Get("settings.version"), Application.version),
                "micro", "text.muted");
            UiComponents.Place(version.rectTransform, new Vector2(0.5f, 0.06f), new Vector2(500f, 40f));

            screen.Refresh();
            return root;
        }

        private static void StatTile(RectTransform card, int index, string icon, string value, string label)
        {
            float x = 0.10f + index * 0.20f;
            RectTransform iconRt = UiComponents.Rect(card, $"statIcon{index}", new Vector2(52f, 52f));
            UiComponents.Place(iconRt, new Vector2(x, 0.60f), new Vector2(52f, 52f));
            var iconImage = iconRt.gameObject.AddComponent<Image>();
            iconImage.sprite = MenuSprites.Icon(icon);
            iconImage.raycastTarget = false;
            ThemedElement.Bind(iconRt.gameObject, "accent.primary");

            TextMeshProUGUI valueText = UiText.Create(card, $"statValue{index}", value, "score", "text.primary");
            UiComponents.Place(valueText.rectTransform, new Vector2(x, 0.36f), new Vector2(180f, 52f));
            TextMeshProUGUI labelText = UiText.Create(card, $"statLabel{index}", label, "micro", "text.muted");
            UiComponents.Place(labelText.rectTransform, new Vector2(x, 0.16f), new Vector2(180f, 34f));
        }

        private void ToggleRow(RectTransform card, int index, string key, string label)
        {
            RectTransform row = GroupRow(card, index, key, label, () => Toggle(key));

            RectTransform track = UiComponents.Rect(row, "track", new Vector2(104f, 56f));
            track.anchorMin = new Vector2(1f, 0.5f);
            track.anchorMax = new Vector2(1f, 0.5f);
            track.anchoredPosition = new Vector2(-92f, 0f);
            var trackImage = UiComponents.RoundedImage(track.gameObject, 28f);
            trackImage.raycastTarget = false;

            RectTransform knob = UiComponents.Rect(track, "knob", new Vector2(42f, 42f));
            knob.anchorMin = new Vector2(0.5f, 0.5f);
            knob.anchorMax = new Vector2(0.5f, 0.5f);
            var knobImage = knob.gameObject.AddComponent<Image>();
            knobImage.sprite = SpriteFactory.Dot();
            knobImage.raycastTarget = false;
            ThemedElement.Bind(knob.gameObject, "text.primary");

            toggles.Add((key, trackImage, knob));
        }

        private static void LinkRow(RectTransform card, int index, string label, Action onClick)
        {
            RectTransform row = GroupRow(card, index, label, label, onClick);
            TextMeshProUGUI chevron = UiText.Create(row, "chevron", "›", "heading", "text.muted");
            UiComponents.Place(chevron.rectTransform, new Vector2(1f, 0.5f), new Vector2(50f, 60f));
            chevron.rectTransform.anchoredPosition = new Vector2(-66f, 0f);
        }

        /// <summary>A grouped-card row: full-width tap target, left label, hairline below.</summary>
        private static RectTransform GroupRow(RectTransform card, int index, string name, string label,
            Action onClick)
        {
            RectTransform row = UiComponents.Rect(card, name, new Vector2(0f, 108f));
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(16f, -22f - 110f * (index + 1));
            row.offsetMax = new Vector2(-16f, -22f - 110f * index);
            var bg = row.gameObject.AddComponent<Image>();
            bg.sprite = SpriteFactory.Solid();
            bg.color = new Color(0f, 0f, 0f, 0f); // tap target only
            UiComponents.PadHitTarget(row); // 120 ref-px touch floor (§3)
            var button = row.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => onClick());
            row.gameObject.AddComponent<PressEffect>();

            TextMeshProUGUI text = UiText.Create(row, "label", label, "body", "text.primary");
            text.alignment = TextAlignmentOptions.Left;
            UiComponents.Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(560f, 60f));
            text.rectTransform.anchoredPosition = new Vector2(330f, 0f);

            if (index < 3)
            {
                RectTransform line = UiComponents.Rect(row, "hairline", Vector2.zero);
                line.anchorMin = new Vector2(0f, 0f);
                line.anchorMax = new Vector2(1f, 0f);
                line.offsetMin = new Vector2(28f, -1f);
                line.offsetMax = new Vector2(-28f, 1f);
                var lineImage = line.gameObject.AddComponent<Image>();
                lineImage.sprite = SpriteFactory.Solid();
                lineImage.raycastTarget = false;
                ThemedElement.Bind(line.gameObject, "stroke.subtle");
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
            foreach ((string key, Image track, RectTransform knob) in toggles)
            {
                int fallback = key == "settings.reducedMotion.on" ? 0 : 1;
                bool on = PlayerPrefs.GetInt(key, fallback) == 1;
                // State-driven switch: cyan track + knob right when on.
                track.color = ThemeRuntime.Color(on ? "accent.deep" : "bg.raised");
                knob.anchoredPosition = new Vector2(on ? 24f : -24f, 0f);
            }
        }
    }

    /// <summary>
    /// Spec §4.7 shop: Remove Ads hero card (price + one-line promise) above three
    /// coin pack cards. Packs stay disabled with a caption until the IAP SDK lands
    /// (§6.2 offline/disabled rule); Remove Ads is fully wired through the seam.
    /// </summary>
    public sealed class ShopScreen : MonoBehaviour, IScreenRefresh
    {
        private GameFlow flow = null!;
        private ScreenManager manager = null!;
        private Button buy = null!;
        private CoinCounter coinPill = null!;

        public static RectTransform Build(RectTransform parent, GameFlow flow, ScreenManager manager)
        {
            RectTransform root = ScreenChrome.Root(parent, "ShopScreen");
            var screen = root.gameObject.AddComponent<ShopScreen>();
            screen.flow = flow;
            screen.manager = manager;

            TextMeshProUGUI title = UiText.Create(root, "title", flow.Strings.Get("shop.title"),
                "title", "text.primary");
            UiComponents.Place(title.rectTransform, new Vector2(0.5f, 0.935f), new Vector2(700f, 100f));

            // Players check their balance in a shop — coin pill, top-right.
            RectTransform coinPill = UiComponents.Rect(root, "coinPill", new Vector2(250f, 78f));
            UiComponents.Place(coinPill, new Vector2(0.84f, 0.935f), new Vector2(250f, 78f));
            Image pillImage = UiComponents.RoundedImage(coinPill.gameObject, 39f);
            pillImage.raycastTarget = false;
            ThemedElement.Bind(coinPill.gameObject, "bg.surface");
            UiComponents.RoundedStrokeImage(coinPill, "stroke.subtle", 39f);
            screen.coinPill = UiComponents.CoinCounterComponent(coinPill);
            UiComponents.Place((RectTransform)screen.coinPill.transform, new Vector2(0.5f, 0.5f), new Vector2(230f, 64f));

            // HERO (research: featured offer up top — icon identity, benefit
            // bullets, the PRICE lives on the button and nowhere else).
            RectTransform hero = UiComponents.Card(root, "hero", new Vector2(960f, 420f));
            UiComponents.Place(hero, new Vector2(0.5f, 0.745f), new Vector2(960f, 420f));
            UiComponents.RoundedStrokeImage(hero, "accent.deep", 32f);

            RectTransform heroBadge = UiComponents.Rect(hero, "badge", new Vector2(140f, 140f));
            UiComponents.Place(heroBadge, new Vector2(0.145f, 0.70f), new Vector2(140f, 140f));
            var heroBadgeBg = heroBadge.gameObject.AddComponent<Image>();
            heroBadgeBg.sprite = SpriteFactory.Dot();
            heroBadgeBg.raycastTarget = false;
            ThemedElement.Bind(heroBadge.gameObject, "bg.raised");
            TextMeshProUGUI adGlyph = UiText.Create(heroBadge, "adText", "AD", "caption", "text.muted");
            UiComponents.Place(adGlyph.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(100f, 50f));
            RectTransform slash = UiComponents.Rect(heroBadge, "slash", new Vector2(96f, 96f));
            UiComponents.Place(slash, new Vector2(0.5f, 0.5f), new Vector2(96f, 96f));
            var slashImage = slash.gameObject.AddComponent<Image>();
            slashImage.sprite = MenuSprites.Icon("noAds");
            slashImage.raycastTarget = false;
            ThemedElement.Bind(slash.gameObject, "danger");

            TextMeshProUGUI heroTitle = UiText.Create(hero, "heroTitle",
                flow.Strings.Get("shop.removeAds"), "title", "text.primary");
            heroTitle.alignment = TextAlignmentOptions.Left;
            UiComponents.Place(heroTitle.rectTransform, new Vector2(0.62f, 0.80f), new Vector2(580f, 80f));
            TextMeshProUGUI benefit1 = UiText.Create(hero, "benefit1",
                flow.Strings.Get("shop.benefit1"), "caption", "text.secondary");
            benefit1.alignment = TextAlignmentOptions.Left;
            UiComponents.Place(benefit1.rectTransform, new Vector2(0.62f, 0.62f), new Vector2(580f, 48f));
            TextMeshProUGUI benefit2 = UiText.Create(hero, "benefit2",
                flow.Strings.Get("shop.benefit2"), "caption", "text.secondary");
            benefit2.alignment = TextAlignmentOptions.Left;
            UiComponents.Place(benefit2.rectTransform, new Vector2(0.62f, 0.50f), new Vector2(580f, 48f));

            screen.buy = UiComponents.ButtonPrimary(hero, "buy",
                flow.Strings.Get("shop.price.removeAds"), screen.OnBuyRemoveAds);
            UiComponents.Place((RectTransform)screen.buy.transform, new Vector2(0.5f, 0.18f), new Vector2(640f, 130f));

            // COIN PACKS (research: scaling coin-pile art, big amounts, price
            // capsules, BEST VALUE badge for hierarchy; one shared caption —
            // the per-card caption was overflowing across its neighbors).
            string[] packKeys = { "shop.pack.small", "shop.pack.medium", "shop.pack.large" };
            string[] amountKeys = { "shop.pack.smallAmount", "shop.pack.mediumAmount", "shop.pack.largeAmount" };
            string[] priceKeys = { "shop.price.small", "shop.price.medium", "shop.price.large" };
            string[] iconIds = { "coins1", "coins2", "coins3" };
            for (int i = 0; i < 3; i++)
            {
                RectTransform pack = UiComponents.Card(root, $"pack{i}", new Vector2(304f, 400f));
                UiComponents.Place(pack, new Vector2(0.185f + 0.315f * i, 0.455f), new Vector2(304f, 400f));

                RectTransform icon = UiComponents.Rect(pack, "icon", new Vector2(96f, 96f));
                UiComponents.Place(icon, new Vector2(0.5f, 0.78f), new Vector2(96f, 96f));
                var iconImage = icon.gameObject.AddComponent<Image>();
                iconImage.sprite = MenuSprites.Icon(iconIds[i]);
                iconImage.raycastTarget = false;
                ThemedElement.Bind(icon.gameObject, "coin");

                TextMeshProUGUI amount = UiText.Create(pack, "amount",
                    flow.Strings.Get(amountKeys[i]), "score", "coin");
                UiComponents.Place(amount.rectTransform, new Vector2(0.5f, 0.52f), new Vector2(280f, 60f));
                TextMeshProUGUI packName = UiText.Create(pack, "name",
                    flow.Strings.Get(packKeys[i]), "caption", "text.secondary");
                UiComponents.Place(packName.rectTransform, new Vector2(0.5f, 0.36f), new Vector2(280f, 44f));

                // Disabled price capsule until the IAP SDK lands.
                RectTransform price = UiComponents.Rect(pack, "price", new Vector2(200f, 76f));
                UiComponents.Place(price, new Vector2(0.5f, 0.14f), new Vector2(200f, 76f));
                var priceImage = price.gameObject.AddComponent<Image>();
                priceImage.sprite = MenuSprites.CapsuleGradient();
                priceImage.type = Image.Type.Sliced;
                priceImage.pixelsPerUnitMultiplier = 22f * (100f / 64f) / 28f;
                priceImage.raycastTarget = false;
                ThemedElement.Bind(price.gameObject, "bg.raised");
                TextMeshProUGUI priceText = UiText.Create(price, "label",
                    flow.Strings.Get(priceKeys[i]), "body", "text.muted");
                UiComponents.Stretch(priceText.rectTransform);

                if (i == 2)
                {
                    RectTransform best = UiComponents.Rect(pack, "bestValue", new Vector2(190f, 52f));
                    UiComponents.Place(best, new Vector2(0.5f, 1.0f), new Vector2(190f, 52f));
                    var bestImage = UiComponents.RoundedImage(best.gameObject, 26f);
                    bestImage.raycastTarget = false;
                    ThemedElement.Bind(best.gameObject, "warning");
                    TextMeshProUGUI bestText = UiText.Create(best, "label",
                        flow.Strings.Get("shop.bestValue"), "micro", "text.onAccent");
                    UiComponents.Stretch(bestText.rectTransform);
                }
            }

            TextMeshProUGUI soon = UiText.Create(root, "soon",
                flow.Strings.Get("shop.comingSoon"), "micro", "text.muted");
            UiComponents.Place(soon.rectTransform, new Vector2(0.5f, 0.30f), new Vector2(900f, 44f));

            Button back = UiComponents.ButtonGhost(root, "back", flow.Strings.Get("common.back"),
                () => flow.GoTo(FlowScreen.Home));
            UiComponents.Place((RectTransform)back.transform, new Vector2(0.5f, 0.08f), new Vector2(400f, 90f));

            screen.Refresh();
            return root;
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
            buy.GetComponentInChildren<TextMeshProUGUI>().text =
                flow.Strings.Get(owned ? "shop.owned" : "shop.price.removeAds");
            coinPill.SetInstant(flow.Meta.Coins);
        }
    }

    /// <summary>
    /// Pause sheet (§4.3 pause slot, §6.2 resume-from-background): resume primary,
    /// quick sound toggle, quit-to-home. Never auto-abandons the run.
    /// </summary>
    public sealed class PauseSheet : MonoBehaviour
    {
        private Sheet sheet = null!;
        private GameFlow flow = null!;
        private Button sound = null!;

        public bool Shown => sheet != null && sheet.Shown;

        public static PauseSheet Build(RectTransform canvasRoot, GameFlow flow)
        {
            Sheet sheet = UiComponents.SheetComponent(canvasRoot, "PauseSheet", 760f);
            var pause = sheet.gameObject.AddComponent<PauseSheet>();
            pause.sheet = sheet;
            pause.flow = flow;

            RectTransform body = sheet.Body;
            TextMeshProUGUI title = UiText.Create(body, "title", flow.Strings.Get("pause.title"),
                "heading", "text.primary");
            UiComponents.Place(title.rectTransform, new Vector2(0.5f, 0.86f), new Vector2(700f, 80f));

            Button resume = UiComponents.ButtonPrimary(body, "resume", flow.Strings.Get("pause.resume"),
                pause.Dismiss);
            UiComponents.Place((RectTransform)resume.transform, new Vector2(0.5f, 0.66f), new Vector2(640f, 124f));

            // Gate feedback: in-game had NO route to the level map — pause now
            // exits to it (same abandon semantics as quit-to-home).
            Button map = UiComponents.ButtonSecondary(body, "map", flow.Strings.Get("pause.map"), () =>
            {
                pause.Dismiss();
                flow.GoTo(FlowScreen.ZoneMap);
            });
            UiComponents.Place((RectTransform)map.transform, new Vector2(0.5f, 0.47f), new Vector2(640f, 110f));

            pause.sound = UiComponents.ButtonSecondary(body, "sound", "", pause.ToggleSound);
            UiComponents.Place((RectTransform)pause.sound.transform, new Vector2(0.5f, 0.30f), new Vector2(640f, 110f));

            Button home = UiComponents.ButtonGhost(body, "home", flow.Strings.Get("pause.home"), () =>
            {
                pause.Dismiss();
                flow.GoTo(FlowScreen.Home);
            });
            UiComponents.Place((RectTransform)home.transform, new Vector2(0.5f, 0.13f), new Vector2(640f, 90f));

            return pause;
        }

        public void Show()
        {
            RefreshSound();
            sheet.Show();
        }

        public void Dismiss() => sheet.Dismiss();

        private void ToggleSound()
        {
            PlayerPrefs.SetInt("settings.audio.on", PlayerPrefs.GetInt("settings.audio.on", 1) == 1 ? 0 : 1);
            PlayerPrefs.Save();
            RefreshSound();
        }

        private void RefreshSound()
        {
            bool on = PlayerPrefs.GetInt("settings.audio.on", 1) == 1;
            sound.GetComponentInChildren<TextMeshProUGUI>().text =
                $"{flow.Strings.Get("settings.audio")} — {(on ? "ON" : "OFF")}";
        }
    }

    /// <summary>
    /// ROADMAP M2: the continue offer — a pre-results interstitial sheet over the
    /// drowned board. Five-second pip countdown; rewarded ad or coins revives,
    /// "Let go" (or the timer) concedes to results. Once per run, never Daily —
    /// the flow and the sim both enforce the rules; this is just the face.
    /// </summary>
    public sealed class ContinueSheet : MonoBehaviour
    {
        private const int CountdownSeconds = 5;

        private Sheet sheet = null!;
        private GameFlow flow = null!;
        private TextMeshProUGUI body = null!;
        private ProgressPips pips = null!;
        private Button adButton = null!;
        private Button coinButton = null!;
        private float deadline;
        private bool resolved;

        public bool Shown => sheet != null && sheet.Shown;

        public static ContinueSheet Build(RectTransform canvasRoot, GameFlow flow)
        {
            Sheet sheet = UiComponents.SheetComponent(canvasRoot, "ContinueSheet", 860f);
            var offer = sheet.gameObject.AddComponent<ContinueSheet>();
            offer.sheet = sheet;
            offer.flow = flow;

            RectTransform bodyRt = sheet.Body;
            TextMeshProUGUI title = UiText.Create(bodyRt, "title", flow.Strings.Get("continue.title"),
                "title", "text.primary");
            UiComponents.Place(title.rectTransform, new Vector2(0.5f, 0.88f), new Vector2(800f, 90f));

            offer.body = UiText.Create(bodyRt, "body", "", "body", "text.secondary");
            UiComponents.Place(offer.body.rectTransform, new Vector2(0.5f, 0.74f), new Vector2(820f, 60f));

            offer.pips = UiComponents.ProgressPipsComponent(bodyRt, CountdownSeconds);
            UiComponents.Place((RectTransform)offer.pips.transform, new Vector2(0.5f, 0.64f), new Vector2(260f, 40f));

            offer.adButton = UiComponents.ButtonReward(bodyRt, "continueAd",
                flow.Strings.Get("continue.ad"), offer.OnAd);
            UiComponents.Place((RectTransform)offer.adButton.transform, new Vector2(0.5f, 0.47f), new Vector2(660f, 124f));

            offer.coinButton = UiComponents.ButtonSecondary(bodyRt, "continueCoins",
                string.Format(flow.Strings.Get("continue.coins"), flow.Economy.Coins.ContinueCost), offer.OnCoins);
            UiComponents.Place((RectTransform)offer.coinButton.transform, new Vector2(0.5f, 0.28f), new Vector2(660f, 110f));

            Button decline = UiComponents.ButtonGhost(bodyRt, "decline",
                flow.Strings.Get("continue.decline"), offer.OnDecline);
            UiComponents.Place((RectTransform)decline.transform, new Vector2(0.5f, 0.10f), new Vector2(420f, 88f));

            return offer;
        }

        public void Show()
        {
            resolved = false;
            deadline = Time.realtimeSinceStartup + CountdownSeconds;
            adButton.interactable = flow.ContinueAdAvailable;
            coinButton.interactable = flow.Meta.CanAfford(flow.Economy.Coins.ContinueCost);
            sheet.Show();
        }

        private void Update()
        {
            if (!Shown || resolved)
            {
                return;
            }

            float remaining = deadline - Time.realtimeSinceStartup;
            int seconds = Mathf.Max(0, Mathf.CeilToInt(remaining));
            body.text = string.Format(flow.Strings.Get("continue.body"), seconds);
            pips.SetFilled(seconds);
            if (remaining <= 0f)
            {
                OnDecline();
            }
        }

        private void OnAd()
        {
            if (flow.TryContinueViaAd())
            {
                Resolve();
            }
        }

        private void OnCoins()
        {
            if (flow.TryContinueWithCoins())
            {
                Resolve();
            }
        }

        private void OnDecline()
        {
            flow.DeclineContinue();
            Resolve();
        }

        private void Resolve()
        {
            resolved = true;
            sheet.Dismiss();
        }

        /// <summary>Test/driver hook: dismiss as if "Let go" was tapped.</summary>
        public void DismissForDriver() => Resolve();
    }

    /// <summary>
    /// Spec §4.7 first-run age gate: neutral year picker on bg.abyss, no game art
    /// that skews child-directed. The stored year feeds ad configuration when the
    /// real SDKs land; the (fake) consent flow itself is requested at boot.
    /// </summary>
    public sealed class ConsentAgeGate : MonoBehaviour
    {
        public const string BirthYearKey = "consent.birthYear";
        private int year = 2000;
        private TextMeshProUGUI yearLabel = null!;
        private GameFlow flow = null!;

        public bool IsOpen => gameObject.activeSelf;

        public static bool Required => !PlayerPrefs.HasKey(BirthYearKey);

        public static ConsentAgeGate Build(RectTransform canvasRoot, GameFlow flow)
        {
            RectTransform root = UiComponents.Rect(canvasRoot, "ConsentAgeGate", Vector2.zero);
            UiComponents.Stretch(root);
            var bg = root.gameObject.AddComponent<Image>();
            bg.sprite = SpriteFactory.Solid();
            ThemedElement.Bind(root.gameObject, "bg.abyss");
            var gate = root.gameObject.AddComponent<ConsentAgeGate>();
            gate.flow = flow;

            // One composed card instead of elements adrift on the void.
            RectTransform card = UiComponents.Card(root, "card", new Vector2(880f, 820f));
            UiComponents.Place(card, new Vector2(0.5f, 0.55f), new Vector2(880f, 820f));

            TextMeshProUGUI title = UiText.Create(card, "title",
                flow.Strings.Get("consent.age.title"), "title", "text.primary");
            UiComponents.Place(title.rectTransform, new Vector2(0.5f, 0.84f), new Vector2(800f, 100f));
            TextMeshProUGUI body = UiText.Create(card, "body",
                flow.Strings.Get("consent.age.body"), "body", "text.secondary");
            UiComponents.Place(body.rectTransform, new Vector2(0.5f, 0.70f), new Vector2(800f, 60f));

            // Year row: steppers flank the numeral, not the screen edges.
            Button minus = UiComponents.IconButton(card, "minus", "‹", () => gate.Step(-1));
            UiComponents.Place((RectTransform)minus.transform, new Vector2(0.5f, 0.50f), new Vector2(96f, 96f));
            ((RectTransform)minus.transform).anchoredPosition = new Vector2(-280f, 0f);
            gate.yearLabel = UiText.Create(card, "year", "2000", "display", "text.primary");
            UiComponents.Place(gate.yearLabel.rectTransform, new Vector2(0.5f, 0.50f), new Vector2(420f, 120f));
            Button plus = UiComponents.IconButton(card, "plus", "›", () => gate.Step(1));
            UiComponents.Place((RectTransform)plus.transform, new Vector2(0.5f, 0.50f), new Vector2(96f, 96f));
            ((RectTransform)plus.transform).anchoredPosition = new Vector2(280f, 0f);

            Button confirm = UiComponents.ButtonPrimary(card, "confirm",
                flow.Strings.Get("consent.age.confirm"), gate.Confirm);
            UiComponents.Place((RectTransform)confirm.transform, new Vector2(0.5f, 0.18f), new Vector2(640f, 130f));

            gate.Step(0);
            return gate;
        }

        private void Step(int delta)
        {
            year = Mathf.Clamp(year + delta, 1920, DateTimeYearNow());
            yearLabel.text = year.ToString();
        }

        private static int DateTimeYearNow() => System.DateTime.Now.Year;

        private void Confirm()
        {
            PlayerPrefs.SetInt(BirthYearKey, year);
            PlayerPrefs.Save();
            gameObject.SetActive(false);

            // ROADMAP M1: a virgin profile dives straight into the tutorial board —
            // the funnel is decided in the first 30 seconds, not on a menu.
            if (flow.Meta.Voyage.CompletedCount == 0 && flow.Screen == FlowScreen.Home)
            {
                flow.StartVoyageLevel(1, 1);
            }
        }
    }

    /// <summary>
    /// Mid-run resume prompt (SAVE_RESUME_DESIGN.md §5): a sheet over Home when a
    /// pending run record survived a process death. Resume replays to the exact
    /// state; Abandon discards (Voyage/Endless lose nothing; a Daily attempt was
    /// already consumed at its StartDaily).
    /// </summary>
    public sealed class ResumeSheet : MonoBehaviour
    {
        private Sheet sheet = null!;

        public bool Shown => sheet != null && sheet.Shown;

        public static ResumeSheet Build(RectTransform canvasRoot, GameFlow flow, ToastManager toasts)
        {
            Sheet sheet = UiComponents.SheetComponent(canvasRoot, "ResumeSheet", 640f);
            var resume = sheet.gameObject.AddComponent<ResumeSheet>();
            resume.sheet = sheet;

            RectTransform body = sheet.Body;
            TextMeshProUGUI title = UiText.Create(body, "title", flow.Strings.Get("resume.title"),
                "heading", "text.primary");
            UiComponents.Place(title.rectTransform, new Vector2(0.5f, 0.84f), new Vector2(700f, 80f));

            Riptide.Core.RunRecord? record = flow.PendingRun;
            string bodyText = record == null ? "" : record.Mode switch
            {
                "Voyage" => string.Format(flow.Strings.Get("resume.body.voyage"),
                    record.Zone, record.Level, record.Moves.Count),
                "Daily" => string.Format(flow.Strings.Get("resume.body.daily"), record.Moves.Count),
                _ => string.Format(flow.Strings.Get("resume.body.endless"), record.Moves.Count),
            };
            TextMeshProUGUI info = UiText.Create(body, "body", bodyText, "body", "text.secondary");
            UiComponents.Place(info.rectTransform, new Vector2(0.5f, 0.64f), new Vector2(780f, 60f));

            Button go = UiComponents.ButtonPrimary(body, "resume", flow.Strings.Get("resume.continue"),
                () =>
                {
                    resume.sheet.Dismiss();
                    if (!flow.ResumeRun())
                    {
                        toasts.Show(flow.Strings.Get("resume.expired"));
                    }
                });
            UiComponents.Place((RectTransform)go.transform, new Vector2(0.5f, 0.40f), new Vector2(640f, 124f));

            Button abandon = UiComponents.ButtonGhost(body, "abandon", flow.Strings.Get("resume.abandon"),
                () =>
                {
                    flow.AbandonPendingRun();
                    resume.sheet.Dismiss();
                });
            UiComponents.Place((RectTransform)abandon.transform, new Vector2(0.5f, 0.15f), new Vector2(640f, 90f));

            return resume;
        }

        public void Show() => sheet.Show();
    }

    /// <summary>
    /// Shared screen scaffolding. Universal-fit pass: each screen root is now a
    /// TRANSPARENT safe-area-padded rect (content never under a notch), while the
    /// backdrop (bg + snow + vignette) is ONE shared full-bleed layer behind the
    /// stack (ScreenBackdrop) — it must extend under notches, and one snow system
    /// beats eight (§9 budgets).
    /// </summary>
    internal static class ScreenChrome
    {
        public static RectTransform Root(RectTransform parent, string name)
        {
            RectTransform root = UiComponents.Rect(parent, name, Vector2.zero);
            UiComponents.Stretch(root);

            // Screens stay opaque to input even though their pixels come from the
            // shared backdrop — stray taps must not reach lower stack entries.
            var blocker = root.gameObject.AddComponent<Image>();
            blocker.sprite = SpriteFactory.Solid();
            blocker.color = new Color(0f, 0f, 0f, 0f);

            root.gameObject.AddComponent<SafeArea>();
            return root;
        }
    }

    /// <summary>
    /// The single full-bleed menu backdrop living behind the screen stack.
    /// Visual pass: layered depth — base color, brighter water above, god rays
    /// from the surface, drifting bokeh snow, vignette. We are UNDER the ocean;
    /// the backdrop finally says so.
    /// </summary>
    internal static class ScreenBackdrop
    {
        public static RectTransform Create(RectTransform parent)
        {
            RectTransform root = UiComponents.Rect(parent, "Backdrop", Vector2.zero);
            UiComponents.Stretch(root);
            var bg = root.gameObject.AddComponent<Image>();
            bg.sprite = SpriteFactory.Solid();
            ThemedElement.Bind(root.gameObject, "bg.deep");

            // Brighter water toward the surface: a top-down gradient band.
            RectTransform glowTop = UiComponents.Rect(root, "surfaceGlow", Vector2.zero);
            glowTop.anchorMin = new Vector2(0f, 0.45f);
            glowTop.anchorMax = new Vector2(1f, 1f);
            glowTop.offsetMin = Vector2.zero;
            glowTop.offsetMax = Vector2.zero;
            glowTop.localScale = new Vector3(1f, -1f, 1f); // fade points down
            var glowImage = glowTop.gameObject.AddComponent<Image>();
            glowImage.sprite = SpriteFactory.VerticalFade();
            glowImage.raycastTarget = false;
            ThemedElement.Bind(glowTop.gameObject, "bg.oceanTop");

            // God rays angling in from the surface.
            BuildRay(root, x: 0.30f, tiltDeg: 14f, widthRefPx: 320f);
            BuildRay(root, x: 0.58f, tiltDeg: -9f, widthRefPx: 230f);
            BuildRay(root, x: 0.80f, tiltDeg: 18f, widthRefPx: 170f);

            CanvasSnow.Create(root);
            RectTransform vignetteRt = UiComponents.Rect(root, "vignette", Vector2.zero);
            UiComponents.Stretch(vignetteRt);
            var vignette = vignetteRt.gameObject.AddComponent<Image>();
            vignette.sprite = SpriteFactory.Vignette();
            vignette.color = new Color(0f, 0f, 0f, 0.55f);
            vignette.raycastTarget = false;

            root.SetAsFirstSibling();
            return root;
        }

        private static void BuildRay(RectTransform root, float x, float tiltDeg, float widthRefPx)
        {
            RectTransform ray = UiComponents.Rect(root, "ray", new Vector2(widthRefPx, 2000f));
            ray.anchorMin = new Vector2(x, 1f);
            ray.anchorMax = new Vector2(x, 1f);
            ray.pivot = new Vector2(0.5f, 1f);
            ray.anchoredPosition = new Vector2(0f, 80f);
            ray.localRotation = Quaternion.Euler(0f, 0f, tiltDeg);
            var image = ray.gameObject.AddComponent<Image>();
            image.sprite = MenuSprites.LightRay();
            image.raycastTarget = false;
            ThemedElement.Bind(ray.gameObject, "ray.light");
        }
    }
}
