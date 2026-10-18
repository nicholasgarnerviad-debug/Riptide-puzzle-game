using System;
using Riptide.Core;
using Riptide.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// The tier-framed Tideling card (GDD §16.6, mockup C "Pod Cards"): a frame in
    /// the tier's tone, a light face tinted toward it, stage dots and the tier word,
    /// the form, its name and (optionally) an ability plate. Unmet Tidelings show a
    /// silhouette, "???" and how to meet them. Sizes scale off a 300-wide design.
    /// </summary>
    public sealed class TidelingCard : MonoBehaviour
    {
        private PopSurface frame = null!;
        private PopGradient face = null!;
        private readonly Image[] dots = new Image[PalsLoader.StageCount];
        private TextMeshProUGUI tier = null!;
        private Image art = null!;
        private TextMeshProUGUI nameLabel = null!;
        private Image? plate;
        private TextMeshProUGUI? plateName;
        private TextMeshProUGUI? plateValue;
        private GameObject crown = null!;
        private GameObject grow = null!;

        /// <summary>The pals.json line shown (−1 before the first Set).</summary>
        public int Line { get; private set; } = -1;

        public RectTransform Rect => (RectTransform)transform;

        /// <summary>Show the In-Pod crown and the Grow up! pill (off where the screen says so itself).</summary>
        public bool Badges { get; set; } = true;

        public Image Art => art;

        public static TidelingCard Create(RectTransform parent, string name, Vector2 size, bool abilityPlate,
            Action? onTap)
        {
            float k = size.x / 300f;
            float radius = 34f * k;
            RectTransform root;
            if (onTap != null)
            {
                root = (RectTransform)PopKit.Tile(parent, name, PopTone.Teal, onTap, size, radius).transform;
            }
            else
            {
                root = PopKit.Panel(parent, name, size, PopTone.Teal, radius, PopKit.Border);
            }

            var card = root.gameObject.AddComponent<TidelingCard>();
            card.frame = root.GetComponent<PopSurface>();
            RectTransform faceRt = card.frame.Face;

            float inset = 14f * k;
            Image inner = PopKit.Well(faceRt, "inner", "panel.light", radius - inset);
            PopKit.Inset(inner.rectTransform, inset);
            card.face = inner.gameObject.AddComponent<PopGradient>();
            RectTransform body = inner.rectTransform;

            float dot = 16f * k;
            for (int i = 0; i < card.dots.Length; i++)
            {
                card.dots[i] = PopKit.Img(body, $"dot{i}", PopSprites.Circle(), "panel.shade");
                RectTransform d = card.dots[i].rectTransform;
                d.anchorMin = d.anchorMax = new Vector2(0f, 1f);
                d.sizeDelta = new Vector2(dot, dot);
                d.anchoredPosition = new Vector2(20f * k + i * dot * 1.45f, -22f * k);
            }

            card.tier = UiText.Create(body, "tier", "", "micro", "text.ink", TextAlignmentOptions.MidlineRight);
            card.tier.fontSize = Mathf.Max(18f, 22f * k);
            card.tier.rectTransform.anchorMin = card.tier.rectTransform.anchorMax = new Vector2(1f, 1f);
            card.tier.rectTransform.pivot = new Vector2(1f, 0.5f);
            card.tier.rectTransform.sizeDelta = new Vector2(200f * k, 30f * k);
            card.tier.rectTransform.anchoredPosition = new Vector2(-14f * k, -22f * k);

            float plateHeight = abilityPlate ? 96f * k : 0f;
            float faceHeight = size.y - (onTap != null ? PopKit.Lip : 0f) - 2f * inset;
            float artSize = Mathf.Min(size.x * (abilityPlate ? 0.7f : 0.78f), faceHeight - plateHeight - 72f * k);
            card.art = PopKit.Img(body, "art", CreatureSprites.Tideling("crab", 1), "pop.white.top");
            card.art.preserveAspect = true;
            RectTransform artRt = card.art.rectTransform;
            artRt.anchorMin = artRt.anchorMax = new Vector2(0.5f, 1f);
            artRt.pivot = new Vector2(0.5f, 1f);
            artRt.sizeDelta = new Vector2(artSize, artSize);
            artRt.anchoredPosition = new Vector2(0f, -30f * k);

            card.nameLabel = UiText.Create(body, "name", "", "body", "text.ink");
            card.nameLabel.fontSize = 32f * k;
            card.nameLabel.enableAutoSizing = true;
            card.nameLabel.fontSizeMin = 18f;
            card.nameLabel.fontSizeMax = 32f * k;
            RectTransform nameRt = card.nameLabel.rectTransform;
            nameRt.anchorMin = new Vector2(0f, 0f);
            nameRt.anchorMax = new Vector2(1f, 0f);
            nameRt.pivot = new Vector2(0.5f, 0f);
            nameRt.offsetMin = new Vector2(10f * k, plateHeight + 14f * k);
            nameRt.offsetMax = new Vector2(-10f * k, plateHeight + 56f * k);

            if (abilityPlate)
            {
                card.plate = PopKit.Well(body, "plate", "panel.edge", 22f * k);
                RectTransform plateRt = card.plate.rectTransform;
                plateRt.anchorMin = new Vector2(0f, 0f);
                plateRt.anchorMax = new Vector2(1f, 0f);
                plateRt.pivot = new Vector2(0.5f, 0f);
                plateRt.offsetMin = new Vector2(8f * k, 8f * k);
                plateRt.offsetMax = new Vector2(-8f * k, plateHeight);
                card.plateName = UiText.Create(plateRt, "ability", "", "caption", "text.primary");
                card.plateName.fontSize = 22f * k;
                Fit(card.plateName, new Vector2(0f, 0.52f), new Vector2(1f, 1f), 14f);
                card.plateValue = UiText.Create(plateRt, "value", "", "heading", "text.primary");
                card.plateValue.fontSize = 32f * k;
                card.plateValue.enableAutoSizing = true;
                card.plateValue.fontSizeMin = 16f;
                card.plateValue.fontSizeMax = 32f * k;
                Fit(card.plateValue, new Vector2(0f, 0f), new Vector2(1f, 0.6f), 16f);
            }

            Image crownIcon = PopKit.Icon(root, "inPod", "crown", "pop.yellow", 64f * k, "pop.yellow.edge", 7);
            RectTransform crownRt = (RectTransform)crownIcon.transform.parent;
            crownRt.anchorMin = crownRt.anchorMax = new Vector2(1f, 1f);
            crownRt.anchoredPosition = new Vector2(-14f * k, -2f * k);
            crownRt.localRotation = Quaternion.Euler(0f, 0f, -14f);
            card.crown = crownRt.gameObject;

            RectTransform growPill = PopKit.Panel(root, "growUp", new Vector2(190f * k, 52f * k), PopTone.Green, 18f * k, 6f);
            growPill.anchorMin = growPill.anchorMax = new Vector2(0.5f, 0f);
            growPill.anchoredPosition = new Vector2(0f, 8f * k);
            TextMeshProUGUI growLabel = UiText.CreateOutlined(growPill, "label", "", "caption", "text.primary",
                "pop.green.edge");
            growLabel.fontSize = 26f * k;
            UiComponents.Stretch(growLabel.rectTransform);
            card.grow = growPill.gameObject;
            return card;
        }

        private static void Fit(TextMeshProUGUI text, Vector2 min, Vector2 max, float pad)
        {
            text.rectTransform.anchorMin = min;
            text.rectTransform.anchorMax = max;
            text.rectTransform.offsetMin = new Vector2(pad, 0f);
            text.rectTransform.offsetMax = new Vector2(-pad, 0f);
        }

        /// <summary>Shows a Tideling — met (its form, name, ability) or unmet (silhouette, "???").</summary>
        public void Set(GameFlow flow, TidelingView t)
        {
            Line = t.Line.Id;
            bool met = t.Owned;
            PopTone tone = TidelingUi.Tone(t.Line.Rarity);
            frame.SetTone(tone);
            face.Set("panel.light", PopKit.Top(tone));
            for (int i = 0; i < dots.Length; i++)
            {
                ThemedElement.Bind(dots[i].gameObject, i < t.Stage ? PopKit.Edge(tone) : "panel.shade");
            }

            tier.text = TidelingUi.TierName(flow, t.Line.Rarity);
            ThemedElement.Bind(tier.gameObject, PopKit.Edge(tone));
            art.sprite = met ? TidelingUi.Art(t) : TidelingUi.Silhouette(t.Line);
            ThemedElement.Bind(art.gameObject, met ? "pop.white.top" : "pop.locked");
            nameLabel.text = met ? t.FormName : flow.Strings.Get("tidepool.unknown");

            if (plate != null)
            {
                ThemedElement.Bind(plate.gameObject, met ? PopKit.Edge(tone) : "pop.locked");
                plateName!.text = met ? TidelingUi.AbilityName(flow, t.Line) : "";
                plateValue!.text = met
                    ? TidelingUi.AbilityShort(flow, t.Line, t.Stage)
                    : flow.Strings.Get(t.Line.Rarity == PalRarity.Common ? "tl.rescueToMeet" : "tl.callToMeet");
            }

            crown.SetActive(Badges && met && t.PodSlot >= 0);
            grow.SetActive(Badges && met && t.CanGrow);
            grow.GetComponentInChildren<TextMeshProUGUI>().text = flow.Strings.Get("pet.growUp");
        }

        /// <summary>Swaps the face for the blink / happy frame (owned forms only).</summary>
        public void SetHappy(GameFlow flow, bool happy)
        {
            if (Line < 0)
            {
                return;
            }

            TidelingView t = flow.Tideling(Line);
            if (t.Owned)
            {
                art.sprite = TidelingUi.Art(t, closed: happy);
            }
        }
    }
}
