using System.Collections.Generic;
using Riptide.Core;
using Riptide.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// GDD §15.3 Quests tab: today's three quest cards (icon, line, progress bar,
    /// coin reward → Claim → Done) and the badge wall (medal, name, goal, progress
    /// or Claim). Claimable badges float to the top. Rebuilt on every show.
    /// </summary>
    public sealed class QuestsScreen : MonoBehaviour, IScreenRefresh
    {
        private const float CardWidth = 980f;
        private const float QuestHeight = 240f;
        private const float BadgeWidth = 310f;
        private const float BadgeHeight = 410f;
        private const float BadgeGap = 25f;

        private GameFlow flow = null!;
        private CoinCounter coins = null!;
        private RectTransform content = null!;

        public static RectTransform Build(RectTransform parent, GameFlow flow)
        {
            RectTransform root = ScreenChrome.Root(parent, "QuestsScreen");
            var screen = root.gameObject.AddComponent<QuestsScreen>();
            screen.flow = flow;
            screen.coins = ScreenChrome.Header(root, flow, flow.Strings.Get("quests.title"), null, coins: true)!;
            screen.content = ScreenChrome.ScrollColumn(root, ScreenChrome.TabFloorRefPx).Content;
            screen.Refresh();
            return root;
        }

        public void Refresh()
        {
            coins.SetInstant(flow.Meta.Coins);

            // Detach before destroying: Destroy is deferred, and a same-frame
            // Find (tests, the tab badge) must only ever see the new cards.
            var old = new List<Transform>();
            foreach (Transform child in content)
            {
                old.Add(child);
            }

            foreach (Transform child in old)
            {
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }

            var column = new ScreenChrome.Column(content);
            column.Space(30f);
            column.Add(Ribbon("questsRibbon", flow.Strings.Get("quests.daily"), PopTone.Coral), 110f, 34f);
            foreach (QuestView quest in flow.TodaysQuests())
            {
                column.Add(QuestCard(quest), QuestHeight, 26f);
            }

            column.Add(Caption("questsResets", flow.Strings.Get("quests.resets")), 50f, 60f);
            BuildBadges(column);
        }

        // ---------------------------------------------------------------- quests

        private RectTransform QuestCard(QuestView quest)
        {
            RectTransform card = UiComponents.Card(content, $"quest{quest.Index}", new Vector2(CardWidth, QuestHeight));
            QuestKind kind = quest.Def.Kind;
            PopTone tone = ToneFor(kind);

            RectTransform well = PopKit.Panel(card, "iconWell", new Vector2(150f, 150f), tone, 75f, 8f);
            well.anchorMin = well.anchorMax = new Vector2(0f, 0.5f);
            well.anchoredPosition = new Vector2(112f, 8f);
            if (kind == QuestKind.RescueSpecies)
            {
                Image pet = PopKit.Img(well, "creature", CreatureSprites.For(quest.Def.Species), "pet.eye");
                UiComponents.Place(pet.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(126f, 126f));
            }
            else
            {
                PopKit.Icon(well, "icon", IconFor(kind), "text.primary", 82f, PopKit.Edge(tone), 8);
            }

            TextMeshProUGUI line = UiText.Create(card, "line", ExpansionText.Quest(flow, quest.Def), "heading",
                "text.ink", TextAlignmentOptions.MidlineLeft);
            Pin(line.rectTransform, 215f, -82f, 500f, 100f);
            line.enableWordWrapping = true;
            line.enableAutoSizing = true;
            line.fontSizeMin = 30f;
            line.fontSizeMax = line.fontSize;

            PopBar bar = PopBar.Create(card, "progress", new Vector2(480f, 60f), quest.Complete ? PopTone.Green : PopTone.Blue);
            Pin(bar.Rect, 215f, -172f, 480f, 60f);
            bar.Set(quest.Progress, quest.Def.Target);

            if (quest.Claimed)
            {
                Done(card);
            }
            else if (quest.Complete)
            {
                RewardPill(card, quest.Def.Reward, new Vector2(1f, 1f), new Vector2(-140f, -54f));
                int index = quest.Index;
                Button claim = PopKit.Button(card, "claim", flow.Strings.Get("quests.claim"), PopTone.Green,
                    () => ClaimQuest(index), new Vector2(230f, 124f));
                var claimRt = (RectTransform)claim.transform;
                claimRt.anchorMin = claimRt.anchorMax = new Vector2(1f, 0f);
                claimRt.pivot = new Vector2(0.5f, 0f);
                claimRt.anchoredPosition = new Vector2(-140f, 30f);
            }
            else
            {
                RewardPill(card, quest.Def.Reward, new Vector2(1f, 0.5f), new Vector2(-140f, 8f));
            }

            return card;
        }

        private void ClaimQuest(int index)
        {
            long before = flow.Meta.Coins;
            if (flow.TryClaimQuest(index))
            {
                Claimed(before);
            }
        }

        // ---------------------------------------------------------------- badges

        private void BuildBadges(ScreenChrome.Column column)
        {
            PlayerStats stats = flow.Stats();
            var claimable = new List<BadgeDef>();
            var rest = new List<BadgeDef>();
            int earned = 0;
            foreach (BadgeDef badge in flow.Expansion.Badges)
            {
                bool isEarned = BadgeRules.Earned(badge, stats);
                earned += isEarned ? 1 : 0;
                (isEarned && !flow.BadgeClaimed(badge) ? claimable : rest).Add(badge);
            }

            claimable.AddRange(rest);
            column.Add(Ribbon("badgesRibbon", flow.Strings.Get("quests.badges"), PopTone.Purple), 110f, 24f);
            column.Add(Caption("badgesEarned", string.Format(flow.Strings.Get("quests.badgesEarned"), earned,
                flow.Expansion.Badges.Count)), 50f, 30f);

            for (int i = 0; i < claimable.Count; i += 3)
            {
                RectTransform row = UiComponents.Rect(content, $"badgeRow{i / 3}", new Vector2(CardWidth, BadgeHeight));
                for (int col = 0; col < 3 && i + col < claimable.Count; col++)
                {
                    BadgeTile(row, col, claimable[i + col], stats);
                }

                column.Add(row, BadgeHeight, BadgeGap);
            }
        }

        private void BadgeTile(RectTransform row, int col, BadgeDef badge, PlayerStats stats)
        {
            bool earned = BadgeRules.Earned(badge, stats);
            bool claimed = flow.BadgeClaimed(badge);
            RectTransform tile = UiComponents.Card(row, $"badge_{badge.Id}", new Vector2(BadgeWidth, BadgeHeight));
            tile.anchorMin = tile.anchorMax = new Vector2(0.5f, 1f);
            tile.pivot = new Vector2(0.5f, 1f);
            tile.anchoredPosition = new Vector2((col - 1) * (BadgeWidth + BadgeGap), 0f);

            PopTone tone = earned ? PopTone.Yellow : PopTone.Locked;
            RectTransform medal = UiComponents.Rect(tile, "medal", new Vector2(150f, 156f));
            medal.anchorMin = medal.anchorMax = new Vector2(0.5f, 1f);
            medal.anchoredPosition = new Vector2(0f, -100f);
            PopSurface surface = PopKit.Surface(medal, tone, 75f, PopKit.Border, lip: 10f);
            PopKit.Icon(surface.Face, "icon", badge.Icon, "text.primary", 80f, PopKit.Edge(tone), 7);
            if (claimed)
            {
                Image check = PopKit.Icon(medal, "claimed", "check", "pop.green.top", 56f, "pop.green.edge", 7);
                var checkRt = (RectTransform)check.transform.parent;
                checkRt.anchorMin = checkRt.anchorMax = new Vector2(1f, 1f);
                checkRt.anchoredPosition = new Vector2(-6f, -10f);
            }

            TextMeshProUGUI name = UiText.Create(tile, "name", ExpansionText.BadgeName(flow, badge), "body",
                "text.ink");
            Pin(name.rectTransform, 0f, -204f, BadgeWidth - 30f, 48f, center: true);
            name.enableAutoSizing = true;
            name.fontSizeMin = 24f;
            name.fontSizeMax = name.fontSize;
            TextMeshProUGUI goal = UiText.Create(tile, "goal", ExpansionText.BadgeGoal(flow, badge), "micro",
                "text.inkSoft");
            Pin(goal.rectTransform, 0f, -252f, BadgeWidth - 40f, 56f, center: true);
            goal.enableWordWrapping = true;

            if (earned && !claimed)
            {
                Button claim = PopKit.Button(tile, "claim", $"+{badge.Reward}", PopTone.Green,
                    () => ClaimBadge(badge), new Vector2(250f, 92f), "body", "coin");
                var claimRt = (RectTransform)claim.transform;
                claimRt.anchorMin = claimRt.anchorMax = new Vector2(0.5f, 0f);
                claimRt.pivot = new Vector2(0.5f, 0f);
                claimRt.anchoredPosition = new Vector2(0f, 20f);
            }
            else if (!earned)
            {
                PopBar bar = PopBar.Create(tile, "progress", new Vector2(260f, 52f), PopTone.Blue, "micro");
                bar.Rect.anchorMin = bar.Rect.anchorMax = new Vector2(0.5f, 0f);
                bar.Rect.anchoredPosition = new Vector2(0f, 64f);
                bar.Set(stats.Get(badge.Stat), badge.Target);
            }
            else
            {
                TextMeshProUGUI done = UiText.Create(tile, "done", flow.Strings.Get("quests.done"), "body",
                    "pop.green.edge");
                UiComponents.Place(done.rectTransform, new Vector2(0.5f, 0f), new Vector2(260f, 52f));
                done.rectTransform.anchoredPosition = new Vector2(0f, 64f);
            }
        }

        private void ClaimBadge(BadgeDef badge)
        {
            long before = flow.Meta.Coins;
            if (flow.TryClaimBadge(badge))
            {
                Claimed(before);
            }
        }

        // ------------------------------------------------------------------ bits

        private void Claimed(long coinsBefore)
        {
            UiJuice.Play("star");
            Refresh();
            coins.SetInstant(coinsBefore);
            coins.AnimateTo(flow.Meta.Coins);
            GetComponentInParent<ScreenManager>()?.Tabs.RefreshBadge();
        }

        private RectTransform Ribbon(string name, string text, PopTone tone)
        {
            RectTransform ribbon = PopKit.Ribbon(content, name, text, tone, new Vector2(700f, 110f));
            return ribbon;
        }

        private RectTransform Caption(string name, string text)
        {
            TextMeshProUGUI caption = UiText.CreateOutlined(content, name, text, "body", "text.primary", "panel.edge");
            caption.rectTransform.sizeDelta = new Vector2(CardWidth, 50f);
            return caption.rectTransform;
        }

        private void Done(RectTransform card)
        {
            Image check = PopKit.Icon(card, "done", "check", "pop.green.top", 90f, "pop.green.edge", 9);
            var checkRt = (RectTransform)check.transform.parent;
            checkRt.anchorMin = checkRt.anchorMax = new Vector2(1f, 0.5f);
            checkRt.anchoredPosition = new Vector2(-140f, 34f);
            TextMeshProUGUI label = UiText.Create(card, "doneLabel", flow.Strings.Get("quests.done"), "body",
                "pop.green.edge");
            UiComponents.Place(label.rectTransform, new Vector2(1f, 0.5f), new Vector2(220f, 50f));
            label.rectTransform.anchoredPosition = new Vector2(-140f, -40f);
        }

        private static void RewardPill(RectTransform card, int reward, Vector2 anchor, Vector2 position)
        {
            RectTransform pill = PopKit.Panel(card, "reward", new Vector2(190f, 70f), PopTone.Navy, 35f, 6f);
            pill.anchorMin = pill.anchorMax = anchor;
            pill.anchoredPosition = position;
            Image coin = PopKit.Icon(pill, "coin", "coin", "coin", 46f, "pop.yellow.edge", 6);
            ((RectTransform)coin.transform.parent).anchoredPosition = new Vector2(-56f, 0f);
            TextMeshProUGUI value = UiText.CreateOutlined(pill, "value", reward.ToString(), "body", "text.primary",
                "panel.edge");
            UiComponents.Stretch(value.rectTransform);
            value.rectTransform.offsetMin = new Vector2(50f, 0f);
        }

        private static void Pin(RectTransform rt, float left, float y, float width, float height, bool center = false)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(center ? 0.5f : 0f, 1f);
            rt.pivot = new Vector2(center ? 0.5f : 0f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(left, y);
        }

        private static PopTone ToneFor(QuestKind kind) => kind switch
        {
            QuestKind.Rescue or QuestKind.RescueSpecies => PopTone.Blue,
            QuestKind.Rows or QuestKind.DeepClear => PopTone.Coral,
            QuestKind.Tides or QuestKind.Endless => PopTone.Purple,
            QuestKind.Powers => PopTone.Green,
            _ => PopTone.Yellow,
        };

        private static string IconFor(QuestKind kind) => kind switch
        {
            QuestKind.Rescue => "fish",
            QuestKind.Rows => "rows",
            QuestKind.DeepClear => "bomb",
            QuestKind.Tides => "waves",
            QuestKind.Score => "trophy",
            QuestKind.Levels => "map",
            QuestKind.Daily => "calendar",
            QuestKind.Powers => "star",
            _ => "crown",
        };
    }
}
