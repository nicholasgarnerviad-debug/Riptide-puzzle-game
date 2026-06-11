using System;

namespace Riptide.Core
{
    /// <summary>Feature snapshot of one candidate placement, per GDD 4's heuristic inputs.</summary>
    public readonly struct MovePeek
    {
        public int RowsCleared { get; }
        public int Rescues { get; }
        public int WaterAfter { get; }
        public bool TideRose { get; }

        /// <summary>The move ends the game lost (drown, or rescue-target lost to the rise).</summary>
        public bool Fatal { get; }

        /// <summary>The move ends the game won (goal completion at step 2 or step 4).</summary>
        public bool Won { get; }

        public int Bumpiness { get; }
        public int AlmostFullRows { get; }
        public int CreaturesInDanger { get; }

        public MovePeek(int rowsCleared, int rescues, int waterAfter, bool tideRose, bool fatal, bool won,
            int bumpiness, int almostFullRows, int creaturesInDanger)
        {
            RowsCleared = rowsCleared;
            Rescues = rescues;
            WaterAfter = waterAfter;
            TideRose = tideRose;
            Fatal = fatal;
            Won = won;
            Bumpiness = bumpiness;
            AlmostFullRows = almostFullRows;
            CreaturesInDanger = creaturesInDanger;
        }
    }

    /// <summary>
    /// Allocation-free mirror of GDD 2.6 steps 1–4 for bot move evaluation —
    /// hundreds of candidates per move would make full ApplyMove calls the
    /// balance-runner bottleneck. PeekDriftTests pin this to the real engine;
    /// if the engine changes, the drift test fails before the bot lies.
    /// NOT thread-safe: one instance per thread (scratch buffers are reused).
    /// </summary>
    public sealed class PeekEvaluator
    {
        private readonly Cell[] scratch = new Cell[BoardSpec.CellCount];
        private readonly int[] heights = new int[BoardSpec.Width];

        public MovePeek Evaluate(GameState state, PieceId piece, GridPos target)
        {
            LevelConfig cfg = state.Config;
            Cell[] source = state.CellsView;
            Array.Copy(source, scratch, source.Length);

            // ---- step 1: commit ----
            var mask = PieceCatalog.MaskOf(piece);
            for (int i = 0; i < mask.Count; i++)
            {
                scratch[BoardSpec.IndexOf(target.Col + mask[i].Dx, target.Row + mask[i].Dy)] = Cell.Block(0);
            }

            // ---- step 2: clears, rescues, drain ----
            int water = state.WaterLevel;
            int rowsCleared = 0;
            int rescues = 0;
            for (int row = water; row < BoardSpec.Height; row++)
            {
                bool complete = true;
                for (int col = 0; col < BoardSpec.Width; col++)
                {
                    if (!scratch[BoardSpec.IndexOf(col, row)].CountsTowardRowCompletion)
                    {
                        complete = false;
                        break;
                    }
                }

                if (!complete)
                {
                    continue;
                }

                rowsCleared++;
                for (int col = 0; col < BoardSpec.Width; col++)
                {
                    int idx = BoardSpec.IndexOf(col, row);
                    if (scratch[idx].Kind == CellKind.Creature)
                    {
                        rescues++;
                    }

                    scratch[idx] = Cell.Empty;
                }
            }

            if (rowsCleared > 0)
            {
                water = Math.Max(water - rowsCleared, cfg.MinWaterLevel);
            }

            // Win check after step 2 (mirrors SimEngine + DECISIONS.md timing).
            int rescuedTotal = state.Goals.Rescued + rescues;
            int rowsTotal = state.Goals.RowsCleared + rowsCleared;
            long score = state.Score + ComputeMovePoints(cfg, state.ComboChain, mask.Count, rowsCleared, rescues);
            if (GoalsMet(cfg.Goals, rescuedTotal, rowsTotal, state.Goals.TidesSurvived, score))
            {
                return Finish(water, rowsCleared, rescues, false, false, true);
            }

            // ---- step 3: tide tick ----
            int tide = state.TideCounter + 1;
            bool rose = false;
            bool fatal = false;
            int effectiveInterval = EscalationRules.EffectiveTideInterval(cfg, state.Goals.TidesSurvived);
            if (tide >= effectiveInterval)
            {
                rose = true;
                if (water < BoardSpec.MaxWaterLevel)
                {
                    water++;
                    int submergedRow = water - 1;
                    for (int col = 0; col < BoardSpec.Width; col++)
                    {
                        int idx = BoardSpec.IndexOf(col, submergedRow);
                        if (scratch[idx].Kind == CellKind.Block)
                        {
                            scratch[idx] = Cell.Coral;
                        }
                        else if (scratch[idx].Kind == CellKind.Creature)
                        {
                            scratch[idx] = Cell.Empty;
                            if (cfg.Goals.HasRescueGoal)
                            {
                                fatal = true;
                            }
                            else
                            {
                                score -= cfg.Scoring.CreatureLossPenalty;
                            }
                        }
                    }
                }
            }

            // ---- step 4: drown, then survival credit + win ----
            if (!fatal && water >= BoardSpec.DrownWaterLevel)
            {
                fatal = true;
            }

            bool won = false;
            if (!fatal && rose)
            {
                if (cfg.Scoring.AwardTideSurvival)
                {
                    score += cfg.Scoring.TideSurvivalBase + cfg.Scoring.TideSurvivalStep * state.Goals.TidesSurvived;
                }

                won = GoalsMet(cfg.Goals, rescuedTotal, rowsTotal, state.Goals.TidesSurvived + 1, score);
            }

            return Finish(water, rowsCleared, rescues, rose, fatal, won);
        }

        private MovePeek Finish(int water, int rowsCleared, int rescues, bool rose, bool fatal, bool won)
        {
            // ---- features over the post-move board ----
            for (int col = 0; col < BoardSpec.Width; col++)
            {
                int height = 0;
                for (int row = BoardSpec.Height - 1; row >= 0; row--)
                {
                    if (!scratch[BoardSpec.IndexOf(col, row)].IsEmpty)
                    {
                        height = row + 1;
                        break;
                    }
                }

                heights[col] = height;
            }

            int bumpiness = 0;
            for (int col = 0; col < BoardSpec.Width - 1; col++)
            {
                bumpiness += Math.Abs(heights[col] - heights[col + 1]);
            }

            int almostFull = 0;
            int creaturesInDanger = 0;
            for (int row = water; row < BoardSpec.Height; row++)
            {
                int empty = 0;
                bool coral = false;
                for (int col = 0; col < BoardSpec.Width; col++)
                {
                    Cell cell = scratch[BoardSpec.IndexOf(col, row)];
                    if (cell.IsEmpty)
                    {
                        empty++;
                    }
                    else if (cell.Kind == CellKind.Coral)
                    {
                        coral = true;
                    }

                    if (cell.Kind == CellKind.Creature && row < water + 2)
                    {
                        creaturesInDanger++;
                    }
                }

                if (!coral && empty > 0 && empty <= 2)
                {
                    almostFull++;
                }
            }

            return new MovePeek(rowsCleared, rescues, water, rose, fatal, won, bumpiness, almostFull, creaturesInDanger);
        }

        private static long ComputeMovePoints(LevelConfig cfg, int comboChainBefore, int placedCells,
            int rowsCleared, int rescues)
        {
            ScoringConfig sc = cfg.Scoring;
            long points = placedCells * sc.PointsPerCell;
            if (rowsCleared > 0)
            {
                int chain = comboChainBefore + 1;
                int halves = Math.Min(sc.ComboStartHalves + (chain - 1) * sc.ComboStepHalves, sc.ComboCapHalves);
                points += sc.RowClearBase * rowsCleared * halves / 2;
                points += rescues * sc.RescuePoints;
            }

            return points;
        }

        private static bool GoalsMet(GoalSet goals, int rescued, int rowsCleared, int tidesSurvived, long score)
        {
            if (!goals.HasAny)
            {
                return false;
            }

            if (goals.RescueAllTarget.HasValue && rescued < goals.RescueAllTarget.Value) return false;
            if (goals.ClearRowsTarget.HasValue && rowsCleared < goals.ClearRowsTarget.Value) return false;
            if (goals.SurviveTidesTarget.HasValue && tidesSurvived < goals.SurviveTidesTarget.Value) return false;
            if (goals.ScoreTarget.HasValue && score < goals.ScoreTarget.Value) return false;
            return true;
        }
    }
}
