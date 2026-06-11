using System;
using System.Collections.Generic;

namespace Riptide.Core
{
    /// <summary>The successor state plus the events the view layer animates from.</summary>
    public readonly struct MoveResult
    {
        public GameState Next { get; }
        public MoveEvents Events { get; }

        public MoveResult(GameState next, MoveEvents events)
        {
            Next = next;
            Events = events;
        }
    }

    /// <summary>
    /// The pure rules engine. <see cref="ApplyMove"/> implements the canonical
    /// GDD 2.6 resolution order as one function with explicit ordered steps:
    /// 1) validate &amp; commit piece cells
    /// 2) detect &amp; clear full rows, rescue creatures, apply drain, score
    /// 3) tide tick — rise, petrify, creature losses
    /// 4) drown game-over check
    /// 5) refill when the tray is empty
    /// 6) stuck game-over check
    /// Win evaluation runs after steps 2 and 4 (DECISIONS.md 2026-06-11).
    /// </summary>
    public static class SimEngine
    {
        private static readonly GridPos[] NoPos = Array.Empty<GridPos>();
        private static readonly int[] NoRows = Array.Empty<int>();
        private static readonly CreatureEvent[] NoCreatures = Array.Empty<CreatureEvent>();
        private static readonly TrayPiece[] NoTray = Array.Empty<TrayPiece>();

        /// <summary>Start a game: preset board, initial deal (tray #1), spawn cadence, initial stuck check.</summary>
        public static GameState NewGame(LevelConfig config, ulong seed)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            var cells = new Cell[BoardSpec.CellCount];
            foreach (PresetCell preset in config.Preset)
            {
                cells[preset.Pos.Index] = preset.Content;
            }

            DeterministicRng rng = DeterministicRng.FromSeed(seed);

            // GDD 2.4: the guarantee applies at every deal time, including tray #1.
            TrayDeal deal = Dealer.DealTrayWithGuaranteeRaw(rng, config, cells, config.StartWaterLevel);
            rng = deal.Rng;
            var tray = new TrayPiece?[BoardSpec.TraySize];
            for (int i = 0; i < BoardSpec.TraySize; i++)
            {
                tray[i] = deal.Pieces[i];
            }

            int traysDealt = 1;
            if (ShouldSpawn(config, traysDealt))
            {
                rng = TrySpawnCreature(cells, config.StartWaterLevel, config, rng, out _);
            }

            // GDD 2.3: stuck is checked after every refill — including the first deal.
            GameStatus status = PlacementValidator.AnyTrayPlacementExistsRaw(cells, config.StartWaterLevel, tray)
                ? GameStatus.InProgress
                : GameStatus.LostStuck;

            return GameState.CreateOwned(config, cells, tray, config.StartWaterLevel, 0, 0L, 0, 0, 0,
                traysDealt, rng, new GoalState(config.Goals, 0, 0, 0), status);
        }

        public static MoveResult ApplyMove(GameState state, Move move)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (move == null) throw new ArgumentNullException(nameof(move));
            if (state.Status.IsTerminal())
            {
                throw new InvalidMoveException($"Game is over ({state.Status}).");
            }

            return move switch
            {
                PlaceMove place => ApplyPlace(state, place),
                _ => throw new InvalidMoveException($"Unsupported move type {move.GetType().Name}."),
            };
        }

        private static MoveResult ApplyPlace(GameState s, PlaceMove move)
        {
            LevelConfig cfg = s.Config;
            ScoringConfig sc = cfg.Scoring;

            // ---------------- GDD 2.6 STEP 1: validate & commit piece cells ----------------
            TrayPiece? slotPiece = s.TrayAt(move.TraySlot);
            if (!slotPiece.HasValue)
            {
                throw new InvalidMoveException($"Tray slot {move.TraySlot} is empty (no discards, GDD 2.3).");
            }

            TrayPiece piece = slotPiece.Value;
            if (!PlacementValidator.CanPlace(s, piece.Piece, move.Target))
            {
                throw new InvalidMoveException($"{piece.Piece} cannot be placed at {move.Target}.");
            }

            Cell[] cells = s.CopyCells();
            TrayPiece?[] tray = s.CopyTray();
            tray[move.TraySlot] = null;

            IReadOnlyList<PieceCell> mask = PieceCatalog.MaskOf(piece.Piece);
            var placed = new GridPos[mask.Count];
            for (int i = 0; i < mask.Count; i++)
            {
                var pos = new GridPos(move.Target.Col + mask[i].Dx, move.Target.Row + mask[i].Dy);
                cells[pos.Index] = Cell.Block(piece.ColorId);
                placed[i] = pos;
            }

            int waterBefore = s.WaterLevel;
            int water = waterBefore;
            int moveCount = s.MoveCount + 1;
            int traysDealt = s.TraysDealt;
            int rescueStreak = s.RescueStreak;
            DeterministicRng rng = s.Rng;
            GoalState goals = s.Goals;

            int placementPoints = mask.Count * sc.PointsPerCell;
            long score = s.Score + placementPoints;

            // ---------------- GDD 2.6 STEP 2: clear full rows, rescue, drain, score ----------------
            var clearedRows = new List<int>();
            for (int row = water; row < BoardSpec.Height; row++)
            {
                bool complete = true;
                for (int col = 0; col < BoardSpec.Width; col++)
                {
                    // Coral counts as filled for nothing (GDD 2.2) — it blocks completion.
                    if (!cells[BoardSpec.IndexOf(col, row)].CountsTowardRowCompletion)
                    {
                        complete = false;
                        break;
                    }
                }

                if (complete)
                {
                    clearedRows.Add(row);
                }
            }

            var rescuedCreatures = new List<CreatureEvent>();
            int comboChain;
            int comboHalves = 0;
            int clearPoints = 0;
            int rescuePoints = 0;
            int drainAmount = 0;
            if (clearedRows.Count > 0)
            {
                // GDD 10: combo = consecutive placements that clear; multiplier in halves, capped.
                comboChain = s.ComboChain + 1;
                comboHalves = Math.Min(sc.ComboStartHalves + (comboChain - 1) * sc.ComboStepHalves, sc.ComboCapHalves);

                foreach (int row in clearedRows)
                {
                    for (int col = 0; col < BoardSpec.Width; col++)
                    {
                        int idx = BoardSpec.IndexOf(col, row);
                        if (cells[idx].Kind == CellKind.Creature)
                        {
                            // GDD 2.3: cleared creature cells = rescued.
                            rescuedCreatures.Add(new CreatureEvent(cells[idx].Id, new GridPos(col, row)));
                        }

                        // GDD 2.3: no gravity — only the cleared rows change.
                        cells[idx] = Cell.Empty;
                    }
                }

                clearPoints = sc.RowClearBase * clearedRows.Count * comboHalves / 2;
                rescuePoints = rescuedCreatures.Count * sc.RescuePoints;

                // GDD 2.2: every cleared row drains one level, floored at minWaterLevel.
                int drainedTo = Math.Max(water - clearedRows.Count, cfg.MinWaterLevel);
                drainAmount = water - drainedTo;
                water = drainedTo;

                goals = goals.AddProgress(rescuedCreatures.Count, clearedRows.Count, 0);
                rescueStreak += rescuedCreatures.Count;
                score += clearPoints + rescuePoints;
            }
            else
            {
                comboChain = 0;
            }

            // Win check after step 2 (DECISIONS.md): a clear can complete the level before the tide ticks.
            if (goals.IsSatisfied(score))
            {
                return BuildResult(cfg, cells, tray, water, s.TideCounter, score, comboChain, rescueStreak,
                    moveCount, traysDealt, rng, goals, GameStatus.Won, waterBefore,
                    placed, clearedRows, NoPos, rescuedCreatures, NoCreatures, NoCreatures,
                    drainAmount, false, NoTray,
                    placementPoints, clearPoints, rescuePoints, 0, 0, comboHalves);
            }

            // ---------------- GDD 2.6 STEP 3: tide tick (drain already resolved in step 2) ----------------
            int tideCounter = s.TideCounter + 1;
            bool tideRose = false;
            var petrified = new List<GridPos>();
            var lostCreatures = new List<CreatureEvent>();
            int penaltyPoints = 0;
            bool rescueTargetLost = false;

            // GDD 3.2: the interval may shrink as tides pass (no-op without escalation).
            int effectiveInterval = EscalationRules.EffectiveTideInterval(cfg, goals.TidesSurvived);
            if (tideCounter >= effectiveInterval)
            {
                tideRose = true;
                tideCounter = 0;
                if (water < BoardSpec.MaxWaterLevel)
                {
                    water += 1;
                    int submergedRow = water - 1;
                    for (int col = 0; col < BoardSpec.Width; col++)
                    {
                        int idx = BoardSpec.IndexOf(col, submergedRow);
                        Cell cell = cells[idx];
                        if (cell.Kind == CellKind.Block)
                        {
                            // GDD 2.2: blocks in a rising row petrify to coral, permanently.
                            cells[idx] = Cell.Coral;
                            petrified.Add(new GridPos(col, submergedRow));
                        }
                        else if (cell.Kind == CellKind.Creature)
                        {
                            // GDD 2.2: a creature in a rising row is lost.
                            lostCreatures.Add(new CreatureEvent(cell.Id, new GridPos(col, submergedRow)));
                            cells[idx] = Cell.Empty;
                        }
                    }
                }

                if (lostCreatures.Count > 0)
                {
                    rescueStreak = 0;
                    if (cfg.Goals.HasRescueGoal)
                    {
                        // GDD 2.2/2.5: losing a rescue target fails the level.
                        rescueTargetLost = true;
                    }
                    else
                    {
                        // GDD 2.5: score penalty when no rescue goal exists.
                        penaltyPoints = lostCreatures.Count * sc.CreatureLossPenalty;
                        score -= penaltyPoints;
                    }
                }
            }

            if (rescueTargetLost)
            {
                // Step-3 outcome precedes the step-4 drown check (DECISIONS.md).
                return BuildResult(cfg, cells, tray, water, tideCounter, score, comboChain, rescueStreak,
                    moveCount, traysDealt, rng, goals, GameStatus.LostCreature, waterBefore,
                    placed, clearedRows, petrified, rescuedCreatures, lostCreatures, NoCreatures,
                    drainAmount, tideRose, NoTray,
                    placementPoints, clearPoints, rescuePoints, 0, penaltyPoints, comboHalves);
            }

            // ---------------- GDD 2.6 STEP 4: drown check ----------------
            if (water >= BoardSpec.DrownWaterLevel)
            {
                // No survival credit on the drowning rise.
                return BuildResult(cfg, cells, tray, water, tideCounter, score, comboChain, rescueStreak,
                    moveCount, traysDealt, rng, goals, GameStatus.LostDrowned, waterBefore,
                    placed, clearedRows, petrified, rescuedCreatures, lostCreatures, NoCreatures,
                    drainAmount, tideRose, NoTray,
                    placementPoints, clearPoints, rescuePoints, 0, penaltyPoints, comboHalves);
            }

            int tideSurvivalPoints = 0;
            if (tideRose)
            {
                if (sc.AwardTideSurvival)
                {
                    // GDD 10: +base, escalating +step per tide already survived.
                    tideSurvivalPoints = sc.TideSurvivalBase + sc.TideSurvivalStep * goals.TidesSurvived;
                    score += tideSurvivalPoints;
                }

                goals = goals.AddProgress(0, 0, 1);

                // SurviveTides/Score goals can complete on the survived rise (DECISIONS.md).
                if (goals.IsSatisfied(score))
                {
                    return BuildResult(cfg, cells, tray, water, tideCounter, score, comboChain, rescueStreak,
                        moveCount, traysDealt, rng, goals, GameStatus.Won, waterBefore,
                        placed, clearedRows, petrified, rescuedCreatures, lostCreatures, NoCreatures,
                        drainAmount, true, NoTray,
                        placementPoints, clearPoints, rescuePoints, tideSurvivalPoints, penaltyPoints, comboHalves);
                }
            }

            // ---------------- GDD 2.6 STEP 5: refill when tray empty ----------------
            IReadOnlyList<TrayPiece> dealtPieces = NoTray;
            CreatureEvent? spawned = null;
            if (tray[0] == null && tray[1] == null && tray[2] == null)
            {
                // GDD 2.4: refills carry the guarantee. GDD 3.2: weights may have escalated.
                int[] effectiveWeights = EscalationRules.EffectiveWeights(cfg, moveCount, out int totalWeight);
                TrayDeal deal = Dealer.DealTrayWithGuaranteeRaw(rng, cfg, cells, water, effectiveWeights, totalWeight);
                rng = deal.Rng;
                for (int i = 0; i < BoardSpec.TraySize; i++)
                {
                    tray[i] = deal.Pieces[i];
                }

                dealtPieces = deal.Pieces;
                traysDealt += 1;

                // GDD 2.5: Endless spawns a creature every N trays, near the danger.
                if (ShouldSpawn(cfg, traysDealt))
                {
                    rng = TrySpawnCreature(cells, water, cfg, rng, out spawned);
                }
            }

            // ---------------- GDD 2.6 STEP 6: stuck check ----------------
            GameStatus status = PlacementValidator.AnyTrayPlacementExistsRaw(cells, water, tray)
                ? GameStatus.InProgress
                : GameStatus.LostStuck;

            IReadOnlyList<CreatureEvent> spawnedCreatures = spawned.HasValue
                ? new[] { spawned.Value }
                : NoCreatures;

            return BuildResult(cfg, cells, tray, water, tideCounter, score, comboChain, rescueStreak,
                moveCount, traysDealt, rng, goals, status, waterBefore,
                placed, clearedRows, petrified, rescuedCreatures, lostCreatures, spawnedCreatures,
                drainAmount, tideRose, dealtPieces,
                placementPoints, clearPoints, rescuePoints, tideSurvivalPoints, penaltyPoints, comboHalves);
        }

        private static bool ShouldSpawn(LevelConfig config, int traysDealt) =>
            config.CreatureSpawnIntervalTrays > 0 && traysDealt % config.CreatureSpawnIntervalTrays == 0;

        /// <summary>
        /// GDD 2.5: spawn a creature into a random empty cell in rows
        /// waterLevel+1 .. waterLevel+3. Skips silently when the band is full
        /// or off-board (DECISIONS.md). Draw order: cell, then species.
        /// </summary>
        private static DeterministicRng TrySpawnCreature(Cell[] cells, int waterLevel, LevelConfig config,
            DeterministicRng rng, out CreatureEvent? spawned)
        {
            spawned = null;
            int lowRow = waterLevel + 1;
            int highRow = Math.Min(waterLevel + 3, BoardSpec.Height - 1);
            if (lowRow > highRow)
            {
                return rng;
            }

            var empties = new List<GridPos>();
            for (int row = lowRow; row <= highRow; row++)
            {
                for (int col = 0; col < BoardSpec.Width; col++)
                {
                    if (cells[BoardSpec.IndexOf(col, row)].IsEmpty)
                    {
                        empties.Add(new GridPos(col, row));
                    }
                }
            }

            if (empties.Count == 0)
            {
                return rng;
            }

            RngIntDraw cellDraw = rng.NextInt(empties.Count);
            rng = cellDraw.Rng;
            RngIntDraw speciesDraw = rng.NextInt(config.CreatureSpeciesCount);
            rng = speciesDraw.Rng;

            GridPos pos = empties[cellDraw.Value];
            var creatureId = (byte)speciesDraw.Value;
            cells[pos.Index] = Cell.Creature(creatureId);
            spawned = new CreatureEvent(creatureId, pos);
            return rng;
        }

        private static MoveResult BuildResult(
            LevelConfig cfg,
            Cell[] cells,
            TrayPiece?[] tray,
            int water,
            int tideCounter,
            long score,
            int comboChain,
            int rescueStreak,
            int moveCount,
            int traysDealt,
            DeterministicRng rng,
            GoalState goals,
            GameStatus status,
            int waterBefore,
            IReadOnlyList<GridPos> placed,
            IReadOnlyList<int> clearedRows,
            IReadOnlyList<GridPos> petrified,
            IReadOnlyList<CreatureEvent> rescued,
            IReadOnlyList<CreatureEvent> lost,
            IReadOnlyList<CreatureEvent> spawnedCreatures,
            int drainAmount,
            bool tideRose,
            IReadOnlyList<TrayPiece> dealtPieces,
            int placementPoints,
            int clearPoints,
            int rescuePoints,
            int tideSurvivalPoints,
            int penaltyPoints,
            int comboHalves)
        {
            GameState next = GameState.CreateOwned(cfg, cells, tray, water, tideCounter, score, comboChain,
                rescueStreak, moveCount, traysDealt, rng, goals, status);

            var scoring = new ScoreBreakdown(placementPoints, clearPoints, rescuePoints, tideSurvivalPoints,
                penaltyPoints, comboHalves);

            var events = new MoveEvents(placed, clearedRows, petrified, rescued, lost, spawnedCreatures,
                drainAmount, tideRose, water - waterBefore, dealtPieces, scoring, status);

            return new MoveResult(next, events);
        }
    }
}
