using System;

namespace Riptide.Core
{
    /// <summary>
    /// GDD 5.2 single soft currency. Mutating container (meta state, not sim state);
    /// the balance can never go negative — every sink checks affordability first.
    /// </summary>
    public sealed class CoinWallet
    {
        public long Balance { get; private set; }

        public CoinWallet(long balance = 0)
        {
            if (balance < 0) throw new ArgumentOutOfRangeException(nameof(balance));
            Balance = balance;
        }

        public void Earn(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Balance += amount;
        }

        public bool CanAfford(int cost) => cost >= 0 && Balance >= cost;

        public bool TrySpend(int cost)
        {
            if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost));
            if (Balance < cost)
            {
                return false;
            }

            Balance -= cost;
            return true;
        }
    }
}
