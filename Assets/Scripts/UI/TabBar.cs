using Riptide.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// Tidal Pop persistent bottom tab bar (Nick's pick, 2026-10-18): Home ·
    /// Tidepool · Quests (GDD §15.3, with a claimable-count bubble) · Shop ·
    /// Settings, one tap from every menu screen. The active tab
    /// rises as a raised blue tile; ZoneMap highlights Home (it is Voyage's page).
    /// Hidden in play and on results (those screens own their CTAs). Navigation
    /// still goes through GameFlow.GoTo — the bar is only a face.
    /// </summary>
    public sealed class TabBar : MonoBehaviour
    {
        /// <summary>Bar height above the safe-area bottom, ref-px — screens keep content above it.</summary>
        public const float HeightRefPx = 210f;

        private static readonly FlowScreen[] Tabs =
            { FlowScreen.Home, FlowScreen.Tidepool, FlowScreen.Quests, FlowScreen.Shop, FlowScreen.Settings };
        private static readonly string[] IconIds = { "home", "fish", "trophy", "bag", "gear" };
        private static readonly string[] IconTokens =
            { "panel.light", "pop.yellow", "pop.yellow", "pop.green", "panel.shade" };
        private static readonly string[] LabelKeys =
            { "tab.home", "home.tidepool", "tab.quests", "shop.title", "home.settings" };

        private readonly GameObject[] tiles = new GameObject[5];
        private readonly RectTransform[] icons = new RectTransform[5];
        private readonly TextMeshProUGUI[] labels = new TextMeshProUGUI[5];
        private RectTransform bar = null!;
        private GameFlow flow = null!;
        private GameObject countBubble = null!;
        private TextMeshProUGUI countLabel = null!;
        private int active = -1;

        /// <summary>Screens that show the bar.</summary>
        public static bool Shows(FlowScreen screen) =>
            screen == FlowScreen.Home || screen == FlowScreen.ZoneMap || screen == FlowScreen.Tidepool
            || screen == FlowScreen.Shop || screen == FlowScreen.Settings || screen == FlowScreen.Quests;

        /// <summary>Tab roots besides Home — switching between them never stacks up.</summary>
        public static bool IsTabRoot(FlowScreen screen) =>
            screen == FlowScreen.Tidepool || screen == FlowScreen.Shop || screen == FlowScreen.Settings
            || screen == FlowScreen.Quests;

        public static TabBar Create(RectTransform canvasRoot, GameFlow flow)
        {
            RectTransform safe = UiComponents.Rect(canvasRoot, "TabBar", Vector2.zero);
            UiComponents.Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();
            var tabBar = safe.gameObject.AddComponent<TabBar>();
            tabBar.flow = flow;

            RectTransform bar = UiComponents.Rect(safe, "bar", Vector2.zero);
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.offsetMin = new Vector2(0f, 0f);
            bar.offsetMax = new Vector2(0f, HeightRefPx);
            tabBar.bar = bar;

            // Skirt: the navy runs on under the gesture area to the screen edge.
            Image skirt = PopKit.Well(bar, "skirt", "pop.navy", 0f);
            skirt.rectTransform.offsetMin = new Vector2(0f, -400f);
            skirt.rectTransform.offsetMax = new Vector2(0f, -40f);
            Image rim = PopKit.Well(bar, "rim", "panel.edgeDeep", PopKit.Radius("r.l"));
            rim.rectTransform.offsetMin = new Vector2(-12f, -80f);
            rim.rectTransform.offsetMax = new Vector2(12f, 0f);
            rim.raycastTarget = true; // the bar swallows taps meant for nothing
            Image fill = PopKit.Well(bar, "fill", "pop.navy", PopKit.Radius("r.l") - 8f);
            fill.rectTransform.offsetMin = new Vector2(-4f, -80f);
            fill.rectTransform.offsetMax = new Vector2(4f, -8f);
            Image gloss = PopKit.Well(bar, "gloss", "shine", PopKit.Radius("r.l") - 12f);
            gloss.rectTransform.anchorMin = new Vector2(0f, 1f);
            gloss.rectTransform.offsetMin = new Vector2(20f, -26f);
            gloss.rectTransform.offsetMax = new Vector2(-20f, -14f);

            for (int i = 0; i < Tabs.Length; i++)
            {
                tabBar.BuildTab(bar, i, flow);
            }

            return tabBar;
        }

        private void BuildTab(RectTransform bar, int index, GameFlow flow)
        {
            float width = 1f / Tabs.Length;
            RectTransform slot = UiComponents.Rect(bar, $"tab_{Tabs[index]}", Vector2.zero);
            slot.anchorMin = new Vector2(width * index, 0f);
            slot.anchorMax = new Vector2(width * (index + 1), 1f);
            slot.offsetMin = new Vector2(10f, 0f);
            slot.offsetMax = new Vector2(-10f, 0f);
            var hit = slot.gameObject.AddComponent<Image>();
            hit.sprite = SpriteFactory.Solid();
            hit.color = new Color(0f, 0f, 0f, 0f);
            var button = slot.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            FlowScreen target = Tabs[index];
            button.onClick.AddListener(() =>
            {
                if (flow.Screen != target)
                {
                    flow.GoTo(target);
                }
            });
            slot.gameObject.AddComponent<PressEffect>();

            // The active tile rises out of the bar (the genre's raised home tab).
            RectTransform tile = UiComponents.Rect(slot, "tile", Vector2.zero);
            tile.anchorMin = new Vector2(0f, 0f);
            tile.anchorMax = new Vector2(1f, 1f);
            tile.offsetMin = new Vector2(4f, 22f);
            tile.offsetMax = new Vector2(-4f, 52f);
            PopKit.Surface(tile, PopTone.Blue, PopKit.Radius("r.m"), PopKit.Border, lip: 12f);
            tiles[index] = tile.gameObject;

            Image icon = PopKit.Icon(slot, "icon", IconIds[index], IconTokens[index], 84f, "panel.edgeDeep", 10);
            icons[index] = (RectTransform)icon.transform.parent;

            labels[index] = UiText.CreateOutlined(slot, "label", flow.Strings.Get(LabelKeys[index]),
                "caption", "text.secondary", "panel.edgeDeep", 0.2f);
            UiComponents.Place(labels[index].rectTransform, new Vector2(0.5f, 0f), new Vector2(204f, 44f));
            labels[index].rectTransform.anchoredPosition = new Vector2(0f, 42f);
            labels[index].enableAutoSizing = true;
            labels[index].fontSizeMin = 22f;
            labels[index].fontSizeMax = labels[index].fontSize;

            if (Tabs[index] == FlowScreen.Quests)
            {
                // Claimable rewards bubble, riding the icon as it rises.
                RectTransform bubble = PopKit.Panel(icons[index], "count", new Vector2(62f, 62f), PopTone.Coral, 31f, 6f);
                bubble.anchoredPosition = new Vector2(42f, 34f);
                countLabel = UiText.CreateOutlined(bubble, "label", "", "caption", "text.primary", "pop.coral.edge");
                UiComponents.Stretch(countLabel.rectTransform);
                countBubble = bubble.gameObject;
            }
        }

        /// <summary>Re-reads the claimable count (quests + badges) into the Quests bubble.</summary>
        public void RefreshBadge()
        {
            int count = flow.ClaimableCount();
            countBubble.SetActive(count > 0);
            countLabel.text = count > 9 ? "9+" : count.ToString();
        }

        /// <summary>Shows/hides the bar for a screen and raises the matching tab.</summary>
        public void Refresh(FlowScreen screen)
        {
            bool shows = Shows(screen);
            bar.gameObject.SetActive(shows);
            if (!shows)
            {
                return;
            }

            RefreshBadge();
            int current = screen == FlowScreen.ZoneMap ? 0 : System.Array.IndexOf(Tabs, screen);
            if (current == active)
            {
                return;
            }

            active = current;
            for (int i = 0; i < Tabs.Length; i++)
            {
                bool on = i == active;
                tiles[i].SetActive(on);
                icons[i].anchoredPosition = new Vector2(0f, on ? 46f : 24f);
                icons[i].localScale = Vector3.one * (on ? 1.15f : 1f);
                labels[i].color = ThemeRuntime.Color(on ? "text.primary" : "text.secondary");
                labels[i].rectTransform.anchoredPosition = new Vector2(0f, on ? 50f : 40f);
            }
        }
    }
}
