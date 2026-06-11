using System;
using Riptide.Core;

namespace Riptide.Game
{
    /// <summary>
    /// GDD 8.2: single store + dispatch on the Unity side. State only ever changes
    /// through SimEngine.ApplyMove; views subscribe and render from state/events.
    /// </summary>
    public sealed class GameStore
    {
        public GameState State { get; private set; }

        /// <summary>Game start or full reset — views rebuild from scratch.</summary>
        public event Action<GameState>? GameReset;

        /// <summary>A move resolved — views animate from the MoveEvents, then render the new state.</summary>
        public event Action<Move, MoveResult>? MoveApplied;

        public GameStore(LevelConfig config, ulong seed)
        {
            State = GameState.NewGame(config, seed);
        }

        public void Reset(LevelConfig config, ulong seed)
        {
            State = GameState.NewGame(config, seed);
            GameReset?.Invoke(State);
        }

        /// <summary>Dispatches a move; returns false when the game is already over.</summary>
        public bool TryDispatch(Move move)
        {
            if (move == null) throw new ArgumentNullException(nameof(move));
            if (State.Status.IsTerminal())
            {
                return false;
            }

            MoveResult result = SimEngine.ApplyMove(State, move);
            State = result.Next;
            MoveApplied?.Invoke(move, result);
            return true;
        }
    }
}
