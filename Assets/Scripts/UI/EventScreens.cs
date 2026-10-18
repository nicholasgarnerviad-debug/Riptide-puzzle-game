using Riptide.Core;
using Riptide.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// GDD §15.4 Event Tide intro, Tidal Pop edition: this week's featured
    /// creature on a sunburst badge (days-left sticker), the event's name ribbon,
    /// a white card with its pitch, the goal, the week's best progress and the
    /// one-time reward, and one huge PLAY. Unlimited attempts; the board is the
    /// week's shared seed.
    /// </summary>
    public sealed class EventIntroScreen : MonoBehaviour, IScreenRefresh
    {
        private GameFlow flow = null!;
        private TextMeshProUGUI ribbonLabel = null!;
        private TextMeshProUGUI daysLeft = null!;
        private TextMeshProUGUI body = null!;
        private TextMeshProUGUI goal = null!;
        private Image goalIcon = null!;
        private PopBar best = null!;
        private TextMeshProUGUI reward = null!;
        private Image rewardIcon = null!;
        private RectTransform badge = null!;
        private Image creature = null!;
        private int featured;
        private float idle;
        private float nextBlink;

        public static RectTransform Build(RectTransform parent, GameFlow flow)
        {
            RectTransform root = ScreenChrome.Root(parent, "EventIntroScreen");
            var screen = root.gameObject.AddComponent<EventIntroScreen>();
            screen.flow = flow;
            ScreenChrome.Header(root, flow, flow.Strings.Get("event.title"), () => flow.GoTo(FlowScreen.Home),
                coins: false);

            RectTransform burst = UiComponents.Rect(root, "burst", new Vector2(1100f, 1100f));
            burst.anchorMin = burst.anchorMax = new Vector2(0.5f, 1f);
            burst.anchoredPosition = new Vector2(0f, -450f);
            Image rays = PopKit.Img(burst, "rays", PopSprites.Sunburst(), "burst");
            UiComponents.Stretch(rays.rectTransform);

            screen.badge = UiComponents.Rect(root, "badge", new Vector2(360f, 370f));
            screen.badge.anchorMin = screen.badge.anchorMax = new Vector2(0.5f, 1f);
            screen.badge.anchoredPosition = new Vector2(0f, -440f);
            PopSurface surface = PopKit.Surface(screen.badge, PopTone.Coral, 180f, PopKit.Border, lip: 16f);
            screen.creature = PopKit.Img(surface.Face, "creature", CreatureSprites.For(0), "pet.eye");
            UiComponents.Place(screen.creature.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(300f, 300f));

            RectTransform sticker = PopKit.Panel(screen.badge, "daysLeft", new Vector2(250f, 74f), PopTone.Yellow, 22f, 7f);
            sticker.anchorMin = sticker.anchorMax = new Vector2(1f, 1f);
            sticker.anchoredPosition = new Vector2(-10f, -20f);
            sticker.localRotation = Quaternion.Euler(0f, 0f, -10f);
            screen.daysLeft = UiText.CreateOutlined(sticker, "label", "", "body", "text.primary", "pop.yellow.edge");
            UiComponents.Stretch(screen.daysLeft.rectTransform);

            RectTransform ribbon = PopKit.Ribbon(root, "ribbon", "", PopTone.Purple, new Vector2(880f, 130f), "title");
            ribbon.anchorMin = ribbon.anchorMax = new Vector2(0.5f, 1f);
            ribbon.anchoredPosition = new Vector2(0f, -710f);
            screen.ribbonLabel = ribbon.Find("band/face/label").GetComponent<TextMeshProUGUI>();

            screen.BuildCard(root);

            Button play = PopKit.Button(root, "play", flow.Strings.Get("home.play"), PopTone.Green,
                flow.StartEvent, new Vector2(860f, 230f), "display", "play");
            var playRt = (RectTransform)play.transform;
            playRt.anchorMin = playRt.anchorMax = new Vector2(0.5f, 0f);
            playRt.pivot = new Vector2(0.5f, 0f);
            playRt.anchoredPosition = new Vector2(0f, 150f);

            screen.Refresh();
            return root;
        }

        private void BuildCard(RectTransform root)
        {
            // The card centers in the band between the ribbon and PLAY, so tall
            // phones get air on both sides instead of one big gap.
            const float height = 560f;
            RectTransform zone = UiComponents.Rect(root, "cardZone", Vector2.zero);
            UiComponents.Stretch(zone);
            zone.offsetMin = new Vector2(0f, 400f);
            zone.offsetMax = new Vector2(0f, -790f);
            RectTransform card = UiComponents.Card(zone, "rules", new Vector2(940f, height));
            UiComponents.Place(card, new Vector2(0.5f, 0.5f), new Vector2(940f, height));

            body = UiText.Create(card, "body", "", "body", "text.ink");
            body.enableWordWrapping = true;
            Top(body.rectTransform, -84f, 840f, 110f);

            goalIcon = PopKit.Icon(card, "goalIcon", "star", "pop.yellow", 72f, "panel.edge", 7);
            var iconRt = (RectTransform)goalIcon.transform.parent;
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 1f);
            iconRt.anchoredPosition = new Vector2(110f, -196f);
            goal = UiText.Create(card, "goal", "", "heading", "text.ink", TextAlignmentOptions.MidlineLeft);
            goal.rectTransform.anchorMin = goal.rectTransform.anchorMax = new Vector2(0f, 1f);
            goal.rectTransform.pivot = new Vector2(0f, 0.5f);
            goal.rectTransform.sizeDelta = new Vector2(720f, 80f);
            goal.rectTransform.anchoredPosition = new Vector2(170f, -196f);

            best = PopBar.Create(card, "best", new Vector2(840f, 66f), PopTone.Coral);
            Top(best.Rect, -286f, 840f, 66f);

            RectTransform well = UiComponents.Rect(card, "rewardWell", new Vector2(860f, 150f));
            well.anchorMin = well.anchorMax = new Vector2(0.5f, 0f);
            well.pivot = new Vector2(0.5f, 0f);
            well.anchoredPosition = new Vector2(0f, 44f);
            PopKit.Well(well, "well", "panel.well", 40f);
            rewardIcon = PopKit.Icon(well, "icon", "gift", "pop.coral", 90f, "panel.edge", 8);
            var rewardIconRt = (RectTransform)rewardIcon.transform.parent;
            rewardIconRt.anchorMin = rewardIconRt.anchorMax = new Vector2(0f, 0.5f);
            rewardIconRt.anchoredPosition = new Vector2(90f, 0f);
            reward = UiText.Create(well, "reward", "", "body", "text.ink", TextAlignmentOptions.MidlineLeft);
            UiComponents.Stretch(reward.rectTransform);
            reward.rectTransform.offsetMin = new Vector2(160f, 0f);
            reward.rectTransform.offsetMax = new Vector2(-30f, 0f);
            reward.enableWordWrapping = true;
        }

        private static void Top(RectTransform rt, float y, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(0f, y);
        }

        public void Refresh()
        {
            EventDef e = flow.ThisWeeksEvent;
            featured = e.FeaturedSpecies;
            creature.sprite = CreatureSprites.For(featured);
            ribbonLabel.text = ExpansionText.EventName(flow, e);
            body.text = ExpansionText.EventDescription(flow, e);
            goal.text = ExpansionText.EventGoal(flow, e);
            string goalIconId = GoalIcon(e.GoalKind);
            goalIcon.sprite = PopSprites.Icon(goalIconId);
            goalIcon.transform.parent.Find("outline").GetComponent<Image>().sprite = PopSprites.IconOutline(goalIconId, 7);

            int days = flow.EventDaysLeft;
            daysLeft.text = days <= 1
                ? flow.Strings.Get("home.eventLastDay")
                : string.Format(flow.Strings.Get("home.eventDays"), days);

            int progress = flow.EventBestProgress;
            best.Set(progress, e.GoalTarget, string.Format(flow.Strings.Get("event.best"),
                ShareCard.GroupThousands(System.Math.Min(progress, e.GoalTarget)), ShareCard.GroupThousands(e.GoalTarget)));

            bool won = flow.EventWonThisWeek;
            string rewardIconId = won ? "check" : "gift";
            rewardIcon.sprite = PopSprites.Icon(rewardIconId);
            rewardIcon.transform.parent.Find("outline").GetComponent<Image>().sprite =
                PopSprites.IconOutline(rewardIconId, 8);
            ThemedElement.Bind(rewardIcon.gameObject, won ? "pop.green" : "pop.coral");
            reward.text = won
                ? flow.Strings.Get("event.won")
                : string.Format(flow.Strings.Get("event.reward"),
                    ExpansionText.DecorationName(flow, e.RewardDecoration), e.RewardCoins);
        }

        internal static string GoalIcon(EventGoalKind kind) => kind switch
        {
            EventGoalKind.Rescue => "fish",
            EventGoalKind.Rows => "rows",
            EventGoalKind.Tides => "waves",
            _ => "trophy",
        };

        private void Update()
        {
            // The featured creature blinks even under reduced motion; it only bobs without it.
            if (Time.time > nextBlink)
            {
                creature.sprite = CreatureSprites.Blink(featured);
                if (Time.time > nextBlink + 0.14f)
                {
                    creature.sprite = CreatureSprites.For(featured);
                    nextBlink = Time.time + Random.Range(2.2f, 4.5f);
                }
            }

            if (ThemeRuntime.ReducedMotion)
            {
                return;
            }

            idle += Time.deltaTime;
            badge.localRotation = Quaternion.Euler(0f, 0f, 3f * Mathf.Sin(idle * 1.8f));
            creature.rectTransform.anchoredPosition = new Vector2(0f, 8f * Mathf.Sin(idle * 2.4f));
        }
    }
}
