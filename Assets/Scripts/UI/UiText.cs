using Riptide.Core;
using TMPro;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// Spec §1.2 typography through TextMeshPro. Font resolution chain:
    /// TMP default (package-bundled SDF) → runtime SDF generated from the builtin
    /// font. Rungo SDF assets slot in here when Nick supplies the TTFs (flagged).
    /// All sizing/tracking/caps come from the type tokens — no literals.
    /// </summary>
    public static class UiText
    {
        private static TMP_FontAsset? cachedFont;

        public static TMP_FontAsset DefaultFont
        {
            get
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
        }

        public static TextMeshProUGUI Create(RectTransform parent, string name, string content,
            string typeToken, string colorToken, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = DefaultFont;
            Apply(text, typeToken);
            text.alignment = alignment;
            text.text = content;
            ThemedElement.Bind(go, colorToken);
            return text;
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
            text.font = DefaultFont;
            TypeStyle style = ThemeRuntime.Theme.TypeStyle(typeToken);
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

        /// <summary>Applies a §1.2 type token: size, line, tracking, caps. Weight maps when Rungo lands.</summary>
        public static void Apply(TextMeshProUGUI text, string typeToken)
        {
            TypeStyle style = ThemeRuntime.Theme.TypeStyle(typeToken);
            text.fontSize = style.Size;
            text.lineSpacing = (style.Line - style.Size) * 1.2f;
            text.characterSpacing = style.Tracking * 100f;
            text.fontStyle = style.AllCaps ? FontStyles.UpperCase : FontStyles.Normal;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
        }
    }
}
