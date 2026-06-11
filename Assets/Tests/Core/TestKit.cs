using System;
using System.Collections.Generic;
using Riptide.Core;

namespace Riptide.Core.Tests
{
    /// <summary>
    /// Fixture builders for the GDD rule suite. The canonical numbers (scoring,
    /// intervals) mirror GDD 10 / 2.2 text and bind to economy.json in Phase 2C —
    /// until then these fixtures are the reference values, never Core defaults.
    /// </summary>
    public static class TestKit
    {
        public static ScoringConfig GddScoring(bool awardTideSurvival = false) => new ScoringConfig(
            pointsPerCell: 1,
            rowClearBase: 80,
            comboStartHalves: 2,
            comboStepHalves: 1,
            comboCapHalves: 5,
            rescuePoints: 250,
            creatureLossPenalty: 250,
            tideSurvivalBase: 30,
            tideSurvivalStep: 5,
            awardTideSurvival: awardTideSurvival);

        public static LevelConfig Config(
            int startWater = 1,
            int? minWater = null,
            int tideInterval = 8,
            int spawnEveryTrays = 0,
            int speciesCount = 8,
            int colorCount = 6,
            GoalSet? goals = null,
            bool awardTideSurvival = false,
            IReadOnlyList<PresetCell>? preset = null)
        {
            return new LevelConfig(
                startWater,
                minWater ?? startWater,
                tideInterval,
                spawnEveryTrays,
                speciesCount,
                colorCount,
                GddScoring(awardTideSurvival),
                goals ?? GoalSet.None,
                preset);
        }

        /// <summary>
        /// Paints a board from 12 strings, TOP row (row 11) first. Chars:
        /// '.' Empty · 'b' Block(0) · '0'-'5' Block(n) · '#' Coral · 'c'/'d'/'e' Creature(0/1/2).
        /// </summary>
        public static Cell[] Paint(params string[] rowsTopDown)
        {
            if (rowsTopDown.Length != BoardSpec.Height)
            {
                throw new ArgumentException($"Need {BoardSpec.Height} rows, got {rowsTopDown.Length}.");
            }

            var cells = new Cell[BoardSpec.CellCount];
            for (int line = 0; line < rowsTopDown.Length; line++)
            {
                string text = rowsTopDown[line];
                if (text.Length != BoardSpec.Width)
                {
                    throw new ArgumentException($"Row '{text}' must be {BoardSpec.Width} chars.");
                }

                int row = BoardSpec.Height - 1 - line;
                for (int col = 0; col < BoardSpec.Width; col++)
                {
                    cells[BoardSpec.IndexOf(col, row)] = ParseCell(text[col]);
                }
            }

            return cells;
        }

        private static Cell ParseCell(char c) => c switch
        {
            '.' => Cell.Empty,
            'b' => Cell.Block(0),
            >= '0' and <= '5' => Cell.Block((byte)(c - '0')),
            '#' => Cell.Coral,
            'c' => Cell.Creature(0),
            'd' => Cell.Creature(1),
            'e' => Cell.Creature(2),
            _ => throw new ArgumentException($"Unknown cell char '{c}'."),
        };

        public static Cell[] EmptyBoard() => new Cell[BoardSpec.CellCount];

        /// <summary>Fills a row with Block(0) except at the given hole columns.</summary>
        public static void FillRow(Cell[] cells, int row, params int[] holes)
        {
            for (int col = 0; col < BoardSpec.Width; col++)
            {
                bool isHole = false;
                for (int i = 0; i < holes.Length; i++)
                {
                    if (holes[i] == col)
                    {
                        isHole = true;
                        break;
                    }
                }

                cells[BoardSpec.IndexOf(col, row)] = isHole ? Cell.Empty : Cell.Block(0);
            }
        }

        public static TrayPiece?[] Tray(params PieceId[] pieces)
        {
            if (pieces.Length > BoardSpec.TraySize)
            {
                throw new ArgumentException("Too many tray pieces.");
            }

            var tray = new TrayPiece?[BoardSpec.TraySize];
            for (int i = 0; i < pieces.Length; i++)
            {
                tray[i] = new TrayPiece(pieces[i], 0);
            }

            return tray;
        }

        public static GameState Build(
            LevelConfig? config = null,
            Cell[]? cells = null,
            TrayPiece?[]? tray = null,
            int? water = null,
            int tide = 0,
            long score = 0,
            int comboChain = 0,
            int rescueStreak = 0,
            int moveCount = 0,
            int traysDealt = 1,
            ulong rngSeed = 42,
            GoalState? goals = null,
            GameStatus status = GameStatus.InProgress)
        {
            LevelConfig cfg = config ?? Config();
            return GameState.Restore(
                cfg,
                cells ?? EmptyBoard(),
                tray ?? Tray(PieceId.Mono1, PieceId.Mono1, PieceId.Mono1),
                water ?? cfg.StartWaterLevel,
                tide,
                score,
                comboChain,
                rescueStreak,
                moveCount,
                traysDealt,
                DeterministicRng.FromSeed(rngSeed),
                goals ?? new GoalState(cfg.Goals, 0, 0, 0),
                status);
        }

        public static MoveResult Place(GameState state, int slot, int col, int row) =>
            SimEngine.ApplyMove(state, new PlaceMove(slot, new GridPos(col, row)));
    }
}
