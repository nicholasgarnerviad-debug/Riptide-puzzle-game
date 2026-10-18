using System.Collections.Generic;
using Riptide.Core;
using TMPro;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// Spec §1.2 typography through TextMeshPro. Font resolution chain:
    /// dropped game font (per weight) → TMP default (package-bundled SDF) →
    /// runtime SDF generated from the builtin font. All sizing/tracking/caps/
    /// weight come from the type tokens — no literals.
    /// </summary>
    public static class UiText
    {
        private static TMP_FontAsset? cachedFont;
        private static Font[]? customFonts;
        private static readonly Dictionary<string, TMP_FontAsset> weighted = new Dictionary<string, TMP_FontAsset>();
        private static readonly Dictionary<long, Material> outlineMaterials = new Dictionary<long, Material>();

        /// <summary>
        /// One-drop typeface swap: every .ttf/.otf in <c>Assets/Resources/Fonts/</c>
        /// is picked up at runtime and a dynamic TMP SDF is generated per file (no
        /// editor SDF step). Files named <c>Family-Weight.ttf</c> serve that weight
        /// token (Bold / SemiBold / Medium); a single dropped file serves them all.
        /// Returns null until a font is dropped, so the placeholder keeps rendering.
        /// </summary>
        public static Font? CustomFont => CustomFontFor("Bold");

        private static Font[] CustomFonts => customFonts ??= Resources.LoadAll<Font>("Fonts");

        /// <summary>The dropped file whose name ends in "-{weight}", else the nearest heavier/any.</summary>
        public static Font? CustomFontFor(string weight)
        {
            Font[] fonts = CustomFonts;
            if (fonts.Length == 0)
            {
                return null;
            }

            foreach (string candidate in WeightFallbacks(weight))
            {
                foreach (Font font in fonts)
                {
                    int dash = font.name.LastIndexOf('-');
                    string suffix = dash >= 0 ? font.name.Substring(dash + 1) : font.name;
                    if (string.Equals(suffix, candidate, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return font;
                    }
                }
            }

            return fonts[0];
        }

        private static IEnumerable<string> WeightFallbacks(string weight)
        {
            yield return weight;
            switch (weight)
            {
                case "Medium":
                    yield return "SemiBold";
                    yield return "Regular";
                    break;
                case "SemiBold":
                    yield return "Bold";
                    yield return "Medium";
                    break;
                case "Bold":
                    yield return "SemiBold";
                    break;
            }
        }

        public static TMP_FontAsset DefaultFont => FontFor("Bold");

        /// <summary>TMP asset for a §1.2 weight token, cached per weight.</summary>
        public static TMP_FontAsset FontFor(string weight)
        {
            if (weighted.TryGetValue(weight, out TMP_FontAsset? asset) && asset != null)
            {
                return asset;
            }

            Font? custom = CustomFontFor(weight);
            if (custom == null)
            {
                asset = PlaceholderFont();
            }
            else
            {
                // Same file for two weights → share one atlas (one material/batch).
                foreach (KeyValuePair<string, TMP_FontAsset> pair in weighted)
                {
                    if (pair.Value != null && pair.Value.sourceFontFile == custom)
                    {
                        weighted[weight] = pair.Value;
                        return pair.Value;
                    }
                }

                asset = TMP_FontAsset.CreateFontAsset(custom);
                asset.name = custom.name + " SDF";
                // Glyphs the game font lacks (e.g. symbols) fall back to the
                // bundled placeholder instead of rendering tofu boxes.
                TMP_FontAsset placeholder = PlaceholderFont();
                if (placeholder != asset)
                {
                    asset.fallbackFontAssetTable = new List<TMP_FontAsset> { placeholder };
                }
            }

            weighted[weight] = asset;
            return asset;
        }

        private static TMP_FontAsset PlaceholderFont()
        {
            if (cachedFont != null)
            {
                return cachedFont;
            }

            // TMP_Settings.defaultFontAsset dereferences instance without a null
            // check; on a project without the TMP essentials asset it throws.
            TMP_FontAsset? fromSettings =
                TMP_Settings.instance != null ? TMP_Settings.defaultFontAsset : null;
            if (fromSettings != null)
            {
                cachedFont = fromSettings;
                return cachedFont;
            }

            var builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            cachedFont = TMP_FontAsset.CreateFontAsset(builtin);
            return cachedFont;
        }

        public static TextMeshProUGUI Create(RectTransform parent, string name, string content,
            string typeToken, string colorToken, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            Apply(text, typeToken);
            text.alignment = alignment;
            text.text = content;
            text.raycastTarget = false;
            ThemedElement.Bind(go, colorToken);
            return text;
        }

        /// <summary>
        /// Tidal Pop display text: a §1.2 token with a chunky outline in an edge
        /// token (the genre's sticker-letter look). Outline materials are shared
        /// per (font, color, width) so outlined labels still batch.
        /// </summary>
        public static TextMeshProUGUI CreateOutlined(RectTransform parent, string name, string content,
            string typeToken, string colorToken, string outlineToken, float outlineWidth = 0.22f,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            TextMeshProUGUI text = Create(parent, name, content, typeToken, colorToken, alignment);
            Outline(text, outlineToken, outlineWidth);
            return text;
        }

        /// <summary>Applies (or re-colors) a shared outline material on an existing label.</summary>
        public static void Outline(TMP_Text text, string outlineToken, float width = 0.22f)
        {
            Color color = ThemeRuntime.Color(outlineToken);
            Color32 c32 = color;
            int widthKey = Mathf.RoundToInt(width * 100f);
            long key = ((long)text.font.GetInstanceID() << 32)
                ^ ((long)c32.r << 24 | (long)c32.g << 16 | (long)c32.b << 8 | (long)c32.a)
                ^ ((long)widthKey << 40);
            if (!outlineMaterials.TryGetValue(key, out Material? material) || material == null)
            {
                material = new Material(text.font.material) { name = $"{text.font.name} outline {outlineToken}" };
                material.SetColor(ShaderUtilities.ID_OutlineColor, color);
                material.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
                // Dilate the face so the outline grows outward, keeping counters open.
                material.SetFloat(ShaderUtilities.ID_FaceDilate, width * 0.5f);
                material.EnableKeyword("OUTLINE_ON");
                outlineMaterials[key] = material;
            }

            text.fontSharedMaterial = material;
        }

        /// <summary>
        /// World-space TMP for the board chrome (depth gauge numerals, ring numeral,
        /// flood marker). Type token sizes are ref-px; the world scale converts them
        /// so a token reads the same size in HUD and on the board.
        /// </summary>
        public static TextMeshPro CreateWorld(Transform parent, string name, string content,
            string typeToken, string colorToken, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshPro>();
            TypeStyle style = ThemeRuntime.Theme.TypeStyle(typeToken);
            text.font = FontFor(style.Weight);
            text.fontSize = style.Size;
            text.characterSpacing = style.Tracking * 100f;
            text.fontStyle = style.AllCaps ? FontStyles.UpperCase : FontStyles.Normal;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.text = content;
            var c = ThemeRuntime.Color(colorToken);
            text.color = c;
            // World-space TMP renders fontSize/10 world units per pt at scale 1,
            // so 10× the ref-px→world factor makes one pt read as one ref-px.
            float scale = 10f * ThemeRuntime.WorldFromRefPx(1f);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sortingOrder = sortingOrder;
            return text;
        }

        /// <summary>Applies a §1.2 type token: font weight, size, line, tracking, caps.</summary>
        public static void Apply(TextMeshProUGUI text, string typeToken)
        {
            TypeStyle style = ThemeRuntime.Theme.TypeStyle(typeToken);
            text.font = FontFor(style.Weight);
            text.fontSize = style.Size;
            text.lineSpacing = (style.Line - style.Size) * 1.2f;
            text.characterSpacing = style.Tracking * 100f;
            text.fontStyle = style.AllCaps ? FontStyles.UpperCase : FontStyles.Normal;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
        }
    }
}
