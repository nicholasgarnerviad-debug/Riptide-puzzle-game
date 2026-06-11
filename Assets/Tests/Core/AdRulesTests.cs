using NUnit.Framework;
using Riptide.Core;

namespace Riptide.Core.Tests
{
    /// <summary>
    /// Phase 7 headless acceptance: GDD 6 interstitial caps as pure predicates,
    /// the pays-exactly-once rewarded latch, and the verbatim GDD 8.5 event names.
    /// </summary>
    [TestFixture]
    public sealed class AdRulesTests
    {
        private static AdsConfig Ads() => TestKit.Economy().Ads;

        private const long Day = 20_600;
        private const long Noon = 1_780_000_000;

        [Test]
        public void Interstitials_NeverShow_BeforeEightLevelCompletions()
        {
            Assert.That(InterstitialCaps.CanShow(Ads(), InterstitialCapState.Empty, Noon, Day,
                voyageLevelsCompleted: 7, removeAdsOwned: false, afterDaily: false), Is.False, "GDD 6: none before level 8");
            Assert.That(InterstitialCaps.CanShow(Ads(), InterstitialCapState.Empty, Noon, Day,
                voyageLevelsCompleted: 8, removeAdsOwned: false, afterDaily: false), Is.True);
        }

        [Test]
        public void Interstitials_Honor_TheMinimumGap()
        {
            InterstitialCapState shown = InterstitialCaps.RecordShown(InterstitialCapState.Empty, Noon, Day);

            Assert.That(InterstitialCaps.CanShow(Ads(), shown, Noon + 149, Day, 20, false, false), Is.False,
                "GDD 6: min 150s between interstitials");
            Assert.That(InterstitialCaps.CanShow(Ads(), shown, Noon + 150, Day, 20, false, false), Is.True);
        }

        [Test]
        public void Interstitials_CapAtSixPerDay_AndResetTomorrow()
        {
            InterstitialCapState state = InterstitialCapState.Empty;
            long t = Noon;
            for (int i = 0; i < 6; i++)
            {
                Assert.That(InterstitialCaps.CanShow(Ads(), state, t, Day, 20, false, false), Is.True, $"show {i + 1}");
                state = InterstitialCaps.RecordShown(state, t, Day);
                t += 200;
            }

            Assert.That(InterstitialCaps.CanShow(Ads(), state, t, Day, 20, false, false), Is.False, "GDD 6: max 6/day");
            Assert.That(InterstitialCaps.CanShow(Ads(), state, t + 86_400, Day + 1, 20, false, false), Is.True,
                "a new day resets the count");
        }

        [Test]
        public void Interstitials_NeverShow_AfterADaily_OrWithRemoveAds()
        {
            Assert.That(InterstitialCaps.CanShow(Ads(), InterstitialCapState.Empty, Noon, Day, 20, false, afterDaily: true),
                Is.False, "GDD 6: never after a daily — protect the ritual");
            Assert.That(InterstitialCaps.CanShow(Ads(), InterstitialCapState.Empty, Noon, Day, 20, removeAdsOwned: true, false),
                Is.False, "GDD 6/7C: Remove Ads kills interstitials");
        }

        [Test]
        public void RewardedGate_PaysExactlyOnce_DespiteDuplicateCallbacks()
        {
            var gate = new RewardedGate();

            Assert.That(gate.BeginShow(), Is.True);
            Assert.That(gate.TryGrantPayout(), Is.True, "first reward callback pays");
            Assert.That(gate.TryGrantPayout(), Is.False, "duplicate SDK reward callback must not double-pay (audit regression)");
            gate.EndShow();
            Assert.That(gate.TryGrantPayout(), Is.False, "late callback after close pays nothing");
        }

        [Test]
        public void RewardedGate_RelatchesPerShow_AndRejectsConcurrentShows()
        {
            var gate = new RewardedGate();

            gate.BeginShow();
            Assert.That(gate.BeginShow(), Is.False, "one show at a time");
            gate.EndShow();

            Assert.That(gate.TryGrantPayout(), Is.False, "no payout without a show");
            Assert.That(gate.BeginShow(), Is.True);
            Assert.That(gate.TryGrantPayout(), Is.True, "a new show pays once again");
            gate.EndShow();
        }

        [Test]
        public void AnalyticsEventNames_MatchGdd85_Verbatim()
        {
            Assert.That(AnalyticsSchema.LevelStart, Is.EqualTo("level_start"));
            Assert.That(AnalyticsSchema.LevelEnd, Is.EqualTo("level_end"));
            Assert.That(AnalyticsSchema.EndlessEnd, Is.EqualTo("endless_end"));
            Assert.That(AnalyticsSchema.DailyAttempt, Is.EqualTo("daily_attempt"));
            Assert.That(AnalyticsSchema.BoosterUsed, Is.EqualTo("booster_used"));
            Assert.That(AnalyticsSchema.AdImpression, Is.EqualTo("ad_impression"));
            Assert.That(AnalyticsSchema.IapPurchase, Is.EqualTo("iap_purchase"));
            Assert.That(AnalyticsSchema.TidepoolPurchase, Is.EqualTo("tidepool_purchase"));
            Assert.That(AnalyticsSchema.TutorialStep, Is.EqualTo("tutorial_step"));
        }

        [Test]
        public void AnalyticsParamLists_MatchGdd85_Verbatim()
        {
            Assert.That(AnalyticsSchema.LevelEndParams,
                Is.EqualTo(new[] { "zone", "level", "result", "moves", "stars", "maxWater", "rescues" }));
            Assert.That(AnalyticsSchema.EndlessEndParams,
                Is.EqualTo(new[] { "placements", "tides", "score", "deathType" }));
            Assert.That(AnalyticsSchema.DailyAttemptParams, Is.EqualTo(new[] { "result", "score", "retryUsed" }));
            Assert.That(AnalyticsSchema.BoosterUsedParams, Is.EqualTo(new[] { "type", "source" }));
            Assert.That(AnalyticsSchema.AdImpressionParams, Is.EqualTo(new[] { "format", "placement" }));
        }

        [Test]
        public void SaveV1_MigratesToV2_WithDefaultCapState()
        {
            SaveData? migrated = SaveData.TryParse(SaveTests.V1Fixture);

            Assert.That(migrated, Is.Not.Null, "contract 6D: v1 saves survive the v2 bump");
            Assert.That(migrated!.LastInterstitialUnixSeconds, Is.EqualTo(0));
            Assert.That(migrated.InterstitialDay, Is.EqualTo(-1));
            Assert.That(migrated.InterstitialsToday, Is.EqualTo(0));
            Assert.That(migrated.Coins, Is.EqualTo(1234), "v1 payload intact after migration");

            SaveData? reserialized = SaveData.TryParse(migrated.Serialize());
            Assert.That(reserialized, Is.Not.Null, "migrated saves re-serialize as v2");
        }
    }
}
