using System.Collections.Generic;

namespace Riptide.Core
{
    /// <summary>The advanced RNG plus the three pieces of a tray deal.</summary>
    public readonly struct TrayDeal
    {
        public DeterministicRng Rng { get; }
        public IReadOnlyList<TrayPiece> Pieces { get; }

        public TrayDeal(DeterministicRng rng, IReadOnlyList<TrayPiece> pieces)
        {
            Rng = rng;
            Pieces = pieces;
        }
    }

    /// <summary>
    /// Phase 1 dealer: uniform piece + color rolls on the GameState RNG stream.
    /// Draw order per slot is (piece, color) — Phase 2 golden files pin this.
    /// Phase 2B replaces uniform selection with the GDD 2.4 weighted bag and adds
    /// the refill guarantee with deterministic redraw (master prompt 2B).
    /// </summary>
    public static class Dealer
    {
        public static TrayDeal DealTray(DeterministicRng rng, LevelConfig config)
        {
            var pieces = new TrayPiece[BoardSpec.TraySize];
            for (int i = 0; i < pieces.Length; i++)
            {
                RngIntDraw pieceDraw = rng.NextInt(PieceCatalog.PieceCount);
                rng = pieceDraw.Rng;
                RngIntDraw colorDraw = rng.NextInt(config.DealColorCount);
                rng = colorDraw.Rng;
                pieces[i] = new TrayPiece((PieceId)pieceDraw.Value, (byte)colorDraw.Value);
            }

            return new TrayDeal(rng, pieces);
        }
    }
}
