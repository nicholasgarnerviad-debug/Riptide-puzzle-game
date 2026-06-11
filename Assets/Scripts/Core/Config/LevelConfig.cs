using System;
using System.Collections.Generic;

namespace Riptide.Core
{
    /// <summary>A pre-placed cell in a level definition (GDD 3.1 grid preset).</summary>
    public readonly struct PresetCell
    {
        public GridPos Pos { get; }
        public Cell Content { get; }

        public PresetCell(GridPos pos, Cell content)
        {
            Pos = pos;
            Content = content;
        }
    }

    /// <summary>
    /// In-memory level parameters per GDD 3.1 / 2.2. The JSON LevelDef schema and
    /// loader bind to this in Phase 2C; until then tests construct it directly.
    /// All tunables are injected — no balance numbers in C# (rule 7).
    /// </summary>
    public sealed class LevelConfig
    {
        /// <summary>GDD 2.2: water rows at game start (typically 1–3; Endless starts at 1).</summary>
        public int StartWaterLevel { get; }

        /// <summary>GDD 2.2: drain floor (default = startWaterLevel; always explicit here).</summary>
        public int MinWaterLevel { get; }

        /// <summary>GDD 2.2: placements per water rise.</summary>
        public int TideInterval { get; }

        /// <summary>GDD 2.5: Endless spawns a creature every N trays; 0 = off.</summary>
        public int CreatureSpawnIntervalTrays { get; }

        /// <summary>Species count for spawned creatures (creatures.json owns the roster, Phase 2).</summary>
        public int CreatureSpeciesCount { get; }

        /// <summary>Cosmetic palette size for dealt block colors (GDD 7.1 ships 6).</summary>
        public int DealColorCount { get; }

        public ScoringConfig Scoring { get; }
        public GoalSet Goals { get; }
        public IReadOnlyList<PresetCell> Preset { get; }

        public LevelConfig(
            int startWaterLevel,
            int minWaterLevel,
            int tideInterval,
            int creatureSpawnIntervalTrays,
            int creatureSpeciesCount,
            int dealColorCount,
            ScoringConfig scoring,
            GoalSet goals,
            IReadOnlyList<PresetCell>? preset = null)
        {
            if (startWaterLevel < 0 || startWaterLevel >= BoardSpec.DrownWaterLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(startWaterLevel), "startWaterLevel must be in [0, drown level).");
            }

            if (minWaterLevel < 0 || minWaterLevel > startWaterLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(minWaterLevel), "minWaterLevel must be in [0, startWaterLevel].");
            }

            if (tideInterval < 1) throw new ArgumentOutOfRangeException(nameof(tideInterval));
            if (creatureSpawnIntervalTrays < 0) throw new ArgumentOutOfRangeException(nameof(creatureSpawnIntervalTrays));
            if (creatureSpeciesCount < 1) throw new ArgumentOutOfRangeException(nameof(creatureSpeciesCount));
            if (dealColorCount < 1) throw new ArgumentOutOfRangeException(nameof(dealColorCount));

            StartWaterLevel = startWaterLevel;
            MinWaterLevel = minWaterLevel;
            TideInterval = tideInterval;
            CreatureSpawnIntervalTrays = creatureSpawnIntervalTrays;
            CreatureSpeciesCount = creatureSpeciesCount;
            DealColorCount = dealColorCount;
            Scoring = scoring ?? throw new ArgumentNullException(nameof(scoring));
            Goals = goals ?? throw new ArgumentNullException(nameof(goals));
            Preset = ValidatePreset(preset ?? Array.Empty<PresetCell>(), startWaterLevel);
        }

        private static IReadOnlyList<PresetCell> ValidatePreset(IReadOnlyList<PresetCell> preset, int startWaterLevel)
        {
            var seen = new HashSet<int>();
            foreach (PresetCell cell in preset)
            {
                if (!seen.Add(cell.Pos.Index))
                {
                    throw new ArgumentException($"Preset places {cell.Pos} twice.", nameof(preset));
                }

                // GDD 2.2 invariant: submerged rows can only hold coral (or nothing) —
                // a live block or creature below the waterline would have petrified/died.
                bool submergedAtStart = cell.Pos.Row < startWaterLevel;
                bool isLive = cell.Content.Kind == CellKind.Block || cell.Content.Kind == CellKind.Creature;
                if (submergedAtStart && isLive)
                {
                    throw new ArgumentException($"Preset puts a live {cell.Content.Kind} at {cell.Pos}, below startWaterLevel {startWaterLevel}.", nameof(preset));
                }
            }

            return preset;
        }
    }
}
