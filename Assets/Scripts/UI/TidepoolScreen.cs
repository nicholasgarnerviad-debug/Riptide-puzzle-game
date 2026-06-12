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
            UiComponents.Place(title.rectTransform, new Vector2(0.40f, 0.945f), new Vector2(600f, 90f));
            screen.coins = UiComponents.CoinCounterComponent(root);
            UiComponents.Place((RectTransform)screen.coins.transform, new Vector2(0.40f, 0.895f), new Vector2(300f, 60f));

            screen.editToggle = UiComponents.ButtonSecondary(root, "edit", flow.Strings.Get("tidepool.edit"),
                screen.ToggleEdit);
            UiComponents.Place((RectTransform)screen.editToggle.transform, new Vector2(0.84f, 0.93f), new Vector2(260f, 100f));

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

            // §4.6 parallax: two background layers tracked against the scroll.
            parallaxFar = ParallaxLayer(viewport, "far", "bg.deep", 220f, -160f);
            parallaxNear = ParallaxLayer(viewport, "near", "bg.raised", 130f, -260f);

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

                CreatureChip chip = UiComponents.CreatureChipComponent(content, (byte)speciesId);
                var chipRt = (RectTransform)chip.transform;
                UiComponents.Place(chipRt, new Vector2(0f, 0.5f), new Vector2(88f, 88f));
                chipRt.anchoredPosition = new Vector2(x, y);
                var button = chip.gameObject.AddComponent<Button>();
                button.onClick.AddListener(() => OnSpeciesTapped(speciesId));

                TextMeshProUGUI name = UiText.Create(content, $"name{speciesId}", "", "micro", "text.secondary");
                UiComponents.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(220f, 40f));
                name.rectTransform.anchoredPosition = new Vector2(x, y - 80f);

                chips.Add((speciesId, chip, name, chipRt, y));
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

        private static RectTransform ParallaxLayer(RectTransform viewport, string name, string colorToken,
            float height, float y)
        {
            RectTransform layer = UiComponents.Rect(viewport, name, Vector2.zero);
            layer.anchorMin = new Vector2(0f, 0.5f);
            layer.anchorMax = new Vector2(0f, 0.5f);
            layer.pivot = new Vector2(0f, 0.5f);
            layer.sizeDelta = new Vector2(ContentWidth, height);
            layer.anchoredPosition = new Vector2(0f, y);
            var image = layer.gameObject.AddComponent<Image>();
            image.sprite = SpriteFactory.Solid();
            image.raycastTarget = false;
            ThemedElement.Bind(layer.gameObject, colorToken);
            return layer;
        }

        private void BuildInfoCard(RectTransform root)
        {
            infoCard = UiComponents.Card(root, "infoCard", new Vector2(980f, 520f));
            UiComponents.Place(infoCard, new Vector2(0.5f, 0.225f), new Vector2(980f, 520f));

            var portraitGo = new GameObject("portrait", typeof(RectTransform));
            portraitGo.transform.SetParent(infoCard, false);
            infoPortrait = portraitGo.AddComponent<Image>();
            infoPortrait.sprite = SpriteFactory.Creature();
            UiComponents.Place((RectTransform)portraitGo.transform, new Vector2(0.16f, 0.62f), new Vector2(180f, 180f));

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
                infoFlavor.text = "";
                infoPortrait.color = ThemeRuntime.Color("bg.raised");
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
