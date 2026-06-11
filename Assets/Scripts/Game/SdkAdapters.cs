// ============================================================================
// REAL SDK ADAPTERS — inactive until the SDKs are installed (DECISIONS.md P7).
//
// Activation checklist for Nick (Visual Gate 4 prerequisites):
//   1. Google Mobile Ads Unity plugin + UMP  → add RIPTIDE_ADMOB to
//      Project Settings > Player > Scripting Define Symbols, set the AdMob app id
//      in the GoogleMobileAds settings asset, and create 2 interstitial + 2
//      rewarded ad units (prod + test pairs, GDD 6).
//   2. Firebase Analytics (.unitypackage + google-services.json) → RIPTIDE_FIREBASE.
//   3. com.unity.purchasing (Unity IAP) → RIPTIDE_IAP, product id "remove_ads".
//
// These bodies follow the official SDK API shapes but CANNOT compile or be
// verified until the packages exist — that verification is part of gate 4.
// ============================================================================

#if RIPTIDE_ADMOB
using System;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;

namespace Riptide.Game
{
    /// <summary>UMP consent flow (GDD 7A): gather/refresh consent before any ad init.</summary>
    public sealed class UmpConsentProvider : IConsentProvider
    {
        public void Request(Action<ConsentState> onResolved)
        {
            var parameters = new ConsentRequestParameters();
            ConsentInformation.Update(parameters, error =>
            {
                if (error != null)
                {
                    onResolved(ConsentState.Unknown);
                    return;
                }

                ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
                {
                    onResolved(ConsentInformation.CanRequestAds()
                        ? ConsentState.Obtained
                        : ConsentState.Denied);
                });
            });
        }

        public void Reopen(Action<ConsentState> onResolved)
        {
            ConsentForm.ShowPrivacyOptionsForm(error =>
            {
                onResolved(ConsentInformation.CanRequestAds()
                    ? ConsentState.Obtained
                    : ConsentState.Denied);
            });
        }
    }

    /// <summary>AdMob interstitial + rewarded with reload-on-close (GDD 6 ad units).</summary>
    public sealed class GoogleMobileAdsAdapter : IAdSdk
    {
        private readonly string interstitialUnitId;
        private readonly string rewardedUnitId;
        private InterstitialAd? interstitial;
        private RewardedAd? rewarded;

        public GoogleMobileAdsAdapter(string interstitialUnitId, string rewardedUnitId)
        {
            this.interstitialUnitId = interstitialUnitId;
            this.rewardedUnitId = rewardedUnitId;
        }

        public bool InterstitialReady => interstitial != null && interstitial.CanShowAd();

        public bool RewardedReady => rewarded != null && rewarded.CanShowAd();

        public void Initialize(Action onReady)
        {
            MobileAds.Initialize(_ =>
            {
                LoadInterstitial();
                LoadRewarded();
                onReady();
            });
        }

        private void LoadInterstitial()
        {
            InterstitialAd.Load(interstitialUnitId, new AdRequest(), (ad, error) =>
            {
                if (error == null)
                {
                    interstitial = ad;
                }
            });
        }

        private void LoadRewarded()
        {
            RewardedAd.Load(rewardedUnitId, new AdRequest(), (ad, error) =>
            {
                if (error == null)
                {
                    rewarded = ad;
                }
            });
        }

        public void ShowInterstitial(Action onClosed)
        {
            InterstitialAd? ad = interstitial;
            interstitial = null;
            if (ad == null)
            {
                onClosed();
                return;
            }

            ad.OnAdFullScreenContentClosed += () =>
            {
                ad.Destroy();
                LoadInterstitial();
                onClosed();
            };
            ad.Show();
        }

        public void ShowRewarded(Action onRewarded, Action onClosed)
        {
            RewardedAd? ad = rewarded;
            rewarded = null;
            if (ad == null)
            {
                onClosed();
                return;
            }

            ad.OnAdFullScreenContentClosed += () =>
            {
                ad.Destroy();
                LoadRewarded();
                onClosed();
            };
            ad.Show(_ => onRewarded());
        }
    }
}
#endif

#if RIPTIDE_FIREBASE
using System.Collections.Generic;
using Firebase.Analytics;

namespace Riptide.Game
{
    /// <summary>Firebase Analytics sink (GDD 8.5).</summary>
    public sealed class FirebaseAnalyticsSink : IAnalyticsSink
    {
        public void Log(string eventName, IReadOnlyList<(string key, string value)> parameters)
        {
            var firebaseParams = new Parameter[parameters.Count];
            for (int i = 0; i < parameters.Count; i++)
            {
                firebaseParams[i] = new Parameter(parameters[i].key, parameters[i].value);
            }

            FirebaseAnalytics.LogEvent(eventName, firebaseParams);
        }
    }
}
#endif

#if RIPTIDE_IAP
using System;
using UnityEngine.Purchasing;

namespace Riptide.Game
{
    /// <summary>Unity IAP adapter for the single Remove Ads product (GDD 7C).</summary>
    public sealed class UnityIapAdapter : IIapSdk, IDetailedStoreListener
    {
        private const string RemoveAdsProductId = "remove_ads";
        private IStoreController? controller;
        private Action<bool>? pendingPurchase;
        private Action<bool>? pendingRestore;

        public UnityIapAdapter()
        {
            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            builder.AddProduct(RemoveAdsProductId, ProductType.NonConsumable);
            UnityPurchasing.Initialize(this, builder);
        }

        public void PurchaseRemoveAds(Action<bool> onResult)
        {
            if (controller == null)
            {
                onResult(false);
                return;
            }

            pendingPurchase = onResult;
            controller.InitiatePurchase(RemoveAdsProductId);
        }

        public void RestorePurchases(Action<bool> onRemoveAdsOwned)
        {
            Product? product = controller?.products.WithID(RemoveAdsProductId);
            onRemoveAdsOwned(product != null && product.hasReceipt);
        }

        public void OnInitialized(IStoreController storeController, IExtensionProvider extensions) =>
            controller = storeController;

        public void OnInitializeFailed(InitializationFailureReason error, string? message)
        {
        }

        public void OnInitializeFailed(InitializationFailureReason error)
        {
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs purchaseEvent)
        {
            if (purchaseEvent.purchasedProduct.definition.id == RemoveAdsProductId)
            {
                pendingPurchase?.Invoke(true);
                pendingPurchase = null;
            }

            return PurchaseProcessingResult.Complete;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            pendingPurchase?.Invoke(false);
            pendingPurchase = null;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
        {
            pendingPurchase?.Invoke(false);
            pendingPurchase = null;
        }
    }
}
#endif
