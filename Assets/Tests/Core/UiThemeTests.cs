using NUnit.Framework;
using Riptide.Core;

namespace Riptide.Core.Tests
{
    /// <summary>
    /// UI spec 4-UI-a acceptance (loader half): token round-trip, locked durations,
    /// missing-token failures, and the WCAG math the ContentCheck audit relies on.
    /// The real ui_theme.json is audited by ContentCheck (gate 3) to avoid fixture drift.
    /// </summary>
    [TestFixture]
    public sealed class UiThemeTests
    {
        private static string MinimalTheme(string riseMs = "350", string drainMs = "450")
        {
            string colors = "";
            foreach (string key in UiThemeLoader.RequiredColors)
            {
                colors += $"\"{key}\": {{ \"hex\": \"3EE6E0\" }},";
            }

            string types = "";
            foreach (string key in UiThemeLoader.RequiredType)
            {
                types += $"\"{key}\": {{ \"size\": 30, \"line\": 40, \"weight\": \"Medium\", \"tracking\": 0, \"tabular\": false, \"allCaps\": false }},";
            }

            return $@"{{
  ""colors"": {{ {colors.TrimEnd(',')} }},
  ""type"": {{ {types.TrimEnd(',')} }},
  ""spacing"": {{ ""scale"": [4, 8], ""gutter"": 48, ""cardPadding"": 32 }},
  ""radius"": {{ ""r.s"": 12, ""r.m"": 20, ""r.l"": 32, ""block"": 8 }},
  ""motion"": {{
    ""durationsMs"": {{ ""t.instant"": 90, ""t.fast"": 180, ""t.base"": 260, ""t.screen"": 340, ""t.rise"": {riseMs}, ""t.drain"": {drainMs} }},
    ""easings"": {{ ""linear"": [[0,0,1,1],[1,1,1,1]] }},
    ""screenTransition"": {{ ""driftPx"": 24, ""overlapMs"": 60, ""staggerMs"": 40, ""maxStagger"": 6 }},
    ""reducedMotionScale"": 0.5
  }},
  ""juice"": {{ ""place"": {{ ""sfx"": ""Place"", ""haptic"": ""light"", ""anim"": ""none"" }} }},
  ""layout"": {{ ""canvasRefWidth"": 1080, ""canvasRefHeight"": 2347, ""hudBandRefPx"": 140, ""boardTopGapRefPx"": 24, ""trayBottomInsetRefPx"": 48, ""boosterRailBandRefPx"": 170, ""boardSideAllowanceRefPx"": 104 }},
  ""accessibility"": {{ ""minTouchTargetRefPx"": 120, ""minBodyContrast"": 4.5, ""minLargeContrast"": 3.0, ""minOnAccentContrast"": 7.0, ""minBlockLuminanceStepRatio"": 1.15 }}
}}";
        }

        [Test]
        public void Theme_RoundTrips_EveryRequiredToken()
        {
            UiTheme theme = UiThemeLoader.Load(MinimalTheme(), "ui_theme.json");

            foreach (string key in UiThemeLoader.RequiredColors)
            {
                Assert.That(theme.Color(key).A, Is.GreaterThan(0f), key);
            }

            foreach (string key in UiThemeLoader.RequiredType)
            {
                Assert.That(theme.TypeStyle(key).Size, Is.GreaterThan(0), key);
            }

            foreach (string key in UiThemeLoader.RequiredDurations)
            {
                Assert.That(theme.Duration(key), Is.GreaterThan(0), key);
            }

            Assert.That(theme.Easing("linear").Count, Is.EqualTo(2));
            Assert.That(theme.MinTouchTargetRefPx, Is.EqualTo(120), "spec §3/§8");

            // Universal-fit amendment: canvas basis is the iPhone 16 Pro Max
            // aspect (19.5:9) at 1080 token width, matched on width.
            Assert.That(theme.Layout.CanvasRefWidth, Is.EqualTo(1080));
            Assert.That(theme.Layout.CanvasRefHeight, Is.EqualTo(2347));
            Assert.That(theme.Layout.BoardSideAllowanceRefPx, Is.GreaterThan(0));
        }

        [Test]
        public void Theme_MissingToken_ThrowsWithKeyName()
        {
            UiTheme theme = UiThemeLoader.Load(MinimalTheme(), "ui_theme.json");

            var ex = Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => theme.Color("bg.typo"));
            Assert.That(ex!.Message, Does.Contain("bg.typo"), "a typo'd key must never silently render");
        }

        [Test]
        public void Theme_RejectsDriftOfGddLockedDurations()
        {
            Assert.Throws<ContentException>(() => UiThemeLoader.Load(MinimalTheme(riseMs: "300"), "ui_theme.json"),
                "t.rise is GDD-locked at 350");
            Assert.Throws<ContentException>(() => UiThemeLoader.Load(MinimalTheme(drainMs: "500"), "ui_theme.json"),
                "t.drain is GDD-locked at 450");
        }

        [Test]
        public void Theme_RejectsMissingRequiredColor()
        {
            string broken = MinimalTheme().Replace("\"accent.primary\"", "\"accent.banana\"");

            var ex = Assert.Throws<ContentException>(() => UiThemeLoader.Load(broken, "ui_theme.json"));
            Assert.That(ex!.Message, Does.Contain("accent.primary"));
        }

        [Test]
        public void WcagContrast_MatchesKnownAnchors()
        {
            var black = new ThemeColor(0f, 0f, 0f, 1f);
            var white = new ThemeColor(1f, 1f, 1f, 1f);

            Assert.That(ThemeColor.ContrastRatio(black, white), Is.EqualTo(21.0).Within(0.01), "WCAG canonical");
            Assert.That(ThemeColor.ContrastRatio(white, black), Is.EqualTo(21.0).Within(0.01), "symmetric");
            Assert.That(ThemeColor.ContrastRatio(white, white), Is.EqualTo(1.0).Within(0.01));
        }
    }
}
