using System;
using Riptide.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// Spec §3 component library, 1–13 + 16 — theme-driven builders. Editor
    /// tooling serializes these into Assets/Prefabs (DECISIONS: generated, never
    /// hand-authored). Every interactable is padded to the 120 ref-px target.
    /// </summary>
    public static class UiComponents
    {
        // ---------------- §3.1–3.5 buttons ----------------

        public static Button ButtonPrimary(RectTransform parent, string name, string label, Action onClick) =>
            BuildButton(parent, name, label, onClick, "accent.primary", "text.onAccent", glow: true);

        public static Button ButtonSecondary(RectTransform parent, string name, string label, Action onClick) =>
            BuildButton(parent, name, label, onClick, "bg.raised", "text.primary", glow: false);

        public static Button ButtonGhost(RectTransform parent, string name, string label, Action onClick)
        {
            Button button = BuildButton(parent, name, label + "  ›", onClick, "bg.abyss", "text.secondary", glow: false);
            button.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // text-only
            return button;
        }

        /// <summary>§3.4: always shows what you get — caller passes the reward text.</summary>
        public static Button ButtonReward(RectTransform parent, string name, string rewardLabel, Action onClick)
        {
            Button button = BuildButton(parent, name, rewardLabel, onClick, "bg.raised", "text.primary", glow: false);
            var badge = new GameObject("adBadge", typeof(RectTransform));
            badge.transform.SetParent(button.transform, false);
            var badgeRt = (RectTransform)badge.transform;
            badgeRt.anchorMin = new Vector2(0f, 0.5f);
            badgeRt.anchorMax = new Vector2(0f, 0.5f);
            badgeRt.sizeDelta = new Vector2(56f, 56f);
            badgeRt.anchoredPosition = new Vector2(52f, 0f);
            var badgeImage = badge.AddComponent<Image>();
            badgeImage.sprite = SpriteFactory.Dot();
            ThemedElement.Bind(badge, "warning");
            UiText.Create(badgeRt, "play", "▶", "micro", "text.onAccent");
            return button;
        }

        public static Button IconButton(RectTransform parent, string name, string glyph, Action onClick)
        {
            RectTransform root = Rect(parent, name, new Vector2(96f, 96f));
            PadHitTarget(root);
            var image = root.gameObject.AddComponent<Image>();
            image.sprite = SpriteFactory.Cell();
            ThemedElement.Bind(root.gameObject, "bg.raised");
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick());
            root.gameObject.AddComponent<PressEffect>();
            UiText.Create(root, "glyph", glyph, "heading", "text.primary");
            return button;
        }

        private static Button BuildButton(RectTransform parent, string name, string label, Action onClick,
            string fillToken, string textToken, bool glow)
        {
            RectTransform root = Rect(parent, name, new Vector2(560f, 130f));
            PadHitTarget(root);
            var image = root.gameObject.AddComponent<Image>();
            image.sprite = SpriteFactory.Cell();
            ThemedElement.Bind(root.gameObject, fillToken);
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick());
            root.gameObject.AddComponent<PressEffect>();
            if (glow)
            {
                AddGlow(root);
            }

            TextMeshProUGUI text = UiText.Create(root, "label", label, "body", textToken);
            Stretch(text.rectTransform);
            return button;
        }

        // ---------------- §3.6–3.9 surfaces ----------------

        public static RectTransform Card(RectTransform parent, string name, Vector2 size)
        {
            RectTransform root = Rect(parent, name, size);
            var image = root.gameObject.AddComponent<Image>();
            image.sprite = SpriteFactory.Cell();
            ThemedElement.Bind(root.gameObject, "bg.surface");
            return root;
        }

        /// <summary>§3.7 bottom sheet with drag-handle bar and scrim; SlideIn() to present.</summary>
        public static Sheet SheetComponent(RectTransform parent, string name, float heightRefPx)
        {
            RectTransform scrim = Rect(parent, name + "_scrim", Vector2.zero);
            Stretch(scrim);
            var scrimImage = scrim.gameObject.AddComponent<Image>();
            scrimImage.sprite = SpriteFactory.Solid();
            ThemedElement.Bind(scrim.gameObject, "scrim");

            RectTransform body = Rect(scrim, name, new Vector2(0f, heightRefPx));
            body.anchorMin = new Vector2(0f, 0f);
            body.anchorMax = new Vector2(1f, 0f);
            body.pivot = new Vector2(0.5f, 0f);
            body.offsetMin = new Vector2(0f, 0f);
            body.offsetMax = new Vector2(0f, heightRefPx);
            var bodyImage = body.gameObject.AddComponent<Image>();
            bodyImage.sprite = SpriteFactory.Cell();
            ThemedElement.Bind(body.gameObject, "bg.surface");

            RectTransform handle = Rect(body, "handle", new Vector2(120f, 10f));
            handle.anchorMin = new Vector2(0.5f, 1f);
            handle.anchorMax = new Vector2(0.5f, 1f);
            handle.anchoredPosition = new Vector2(0f, -24f);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.sprite = SpriteFactory.Solid();
            ThemedElement.Bind(handle.gameObject, "stroke.bright");

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

            RectTransform card = Card(scrim, name, new Vector2(820f, 520f));
            TextMeshProUGUI heading = UiText.Create(card, "title", title, "heading", "text.primary");
            Place(heading.rectTransform, new Vector2(0.5f, 0.82f), new Vector2(720f, 80f));
            TextMeshProUGUI copy = UiText.Create(card, "body", bodyCopy, "body", "text.secondary");
            Place(copy.rectTransform, new Vector2(0.5f, 0.58f), new Vector2(720f, 160f));
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
            RectTransform root = Rect(parent, "StarTriplet", new Vector2(420f, 130f));
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

        private static void AddGlow(RectTransform root)
        {
            var glow = new GameObject("glow", typeof(RectTransform));
            glow.transform.SetParent(root, false);
            glow.transform.SetAsFirstSibling();
            var glowRt = (RectTransform)glow.transform;
            Stretch(glowRt);
            glowRt.offsetMin = new Vector2(-18f, -18f);
            glowRt.offsetMax = new Vector2(18f, 18f);
            var image = glow.AddComponent<Image>();
            image.sprite = SpriteFactory.Dot();
            image.raycastTarget = false;
            ThemedElement.Bind(glow, "glow.primary");
        }
    }

    /// <summary>§3 pressed state: scale 0.97 + brightness −8%, t.instant.</summary>
    public sealed class PressEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private Vector3 baseScale;
        private Graphic? graphic;
        private Color baseColor;

        private void Awake()
        {
            baseScale = transform.localScale;
            graphic = GetComponent<Graphic>();
            if (graphic != null)
            {
                baseColor = graphic.color;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            transform.localScale = baseScale * 0.97f;
            if (graphic != null)
            {
                baseColor = graphic.color;
                graphic.color = new Color(baseColor.r * 0.92f, baseColor.g * 0.92f, baseColor.b * 0.92f, baseColor.a);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            transform.localScale = baseScale;
            if (graphic != null)
            {
                graphic.color = baseColor;
            }
        }
    }
}
