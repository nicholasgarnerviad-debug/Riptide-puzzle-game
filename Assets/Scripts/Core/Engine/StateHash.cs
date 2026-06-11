namespace Riptide.Core
{
    /// <summary>
    /// Stable FNV-1a-style end-state hash over all dynamic state (GDD 8.3 determinism
    /// contract: identical (levelDef, seed, moves) must produce identical hashes,
    /// across runs and platforms — never use object hash codes here).
    /// </summary>
    public static class StateHash
    {
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public static ulong Compute(GameState state)
        {
            ulong h = Offset;

            Cell[] cells = state.CellsView;
            for (int i = 0; i < cells.Length; i++)
            {
                h = Mix(h, (ulong)cells[i].Kind | ((ulong)cells[i].Id << 8));
            }

            TrayPiece?[] tray = state.TrayView;
            for (int i = 0; i < tray.Length; i++)
            {
                h = tray[i].HasValue
                    ? Mix(h, 0x1UL | ((ulong)tray[i]!.Value.Piece << 8) | ((ulong)tray[i]!.Value.ColorId << 16))
                    : Mix(h, 0xFFUL);
            }

            h = Mix(h, (ulong)state.WaterLevel);
            h = Mix(h, (ulong)state.TideCounter);
            h = Mix(h, unchecked((ulong)state.Score));
            h = Mix(h, (ulong)state.ComboChain);
            h = Mix(h, (ulong)state.RescueStreak);
            h = Mix(h, (ulong)state.MoveCount);
            h = Mix(h, (ulong)state.TraysDealt);
            h = Mix(h, state.Rng.S0);
            h = Mix(h, state.Rng.S1);
            h = Mix(h, (ulong)state.Goals.Rescued);
            h = Mix(h, (ulong)state.Goals.RowsCleared);
            h = Mix(h, (ulong)state.Goals.TidesSurvived);
            h = Mix(h, (ulong)state.Status);
            return h;
        }

        private static ulong Mix(ulong h, ulong value) => (h ^ value) * Prime;
    }
}
