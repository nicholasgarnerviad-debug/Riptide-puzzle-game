using System;
using Riptide.Core;
using Riptide.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// A Tideling's card, up close (GDD §16.2 / §16.4; mockup C): the big
    /// tier-framed card, its ability now and at the next stage, bond toward growing
    /// up, the three Pod slots (Leader · Slot 2 · Slot 3; locked ones say when they
    /// open), Grow up! when it's ready (→ the celebration) and the Sparkle toggle.
    /// Unmet Tidelings say how to meet them — Call-only ones offer the Conch.
    /// </summary>
    public sealed class TidelingSheet : Overlay
    {
        private GameFlow flow = null!;
        private Action openConch = null!;
        private TidelingCard card = null!;
        private Button sparkle = null!;
        private TextMeshProUGUI abilityName = null!;
        private TextMeshProUGUI abilityDesc = null!;
        private TextMeshProUGUI nextLine = null!;
        private PopBar bond = null!;
        private RectTransform podRow = null!;
        private readonly Button[] slots = new Button[3];
        private Button action = null!;
        private TextMeshProUGUI actionLabel = null!;
        private GrowUpCelebration? celebration;
        private int line = -1;

        /// <summary>The line on the card. Test surface.</summary>
        public int Line => line;

        public static TidelingSheet Build(RectTransform canvasRoot, GameFlow flow, Action openConch)
        {
            TidelingSheet sheet = Create<TidelingSheet>(canvasRoot, "TidelingSheet", "scrim", "scrim", opaque: false);
            sheet.flow = flow;
            sheet.openConch = openConch;
            RectTransform root = sheet.Content;

            Button close = PopKit.IconButton(root, "close", "close", PopTone.White, sheet.Close, new Vector2(124f, 130f));
            var closeRt = (RectTransform)close.transform;
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(0f, 1f);
            closeRt.anchoredPosition = new Vector2(100f, -100f);

            sheet.card = TidelingCard.Create(root, "card", new Vector2(520f, 700f), abilityPlate: false, null);
            sheet.card.Badges = false; // the sheet's own buttons say Pod and Grow up
            sheet.card.Rect.anchorMin = sheet.card.Rect.anchorMax = new Vector2(0.5f, 1f);
            sheet.card.Rect.pivot = new Vector2(0.5f, 1f);
            sheet.card.Rect.anchoredPosition = new Vector2(0f, -190f);

            sheet.sparkle = PopKit.Button(root, "sparkle", "", PopTone.Purple, sheet.ToggleSparkle,
                new Vector2(330f, 100f), "caption", "glint");
            var sparkleRt = (RectTransform)sheet.sparkle.transform;
            sparkleRt.anchorMin = sparkleRt.anchorMax = new Vector2(0.5f, 1f);
            sparkleRt.anchoredPosition = new Vector2(0f, -950f);

            RectTransform info = UiComponents.Card(root, "info", new Vector2(1000f, 720f));
            info.anchorMin = info.anchorMax = new Vector2(0.5f, 0f);
            info.pivot = new Vector2(0.5f, 0f);
            info.anchoredPosition = new Vector2(0f, 50f);

            sheet.abilityName = UiText.Create(info, "abilityName", "", "heading", "text.ink");
            Row(sheet.abilityName.rectTransform, -72f, 64f);
            sheet.abilityDesc = UiText.Create(info, "abilityDesc", "", "body", "text.ink");
            sheet.abilityDesc.enableWordWrapping = true;
            Row(sheet.abilityDesc.rectTransform, -146f, 84f);
            sheet.nextLine = UiText.Create(info, "next", "", "caption", "text.inkSoft");
            sheet.nextLine.enableWordWrapping = true;
            Row(sheet.nextLine.rectTransform, -222f, 50f);

            sheet.bond = PopBar.Create(info, "bond", new Vector2(880f, 70f), PopTone.Coral);
            sheet.bond.Rect.anchorMin = sheet.bond.Rect.anchorMax = new Vector2(0.5f, 1f);
            sheet.bond.Rect.anchoredPosition = new Vector2(0f, -300f);

            sheet.podRow = UiComponents.Rect(info, "pod", new Vector2(900f, 112f));
            sheet.podRow.anchorMin = sheet.podRow.anchorMax = new Vector2(0.5f, 1f);
            sheet.podRow.anchoredPosition = new Vector2(0f, -420f);
            for (int slot = 0; slot < sheet.slots.Length; slot++)
            {
                int slotCopy = slot;
                sheet.slots[slot] = PopKit.Button(sheet.podRow, $"slot{slot}", "", PopTone.White,
                    () => sheet.OnSlot(slotCopy), new Vector2(288f, 112f), "caption", slot == 0 ? "crown" : null);
                var slotRt = (RectTransform)sheet.slots[slot].transform;
                slotRt.anchorMin = slotRt.anchorMax = new Vector2(0.5f, 0.5f);
                slotRt.anchoredPosition = new Vector2((slot - 1) * 302f, 0f);
                TextMeshProUGUI label = Label(sheet.slots[slot]);
                label.enableAutoSizing = true;
                label.fontSizeMin = 18f;
                label.fontSizeMax = label.fontSize;
            }

            sheet.action = PopKit.Button(info, "action", "", PopTone.Green, sheet.OnAction, new Vector2(880f, 140f),
                "heading");
            var actionRt = (RectTransform)sheet.action.transform;
            actionRt.anchorMin = actionRt.anchorMax = new Vector2(0.5f, 0f);
            actionRt.pivot = new Vector2(0.5f, 0f);
            actionRt.anchoredPosition = new Vector2(0f, PopKit.Lip + 34f);
            sheet.actionLabel = Label(sheet.action);
            return sheet;
        }

        private static void Row(RectTransform rt, float y, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(48f, y - height * 0.5f);
            rt.offsetMax = new Vector2(-48f, y + height * 0.5f);
        }

        private static TextMeshProUGUI Label(Button button) =>
            PopKit.FaceOf(button).Find("label").GetComponent<TextMeshProUGUI>();

        /// <summary>Shows a Tideling's card; <paramref name="closed"/> runs when it is dismissed.</summary>
        public void Show(int tideling, Action? closed = null)
        {
            line = tideling;
            Open(closed);
            Refresh();
        }

        private void Refresh()
        {
            TidelingView t = flow.Tideling(line);
            PopTone tone = TidelingUi.Tone(t.Line.Rarity);
            SetBackdrop("scrim", "scrim");
            card.Set(flow, t);

            sparkle.gameObject.SetActive(t.Sparkle);
            Label(sparkle).text = flow.Strings.Get(t.SparkleOn ? "card.sparkleOn" : "card.sparkleOff");
            sparkle.GetComponent<PopSurface>().SetTone(t.SparkleOn ? PopTone.Purple : PopTone.White);
            ApplyLabelTone(sparkle, t.SparkleOn ? PopTone.Purple : PopTone.White);

            abilityName.text = TidelingUi.AbilityName(flow, t.Line);
            ThemedElement.Bind(abilityName.gameObject, PopKit.Edge(tone));
            abilityDesc.text = TidelingUi.AbilityDesc(flow, t.Line, Math.Max(1, t.Stage));
            podRow.gameObject.SetActive(t.Owned);
            bond.gameObject.SetActive(t.Owned);
            if (!t.Owned)
            {
                bool callOnly = t.Line.Rarity != PalRarity.Common;
                nextLine.text = flow.Strings.Get(callOnly ? "pet.callOnly" : "card.rescueOnly");
                action.gameObject.SetActive(callOnly);
                SetAction(PopTone.Yellow, flow.Strings.Get("conch.title"));
                return;
            }

            nextLine.text = t.Stage < PalsLoader.StageCount
                ? string.Format(flow.Strings.Get("card.next"), TidelingUi.StageName(flow, t.Stage + 1),
                    TidelingUi.AbilityShort(flow, t.Line, t.Stage + 1)) + "  ·  " + flow.Strings.Get("tl.notDaily")
                : flow.Strings.Get("tl.notDaily");
            if (t.NextBond.HasValue)
            {
                bond.Set(t.Bond, t.NextBond.Value, t.CanGrow
                    ? flow.Strings.Get("pet.ready")
                    : string.Format(flow.Strings.Get("pet.next"), t.Bond, t.NextBond.Value));
            }
            else
            {
                bond.Set(1, 1, flow.Strings.Get("pet.maxed"));
            }

            int open = flow.PodSlotsOpen;
            for (int slot = 0; slot < slots.Length; slot++)
            {
                bool here = t.PodSlot == slot;
                bool locked = slot >= open;
                PopTone slotTone = here ? PopTone.Yellow : locked ? PopTone.Locked : PopTone.White;
                slots[slot].GetComponent<PopSurface>().SetTone(slotTone);
                slots[slot].interactable = !locked && !(here && slot == 0);
                Label(slots[slot]).text = locked
                    ? string.Format(flow.Strings.Get("card.slotLocked"), flow.Pals.PodUnlocks[slot])
                    : slot == 0 ? flow.Strings.Get("card.leader") : string.Format(flow.Strings.Get("card.slot"), slot + 1);
                ApplyLabelTone(slots[slot], slotTone);
            }

            action.gameObject.SetActive(t.CanGrow);
            SetAction(PopTone.Green, flow.Strings.Get("pet.growUp"));
        }

        private void SetAction(PopTone tone, string text)
        {
            action.GetComponent<PopSurface>().SetTone(tone);
            actionLabel.text = text;
            UiText.Outline(actionLabel, PopKit.Edge(tone));
        }

        private static void ApplyLabelTone(Button button, PopTone tone)
        {
            TextMeshProUGUI label = Label(button);
            ThemedElement.Bind(label.gameObject, PopKit.LabelColor(tone));
            if (tone == PopTone.White)
            {
                label.fontSharedMaterial = label.font.material;
            }
            else
            {
                UiText.Outline(label, PopKit.Edge(tone));
            }
        }

        private void OnSlot(int slot)
        {
            TidelingView t = flow.Tideling(line);
            bool changed = t.PodSlot == slot ? flow.TrySetPod(slot, -1) : flow.TrySetPod(slot, line);
            if (changed)
            {
                UiJuice.Play("star");
                Refresh();
            }
        }

        private void ToggleSparkle()
        {
            TidelingView t = flow.Tideling(line);
            if (flow.TrySetSparkle(line, !t.SparkleOn))
            {
                Refresh();
            }
        }

        private void OnAction()
        {
            TidelingView before = flow.Tideling(line);
            if (!before.Owned)
            {
                Close();
                openConch();
                return;
            }

            bool hadReward = flow.LineRewarded(line);
            if (!flow.TryGrow(line))
            {
                return;
            }

            bool reward = !hadReward && flow.LineRewarded(line);
            Refresh();
            RectTransform canvasRoot = (RectTransform)transform.parent;
            celebration ??= GrowUpCelebration.Build(canvasRoot, flow);
            celebration.Show(line, before.Stage, reward);
        }
    }
}
