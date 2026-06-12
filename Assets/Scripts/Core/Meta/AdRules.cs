using System;

namespace Riptide.Core
{
    /// <summary>GDD 6 interstitial cap numbers, from economy.json `ads` (rule 7).</summary>
    public sealed class AdsConfig
    {
        public int MinLevelCompletions { get; }
        public int MinGapSeconds { get; }
        public int MaxPerDay { get; }

        public AdsConfig(int minLevelCompletions, int minGapSeconds, int maxPerDay)
        {
            MinLevelCompletions = minLevelCompletions;
            MinGapSeconds = minGapSeconds;
            MaxPerDay = maxPerDay;
        }
    }

    /// <summary>Persistent interstitial cap state (saved); pure value.</summary>
    public readonly struct InterstitialCapState
    {
        public long LastShownUnixSeconds { get; }
        public long Day { get; }
        public int ShownToday { get; }

        public InterstitialCapState(long lastShownUnixSeconds, long day, int shownToday)
        {
            LastShownUnixSeconds = lastShownUnixSeconds;
            Day = day;
            ShownToday = shownToday;
        }

        public static readonly InterstitialCapState Empty = new InterstitialCapState(0, -1, 0);
    }

    /// <summary>
    /// GDD 6 interstitial rules as one pure, exhaustively-tested predicate:
    /// none before level 8 · min 150s gap · max 6/day · never after a daily ·
    /// Remove Ads kills interstitials only.
    /// </summary>
    public static class InterstitialCaps
    {
        public static bool CanShow(AdsConfig config, InterstitialCapState state, long nowUnixSeconds,
            long todayEpochDay, int voyageLevelsCompleted, bool removeAdsOwned, bool afterDaily)
        {
            if (removeAdsOwned)
            {
                return false; // GDD 6: the IAP kills interstitials (rewarded stays alive)
            }

            if (afterDaily)
            {
                return false; // GDD 6: never after a daily — protect the ritual
            }

            if (voyageLevelsCompleted < config.MinLevelCompletions)
            {
                return false; // GDD 6: none before level 8
            }

            if (state.LastShownUnixSeconds > 0 && nowUnixSeconds - state.LastShownUnixSeconds < config.MinGapSeconds)
            {
                return false; // GDD 6: min 150s between interstitials
            }

            int shownToday = state.Day == todayEpochDay ? state.ShownToday : 0;
            return shownToday < config.MaxPerDay; // GDD 6: max 6/day
        }

        public static InterstitialCapState RecordShown(InterstitialCapState state, long nowUnixSeconds, long todayEpochDay)
        {
            int shownToday = state.Day == todayEpochDay ? state.ShownToday + 1 : 1;
            return new InterstitialCapState(nowUnixSeconds, todayEpochDay, shownToday);
        }
    }

    /// <summary>GDD 6 rewarded placements; names feed analytics parameters.</summary>
    public enum RewardedPlacementId
    {
        DailyRetry,
        FreeDrainPump,
        FreeNewTide,
        CoinChest,
        DoubleCoins,
        ContinueRun,
    }

    /// <summary>
    /// The payout latch (§7B "each pays exactly once" — the Star Ladder audit
    /// regression): one grant per show, no matter how many times — or how late —
    /// the SDK fires its reward callback.
    /// </summary>
    public sealed class RewardedGate
    {
        public bool Showing { get; private set; }

        public bool PaidThisShow { get; private set; }

        /// <summary>False when a show is already in flight.</summary>
        public bool BeginShow()
        {
            if (Showing)
            {
                return false;
            }

            Showing = true;
            PaidThisShow = false;
            return true;
        }

        /// <summary>True exactly once between BeginShow and EndShow.</summary>
        public bool TryGrantPayout()
        {
            if (!Showing || PaidThisShow)
            {
                return false;
            }

            PaidThisShow = true;
            return true;
        }

        public void EndShow()
        {
            Showing = false;
        }
    }
}
