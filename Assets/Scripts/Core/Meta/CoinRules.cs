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
