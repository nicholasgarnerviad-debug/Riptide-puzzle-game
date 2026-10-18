using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// Spec §3 component library — Tidal Pop edition (UI spec §13): the same
    /// builders every screen calls, now producing the chunky sticker kit
    /// (<see cref="PopKit"/>). Editor tooling serializes these into Assets/Prefabs
    /// (DECISIONS: generated, never hand-authored). Every interactable is padded
    /// to the 120 ref-px target.
    /// </summary>
    public static class UiComponents
    {
        // ---------------- §3.1–3.5 buttons ----------------

        /// <summary>§3.1 the one action per screen: green candy button.</summary>
        public static Button ButtonPrimary(RectTransform parent, string name, string label, Action onClick) =>
            PopKit.Button(parent, name, label, PopTone.Green, onClick, new Vector2(640f, 150f));

        /// <summary>§3.2: white chunky button, ink label.</summary>
        public static Button ButtonSecondary(RectTransform parent, string name, string label, Action onClick) =>
            PopKit.Button(parent, name, label, PopTone.White, onClick, new Vector2(560f, 130f), "body");

        /// <summary>§3.3 tertiary: sticker text only (white with navy outline — reads on blue and white).</summary>
        public static Button ButtonGhost(RectTransform parent, string name, string label, Action onClick)
        {
            RectTransform root = Rect(parent, name, new Vector2(420f, 96f));
            var hit = root.gameObject.AddComponent<Image>();
            hit.sprite = SpriteFactory.Solid();
            hit.color = new Color(0f, 0f, 0f, 0f);
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClick());
            root.gameObject.AddComponent<PressEffect>();
            // On a light sheet the link is ink; over the ocean it is a sticker.
            bool onLight = parent.GetComponentInParent<Sheet>(true) != null;
            TextMeshProUGUI text = onLight
                ? UiText.Create(root, "label", label, "body", "text.inkSoft")
                : UiText.CreateOutlined(root, "label", label, "body", "text.primary", "panel.edge");
            Stretch(text.rectTransform);
            PadHitTarget(root);
            return button;
        }

        /// <summary>§3.4: always shows what you get — purple reward button with a play badge.</summary>
        public static Button ButtonReward(RectTransform parent, string name, string rewardLabel, Action onClick)
        {
            Button button = PopKit.Button(parent, name, rewardLabel, PopTone.Purple, onClick,
                new Vector2(620f, 130f), "body");
            RectTransform face = button.GetComponent<PopSurface>().Face;
            TextMeshProUGUI label = face.Find("label").GetComponent<TextMeshProUGUI>();

            RectTransform badge = Rect(face, "adBadge", new Vector2(76f, 58f));
            badge.anchorMin = badge.anchorMax = new Vector2(0f, 0.5f);
            badge.anchoredPosition = new Vector2(PopKit.Border + 56f, 0f);
            PopKit.Well(badge, "plate", "panel.light", 16f);
            Image play = PopKit.Img(badge, "play", PopSprites.Icon("play"), "pop.purple");
            Place(play.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(34f, 34f));
            label.rectTransform.offsetMin = new Vector2(PopKit.Border + 104f, 0f);
            label.rectTransform.offsetMax = new Vector2(-(PopKit.Border + 16f), 0f);
            return button;
        }

        /// <summary>§3.5: 96px chunky icon button. Text glyphs map onto the icon set.</summary>
        public static Button IconButton(RectTransform parent, string name, string glyph, Action onClick)
        {
            string iconId = glyph switch
            {
                "‹" => "chevronLeft",
                "›" => "chevronRight",
                "✕" => "close",
                "+" => "plus",
                _ => glyph,
            };
            return PopKit.IconButton(parent, name, iconId, PopTone.Blue, onClick, new Vector2(110f, 110f));
        }

        /// <summary>Rounded fill at a radius token (§1.3) — never stretch the corners.</summary>
        internal static Image RoundedImage(GameObject go, float cornerRefPx)
        {
            var image = go.AddComponent<Image>();
            image.sprite = PopSprites.Rounded();
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = PopSprites.CornerMultiplier(cornerRefPx);
            return image;
        }

        internal static Image RoundedStrokeImage(RectTransform parent, string colorToken, float cornerRefPx)
        {
            var go = new GameObject("stroke", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            var image = go.AddComponent<Image>();
            image.sprite = SpriteFactory.RoundedStroke();
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 14f * (100f / 64f) / Mathf.Max(8f, cornerRefPx);
            image.raycastTarget = false;
            ThemedElement.Bind(go, colorToken);
            return image;
        }

        // ---------------- §3.6–3.9 surfaces ----------------

        /// <summary>§3.6 card: white chunky panel. Content uses the ink text tokens.</summary>
        public static RectTransform Card(RectTransform parent, string name, Vector2 size) =>
            PopKit.Card(parent, name, size);

        /// <summary>§3.7 bottom sheet: white body with a navy rim and handle; scrim behind.</summary>
        public static Sheet SheetComponent(RectTransform parent, string name, float heightRefPx)
        {
            RectTransform scrim = Rect(parent, name + "_scrim", Vector2.zero);
            Stretch(scrim);
            var scrimImage = scrim.gameObject.AddComponent<Image>();
            scrimImage.sprite = SpriteFactory.Solid();
            ThemedElement.Bind(scrim.gameObject, "scrim");

            // The rounded body runs past the screen's bottom edge so only the
            // top corners show (r.l, §1.3: sheets).
            const float tuck = 90f;
            RectTransform body = Rect(scrim, name, new Vector2(0f, heightRefPx));
            body.anchorMin = new Vector2(0f, 0f);
            body.anchorMax = new Vector2(1f, 0f);
            body.pivot = new Vector2(0.5f, 0f);
            body.offsetMin = new Vector2(0f, -tuck);
            body.offsetMax = new Vector2(0f, heightRefPx);
            Image rim = RoundedImage(body.gameObject, PopKit.Radius("r.l"));
            ThemedElement.Bind(body.gameObject, "panel.edge");
            Image fill = PopKit.Well(body, "fill", "panel.light", PopKit.Radius("r.l") - PopKit.Border);
            fill.rectTransform.offsetMin = new Vector2(PopKit.Border, PopKit.Border);
            fill.rectTransform.offsetMax = new Vector2(-PopKit.Border, -PopKit.Border);
            rim.raycastTarget = true;

            RectTransform handle = Rect(body, "handle", new Vector2(140f, 14f));
            handle.anchorMin = new Vector2(0.5f, 1f);
            handle.anchorMax = new Vector2(0.5f, 1f);
            handle.anchoredPosition = new Vector2(0f, -36f);
            PopKit.Well(handle, "bar", "panel.shade", 7f);

            var sheet = scrim.gameObject.AddComponent<Sheet>();
            sheet.Init(body, heightRefPx);
            return sheet;
        }

        /// <summary>§3.8 modal: confirm-only, one button pair max (caller adds them).</summary>
        public static RectTransform Modal(RectTransform parent, string name, string title, string bodyCopy)
        {
            RectTransform scrim = Rect(parent, name + "_scrim", Vector2.zero);
            Stretch(scrim);
            var scrimImage = scrim.gameObject.AddComponent<Image>();
            scrimImage.sprite = SpriteFactory.Solid();
            ThemedElement.Bind(scrim.gameObject, "scrim");

            RectTransform card = Card(scrim, name, new Vector2(860f, 560f));
            TextMeshProUGUI heading = UiText.Create(card, "title", title, "heading", "text.ink");
            Place(heading.rectTransform, new Vector2(0.5f, 0.82f), new Vector2(760f, 80f));
            TextMeshProUGUI copy = UiText.Create(card, "body", bodyCopy, "body", "text.inkSoft");
            Place(copy.rectTransform, new Vector2(0.5f, 0.58f), new Vector2(760f, 160f));
            return card;
        }

        // ---------------- §3.10–3.13, 16 widgets ----------------

        public static CoinCounter CoinCounterComponent(RectTransform parent)
        {
            RectTransform root = Rect(parent, "CoinCounter", new Vector2(300f, 72f));
            var counter = root.gameObject.AddComponent<CoinCounter>();
            counter.Build(root);
            return counter;
        }

        public static StarTriplet StarTripletComponent(RectTransform parent)
        {
            RectTransform root = Rect(parent, "StarTriplet", new Vector2(520f, 200f));
            var stars = root.gameObject.AddComponent<StarTriplet>();
            stars.Build(root);
            return stars;
        }

        public static ProgressPips ProgressPipsComponent(RectTransform parent, int count)
        {
            RectTransform root = Rect(parent, "ProgressPips", new Vector2(count * 40f, 40f));
            var pips = root.gameObject.AddComponent<ProgressPips>();
            pips.Build(root, count);
            return pips;
        }

        public static StreakFlame StreakFlameComponent(RectTransform parent)
        {
            RectTransform root = Rect(parent, "StreakFlame", new Vector2(220f, 72f));
            var flame = root.gameObject.AddComponent<StreakFlame>();
            flame.Build(root);
            return flame;
        }

        /// <summary>§3.16: 88px roundel; silhouette / normal / rescued-sparkle states.</summary>
        public static CreatureChip CreatureChipComponent(RectTransform parent, byte creatureId)
        {
            RectTransform root = Rect(parent, $"CreatureChip{creatureId}", new Vector2(88f, 88f));
            PadHitTarget(root);
            var chip = root.gameObject.AddComponent<CreatureChip>();
            chip.Build(root, creatureId);
            return chip;
        }

        // ---------------- shared plumbing ----------------

        internal static RectTransform Rect(RectTransform parent, string name, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            return rt;
        }

        internal static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        internal static void Place(RectTransform rt, Vector2 anchor, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
        }

        /// <summary>§3: min touch target 120 ref-px even when the visual is smaller.</summary>
        internal static void PadHitTarget(RectTransform rt)
        {
            float min = ThemeRuntime.Theme.MinTouchTargetRefPx;
            if (rt.sizeDelta.x < min || rt.sizeDelta.y < min)
            {
                var pad = new GameObject("hitPad", typeof(RectTransform));
                pad.transform.SetParent(rt, false);
                var padRt = (RectTransform)pad.transform;
                padRt.sizeDelta = new Vector2(Mathf.Max(min, rt.sizeDelta.x), Mathf.Max(min, rt.sizeDelta.y));
                var img = pad.AddComponent<Image>();
                img.color = new Color(0f, 0f, 0f, 0f);
                img.raycastTarget = true;
            }
        }
    }

    /// <summary>§3 pressed state for non-chunky tappables (cards, ghost links): scale 0.97, t.instant.</summary>
    public sealed class PressEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private Vector3 baseScale = Vector3.one;
        private bool down;

        private void Awake()
        {
            baseScale = transform.localScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            down = true;
            transform.localScale = baseScale * 0.97f;
        }

        public void OnPointerUp(PointerEventData eventData) => Release();

        public void OnPointerExit(PointerEventData eventData) => Release();

        private void Release()
        {
            if (down)
            {
                down = false;
                transform.localScale = baseScale;
            }
        }
    }
}
