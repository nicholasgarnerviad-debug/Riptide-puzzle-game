using Riptide.Core;
using Riptide.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>Voyage/Endless results (GDD 9): win/lose, stars, coins, next/retry/map, rewarded stub.</summary>
    public sealed class ResultsScreen : MonoBehaviour, IScreenRefresh
    {
        private GameFlow flow = null!;
        private Text title = null!;
        private Text stars = null!;
        private Text detail = null!;
        private Text coins = null!;
        private Button next = null!;
        private Button retry = null!;

        public static RectTransform Build(RectTransform parent, GameFlow flow)
        {
            RectTransform root = UiKit.Panel(parent, "ResultsScreen", UiKit.PanelColor);
            UiKit.Stretch(root);
            var screen = root.gameObject.AddComponent<ResultsScreen>();
            screen.flow = flow;

            screen.title = UiKit.Label(root, "title", "", 64, UiKit.TextColor);
            UiKit.Place(screen.title.rectTransform, new Vector2(0.5f, 0.82f), new Vector2(900f, 110f), Vector2.zero);

            screen.stars = UiKit.Label(root, "stars", "", 92, Palette.MeterFilled);
            UiKit.Place(screen.stars.rectTransform, new Vector2(0.5f, 0.68f), new Vector2(700f, 120f), Vector2.zero);

            screen.detail = UiKit.Label(root, "detail", "", 40, UiKit.TextDim);
            UiKit.Place(screen.detail.rectTransform, new Vector2(0.5f, 0.575f), new Vector2(800f, 80f), Vector2.zero);

            screen.coins = UiKit.Label(root, "coins", "", 48, Palette.Blocks[3]);
            UiKit.Place(screen.coins.rectTransform, new Vector2(0.5f, 0.49f), new Vector2(700f, 90f), Vector2.zero);

            Button doubleCoins = UiKit.TextButton(root, "double", flow.Strings.Get("results.doubleCoins"), 36, () => { });
            doubleCoins.interactable = false;
            UiKit.Place((RectTransform)doubleCoins.transform, new Vector2(0.5f, 0.39f), new Vector2(560f, 90f), Vector2.zero);

            screen.next = UiKit.TextButton(root, "next", flow.Strings.Get("results.next"), 46,
                screen.OnNext, UiKit.ButtonAccent);
            UiKit.Place((RectTransform)screen.next.transform, new Vector2(0.5f, 0.27f), new Vector2(560f, 110f), Vector2.zero);

            screen.retry = UiKit.TextButton(root, "retry", flow.Strings.Get("results.retry"), 42, screen.OnRetry);
            UiKit.Place((RectTransform)screen.retry.transform, new Vector2(0.5f, 0.16f), new Vector2(560f, 100f), Vector2.zero);

            Button map = UiKit.TextButton(root, "map", flow.Strings.Get("results.map"), 38, screen.OnMap);
            UiKit.Place((RectTransform)map.transform, new Vector2(0.5f, 0.06f), new Vector2(420f, 90f), Vector2.zero);

            screen.Refresh();
            return root;
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

        public void Refresh()
        {
            RunOutcome? outcome = flow.LastOutcome;
            if (outcome == null)
            {
                return;
            }

            if (outcome.Mode == GameMode.Endless)
            {
                title.text = flow.Strings.Get("endless.gameOver");
                stars.text = string.Format(flow.Strings.Get("endless.tides"), outcome.TidesSurvived);
                stars.fontSize = 44;
                detail.text = string.Format(flow.Strings.Get("endless.score"), ShareCard.GroupThousands(outcome.Score))
                    + "  ·  " + string.Format(flow.Strings.Get("endless.best"), ShareCard.GroupThousands(flow.Meta.EndlessBest));
                coins.text = outcome.NewEndlessBest
                    ? flow.Strings.Get("endless.newBest") + "  " + string.Format(flow.Strings.Get("results.coins"), outcome.CoinsAwarded)
                    : "";
                next.GetComponentInChildren<Text>().text = flow.Strings.Get("common.play");
                retry.gameObject.SetActive(false);
                return;
            }

            retry.gameObject.SetActive(true);
            next.GetComponentInChildren<Text>().text = flow.Strings.Get("results.next");
            if (outcome.Won)
            {
                title.text = flow.Strings.Get("results.win");
                stars.fontSize = 92;
                stars.text = new string('★', outcome.Stars) + new string('☆', 3 - outcome.Stars);
                detail.text = outcome.ParMoves.HasValue
                    ? string.Format(flow.Strings.Get("results.par"), outcome.ParMoves.Value, outcome.Moves)
                    : "";
                coins.text = string.Format(flow.Strings.Get("results.coins"), outcome.CoinsAwarded);
                next.gameObject.SetActive(true);
            }
            else
            {
                title.text = flow.Strings.Get(outcome.Status switch
                {
                    GameStatus.LostDrowned => "results.lose.drown",
                    GameStatus.LostStuck => "results.lose.stuck",
                    _ => "results.lose.creature",
                });
                stars.text = "";
                detail.text = "";
                coins.text = "";
                next.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>Daily results + share (GDD 3.3/9): card text to clipboard, retry hook stub.</summary>
    public sealed class DailyResultsScreen : MonoBehaviour, IScreenRefresh
    {
        private GameFlow flow = null!;
        private Text title = null!;
        private Text result = null!;
        private Text card = null!;
        private Text streak = null!;
        private Text toast = null!;
        private Button retry = null!;

        public static RectTransform Build(RectTransform parent, GameFlow flow)
        {
            RectTransform root = UiKit.Panel(parent, "DailyResultsScreen", UiKit.PanelColor);
            UiKit.Stretch(root);
            var screen = root.gameObject.AddComponent<DailyResultsScreen>();
            screen.flow = flow;

            screen.title = UiKit.Label(root, "title", "", 56, UiKit.TextColor);
            UiKit.Place(screen.title.rectTransform, new Vector2(0.5f, 0.88f), new Vector2(900f, 100f), Vector2.zero);

            screen.result = UiKit.Label(root, "result", "", 46, Palette.MeterFilled);
            UiKit.Place(screen.result.rectTransform, new Vector2(0.5f, 0.79f), new Vector2(900f, 80f), Vector2.zero);

            RectTransform cardPanel = UiKit.Panel(root, "cardPanel", new Color(0f, 0f, 0f, 0.45f));
            UiKit.Place(cardPanel, new Vector2(0.5f, 0.58f), new Vector2(820f, 420f), Vector2.zero);
            screen.card = UiKit.Label(cardPanel, "card", "", 38, UiKit.TextColor);
            UiKit.Stretch(screen.card.rectTransform, 24f);

            screen.streak = UiKit.Label(root, "streak", "", 40, UiKit.TextDim);
            UiKit.Place(screen.streak.rectTransform, new Vector2(0.5f, 0.365f), new Vector2(800f, 70f), Vector2.zero);

            Button share = UiKit.TextButton(root, "share", flow.Strings.Get("daily.share"), 48,
                screen.OnShare, UiKit.ButtonAccent);
            UiKit.Place((RectTransform)share.transform, new Vector2(0.5f, 0.26f), new Vector2(560f, 110f), Vector2.zero);

            screen.toast = UiKit.Label(root, "toast", "", 34, Palette.MeterFilled);
            UiKit.Place(screen.toast.rectTransform, new Vector2(0.5f, 0.195f), new Vector2(700f, 60f), Vector2.zero);

            screen.retry = UiKit.TextButton(root, "retry", flow.Strings.Get("daily.retry"), 40, screen.OnRetry);
            UiKit.Place((RectTransform)screen.retry.transform, new Vector2(0.5f, 0.12f), new Vector2(560f, 95f), Vector2.zero);

            Button home = UiKit.TextButton(root, "home", flow.Strings.Get("common.back"), 40,
                () => flow.GoTo(FlowScreen.Home));
            UiKit.Place((RectTransform)home.transform, new Vector2(0.5f, 0.04f), new Vector2(400f, 85f), Vector2.zero);

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

            GUIUtility.systemCopyBuffer = outcome.ShareCardText;
            toast.text = flow.Strings.Get("daily.shared");
        }

        private void OnRetry()
        {
            // GDD 3.3 retry hook — the rewarded ad arrives in Phase 7; the stub grants it.
            flow.StartDaily(isRetry: true);
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
            card.text = outcome.ShareCardText;
            streak.text = string.Format(flow.Strings.Get("daily.streak"),
                flow.Meta.Streak.Current, flow.Meta.Streak.Best);
            toast.text = "";
            retry.gameObject.SetActive(flow.Meta.DailyRetryAvailable());
        }
    }
}
