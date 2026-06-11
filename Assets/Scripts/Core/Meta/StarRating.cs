using System;

namespace Riptide.Core
{
    /// <summary>
    /// GDD 3.1: 3★ = goal met within par moves, 2★ = within par ×1.4 (ceil,
    /// player-generous — DECISIONS.md), 1★ = goal met. Stars gate nothing in v1.
    /// </summary>
    public static class StarRating
    {
        public static int For(int movesUsed, int parMoves)
        {
            if (movesUsed < 1) throw new ArgumentOutOfRangeException(nameof(movesUsed));
            if (parMoves < 1) throw new ArgumentOutOfRangeException(nameof(parMoves));

            if (movesUsed <= parMoves)
            {
                return 3;
            }

            long twoStarCeiling = (parMoves * 14L + 9) / 10; // ceil(par * 1.4) in integer math
            return movesUsed <= twoStarCeiling ? 2 : 1;
        }
    }
}
