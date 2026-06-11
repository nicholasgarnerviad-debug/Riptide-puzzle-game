using System;
using System.Collections.Generic;

namespace Riptide.Core
{
    /// <summary>
    /// Immutable game state per GDD 8.2: board cells, tray, waterLevel, tideCounter,
    /// score, combo, goal progress, rngState, moveCount, status. Arrays are owned and
    /// never mutated after construction; SimEngine builds successors via copies.
    /// </summary>
    public sealed class GameState
    {
        private readonly Cell[] cells;
        private readonly TrayPiece?[] tray;

        public LevelConfig Config { get; }
        public int WaterLevel { get; }
        public int TideCounter { get; }
        public long Score { get; }

        /// <summary>Consecutive clearing placements (GDD 10 combo); 0 after a non-clearing move.</summary>
        public int ComboChain { get; }

        /// <summary>GDD 2.5: rescue streak, reset when a creature is lost. No scoring effect in v1.</summary>
        public int RescueStreak { get; }

        public int MoveCount { get; }
        public int TraysDealt { get; }
        public DeterministicRng Rng { get; }
        public GoalState Goals { get; }
        public GameStatus Status { get; }

        private GameState(
            LevelConfig config,
            Cell[] cells,
            TrayPiece?[] tray,
            int waterLevel,
            int tideCounter,
            long score,
            int comboChain,
            int rescueStreak,
            int moveCount,
            int traysDealt,
            DeterministicRng rng,
            GoalState goals,
            GameStatus status)
        {
            Config = config;
            this.cells = cells;
            this.tray = tray;
            WaterLevel = waterLevel;
            TideCounter = tideCounter;
            Score = score;
            ComboChain = comboChain;
            RescueStreak = rescueStreak;
            MoveCount = moveCount;
            TraysDealt = traysDealt;
            Rng = rng;
            Goals = goals;
            Status = status;
        }

        /// <summary>Engine-only constructor: takes ownership of the arrays, trusts invariants.</summary>
        internal static GameState CreateOwned(
            LevelConfig config,
            Cell[] cells,
            TrayPiece?[] tray,
            int waterLevel,
            int tideCounter,
            long score,
            int comboChain,
            int rescueStreak,
            int moveCount,
            int traysDealt,
            DeterministicRng rng,
            GoalState goals,
            GameStatus status)
        {
            return new GameState(config, cells, tray, waterLevel, tideCounter, score, comboChain,
                rescueStreak, moveCount, traysDealt, rng, goals, status);
        }

        /// <summary>GDD 2.6 step 0: start a new game (preset, initial deal, initial stuck check).</summary>
        public static GameState NewGame(LevelConfig config, ulong seed) => SimEngine.NewGame(config, seed);

        /// <summary>
        /// Rebuilds a state from raw values, validating every GDD invariant. Used by
        /// tests now and by save-load (Phase 6) later. Arrays are copied.
        /// </summary>
        public static GameState Restore(
            LevelConfig config,
            IReadOnlyList<Cell> cells,
            IReadOnlyList<TrayPiece?> tray,
            int waterLevel,
            int tideCounter,
            long score,
            int comboChain,
            int rescueStreak,
            int moveCount,
            int traysDealt,
            DeterministicRng rng,
            GoalState goals,
            GameStatus status)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            if (tray == null) throw new ArgumentNullException(nameof(tray));
            if (goals == null) throw new ArgumentNullException(nameof(goals));
            if (cells.Count != BoardSpec.CellCount)
            {
                throw new ArgumentException($"Expected {BoardSpec.CellCount} cells, got {cells.Count}.", nameof(cells));
            }

            if (tray.Count != BoardSpec.TraySize)
            {
                throw new ArgumentException($"Expected tray of {BoardSpec.TraySize}, got {tray.Count}.", nameof(tray));
            }

            if (waterLevel < 0 || waterLevel > BoardSpec.MaxWaterLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(waterLevel));
            }

            if (tideCounter < 0 || tideCounter >= config.TideInterval)
            {
                throw new ArgumentOutOfRangeException(nameof(tideCounter), "tideCounter must be in [0, tideInterval).");
            }

            if (comboChain < 0) throw new ArgumentOutOfRangeException(nameof(comboChain));
            if (rescueStreak < 0) throw new ArgumentOutOfRangeException(nameof(rescueStreak));
            if (moveCount < 0) throw new ArgumentOutOfRangeException(nameof(moveCount));
            if (traysDealt < 0) throw new ArgumentOutOfRangeException(nameof(traysDealt));

            var cellCopy = new Cell[BoardSpec.CellCount];
            for (int i = 0; i < cellCopy.Length; i++)
            {
                cellCopy[i] = cells[i];
            }

            // GDD 2.2 invariant: submerged rows hold only Empty or Coral.
            for (int row = 0; row < waterLevel; row++)
            {
                for (int col = 0; col < BoardSpec.Width; col++)
                {
                    Cell cell = cellCopy[BoardSpec.IndexOf(col, row)];
                    if (cell.Kind == CellKind.Block || cell.Kind == CellKind.Creature)
                    {
                        throw new ArgumentException($"Live {cell.Kind} at ({col},{row}) below waterLevel {waterLevel} violates GDD 2.2.", nameof(cells));
                    }
                }
            }

            var trayCopy = new TrayPiece?[BoardSpec.TraySize];
            for (int i = 0; i < trayCopy.Length; i++)
            {
                trayCopy[i] = tray[i];
            }

            return new GameState(config, cellCopy, trayCopy, waterLevel, tideCounter, score, comboChain,
                rescueStreak, moveCount, traysDealt, rng, goals, status);
        }

        public Cell CellAt(int col, int row)
        {
            if (!BoardSpec.InBounds(col, row))
            {
                throw new ArgumentOutOfRangeException(nameof(col), $"({col},{row}) is out of bounds.");
            }

            return cells[BoardSpec.IndexOf(col, row)];
        }

        public TrayPiece? TrayAt(int slot)
        {
            if (slot < 0 || slot >= BoardSpec.TraySize)
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }

            return tray[slot];
        }

        public int TrayPieceCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < tray.Length; i++)
                {
                    if (tray[i].HasValue) count++;
                }

                return count;
            }
        }

        /// <summary>GDD 2.2: water occupies rows 0..waterLevel-1 contiguously; derived, never stored.</summary>
        public bool IsSubmergedRow(int row) => row < WaterLevel;

        public ulong ComputeHash() => StateHash.Compute(this);

        internal Cell[] CellsView => cells;
        internal TrayPiece?[] TrayView => tray;

        internal Cell[] CopyCells()
        {
            var copy = new Cell[cells.Length];
            Array.Copy(cells, copy, cells.Length);
            return copy;
        }

        internal TrayPiece?[] CopyTray()
        {
            var copy = new TrayPiece?[tray.Length];
            Array.Copy(tray, copy, tray.Length);
            return copy;
        }
    }
}
