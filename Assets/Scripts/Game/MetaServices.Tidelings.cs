using Riptide.Core;

namespace Riptide.Game
{
    /// <summary>GDD §16 Tidelings persistence beyond the PalSave itself: the daily pet gift.</summary>
    public sealed partial class MetaServices
    {
        /// <summary>Pays today's pet gift (strictly-greater day guard, audit B5); returns the coins paid, 0 when unavailable.</summary>
        public int TryClaimPalGift(int coins)
        {
            SaveData d = store.Data;
            long today = TodayEpochDay();
            if (coins <= 0 || today <= d.PetGiftDay)
            {
                return 0;
            }

            d.PetGiftDay = today;
            wallet.Earn(coins);
            SaveNow();
            return coins;
        }
    }
}
