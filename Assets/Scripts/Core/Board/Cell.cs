using System;

namespace Riptide.Core
{
    /// <summary>GDD 2.1 cell states. Submersion is derived from waterLevel, not stored.</summary>
    public enum CellKind : byte
    {
        Empty = 0,
        Block = 1,
        Coral = 2,
        Creature = 3,
    }

    /// <summary>
    /// One board cell. Id carries the colorId for Block cells and the creatureId
    /// for Creature cells (GDD 2.1); zero otherwise.
    /// </summary>
    public readonly struct Cell : IEquatable<Cell>
    {
        public CellKind Kind { get; }
        public byte Id { get; }

        private Cell(CellKind kind, byte id)
        {
            Kind = kind;
            Id = id;
        }

        public static readonly Cell Empty = default;
        public static readonly Cell Coral = new Cell(CellKind.Coral, 0);

        public static Cell Block(byte colorId) => new Cell(CellKind.Block, colorId);

        public static Cell Creature(byte creatureId) => new Cell(CellKind.Creature, creatureId);

        public bool IsEmpty => Kind == CellKind.Empty;

        /// <summary>
        /// GDD 2.2/2.3: a row completes when every cell counts as filled; Block and
        /// Creature count, Coral "counts as filled for nothing" and Empty is empty.
        /// </summary>
        public bool CountsTowardRowCompletion => Kind == CellKind.Block || Kind == CellKind.Creature;

        public bool Equals(Cell other) => Kind == other.Kind && Id == other.Id;

        public override bool Equals(object? obj) => obj is Cell other && Equals(other);

        public override int GetHashCode() => ((int)Kind << 8) | Id;

        public override string ToString() => Kind switch
        {
            CellKind.Empty => ".",
            CellKind.Block => $"B{Id}",
            CellKind.Coral => "#",
            CellKind.Creature => $"C{Id}",
            _ => "?",
        };
    }

    /// <summary>A board coordinate; col 0..8, row 0 (seabed) .. 11 (surface).</summary>
    public readonly struct GridPos : IEquatable<GridPos>
    {
        public byte Col { get; }
        public byte Row { get; }

        public GridPos(int col, int row)
        {
            if (!BoardSpec.InBounds(col, row))
            {
                throw new ArgumentOutOfRangeException(nameof(col), $"({col},{row}) is outside the {BoardSpec.Width}x{BoardSpec.Height} board.");
            }

            Col = (byte)col;
            Row = (byte)row;
        }

        public int Index => BoardSpec.IndexOf(Col, Row);

        public bool Equals(GridPos other) => Col == other.Col && Row == other.Row;

        public override bool Equals(object? obj) => obj is GridPos other && Equals(other);

        public override int GetHashCode() => (Col << 8) | Row;

        public override string ToString() => $"({Col},{Row})";
    }
}
