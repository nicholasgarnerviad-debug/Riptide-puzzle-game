using Riptide.Core;
using Riptide.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// Process-wide UiTheme access for the UI layer (spec §1: single source of
    /// truth; no literals in code). Tests may inject a theme before first use.
    /// </summary>
    public static class ThemeRuntime
    {
        private static UiTheme? theme;

        public static UiTheme Theme => theme ??= RuntimeContent.LoadTheme();

        public static void Inject(UiTheme custom) => theme = custom;

        public static Color Color(string key)
        {
            ThemeColor c = Theme.Color(key);
            return new Color(c.R, c.G, c.B, c.A);
        }

        public static float Seconds(string durationKey) => Theme.Duration(durationKey) / 1000f;

        /// <summary>Reduced-motion (spec §1.4): durations ×0.5, staggers off.</summary>
        public static bool ReducedMotion => PlayerPrefs.GetInt("settings.reducedMotion.on", 0) == 1;

        public static float MotionSeconds(string durationKey) =>
            Seconds(durationKey) * (ReducedMotion ? Theme.ReducedMotionScale : 1f);

        public static AnimationCurve Curve(string easingKey)
        {
            var keys = Theme.Easing(easingKey);
            var frames = new Keyframe[keys.Count];
            for (int i = 0; i < keys.Count; i++)
            {
                frames[i] = new Keyframe(keys[i].Time, keys[i].Value, keys[i].InTangent, keys[i].OutTangent);
            }

            return new AnimationCurve(frames);
        }

        /// <summary>Spec §2 reference canvas width the ref-px measurements assume.</summary>
        public const float ReferenceWidthPx = 1080f;

        /// <summary>
        /// Spec §4.3: board width = reference width minus the two gutters, spread
        /// across the 9 columns — this converts the spec's ref-px into world units
        /// for the world-space game views (1 world unit = 1 cell).
        /// </summary>
        public static float WorldFromRefPx(float refPx) =>
            refPx * BoardSpec.Width / (ReferenceWidthPx - 2f * Theme.Gutter);
    }

    /// <summary>
    /// Spec §1: binds a Graphic's color to a theme key. The literal-scan editor
    /// script flags any Graphic that carries a hand-set color without one of these.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThemedElement : MonoBehaviour
    {
        public string colorKey = "";

        private void Awake() => Apply();

        public void Apply()
        {
            if (string.IsNullOrEmpty(colorKey))
            {
                return;
            }

            var graphic = GetComponent<Graphic>();
            if (graphic != null)
            {
                graphic.color = ThemeRuntime.Color(colorKey);
            }
        }

        public static ThemedElement Bind(GameObject target, string key)
        {
            var element = target.GetComponent<ThemedElement>();
            if (element == null)
            {
                element = target.AddComponent<ThemedElement>();
            }

            element.colorKey = key;
            element.Apply();
            return element;
        }
    }
}
