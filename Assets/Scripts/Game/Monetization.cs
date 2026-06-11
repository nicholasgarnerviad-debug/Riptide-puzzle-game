using System;
using Riptide.Core;
using UnityEngine;

namespace Riptide.Game
{
    public enum ConsentState
    {
        Unknown,
        Obtained,
        NotRequired,
        Denied,
    }

    /// <summary>Seam for UMP (GDD 6/7A). Callbacks may arrive on any thread.</summary>
    public interface IConsentProvider
    {
        void Request(Action<ConsentState> onResolved);

        void Reopen(Action<ConsentState> onResolved);
    }

    /// <summary>Seam for the ad SDK (GDD 7B). Callbacks may arrive on any thread.</summary>
    public interface IAdSdk
    {
        void Initialize(Action onReady);

        bool InterstitialReady { get; }

        bool RewardedReady { get; }

        void ShowInterstitial(Action onClosed);

        /// <summary>onRewarded may fire multiple times or after close — the service latches it.</summary>
        void ShowRewarded(Action onRewarded, Action onClosed);
    }

    /// <summary>Seam for the store (GDD 7C).</summary>
    public interface IIapSdk
    {
        void PurchaseRemoveAds(Action<bool> onResult);

        void RestorePurchases(Action<bool> onRemoveAdsOwned);
    }

    /// <summary>
    /// GDD 7A: UMP consent strictly precedes ad initialization. Holds the resolved
    /// state and the settings re-open path.
    /// </summary>
    public sealed class ConsentService
    {
        private readonly IConsentProvider provider;

        public ConsentState State { get; private set; } = ConsentState.Unknown;

        public event Action<ConsentState>? Resolved;

        public ConsentService(IConsentProvider provider)
        {
            this.provider = provider;
        }

        public void Request()
        {
            provider.Request(state => MainThreadDispatcher.Post(() => Apply(state)));
        }

        /// <summary>GDD 7A: settings re-open path.</summary>
        public void Reopen()
        {
            provider.Reopen(state => MainThreadDispatcher.Post(() => Apply(state)));
        }

        private void Apply(ConsentState state)
        {
            State = state;
            Resolved?.Invoke(state);
        }
    }

    /// <summary>
    /// GDD 6/7B ad orchestration: consent-gated init, pure interstitial caps from
    /// Core against save state, rewarded payouts through the exactly-once latch,
    /// every SDK callback marshaled to the main thread, every show logged.
    /// </summary>
    public sealed class AdService
    {
        private readonly IAdSdk sdk;
        private readonly ConsentService consent;
        private readonly MetaServices meta;
        private readonly AdsConfig config;
        private readonly AnalyticsService analytics;
        private readonly RewardedGate gate = new RewardedGate();

        public Func<long> NowUnixSeconds = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public bool Initialized { get; private set; }

        public AdService(IAdSdk sdk, ConsentService consent, MetaServices meta, AdsConfig config, AnalyticsService analytics)
        {
            this.sdk = sdk;
            this.consent = consent;
            this.meta = meta;
            this.config = config;
            this.analytics = analytics;
            consent.Resolved += OnConsentResolved;
        }

        private void OnConsentResolved(ConsentState state)
        {
            // GDD 7A: no ad init before the consent flow resolves.
            if (Initialized || state == ConsentState.Unknown)
            {
                return;
            }

            sdk.Initialize(() => MainThreadDispatcher.Post(() => Initialized = true));
        }

        public bool CanShowInterstitial(bool afterDaily)
        {
            var state = new InterstitialCapState(
                meta.Save.LastInterstitialUnixSeconds,
                meta.Save.InterstitialDay,
                meta.Save.InterstitialsToday);
            return Initialized
                && sdk.InterstitialReady
                && InterstitialCaps.CanShow(config, state, NowUnixSeconds(), meta.TodayEpochDay(),
                    meta.Voyage.CompletedCount, meta.Save.RemoveAds, afterDaily);
        }

        /// <summary>Shows when the GDD 6 caps allow; records the show into the save.</summary>
        public bool TryShowInterstitial(bool afterDaily, string placement, Action? onClosed = null)
        {
            if (!CanShowInterstitial(afterDaily))
            {
                return false;
            }

            var state = new InterstitialCapState(
                meta.Save.LastInterstitialUnixSeconds,
                meta.Save.InterstitialDay,
                meta.Save.InterstitialsToday);
            InterstitialCapState next = InterstitialCaps.RecordShown(state, NowUnixSeconds(), meta.TodayEpochDay());
            meta.Save.LastInterstitialUnixSeconds = next.LastShownUnixSeconds;
            meta.Save.InterstitialDay = next.Day;
            meta.Save.InterstitialsToday = next.ShownToday;
            meta.SaveNow();

            analytics.LogAdImpression("interstitial", placement);
            sdk.ShowInterstitial(() => MainThreadDispatcher.Post(() => onClosed?.Invoke()));
            return true;
        }

        public bool RewardedAvailable => Initialized && sdk.RewardedReady && !gate.Showing;

        /// <summary>
        /// GDD 7B: the payout fires exactly once per show regardless of SDK callback
        /// duplication or ordering; both callbacks land on the main thread.
        /// </summary>
        public bool ShowRewarded(RewardedPlacementId placement, Action onPaid, Action? onClosed = null)
        {
            if (!RewardedAvailable || !gate.BeginShow())
            {
                return false;
            }

            analytics.LogAdImpression("rewarded", placement.ToString());
            sdk.ShowRewarded(
                () => MainThreadDispatcher.Post(() =>
                {
                    if (gate.TryGrantPayout())
                    {
                        onPaid();
                    }
                }),
                () => MainThreadDispatcher.Post(() =>
                {
                    gate.EndShow();
                    onClosed?.Invoke();
                }));
            return true;
        }
    }

    /// <summary>GDD 7C: Remove Ads ($4.99) — kills interstitials only; restore supported.</summary>
    public sealed class IapService
    {
        private readonly IIapSdk sdk;
        private readonly MetaServices meta;
        private readonly AnalyticsService analytics;

        public IapService(IIapSdk sdk, MetaServices meta, AnalyticsService analytics)
        {
            this.sdk = sdk;
            this.meta = meta;
            this.analytics = analytics;
        }

        public bool RemoveAdsOwned => meta.Save.RemoveAds;

        public void PurchaseRemoveAds(Action<bool>? onDone = null)
        {
            sdk.PurchaseRemoveAds(success => MainThreadDispatcher.Post(() =>
            {
                if (success && !meta.Save.RemoveAds)
                {
                    meta.Save.RemoveAds = true;
                    meta.SaveNow();
                    analytics.Log(AnalyticsSchema.IapPurchase);
                }

                onDone?.Invoke(success);
            }));
        }

        public void Restore(Action<bool>? onDone = null)
        {
            sdk.RestorePurchases(owned => MainThreadDispatcher.Post(() =>
            {
                if (owned && !meta.Save.RemoveAds)
                {
                    meta.Save.RemoveAds = true;
                    meta.SaveNow();
                }

                onDone?.Invoke(owned);
            }));
        }
    }

    // ---------------- fakes (editor + tests; replaced by real adapters at gate 4) ----------------

    public sealed class FakeConsentProvider : IConsentProvider
    {
        public ConsentState ResolveAs = ConsentState.NotRequired;

        public void Request(Action<ConsentState> onResolved) => onResolved(ResolveAs);

        public void Reopen(Action<ConsentState> onResolved) => onResolved(ResolveAs);
    }

    /// <summary>
    /// Scriptable fake: can fire callbacks synchronously, or from a worker thread
    /// (FireOnWorkerThread) to regression-test the marshaling, and can duplicate
    /// the reward callback (DuplicateRewardCallbacks) to attack the payout latch.
    /// </summary>
    public sealed class FakeAdSdk : IAdSdk
    {
        public bool FireOnWorkerThread;
        public int DuplicateRewardCallbacks = 1;
        public bool InterstitialReady { get; set; } = true;
        public bool RewardedReady { get; set; } = true;
        public int InitCalls { get; private set; }
        public int InterstitialsShown { get; private set; }
        public int RewardedShown { get; private set; }

        public void Initialize(Action onReady)
        {
            InitCalls++;
            Fire(onReady);
        }

        public void ShowInterstitial(Action onClosed)
        {
            InterstitialsShown++;
            Fire(onClosed);
        }

        public void ShowRewarded(Action onRewarded, Action onClosed)
        {
            RewardedShown++;
            Fire(() =>
            {
                for (int i = 0; i < DuplicateRewardCallbacks; i++)
                {
                    onRewarded();
                }

                onClosed();
            });
        }

        private void Fire(Action action)
        {
            if (FireOnWorkerThread)
            {
                System.Threading.Tasks.Task.Run(action);
            }
            else
            {
                action();
            }
        }
    }

    public sealed class FakeIapSdk : IIapSdk
    {
        public bool PurchaseSucceeds = true;
        public bool RestoreFindsOwnership;

        public void PurchaseRemoveAds(Action<bool> onResult) => onResult(PurchaseSucceeds);

        public void RestorePurchases(Action<bool> onRemoveAdsOwned) => onRemoveAdsOwned(RestoreFindsOwnership);
    }
}
