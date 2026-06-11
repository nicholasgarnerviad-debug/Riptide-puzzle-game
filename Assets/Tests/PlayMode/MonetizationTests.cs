using System.Collections;
using NUnit.Framework;
using Riptide.Core;
using Riptide.Game;
using UnityEngine;
using UnityEngine.TestTools;

namespace Riptide.PlayMode.Tests
{
    /// <summary>
    /// Phase 7 acceptance against fakes: consent gates init, SDK callbacks from
    /// worker threads land on the main thread (the Star Ladder audit regression),
    /// rewarded pays exactly once through the service, Remove Ads kills
    /// interstitials only, and the debug ring holds the last 20 events.
    /// </summary>
    public sealed class MonetizationTests
    {
        private static (MetaServices meta, AnalyticsService analytics, ConsentService consent, FakeAdSdk sdk, AdService ads)
            BuildServices(FakeConsentProvider? consentProvider = null)
        {
            MainThreadDispatcher.Ensure();
            string tempSave = System.IO.Path.Combine(Application.temporaryCachePath,
                $"mon_test_{System.Guid.NewGuid():N}.json");
            var meta = new MetaServices(new SaveStore(tempSave));
            meta.Load();
            var analytics = new AnalyticsService();
            var consent = new ConsentService(consentProvider ?? new FakeConsentProvider());
            var sdk = new FakeAdSdk();
            var ads = new AdService(sdk, consent, meta,
                new AdsConfig(minLevelCompletions: 0, minGapSeconds: 0, maxPerDay: 99), analytics);
            return (meta, analytics, consent, sdk, ads);
        }

        [UnityTest]
        public IEnumerator AdInit_WaitsForConsent_ThenInitializesOnce()
        {
            (MetaServices _, AnalyticsService _, ConsentService consent, FakeAdSdk sdk, AdService ads) = BuildServices();

            yield return null;
            Assert.That(sdk.InitCalls, Is.EqualTo(0), "GDD 7A: no ad init before consent resolves");
            Assert.That(ads.Initialized, Is.False);

            consent.Request();
            yield return null;
            yield return null;
            Assert.That(sdk.InitCalls, Is.EqualTo(1));
            Assert.That(ads.Initialized, Is.True);
        }

        [UnityTest]
        public IEnumerator WorkerThreadCallbacks_AreMarshaled_ToTheMainThread()
        {
            (MetaServices _, AnalyticsService _, ConsentService consent, FakeAdSdk sdk, AdService ads) = BuildServices();
            sdk.FireOnWorkerThread = true;
            consent.Request();
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!ads.Initialized && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(ads.Initialized, Is.True, "init callback marshaled");

            bool paid = false;
            bool paidOnMainThread = false;
            bool closed = false;
            Assert.That(ads.ShowRewarded(RewardedPlacementId.CoinChest, onPaid: () =>
            {
                paid = true;
                paidOnMainThread = MainThreadDispatcher.IsMainThread;
            }, onClosed: () => closed = true), Is.True);

            deadline = Time.realtimeSinceStartup + 5f;
            while (!(paid && closed) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(paid, Is.True, "payout arrived");
            Assert.That(paidOnMainThread, Is.True,
                "GDD 6/7B: the SDK fired from a worker thread; the handler must run on main (audit regression)");
        }

        [UnityTest]
        public IEnumerator Rewarded_PaysExactlyOnce_WhenTheSdkDoubleFires()
        {
            (MetaServices _, AnalyticsService _, ConsentService consent, FakeAdSdk sdk, AdService ads) = BuildServices();
            sdk.DuplicateRewardCallbacks = 3;
            consent.Request();
            yield return null;
            yield return null;

            int payouts = 0;
            ads.ShowRewarded(RewardedPlacementId.DoubleCoins, onPaid: () => payouts++);
            yield return null;
            yield return null;

            Assert.That(payouts, Is.EqualTo(1), "GDD 7B: each rewarded placement pays exactly once");
        }

        [UnityTest]
        public IEnumerator RemoveAds_KillsInterstitialsOnly()
        {
            (MetaServices meta, AnalyticsService analytics, ConsentService consent, FakeAdSdk _, AdService ads) = BuildServices();
            consent.Request();
            yield return null;
            yield return null;

            Assert.That(ads.CanShowInterstitial(afterDaily: false), Is.True, "pre-purchase: caps allow");

            var iap = new IapService(new FakeIapSdk { PurchaseSucceeds = true }, meta, analytics);
            bool done = false;
            iap.PurchaseRemoveAds(_ => done = true);
            yield return null;
            yield return null;

            Assert.That(done, Is.True);
            Assert.That(meta.Save.RemoveAds, Is.True, "purchase persisted");
            Assert.That(ads.CanShowInterstitial(afterDaily: false), Is.False, "GDD 7C: interstitials dead");
            Assert.That(ads.RewardedAvailable, Is.True, "GDD 7C: rewarded stays alive (player-positive)");
        }

        [Test]
        public void AnalyticsRing_HoldsExactlyTheLastTwenty()
        {
            var analytics = new AnalyticsService();
            for (int i = 1; i <= 25; i++)
            {
                analytics.Log(AnalyticsSchema.TutorialStep, ("step", i.ToString()));
            }

            Assert.That(analytics.LastEvents.Count, Is.EqualTo(20), "contract 7D: last 20 events");
            var all = new System.Collections.Generic.List<string>(analytics.LastEvents);
            Assert.That(all[0], Does.Contain("step=6"), "oldest five dropped");
            Assert.That(all[19], Does.Contain("step=25"));
        }
    }
}
