using Riptide.Core;
using Riptide.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// Spec §4.5 daily intro, Tidal Pop edition: a big calendar badge, the dated
    /// ribbon, the ritual line on a white card with the streak, one huge DIVE.
    /// Honors the attempted-today lock.
    /// </summary>
    public sealed class DailyIntroScreen : MonoBehaviour, IScreenRefresh
    {
        private GameFlow flow = null!;
        private TextMeshProUGUI ribbonLabel = null!;
        private TextMeshProUGUI body = null!;
        private RectTransform streakRow = null!;
        private TextMeshProUGUI streakText = null!;
        private Button dive = null!;
        private RectTransform badge = null!;
        private float idle;

        public static RectTransform Build(RectTransform parent, GameFlow flow)
        {
            RectTransform root = ScreenChrome.Root(parent, "DailyIntroScreen");
            var screen = root.gameObject.AddComponent<DailyIntroScreen>();
            screen.flow = flow;
            ScreenChrome.Header(root, flow, "", () => flow.GoTo(FlowScreen.Home), coins: false);

            RectTransform burst = UiComponents.Rect(root, "burst", new Vector2(1100f, 1100f));
            burst.anchorMin = burst.anchorMax = new Vector2(0.5f, 1f);
            burst.anchoredPosition = new Vector2(0f, -470f);
            Image rays = PopKit.Img(burst, "rays", PopSprites.Sunburst(), "burst");
            UiComponents.Stretch(rays.rectTransform);

            screen.badge = UiComponents.Rect(root, "badge", new Vector2(340f, 350f));
            screen.badge.anchorMin = screen.badge.anchorMax = new Vector2(0.5f, 1f);
            screen.badge.anchoredPosition = new Vector2(0f, -460f);
            PopSurface surface = PopKit.Surface(screen.badge, PopTone.Yellow, 170f, PopKit.Border, lip: 16f);
            PopKit.Icon(surface.Face, "calendar", "calendar", "pop.coral", 190f, "pop.yellow.edge", 10);

            RectTransform ribbon = PopKit.Ribbon(root, "ribbon", "", PopTone.Coral, new Vector2(880f, 130f), "title");
            ribbon.anchorMin = ribbon.anchorMax = new Vector2(0.5f, 1f);
            ribbon.anchoredPosition = new Vector2(0f, -720f);
            screen.ribbonLabel = ribbon.Find("band/face/label").GetComponent<TextMeshProUGUI>();

            RectTransform card = UiComponents.Card(root, "rules", new Vector2(940f, 400f));
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0f);
            card.pivot = new Vector2(0.5f, 0f);
            card.anchoredPosition = new Vector2(0f, 470f);
            screen.body = UiText.Create(card, "body", "", "heading", "text.ink");
            screen.body.enableWordWrapping = true;
            UiComponents.Place(screen.body.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(820f, 180f));

            screen.streakRow = PopKit.Panel(card, "streak", new Vector2(420f, 92f), PopTone.Coral, 46f, 7f);
            screen.streakRow.anchorMin = screen.streakRow.anchorMax = new Vector2(0.5f, 0f);
            screen.streakRow.anchoredPosition = new Vector2(0f, 90f);
            Image flame = PopKit.Icon(screen.streakRow, "flame", "flame", "pop.yellow", 64f, "pop.coral.edge", 8);
            ((RectTransform)flame.transform.parent).anchoredPosition = new Vector2(-150f, 2f);
            screen.streakText = UiText.CreateOutlined(screen.streakRow, "label", "", "body", "text.primary",
                "pop.coral.edge");
            UiComponents.Stretch(screen.streakText.rectTransform);
            screen.streakText.rectTransform.offsetMin = new Vector2(70f, 0f);

            screen.dive = PopKit.Button(root, "dive", flow.Strings.Get("daily.intro.dive"), PopTone.Green,
                () => flow.StartDaily(), new Vector2(860f, 230f), "display", "play");
            var diveRt = (RectTransform)screen.dive.transform;
            diveRt.anchorMin = diveRt.anchorMax = new Vector2(0.5f, 0f);
            diveRt.pivot = new Vector2(0.5f, 0f);
            diveRt.anchoredPosition = new Vector2(0f, 150f);

            screen.Refresh();
            return root;
        }

        public void Refresh()
        {
            long today = flow.Meta.TodayEpochDay();
            ribbonLabel.text = string.Format(flow.Strings.Get("daily.title"), flow.Economy.Daily.DailyNumber(today));

            bool canAttempt = flow.Meta.CanAttemptDailyToday();
            body.text = canAttempt
                ? string.Format(flow.Strings.Get("daily.intro.body"), flow.Economy.Daily.SurviveTides)
                : flow.Strings.Get("daily.attempted");
            dive.interactable = canAttempt;

            int current = flow.Meta.Streak.Current;
            streakRow.gameObject.SetActive(current > 0);
            streakText.text = string.Format(flow.Strings.Get("daily.intro.streak"), current);
        }

        private void Update()
        {
            if (ThemeRuntime.ReducedMotion)
            {
                return;
            }

            idle += Time.deltaTime;
            badge.localRotation = Quaternion.Euler(0f, 0f, 4f * Mathf.Sin(idle * 1.8f));
        }
    }

    /// <summary>
    /// Spec §4.5 daily results, Tidal Pop edition: dated ribbon, the result line,
    /// the tide pips on a navy bar, the ShareCard preview rendered exactly as it
    /// will paste on a white card, Share primary, retry reward (once, if failed),
    /// coin retry + streak freeze (GDD 5.2), Home.
    /// </summary>
    public sealed class DailyResultsScreen : MonoBehaviour, IScreenRefresh
    {
        private GameFlow flow = null!;
        private TextMeshProUGUI ribbonLabel = null!;
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

            RectTransform ribbon = PopKit.Ribbon(root, "ribbon", "", PopTone.Coral, new Vector2(880f, 130f), "title");
            ribbon.anchorMin = ribbon.anchorMax = new Vector2(0.5f, 1f);
            ribbon.anchoredPosition = new Vector2(0f, -120f);
            screen.ribbonLabel = ribbon.Find("band/face/label").GetComponent<TextMeshProUGUI>();

            screen.result = UiText.CreateOutlined(root, "result", "", "title", "text.primary", "panel.edge", 0.26f);
            Top(screen.result.rectTransform, -260f, 980f, 90f);

            RectTransform tideBar = PopKit.Panel(root, "tideBar", new Vector2(940f, 150f), PopTone.Navy, 40f, 8f);
            tideBar.anchorMin = tideBar.anchorMax = new Vector2(0.5f, 1f);
            tideBar.anchoredPosition = new Vector2(0f, -400f);
            screen.tides = UiText.CreateOutlined(tideBar, "tides", "", "body", "text.primary", "panel.edge");
            UiComponents.Place(screen.tides.rectTransform, new Vector2(0.5f, 0.72f), new Vector2(700f, 50f));
            screen.pips = UiComponents.ProgressPipsComponent(tideBar, flow.Economy.Daily.SurviveTides);
            UiComponents.Place((RectTransform)screen.pips.transform, new Vector2(0.5f, 0.32f), new Vector2(860f, 40f));

            screen.cardPanel = UiComponents.Card(root, "cardPanel", new Vector2(900f, 470f));
            screen.cardPanel.anchorMin = screen.cardPanel.anchorMax = new Vector2(0.5f, 1f);
            screen.cardPanel.pivot = new Vector2(0.5f, 1f);
            screen.cardPanel.anchoredPosition = new Vector2(0f, -510f);

            screen.streak = UiText.CreateOutlined(root, "streak", "", "body", "text.primary", "panel.edge");
            Top(screen.streak.rectTransform, -1030f, 800f, 56f);

            screen.share = PopKit.Button(root, "share", flow.Strings.Get("daily.share"), PopTone.Green,
                screen.OnShare, new Vector2(860f, 200f), "display", "share");
            Bottom((RectTransform)screen.share.transform, 0f, 470f);

            screen.retry = UiComponents.ButtonReward(root, "retry", flow.Strings.Get("daily.retry"), screen.OnRetry);
            Bottom((RectTransform)screen.retry.transform, -232f, 320f, new Vector2(420f, 124f));
            screen.retryCoins = UiComponents.ButtonSecondary(root, "retryCoins",
                string.Format(flow.Strings.Get("daily.retryCoins"), flow.Economy.Coins.DailyRetryCost),
                screen.OnRetryWithCoins);
            Bottom((RectTransform)screen.retryCoins.transform, 232f, 320f, new Vector2(420f, 124f));

            screen.freeze = UiComponents.ButtonSecondary(root, "freeze", "", screen.OnBuyFreeze);
            Bottom((RectTransform)screen.freeze.transform, -232f, 170f, new Vector2(420f, 124f));
            Button home = PopKit.Button(root, "home", flow.Strings.Get("results.home"), PopTone.Blue,
                () => flow.GoTo(FlowScreen.Home), new Vector2(420f, 124f), "body", "home");
            Bottom((RectTransform)home.transform, 232f, 170f, new Vector2(420f, 124f));

            screen.Refresh();
            return root;
        }

        private static void Top(RectTransform rt, float y, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(0f, y);
        }

        private static void Bottom(RectTransform rt, float x, float y, Vector2? size = null)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            if (size.HasValue)
            {
                rt.sizeDelta = size.Value;
            }

            rt.anchoredPosition = new Vector2(x, y);
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
                if (child.name.StartsWith("line") || child.name.StartsWith("bar"))
                {
                    Destroy(child.gameObject);
                }
            }

            string[] lines = text.Split('\n');
            float y = 0.84f;
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
                        "body", "text.ink");
                    UiComponents.Place(row.rectTransform, new Vector2(0.5f, y), new Vector2(820f, 54f));
                }

                y -= 0.16f;
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

            const float size = 52f;
            float startX = -(cells.Count - 1) * 0.5f * (size + 10f);
            for (int i = 0; i < cells.Count; i++)
            {
                Image cell = PopKit.Well(cardPanel, $"bar{i}", cells[i] ? "pop.blue" : "panel.shade", 14f);
                UiComponents.Place(cell.rectTransform, new Vector2(0.5f, y), new Vector2(size, size));
                cell.rectTransform.anchoredPosition = new Vector2(startX + i * (size + 10f), 0f);
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

            ribbonLabel.text = string.Format(flow.Strings.Get("daily.title"), outcome.DailyNumber);
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

            TextMeshProUGUI freezeLabel = PopKit.FaceOf(freeze).Find("label").GetComponent<TextMeshProUGUI>();
            if (flow.Meta.Streak.FreezesHeld > 0)
            {
                freeze.gameObject.SetActive(true);
                freezeLabel.text = flow.Strings.Get("daily.freezeOwned");
                freeze.interactable = false;
            }
            else
            {
                bool canBuy = flow.Meta.CanBuyStreakFreeze();
                freeze.gameObject.SetActive(canBuy);
                freezeLabel.text = string.Format(flow.Strings.Get("daily.buyFreeze"), flow.Economy.Coins.StreakFreezeCost);
                freeze.interactable = flow.Meta.CanAfford(flow.Economy.Coins.StreakFreezeCost);
            }
        }
    }
}
