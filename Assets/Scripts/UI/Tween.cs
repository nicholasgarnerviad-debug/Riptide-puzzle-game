using System;
using System.Collections;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// Spec §2: the single in-house tween utility — theme-curve driven, no third
    /// party, cancellable and idempotent. Durations honor reduced motion.
    /// </summary>
    public sealed class TweenHandle
    {
        internal bool Cancelled;
        public bool Completed { get; internal set; }

        /// <summary>Idempotent; a cancelled tween never fires onComplete.</summary>
        public void Cancel() => Cancelled = true;
    }

    public static class Tween
    {
        /// <summary>Animates 0→1 over a theme duration with a theme easing.</summary>
        public static TweenHandle Run(MonoBehaviour host, string durationKey, string easingKey,
            Action<float> apply, Action? onComplete = null)
        {
            var handle = new TweenHandle();
            host.StartCoroutine(RunRoutine(handle, ThemeRuntime.MotionSeconds(durationKey),
                ThemeRuntime.Curve(easingKey), apply, onComplete));
            return handle;
        }

        /// <summary>Raw-seconds variant for gameplay-locked timings already resolved elsewhere.</summary>
        public static TweenHandle RunSeconds(MonoBehaviour host, float seconds, AnimationCurve curve,
            Action<float> apply, Action? onComplete = null)
        {
            var handle = new TweenHandle();
            host.StartCoroutine(RunRoutine(handle, seconds, curve, apply, onComplete));
            return handle;
        }

        private static IEnumerator RunRoutine(TweenHandle handle, float seconds, AnimationCurve curve,
            Action<float> apply, Action? onComplete)
        {
            if (seconds <= 0f)
            {
                apply(1f);
                handle.Completed = true;
                onComplete?.Invoke();
                yield break;
            }

            float t = 0f;
            while (t < seconds)
            {
                if (handle.Cancelled)
                {
                    yield break;
                }

                t += Time.deltaTime;
                apply(curve.Evaluate(Mathf.Clamp01(t / seconds)));
                yield return null;
            }

            apply(curve.Evaluate(1f));
            handle.Completed = true;
            onComplete?.Invoke();
        }
    }

    /// <summary>
    /// Spec §2: pads a screen root to the device safe area; the board never
    /// enters notch territory, the HUD pins inside it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SafeArea : MonoBehaviour
    {
        private Rect applied;
        private int appliedWidth;
        private int appliedHeight;

        private void Awake() => Apply();

        private void Update()
        {
            // Gate feedback (Device Simulator blank screen): the cache key must
            // include the SCREEN size — switching Game view ↔ Simulator can change
            // Screen.width/height while safeArea compares equal, freezing anchors
            // computed against the wrong basis.
            if (Screen.safeArea != applied
                || Screen.width != appliedWidth || Screen.height != appliedHeight)
            {
                Apply();
            }
        }

        public void Apply()
        {
            var rt = GetComponent<RectTransform>();
            if (rt == null)
            {
                return;
            }

            Rect safe = Screen.safeArea;
            applied = safe;
            appliedWidth = Screen.width;
            appliedHeight = Screen.height;
            (Vector2 min, Vector2 max) = Anchors(safe, appliedWidth, appliedHeight);
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>Pure anchor math — degenerate or mismatched-basis reports
        /// full-stretch instead of collapsing the screen. Public test surface.</summary>
        public static (Vector2 min, Vector2 max) Anchors(Rect safe, int width, int height)
        {
            if (safe.width <= 1f || safe.height <= 1f || width <= 1 || height <= 1)
            {
                return (Vector2.zero, Vector2.one);
            }

            var min = new Vector2(
                Mathf.Clamp01(safe.xMin / width), Mathf.Clamp01(safe.yMin / height));
            var max = new Vector2(
                Mathf.Clamp01(safe.xMax / width), Mathf.Clamp01(safe.yMax / height));
            if (max.x - min.x < 0.5f || max.y - min.y < 0.5f)
            {
                // A "safe area" under half the screen is not a notch — it's a
                // wrong-basis artifact; full-stretch beats an invisible screen.
                return (Vector2.zero, Vector2.one);
            }

            return (min, max);
        }
    }
}
