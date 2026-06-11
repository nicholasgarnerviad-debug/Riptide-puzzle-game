using System;
using System.Collections.Generic;

namespace Riptide.Core
{
    /// <summary>The advanced RNG plus the three pieces of a tray deal and guarantee telemetry.</summary>
    public readonly struct TrayDeal
    {
        public DeterministicRng Rng { get; }
        public IReadOnlyList<TrayPiece> Pieces { get; }

        /// <summary>Redraw rounds consumed by the GDD 2.4 refill guarantee (0 = clean deal).</summary>
        public int RedrawRounds { get; }

        /// <summary>
        /// True when 5 redraw rounds were exhausted with nothing placeable —
        /// the deal stands and the player is legitimately stuck (GDD 2.4).
        /// </summary>
        public bool GuaranteeExhausted { get; }

        public TrayDeal(DeterministicRng rng, IReadOnlyList<TrayPiece> pieces, int redrawRounds, bool guaranteeExhausted)
        {
            Rng = rng;
            Pieces = pieces;
            RedrawRounds = redrawRounds;
            GuaranteeExhausted = guaranteeExhausted;
        }
    }

    /// <summary>
    /// GDD 2.4 dealer: weighted-bag piece selection on the GameState RNG stream.
    /// Draw order per slot is (piece, color); redraws repeat that order for the
    /// offending slots only — Phase 2 golden files pin the stream forever.
    /// With uniform weights the piece draw is bit-identical to NextInt(PieceCount).
    /// </summary>
    public static class Dealer
    {
        /// <summary>GDD 2.4: maximum deterministic redraw rounds before a deal stands.</summary>
        public const int MaxRedrawRounds = 5;

        /// <summary>Raw weighted deal, no placement guarantee (mid-tray never redraws, GDD 2.4).</summary>
        public static TrayDeal DealTray(DeterministicRng rng, LevelConfig config)
        {
            var pieces = new TrayPiece[BoardSpec.TraySize];
            for (int i = 0; i < pieces.Length; i++)
            {
                rng = DrawOne(rng, config, out pieces[i]);
            }

            return new TrayDeal(rng, pieces, 0, false);
        }

        /// <summary>
        /// GDD 2.4 refill guarantee: if none of the 3 dealt pieces fits the board,
        /// redraw every unplaceable piece, up to 5 rounds. If nothing fits after
        /// that, the deal stands — a fair loss, not a bad deal.
        /// </summary>
        public static TrayDeal DealTrayWithGuarantee(DeterministicRng rng, LevelConfig config,
            IReadOnlyList<Cell> boardCells, int waterLevel)
        {
            if (boardCells == null) throw new ArgumentNullException(nameof(boardCells));
            if (boardCells.Count != BoardSpec.CellCount)
            {
                throw new ArgumentException($"Need {BoardSpec.CellCount} cells, got {boardCells.Count}.", nameof(boardCells));
            }

            var cells = new Cell[BoardSpec.CellCount];
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = boardCells[i];
            }

            return DealTrayWithGuaranteeRaw(rng, config, cells, waterLevel);
        }

        internal static TrayDeal DealTrayWithGuaranteeRaw(DeterministicRng rng, LevelConfig config,
            Cell[] cells, int waterLevel)
        {
            var pieces = new TrayPiece[BoardSpec.TraySize];
            var placeable = new bool[BoardSpec.TraySize];
            for (int i = 0; i < pieces.Length; i++)
            {
                rng = DrawOne(rng, config, out pieces[i]);
            }

            bool anyFits = RefreshPlaceability(cells, waterLevel, pieces, placeable);
            int rounds = 0;
            while (!anyFits && rounds < MaxRedrawRounds)
            {
                rounds++;
                for (int i = 0; i < pieces.Length; i++)
                {
                    if (!placeable[i])
                    {
                        rng = DrawOne(rng, config, out pieces[i]);
                    }
                }

                anyFits = RefreshPlaceability(cells, waterLevel, pieces, placeable);
            }

            return new TrayDeal(rng, pieces, rounds, !anyFits);
        }

        private static bool RefreshPlaceability(Cell[] cells, int waterLevel, TrayPiece[] pieces, bool[] placeable)
        {
            bool any = false;
            for (int i = 0; i < pieces.Length; i++)
            {
                placeable[i] = PlacementValidator.AnyPlacementExistsRaw(cells, waterLevel, pieces[i].Piece);
                any |= placeable[i];
            }

            return any;
        }

        private static DeterministicRng DrawOne(DeterministicRng rng, LevelConfig config, out TrayPiece piece)
        {
            RngIntDraw pieceDraw = rng.NextInt(config.TotalPieceWeight);
            rng = pieceDraw.Rng;
            int[] weights = config.PieceWeightsView;
            int roll = pieceDraw.Value;
            int pieceIndex = 0;
            int accumulated = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                accumulated += weights[i];
                if (roll < accumulated)
                {
                    pieceIndex = i;
                    break;
                }
            }

            RngIntDraw colorDraw = rng.NextInt(config.DealColorCount);
            rng = colorDraw.Rng;
            piece = new TrayPiece((PieceId)pieceIndex, (byte)colorDraw.Value);
            return rng;
        }
    }
}
