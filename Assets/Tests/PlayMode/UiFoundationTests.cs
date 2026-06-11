using System.Collections;
using NUnit.Framework;
using Riptide.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Riptide.PlayMode.Tests
{
    /// <summary>UI spec 4-UI-a ✅ (runtime half): theme application, tween timing/cancel, screen stack.</summary>
    public sealed class UiFoundationTests
    {
        [Test]
        public void ThemedElement_AppliesTokenColors_AndRejectsTypos()
        {
            var go = new GameObject("themed", typeof(RectTransform));
            var image = go.AddComponent<Image>();
            ThemedElement.Bind(go, "accent.primary");

            Color expected = ThemeRuntime.Color("accent.primary");
            Assert.That(image.color.r, Is.EqualTo(expected.r).Within(0.001f));
            Assert.That(image.color.g, Is.EqualTo(expected.g).Within(0.001f));

            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(
                () => ThemedElement.Bind(go, "accent.typo"));
            Object.Destroy(go);
        }

        [Test]
        public void Theme_LoadsTheRealFile_WithLockedDurations()
        {
            Assert.That(ThemeRuntime.Theme.Duration("t.rise"), Is.EqualTo(350), "GDD-locked");
            Assert.That(ThemeRuntime.Theme.Duration("t.drain"), Is.EqualTo(450), "GDD-locked");
            Assert.That(ThemeRuntime.Theme.MinTouchTargetRefPx, Is.EqualTo(120), "spec §3");
        }

        [UnityTest]
        public IEnumerator Tween_CompletesNearItsDuration_AndAppliesMonotonically()
        {
            var host = new GameObject("tweenHost").AddComponent<TweenProbe>();
            float duration = ThemeRuntime.Seconds("t.fast");
            bool done = false;
            float last = -1f;
            bool monotonic = true;
            float started = Time.realtimeSinceStartup;
            Tween.Run(host, "t.fast", "easeOutQuart", u =>
            {
                if (u < last - 0.0001f)
                {
                    monotonic = false;
                }

                last = u;
            }, () => done = true);

            float deadline = Time.realtimeSinceStartup + duration + 2f;
            while (!done && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            float elapsed = Time.realtimeSinceStartup - started;
            Assert.That(done, Is.True, "tween completes");
            Assert.That(last, Is.EqualTo(1f).Within(0.001f), "ends exactly at 1");
            Assert.That(monotonic, Is.True, "easeOutQuart never reverses");
            Assert.That(elapsed, Is.GreaterThanOrEqualTo(duration * 0.5f), "duration respected (frame-quantized)");
            Object.Destroy(host.gameObject);
        }

        [UnityTest]
        public IEnumerator Tween_Cancel_StopsApplication_AndSkipsOnComplete()
        {
            var host = new GameObject("tweenHost2").AddComponent<TweenProbe>();
            int applications = 0;
            bool completed = false;
            TweenHandle handle = Tween.Run(host, "t.screen", "linear", _ => applications++, () => completed = true);
            yield return null;
            handle.Cancel();
            int frozen = applications;
            yield return null;
            yield return null;

            Assert.That(applications, Is.EqualTo(frozen), "no application after cancel");
            Assert.That(completed, Is.False, "cancelled tweens never complete");
            Object.Destroy(host.gameObject);
        }

        [UnityTest]
        public IEnumerator ScreenStack_PushPop_TracksTopAndRestoresPrevious()
        {
            var canvasGo = new GameObject("canvas", typeof(RectTransform));
            ScreenStack stack = ScreenStack.Create(canvasGo.transform);

            RectTransform a = NewScreen(canvasGo.transform, "A");
            RectTransform b = NewScreen(canvasGo.transform, "B");

            stack.Push("home", a);
            Assert.That(stack.TopId, Is.EqualTo("home"));
            Assert.That(stack.Pop(), Is.False, "root never pops (Android back backgrounds the app)");

            stack.Push("settings", b);
            Assert.That(stack.TopId, Is.EqualTo("settings"));
            Assert.That(stack.Depth, Is.EqualTo(2));

            // Let the transition play out, then pop.
            float wait = ThemeRuntime.Seconds("t.screen") + 0.2f;
            float until = Time.realtimeSinceStartup + wait;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Assert.That(stack.Pop(), Is.True);
            Assert.That(stack.TopId, Is.EqualTo("home"));
            until = Time.realtimeSinceStartup + wait;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Assert.That(a.gameObject.activeSelf, Is.True, "previous screen restored");
            Assert.That(b.gameObject.activeSelf, Is.False, "popped screen hidden");
            Object.Destroy(canvasGo);
        }

        private static RectTransform NewScreen(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.SetActive(false);
            return (RectTransform)go.transform;
        }

        private sealed class TweenProbe : MonoBehaviour
        {
        }
    }
}
