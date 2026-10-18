using System;
using System.Collections.Generic;

namespace Riptide.Core
{
    /// <summary>GDD §15.1: one power per species, in roster order.</summary>
    public enum CreaturePower : byte
    {
        Pinch = 0,   // Crab
        Wish = 1,    // Starfish
        Steady = 2,  // Seahorse
        Ink = 3,     // Octopus
        Calm = 4,    // Turtle
        Puff = 5,    // Pufferfish
        Glow = 6,    // Jellyfish
        Regrow = 7,  // Axolotl
    }

    /// <summary>GDD §15.1 power tunables — balance data from expansion.json, never C#.</summary>
    public sealed class CreaturePowersConfig
    {
        public int CrabCoral { get; }
        public int StarfishScore { get; }
        public int SeahorseTideRelief { get; }
        public int OctopusCoral { get; }
        public int TurtleDrain { get; }
        public int AxolotlDrain { get; }

        public CreaturePowersConfig(int crabCoral, int starfishScore, int seahorseTideRelief, int octopusCoral,
            int turtleDrain, int axolotlDrain)
        {
            if (crabCoral < 0 || starfishScore < 0 || seahorseTideRelief < 0 || octopusCoral < 0
                || turtleDrain < 0 || axolotlDrain < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(crabCoral), "Power tunables must be non-negative.");
            }

            CrabCoral = crabCoral;
            StarfishScore = starfishScore;
            SeahorseTideRelief = seahorseTideRelief;
            OctopusCoral = octopusCoral;
            TurtleDrain = turtleDrain;
            AxolotlDrain = axolotlDrain;
        }

        /// <summary>The power a species carries (roster order; wraps for safety).</summary>
        public static CreaturePower PowerOf(byte speciesId) => (CreaturePower)(speciesId % 8);
    }

    /// <summary>One fired power — everything the view needs to animate it (GDD 8.2).</summary>
    public readonly struct PowerEvent
    {
        public byte CreatureId { get; }
        public CreaturePower Power { get; }
        public GridPos Pos { get; }

        /// <summary>Cells the power emptied (coral or blocks), in the order removed.</summary>
        public IReadOnlyList<GridPos> ClearedCells { get; }

        /// <summary>Water change from the power (≤ 0).</summary>
        public int WaterDelta { get; }

        public int ScoreBonus { get; }
        public int TideRelief { get; }
        public bool TideReset { get; }
        public int ComboBonus { get; }

        public PowerEvent(byte creatureId, CreaturePower power, GridPos pos, IReadOnlyList<GridPos> clearedCells,
            int waterDelta, int scoreBonus, int tideRelief, bool tideReset, int comboBonus)
        {
            CreatureId = creatureId;
            Power = power;
            Pos = pos;
            ClearedCells = clearedCells ?? throw new ArgumentNullException(nameof(clearedCells));
            WaterDelta = waterDelta;
            ScoreBonus = scoreBonus;
            TideRelief = tideRelief;
            TideReset = tideReset;
            ComboBonus = comboBonus;
        }
    }

    /// <summary>
    /// GDD §15.1 power resolution. Pure and RNG-free: runs inside SimEngine step 2
    /// after the clear's drain, in rescue order, mutating the move's working state.
    /// </summary>
    internal static class CreaturePowerRules
    {
        private static readonly GridPos[] None = Array.Empty<GridPos>();

        internal struct Working
        {
            public int Water;
            public long Score;
            public int ComboChain;
            public int TideRelief;
            public bool TideReset;
            public int ScoreBonusTotal;
            public int DrainTotal;
        }

        internal static PowerEvent Apply(LevelConfig cfg, CreaturePowersConfig p, Cell[] cells, CreatureEvent rescue,
            ref Working w)
        {
            CreaturePower power = CreaturePowersConfig.PowerOf(rescue.CreatureId);
            IReadOnlyList<GridPos> cleared = None;
            int waterDelta = 0;
            int scoreBonus = 0;
            int relief = 0;
            bool reset = false;
            int combo = 0;

            switch (power)
            {
                case CreaturePower.Pinch:
                    cleared = RemoveNearestCoral(cells, w.Water, rescue.Pos, p.CrabCoral);
                    break;
                case CreaturePower.Wish:
                    scoreBonus = p.StarfishScore;
                    break;
                case CreaturePower.Steady:
                    relief = p.SeahorseTideRelief;
                    break;
                case CreaturePower.Ink:
                    cleared = RemoveLowestCoral(cells, w.Water, p.OctopusCoral);
                    break;
                case CreaturePower.Calm:
                    waterDelta = Drain(cfg, ref w, p.TurtleDrain);
                    break;
                case CreaturePower.Puff:
                    cleared = PopNeighbors(cells, w.Water, rescue.Pos);
                    break;
                case CreaturePower.Glow:
                    combo = 1;
                    break;
                case CreaturePower.Regrow:
                    waterDelta = Drain(cfg, ref w, p.AxolotlDrain);
                    reset = true;
                    break;
            }

            w.Score += scoreBonus;
            w.ScoreBonusTotal += scoreBonus;
            w.ComboChain += combo;
            w.TideRelief += relief;
            w.TideReset |= reset;
            return new PowerEvent(rescue.CreatureId, power, rescue.Pos, cleared, waterDelta, scoreBonus, relief,
                reset, combo);
        }

        private static int Drain(LevelConfig cfg, ref Working w, int amount)
        {
            int to = Math.Max(w.Water - amount, cfg.MinWaterLevel);
            int delta = to - w.Water;
            w.DrainTotal -= delta;
            w.Water = to;
            return delta;
        }

        /// <summary>Crab: up to N coral cells nearest the crab (Manhattan; ties → lower row, lower col).</summary>
        private static IReadOnlyList<GridPos> RemoveNearestCoral(Cell[] cells, int water, GridPos from, int count)
        {
            var removed = new List<GridPos>(count);
            for (int n = 0; n < count; n++)
            {
                int best = -1;
                int bestDist = int.MaxValue;
                for (int row = water; row < BoardSpec.Height; row++)
                {
                    for (int col = 0; col < BoardSpec.Width; col++)
                    {
                        int idx = BoardSpec.IndexOf(col, row);
                        if (cells[idx].Kind != CellKind.Coral)
                        {
                            continue;
                        }

                        int dist = Math.Abs(col - from.Col) + Math.Abs(row - from.Row);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            best = idx;
                        }
                    }
                }

                if (best < 0)
                {
                    break;
                }

                cells[best] = Cell.Empty;
                removed.Add(new GridPos(best % BoardSpec.Width, best / BoardSpec.Width));
            }

            return removed;
        }

        /// <summary>Octopus: up to N coral cells from the lowest dry rows upward, left → right.</summary>
        private static IReadOnlyList<GridPos> RemoveLowestCoral(Cell[] cells, int water, int count)
        {
            var removed = new List<GridPos>(count);
            for (int row = water; row < BoardSpec.Height && removed.Count < count; row++)
            {
                for (int col = 0; col < BoardSpec.Width && removed.Count < count; col++)
                {
                    int idx = BoardSpec.IndexOf(col, row);
                    if (cells[idx].Kind == CellKind.Coral)
                    {
                        cells[idx] = Cell.Empty;
                        removed.Add(new GridPos(col, row));
                    }
                }
            }

            return removed;
        }

        /// <summary>Pufferfish: blocks/coral orthogonally adjacent to its cell (dry rows only; never creatures).</summary>
        private static IReadOnlyList<GridPos> PopNeighbors(Cell[] cells, int water, GridPos at)
        {
            var removed = new List<GridPos>(4);
            // Fixed order: below, left, right, above — deterministic event order.
            int[] dc = { 0, -1, 1, 0 };
            int[] dr = { -1, 0, 0, 1 };
            for (int i = 0; i < 4; i++)
            {
                int col = at.Col + dc[i];
                int row = at.Row + dr[i];
                if (!BoardSpec.InBounds(col, row) || row < water)
                {
                    continue;
                }

                int idx = BoardSpec.IndexOf(col, row);
                CellKind kind = cells[idx].Kind;
                if (kind == CellKind.Block || kind == CellKind.Coral)
                {
                    cells[idx] = Cell.Empty;
                    removed.Add(new GridPos(col, row));
                }
            }

            return removed;
        }
    }
}
