using System;

namespace Riptide.Core
{
    /// <summary>A chosen move (null = no legal move exists) plus the advanced bot RNG.</summary>
    public readonly struct BotDecision
    {
        public PlaceMove? Move { get; }
        public DeterministicRng Rng { get; }

        public BotDecision(PlaceMove? move, DeterministicRng rng)
        {
            Move = move;
            Rng = rng;
        }
    }

    /// <summary>
    /// GDD 4: a headless autoplay policy. Implementations are deterministic given
    /// (state, rng); greedy policies ignore the rng entirely and tie-break by scan
    /// order (slot, then row from the waterline up, then column).
    /// </summary>
    public interface IBotPolicy
    {
        string Name { get; }

        BotDecision Choose(GameState state, DeterministicRng rng);
    }

    /// <summary>Uniform random over legal placements; the weakest baseline (GDD 4).</summary>
    public sealed class RandomLegalPolicy : IBotPolicy
    {
        public string Name => "RandomLegal";

        public BotDecision Choose(GameState state, DeterministicRng rng)
        {
            for (int attempt = 0; attempt < 64; attempt++)
            {
                RngIntDraw slotDraw = rng.NextInt(BoardSpec.TraySize);
                rng = slotDraw.Rng;
                RngIntDraw colDraw = rng.NextInt(BoardSpec.Width);
                rng = colDraw.Rng;
                RngIntDraw rowDraw = rng.NextInt(BoardSpec.Height);
                rng = rowDraw.Rng;

                TrayPiece? piece = state.TrayAt(slotDraw.Value);
                if (!piece.HasValue || rowDraw.Value < state.WaterLevel)
                {
                    continue;
                }

                var pos = new GridPos(colDraw.Value, rowDraw.Value);
                if (PlacementValidator.CanPlace(state, piece.Value.Piece, pos))
                {
                    return new BotDecision(new PlaceMove(slotDraw.Value, pos), rng);
                }
            }

            // Deterministic fallback: first legal in scan order.
            for (int slot = 0; slot < BoardSpec.TraySize; slot++)
            {
                TrayPiece? piece = state.TrayAt(slot);
                if (!piece.HasValue)
                {
                    continue;
                }

                for (int row = state.WaterLevel; row < BoardSpec.Height; row++)
                {
                    for (int col = 0; col < BoardSpec.Width; col++)
                    {
                        var pos = new GridPos(col, row);
                        if (PlacementValidator.CanPlace(state, piece.Value.Piece, pos))
                        {
                            return new BotDecision(new PlaceMove(slot, pos), rng);
                        }
                    }
                }
            }

            return new BotDecision(null, rng);
        }
    }
}
