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

        /// <summary>Uniform weights: bit-identical RNG stream to the Phase 1 NextInt(20) deal.</summary>
        public static int[] UniformWeights()
        {
            var weights = new int[PieceCatalog.PieceCount];
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = 1;
            }

            return weights;
        }

        public static LevelConfig Config(
            int startWater = 1,
            int? minWater = null,
            int tideInterval = 8,
            int spawnEveryTrays = 0,
            int speciesCount = 8,
            int colorCount = 6,
            GoalSet? goals = null,
            bool awardTideSurvival = false,
            IReadOnlyList<PresetCell>? preset = null,
            IReadOnlyList<int>? pieceWeights = null,
            EscalationConfig? escalation = null)
        {
            return new LevelConfig(
                startWater,
                minWater ?? startWater,
                tideInterval,
                spawnEveryTrays,
                speciesCount,
                colorCount,
                pieceWeights ?? UniformWeights(),
                GddScoring(awardTideSurvival),
                goals ?? GoalSet.None,
                preset,
                escalation);
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

        /// <summary>Canonical economy fixture, structurally mirroring Assets/Content/economy.json.</summary>
        public const string CanonicalEconomyJson = @"{
  ""scoring"": {
    ""pointsPerCell"": 1, ""rowClearBase"": 80, ""comboStartHalves"": 2, ""comboStepHalves"": 1,
    ""comboCapHalves"": 5, ""rescuePoints"": 250, ""creatureLossPenalty"": 250,
    ""tideSurvivalBase"": 30, ""tideSurvivalStep"": 5
  },
  ""deal"": { ""colorCount"": 6 },
  ""pieceWeightBands"": {
    ""1"": [7,7,7,7,7,7,7,7,7,5,5,5,5,5,5,5,5,0,0,0]
  },
  ""endless"": {
    ""startWaterLevel"": 1, ""weightBand"": 1,
    ""startTideInterval"": 7, ""intervalShrinkEveryTides"": 4, ""intervalFloor"": 3,
    ""weightEscalationEveryPlacements"": 25, ""bigWeightBonusPerStep"": 2,
    ""maxEscalationSteps"": 8, ""creatureSpawnIntervalTrays"": 4
  },
  ""daily"": {
    ""surviveTides"": 20, ""weightBand"": 1, ""startWaterLevel"": 1,
    ""startTideInterval"": 7, ""intervalShrinkEveryTides"": 3, ""intervalFloor"": 2,
    ""bigWeightBonusPerStep"": 1, ""maxEscalationSteps"": 1, ""creatureSpawnIntervalTrays"": 4,
    ""epochDate"": ""2026-06-11""
  },
  ""coins"": {
    ""levelCompleteBase"": 20, ""levelCompletePerBand"": 3, ""levelCompletePerStar"": 5,
    ""dailyComplete"": 75,
    ""streakMilestones"": [
      { ""days"": 7, ""award"": 200 }, { ""days"": 30, ""award"": 750 }, { ""days"": 100, ""award"": 2000 }
    ],
    ""endlessPersonalBest"": 50, ""rewardedChest"": 50, ""rewardedChestCapPerDay"": 3,
    ""dailyRetryCost"": 100, ""streakFreezeCost"": 300
  },
  ""boosters"": { ""drainPump"": 150, ""bubblePop"": 100, ""newTide"": 120 },
  ""ads"": { ""minLevelCompletions"": 8, ""minGapSeconds"": 150, ""maxPerDay"": 6 },
  ""bot"": {
    ""greedyHeuristic"": {
      ""clears"": 100, ""rescues"": 130, ""waterHeadroom"": 8, ""bumpiness"": 2,
      ""creatureDanger"": 15, ""almostFullRows"": 6, ""gameOverPenalty"": 10000
    }
  }
}";

        public static EconomyConfig Economy() => EconomyLoader.Load(CanonicalEconomyJson, "test-economy");
    }
}
