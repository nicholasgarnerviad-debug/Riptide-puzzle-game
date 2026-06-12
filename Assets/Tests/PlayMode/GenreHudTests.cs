using System.Collections;
using NUnit.Framework;
using Riptide.Game;
using Riptide.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Riptide.PlayMode.Tests
{
    /// <summary>
    /// Genre UI pass (spec §12.4): praise flash, combo chip, endless best chip —
    /// present in the HUD, correct copy from strings.json, correct initial states.
    /// The on-device look is Gate material; the wiring is provable here.
    /// </summary>
    public sealed class GenreHudTests
    {
        [UnityTest]
        public IEnumerator Hud_HasGenreElements_InCorrectInitialStates()
        {
            (GameFlow flow, ScreenManager screens) = GameBootstrap.CreateApp(instantAnimations: true);
            yield return null;

            flow.StartEndless();
            yield return null;

            HudOverlay hud = Object.FindFirstObjectByType<HudOverlay>(FindObjectsInactive.Include);
            Assert.That(hud, Is.Not.Null, "HUD builds with the board rig");

            Transform praise = hud.transform.Find("praise");
            Assert.That(praise, Is.Not.Null, "praise flash element exists");
            Assert.That(praise.gameObject.activeSelf, Is.False, "praise hidden until a multi-clear");

            Transform combo = hud.transform.Find("safe/combo");
            Assert.That(combo, Is.Not.Null, "combo chip exists");
            Assert.That(combo.gameObject.activeSelf, Is.False, "no chain at run start");

            Object.Destroy(screens.transform.parent != null ? screens.transform.parent.gameObject : screens.gameObject);
        }

        [UnityTest]
        public IEnumerator Praise_ShowsMagnitudeCopy_FromStrings()
        {
            (GameFlow flow, ScreenManager screens) = GameBootstrap.CreateApp(instantAnimations: true);
            yield return null;

            flow.StartEndless();
            yield return null;

            HudOverlay hud = Object.FindFirstObjectByType<HudOverlay>(FindObjectsInactive.Include);
            Text praise = hud.transform.Find("praise").GetComponent<Text>();

            hud.ShowPraise(2);
            Assert.That(praise.gameObject.activeSelf, Is.True);
            Assert.That(praise.text, Is.EqualTo(flow.Strings.Get("praise.double")));

            hud.ShowPraise(3);
            Assert.That(praise.text, Is.EqualTo(flow.Strings.Get("praise.triple")));

            hud.ShowPraise(4);
            Assert.That(praise.text, Is.EqualTo(flow.Strings.Get("praise.quad")), "4+ rows = the brand beat");

            Object.Destroy(screens.transform.parent != null ? screens.transform.parent.gameObject : screens.gameObject);
        }

        [Test]
        public void ComboMultiplier_FormatsHalves_InvariantAndClean()
        {
            Assert.That(HudOverlay.FormatHalves(2), Is.EqualTo("1"));
            Assert.That(HudOverlay.FormatHalves(3), Is.EqualTo("1.5"));
            Assert.That(HudOverlay.FormatHalves(4), Is.EqualTo("2"));
            Assert.That(HudOverlay.FormatHalves(5), Is.EqualTo("2.5"));
        }

        [UnityTest]
        public IEnumerator BestChip_HiddenForFreshProfile_VisibleWithABest()
        {
            (GameFlow flow, ScreenManager screens) = GameBootstrap.CreateApp(instantAnimations: true);
            yield return null;

            bool hadBest = flow.Meta.EndlessBest > 0;
            flow.StartEndless();
            yield return null;

            HudOverlay hud = Object.FindFirstObjectByType<HudOverlay>(FindObjectsInactive.Include);
            Transform best = hud.transform.Find("safe/best");
            Assert.That(best, Is.Not.Null, "best chip exists");
            Assert.That(best.gameObject.activeSelf, Is.EqualTo(hadBest),
                "chip shows exactly when a personal best exists to chase");

            Object.Destroy(screens.transform.parent != null ? screens.transform.parent.gameObject : screens.gameObject);
        }
    }
}
