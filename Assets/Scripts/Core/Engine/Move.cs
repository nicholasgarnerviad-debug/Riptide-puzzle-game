using System;

namespace Riptide.Core
{
    /// <summary>
    /// A recorded player action. A full game is (levelDef, seed, List&lt;Move&gt;)
    /// per GDD 8.2; booster moves join in Phase 6.
    /// </summary>
    public abstract class Move
    {
        private protected Move()
        {
        }
    }

    /// <summary>Place the piece in <paramref name="TraySlot"/> with its mask anchor at Target.</summary>
    public sealed class PlaceMove : Move
    {
        public int TraySlot { get; }
        public GridPos Target { get; }

        public PlaceMove(int traySlot, GridPos target)
        {
            if (traySlot < 0 || traySlot >= BoardSpec.TraySize)
            {
                throw new ArgumentOutOfRangeException(nameof(traySlot));
            }

            TraySlot = traySlot;
            Target = target;
        }

        public override string ToString() => $"Place[slot {TraySlot} @ {Target}]";
    }

    /// <summary>Thrown when a move is illegal in the given state. Replays must never see this.</summary>
    public sealed class InvalidMoveException : InvalidOperationException
    {
        public InvalidMoveException(string message)
            : base(message)
        {
        }
    }
}
