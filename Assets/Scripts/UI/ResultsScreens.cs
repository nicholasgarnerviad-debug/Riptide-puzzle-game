using System.Collections;
using Riptide.Core;
using Riptide.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// Spec §4.4 results (Voyage win / Voyage lose / Endless over): centered card
    /// stack — headline (distinct copy per status), StarTriplet on wins, stat rows,
    /// CoinCounter fly-in, Double Coins reward button (once), Next/Retry/Home.
    /// Lose-by-drown opens with a 600ms top-down water tint; lose-by-stuck with a
    /// single muted flash — the two deaths must feel different. No continue offer
    /// (GDD conflict flagged in DECISIONS.md).
    /// </summary>
    public sealed class ResultsScreen : MonoBehaviour, IScreenRefresh
    {
        private GameFlow flow = null!;
        private TextMeshProUGUI title = null!;
        private StarTriplet stars = null!;
        private TextMeshProUGUI statScore = null!;
        private TextMeshProUGUI statMoves = null!;
        private TextMeshProUGUI statTides = null!;
        private TextMeshProUGUI statRescued = null!;
        private CoinCounter coins = null!;
        private Button next = null!;
        private Button retry = null!;
        private Button doubleCoins = null!;
        private Button upsell = null!;
        private Image intro = null!;
        private TextMeshProUGUI newBestBanner = null!;

        public static RectTransform Build(RectTransform parent, GameFlow flow)
        {
            RectTransform root = ScreenChrome.Root(parent, "ResultsScreen");
            var screen = root.gameObject.AddComponent<ResultsScreen>();
            screen.flow = flow;

            RectTransform card = UiComponents.Card(root, "card", new Vector2(920f, 1500f));
            UiComponents.Place(card, new Vector2(0.5f, 0.52f), new Vector2(920f, 1500f));

            screen.title = UiText.Create(card, "title", "", "title", "text.primary");
            UiComponents.Place(screen.title.rectTransform, new Vector2(0.5f, 0.90f), new Vector2(860f, 110f));

            // Genre pass (spec §12.4): beating the personal best is a MOMENT, not
            // a stat-row footnote — one coin-gold banner beat over the headline.
            screen.newBestBanner = UiText.Create(card, "newBest",
                flow.Strings.Get("results.newBest"), "heading", "coin");
            UiComponents.Place(screen.newBestBanner.rectTransform, new Vector2(0.5f, 0.97f), new Vector2(700f, 70f));
            screen.newBestBanner.gameObject.SetActive(false);

            screen.stars = UiComponents.StarTripletComponent(card);
            UiComponents.Place((RectTransform)screen.stars.transform, new Vector2(0.5f, 0.76f), new Vector2(420f, 130f));

            screen.statScore = Stat(card, "statScore", 0.645f);
            screen.statMoves = Stat(card, "statMoves", 0.585f);
            screen.statTides = Stat(card, "statTides", 0.525f);
            screen.statRescued = Stat(card, "statRescued", 0.465f);

            screen.coins = UiComponents.CoinCounterComponent(card);
            UiComponents.Place((RectTransform)screen.coins.transform, new Vector2(0.5f, 0.38f), new Vector2(300f, 72f));

            screen.doubleCoins = UiComponents.ButtonReward(card, "double",
                flow.Strings.Get("results.doubleCoins"), screen.OnDoubleCoins);
            UiComponents.Place((RectTransform)screen.doubleCoins.transform, new Vector2(0.5f, 0.28f), new Vector2(620f, 110f));

            screen.next = UiComponents.ButtonPrimary(card, "next", flow.Strings.Get("results.next"), screen.OnNext);
            UiComponents.Place((RectTransform)screen.next.transform, new Vector2(0.5f, 0.17f), new Vector2(620f, 124f));

            screen.retry = UiComponents.ButtonSecondary(card, "retry", flow.Strings.Get("results.retry"), screen.OnRetry);
            UiComponents.Place((RectTransform)screen.retry.transform, new Vector2(0.5f, 0.075f), new Vector2(620f, 110f));

            Button home = UiComponents.ButtonGhost(root, "home", flow.Strings.Get("results.map"), screen.OnMap);
            UiComponents.Place((RectTransform)home.transform, new Vector2(0.5f, 0.03f), new Vector2(420f, 84f));

            // ROADMAP M9: convert the annoyance moment — after the session's 2nd
            // interstitial, one tap to never see them again.
            screen.upsell = UiComponents.ButtonGhost(root, "upsell",
                flow.Strings.Get("upsell.removeAds"), () => flow.GoTo(FlowScreen.Shop));
            UiComponents.Place((RectTransform)screen.upsell.transform, new Vector2(0.5f, 0.085f), new Vector2(720f, 76f));

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

        private static TextMeshProUGUI Stat(RectTransform card, string name, float y)
        {
            TextMeshProUGUI stat = UiText.Create(card, name, "", "body", "text.secondary");
            UiComponents.Place(stat.rectTransform, new Vector2(0.5f, y), new Vector2(820f, 60f));
            return stat;
        }

        private void OnNext()
        {
            RunOutcome outcome = flow.LastOutcome!;
            if (outcome.Mode == GameMode.Endless)
            {
                flow.StartEndless();
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
            else
            {
                flow.StartVoyageLevel(outcome.Zone, outcome.LevelIndex);
            }
        }

        private void OnMap() =>
            flow.GoTo(flow.LastOutcome!.Mode == GameMode.Endless ? FlowScreen.Home : FlowScreen.ZoneMap);

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

            statScore.text = string.Format(flow.Strings.Get("stat.score"), ShareCard.GroupThousands(outcome.Score));
            statMoves.text = string.Format(flow.Strings.Get("stat.moves"), outcome.Moves);
            statTides.text = string.Format(flow.Strings.Get("stat.tides"), outcome.TidesSurvived);
            statRescued.text = string.Format(flow.Strings.Get("stat.rescued"), outcome.Rescues);

            bool win = outcome.Won;
            stars.gameObject.SetActive(win && outcome.Mode == GameMode.Voyage);
            if (win && outcome.Mode == GameMode.Voyage)
            {
                stars.Show(outcome.Stars, _ => UiJuice.Play("star")); // §7: star, per star
            }

            if (outcome.Mode == GameMode.Endless)
            {
                title.text = flow.Strings.Get("endless.gameOver");
                statMoves.text = outcome.NewEndlessBest
                    ? flow.Strings.Get("endless.newBest")
                    : string.Format(flow.Strings.Get("endless.best"), ShareCard.GroupThousands(flow.Meta.EndlessBest));
                next.GetComponentInChildren<TextMeshProUGUI>().text = flow.Strings.Get("common.play");
                retry.gameObject.SetActive(false);
                newBestBanner.gameObject.SetActive(outcome.NewEndlessBest);
                if (outcome.NewEndlessBest && isActiveAndEnabled)
                {
                    RectTransform bannerRt = newBestBanner.rectTransform;
                    Tween.Run(this, "t.fast", "linear",
                        u => bannerRt.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(u * Mathf.PI)),
                        () => bannerRt.localScale = Vector3.one);
                    UiJuice.Play("streak");
                }
            }
            else
            {
                newBestBanner.gameObject.SetActive(false);
                next.GetComponentInChildren<TextMeshProUGUI>().text = flow.Strings.Get("results.next");
                retry.gameObject.SetActive(true);
                title.text = win
                    ? flow.Strings.Get("results.win")
                    : flow.Strings.Get(outcome.Status switch
                    {
                        GameStatus.LostDrowned => "results.lose.drown",
                        GameStatus.LostStuck => "results.lose.stuck",
                        _ => "results.lose.creature",
                    });
                next.gameObject.SetActive(win);
            }

            coins.gameObject.SetActive(outcome.CoinsAwarded > 0);
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

    /// <summary>
    /// Spec §4.5 daily intro: date headline, the ritual line, streak flame, one
    /// tap to dive. Honors the attempted-today lock.
    /// </summary>
    public sealed class DailyIntroScreen : MonoBehaviour, IScreenRefresh
    {
        private GameFlow flow = null!;
        private TextMeshProUGUI title = null!;
        private TextMeshProUGUI body = null!;
        private StreakFlame streak = null!;
        private Button dive = null!;

        public static RectTransform Build(RectTransform parent, GameFlow flow)
        {
            RectTransform root = ScreenChrome.Root(parent, "DailyIntroScreen");
            var screen = root.gameObject.AddComponent<DailyIntroScreen>();
            screen.flow = flow;

            // The ritual gets an identity: a glowing sun mark above the date.
            RectTransform sunGlow = UiComponents.Rect(root, "sunGlow", new Vector2(360f, 360f));
            UiComponents.Place(sunGlow, new Vector2(0.5f, 0.845f), new Vector2(360f, 360f));
            var sunGlowImage = sunGlow.gameObject.AddComponent<Image>();
            sunGlowImage.sprite = MenuSprites.SoftGlow();
            sunGlowImage.raycastTarget = false;
            ThemedElement.Bind(sunGlow.gameObject, "glow.primary");
            RectTransform sun = UiComponents.Rect(root, "sun", new Vector2(150f, 150f));
            UiComponents.Place(sun, new Vector2(0.5f, 0.845f), new Vector2(150f, 150f));
            var sunImage = sun.gameObject.AddComponent<Image>();
            sunImage.sprite = MenuSprites.Icon("sun");
            sunImage.raycastTarget = false;
            ThemedElement.Bind(sun.gameObject, "warning");

            screen.title = UiText.Create(root, "title", "", "title", "text.primary");
            UiComponents.Place(screen.title.rectTransform, new Vector2(0.5f, 0.72f), new Vector2(920f, 120f));

            screen.body = UiText.Create(root, "body", "", "body", "text.secondary");
            UiComponents.Place(screen.body.rectTransform, new Vector2(0.5f, 0.61f), new Vector2(900f, 80f));

            screen.streak = UiComponents.StreakFlameComponent(root);
            UiComponents.Place((RectTransform)screen.streak.transform, new Vector2(0.5f, 0.52f), new Vector2(240f, 64f));

            screen.dive = UiComponents.ButtonPrimary(root, "dive", flow.Strings.Get("daily.intro.dive"),
                () => flow.StartDaily());
            UiComponents.Place((RectTransform)screen.dive.transform, new Vector2(0.5f, 0.38f), new Vector2(640f, 140f));

            Button back = UiComponents.ButtonGhost(root, "back", flow.Strings.Get("common.back"),
                () => flow.GoTo(FlowScreen.Home));
            UiComponents.Place((RectTransform)back.transform, new Vector2(0.5f, 0.10f), new Vector2(400f, 84f));

            screen.Refresh();
            return root;
        }

        public void Refresh()
        {
            long today = flow.Meta.TodayEpochDay();
            title.text = string.Format(flow.Strings.Get("daily.title"), flow.Economy.Daily.DailyNumber(today));

            bool canAttempt = flow.Meta.CanAttemptDailyToday();
            body.text = canAttempt
                ? string.Format(flow.Strings.Get("daily.intro.body"), flow.Economy.Daily.SurviveTides)
                : flow.Strings.Get("daily.attempted");
            dive.interactable = canAttempt;

            int current = flow.Meta.Streak.Current;
            streak.gameObject.SetActive(current > 0);
            streak.Set(current, pulse: false);
        }
    }

    /// <summary>
    /// Spec §4.5 daily results: tide pips, score, the ShareCard preview rendered
    /// exactly as it will paste, Share primary, retry reward (once, if failed),
    /// coin retry + streak freeze (GDD 5.2), Home.
    /// </summary>
    public sealed class DailyResultsScreen : MonoBehaviour, IScreenRefresh
    {
        private GameFlow flow = null!;
        private TextMeshProUGUI title = null!;
        private TextMeshProUGUI result = null!;
        private TextMeshProUGUI tides = null!;
        private ProgressPips pips = null!;
        private RectTransform cardPanel = null!;
        private TextMeshProUGUI streak = null!;
        private Button share = null!;
        private Button retry = null!;
        private Button retryCoins = null!;
        private Button freeze = null!;
        private ScreenManager? manager;
        private RunOutcome? juicedOutcome;
        private string previewRaw = "";

        /// <summary>The share PAYLOAD — tests assert it equals the Core golden verbatim.
        /// (The card renders it visually: square rows for the bar, bullets for emoji.)</summary>
        public string PreviewText => previewRaw;

        public static RectTransform Build(RectTransform parent, GameFlow flow)
        {
            RectTransform root = ScreenChrome.Root(parent, "DailyResultsScreen");
            var screen = root.gameObject.AddComponent<DailyResultsScreen>();
            screen.flow = flow;
            screen.manager = parent.GetComponentInParent<ScreenManager>();

            screen.title = UiText.Create(root, "title", "", "title", "text.primary");
            UiComponents.Place(screen.title.rectTransform, new Vector2(0.5f, 0.92f), new Vector2(920f, 100f));

            screen.result = UiText.Create(root, "result", "", "heading", "accent.primary");
            UiComponents.Place(screen.result.rectTransform, new Vector2(0.5f, 0.855f), new Vector2(920f, 80f));

            screen.tides = UiText.Create(root, "tides", "", "body", "text.secondary");
            UiComponents.Place(screen.tides.rectTransform, new Vector2(0.5f, 0.80f), new Vector2(700f, 60f));
            screen.pips = UiComponents.ProgressPipsComponent(root, flow.Economy.Daily.SurviveTides);
            UiComponents.Place((RectTransform)screen.pips.transform, new Vector2(0.5f, 0.765f), new Vector2(820f, 40f));

            screen.cardPanel = UiComponents.Card(root, "cardPanel", new Vector2(860f, 460f));
            UiComponents.Place(screen.cardPanel, new Vector2(0.5f, 0.585f), new Vector2(860f, 460f));

            screen.streak = UiText.Create(root, "streak", "", "body", "text.secondary");
            UiComponents.Place(screen.streak.rectTransform, new Vector2(0.5f, 0.385f), new Vector2(800f, 60f));

            screen.share = UiComponents.ButtonPrimary(root, "share", flow.Strings.Get("daily.share"),
                screen.OnShare);
            UiComponents.Place((RectTransform)screen.share.transform, new Vector2(0.5f, 0.295f), new Vector2(640f, 130f));

            screen.retry = UiComponents.ButtonReward(root, "retry", flow.Strings.Get("daily.retry"),
                screen.OnRetry);
            UiComponents.Place((RectTransform)screen.retry.transform, new Vector2(0.28f, 0.185f), new Vector2(480f, 100f));

            screen.retryCoins = UiComponents.ButtonSecondary(root, "retryCoins",
                string.Format(flow.Strings.Get("daily.retryCoins"), flow.Economy.Coins.DailyRetryCost),
                screen.OnRetryWithCoins);
            UiComponents.Place((RectTransform)screen.retryCoins.transform, new Vector2(0.73f, 0.185f), new Vector2(480f, 100f));

            screen.freeze = UiComponents.ButtonSecondary(root, "freeze", "", screen.OnBuyFreeze);
            UiComponents.Place((RectTransform)screen.freeze.transform, new Vector2(0.28f, 0.095f), new Vector2(480f, 95f));

            Button home = UiComponents.ButtonGhost(root, "home", flow.Strings.Get("common.back"),
                () => flow.GoTo(FlowScreen.Home));
            UiComponents.Place((RectTransform)home.transform, new Vector2(0.73f, 0.095f), new Vector2(400f, 90f));

            screen.Refresh();
            return root;
        }

        private void OnShare()
        {
            RunOutcome? outcome = flow.LastOutcome;
            if (outcome == null)
            {
                return;
            }

            // §6.4: renders the 1080×1350 card + fires the platform share
            // (Android text intent / editor clipboard).
            ShareCardRenderer.Share(outcome.ShareCardText);
            manager?.Toasts.Show(flow.Strings.Get("daily.shared"));
        }

        private void OnRetry()
        {
            if (!flow.TryDailyRetryViaAd())
            {
                manager?.Toasts.Show(flow.Strings.Get("errors.ad_unavailable"));
            }
        }

        private void OnRetryWithCoins() => flow.StartDailyRetryWithCoins();

        /// <summary>
        /// ROADMAP M7: render the share string as a composition — the 🟦/⬛ water
        /// bar becomes colored squares, other emoji become bullets — so the
        /// preview shows no tofu boxes while the PAYLOAD stays byte-identical.
        /// </summary>
        private void RenderPreview(string text)
        {
            foreach (Transform child in cardPanel)
            {
                if (child.name != "stroke")
                {
                    Destroy(child.gameObject);
                }
            }

            string[] lines = text.Split('\n');
            float y = 0.86f;
            foreach (string line in lines)
            {
                if (line.Contains("\U0001F7E6") || line.IndexOf('⬛') >= 0)
                {
                    RenderBarRow(line, y);
                }
                else
                {
                    var sb = new System.Text.StringBuilder(line.Length);
                    for (int i = 0; i < line.Length; i++)
                    {
                        if (char.IsSurrogate(line[i]))
                        {
                            if (!char.IsLowSurrogate(line[i]))
                            {
                                sb.Append('•');
                            }
                        }
                        else if (line[i] > 0x2000 && line[i] != '·')
                        {
                            sb.Append('•');
                        }
                        else
                        {
                            sb.Append(line[i]);
                        }
                    }

                    TextMeshProUGUI row = UiText.Create(cardPanel, "line", sb.ToString().Trim(),
                        "caption", "text.primary");
                    UiComponents.Place(row.rectTransform, new Vector2(0.5f, y), new Vector2(800f, 50f));
                }

                y -= 0.155f;
            }
        }

        private void RenderBarRow(string line, float y)
        {
            var cells = new System.Collections.Generic.List<bool>(); // true = water
            for (int i = 0; i < line.Length; i++)
            {
                if (char.IsHighSurrogate(line[i]) && i + 1 < line.Length)
                {
                    cells.Add(char.ConvertToUtf32(line[i], line[i + 1]) == 0x1F7E6);
                    i++;
                }
                else if (line[i] == '⬛')
                {
                    cells.Add(false);
                }
            }

            const float size = 44f;
            float startX = -(cells.Count - 1) * 0.5f * (size + 8f);
            for (int i = 0; i < cells.Count; i++)
            {
                var go = new GameObject($"bar{i}", typeof(RectTransform));
                go.transform.SetParent(cardPanel, false);
                var rt = (RectTransform)go.transform;
                UiComponents.Place(rt, new Vector2(0.5f, y), new Vector2(size, size));
                rt.anchoredPosition = new Vector2(startX + i * (size + 8f), rt.anchoredPosition.y);
                var image = go.AddComponent<Image>();
                image.sprite = SpriteFactory.RoundedFill();
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 14f * (100f / 64f) / 10f;
                image.raycastTarget = false;
                ThemedElement.Bind(go, cells[i] ? "water.calm.top" : "bg.raised");
            }
        }

        private void OnBuyFreeze()
        {
            if (flow.Meta.TryBuyStreakFreeze(flow.Economy.Coins.StreakFreezeCost))
            {
                Refresh();
            }
        }

        public void Refresh()
        {
            RunOutcome? outcome = flow.LastOutcome;
            if (outcome == null)
            {
                return;
            }

            title.text = string.Format(flow.Strings.Get("daily.title"), outcome.DailyNumber);
            result.text = flow.Strings.Get(outcome.Won ? "daily.result.win" : "daily.result.lose");
            int target = flow.Economy.Daily.SurviveTides;
            tides.text = string.Format(flow.Strings.Get("daily.tides"),
                Mathf.Min(outcome.TidesSurvived, target), target);
            pips.SetFilled(Mathf.Min(outcome.TidesSurvived, target));
            previewRaw = outcome.ShareCardText;
            RenderPreview(previewRaw);
            streak.text = string.Format(flow.Strings.Get("daily.streak"),
                flow.Meta.Streak.Current, flow.Meta.Streak.Best);

            // §7: streak milestone juice, once per outcome.
            if (outcome.Won && juicedOutcome != outcome)
            {
                juicedOutcome = outcome;
                UiJuice.Play("streak");
            }

            bool retryAvailable = !outcome.Won && flow.Meta.DailyRetryAvailable();
            retry.gameObject.SetActive(retryAvailable);
            retryCoins.gameObject.SetActive(retryAvailable);
            retryCoins.interactable = flow.Meta.CanAfford(flow.Economy.Coins.DailyRetryCost);

            if (flow.Meta.Streak.FreezesHeld > 0)
            {
                freeze.gameObject.SetActive(true);
                freeze.GetComponentInChildren<TextMeshProUGUI>().text = flow.Strings.Get("daily.freezeOwned");
                freeze.interactable = false;
            }
            else
            {
                bool canBuy = flow.Meta.CanBuyStreakFreeze();
                freeze.gameObject.SetActive(canBuy);
                freeze.GetComponentInChildren<TextMeshProUGUI>().text =
                    string.Format(flow.Strings.Get("daily.buyFreeze"), flow.Economy.Coins.StreakFreezeCost);
                freeze.interactable = flow.Meta.CanAfford(flow.Economy.Coins.StreakFreezeCost);
            }
        }
    }
}
