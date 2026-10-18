using Riptide.Core;
using Riptide.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// Spec §4.1 Home, Tidal Pop edition (UI spec §13): streak + coin pills, the
    /// stickered wordmark with a resident creature, the Voyage level card (zone
    /// ribbon, level badge, zone stars, progress, one huge PLAY), and the Daily ·
    /// Event Tide · Endless tiles above the tab bar (GDD §15.4). The card body
    /// opens the level map; PLAY dives straight into the next level. The mascot
    /// over the wordmark is your buddy (the crab until you pick one).
    /// </summary>
    public sealed class HomeScreen : MonoBehaviour, IScreenRefresh
    {
        private const float TilesBottom = ScreenChrome.TabFloorRefPx - 20f;
        private const float TileHeight = 340f;
        private const float TileWidth = 314f;
        private const float TileStep = 333f;
        private const float CardHeight = 700f;
        private const float CardBottom = TilesBottom + TileHeight + 50f;

        private GameFlow flow = null!;
        private CoinCounter coins = null!;
        private RectTransform streakPill = null!;
        private TextMeshProUGUI streakCount = null!;
        private TextMeshProUGUI ribbonLabel = null!;
        private TextMeshProUGUI levelNumber = null!;
        private TextMeshProUGUI zoneStars = null!;
        private RectTransform progressFill = null!;
        private TextMeshProUGUI progressLabel = null!;
        private TextMeshProUGUI dailyCaption = null!;
        private GameObject dailyNew = null!;
        private GameObject dailyDone = null!;
        private TextMeshProUGUI endlessCaption = null!;
        private TextMeshProUGUI eventCaption = null!;
        private Image eventCreature = null!;
        private GameObject eventWon = null!;
        private RectTransform play = null!;
        private RectTransform mascot = null!;
        private Image mascotImage = null!;
        private int mascotSpecies;
        private float nextBlink;
        private RectTransform[] sections = null!;
        private float idle;

        public static RectTransform Build(RectTransform parent, GameFlow flow)
        {
            RectTransform root = ScreenChrome.Root(parent, "HomeScreen");
            var screen = root.gameObject.AddComponent<HomeScreen>();
            screen.flow = flow;

            RectTransform topBar = screen.BuildTopBar(root);
            RectTransform logo = screen.BuildLogo(root);
            RectTransform card = screen.BuildVoyageCard(root);
            (RectTransform daily, RectTransform tide, RectTransform endless) = screen.BuildModeTiles(root);

            screen.sections = new[] { topBar, logo, card, daily, tide, endless };
            screen.Refresh();
            return root;
        }

        private RectTransform BuildTopBar(RectTransform root)
        {
            RectTransform bar = UiComponents.Rect(root, "topBar", Vector2.zero);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.offsetMin = new Vector2(0f, -170f);
            bar.offsetMax = Vector2.zero;

            streakPill = PopKit.Panel(bar, "streakPill", new Vector2(200f, 104f), PopTone.Navy, 52f, 8f);
            streakPill.anchorMin = streakPill.anchorMax = new Vector2(0f, 0.5f);
            streakPill.anchoredPosition = new Vector2(290f, 0f);
            Image flame = PopKit.Icon(streakPill, "flame", "flame", "warning", 64f, "pop.coral.edge", 8);
            ((RectTransform)flame.transform.parent).anchoredPosition = new Vector2(-46f, 2f);
            streakCount = UiText.CreateOutlined(streakPill, "count", "0", "heading", "text.primary", "panel.edge");
            UiComponents.Place(streakCount.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(110f, 70f));
            streakCount.rectTransform.anchoredPosition = new Vector2(38f, 0f);

            // Rewarded daily chest: a bobbing gift bubble, always one tap away.
            Button chest = PopKit.IconButton(bar, "chest", "chest", PopTone.Yellow,
                () => { flow.TryClaimChestViaAd(); Refresh(); }, new Vector2(116f, 122f), round: true);
            var chestRt = (RectTransform)chest.transform;
            chestRt.anchorMin = chestRt.anchorMax = new Vector2(0f, 0.5f);
            chestRt.anchoredPosition = new Vector2(104f, 0f);
            RectTransform adBadge = PopKit.Panel(chestRt, "adBadge", new Vector2(70f, 44f), PopTone.Purple, 14f, 5f);
            adBadge.anchorMin = adBadge.anchorMax = new Vector2(1f, 1f);
            adBadge.anchoredPosition = new Vector2(-6f, -10f);
            TextMeshProUGUI ad = UiText.CreateOutlined(adBadge, "ad", "AD", "micro", "text.primary", "pop.purple.edge");
            UiComponents.Stretch(ad.rectTransform);

            coins = PopKit.CoinPill(bar, () => flow.GoTo(FlowScreen.Shop));
            var pill = (RectTransform)coins.transform.parent;
            pill.anchorMin = pill.anchorMax = new Vector2(1f, 0.5f);
            pill.anchoredPosition = new Vector2(-205f, 0f);
            return bar;
        }

        private RectTransform BuildLogo(RectTransform root)
        {
            // The logo floats centered in whatever height sits between the top
            // bar and the card — tall phones get air, 16:9 stays tight.
            RectTransform zone = UiComponents.Rect(root, "logoZone", Vector2.zero);
            zone.anchorMin = new Vector2(0f, 0f);
            zone.anchorMax = new Vector2(1f, 1f);
            zone.offsetMin = new Vector2(0f, CardBottom + CardHeight + 40f);
            zone.offsetMax = new Vector2(0f, -170f);

            RectTransform logo = UiComponents.Rect(zone, "logo", new Vector2(980f, 330f));
            UiComponents.Place(logo, new Vector2(0.5f, 0.5f), new Vector2(980f, 330f));
            logo.localRotation = Quaternion.Euler(0f, 0f, 4f);

            TextMeshProUGUI word = UiText.CreateOutlined(logo, "wordmark", flow.Strings.Get("app.title"),
                "display", "text.primary", "panel.edge", 0.34f);
            word.fontSize = 210f;
            UiComponents.Place(word.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(980f, 230f));

            Image waveBack = PopKit.Img(logo, "waveOutline", PopSprites.WaveLine(34), "panel.edge");
            UiComponents.Place(waveBack.rectTransform, new Vector2(0.5f, 0.12f), new Vector2(640f, 120f));
            Image wave = PopKit.Img(logo, "wave", PopSprites.WaveLine(14), "accent.primary");
            UiComponents.Place(wave.rectTransform, new Vector2(0.5f, 0.12f), new Vector2(640f, 120f));

            // A resident creature peeks over the wordmark's shoulder.
            mascot = UiComponents.Rect(logo, "mascot", new Vector2(190f, 190f));
            UiComponents.Place(mascot, new Vector2(0.93f, 0.86f), new Vector2(190f, 190f));
            mascot.localRotation = Quaternion.Euler(0f, 0f, -14f);
            mascotImage = PopKit.Img(mascot, "creature", CreatureSprites.For(0), "pet.eye");
            UiComponents.Stretch(mascotImage.rectTransform);
            return zone;
        }

        private RectTransform BuildVoyageCard(RectTransform root)
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

            RectTransform ribbon = PopKit.Ribbon(card, "ribbon", "", PopTone.Coral, new Vector2(700f, 112f));
            ribbon.anchorMin = ribbon.anchorMax = new Vector2(0.5f, 1f);
            ribbon.anchoredPosition = new Vector2(0f, 6f);
            ribbonLabel = ribbon.Find("band/face/label").GetComponent<TextMeshProUGUI>();

            Button map = PopKit.IconButton(card, "mapLink", "map", PopTone.White,
                () => flow.GoTo(FlowScreen.ZoneMap), new Vector2(116f, 120f));
            var mapRt = (RectTransform)map.transform;
            mapRt.anchorMin = mapRt.anchorMax = new Vector2(1f, 1f);
            mapRt.anchoredPosition = new Vector2(-96f, -136f);

            // Level badge: a big candy disc.
            RectTransform badge = UiComponents.Rect(card, "levelBadge", new Vector2(250f, 250f));
            badge.anchorMin = badge.anchorMax = new Vector2(0f, 1f);
            badge.anchoredPosition = new Vector2(205f, -260f);
            PopKit.Surface(badge, PopTone.Blue, 125f, PopKit.Border, lip: 12f);
            RectTransform badgeFace = PopKit.FaceOf(badge);
            TextMeshProUGUI levelWord = UiText.CreateOutlined(badgeFace, "levelWord", flow.Strings.Get("home.level"),
                "micro", "text.primary", "pop.blue.edge");
            UiComponents.Place(levelWord.rectTransform, new Vector2(0.5f, 0.74f), new Vector2(200f, 40f));
            levelNumber = UiText.CreateOutlined(badgeFace, "levelNumber", "1", "display", "text.primary",
                "pop.blue.edge", 0.3f);
            UiComponents.Place(levelNumber.rectTransform, new Vector2(0.5f, 0.42f), new Vector2(220f, 120f));

            // Zone stars + progress bar to the badge's right.
            Image star = PopKit.Icon(card, "zoneStar", "star", "pop.yellow", 76f, "pop.yellow.edge", 9);
            var starRt = (RectTransform)star.transform.parent;
            starRt.anchorMin = starRt.anchorMax = new Vector2(0f, 1f);
            starRt.anchoredPosition = new Vector2(420f, -214f);
            zoneStars = UiText.Create(card, "zoneStars", "", "heading", "text.ink", TextAlignmentOptions.MidlineLeft);
            zoneStars.rectTransform.anchorMin = zoneStars.rectTransform.anchorMax = new Vector2(0f, 1f);
            zoneStars.rectTransform.pivot = new Vector2(0f, 0.5f);
            zoneStars.rectTransform.sizeDelta = new Vector2(300f, 70f);
            zoneStars.rectTransform.anchoredPosition = new Vector2(470f, -214f);

            RectTransform track = PopKit.Panel(card, "progress", new Vector2(480f, 74f), PopTone.Navy, 37f, 7f);
            track.anchorMin = track.anchorMax = new Vector2(0f, 1f);
            track.pivot = new Vector2(0f, 0.5f);
            track.anchoredPosition = new Vector2(388f, -318f);
            RectTransform clip = UiComponents.Rect(track, "clip", Vector2.zero);
            UiComponents.Stretch(clip);
            clip.offsetMin = new Vector2(9f, 9f);
            clip.offsetMax = new Vector2(-9f, -9f);
            progressFill = UiComponents.Rect(clip, "fill", Vector2.zero);
            progressFill.anchorMin = Vector2.zero;
            progressFill.anchorMax = new Vector2(0.1f, 1f);
            progressFill.offsetMin = progressFill.offsetMax = Vector2.zero;
            PopKit.Surface(progressFill, PopTone.Green, 28f, 0f, lip: 0f);
            progressLabel = UiText.CreateOutlined(track, "label", "", "caption", "text.primary", "panel.edge");
            UiComponents.Stretch(progressLabel.rectTransform);

            Button playButton = PopKit.Button(card, "play", flow.Strings.Get("home.play"), PopTone.Green, () =>
            {
                (int z, int i) = flow.Meta.Voyage.NextLevel();
                flow.StartVoyageLevel(z, i);
            }, new Vector2(860f, 220f), "display", "play");
            play = (RectTransform)playButton.transform;
            play.anchorMin = play.anchorMax = new Vector2(0.5f, 0f);
            play.pivot = new Vector2(0.5f, 0f);
            play.anchoredPosition = new Vector2(0f, 64f);
            return card;
        }

        private (RectTransform daily, RectTransform tide, RectTransform endless) BuildModeTiles(RectTransform root)
        {
            Button daily = PopKit.Tile(root, "daily", PopTone.Yellow, () => flow.GoTo(FlowScreen.DailyIntro),
                new Vector2(TileWidth, TileHeight));
            var dailyRt = (RectTransform)daily.transform;
            dailyRt.anchorMin = dailyRt.anchorMax = new Vector2(0.5f, 0f);
            dailyRt.pivot = new Vector2(0.5f, 0f);
            dailyRt.anchoredPosition = new Vector2(-TileStep, TilesBottom);
            RectTransform dailyFace = PopKit.FaceOf(daily);
            TileContent(dailyFace, "calendar", "pop.coral", "pop.yellow.edge", flow.Strings.Get("home.dailyShort"),
                PopTone.Yellow, out dailyCaption);

            RectTransform sticker = PopKit.Panel(dailyRt, "new", new Vector2(150f, 70f), PopTone.Coral, 20f, 7f);
            sticker.anchorMin = sticker.anchorMax = new Vector2(1f, 1f);
            sticker.anchoredPosition = new Vector2(-20f, -14f);
            sticker.localRotation = Quaternion.Euler(0f, 0f, -8f);
            TextMeshProUGUI newText = UiText.CreateOutlined(sticker, "label", flow.Strings.Get("home.dailyNew"),
                "body", "text.primary", "pop.coral.edge");
            UiComponents.Stretch(newText.rectTransform);
            dailyNew = sticker.gameObject;

            Image check = PopKit.Icon(dailyRt, "done", "check", "pop.green.top", 70f, "pop.green.edge", 9);
            var checkRt = (RectTransform)check.transform.parent;
            checkRt.anchorMin = checkRt.anchorMax = new Vector2(1f, 1f);
            checkRt.anchoredPosition = new Vector2(-62f, -70f);
            dailyDone = checkRt.gameObject;

            // GDD §15.4 Event Tide: this week's featured creature fronts the tile.
            Button tide = PopKit.Tile(root, "event", PopTone.Coral, () => flow.GoTo(FlowScreen.EventIntro),
                new Vector2(TileWidth, TileHeight));
            var tideRt = (RectTransform)tide.transform;
            tideRt.anchorMin = tideRt.anchorMax = new Vector2(0.5f, 0f);
            tideRt.pivot = new Vector2(0.5f, 0f);
            tideRt.anchoredPosition = new Vector2(0f, TilesBottom);
            RectTransform tideFace = PopKit.FaceOf(tide);
            TileContent(tideFace, "star", "pop.yellow", "pop.coral.edge", flow.Strings.Get("home.eventShort"),
                PopTone.Coral, out eventCaption);
            tideFace.Find("icon").gameObject.SetActive(false);
            eventCreature = PopKit.Img(tideFace, "creature", CreatureSprites.For(0), "pet.eye");
            UiComponents.Place(eventCreature.rectTransform, new Vector2(0f, 1f), new Vector2(150f, 150f));
            eventCreature.rectTransform.anchoredPosition = new Vector2(104f, -98f);
            Image won = PopKit.Icon(tideRt, "won", "check", "pop.green.top", 70f, "pop.green.edge", 9);
            var wonRt = (RectTransform)won.transform.parent;
            wonRt.anchorMin = wonRt.anchorMax = new Vector2(1f, 1f);
            wonRt.anchoredPosition = new Vector2(-62f, -70f);
            eventWon = wonRt.gameObject;

            Button endless = PopKit.Tile(root, "endless", PopTone.Purple, flow.StartEndless,
                new Vector2(TileWidth, TileHeight));
            var endlessRt = (RectTransform)endless.transform;
            endlessRt.anchorMin = endlessRt.anchorMax = new Vector2(0.5f, 0f);
            endlessRt.pivot = new Vector2(0.5f, 0f);
            endlessRt.anchoredPosition = new Vector2(TileStep, TilesBottom);
            TileContent(PopKit.FaceOf(endless), "waves", "accent.primary", "pop.purple.edge",
                flow.Strings.Get("home.endlessShort"), PopTone.Purple, out endlessCaption);
            return (dailyRt, tideRt, endlessRt);
        }

        private static void TileContent(RectTransform face, string iconId, string iconToken, string edgeToken,
            string title, PopTone tone, out TextMeshProUGUI caption)
        {
            Image icon = PopKit.Icon(face, "icon", iconId, iconToken, 120f, edgeToken, 10);
            var iconRt = (RectTransform)icon.transform.parent;
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 1f);
            iconRt.anchoredPosition = new Vector2(100f, -96f);

            TextMeshProUGUI titleText = UiText.CreateOutlined(face, "title", title, "title", "text.primary",
                PopKit.Edge(tone), 0.26f, TextAlignmentOptions.BottomLeft);
            titleText.rectTransform.anchorMin = new Vector2(0f, 0f);
            titleText.rectTransform.anchorMax = new Vector2(1f, 0f);
            titleText.rectTransform.pivot = new Vector2(0.5f, 0f);
            titleText.rectTransform.offsetMin = new Vector2(30f, 84f);
            titleText.rectTransform.offsetMax = new Vector2(-20f, 160f);
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 34f;
            titleText.fontSizeMax = titleText.fontSize;

            caption = UiText.CreateOutlined(face, "caption", "", "body", "text.primary", PopKit.Edge(tone), 0.2f,
                TextAlignmentOptions.BottomLeft);
            caption.rectTransform.anchorMin = new Vector2(0f, 0f);
            caption.rectTransform.anchorMax = new Vector2(1f, 0f);
            caption.rectTransform.pivot = new Vector2(0.5f, 0f);
            caption.rectTransform.offsetMin = new Vector2(30f, 34f);
            caption.rectTransform.offsetMax = new Vector2(-20f, 84f);
            caption.enableAutoSizing = true;
            caption.fontSizeMin = 24f;
            caption.fontSizeMax = caption.fontSize;
        }

        public void Refresh()
        {
            (int zone, int index) = flow.Meta.Voyage.NextLevel();
            ribbonLabel.text = string.Format(flow.Strings.Get("home.zoneRibbon"), zone,
                flow.Strings.Get($"zones.{zone}"));
            levelNumber.text = index.ToString();

            int stars = 0;
            for (int i = 1; i <= 20; i++)
            {
                stars += flow.Meta.Voyage.StarsFor(VoyageProgress.LevelId(zone, i));
            }

            zoneStars.text = string.Format(flow.Strings.Get("home.zoneStars"), stars);
            int completed = Mathf.Clamp(index - 1, 0, 20);
            progressFill.anchorMax = new Vector2(Mathf.Max(0.12f, completed / 20f), 1f);
            progressLabel.text = string.Format(flow.Strings.Get("home.levelProgress"), completed);

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

            int buddy = flow.Meta.Save.BuddySpecies;
            mascotSpecies = buddy >= 0 && buddy < flow.Roster.Count ? buddy : 0;
            mascotImage.sprite = CreatureSprites.For(mascotSpecies);

            int current = flow.Meta.Streak.Current;
            streakPill.gameObject.SetActive(current > 0);
            streakCount.text = current.ToString();
            coins.SetInstant(flow.Meta.Coins);

            if (isActiveAndEnabled && sections != null)
            {
                UiCascade.Run(this, sections);
            }
        }

        private void Update()
        {
            if (Time.time > nextBlink)
            {
                bool closing = Time.time < nextBlink + 0.14f;
                mascotImage.sprite = closing ? CreatureSprites.Blink(mascotSpecies) : CreatureSprites.For(mascotSpecies);
                if (!closing)
                {
                    nextBlink = Time.time + Random.Range(2.5f, 5f);
                }
            }

            if (ThemeRuntime.ReducedMotion)
            {
                return;
            }

            // The genre's idle invitation: PLAY breathes, the mascot bobs.
            idle += Time.deltaTime;
            play.localScale = Vector3.one * (1f + 0.025f * Mathf.Sin(idle * 3.2f));
            mascot.anchoredPosition = new Vector2(0f, 8f * Mathf.Sin(idle * 1.6f));
        }
    }
}
