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

        private void Awake() => Apply();

        private void Update()
        {
            if (Screen.safeArea != applied)
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
            var min = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            var max = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
