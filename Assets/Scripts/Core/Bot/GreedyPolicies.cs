using System;

namespace Riptide.Core
{
    /// <summary>GDD 4: maximizes immediate rows cleared; scan order breaks ties. Ignores its RNG.</summary>
    public sealed class GreedyClearPolicy : IBotPolicy
    {
        private readonly PeekEvaluator peek = new PeekEvaluator();

        public string Name => "GreedyClear";

        public BotDecision Choose(GameState state, DeterministicRng rng)
        {
            int bestSlot = -1;
            int bestCol = 0;
            int bestRow = 0;
            int bestCleared = -1;
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
                        if (!PlacementValidator.CanPlace(state, piece.Value.Piece, pos))
                        {
                            continue;
                        }

                        MovePeek result = peek.Evaluate(state, piece.Value.Piece, pos);
                        if (result.RowsCleared > bestCleared)
                        {
                            bestCleared = result.RowsCleared;
                            bestSlot = slot;
                            bestCol = col;
                            bestRow = row;
                        }
                    }
                }
            }

            return bestSlot < 0
                ? new BotDecision(null, rng)
                : new BotDecision(new PlaceMove(bestSlot, new GridPos(bestCol, bestRow)), rng);
        }
    }

    /// <summary>
    /// GDD 4: weighted heuristic over clears, water headroom, board flatness and
    /// rescue features. All weights come from economy.json (rule 7). Deterministic;
    /// ignores its RNG.
    /// </summary>
    public sealed class GreedyHeuristicPolicy : IBotPolicy
    {
        private readonly GreedyHeuristicWeights weights;
        private readonly PeekEvaluator peek = new PeekEvaluator();

        public GreedyHeuristicPolicy(GreedyHeuristicWeights weights)
        {
            this.weights = weights ?? throw new ArgumentNullException(nameof(weights));
        }

        public string Name => "GreedyHeuristic";

        public BotDecision Choose(GameState state, DeterministicRng rng)
        {
            int bestSlot = -1;
            int bestCol = 0;
            int bestRow = 0;
            long bestScore = long.MinValue;
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
                        if (!PlacementValidator.CanPlace(state, piece.Value.Piece, pos))
                        {
                            continue;
                        }

                        long score = Score(peek.Evaluate(state, piece.Value.Piece, pos));
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestSlot = slot;
                            bestCol = col;
                            bestRow = row;
                        }
                    }
                }
            }

            return bestSlot < 0
                ? new BotDecision(null, rng)
                : new BotDecision(new PlaceMove(bestSlot, new GridPos(bestCol, bestRow)), rng);
        }

        private long Score(MovePeek peekResult)
        {
            if (peekResult.Won)
            {
                return long.MaxValue;
            }

            long score = 0;
            score += (long)peekResult.RowsCleared * weights.Clears;
            score += (long)peekResult.Rescues * weights.Rescues;
            score += (long)(BoardSpec.DrownWaterLevel - peekResult.WaterAfter) * weights.WaterHeadroom;
            score -= (long)peekResult.Bumpiness * weights.Bumpiness;
            score -= (long)peekResult.CreaturesInDanger * weights.CreatureDanger;
            score += (long)peekResult.AlmostFullRows * weights.AlmostFullRows;
            if (peekResult.Fatal)
            {
                score -= weights.GameOverPenalty;
            }

            return score;
        }
    }
}
