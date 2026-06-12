using NUnit.Framework;
using Riptide.Core;

namespace Riptide.Core.Tests
{
    /// <summary>
    /// Universal screen fit (spec §2 as amended): the world camera must show the
    /// full 9-column board + side allowance on every portrait device, keep the
    /// content column between the HUD band and the bottom inset, and never let
    /// safe-area insets eat the board. The old fixed orthoSize 8.7 clipped the
    /// outer columns on every modern phone (aspect &lt; 0.517) — these tests pin
    /// the fix across the device matrix, iPhone 16 Pro Max as the basis.
    /// </summary>
    [TestFixture]
    public sealed class CameraFitTests
    {
        // Mirrors the runtime wiring: board half-width 4.5 (9 cols × 1 world unit)
        // + the theme's 104 ref-px side allowance converted at 1080−2×48 gutters.
        private const float HalfWidthWorld = 4.5f + 104f * 9f / (1080f - 96f);
        private const float ContentTop = 7.45f;      // board frame top (6.9 + 0.55 pad)
        private const float ContentBottom = -8.45f;  // tray card bottom (−7.1 − 1.35)
        private const float TopUiRefPx = 140f + 24f; // HUD band + gap
        private const float BottomUiRefPx = 48f + 170f; // bottom inset + booster rail band
        private const float CanvasRefWidth = 1080f;

        private static CameraFitInput Device(float w, float h, float safeTop, float safeBottom) =>
            new CameraFitInput(w, h, safeTop, safeBottom, HalfWidthWorld,
                ContentTop, ContentBottom, TopUiRefPx, BottomUiRefPx, CanvasRefWidth);

        private static readonly object[] Matrix =
        {
            new object[] { "iPhone 16 Pro Max (basis)", 1320f, 2868f, 186f, 102f },
            new object[] { "iPhone SE 2 (16:9)", 750f, 1334f, 0f, 0f },
            new object[] { "Pixel 8 (20:9)", 1080f, 2400f, 118f, 63f },
            new object[] { "Galaxy S24 Ultra (19.5:9 hi-dpi)", 1440f, 3120f, 120f, 0f },
            new object[] { "Xperia 1 (21:9)", 1644f, 3840f, 0f, 0f },
            new object[] { "Tablet (3:4)", 1620f, 2160f, 0f, 0f },
        };

        [TestCaseSource(nameof(Matrix))]
        public void Fit_ShowsFullBoardWidth(string name, float w, float h, float safeTop, float safeBottom)
        {
            CameraFitResult fit = CameraFit.Solve(Device(w, h, safeTop, safeBottom));
            float visibleHalfWidth = fit.OrthoSize * (w / h);

            Assert.That(visibleHalfWidth, Is.GreaterThanOrEqualTo(HalfWidthWorld - 0.001f),
                $"{name}: board + allowance must span the screen");
        }

        [TestCaseSource(nameof(Matrix))]
        public void Fit_KeepsContentBelowHudAndAboveInset(string name, float w, float h, float safeTop, float safeBottom)
        {
            CameraFitResult fit = CameraFit.Solve(Device(w, h, safeTop, safeBottom));
            float worldPerPx = 2f * fit.OrthoSize / h;
            float visibleTop = fit.CameraY + fit.OrthoSize;
            float visibleBottom = fit.CameraY - fit.OrthoSize;
            float uiScale = w / CanvasRefWidth;
            float topPadWorld = (safeTop + TopUiRefPx * uiScale) * worldPerPx;
            float bottomPadWorld = (safeBottom + BottomUiRefPx * uiScale) * worldPerPx;

            Assert.That(visibleTop - topPadWorld, Is.EqualTo(ContentTop).Within(0.001f),
                $"{name}: content is top-anchored exactly below safe area + HUD band");
            Assert.That(visibleBottom + bottomPadWorld, Is.LessThanOrEqualTo(ContentBottom + 0.001f),
                $"{name}: tray bottom clears the bottom inset");
        }

        [TestCaseSource(nameof(Matrix))]
        public void Fit_TrayLandsInBottomThumbZone(string name, float w, float h, float safeTop, float safeBottom)
        {
            CameraFitResult fit = CameraFit.Solve(Device(w, h, safeTop, safeBottom));
            float visibleBottom = fit.CameraY - fit.OrthoSize;
            float trayCenterFraction = (-7.1f - visibleBottom) / (2f * fit.OrthoSize);

            // Spec §8: primary play actions live in the bottom 60% of the screen.
            Assert.That(trayCenterFraction, Is.LessThan(0.4f),
                $"{name}: tray center must sit in the bottom 40% of the view");
        }

        [Test]
        public void Fit_OldFixedOrtho_WouldHaveClippedTheBasisDevice()
        {
            // Regression documentation: the bug this system replaces.
            float oldVisibleHalfWidth = 8.7f * (1320f / 2868f);
            Assert.That(oldVisibleHalfWidth, Is.LessThan(4.5f),
                "fixed 8.7 could not even show the bare board on the basis device");
        }

        [Test]
        public void Fit_WidthDrives_OnPhones_HeightDrives_OnTablets()
        {
            CameraFitResult phone = CameraFit.Solve(Device(1320f, 2868f, 186f, 102f));
            float phoneWidthOrtho = HalfWidthWorld / (1320f / 2868f);
            Assert.That(phone.OrthoSize, Is.EqualTo(phoneWidthOrtho).Within(0.001f),
                "narrow phone: the width requirement binds");

            CameraFitResult tablet = CameraFit.Solve(Device(1620f, 2160f, 0f, 0f));
            float tabletWidthOrtho = HalfWidthWorld / (1620f / 2160f);
            Assert.That(tablet.OrthoSize, Is.GreaterThan(tabletWidthOrtho),
                "tablet: vertical content span binds and zooms the camera out");
        }

        [Test]
        public void Fit_DegenerateInput_DoesNotExplode()
        {
            CameraFitResult fit = CameraFit.Solve(new CameraFitInput(0f, 0f, 0f, 0f,
                HalfWidthWorld, ContentTop, ContentBottom, TopUiRefPx, BottomUiRefPx, CanvasRefWidth));

            Assert.That(float.IsNaN(fit.OrthoSize), Is.False);
            Assert.That(float.IsInfinity(fit.OrthoSize), Is.False);
        }
    }
}
