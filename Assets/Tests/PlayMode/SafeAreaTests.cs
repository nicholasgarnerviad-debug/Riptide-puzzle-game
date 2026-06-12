using NUnit.Framework;
using Riptide.UI;
using UnityEngine;

namespace Riptide.PlayMode.Tests
{
    /// <summary>
    /// Gate-feedback regression (Device Simulator blank screen): SafeArea anchor
    /// math must never collapse a screen — degenerate or wrong-basis safe-area
    /// reports fall back to full stretch, and real notch insets stay fractional.
    /// </summary>
    public sealed class SafeAreaTests
    {
        [Test]
        public void Anchors_RealNotchInsets_AreFractional()
        {
            // iPhone 13 Pro Max simulator: 1284×2778, top notch 141px, bottom 102px.
            (Vector2 min, Vector2 max) = SafeArea.Anchors(
                new Rect(0f, 102f, 1284f, 2535f), 1284, 2778);

            Assert.That(min.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(min.y, Is.EqualTo(102f / 2778f).Within(0.001f));
            Assert.That(max.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(max.y, Is.EqualTo(2637f / 2778f).Within(0.001f));
        }

        [Test]
        public void Anchors_DegenerateRect_FullStretch()
        {
            Assert.That(SafeArea.Anchors(Rect.zero, 1080, 2340),
                Is.EqualTo((Vector2.zero, Vector2.one)), "zero safe rect");
            Assert.That(SafeArea.Anchors(new Rect(0f, 0f, 1080f, 2340f), 0, 0),
                Is.EqualTo((Vector2.zero, Vector2.one)), "zero screen");
        }

        [Test]
        public void Anchors_WrongBasisCollapse_FullStretch()
        {
            // A "safe area" smaller than half the screen is a mismatched-basis
            // artifact (Game view ↔ Simulator switch), never a real notch.
            Assert.That(SafeArea.Anchors(new Rect(0f, 0f, 300f, 300f), 1284, 2778),
                Is.EqualTo((Vector2.zero, Vector2.one)));
        }
    }
}
