using Riptide.Core;
using Riptide.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// Spec §4.1 Home, "Buddy Stage" edition (Nick's pick of five Home directions,
    /// 2026-10-18): chest · streak · wordmark · coins along the top; your buddy pet
    /// center stage (<see cref="BuddyStage"/>: speech bubble, pet gift, taps); its
    /// name pill (→ Tidelings) and egg count; the level card (zone ribbon, level, zone stars,
    /// progress, one huge PLAY — the card body opens the map); and the Daily ·
    /// Event Tide · Endless cards above the tab bar (GDD §15.4).
    /// </summary>
    public sealed class HomeScreen : MonoBehaviour, IScreenRefresh
    {
        private const float TopBarRefPx = 170f;
        private const float TilesBottom = ScreenChrome.TabFloorRefPx - 20f;
        private const float TileHeight = 210f;
        private const float TileWidth = 314f;
        private const float TileStep = 333f;
        private const float CardHeight = 500f;
        private const float CardBottom = TilesBottom + TileHeight + 46f;
        private const float PillCenter = CardBottom + CardHeight + 122f;
        private const float StageBottom = PillCenter + 56f;

        private GameFlow flow = null!;
        private BuddyStage stage = null!;
        private CoinCounter coins = null!;
        private RectTransform streakPill = null!;
        private TextMeshProUGUI streakCount = null!;
        private TextMeshProUGUI petName = null!;
        private PopBar petBar = null!;
        private TextMeshProUGUI eggCount = null!;
        private RectTransform eggPill = null!;
        private TextMeshProUGUI ribbonLabel = null!;
        private TextMeshProUGUI levelLabel = null!;
        private TextMeshProUGUI zoneStars = null!;
        private PopBar levelBar = null!;
        private TextMeshProUGUI dailyCaption = null!;
        private GameObject dailyNew = null!;
        private GameObject dailyDone = null!;
        private TextMeshProUGUI endlessCaption = null!;
        private TextMeshProUGUI eventCaption = null!;
        private Image eventCreature = null!;
        private GameObject eventWon = null!;
        private RectTransform play = null!;
        private RectTransform[] sections = null!;
        private float idle;

        /// <summary>The buddy stage. Test surface.</summary>
        public BuddyStage Stage => stage;

        public static RectTransform Build(RectTransform parent, GameFlow flow)
        {
            RectTransform root = ScreenChrome.Root(parent, "HomeScreen");
            var screen = root.gameObject.AddComponent<HomeScreen>();
            screen.flow = flow;

            RectTransform topBar = screen.BuildTopBar(root);
            screen.stage = BuddyStage.Build(root, flow, StageBottom, TopBarRefPx,
                () => screen.coins.AnimateTo(flow.Meta.Coins));
            RectTransform pill = screen.BuildNamePill(root);
            RectTransform card = screen.BuildLevelCard(root);
            (RectTransform daily, RectTransform tide, RectTransform endless) = screen.BuildModeTiles(root);

            screen.sections = new[] { topBar, screen.stage.Rect, pill, screen.eggPill, card, daily, tide, endless };
            screen.Refresh();
            return root;
        }

        private RectTransform BuildTopBar(RectTransform root)
        {
            RectTransform bar = UiComponents.Rect(root, "topBar", Vector2.zero);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.offsetMin = new Vector2(0f, -TopBarRefPx);
            bar.offsetMax = Vector2.zero;

            // Rewarded daily chest: a bobbing gift bubble, always one tap away.
            Button chest = PopKit.IconButton(bar, "chest", "chest", PopTone.Yellow,
                () => { flow.TryClaimChestViaAd(); Refresh(); }, new Vector2(116f, 122f), round: true);
            var chestRt = (RectTransform)chest.transform;
            chestRt.anchorMin = chestRt.anchorMax = new Vector2(0f, 0.5f);
            chestRt.anchoredPosition = new Vector2(100f, 0f);
            RectTransform adBadge = PopKit.Panel(chestRt, "adBadge", new Vector2(70f, 44f), PopTone.Purple, 14f, 5f);
            adBadge.anchorMin = adBadge.anchorMax = new Vector2(1f, 1f);
            adBadge.anchoredPosition = new Vector2(-6f, -10f);
            TextMeshProUGUI ad = UiText.CreateOutlined(adBadge, "ad", "AD", "micro", "text.primary", "pop.purple.edge");
            UiComponents.Stretch(ad.rectTransform);

            streakPill = PopKit.Panel(bar, "streakPill", new Vector2(170f, 104f), PopTone.Navy, 52f, 8f);
            streakPill.anchorMin = streakPill.anchorMax = new Vector2(0f, 0.5f);
            streakPill.anchoredPosition = new Vector2(262f, 0f);
            Image flame = PopKit.Icon(streakPill, "flame", "flame", "warning", 64f, "pop.coral.edge", 8);
            ((RectTransform)flame.transform.parent).anchoredPosition = new Vector2(-38f, 2f);
            streakCount = UiText.CreateOutlined(streakPill, "count", "0", "heading", "text.primary", "panel.edge");
            UiComponents.Place(streakCount.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(90f, 70f));
            streakCount.rectTransform.anchoredPosition = new Vector2(34f, 0f);

            TextMeshProUGUI word = UiText.CreateOutlined(bar, "wordmark", flow.Strings.Get("app.title"), "display",
                "text.primary", "panel.edge", 0.34f);
            word.fontSize = 84f;
            UiComponents.Place(word.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(330f, 110f));
            word.rectTransform.anchoredPosition = new Vector2(10f, 0f);

            coins = PopKit.CoinPill(bar, () => flow.GoTo(FlowScreen.Shop));
            var pill = (RectTransform)coins.transform.parent;
            pill.anchorMin = pill.anchorMax = new Vector2(1f, 0.5f);
            pill.anchoredPosition = new Vector2(-195f, 0f);
            return bar;
        }

        /// <summary>The buddy's name, level and progress — opens the Tidepool (where buddies are picked).</summary>
        private RectTransform BuildNamePill(RectTransform root)
        {
            RectTransform pill = PopKit.Panel(root, "buddyPill", new Vector2(560f, 96f), PopTone.Navy, 48f, 7f);
            pill.anchorMin = pill.anchorMax = new Vector2(0.5f, 0f);
            pill.anchoredPosition = new Vector2(-110f, PillCenter);
            var hit = pill.gameObject.AddComponent<Image>();
            hit.sprite = SpriteFactory.Solid();
            hit.color = new Color(0f, 0f, 0f, 0f);
            var button = pill.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => flow.GoTo(FlowScreen.Tidepool));
            pill.gameObject.AddComponent<PressEffect>();

            Image heart = PopKit.Icon(pill, "heart", "heart", "pop.coral.top", 54f, "pop.coral.edge", 6);
            var heartRt = (RectTransform)heart.transform.parent;
            heartRt.anchorMin = heartRt.anchorMax = new Vector2(0f, 0.5f);
            heartRt.anchoredPosition = new Vector2(56f, 2f);
            petName = UiText.CreateOutlined(pill, "name", "", "heading", "text.primary", "panel.edge", 0.2f,
                TextAlignmentOptions.MidlineLeft);
            UiComponents.Stretch(petName.rectTransform);
            petName.rectTransform.offsetMin = new Vector2(96f, 0f);
            petName.rectTransform.offsetMax = new Vector2(-190f, 0f);
            petName.enableAutoSizing = true;
            petName.fontSizeMin = 28f;
            petName.fontSizeMax = petName.fontSize;
            petBar = PopBar.Create(pill, "petBar", new Vector2(160f, 46f), PopTone.Coral, "micro");
            petBar.Rect.anchorMin = petBar.Rect.anchorMax = new Vector2(1f, 0.5f);
            petBar.Rect.anchoredPosition = new Vector2(-104f, 0f);

            // GDD §16.6: your eggs beside the leader's name — a tap opens the Tidelings tab.
            eggCount = TidelingUi.EggPill(root, "eggPill", new Vector2(200f, 96f));
            eggPill = (RectTransform)eggCount.transform.parent;
            eggPill.anchorMin = eggPill.anchorMax = new Vector2(0.5f, 0f);
            eggPill.anchoredPosition = new Vector2(300f, PillCenter);
            var eggHit = eggPill.gameObject.AddComponent<Image>();
            eggHit.sprite = SpriteFactory.Solid();
            eggHit.color = new Color(0f, 0f, 0f, 0f);
            var eggButton = eggPill.gameObject.AddComponent<Button>();
            eggButton.transition = Selectable.Transition.None;
            eggButton.onClick.AddListener(() => flow.GoTo(FlowScreen.Tidepool));
            eggPill.gameObject.AddComponent<PressEffect>();
            return pill;
        }

        private RectTransform BuildLevelCard(RectTransform root)
        {
            RectTransform card = UiComponents.Card(root, "voyage", new Vector2(980f, CardHeight));
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0f);
            card.pivot = new Vector2(0.5f, 0f);
            card.anchoredPosition = new Vector2(0f, CardBottom);

            // The card body is the map door (the spec's tap-the-card affordance).
            var hit = card.gameObject.AddComponent<Image>();
            hit.sprite = SpriteFactory.Solid();
            hit.color = new Color(0f, 0f, 0f, 0f);
            var cardButton = card.gameObject.AddComponent<Button>();
            cardButton.transition = Selectable.Transition.None;
            cardButton.onClick.AddListener(() => flow.GoTo(FlowScreen.ZoneMap));
            card.gameObject.AddComponent<PressEffect>();

            RectTransform ribbon = PopKit.Ribbon(card, "ribbon", "", PopTone.Coral, new Vector2(640f, 104f));
            ribbon.anchorMin = ribbon.anchorMax = new Vector2(0.5f, 1f);
            ribbon.anchoredPosition = new Vector2(0f, 6f);
            ribbonLabel = ribbon.Find("band/face/label").GetComponent<TextMeshProUGUI>();

            Button map = PopKit.IconButton(card, "mapLink", "map", PopTone.White,
                () => flow.GoTo(FlowScreen.ZoneMap), new Vector2(104f, 108f));
            var mapRt = (RectTransform)map.transform;
            mapRt.anchorMin = mapRt.anchorMax = new Vector2(1f, 1f);
            mapRt.anchoredPosition = new Vector2(-82f, -74f);

            levelLabel = UiText.Create(card, "level", "", "title", "text.ink", TextAlignmentOptions.MidlineLeft);
            Row(levelLabel.rectTransform, 56f, 300f);
            Image star = PopKit.Icon(card, "zoneStar", "star", "pop.yellow", 66f, "pop.yellow.edge", 8);
            var starRt = (RectTransform)star.transform.parent;
            starRt.anchorMin = starRt.anchorMax = new Vector2(0f, 1f);
            starRt.anchoredPosition = new Vector2(398f, -178f);
            zoneStars = UiText.Create(card, "zoneStars", "", "heading", "text.ink", TextAlignmentOptions.MidlineLeft);
            Row(zoneStars.rectTransform, 440f, 170f);
            levelBar = PopBar.Create(card, "progress", new Vector2(300f, 66f));
            Row(levelBar.Rect, 620f, 300f);

            Button playButton = PopKit.Button(card, "play", flow.Strings.Get("home.play"), PopTone.Green, () =>
            {
                (int z, int i) = flow.Meta.Voyage.NextLevel();
                flow.StartVoyageLevel(z, i);
            }, new Vector2(900f, 200f), "display", "play");
            play = (RectTransform)playButton.transform;
            play.anchorMin = play.anchorMax = new Vector2(0.5f, 0f);
            play.pivot = new Vector2(0.5f, 0f);
            play.anchoredPosition = new Vector2(0f, 46f);
            return card;
        }

        private static void Row(RectTransform rt, float left, float width)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(width, rt.sizeDelta.y > 0f ? rt.sizeDelta.y : 90f);
            rt.anchoredPosition = new Vector2(left, -178f);
        }

        private (RectTransform daily, RectTransform tide, RectTransform endless) BuildModeTiles(RectTransform root)
        {
            Button daily = ModeTile(root, "daily", PopTone.Yellow, () => flow.GoTo(FlowScreen.DailyIntro), -TileStep,
                flow.Strings.Get("home.dailyShort"), out dailyCaption);
            RectTransform dailyRt = (RectTransform)daily.transform;
            TileIcon(daily, "calendar", "pop.coral", "pop.yellow.edge");
            RectTransform sticker = PopKit.Panel(dailyRt, "new", new Vector2(130f, 62f), PopTone.Coral, 18f, 6f);
            sticker.anchorMin = sticker.anchorMax = new Vector2(1f, 1f);
            sticker.anchoredPosition = new Vector2(-10f, 6f);
            sticker.localRotation = Quaternion.Euler(0f, 0f, -8f);
            TextMeshProUGUI newText = UiText.CreateOutlined(sticker, "label", flow.Strings.Get("home.dailyNew"),
                "caption", "text.primary", "pop.coral.edge");
            UiComponents.Stretch(newText.rectTransform);
            dailyNew = sticker.gameObject;
            Image check = PopKit.Icon(dailyRt, "done", "check", "pop.green.top", 62f, "pop.green.edge", 8);
            var checkRt = (RectTransform)check.transform.parent;
            checkRt.anchorMin = checkRt.anchorMax = new Vector2(1f, 1f);
            checkRt.anchoredPosition = new Vector2(-34f, -16f);
            dailyDone = checkRt.gameObject;

            // GDD §15.4 Event Tide: this week's featured creature pokes out of its card.
            Button tide = ModeTile(root, "event", PopTone.Coral, () => flow.GoTo(FlowScreen.EventIntro), 0f,
                flow.Strings.Get("home.eventShort"), out eventCaption);
            RectTransform tideRt = (RectTransform)tide.transform;
            eventCreature = PopKit.Img(tideRt, "creature", CreatureSprites.For(0), "pet.eye");
            eventCreature.rectTransform.anchorMin = eventCreature.rectTransform.anchorMax = new Vector2(1f, 1f);
            eventCreature.rectTransform.sizeDelta = new Vector2(132f, 132f);
            eventCreature.rectTransform.anchoredPosition = new Vector2(-62f, -30f);
            Image won = PopKit.Icon(tideRt, "won", "check", "pop.green.top", 62f, "pop.green.edge", 8);
            var wonRt = (RectTransform)won.transform.parent;
            wonRt.anchorMin = wonRt.anchorMax = new Vector2(1f, 1f);
            wonRt.anchoredPosition = new Vector2(-24f, -86f);
            eventWon = wonRt.gameObject;

            Button endless = ModeTile(root, "endless", PopTone.Purple, flow.StartEndless, TileStep,
                flow.Strings.Get("home.endlessShort"), out endlessCaption);
            TileIcon(endless, "waves", "accent.primary", "pop.purple.edge");
            return (dailyRt, tideRt, (RectTransform)endless.transform);
        }

        private static Button ModeTile(RectTransform root, string name, PopTone tone, System.Action onClick, float x,
            string title, out TextMeshProUGUI caption)
        {
            Button tile = PopKit.Tile(root, name, tone, onClick, new Vector2(TileWidth, TileHeight));
            var rt = (RectTransform)tile.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(x, TilesBottom);
            RectTransform face = PopKit.FaceOf(tile);

            TextMeshProUGUI titleText = UiText.CreateOutlined(face, "title", title, "title", "text.primary",
                PopKit.Edge(tone), 0.26f, TextAlignmentOptions.TopLeft);
            Band(titleText.rectTransform, 92f, 172f);
            titleText.rectTransform.offsetMax = new Vector2(-118f, 172f); // clear of the corner icon
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 34f;
            titleText.fontSizeMax = titleText.fontSize;

            caption = UiText.CreateOutlined(face, "caption", "", "body", "text.primary", PopKit.Edge(tone), 0.2f,
                TextAlignmentOptions.BottomLeft);
            Band(caption.rectTransform, 26f, 80f);
            caption.enableAutoSizing = true;
            caption.fontSizeMin = 24f;
            caption.fontSizeMax = caption.fontSize;
            return tile;
        }

        private static void Band(RectTransform rt, float bottom, float top)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(28f, bottom);
            rt.offsetMax = new Vector2(-20f, top);
        }

        private static void TileIcon(Button tile, string iconId, string token, string edge)
        {
            Image icon = PopKit.Icon(PopKit.FaceOf(tile), "icon", iconId, token, 72f, edge, 8);
            var iconRt = (RectTransform)icon.transform.parent;
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(1f, 1f);
            iconRt.anchoredPosition = new Vector2(-54f, -62f);
        }

        public void Refresh()
        {
            (int zone, int index) = flow.Meta.Voyage.NextLevel();
            ribbonLabel.text = string.Format(flow.Strings.Get("home.zoneRibbon"), zone,
                flow.Strings.Get($"zones.{zone}"));
            levelLabel.text = $"{flow.Strings.Get("home.level")} {index}";

            int stars = 0;
            for (int i = 1; i <= 20; i++)
            {
                stars += flow.Meta.Voyage.StarsFor(VoyageProgress.LevelId(zone, i));
            }

            zoneStars.text = string.Format(flow.Strings.Get("home.zoneStars"), stars);
            int completed = Mathf.Clamp(index - 1, 0, 20);
            levelBar.Set(completed, 20, string.Format(flow.Strings.Get("home.levelProgress"), completed));

            RefreshBuddy();

            long best = flow.Meta.EndlessBest;
            endlessCaption.text = best > 0
                ? string.Format(flow.Strings.Get("endless.best"), ShareCard.GroupThousands(best))
                : flow.Strings.Get("home.endlessHint");

            bool dailyReady = flow.Meta.CanAttemptDailyToday();
            long today = flow.Meta.TodayEpochDay();
            dailyCaption.text = dailyReady
                ? string.Format(flow.Strings.Get("home.dailyBoard"), flow.Economy.Daily.DailyNumber(today))
                : flow.Strings.Get("home.dailyDone");
            dailyNew.SetActive(dailyReady);
            dailyDone.SetActive(!dailyReady);

            EventDef tide = flow.ThisWeeksEvent;
            eventCreature.sprite = CreatureSprites.For(tide.FeaturedSpecies);
            int days = flow.EventDaysLeft;
            eventCaption.text = flow.EventWonThisWeek ? flow.Strings.Get("home.eventWon")
                : days <= 1 ? flow.Strings.Get("home.eventLastDay")
                : string.Format(flow.Strings.Get("home.eventDays"), days);
            eventWon.SetActive(flow.EventWonThisWeek);

            int current = flow.Meta.Streak.Current;
            streakPill.gameObject.SetActive(current > 0);
            streakCount.text = current.ToString();
            coins.SetInstant(flow.Meta.Coins);

            if (isActiveAndEnabled && sections != null)
            {
                UiCascade.Run(this, sections);
            }
        }

        /// <summary>The stage and the name pill: buddy name · level, rescues toward the next level.</summary>
        private void RefreshBuddy()
        {
            stage.Refresh();
            eggCount.text = flow.Eggs.ToString();
            int line = flow.LeaderLine;
            petBar.gameObject.SetActive(line >= 0);
            if (line < 0)
            {
                petName.text = flow.Strings.Get("buddy.pick");
                return;
            }

            TidelingView leader = flow.Tideling(line);
            petName.text = $"{leader.FormName} · {string.Format(flow.Strings.Get("pet.level"), leader.Stage)}";
            petBar.Set(leader.NextBond.HasValue ? leader.Bond : 1, leader.NextBond ?? 1, "");
        }

        private void Update()
        {
            if (ThemeRuntime.ReducedMotion)
            {
                return;
            }

            // The genre's idle invitation: PLAY breathes.
            idle += Time.deltaTime;
            play.localScale = Vector3.one * (1f + 0.025f * Mathf.Sin(idle * 3.2f));
        }
    }
}
