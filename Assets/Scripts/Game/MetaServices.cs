using System;
using Riptide.Core;

namespace Riptide.Game
{
    /// <summary>
    /// All meta state behind one API, backed by the versioned save file (Phase 6;
    /// the Phase 5 PlayerPrefs shim is imported once by SaveStore). Also the
    /// single source of "today" — injectable so flow tests can travel in time.
    /// </summary>
    public sealed class MetaServices
    {
        private readonly SaveStore store;
        private CoinWallet wallet;

        /// <summary>Tests override this; production uses the device clock.</summary>
        public Func<long> TodayEpochDay = () =>
        {
            DateTime now = DateTime.Now;
            return CivilDate.ToEpochDays(now.Year, now.Month, now.Day);
        };

        public VoyageProgress Voyage { get; private set; } = new VoyageProgress();

        public MetaServices(SaveStore? saveStore = null)
        {
            store = saveStore ?? new SaveStore();
            wallet = new CoinWallet(0);
        }

        public SaveData Save => store.Data;

        public bool RecoveredFromCorruption => store.RecoveredFromCorruption;

        public long Coins => wallet.Balance;

        public StreakState Streak => store.Data.Streak;

        public long EndlessBest => store.Data.EndlessBest;

        public void Load()
        {
            store.Load();
            Voyage = VoyageProgress.Deserialize(store.Data.VoyageProgress);
            wallet = new CoinWallet(Math.Max(0, store.Data.Coins));
        }

        public void SaveNow()
        {
            store.Data.VoyageProgress = Voyage.Serialize();
            store.Data.Coins = Coins;
            store.Save();
        }

        // ---------------- coins (GDD 5.2) ----------------

        public void EarnCoins(int amount)
        {
            if (amount > 0)
            {
                wallet.Earn(amount);
            }
        }

        public bool CanAfford(int cost) => wallet.CanAfford(cost);

        public bool TrySpendCoins(int cost) => wallet.TrySpend(cost);

        // ---------------- voyage ----------------

        public void RecordLevelResult(string levelId, int stars)
        {
            Voyage.Record(levelId, stars);
            SaveNow();
        }

        // ---------------- endless ----------------

        public bool RecordEndlessScore(long score)
        {
            if (score <= store.Data.EndlessBest)
            {
                return false;
            }

            store.Data.EndlessBest = score;
            SaveNow();
            return true;
        }

        // ---------------- daily (GDD 3.3) ----------------

        public bool CanAttemptDailyToday() => store.Data.DailyAttemptDay != TodayEpochDay();

        public bool DailyRetryAvailable() =>
            store.Data.DailyAttemptDay == TodayEpochDay() && !store.Data.DailyRetryUsed;

        public void RecordDailyAttempt()
        {
            store.Data.DailyAttemptDay = TodayEpochDay();
            store.Data.DailyRetryUsed = false;
            SaveNow();
        }

        public void ConsumeDailyRetry()
        {
            store.Data.DailyRetryUsed = true;
            SaveNow();
        }

        public int RecordDailyCompletion(CoinsConfig coins)
        {
            store.Data.Streak = StreakLogic.CompleteDaily(store.Data.Streak, TodayEpochDay());
            // UI-spec §4.5 ruling (DECISIONS 2026-06-11, delegated): retries exist
            // to rescue a FAILED daily — a win consumes the retry hook, so the
            // shared result stays the day's one honest outcome.
            store.Data.DailyRetryUsed = true;
            SaveNow();
            return CoinRules.StreakMilestoneAward(coins, store.Data.Streak.Current);
        }

        public bool CanBuyStreakFreeze() => StreakLogic.CanAcquireFreeze(store.Data.Streak, TodayEpochDay());

        public bool TryBuyStreakFreeze(int cost)
        {
            if (!CanBuyStreakFreeze() || !wallet.TrySpend(cost))
            {
                return false;
            }

            store.Data.Streak = StreakLogic.AcquireFreeze(store.Data.Streak, TodayEpochDay());
            SaveNow();
            return true;
        }

        // ---------------- tidepool (GDD 5.1) ----------------

        public void RecordRescues(System.Collections.Generic.IReadOnlyList<CreatureEvent> rescued, int speciesCount)
        {
            foreach (CreatureEvent rescue in rescued)
            {
                store.Data.RecordRescue(rescue.CreatureId, speciesCount);
            }
        }

        public bool OwnsDecoration(string id) => store.Data.DecorationsOwned.Contains(id);

        /// <summary>UI spec §4.6 Tidepool placement (save v3) — persists immediately.</summary>
        public string DecorationAt(int slot) => store.Data.DecorationAt(slot);

        public bool TryPlaceDecoration(int slot, string decorationId)
        {
            if (!store.Data.TryPlaceDecoration(slot, decorationId))
            {
                return false;
            }

            SaveNow();
            return true;
        }

        public void ClearDecorationSlot(int slot)
        {
            store.Data.ClearDecorationSlot(slot);
            SaveNow();
        }

        public bool TryBuyDecoration(Decoration decoration)
        {
            if (OwnsDecoration(decoration.Id) || !wallet.TrySpend(decoration.Cost))
            {
                return false;
            }

            store.Data.DecorationsOwned.Add(decoration.Id);
            SaveNow();
            return true;
        }

        // ---------------- rewarded chest cap (GDD 5.2; the ad arrives in Phase 7) ----------------

        public bool TryClaimChest(CoinsConfig coins)
        {
            if (!store.Data.TryClaimChest(TodayEpochDay(), coins.RewardedChestCapPerDay))
            {
                return false;
            }

            wallet.Earn(coins.RewardedChest);
            SaveNow();
            return true;
        }
    }
}
