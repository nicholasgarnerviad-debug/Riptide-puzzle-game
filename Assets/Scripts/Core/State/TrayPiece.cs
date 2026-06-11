using System;

namespace Riptide.Core
{
    /// <summary>A dealt piece: shape plus its cosmetic color (rolled at deal time).</summary>
    public readonly struct TrayPiece : IEquatable<TrayPiece>
    {
        public PieceId Piece { get; }
        public byte ColorId { get; }

        public TrayPiece(PieceId piece, byte colorId)
        {
            Piece = piece;
            ColorId = colorId;
        }

        public bool Equals(TrayPiece other) => Piece == other.Piece && ColorId == other.ColorId;

        public override bool Equals(object? obj) => obj is TrayPiece other && Equals(other);

        public override int GetHashCode() => ((int)Piece << 8) | ColorId;

        public override string ToString() => $"{Piece}/c{ColorId}";
    }
}
