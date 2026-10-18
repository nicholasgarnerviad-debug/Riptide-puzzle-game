using System.Collections;
using Riptide.Core;
using Riptide.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// Spec §4.4 results (Voyage win / Voyage lose / Endless over / Event Tide,
    /// GDD §15.4), Tidal Pop edition: a rotating sunburst behind popping stars on wins (a mood icon on
    /// losses), the ribbon headline, a white score card (score, stat chips, coin
    /// reward with the ×2 ad, the buddy bonus and powers line), and a Home ·
    /// Retry · NEXT row. Lose-by-drown opens
    /// with a 600ms top-down water tint; lose-by-stuck with a single muted flash —
    /// the two deaths must feel different.
    /// </summary>
    public sealed class ResultsScreen : MonoBehaviour, IScreenRefresh
    {
        private const float ButtonsBottom = 120f;
        private const float ButtonHeight = 220f;

        private GameFlow flow = null!;
        private RectTransform burst = null!;
        private StarTriplet stars = null!;
        private RectTransform moodBadge = null!;
        private PopSurface moodSurface = null!;
        private Image moodIcon = null!;
        private RectTransform ribbon = null!;
        private PopSurface ribbonSurface = null!;
        private TextMeshProUGUI ribbonLabel = null!;
        private TextMeshProUGUI levelLabel = null!;
        private TextMeshProUGUI score = null!;
        private TextMeshProUGUI bestCaption = null!;
        private GameObject newBestSticker = null!;
        private TextMeshProUGUI chipMoves = null!;
        private TextMeshProUGUI chipTides = null!;
        private TextMeshProUGUI chipRescued = null!;
        private RectTransform coinRow = null!;
        private CoinCounter coins = null!;
        private Button doubleCoins = null!;
        private TextMeshProUGUI bonusLine = null!;
        private RectTransform eggRow = null!;
        private EggPips eggPips = null!;
        private RectTransform card = null!;
        private Button primary = null!;
        private Button retry = null!;
        private Button upsell = null!;
        private Image intro = null!;

        public static RectTransform Build(RectTransform parent, GameFlow flow)
        {
            RectTransform root = ScreenChrome.Root(parent, "ResultsScreen");
            var screen = root.gameObject.AddComponent<ResultsScreen>();
            screen.flow = flow;

            screen.BuildCrown(root);
            screen.BuildCard(root);
            screen.BuildButtons(root);

            // Full-screen intro overlay for the two lose moods (§4.4).
            RectTransform introRect = UiComponents.Rect(root, "intro", Vector2.zero);
            UiComponents.Stretch(introRect);
            screen.intro = introRect.gameObject.AddComponent<Image>();
            screen.intro.sprite = SpriteFactory.Solid();
            screen.intro.raycastTarget = false;
            screen.intro.color = Color.clear;

            screen.Refresh();
            return root;
        }

        private void BuildCrown(RectTransform root)
        {
            burst = UiComponents.Rect(root, "burst", new Vector2(1300f, 1300f));
            burst.anchorMin = burst.anchorMax = new Vector2(0.5f, 1f);
            burst.anchoredPosition = new Vector2(0f, -330f);
            Image burstImage = PopKit.Img(burst, "rays", PopSprites.Sunburst(), "burst");
            UiComponents.Stretch(burstImage.rectTransform);

            stars = UiComponents.StarTripletComponent(root);
            var starsRt = (RectTransform)stars.transform;
            starsRt.anchorMin = starsRt.anchorMax = new Vector2(0.5f, 1f);
            starsRt.sizeDelta = new Vector2(620f, 260f);
            starsRt.anchoredPosition = new Vector2(0f, -310f);

            moodBadge = UiComponents.Rect(root, "mood", new Vector2(250f, 250f));
            moodBadge.anchorMin = moodBadge.anchorMax = new Vector2(0.5f, 1f);
            moodBadge.anchoredPosition = new Vector2(0f, -320f);
            moodSurface = PopKit.Surface(moodBadge, PopTone.Blue, 125f, PopKit.Border, lip: 12f);
            moodIcon = PopKit.Icon(moodSurface.Face, "icon", "drop", "text.primary", 130f);

            ribbon = PopKit.Ribbon(root, "ribbon", "", PopTone.Coral, new Vector2(900f, 130f), "title");
            ribbon.anchorMin = ribbon.anchorMax = new Vector2(0.5f, 1f);
            ribbon.anchoredPosition = new Vector2(0f, -560f);
            ribbonSurface = ribbon.Find("band").GetComponent<PopSurface>();
            ribbonLabel = ribbon.Find("band/face/label").GetComponent<TextMeshProUGUI>();
        }

        private void BuildCard(RectTransform root)
        {
            RectTransform zone = UiComponents.Rect(root, "cardZone", Vector2.zero);
            UiComponents.Stretch(zone);
            zone.offsetMin = new Vector2(0f, ButtonsBottom + ButtonHeight + 30f);
            zone.offsetMax = new Vector2(0f, -650f);

            card = UiComponents.Card(zone, "card", new Vector2(980f, 820f));
            UiComponents.Place(card, new Vector2(0.5f, 0.5f), new Vector2(980f, 820f));

            levelLabel = UiText.Create(card, "level", "", "micro", "text.inkSoft");
            Top(levelLabel.rectTransform, -70f, 700f, 40f);
            TextMeshProUGUI scoreWord = UiText.Create(card, "scoreLabel", flow.Strings.Get("results.scoreLabel"),
                "micro", "text.inkSoft");
            Top(scoreWord.rectTransform, -140f, 400f, 40f);
            score = UiText.Create(card, "score", "0", "display", "text.ink");
            score.fontSize = 132f;
            Top(score.rectTransform, -240f, 800f, 140f);
            bestCaption = UiText.Create(card, "best", "", "body", "text.inkSoft");
            Top(bestCaption.rectTransform, -335f, 800f, 50f);

            RectTransform sticker = PopKit.Panel(card, "newBest", new Vector2(250f, 74f), PopTone.Green, 20f, 7f);
            sticker.anchorMin = sticker.anchorMax = new Vector2(1f, 1f);
            sticker.anchoredPosition = new Vector2(-140f, -180f);
            sticker.localRotation = Quaternion.Euler(0f, 0f, -10f);
            TextMeshProUGUI stickerText = UiText.CreateOutlined(sticker, "label", flow.Strings.Get("results.newBest"),
                "body", "text.primary", "pop.green.edge");
            UiComponents.Stretch(stickerText.rectTransform);
            newBestSticker = sticker.gameObject;

            chipMoves = Chip(card, 0, "retry", flow.Strings.Get("stat.movesShort"));
            chipTides = Chip(card, 1, "waves", flow.Strings.Get("stat.tidesShort"));
            chipRescued = Chip(card, 2, "fish", flow.Strings.Get("stat.rescuedShort"));

            coinRow = UiComponents.Rect(card, "coinRow", new Vector2(860f, 150f));
            coinRow.anchorMin = coinRow.anchorMax = new Vector2(0.5f, 0f);
            coinRow.anchoredPosition = new Vector2(0f, 120f);
            PopKit.Well(coinRow, "well", "panel.well", 40f);
            coins = UiComponents.CoinCounterComponent(coinRow);
            var coinsRt = (RectTransform)coins.transform;
            coinsRt.anchorMin = coinsRt.anchorMax = new Vector2(0f, 0.5f);
            coinsRt.pivot = new Vector2(0f, 0.5f);
            coinsRt.sizeDelta = new Vector2(300f, 80f);
            coinsRt.anchoredPosition = new Vector2(40f, 0f);
            doubleCoins = UiComponents.ButtonReward(coinRow, "double", flow.Strings.Get("results.doubleCoins"),
                OnDoubleCoins);
            var doubleRt = (RectTransform)doubleCoins.transform;
            doubleRt.anchorMin = doubleRt.anchorMax = new Vector2(1f, 0.5f);
            doubleRt.pivot = new Vector2(1f, 0.5f);
            doubleRt.sizeDelta = new Vector2(440f, 124f);
            doubleRt.anchoredPosition = new Vector2(-14f, 4f);

            // GDD §15.2/§15.1: what your buddy added and how many powers fired.
            bonusLine = UiText.Create(card, "bonus", "", "body", "text.inkSoft");
            bonusLine.rectTransform.anchorMin = bonusLine.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            bonusLine.rectTransform.sizeDelta = new Vector2(860f, 50f);

            // GDD §16.6: a quiet "next egg" pip row — egg news is never a popup.
            eggRow = UiComponents.Rect(card, "eggRow", new Vector2(520f, 52f));
            eggRow.anchorMin = eggRow.anchorMax = new Vector2(0.5f, 0f);
            RectTransform egg = TidelingUi.Egg(eggRow, "egg", 50f);
            egg.anchorMin = egg.anchorMax = new Vector2(0f, 0.5f);
            egg.anchoredPosition = new Vector2(25f, 2f);
            TextMeshProUGUI next = UiText.Create(eggRow, "label", flow.Strings.Get("results.nextEgg"), "caption",
                "text.inkSoft", TextAlignmentOptions.MidlineLeft);
            next.rectTransform.anchorMin = next.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            next.rectTransform.pivot = new Vector2(0f, 0.5f);
            next.rectTransform.sizeDelta = new Vector2(240f, 44f);
            next.rectTransform.anchoredPosition = new Vector2(66f, 0f);
            eggPips = TidelingUi.Pips(eggRow, "pips", flow.Pals.Eggs.BoardsPerEgg, 30f);
            var pipsRt = (RectTransform)eggPips.transform;
            pipsRt.anchorMin = pipsRt.anchorMax = new Vector2(1f, 0.5f);
            pipsRt.pivot = new Vector2(1f, 0.5f);
            pipsRt.anchoredPosition = Vector2.zero;
        }

        private static TextMeshProUGUI Chip(RectTransform card, int index, string icon, string label)
        {
            RectTransform chip = UiComponents.Rect(card, $"chip{index}", new Vector2(270f, 170f));
            chip.anchorMin = chip.anchorMax = new Vector2(0.5f, 1f);
            chip.anchoredPosition = new Vector2((index - 1) * 288f, -480f);
            PopKit.Well(chip, "well", "panel.well", 32f);
            PopKit.Icon(chip, "icon", icon, "pop.blue", 52f, "panel.edge", 6);
            ((RectTransform)chip.Find("icon")).anchoredPosition = new Vector2(0f, 46f);
            TextMeshProUGUI value = UiText.Create(chip, "value", "0", "heading", "text.ink");
            UiComponents.Place(value.rectTransform, new Vector2(0.5f, 0.42f), new Vector2(250f, 56f));
            TextMeshProUGUI caption = UiText.Create(chip, "label", label, "micro", "text.inkSoft");
            UiComponents.Place(caption.rectTransform, new Vector2(0.5f, 0.15f), new Vector2(250f, 34f));
            return value;
        }

        private static void Top(RectTransform rt, float y, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(0f, y);
        }

        private void BuildButtons(RectTransform root)
        {
            RectTransform row = UiComponents.Rect(root, "buttons", new Vector2(980f, ButtonHeight));
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 0f);
            row.pivot = new Vector2(0.5f, 0f);
            row.anchoredPosition = new Vector2(0f, ButtonsBottom);

            Button home = PopKit.IconButton(row, "home", "map", PopTone.Blue, OnMap, new Vector2(200f, ButtonHeight));
            Side((RectTransform)home.transform, 0f, 200f);
            retry = PopKit.IconButton(row, "retry", "retry", PopTone.Yellow, OnRetry, new Vector2(200f, ButtonHeight));
            Side((RectTransform)retry.transform, 220f, 200f);
            primary = PopKit.Button(row, "next", flow.Strings.Get("results.next"), PopTone.Green, OnPrimary,
                new Vector2(540f, ButtonHeight), "display");
            Side((RectTransform)primary.transform, 440f, 540f);

            // ROADMAP M9: convert the annoyance moment — after the session's 2nd
            // interstitial, one tap to never see them again.
            upsell = UiComponents.ButtonGhost(root, "upsell", flow.Strings.Get("upsell.removeAds"),
                () => flow.GoTo(FlowScreen.Shop));
            var upsellRt = (RectTransform)upsell.transform;
            upsellRt.anchorMin = upsellRt.anchorMax = new Vector2(0.5f, 0f);
            upsellRt.sizeDelta = new Vector2(900f, 80f);
            upsellRt.anchoredPosition = new Vector2(0f, 60f);
        }

        private static void Side(RectTransform rt, float left, float width)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(width, ButtonHeight);
            rt.anchoredPosition = new Vector2(left, 0f);
        }

        /// <summary>The wide green button: Next (voyage win), Retry (voyage loss), Play (endless / event).</summary>
        private void OnPrimary()
        {
            RunOutcome outcome = flow.LastOutcome!;
            if (outcome.Mode != GameMode.Voyage)
            {
                OnRetry();
                return;
            }

            if (!outcome.Won)
            {
                OnRetry();
                return;
            }

            (int zone, int index) = outcome.LevelIndex >= 20
                ? (outcome.Zone + 1, 1)
                : (outcome.Zone, outcome.LevelIndex + 1);
            if (zone <= 10)
            {
                flow.StartVoyageLevel(zone, index);
            }
            else
            {
                flow.GoTo(FlowScreen.ZoneMap);
            }
        }

        private void OnRetry()
        {
            RunOutcome outcome = flow.LastOutcome!;
            if (outcome.Mode == GameMode.Endless)
            {
                flow.StartEndless();
            }
            else if (outcome.Mode == GameMode.Event)
            {
                flow.StartEvent();
            }
            else
            {
                flow.StartVoyageLevel(outcome.Zone, outcome.LevelIndex);
            }
        }

        private void OnMap() =>
            flow.GoTo(flow.LastOutcome!.Mode == GameMode.Voyage ? FlowScreen.ZoneMap : FlowScreen.Home);

        private void OnDoubleCoins()
        {
            if (flow.TryDoubleCoinsViaAd())
            {
                Refresh();
                coins.AnimateTo(flow.LastOutcome!.CoinsAwarded * 2);
            }
        }

        public void Refresh()
        {
            RunOutcome? outcome = flow.LastOutcome;
            if (outcome == null)
            {
                return;
            }

            bool endless = outcome.Mode == GameMode.Endless;
            bool tide = outcome.Mode == GameMode.Event;
            bool replay = endless || tide;
            bool win = outcome.Won;
            bool voyageWin = win && outcome.Mode == GameMode.Voyage;

            score.text = ShareCard.GroupThousands(outcome.Score);
            chipMoves.text = outcome.Moves.ToString();
            chipTides.text = outcome.TidesSurvived.ToString();
            chipRescued.text = outcome.Rescues.ToString();

            burst.gameObject.SetActive(voyageWin || (tide && win) || (endless && outcome.NewEndlessBest));
            stars.gameObject.SetActive(voyageWin);
            moodBadge.gameObject.SetActive(!voyageWin);
            if (voyageWin)
            {
                stars.Show(outcome.Stars, _ => UiJuice.Play("star")); // §7: star, per star
            }

            newBestSticker.SetActive(endless && outcome.NewEndlessBest);
            PopKit.FaceOf(primary).Find("label").GetComponent<TextMeshProUGUI>().text = flow.Strings.Get(
                replay ? "common.play" : win ? "results.next" : "results.retry");
            // Win: Map · Retry · Next. Loss/endless/event: Map · one wide primary.
            bool showRetry = !replay && win;
            retry.gameObject.SetActive(showRetry);
            Side((RectTransform)primary.transform, showRetry ? 440f : 220f, showRetry ? 540f : 760f);

            if (endless)
            {
                levelLabel.text = flow.Strings.Get("home.endless");
                SetRibbon(flow.Strings.Get("endless.gameOver"), PopTone.Purple);
                SetMood("crown", PopTone.Purple);
                bestCaption.text = outcome.NewEndlessBest
                    ? flow.Strings.Get("endless.newBest")
                    : string.Format(flow.Strings.Get("endless.best"), ShareCard.GroupThousands(flow.Meta.EndlessBest));
                if (outcome.NewEndlessBest && isActiveAndEnabled)
                {
                    // Slap-on from slightly oversized: if a transition interrupts
                    // it, the sticker is still (near) full size, never tiny.
                    Transform stickerRt = newBestSticker.transform;
                    Tween.Run(this, "t.base", "easeOutQuart",
                        u => stickerRt.localScale = Vector3.one * Mathf.Lerp(1.35f, 1f, u),
                        () => stickerRt.localScale = Vector3.one);
                    UiJuice.Play("streak");
                }
            }
            else if (tide && win)
            {
                levelLabel.text = ExpansionText.EventLabel(flow, outcome);
                SetRibbon(flow.Strings.Get("results.eventWin"), PopTone.Coral);
                SetMood("trophy", PopTone.Yellow);
                bestCaption.text = outcome.EventFirstWin
                    ? string.Format(flow.Strings.Get("results.eventReward"),
                        ExpansionText.DecorationName(flow, outcome.EventRewardDecoration))
                    : flow.Strings.Get("event.won");
            }
            else
            {
                levelLabel.text = tide
                    ? ExpansionText.EventLabel(flow, outcome)
                    : string.Format(flow.Strings.Get("results.zoneLevel"), outcome.Zone, outcome.LevelIndex);
                bestCaption.text = "";
                if (win)
                {
                    SetRibbon(flow.Strings.Get("results.win"), PopTone.Coral);
                }
                else
                {
                    SetRibbon(flow.Strings.Get(outcome.Status switch
                    {
                        GameStatus.LostDrowned => "results.lose.drown",
                        GameStatus.LostStuck => "results.lose.stuck",
                        _ => "results.lose.creature",
                    }), PopTone.Navy);
                    switch (outcome.Status)
                    {
                        case GameStatus.LostDrowned:
                            SetMood("drop", PopTone.Blue);
                            break;
                        case GameStatus.LostStuck:
                            SetMood("close", PopTone.Coral);
                            break;
                        default:
                            SetMood("fish", PopTone.Navy);
                            break;
                    }
                }
            }

            string bonus = ExpansionText.RunBonus(flow, outcome);
            bool coinsShown = outcome.CoinsAwarded > 0;
            coinRow.gameObject.SetActive(coinsShown);
            bonusLine.text = bonus;
            bonusLine.gameObject.SetActive(bonus.Length > 0);
            float eggY = coinsShown ? 226f : 62f;
            eggRow.anchoredPosition = new Vector2(0f, eggY);
            eggPips.Set(outcome.BoardsTowardEgg);
            bonusLine.rectTransform.anchoredPosition = new Vector2(0f, eggY + 56f);
            card.sizeDelta = new Vector2(980f, coinsShown ? 880f : bonus.Length > 0 ? 760f : 700f);
            coins.SetInstant(0);
            if (outcome.CoinsAwarded > 0)
            {
                coins.AnimateTo(outcome.DoubledClaimed ? outcome.CoinsAwarded * 2 : outcome.CoinsAwarded);
            }

            doubleCoins.gameObject.SetActive(win && outcome.CoinsAwarded > 0 && !outcome.DoubledClaimed);
            doubleCoins.interactable = flow.Ads != null && flow.Ads.RewardedAvailable && !outcome.DoubledClaimed;

            upsell.gameObject.SetActive(flow.Ads != null
                && flow.Ads.InterstitialsShownThisSession >= 2
                && (flow.Iap == null || !flow.Iap.RemoveAdsOwned));

            PlayIntro(outcome);
        }

        private void SetRibbon(string text, PopTone tone)
        {
            ribbonLabel.text = text;
            ribbonSurface.SetTone(tone);
            UiText.Outline(ribbonLabel, PopKit.Edge(tone));
            foreach (string tail in new[] { "tailL", "tailR" })
            {
                PopSurface tailSurface = ribbon.Find(tail).GetComponent<PopSurface>();
                tailSurface.SetTone(tone);
                tailSurface.Gradient.Set(PopKit.Edge(tone), PopKit.Edge(tone));
            }
        }

        private void SetMood(string iconId, PopTone tone)
        {
            moodSurface.SetTone(tone);
            moodIcon.sprite = PopSprites.Icon(iconId);
        }

        private void Update()
        {
            if (burst.gameObject.activeSelf && !ThemeRuntime.ReducedMotion)
            {
                burst.Rotate(0f, 0f, -12f * Time.deltaTime);
            }
        }

        /// <summary>§4.4: drown and stuck must FEEL different the instant the screen lands.</summary>
        private void PlayIntro(RunOutcome outcome)
        {
            StopAllCoroutines();
            intro.color = Color.clear;
            if (!isActiveAndEnabled || outcome.Won)
            {
                return;
            }

            if (outcome.Status == GameStatus.LostDrowned)
            {
                StartCoroutine(DrownIntro());
            }
            else if (outcome.Status == GameStatus.LostStuck)
            {
                StartCoroutine(StuckFlash());
            }
        }

        private IEnumerator DrownIntro()
        {
            // 600ms: the tint dips in from the top in water.calm, then settles out.
            Color tint = ThemeRuntime.Color("water.calm.top");
            float t = 0f;
            const float life = 0.6f;
            while (t < life)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / life);
                float alpha = 0.55f * Mathf.Sin(Mathf.PI * u);
                intro.color = new Color(tint.r, tint.g, tint.b, alpha);
                yield return null;
            }

            intro.color = Color.clear;
        }

        private IEnumerator StuckFlash()
        {
            Color tint = ThemeRuntime.Color("block.dead");
            float t = 0f;
            float life = ThemeRuntime.Seconds("t.fast");
            while (t < life)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / life);
                intro.color = new Color(tint.r, tint.g, tint.b, 0.4f * (1f - u));
                yield return null;
            }

            intro.color = Color.clear;
        }
    }
}
