namespace Riptide.Core
{
    /// <summary>
    /// Contract geometry from GDD 2.1–2.3 (exact rules, not tunables — see
    /// DECISIONS.md 2026-06-11). Balance numbers never live here.
    /// </summary>
    public static class BoardSpec
    {
        /// <summary>GDD 2.1: 9 columns.</summary>
        public const int Width = 9;

        /// <summary>GDD 2.1: 12 rows; row 0 = seabed, row 11 = surface.</summary>
        public const int Height = 12;

        public const int CellCount = Width * Height;

        /// <summary>GDD 2.2: drown game-over at waterLevel >= 10.</summary>
        public const int DrownWaterLevel = 10;

        /// <summary>GDD 2.2: waterLevel range 0..12.</summary>
        public const int MaxWaterLevel = 12;

        /// <summary>GDD 2.3: 3 pieces dealt at once, all placed before refill.</summary>
        public const int TraySize = 3;

        public static int IndexOf(int col, int row) => row * Width + col;

        public static bool InBounds(int col, int row) => col >= 0 && col < Width && row >= 0 && row < Height;
    }
}
