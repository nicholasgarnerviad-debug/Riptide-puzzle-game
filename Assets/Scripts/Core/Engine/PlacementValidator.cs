using System.Collections.Generic;

namespace Riptide.Core
{
    /// <summary>
    /// GDD 2.3 placement legality: every mask cell in bounds, on an Empty cell,
    /// at or above the waterline. Also answers the stuck question (GDD 2.3/2.6 step 6).
    /// </summary>
    public static class PlacementValidator
    {
        public static bool CanPlace(GameState state, PieceId piece, GridPos target) =>
            CanPlaceRaw(state.CellsView, state.WaterLevel, piece, target.Col, target.Row);

        public static bool AnyPlacementExists(GameState state, PieceId piece) =>
            AnyPlacementExistsRaw(state.CellsView, state.WaterLevel, piece);

        /// <summary>True when at least one tray piece has at least one legal placement.</summary>
        public static bool AnyTrayPlacementExists(GameState state) =>
            AnyTrayPlacementExistsRaw(state.CellsView, state.WaterLevel, state.TrayView);

        internal static bool CanPlaceRaw(Cell[] cells, int waterLevel, PieceId piece, int anchorCol, int anchorRow)
        {
            IReadOnlyList<PieceCell> mask = PieceCatalog.MaskOf(piece);
            for (int i = 0; i < mask.Count; i++)
            {
                int col = anchorCol + mask[i].Dx;
                int row = anchorRow + mask[i].Dy;
                if (!BoardSpec.InBounds(col, row))
                {
                    return false;
                }

                // GDD 2.3: "at or above waterLevel" — rows 0..waterLevel-1 are submerged.
                if (row < waterLevel)
                {
                    return false;
                }

                if (!cells[BoardSpec.IndexOf(col, row)].IsEmpty)
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool AnyPlacementExistsRaw(Cell[] cells, int waterLevel, PieceId piece)
        {
            // Masks only extend up/right, so anchors below the waterline can never be legal.
            for (int row = waterLevel; row < BoardSpec.Height; row++)
            {
                for (int col = 0; col < BoardSpec.Width; col++)
                {
                    if (CanPlaceRaw(cells, waterLevel, piece, col, row))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        internal static bool AnyTrayPlacementExistsRaw(Cell[] cells, int waterLevel, TrayPiece?[] tray)
        {
            for (int i = 0; i < tray.Length; i++)
            {
                if (tray[i].HasValue && AnyPlacementExistsRaw(cells, waterLevel, tray[i]!.Value.Piece))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
