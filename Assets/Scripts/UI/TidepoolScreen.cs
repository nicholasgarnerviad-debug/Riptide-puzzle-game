using System.Collections.Generic;
using Riptide.Core;
using Riptide.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// Spec §4.6 Tidepool: a 3-screen-wide horizontally scrolling diorama with a
    /// 2-layer parallax background, creatures idle-bobbing at fixed slots (locked
    /// species = dark silhouette + "???"), tap → bottom card with portrait, name,
    /// lifetime rescues and a flavor line cycling its 3 entries. Decorations get
    /// an edit-mode toggle: owned pieces place at predefined slot points (save
    /// v3); unowned show in the tray with their coin cost (the shop entry).
    /// </summary>
    public sealed class TidepoolScreen : MonoBehaviour, IScreenRefresh
    {
        private const int DecoSlotCount = 8;
        private const float ContentWidth = 3240f; // §4.6: three screens wide

        private GameFlow flow = null!;
        private CoinCounter coins = null!;
        private RectTransform content = null!;
        private RectTransform parallaxFar = null!;
        private RectTransform parallaxNear = null!;
        private ScrollRect scroll = null!;
        private Button editToggle = null!;
        private RectTransform infoCard = null!;
        private TextMeshProUGUI infoName = null!;
        private TextMeshProUGUI infoCount = null!;
        private TextMeshProUGUI infoFlavor = null!;
        private Image infoPortrait = null!;
        private RectTransform tray = null!;
        private TextMeshProUGUI hint = null!;

        private readonly List<(int id, CreatureChip chip, TextMeshProUGUI name, RectTransform rt, float baseY)> chips
            = new List<(int, CreatureChip, TextMeshProUGUI, RectTransform, float)>();
        private readonly List<(Button marker, RectTransform slotRt, TextMeshProUGUI label, Image dot)> decoSlots
            = new List<(Button, RectTransform, TextMeshProUGUI, Image)>();

        private bool editMode;
        private string selectedDecoration = "";
        private int flavorCursor;
        private int shownSpecies = -1;
        private float bobTime;

        public static RectTransform Build(RectTransform parent, GameFlow flow)
        {
            RectTransform root = ScreenChrome.Root(parent, "TidepoolScreen");
            var screen = root.gameObject.AddComponent<TidepoolScreen>();
            screen.flow = flow;

            TextMeshProUGUI title = UiText.Create(root, "title", flow.Strings.Get("tidepool.title"),
                "title", "text.primary");
            UiComponents.Place(title.rectTransform, new Vector2(0.5f, 0.945f), new Vector2(600f, 90f));

            // Header consistency with Home/Shop: balance pill left, action right.
            RectTransform coinPill = UiComponents.Rect(root, "coinPill", new Vector2(250f, 78f));
            UiComponents.Place(coinPill, new Vector2(0.155f, 0.94f), new Vector2(250f, 78f));
            Image pillImage = UiComponents.RoundedImage(coinPill.gameObject, 39f);
            pillImage.raycastTarget = false;
            ThemedElement.Bind(coinPill.gameObject, "bg.surface");
            UiComponents.RoundedStrokeImage(coinPill, "stroke.subtle", 39f);
            screen.coins = UiComponents.CoinCounterComponent(coinPill);
            UiComponents.Place((RectTransform)screen.coins.transform, new Vector2(0.5f, 0.5f), new Vector2(230f, 64f));

            screen.editToggle = UiComponents.ButtonSecondary(root, "edit", flow.Strings.Get("tidepool.edit"),
                screen.ToggleEdit);
            UiComponents.Place((RectTransform)screen.editToggle.transform, new Vector2(0.85f, 0.94f), new Vector2(230f, 92f));

            screen.BuildDiorama(root);
            screen.BuildInfoCard(root);
            screen.BuildTray(root);

            screen.hint = UiText.Create(root, "hint", flow.Strings.Get("tidepool.placeHint"),
                "caption", "accent.primary");
            UiComponents.Place(screen.hint.rectTransform, new Vector2(0.5f, 0.40f), new Vector2(900f, 60f));

            Button back = UiComponents.ButtonGhost(root, "back", flow.Strings.Get("common.back"),
                () => flow.GoTo(FlowScreen.Home));
            UiComponents.Place((RectTransform)back.transform, new Vector2(0.5f, 0.035f), new Vector2(380f, 80f));

            screen.Refresh();
            return root;
        }

        private void BuildDiorama(RectTransform root)
        {
            RectTransform viewport = UiComponents.Rect(root, "diorama", Vector2.zero);
            viewport.anchorMin = new Vector2(0.02f, 0.44f);
            viewport.anchorMax = new Vector2(0.98f, 0.875f);
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.sprite = SpriteFactory.Solid();
            ThemedElement.Bind(viewport.gameObject, "bg.abyss");
            viewport.gameObject.AddComponent<RectMask2D>();

            // Visual pass: the diorama is a SCENE, not stacked rectangles —
            // water column light, sunbeams, then dune/kelp silhouette layers.
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

            // §4.6 parallax: two silhouette layers tracked against the scroll.
            parallaxFar = SilhouetteLayer(viewport, "far", "bg.deep", 300f, kelp: false);
            parallaxNear = SilhouetteLayer(viewport, "near", "bg.raised", 230f, kelp: true);

            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            content = UiComponents.Rect(viewport, "content", Vector2.zero);
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.sizeDelta = new Vector2(ContentWidth, 0f);
            scroll.content = content;
            scroll.horizontal = true;
            scroll.vertical = false;

            // Creature slots, fixed positions across the three screens.
            for (int i = 0; i < flow.Roster.Count; i++)
            {
                CreatureSpecies species = flow.Roster.Species[i];
                int speciesId = species.Id;
                float x = 260f + i * (ContentWidth - 480f) / Mathf.Max(1, flow.Roster.Count - 1);
                float y = (i % 2 == 0) ? 110f : -10f;

                // Visual pass: each creature sits in a circular well so it reads
                // as an inhabitant, not a blob adrift in the void.
                RectTransform well = UiComponents.Rect(content, $"well{speciesId}", new Vector2(132f, 132f));
                UiComponents.Place(well, new Vector2(0f, 0.5f), new Vector2(132f, 132f));
                well.anchoredPosition = new Vector2(x, y);
                var wellBg = well.gameObject.AddComponent<Image>();
                wellBg.sprite = SpriteFactory.Dot();
                wellBg.raycastTarget = false;
                ThemedElement.Bind(well.gameObject, "bg.surface");
                RectTransform wellInner = UiComponents.Rect(well, "inner", new Vector2(118f, 118f));
                UiComponents.Place(wellInner, new Vector2(0.5f, 0.5f), new Vector2(118f, 118f));
                var wellInnerImage = wellInner.gameObject.AddComponent<Image>();
                wellInnerImage.sprite = SpriteFactory.Dot();
                wellInnerImage.raycastTarget = false;
                ThemedElement.Bind(wellInner.gameObject, "bg.raised");

                CreatureChip chip = UiComponents.CreatureChipComponent(well, (byte)speciesId);
                var chipRt = (RectTransform)chip.transform;
                UiComponents.Place(chipRt, new Vector2(0.5f, 0.5f), new Vector2(88f, 88f));
                var button = chip.gameObject.AddComponent<Button>();
                button.onClick.AddListener(() => OnSpeciesTapped(speciesId));

                TextMeshProUGUI name = UiText.Create(content, $"name{speciesId}", "", "micro", "text.secondary");
                UiComponents.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(220f, 40f));
                name.rectTransform.anchoredPosition = new Vector2(x, y - 100f);

                chips.Add((speciesId, chip, name, well, y));
            }

            // §4.6 decoration slot points along the floor.
            for (int slot = 0; slot < DecoSlotCount; slot++)
            {
                float x = 200f + slot * (ContentWidth - 400f) / (DecoSlotCount - 1);
                RectTransform slotRt = UiComponents.Rect(content, $"decoSlot{slot}", new Vector2(120f, 120f));
                UiComponents.Place(slotRt, new Vector2(0f, 0.5f), new Vector2(120f, 120f));
                slotRt.anchoredPosition = new Vector2(x, -210f);

                var dotGo = new GameObject("dot", typeof(RectTransform));
                dotGo.transform.SetParent(slotRt, false);
                var dot = dotGo.AddComponent<Image>();
                dot.sprite = SpriteFactory.Dot();
                ((RectTransform)dotGo.transform).sizeDelta = new Vector2(64f, 64f);

                TextMeshProUGUI label = UiText.Create(slotRt, "label", "", "micro", "text.muted");
                UiComponents.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(200f, 36f));

                var marker = slotRt.gameObject.AddComponent<Button>();
                int slotCopy = slot;
                marker.onClick.AddListener(() => OnSlotTapped(slotCopy));
                decoSlots.Add((marker, slotRt, label, dot));
            }
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

            // Tiled dune strips across the layer width.
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
                float[] kelpXs = { 180f, 740f, 1260f, 1840f, 2420f, 2980f };
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
                    ThemedElement.Bind(prop.gameObject, "accent.deep");
                }
            }

            return layer;
        }

        private void BuildInfoCard(RectTransform root)
        {
            infoCard = UiComponents.Card(root, "infoCard", new Vector2(980f, 520f));
            UiComponents.Place(infoCard, new Vector2(0.5f, 0.225f), new Vector2(980f, 520f));

            RectTransform portraitWell = UiComponents.Rect(infoCard, "portraitWell", new Vector2(220f, 220f));
            UiComponents.Place(portraitWell, new Vector2(0.16f, 0.62f), new Vector2(220f, 220f));
            var wellImage = portraitWell.gameObject.AddComponent<Image>();
            wellImage.sprite = SpriteFactory.Dot();
            wellImage.raycastTarget = false;
            ThemedElement.Bind(portraitWell.gameObject, "bg.raised");

            var portraitGo = new GameObject("portrait", typeof(RectTransform));
            portraitGo.transform.SetParent(portraitWell, false);
            infoPortrait = portraitGo.AddComponent<Image>();
            infoPortrait.sprite = SpriteFactory.Creature();
            UiComponents.Place((RectTransform)portraitGo.transform, new Vector2(0.5f, 0.5f), new Vector2(170f, 170f));

            infoName = UiText.Create(infoCard, "name", "", "heading", "text.primary");
            UiComponents.Place(infoName.rectTransform, new Vector2(0.62f, 0.78f), new Vector2(560f, 70f));
            infoCount = UiText.Create(infoCard, "count", "", "body", "text.secondary");
            UiComponents.Place(infoCount.rectTransform, new Vector2(0.62f, 0.60f), new Vector2(560f, 60f));
            infoFlavor = UiText.Create(infoCard, "flavor", "", "caption", "accent.primary");
            UiComponents.Place(infoFlavor.rectTransform, new Vector2(0.5f, 0.24f), new Vector2(900f, 110f));
        }

        private void BuildTray(RectTransform root)
        {
            tray = UiComponents.Card(root, "decoTray", new Vector2(1020f, 560f));
            UiComponents.Place(tray, new Vector2(0.5f, 0.225f), new Vector2(1020f, 560f));
        }

        private void ToggleEdit()
        {
            editMode = !editMode;
            selectedDecoration = "";
            Refresh();
        }

        private void OnSpeciesTapped(int speciesId)
        {
            if (editMode)
            {
                return;
            }

            CreatureSpecies species = flow.Roster.Species[speciesId];
            bool rescued = flow.Meta.Save.RescuesFor(speciesId) > 0;
            flavorCursor = shownSpecies == speciesId ? (flavorCursor + 1) % species.Flavor.Count : 0;
            shownSpecies = speciesId;

            infoPortrait.color = rescued ? Palette.CreatureColor((byte)speciesId) : ThemeRuntime.Color("bg.raised");
            infoName.text = rescued ? species.Name : flow.Strings.Get("tidepool.unknown");
            infoCount.text = rescued
                ? string.Format(flow.Strings.Get("tidepool.rescued"), flow.Meta.Save.RescuesFor(speciesId))
                : flow.Strings.Get("tidepool.never");
            infoFlavor.text = rescued ? species.Flavor[flavorCursor] : "";
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
                flow.Strings.Get("tidepool.decorations"), "heading", "text.primary");
            UiComponents.Place(heading.rectTransform, new Vector2(0.5f, 0.92f), new Vector2(700f, 60f));

            var decorations = flow.Decorations;
            int perRow = 4;
            for (int i = 0; i < decorations.Count; i++)
            {
                Decoration deco = decorations[i];
                int col = i % perRow;
                int row = i / perRow;
                bool owned = flow.Meta.OwnsDecoration(deco.Id);

                string label = owned
                    ? deco.Name
                    : $"{deco.Name}\n{string.Format(flow.Strings.Get("tidepool.buy"), deco.Cost)}";
                Decoration decoCopy = deco;
                Button cell = UiComponents.ButtonSecondary(tray, $"deco_{deco.Id}", label,
                    () => OnTrayDecoTapped(decoCopy));
                var cellRt = (RectTransform)cell.transform;
                UiComponents.Place(cellRt, new Vector2((col + 0.5f) / perRow, 0.78f), new Vector2(235f, 130f));
                cellRt.anchoredPosition = new Vector2(0f, -row * 145f);
                cell.GetComponentInChildren<TextMeshProUGUI>().fontSize =
                    ThemeRuntime.Theme.TypeStyle("micro").Size;

                bool selected = owned && selectedDecoration == deco.Id;
                cell.GetComponent<Image>().color = selected
                    ? ThemeRuntime.Color("accent.deep")
                    : ThemeRuntime.Color("bg.raised");
                cell.interactable = owned || flow.Meta.CanAfford(deco.Cost);
            }
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
            editToggle.GetComponentInChildren<TextMeshProUGUI>().text =
                flow.Strings.Get(editMode ? "tidepool.done" : "tidepool.edit");

            foreach ((int id, CreatureChip chip, TextMeshProUGUI name, RectTransform _, float _) in chips)
            {
                bool rescued = flow.Meta.Save.RescuesFor(id) > 0;
                chip.Apply(rescued ? CreatureChip.State.Normal : CreatureChip.State.Silhouette);
                name.text = rescued ? flow.Roster.Species[id].Name : flow.Strings.Get("tidepool.unknown");
            }

            for (int slot = 0; slot < decoSlots.Count; slot++)
            {
                (Button marker, RectTransform _, TextMeshProUGUI label, Image dot) = decoSlots[slot];
                string placed = flow.Meta.DecorationAt(slot);
                bool hasDeco = !string.IsNullOrEmpty(placed);
                Decoration? deco = hasDeco ? Find(placed) : null;
                label.text = deco != null ? deco.Name : "";
                dot.color = deco != null
                    ? ThemeRuntime.Color("coin")
                    : (editMode ? ThemeRuntime.Color("stroke.bright") : Color.clear);
                marker.interactable = editMode;
            }

            infoCard.gameObject.SetActive(!editMode);
            tray.gameObject.SetActive(editMode);
            hint.gameObject.SetActive(editMode && !string.IsNullOrEmpty(selectedDecoration));
            if (editMode)
            {
                RebuildTray();
            }
            else if (shownSpecies < 0)
            {
                infoName.text = "";
                infoCount.text = "";
                // An empty card is a dead card — invite the tap instead.
                infoFlavor.text = flow.Strings.Get("tidepool.tapHint");
                infoPortrait.color = ThemeRuntime.Color("bg.surface");
            }
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
            // §4.6 idle bob + parallax; both park under reduced motion.
            if (ThemeRuntime.ReducedMotion)
            {
                return;
            }

            bobTime += Time.deltaTime;
            for (int i = 0; i < chips.Count; i++)
            {
                (int _, CreatureChip _, TextMeshProUGUI _, RectTransform rt, float baseY) = chips[i];
                Vector2 pos = rt.anchoredPosition;
                pos.y = baseY + Mathf.Sin(bobTime * 1.4f + i * 0.9f) * 6f;
                rt.anchoredPosition = pos;
            }

            float scrollX = content.anchoredPosition.x;
            parallaxFar.anchoredPosition = new Vector2(scrollX * 0.25f, parallaxFar.anchoredPosition.y);
            parallaxNear.anchoredPosition = new Vector2(scrollX * 0.55f, parallaxNear.anchoredPosition.y);
        }
    }
}
