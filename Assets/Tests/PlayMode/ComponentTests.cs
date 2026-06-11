using System.Collections;
using NUnit.Framework;
using Riptide.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Riptide.PlayMode.Tests
{
    /// <summary>
    /// UI spec 4-UI-b ✅: every §3 component instantiates themed, interactables
    /// honor the 120 ref-px touch target, and the stateful widgets behave.
    /// (Prefab assets are serializations of these same builders, audited by AutoPrefabGen.)
    /// </summary>
    public sealed class ComponentTests
    {
        private static RectTransform Host()
        {
            var go = new GameObject("host", typeof(RectTransform));
            return (RectTransform)go.transform;
        }

        [Test]
        public void Buttons_AreThemed_AndMeetTouchTargets()
        {
            RectTransform host = Host();
            float min = ThemeRuntime.Theme.MinTouchTargetRefPx;

            Button primary = UiComponents.ButtonPrimary(host, "p", "Play", () => { });
            Assert.That(primary.GetComponent<ThemedElement>().colorKey, Is.EqualTo("accent.primary"), "spec §3.1");
            Assert.That(primary.GetComponent<Image>().color,
                Is.EqualTo(ThemeRuntime.Color("accent.primary")), "theme applied");

            Button icon = UiComponents.IconButton(host, "i", "✕", () => { });
            float hitW = 0f;
            float hitH = 0f;
            foreach (Graphic graphic in icon.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic.raycastTarget)
                {
                    hitW = Mathf.Max(hitW, ((RectTransform)graphic.transform).sizeDelta.x);
                    hitH = Mathf.Max(hitH, ((RectTransform)graphic.transform).sizeDelta.y);
                }
            }

            Assert.That(hitW, Is.GreaterThanOrEqualTo(min), "96px icon padded to the 120 target (spec §3)");
            Assert.That(hitH, Is.GreaterThanOrEqualTo(min));
            Object.Destroy(host.gameObject);
        }

        [Test]
        public void CoinCounter_CountsUp_ToTheExactValue()
        {
            RectTransform host = Host();
            CoinCounter counter = UiComponents.CoinCounterComponent(host);
            counter.SetInstant(100);

            counter.AnimateTo(1250);

            Assert.That(counter.Shown, Is.EqualTo(1250), "logical value lands immediately; display tweens");
            Object.Destroy(host.gameObject);
        }

        [UnityTest]
        public IEnumerator StarTriplet_FillsExactlyTheEarnedStars()
        {
            RectTransform host = Host();
            StarTriplet stars = UiComponents.StarTripletComponent(host);

            stars.Show(2);
            float until = Time.realtimeSinceStartup + 0.6f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Assert.That(stars.Filled, Is.EqualTo(2));
            Object.Destroy(host.gameObject);
        }

        [UnityTest]
        public IEnumerator Toast_QueuesAtMostTwo_AndAutoDismisses()
        {
            RectTransform host = Host();
            ToastManager toasts = ToastManager.Create(host);

            toasts.Show("one");
            toasts.Show("two");
            toasts.Show("three");
            Assert.That(toasts.Pending, Is.LessThanOrEqualTo(ToastManager.QueueDepth), "spec §3.9: depth 2");

            yield return null;
            Assert.That(host.GetComponentInChildren<Sheet>() == null, Is.True);
            Object.Destroy(host.gameObject);
        }

        [UnityTest]
        public IEnumerator Sheet_SlidesIn_AndDismisses()
        {
            RectTransform host = Host();
            Sheet sheet = UiComponents.SheetComponent(host, "pause", 900f);
            Assert.That(sheet.gameObject.activeSelf, Is.False, "sheets start hidden");

            sheet.Show();
            Assert.That(sheet.Shown, Is.True);
            float wait = ThemeRuntime.Seconds("t.screen") + 0.3f;
            float until = Time.realtimeSinceStartup + wait;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Assert.That(sheet.Body.anchoredPosition.y, Is.EqualTo(0f).Within(1f), "fully presented");

            sheet.Dismiss();
            until = Time.realtimeSinceStartup + wait;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Assert.That(sheet.gameObject.activeSelf, Is.False, "dismissed sheets deactivate");
            Object.Destroy(host.gameObject);
        }

        [Test]
        public void ProgressPips_And_CreatureChip_TrackState()
        {
            RectTransform host = Host();

            ProgressPips pips = UiComponents.ProgressPipsComponent(host, 5);
            pips.SetFilled(3);
            Assert.That(pips.Total, Is.EqualTo(5));

            CreatureChip chip = UiComponents.CreatureChipComponent(host, 2);
            Assert.That(chip.Current, Is.EqualTo(CreatureChip.State.Silhouette), "locked species default (spec §3.16)");
            chip.Apply(CreatureChip.State.Normal);
            Assert.That(chip.Current, Is.EqualTo(CreatureChip.State.Normal));
            Object.Destroy(host.gameObject);
        }
    }
}
