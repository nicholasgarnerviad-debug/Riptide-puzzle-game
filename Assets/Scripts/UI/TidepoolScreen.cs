using System.Collections.Generic;
using Riptide.Core;
using Riptide.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// The Tidelings tab (GDD §16.6; Nick's mix of the three mocked directions,
    /// 2026-10-18): the Tidepool diorama (§4.6, 3 screens wide, 2-layer parallax)
    /// is now where your Pod swims — the leader center, the other two beside it —
    /// with every other Tideling you own idling further along; the Conch sits on the
    /// sand (egg count, CALL); the Tide Journal card below shows each tier's
    /// tier-framed cards. Decorations keep their edit mode: owned pieces place at
    /// slot points (save v3), unowned show in the tray with their coin cost; event
    /// prizes are earned, never sold. Pod and Conch live in TidepoolScreen.Pod,
    /// the Journal in TidepoolScreen.Journal.
    /// </summary>
    public sealed partial class TidepoolScreen : MonoBehaviour, IScreenRefresh
    {
        private const int DecoSlotCount = 8;
        private const float ContentWidth = 3240f; // §4.6: three screens wide

        private GameFlow flow = null!;
        private CoinCounter coins = null!;
        private TextMeshProUGUI eggCount = null!;
        private RectTransform frame = null!;
        private RectTransform content = null!;
        private RectTransform parallaxFar = null!;
        private RectTransform parallaxNear = null!;
        private Button editToggle = null!;
        private RectTransform tray = null!;
        private TextMeshProUGUI hint = null!;

        private readonly List<(Button marker, RectTransform slotRt, TextMeshProUGUI label, Image dot)> decoSlots
            = new List<(Button, RectTransform, TextMeshProUGUI, Image)>();

        private bool editMode;
        private string selectedDecoration = "";

        public static RectTransform Build(RectTransform parent, GameFlow flow)
        {
            RectTransform root = ScreenChrome.Root(parent, "TidepoolScreen");
            var screen = root.gameObject.AddComponent<TidepoolScreen>();
            screen.flow = flow;

            screen.BuildHeader(root);
            screen.BuildDiorama(root);
            screen.BuildPod();
            screen.BuildConch();
            screen.BuildJournal(root);
            screen.BuildTray(root);

            screen.hint = UiText.CreateOutlined(root, "hint", flow.Strings.Get("tidepool.placeHint"),
                "body", "text.primary", "panel.edge");
            screen.hint.rectTransform.anchorMin = screen.hint.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            screen.hint.rectTransform.sizeDelta = new Vector2(900f, 60f);
            screen.hint.rectTransform.anchoredPosition = new Vector2(0f, InfoTop + 6f);

            screen.Refresh();
            return root;
        }

        // Vertical budget (ref-px from the safe bottom): tab bar, then the Journal /
        // decoration card, then the diorama window up to the header.
        private const float InfoBottom = ScreenChrome.TabFloorRefPx - 20f;
        private const float InfoHeight = 640f;
        private const float InfoTop = InfoBottom + InfoHeight;

        /// <summary>Tidal Pop header, Tidelings edition: the title left, eggs and coins right.</summary>
        private void BuildHeader(RectTransform root)
        {
            RectTransform header = UiComponents.Rect(root, "header", Vector2.zero);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.offsetMin = new Vector2(0f, -ScreenChrome.HeaderRefPx);
            header.offsetMax = Vector2.zero;

            TextMeshProUGUI title = UiText.CreateOutlined(header, "title", flow.Strings.Get("tidepool.title"), "title",
                "text.primary", "panel.edge", 0.26f, TextAlignmentOptions.MidlineLeft);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            title.rectTransform.pivot = new Vector2(0f, 0.5f);
            title.rectTransform.sizeDelta = new Vector2(420f, 100f);
            title.rectTransform.anchoredPosition = new Vector2(44f, 0f);

            coins = PopKit.CoinPill(header, () => flow.GoTo(FlowScreen.Shop));
            var coinPill = (RectTransform)coins.transform.parent;
            coinPill.anchorMin = coinPill.anchorMax = new Vector2(1f, 0.5f);
            coinPill.anchoredPosition = new Vector2(-205f, 0f);

            eggCount = TidelingUi.EggPill(header, "eggPill", new Vector2(220f, 104f));
            var eggPill = (RectTransform)eggCount.transform.parent;
            eggPill.anchorMin = eggPill.anchorMax = new Vector2(1f, 0.5f);
            eggPill.anchoredPosition = new Vector2(-500f, 0f);
            Tappable(eggPill, OpenConch);
        }

        private void BuildDiorama(RectTransform root)
        {
            // Tidal Pop: the diorama is a rounded aquarium window — navy rim, a
            // sunlit water gradient, sand dunes on the floor.
            frame = UiComponents.Rect(root, "dioramaFrame", Vector2.zero);
            UiComponents.Stretch(frame);
            frame.offsetMin = new Vector2(26f, InfoTop + 40f);
            frame.offsetMax = new Vector2(-26f, -ScreenChrome.HeaderRefPx);
            PopKit.Well(frame, "rim", "panel.edge", PopKit.Radius("r.l"));

            RectTransform viewport = UiComponents.Rect(frame, "diorama", Vector2.zero);
            UiComponents.Stretch(viewport);
            viewport.offsetMin = new Vector2(PopKit.Border, PopKit.Border);
            viewport.offsetMax = new Vector2(-PopKit.Border, -PopKit.Border);
            Image viewportImage = UiComponents.RoundedImage(viewport.gameObject, PopKit.Radius("r.l") - PopKit.Border);
            viewport.gameObject.AddComponent<PopGradient>().Set("bg.oceanTop", "bg.deep");
            viewportImage.raycastTarget = true;
            viewport.gameObject.AddComponent<Mask>();

            // Water column light and sunbeams, then dune/kelp silhouette layers.
            RectTransform waterGrad = UiComponents.Rect(viewport, "waterGrad", Vector2.zero);
            waterGrad.anchorMin = new Vector2(0f, 0.35f);
            waterGrad.anchorMax = new Vector2(1f, 1f);
            waterGrad.offsetMin = Vector2.zero;
            waterGrad.offsetMax = Vector2.zero;
            waterGrad.localScale = new Vector3(1f, -1f, 1f);
            var gradImage = waterGrad.gameObject.AddComponent<Image>();
            gradImage.sprite = SpriteFactory.VerticalFade();
            gradImage.raycastTarget = false;
            ThemedElement.Bind(waterGrad.gameObject, "bg.oceanTop");

            for (int r = 0; r < 2; r++)
            {
                RectTransform ray = UiComponents.Rect(viewport, "ray", new Vector2(190f - r * 60f, 1100f));
                ray.anchorMin = new Vector2(0.25f + r * 0.42f, 1f);
                ray.anchorMax = new Vector2(0.25f + r * 0.42f, 1f);
                ray.pivot = new Vector2(0.5f, 1f);
                ray.anchoredPosition = new Vector2(0f, 60f);
                ray.localRotation = Quaternion.Euler(0f, 0f, r == 0 ? 11f : -8f);
                var rayImage = ray.gameObject.AddComponent<Image>();
                rayImage.sprite = MenuSprites.LightRay();
                rayImage.raycastTarget = false;
                ThemedElement.Bind(ray.gameObject, "ray.light");
            }

            // §4.6 parallax: two seabed layers tracked against the scroll.
            parallaxFar = SilhouetteLayer(viewport, "far", "sand.shade", 300f, kelp: false);
            parallaxNear = SilhouetteLayer(viewport, "near", "sand", 230f, kelp: true);

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            content = UiComponents.Rect(viewport, "content", Vector2.zero);
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.sizeDelta = new Vector2(ContentWidth, 0f);
            scroll.content = content;
            scroll.horizontal = true;
            scroll.vertical = false;

            // §4.6 decoration slot points along the floor.
            for (int slot = 0; slot < DecoSlotCount; slot++)
            {
                float x = 200f + slot * (ContentWidth - 400f) / (DecoSlotCount - 1);
                RectTransform slotRt = UiComponents.Rect(content, $"decoSlot{slot}", new Vector2(120f, 120f));
                UiComponents.Place(slotRt, new Vector2(0f, 0.5f), new Vector2(120f, 120f));
                slotRt.anchoredPosition = new Vector2(x, -330f);

                var dotGo = new GameObject("dot", typeof(RectTransform));
                dotGo.transform.SetParent(slotRt, false);
                var dot = dotGo.AddComponent<Image>();
                dot.sprite = PopSprites.Circle();
                ((RectTransform)dotGo.transform).sizeDelta = new Vector2(64f, 64f);

                TextMeshProUGUI label = UiText.CreateOutlined(slotRt, "label", "", "micro", "text.primary", "panel.edge");
                UiComponents.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(200f, 36f));

                var marker = slotRt.gameObject.AddComponent<Button>();
                int slotCopy = slot;
                marker.onClick.AddListener(() => OnSlotTapped(slotCopy));
                decoSlots.Add((marker, slotRt, label, dot));
            }

            // Decorate toggle, top-right of the window.
            editToggle = PopKit.Button(frame, "edit", flow.Strings.Get("tidepool.edit"), PopTone.White,
                ToggleEdit, new Vector2(210f, 110f), "body");
            var editRt = (RectTransform)editToggle.transform;
            editRt.anchorMin = editRt.anchorMax = new Vector2(1f, 1f);
            editRt.anchoredPosition = new Vector2(-136f, -86f);
        }

        /// <summary>A scrolling seabed silhouette: tiled dunes, optional kelp and rocks.</summary>
        private static RectTransform SilhouetteLayer(RectTransform viewport, string name, string colorToken,
            float height, bool kelp)
        {
            RectTransform layer = UiComponents.Rect(viewport, name, Vector2.zero);
            layer.anchorMin = new Vector2(0f, 0f);
            layer.anchorMax = new Vector2(0f, 0f);
            layer.pivot = new Vector2(0f, 0f);
            layer.sizeDelta = new Vector2(ContentWidth + 600f, height);
            layer.anchoredPosition = Vector2.zero;

            const float duneTile = 760f;
            for (float x = 0f; x < ContentWidth + 600f; x += duneTile)
            {
                RectTransform dune = UiComponents.Rect(layer, "dune", new Vector2(duneTile + 4f, height));
                dune.anchorMin = new Vector2(0f, 0f);
                dune.anchorMax = new Vector2(0f, 0f);
                dune.pivot = new Vector2(0f, 0f);
                dune.anchoredPosition = new Vector2(x, 0f);
                var image = dune.gameObject.AddComponent<Image>();
                image.sprite = MenuSprites.Dunes();
                image.raycastTarget = false;
                ThemedElement.Bind(dune.gameObject, colorToken);
            }

            if (kelp)
            {
                float[] kelpXs = { 40f, 1020f, 1460f, 1840f, 2420f, 2980f };
                for (int i = 0; i < kelpXs.Length; i++)
                {
                    string id = i % 3 == 2 ? "rocks" : "kelp";
                    float size = id == "kelp" ? 230f + (i % 2) * 70f : 170f;
                    RectTransform prop = UiComponents.Rect(layer, id, new Vector2(size, size));
                    prop.anchorMin = new Vector2(0f, 0f);
                    prop.anchorMax = new Vector2(0f, 0f);
                    prop.pivot = new Vector2(0.5f, 0f);
                    prop.anchoredPosition = new Vector2(kelpXs[i], 26f);
                    var propImage = prop.gameObject.AddComponent<Image>();
                    propImage.sprite = MenuSprites.Icon(id);
                    propImage.raycastTarget = false;
                    ThemedElement.Bind(prop.gameObject, id == "kelp" ? "kelp" : "rock");
                }
            }

            return layer;
        }

        private void BuildTray(RectTransform root)
        {
            tray = BottomCard(root, "decoTray");
        }

        private static RectTransform BottomCard(RectTransform root, string name)
        {
            RectTransform card = UiComponents.Card(root, name, new Vector2(1000f, InfoHeight));
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0f);
            card.pivot = new Vector2(0.5f, 0f);
            card.anchoredPosition = new Vector2(0f, InfoBottom);
            return card;
        }

        /// <summary>Makes a plain rect a pressable (transparent hit target + press scale).</summary>
        private static Button Tappable(RectTransform rt, System.Action onTap)
        {
            if (!rt.TryGetComponent(out Image hit))
            {
                hit = rt.gameObject.AddComponent<Image>();
                hit.sprite = SpriteFactory.Solid();
                hit.color = new Color(0f, 0f, 0f, 0f);
            }

            hit.raycastTarget = true;
            var button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onTap());
            rt.gameObject.AddComponent<PressEffect>();
            return button;
        }

        private void ToggleEdit()
        {
            editMode = !editMode;
            selectedDecoration = "";
            Refresh();
        }

        private void OnSlotTapped(int slot)
        {
            if (!editMode)
            {
                return;
            }

            string placed = flow.Meta.DecorationAt(slot);
            if (!string.IsNullOrEmpty(selectedDecoration))
            {
                if (flow.Meta.TryPlaceDecoration(slot, selectedDecoration))
                {
                    selectedDecoration = "";
                    Refresh();
                }
            }
            else if (!string.IsNullOrEmpty(placed))
            {
                flow.Meta.ClearDecorationSlot(slot);
                Refresh();
            }
        }

        private void RebuildTray()
        {
            foreach (Transform child in tray)
            {
                Destroy(child.gameObject);
            }

            TextMeshProUGUI heading = UiText.Create(tray, "heading",
                flow.Strings.Get("tidepool.decorations"), "heading", "text.ink");
            heading.rectTransform.anchorMin = heading.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            heading.rectTransform.sizeDelta = new Vector2(700f, 60f);
            heading.rectTransform.anchoredPosition = new Vector2(0f, -56f);

            // 26 pieces (20 shop + 6 event prizes) outgrow the card: the grid scrolls.
            RectTransform viewport = UiComponents.Rect(tray, "grid", Vector2.zero);
            UiComponents.Stretch(viewport);
            viewport.offsetMin = new Vector2(24f, 36f);
            viewport.offsetMax = new Vector2(-24f, -100f);
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.sprite = SpriteFactory.Solid();
            hit.color = new Color(0f, 0f, 0f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scrollRect = viewport.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            RectTransform grid = UiComponents.Rect(viewport, "content", Vector2.zero);
            grid.anchorMin = new Vector2(0f, 1f);
            grid.anchorMax = new Vector2(1f, 1f);
            grid.pivot = new Vector2(0.5f, 1f);
            scrollRect.content = grid;

            var decorations = flow.Decorations;
            const int perRow = 4;
            const float rowStep = 145f;
            for (int i = 0; i < decorations.Count; i++)
            {
                Decoration deco = decorations[i];
                int col = i % perRow;
                int row = i / perRow;
                bool owned = flow.Meta.OwnsDecoration(deco.Id);

                string price = deco.EventOnly
                    ? flow.Strings.Get("tidepool.eventReward")
                    : string.Format(flow.Strings.Get("tidepool.buy"), deco.Cost);
                string label = owned ? deco.Name : $"{deco.Name}\n{price}";
                Decoration decoCopy = deco;
                Button cell = UiComponents.ButtonSecondary(grid, $"deco_{deco.Id}", label,
                    () => OnTrayDecoTapped(decoCopy));
                var cellRt = (RectTransform)cell.transform;
                UiComponents.Place(cellRt, new Vector2((col + 0.5f) / perRow, 1f), new Vector2(225f, 130f));
                cellRt.pivot = new Vector2(0.5f, 1f);
                cellRt.anchoredPosition = new Vector2(0f, -8f - row * rowStep);
                cell.GetComponentInChildren<TextMeshProUGUI>().fontSize =
                    ThemeRuntime.Theme.TypeStyle("micro").Size;

                bool selected = owned && selectedDecoration == deco.Id;
                cell.GetComponent<PopSurface>().SetTone(selected ? PopTone.Yellow
                    : !owned && deco.EventOnly ? PopTone.Purple : PopTone.White);
                cell.interactable = owned || (!deco.EventOnly && flow.Meta.CanAfford(deco.Cost));
            }

            grid.sizeDelta = new Vector2(0f, 16f + ((decorations.Count + perRow - 1) / perRow) * rowStep);
        }

        private void OnTrayDecoTapped(Decoration deco)
        {
            if (flow.Meta.OwnsDecoration(deco.Id))
            {
                selectedDecoration = selectedDecoration == deco.Id ? "" : deco.Id;
                Refresh();
            }
            else if (flow.TryBuyDecoration(deco))
            {
                selectedDecoration = deco.Id;
                Refresh();
            }
        }

        public void Refresh()
        {
            coins.SetInstant(flow.Meta.Coins);
            eggCount.text = flow.Eggs.ToString();
            editToggle.GetComponentInChildren<TextMeshProUGUI>().text =
                flow.Strings.Get(editMode ? "tidepool.done" : "tidepool.edit");

            for (int slot = 0; slot < decoSlots.Count; slot++)
            {
                (Button marker, RectTransform _, TextMeshProUGUI label, Image dot) = decoSlots[slot];
                string placed = flow.Meta.DecorationAt(slot);
                bool hasDeco = !string.IsNullOrEmpty(placed);
                Decoration? deco = hasDeco ? Find(placed) : null;
                label.text = deco != null ? deco.Name : "";
                dot.color = deco != null
                    ? ThemeRuntime.Color("coin")
                    : (editMode ? ThemeRuntime.Color("panel.light") : Color.clear);
                marker.interactable = editMode;
            }

            journal.gameObject.SetActive(!editMode);
            tray.gameObject.SetActive(editMode);
            hint.gameObject.SetActive(editMode && !string.IsNullOrEmpty(selectedDecoration));
            if (editMode)
            {
                RebuildTray();
            }

            RefreshPod();
            RefreshJournal();
        }

        private Decoration? Find(string id)
        {
            foreach (Decoration deco in flow.Decorations)
            {
                if (deco.Id == id)
                {
                    return deco;
                }
            }

            return null;
        }

        private void Update()
        {
            UpdatePod();

            // §4.6 parallax parks under reduced motion.
            if (ThemeRuntime.ReducedMotion)
            {
                return;
            }

            float scrollX = content.anchoredPosition.x;
            parallaxFar.anchoredPosition = new Vector2(scrollX * 0.25f, parallaxFar.anchoredPosition.y);
            parallaxNear.anchoredPosition = new Vector2(scrollX * 0.55f, parallaxNear.anchoredPosition.y);
        }
    }
}
