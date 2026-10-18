using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>Tidal Pop tones — each names a (top, base, edge) token triple in ui_theme.json.</summary>
    public enum PopTone
    {
        Green,
        Yellow,
        Coral,
        Purple,
        Blue,
        White,
        Navy,
        Locked,
    }

    /// <summary>
    /// Tidal Pop component kit (DECISIONS 2026-10-18, UI spec §13): the genre's
    /// chunky sticker surfaces — dark outline, 3D lip, top-lit gradient face, gloss
    /// band — built from theme tokens only. Every builder returns plain uGUI so the
    /// rest of the UI (tests, stack, raycasts) is unchanged.
    /// </summary>
    public static class PopKit
    {
        /// <summary>Outline thickness and 3D lip depth, ref-px.</summary>
        public const float Border = 10f;
        public const float Lip = 16f;

        public static string Top(PopTone tone) => $"pop.{Key(tone)}.top";
        public static string Base(PopTone tone) => $"pop.{Key(tone)}";
        public static string Edge(PopTone tone) => $"pop.{Key(tone)}.edge";

        /// <summary>Label fill token for a tone: ink on white, white sticker letters elsewhere.</summary>
        public static string LabelColor(PopTone tone) => tone == PopTone.White ? "text.ink" : "text.primary";

        private static string Key(PopTone tone) => tone switch
        {
            PopTone.Green => "green",
            PopTone.Yellow => "yellow",
            PopTone.Coral => "coral",
            PopTone.Purple => "purple",
            PopTone.Blue => "blue",
            PopTone.White => "white",
            PopTone.Navy => "navy",
            _ => "locked",
        };

        public static float Radius(string token) =>
            ThemeRuntime.Theme.Radius.TryGetValue(token, out int r) ? r : 24f;

        // ------------------------------------------------------------ surfaces

        /// <summary>
        /// Builds the chunky layers inside <paramref name="root"/>: a lip (edge color,
        /// peeking below), and a face holding outline → fill gradient → gloss. Content
        /// belongs in the returned surface's <see cref="PopSurface.Face"/>.
        /// </summary>
        public static PopSurface Surface(RectTransform root, PopTone tone, float radius,
            float border = Border, float lip = Lip, bool shine = true)
        {
            var surface = root.gameObject.AddComponent<PopSurface>();
            surface.Lip = lip;

            surface.BaseImage = Layer(root, "lip", radius);
            var baseRt = surface.BaseImage.rectTransform;
            baseRt.offsetMax = new Vector2(0f, -lip);

            var face = UiComponents.Rect(root, "face", Vector2.zero);
            UiComponents.Stretch(face);
            face.offsetMin = new Vector2(0f, lip);
            surface.Face = face;

            surface.EdgeImage = Layer(face, "edge", radius);
            surface.FillImage = Layer(face, "fill", Mathf.Max(4f, radius - border));
            Inset(surface.FillImage.rectTransform, border);
            surface.Gradient = surface.FillImage.gameObject.AddComponent<PopGradient>();

            if (shine)
            {
                surface.ShineImage = Layer(face, "shine", Mathf.Max(4f, radius - border - 6f));
                RectTransform s = surface.ShineImage.rectTransform;
                s.anchorMin = new Vector2(0f, 0.5f);
                s.anchorMax = new Vector2(1f, 1f);
                s.offsetMin = new Vector2(border + 10f, 0f);
                s.offsetMax = new Vector2(-(border + 10f), -(border + 5f));
                var gloss = surface.ShineImage.gameObject.AddComponent<PopGradient>();
                gloss.Set("shine", "shine", topAlpha: 1f, bottomAlpha: 0f);
            }

            surface.SetTone(tone);
            return surface;
        }

        /// <summary>
        /// Light card (white face, navy outline, inner shade lip) — the panel of the
        /// style. Returns the ROOT: callers place it and parent content to it.
        /// </summary>
        public static RectTransform Card(RectTransform parent, string name, Vector2 size, float? radius = null)
        {
            RectTransform root = UiComponents.Rect(parent, name, size);
            float r = radius ?? Radius("r.l");
            var surface = root.gameObject.AddComponent<PopSurface>();
            surface.Lip = Lip;

            surface.BaseImage = Layer(root, "lip", r);
            surface.BaseImage.rectTransform.offsetMax = new Vector2(0f, -Lip);
            ThemedElement.Bind(surface.BaseImage.gameObject, "panel.edge");

            RectTransform face = UiComponents.Rect(root, "face", Vector2.zero);
            UiComponents.Stretch(face);
            face.offsetMin = new Vector2(0f, Lip);
            surface.Face = face;

            surface.EdgeImage = Layer(face, "edge", r);
            ThemedElement.Bind(surface.EdgeImage.gameObject, "panel.edge");
            Image shade = Layer(face, "shade", Mathf.Max(4f, r - Border));
            Inset(shade.rectTransform, Border);
            ThemedElement.Bind(shade.gameObject, "panel.shade");
            surface.FillImage = Layer(face, "fill", Mathf.Max(4f, r - Border));
            Inset(surface.FillImage.rectTransform, Border);
            surface.FillImage.rectTransform.offsetMin += new Vector2(0f, 14f);
            ThemedElement.Bind(surface.FillImage.gameObject, "panel.light");
            return root;
        }

        /// <summary>Flat rounded panel in a tone (no lip) — pills, wells, tab bars.</summary>
        public static RectTransform Panel(RectTransform parent, string name, Vector2 size, PopTone tone,
            float radius, float border = 6f)
        {
            RectTransform root = UiComponents.Rect(parent, name, size);
            Surface(root, tone, radius, border, lip: 0f, shine: false);
            return root;
        }

        /// <summary>A flat rounded fill in one color token.</summary>
        public static Image Well(RectTransform parent, string name, string colorToken, float radius)
        {
            Image image = Layer(parent, name, radius);
            ThemedElement.Bind(image.gameObject, colorToken);
            return image;
        }

        // ------------------------------------------------------------- buttons

        /// <summary>Chunky pressable button: sticker label (and optional leading icon).</summary>
        public static Button Button(RectTransform parent, string name, string label, PopTone tone,
            Action onClick, Vector2 size, string typeToken = "heading", string? iconId = null)
        {
            RectTransform root = UiComponents.Rect(parent, name, size);
            Button button = Pressable(root, tone, Mathf.Min(Radius("r.m"), size.y * 0.42f), onClick);
            RectTransform face = root.GetComponent<PopSurface>().Face;

            TextMeshProUGUI text = Label(face, "label", label, typeToken, tone);
            UiComponents.Stretch(text.rectTransform);
            if (iconId != null)
            {
                float iconSize = Mathf.Min(size.y * 0.42f, 72f);
                Image icon = Icon(face, "icon", iconId, LabelColor(tone), iconSize,
                    tone == PopTone.White ? null : Edge(tone));
                var iconRt = (RectTransform)icon.transform.parent;
                iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 0.5f);
                iconRt.anchoredPosition = new Vector2(Border + 26f + iconSize * 0.5f, 0f);
                text.rectTransform.offsetMin = new Vector2(Border + 30f + iconSize, 0f);
                text.rectTransform.offsetMax = new Vector2(-(Border + 18f), 0f);
            }

            return button;
        }

        /// <summary>Square/round chunky icon button.</summary>
        public static Button IconButton(RectTransform parent, string name, string iconId, PopTone tone,
            Action onClick, Vector2 size, bool round = false)
        {
            RectTransform root = UiComponents.Rect(parent, name, size);
            float radius = round ? size.x * 0.5f : Mathf.Min(Radius("r.m"), size.y * 0.32f);
            Button button = Pressable(root, tone, radius, onClick);
            RectTransform face = root.GetComponent<PopSurface>().Face;
            Icon(face, "icon", iconId, LabelColor(tone), (size.y - Lip) * 0.52f,
                tone == PopTone.White ? null : Edge(tone));
            UiComponents.PadHitTarget(root);
            return button;
        }

        /// <summary>A pressable chunky tile with no label — callers fill its Face.</summary>
        public static Button Tile(RectTransform parent, string name, PopTone tone, Action onClick, Vector2 size,
            float? radius = null)
        {
            RectTransform root = UiComponents.Rect(parent, name, size);
            return Pressable(root, tone, radius ?? Radius("r.l"), onClick);
        }

        /// <summary>The face (content parent) of a chunky element built by this kit.</summary>
        public static RectTransform FaceOf(Component element) => element.GetComponent<PopSurface>().Face;

        /// <summary>
        /// Navy balance pill: stickered coin + count, and (optionally) a green plus
        /// that opens the shop. Returns the counter to drive.
        /// </summary>
        public static CoinCounter CoinPill(RectTransform parent, Action? onPlus)
        {
            RectTransform pill = Panel(parent, "coinPill", new Vector2(330f, 104f), PopTone.Navy, 52f, 8f);
            CoinCounter counter = UiComponents.CoinCounterComponent(pill);
            var counterRt = (RectTransform)counter.transform;
            counterRt.anchorMin = counterRt.anchorMax = new Vector2(0f, 0.5f);
            counterRt.anchoredPosition = new Vector2(150f, 0f);
            counterRt.sizeDelta = new Vector2(260f, 72f);
            if (onPlus != null)
            {
                Button plus = IconButton(pill, "plus", "plus", PopTone.Green, onPlus, new Vector2(80f, 84f));
                var plusRt = (RectTransform)plus.transform;
                plusRt.anchorMin = plusRt.anchorMax = new Vector2(1f, 0.5f);
                plusRt.anchoredPosition = new Vector2(-46f, 4f);
            }

            return counter;
        }

        private static Button Pressable(RectTransform root, PopTone tone, float radius, Action onClick)
        {
            // Transparent full-rect hit target (raycasts land on the root Button).
            var hit = root.gameObject.AddComponent<Image>();
            hit.sprite = SpriteFactory.Solid();
            hit.color = new Color(0f, 0f, 0f, 0f);

            PopSurface surface = Surface(root, tone, radius);
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClick());
            root.gameObject.AddComponent<PopPress>().Init(surface);
            return button;
        }

        // --------------------------------------------------------------- bits

        /// <summary>Sticker text for a tone (outlined white, or plain ink on white).</summary>
        public static TextMeshProUGUI Label(RectTransform parent, string name, string text, string typeToken,
            PopTone tone)
        {
            return tone == PopTone.White
                ? UiText.Create(parent, name, text, typeToken, "text.ink")
                : UiText.CreateOutlined(parent, name, text, typeToken, "text.primary", Edge(tone));
        }

        /// <summary>
        /// Icon image (centered in a holder of <paramref name="size"/>), optionally
        /// backed by its dilated outline in <paramref name="outlineToken"/>. Returns the
        /// fill image; its parent is the holder to position.
        /// </summary>
        public static Image Icon(RectTransform parent, string name, string iconId, string colorToken, float size,
            string? outlineToken = null, int outlinePx = 9)
        {
            RectTransform holder = UiComponents.Rect(parent, name, new Vector2(size, size));
            UiComponents.Place(holder, new Vector2(0.5f, 0.5f), new Vector2(size, size));
            if (outlineToken != null)
            {
                float scale = PopSprites.OutlineScale(outlinePx);
                Image back = Img(holder, "outline", PopSprites.IconOutline(iconId, outlinePx), outlineToken);
                UiComponents.Place(back.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(size * scale, size * scale));
            }

            Image fill = Img(holder, "fill", PopSprites.Icon(iconId), colorToken);
            UiComponents.Stretch(fill.rectTransform);
            return fill;
        }

        public static Image Img(RectTransform parent, string name, Sprite sprite, string colorToken)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            ThemedElement.Bind(go, colorToken);
            return image;
        }

        /// <summary>Banner ribbon: tone face with folded tails tucked behind each end.</summary>
        public static RectTransform Ribbon(RectTransform parent, string name, string text, PopTone tone,
            Vector2 size, string typeToken = "heading")
        {
            RectTransform root = UiComponents.Rect(parent, name, size);
            foreach (float side in new[] { -1f, 1f })
            {
                RectTransform tail = UiComponents.Rect(root, side < 0 ? "tailL" : "tailR",
                    new Vector2(size.y * 0.8f, size.y * 0.62f));
                tail.anchorMin = tail.anchorMax = new Vector2(side < 0 ? 0f : 1f, 0.5f);
                tail.anchoredPosition = new Vector2(side * size.y * 0.12f, -size.y * 0.26f);
                PopSurface tailSurface = Surface(tail, tone, 10f, Border, lip: 0f, shine: false);
                tailSurface.Gradient.Set(Edge(tone), Edge(tone));
            }

            RectTransform band = UiComponents.Rect(root, "band", size);
            UiComponents.Stretch(band);
            PopSurface surface = Surface(band, tone, Mathf.Min(24f, size.y * 0.3f), Border, lip: 8f);
            TextMeshProUGUI label = Label(surface.Face, "label", text, typeToken, tone);
            UiComponents.Stretch(label.rectTransform);
            return root;
        }

        internal static Image Layer(RectTransform parent, string name, float radius)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            UiComponents.Stretch((RectTransform)go.transform);
            var image = go.AddComponent<Image>();
            image.sprite = PopSprites.Rounded();
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = PopSprites.CornerMultiplier(radius);
            image.raycastTarget = false;
            return image;
        }

        internal static void Inset(RectTransform rt, float px)
        {
            rt.offsetMin = new Vector2(px, px);
            rt.offsetMax = new Vector2(-px, -px);
        }
    }

    /// <summary>The layers of one chunky surface; re-tone it in place (selection states, locks).</summary>
    public sealed class PopSurface : MonoBehaviour
    {
        public RectTransform Face = null!;
        public Image BaseImage = null!;
        public Image EdgeImage = null!;
        public Image FillImage = null!;
        public Image? ShineImage;
        public PopGradient Gradient = null!;
        public float Lip;

        public PopTone Tone { get; private set; }

        public void SetTone(PopTone tone)
        {
            Tone = tone;
            ThemedElement.Bind(BaseImage.gameObject, PopKit.Edge(tone));
            ThemedElement.Bind(EdgeImage.gameObject, PopKit.Edge(tone));
            if (Gradient != null)
            {
                Gradient.Set(PopKit.Top(tone), PopKit.Base(tone));
            }
        }
    }

    /// <summary>
    /// Vertical two-token gradient as vertex colors (multiplies the graphic color):
    /// the top-lit candy face of every Tidal Pop surface, theme-sourced.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PopGradient : BaseMeshEffect
    {
        public string TopKey = "";
        public string BottomKey = "";
        public float TopAlpha = 1f;
        public float BottomAlpha = 1f;

        public void Set(string top, string bottom, float topAlpha = 1f, float bottomAlpha = 1f)
        {
            TopKey = top;
            BottomKey = bottom;
            TopAlpha = topAlpha;
            BottomAlpha = bottomAlpha;
            if (graphic != null)
            {
                graphic.color = Color.white;
                graphic.SetVerticesDirty();
            }
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0 || TopKey.Length == 0)
            {
                return;
            }

            Color top = ThemeRuntime.Color(TopKey);
            top.a *= TopAlpha;
            Color bottom = ThemeRuntime.Color(BottomKey);
            bottom.a *= BottomAlpha;
            Rect rect = graphic.rectTransform.rect;
            UIVertex v = default;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                float t = rect.height > 0f ? Mathf.InverseLerp(rect.yMin, rect.yMax, v.position.y) : 1f;
                v.color = Color.Lerp(bottom, top, t) * (Color)v.color;
                vh.SetUIVertex(v, i);
            }
        }
    }

    /// <summary>
    /// Press feel for chunky buttons: the face sinks onto its lip (the genre's
    /// physical click) at t.instant; a disabled button fades to half alpha.
    /// Reads the Selectable's OWN flag so screen-transition CanvasGroups never
    /// flash buttons as disabled.
    /// </summary>
    public sealed class PopPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private PopSurface surface = null!;
        private Selectable? selectable;
        private CanvasGroup? group;
        private bool down;
        private bool shownInteractable = true;

        internal void Init(PopSurface target)
        {
            surface = target;
            selectable = GetComponent<Selectable>();
        }

        private bool Interactable => selectable == null || selectable.interactable;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!Interactable)
            {
                return;
            }

            down = true;
            surface.Face.anchoredPosition = new Vector2(0f, -surface.Lip * 0.8f);
            transform.localScale = Vector3.one * 0.98f;
        }

        public void OnPointerUp(PointerEventData eventData) => Release();

        public void OnPointerExit(PointerEventData eventData) => Release();

        private void Release()
        {
            if (!down)
            {
                return;
            }

            down = false;
            surface.Face.anchoredPosition = Vector2.zero;
            transform.localScale = Vector3.one;
        }

        private void OnDisable()
        {
            Release();
        }

        private void LateUpdate()
        {
            bool interactable = Interactable;
            if (interactable == shownInteractable)
            {
                return;
            }

            shownInteractable = interactable;
            if (group == null)
            {
                group = GetComponent<CanvasGroup>();
                if (group == null)
                {
                    group = gameObject.AddComponent<CanvasGroup>();
                }
            }

            group.alpha = interactable ? 1f : 0.5f;
        }
    }
}
