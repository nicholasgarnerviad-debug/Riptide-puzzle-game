using System;

namespace Riptide.Core
{
    /// <summary>GDD 5.2 coin sources as pure functions of economy.json data. Wallet wiring is Phase 6.</summary>
    public static class CoinRules
    {
        /// <summary>"Level complete 20–60 (by band/stars)" — base + perBand + perStar (DECISIONS.md).</summary>
        public static int LevelCompleteAward(CoinsConfig coins, int zone, int stars)
        {
            if (zone < 1 || zone > 10) throw new ArgumentOutOfRangeException(nameof(zone));
            if (stars < 1 || stars > 3) throw new ArgumentOutOfRangeException(nameof(stars));
            return coins.LevelCompleteBase + coins.LevelCompletePerBand * (zone - 1)
                + coins.LevelCompletePerStar * (stars - 1);
        }

        /// <summary>
        /// Endless milestones (ROADMAP ruling 2026-06-11): every N tides survived
        /// banks a fixed award, summed and paid at run end through the outcome.
        /// </summary>
        public static int EndlessMilestoneAward(CoinsConfig coins, int tidesSurvived)
        {
            if (tidesSurvived < 0) throw new ArgumentOutOfRangeException(nameof(tidesSurvived));
            if (coins.EndlessMilestoneEvery <= 0)
            {
                return 0;
            }

            return (tidesSurvived / coins.EndlessMilestoneEvery) * coins.EndlessMilestoneCoins;
        }

        /// <summary>GDD 5.2: streak milestone payouts (7/30/100 days); 0 when the streak is no milestone.</summary>
        public static int StreakMilestoneAward(CoinsConfig coins, int streak)
        {
            foreach ((int days, int award) in coins.StreakMilestones)
            {
                if (days == streak)
                {
                    return award;
                }
            }

            return 0;
        }
    }
}
