using System;

namespace Riptide.Core
{
    /// <summary>
    /// GDD 3.3: daily seed = hash(yyyy-MM-dd + "riptide-daily-v1"). The hash is
    /// FNV-1a 64 over the ASCII bytes of the key (DECISIONS.md 2026-06-11) —
    /// platform-stable, pinned by golden tests. The date arrives as plain ints:
    /// Core never touches DateTime (GDD 8.3).
    /// </summary>
    public static class DailySeed
    {
        public const string SaltV1 = "riptide-daily-v1";

        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public static ulong For(int year, int month, int day)
        {
            if (year < 2000 || year > 2100) throw new ArgumentOutOfRangeException(nameof(year));
            if (month < 1 || month > 12) throw new ArgumentOutOfRangeException(nameof(month));
            if (day < 1 || day > 31) throw new ArgumentOutOfRangeException(nameof(day));

            string key = $"{year:D4}-{month:D2}-{day:D2}{SaltV1}";
            ulong hash = Offset;
            for (int i = 0; i < key.Length; i++)
            {
                hash = (hash ^ (byte)key[i]) * Prime;
            }

            return hash;
        }
    }
}
