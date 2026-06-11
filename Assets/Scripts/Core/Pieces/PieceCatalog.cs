using System;
using System.Collections.Generic;

namespace Riptide.Core
{
    /// <summary>
    /// The fixed piece set enumerated by GDD 2.3 (20 masks — see the flagged
    /// DECISIONS.md entry on the "18 shapes" headline). No player rotation:
    /// each id is one fixed mask.
    /// </summary>
    public enum PieceId : byte
    {
        Mono1 = 0,
        DominoH = 1,
        DominoV = 2,
        I3H = 3,
        I3V = 4,
        L3MissingNE = 5,
        L3MissingNW = 6,
        L3MissingSE = 7,
        L3MissingSW = 8,
        O4 = 9,
        I4H = 10,
        I4V = 11,
        S4 = 12,
        Z4 = 13,
        L4 = 14,
        J4 = 15,
        T4 = 16,
        Sq3 = 17,
        I5H = 18,
        I5V = 19,
    }

    /// <summary>One mask cell as an offset from the piece anchor (dy grows upward, like rows).</summary>
    public readonly struct PieceCell
    {
        public sbyte Dx { get; }
        public sbyte Dy { get; }

        public PieceCell(int dx, int dy)
        {
            Dx = (sbyte)dx;
            Dy = (sbyte)dy;
        }
    }

    /// <summary>All masks as data (GDD 2.3 / master prompt 1A). Anchor = bottom-left of the bounding box.</summary>
    public static class PieceCatalog
    {
        public const int PieceCount = 20;

        private static readonly PieceCell[][] Masks = BuildMasks();

        public static IReadOnlyList<PieceCell> MaskOf(PieceId id)
        {
            int index = (int)id;
            if (index < 0 || index >= PieceCount)
            {
                throw new ArgumentOutOfRangeException(nameof(id));
            }

            return Masks[index];
        }

        public static int CellCountOf(PieceId id) => MaskOf(id).Count;

        private static PieceCell[][] BuildMasks()
        {
            var masks = new PieceCell[PieceCount][];
            masks[(int)PieceId.Mono1] = Mask((0, 0));
            masks[(int)PieceId.DominoH] = Mask((0, 0), (1, 0));
            masks[(int)PieceId.DominoV] = Mask((0, 0), (0, 1));
            masks[(int)PieceId.I3H] = Mask((0, 0), (1, 0), (2, 0));
            masks[(int)PieceId.I3V] = Mask((0, 0), (0, 1), (0, 2));
            masks[(int)PieceId.L3MissingNE] = Mask((0, 0), (1, 0), (0, 1));
            masks[(int)PieceId.L3MissingNW] = Mask((0, 0), (1, 0), (1, 1));
            masks[(int)PieceId.L3MissingSE] = Mask((0, 0), (0, 1), (1, 1));
            masks[(int)PieceId.L3MissingSW] = Mask((1, 0), (0, 1), (1, 1));
            masks[(int)PieceId.O4] = Mask((0, 0), (1, 0), (0, 1), (1, 1));
            masks[(int)PieceId.I4H] = Mask((0, 0), (1, 0), (2, 0), (3, 0));
            masks[(int)PieceId.I4V] = Mask((0, 0), (0, 1), (0, 2), (0, 3));
            masks[(int)PieceId.S4] = Mask((0, 0), (1, 0), (1, 1), (2, 1));
            masks[(int)PieceId.Z4] = Mask((1, 0), (2, 0), (0, 1), (1, 1));
            masks[(int)PieceId.L4] = Mask((0, 0), (1, 0), (0, 1), (0, 2));
            masks[(int)PieceId.J4] = Mask((0, 0), (1, 0), (1, 1), (1, 2));
            masks[(int)PieceId.T4] = Mask((0, 0), (1, 0), (2, 0), (1, 1));
            masks[(int)PieceId.Sq3] = Mask((0, 0), (1, 0), (2, 0), (0, 1), (1, 1), (2, 1), (0, 2), (1, 2), (2, 2));
            masks[(int)PieceId.I5H] = Mask((0, 0), (1, 0), (2, 0), (3, 0), (4, 0));
            masks[(int)PieceId.I5V] = Mask((0, 0), (0, 1), (0, 2), (0, 3), (0, 4));
            return masks;
        }

        private static PieceCell[] Mask(params (int dx, int dy)[] cells)
        {
            var result = new PieceCell[cells.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                result[i] = new PieceCell(cells[i].dx, cells[i].dy);
            }

            return result;
        }
    }
}
