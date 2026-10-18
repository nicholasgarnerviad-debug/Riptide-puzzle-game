using System.Collections.Generic;
using Riptide.Core;
using Riptide.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// Spec §4.2 zone map, Tidal Pop edition (Nick's pick: winding seabed path).
    /// Zones descend from the sunlit tidepool to the abyss — each a band of water
    /// that darkens with depth, a ribbon header with its star tally, and 20
    /// chunky level nodes on an S-curve joined by a dotted trail. Node states:
    /// complete (blue, stars beneath) · current (green, pulsing) · locked (slate,
    /// padlock). Opens centered on the current level.
    /// </summary>
    public sealed class ZoneMapScreen : MonoBehaviour, IScreenRefresh
    {
        private const float HeaderBand = 300f;
        private const float NodeStep = 196f;
        private const float NodeSize = 172f;
        private const float CurrentSize = 212f;
        private const float Swing = 290f;
        private const float ZoneGap = 120f;

        private sealed class Node
        {
            public int Zone;
            public int Index;
            public float Y;
            public Button Button = null!;
            public PopSurface Surface = null!;
            public RectTransform Rt = null!;
            public TextMeshProUGUI Label = null!;
            public GameObject Lock = null!;
            public GameObject Glow = null!;
            public GameObject Stars = null!;
            public Image[] StarFills = null!;
        }

        private sealed class ZoneHeader
        {
            public int Zone;
            public PopSurface Ribbon = null!;
            public TextMeshProUGUI Stars = null!;
            public TextMeshProUGUI LockCaption = null!;
            public CanvasGroup Dim = null!;
        }

        private GameFlow flow = null!;
        private CoinCounter? coins;
        private ScrollRect scroll = null!;
        private RectTransform content = null!;
        private RectTransform viewport = null!;
        private readonly List<Node> nodes = new List<Node>();
        private readonly List<ZoneHeader> headers = new List<ZoneHeader>();
        private Node? current;
        private float pulse;

        public static RectTransform Build(RectTransform parent, GameFlow flow)
        {
            RectTransform root = ScreenChrome.Root(parent, "ZoneMapScreen");
            var screen = root.gameObject.AddComponent<ZoneMapScreen>();
            screen.flow = flow;
            screen.coins = ScreenChrome.Header(root, flow, flow.Strings.Get("home.voyage"),
                () => flow.GoTo(FlowScreen.Home), coins: true);

            screen.viewport = UiComponents.Rect(root, "scroll", Vector2.zero);
            UiComponents.Stretch(screen.viewport);
            screen.viewport.offsetMin = new Vector2(0f, ScreenChrome.TabFloorRefPx - 60f);
            screen.viewport.offsetMax = new Vector2(0f, -ScreenChrome.HeaderRefPx);
            var hit = screen.viewport.gameObject.AddComponent<Image>();
            hit.sprite = SpriteFactory.Solid();
            hit.color = new Color(0f, 0f, 0f, 0f);
            screen.viewport.gameObject.AddComponent<RectMask2D>();
            screen.scroll = screen.viewport.gameObject.AddComponent<ScrollRect>();
            screen.scroll.horizontal = false;
            screen.scroll.vertical = true;
            screen.scroll.movementType = ScrollRect.MovementType.Elastic;
            screen.scroll.decelerationRate = 0.12f;

            screen.content = UiComponents.Rect(screen.viewport, "content", Vector2.zero);
            screen.content.anchorMin = new Vector2(0f, 1f);
            screen.content.anchorMax = new Vector2(1f, 1f);
            screen.content.pivot = new Vector2(0.5f, 1f);
            screen.scroll.content = screen.content;

            float y = 40f;
            for (int zone = 1; zone <= 10; zone++)
            {
                y = screen.BuildZone(zone, y);
            }

            screen.content.sizeDelta = new Vector2(0f, y + 80f);
            screen.Refresh();
            return root;
        }

        private float BuildZone(int zone, float top)
        {
            float height = HeaderBand + 20 * NodeStep + ZoneGap;

            // The zone's water: deeper zones sink toward the abyss color.
            RectTransform band = UiComponents.Rect(content, $"zone{zone}", Vector2.zero);
            band.anchorMin = new Vector2(0f, 1f);
            band.anchorMax = new Vector2(1f, 1f);
            band.pivot = new Vector2(0.5f, 1f);
            band.offsetMin = new Vector2(24f, -(top + height - ZoneGap * 0.5f));
            band.offsetMax = new Vector2(-24f, -top);
            Image water = PopKit.Well(band, "water", "bg.abyss", PopKit.Radius("r.l"));
            Color tint = water.color;
            water.color = new Color(tint.r, tint.g, tint.b, 0.10f + 0.06f * (zone - 1));
            var group = band.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            Decorate(band, zone);

            // Ribbon header + star tally.
            RectTransform ribbon = PopKit.Ribbon(content, $"zoneHeader{zone}",
                string.Format(flow.Strings.Get("home.zoneRibbon"), zone, flow.Strings.Get($"zones.{zone}")),
                PopTone.Coral, new Vector2(760f, 120f));
            ribbon.anchorMin = ribbon.anchorMax = new Vector2(0.5f, 1f);
            ribbon.anchoredPosition = new Vector2(0f, -top - 90f);
            RectTransform starPill = PopKit.Panel(content, $"zoneStars{zone}", new Vector2(250f, 76f), PopTone.Navy,
                38f, 7f);
            starPill.anchorMin = starPill.anchorMax = new Vector2(0.5f, 1f);
            starPill.anchoredPosition = new Vector2(0f, -top - 196f);
            Image star = PopKit.Icon(starPill, "star", "star", "pop.yellow", 52f, "pop.yellow.edge", 8);
            ((RectTransform)star.transform.parent).anchoredPosition = new Vector2(-72f, 2f);
            TextMeshProUGUI stars = UiText.CreateOutlined(starPill, "count", "", "body", "text.primary", "panel.edge");
            UiComponents.Place(stars.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(160f, 60f));
            stars.rectTransform.anchoredPosition = new Vector2(26f, 0f);
            TextMeshProUGUI lockCaption = UiText.CreateOutlined(content, $"zoneLock{zone}",
                string.Format(flow.Strings.Get("zone.locked"), zone - 1), "body", "text.primary", "panel.edge");
            UiComponents.Place(lockCaption.rectTransform, new Vector2(0.5f, 1f), new Vector2(900f, 60f));
            lockCaption.rectTransform.anchoredPosition = new Vector2(0f, -top - 268f);
            headers.Add(new ZoneHeader
            {
                Zone = zone,
                Ribbon = ribbon.Find("band").GetComponent<PopSurface>(),
                Stars = stars,
                LockCaption = lockCaption,
                Dim = group,
            });

            // Nodes on the S-curve; the whole dotted trail first so its dots
            // always sit UNDER the nodes.
            var points = new Vector2[20];
            for (int index = 1; index <= 20; index++)
            {
                float nodeY = top + HeaderBand + (index - 0.5f) * NodeStep;
                float x = Mathf.Sin((index - 1) * 0.62f + zone * 1.7f) * Swing;
                points[index - 1] = new Vector2(x, -nodeY);
                if (index > 1)
                {
                    Trail(points[index - 2], points[index - 1]);
                }
            }

            for (int index = 1; index <= 20; index++)
            {
                nodes.Add(BuildNode(zone, index, points[index - 1]));
            }

            return top + height;
        }

        private void Decorate(RectTransform band, int zone)
        {
            // Kelp and rocks along the band's edges — scenery, never in the path.
            for (int i = 0; i < 6; i++)
            {
                bool left = i % 2 == 0;
                string id = (i + zone) % 3 == 0 ? "rocks" : "kelp";
                float size = id == "kelp" ? 220f + 50f * ((i + zone) % 2) : 170f;
                RectTransform prop = UiComponents.Rect(band, id, new Vector2(size, size));
                prop.anchorMin = prop.anchorMax = new Vector2(left ? 0f : 1f, 1f - (i + 0.6f) / 6.2f);
                prop.pivot = new Vector2(left ? 0f : 1f, 0.5f);
                prop.anchoredPosition = new Vector2(left ? -20f : 20f, 0f);
                var image = prop.gameObject.AddComponent<Image>();
                image.sprite = MenuSprites.Icon(id);
                image.raycastTarget = false;
                ThemedElement.Bind(prop.gameObject, id == "kelp" ? "kelp" : "rock");
            }
        }

        private void Trail(Vector2 from, Vector2 to)
        {
            for (int d = 1; d <= 3; d++)
            {
                Vector2 p = Vector2.Lerp(from, to, d / 4f);
                Image dot = PopKit.Img(content, "trail", PopSprites.Circle(), "panel.light");
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                dot.rectTransform.sizeDelta = new Vector2(26f, 26f);
                dot.rectTransform.anchoredPosition = p;
            }
        }

        private Node BuildNode(int zone, int index, Vector2 at)
        {
            var node = new Node { Zone = zone, Index = index, Y = -at.y };

            RectTransform glow = UiComponents.Rect(content, $"glow{zone}_{index}", new Vector2(360f, 360f));
            glow.anchorMin = glow.anchorMax = new Vector2(0.5f, 1f);
            glow.anchoredPosition = at;
            var glowImage = glow.gameObject.AddComponent<Image>();
            glowImage.sprite = MenuSprites.SoftGlow();
            glowImage.raycastTarget = false;
            ThemedElement.Bind(glow.gameObject, "glow.primary");
            node.Glow = glow.gameObject;

            int zoneCopy = zone;
            int indexCopy = index;
            node.Button = PopKit.Tile(content, $"z{zone}l{index}", PopTone.Blue, () => OnNode(zoneCopy, indexCopy),
                new Vector2(NodeSize, NodeSize), NodeSize * 0.5f);
            node.Rt = (RectTransform)node.Button.transform;
            node.Rt.anchorMin = node.Rt.anchorMax = new Vector2(0.5f, 1f);
            node.Rt.anchoredPosition = at;
            node.Surface = node.Rt.GetComponent<PopSurface>();
            RectTransform face = node.Surface.Face;

            node.Label = UiText.CreateOutlined(face, "label", index.ToString(), "title", "text.primary",
                PopKit.Edge(PopTone.Blue), 0.28f);
            UiComponents.Stretch(node.Label.rectTransform);
            Image padlock = PopKit.Icon(face, "lock", "lock", "panel.shade", 70f, "pop.locked.edge", 8);
            node.Lock = padlock.transform.parent.gameObject;

            RectTransform starRow = UiComponents.Rect(node.Rt, "stars", new Vector2(170f, 60f));
            starRow.anchorMin = starRow.anchorMax = new Vector2(0.5f, 0f);
            starRow.anchoredPosition = new Vector2(0f, -10f);
            node.StarFills = new Image[3];
            for (int s = 0; s < 3; s++)
            {
                Image fill = PopKit.Icon(starRow, $"s{s}", "star", "pop.yellow", s == 1 ? 58f : 48f, "panel.edge", 9);
                var holder = (RectTransform)fill.transform.parent;
                holder.anchoredPosition = new Vector2((s - 1) * 54f, s == 1 ? -6f : 4f);
                node.StarFills[s] = fill;
            }

            node.Stars = starRow.gameObject;
            return node;
        }

        private void OnNode(int zone, int index)
        {
            if (flow.Meta.Voyage.IsUnlocked(zone, index))
            {
                flow.StartVoyageLevel(zone, index);
            }
        }

        public void Refresh()
        {
            (int currentZone, int currentIndex) = flow.Meta.Voyage.NextLevel();
            current = null;
            foreach (Node node in nodes)
            {
                bool unlocked = flow.Meta.Voyage.IsUnlocked(node.Zone, node.Index);
                bool isCurrent = node.Zone == currentZone && node.Index == currentIndex;
                int stars = flow.Meta.Voyage.StarsFor(VoyageProgress.LevelId(node.Zone, node.Index));
                PopTone tone = isCurrent ? PopTone.Green : unlocked ? PopTone.Blue : PopTone.Locked;
                node.Surface.SetTone(tone);
                UiText.Outline(node.Label, PopKit.Edge(tone), 0.28f);
                node.Button.interactable = unlocked;
                node.Label.gameObject.SetActive(unlocked);
                node.Lock.SetActive(!unlocked);
                node.Glow.SetActive(isCurrent);
                node.Rt.localScale = Vector3.one * (isCurrent ? CurrentSize / NodeSize : 1f);
                node.Stars.SetActive(stars > 0 && !isCurrent);
                for (int s = 0; s < 3; s++)
                {
                    node.StarFills[s].color = ThemeRuntime.Color(s < stars ? "pop.yellow" : "pop.locked");
                }

                if (isCurrent)
                {
                    current = node;
                }
            }

            foreach (ZoneHeader header in headers)
            {
                int total = 0;
                for (int index = 1; index <= 20; index++)
                {
                    total += flow.Meta.Voyage.StarsFor(VoyageProgress.LevelId(header.Zone, index));
                }

                header.Stars.text = string.Format(flow.Strings.Get("home.zoneStars"), total);
                bool open = flow.Meta.Voyage.IsUnlocked(header.Zone, 1);
                header.Ribbon.SetTone(open ? PopTone.Coral : PopTone.Locked);
                header.LockCaption.gameObject.SetActive(!open);
                header.Dim.alpha = open ? 1f : 0.6f;
            }

            if (coins != null)
            {
                coins.SetInstant(flow.Meta.Coins);
            }

            CenterOnCurrent();
        }

        private void CenterOnCurrent()
        {
            if (current == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            float viewportHeight = viewport.rect.height;
            float target = current.Y - viewportHeight * 0.5f;
            float max = Mathf.Max(0f, content.sizeDelta.y - viewportHeight);
            content.anchoredPosition = new Vector2(0f, Mathf.Clamp(target, 0f, max));
            scroll.velocity = Vector2.zero;
        }

        private void Update()
        {
            if (current == null || ThemeRuntime.ReducedMotion)
            {
                return;
            }

            // The current node breathes; its glow swells with it.
            pulse += Time.deltaTime;
            float s = 1f + 0.06f * Mathf.Sin(pulse * 3.4f);
            current.Rt.localScale = Vector3.one * (CurrentSize / NodeSize * s);
            current.Glow.transform.localScale = Vector3.one * (0.9f + 0.2f * Mathf.Sin(pulse * 3.4f));
        }
    }
}
