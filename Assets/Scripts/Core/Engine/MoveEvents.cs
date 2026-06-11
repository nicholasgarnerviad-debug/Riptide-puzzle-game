using System;
using System.Collections.Generic;

namespace Riptide.Core
{
    /// <summary>A creature event (rescued, lost, or spawned) at a board position.</summary>
    public readonly struct CreatureEvent
    {
        public byte CreatureId { get; }
        public GridPos Pos { get; }

        public CreatureEvent(byte creatureId, GridPos pos)
        {
            CreatureId = creatureId;
            Pos = pos;
        }
    }

    /// <summary>GDD 8.2 scoring breakdown for one move. PenaltyPoints is stored positive and subtracted.</summary>
    public sealed class ScoreBreakdown
    {
        public int PlacementPoints { get; }
        public int ClearPoints { get; }
        public int RescuePoints { get; }
        public int TideSurvivalPoints { get; }
        public int PenaltyPoints { get; }

        /// <summary>Combo multiplier applied to clears, in halves (2=x1 .. 5=x2.5); 0 when nothing cleared.</summary>
        public int ComboHalves { get; }

        public long Total => (long)PlacementPoints + ClearPoints + RescuePoints + TideSurvivalPoints - PenaltyPoints;

        public ScoreBreakdown(int placementPoints, int clearPoints, int rescuePoints, int tideSurvivalPoints, int penaltyPoints, int comboHalves)
        {
            PlacementPoints = placementPoints;
            ClearPoints = clearPoints;
            RescuePoints = rescuePoints;
            TideSurvivalPoints = tideSurvivalPoints;
            PenaltyPoints = penaltyPoints;
            ComboHalves = comboHalves;
        }
    }

    /// <summary>
    /// Everything the view layer animates from (GDD 8.2): rows cleared, cells petrified,
    /// creatures rescued/lost, water delta, scoring breakdown — views never re-derive rules.
    /// </summary>
    public sealed class MoveEvents
    {
        public IReadOnlyList<GridPos> PlacedCells { get; }

        /// <summary>Cleared row indices, ascending. Simultaneous per GDD 2.3.</summary>
        public IReadOnlyList<int> RowsCleared { get; }

        public IReadOnlyList<GridPos> PetrifiedCells { get; }
        public IReadOnlyList<CreatureEvent> RescuedCreatures { get; }
        public IReadOnlyList<CreatureEvent> LostCreatures { get; }
        public IReadOnlyList<CreatureEvent> SpawnedCreatures { get; }

        /// <summary>Water rows drained by clears this move (after the MinWaterLevel floor).</summary>
        public int DrainAmount { get; }

        /// <summary>GDD 2.6 step 3 fired (water rose by one).</summary>
        public bool TideRose { get; }

        /// <summary>Net water change: waterAfter - waterBefore.</summary>
        public int WaterDelta { get; }

        /// <summary>The 3 pieces dealt at step 5, or empty when no refill happened.</summary>
        public IReadOnlyList<TrayPiece> DealtPieces { get; }

        public ScoreBreakdown Scoring { get; }
        public GameStatus StatusAfter { get; }

        public MoveEvents(
            IReadOnlyList<GridPos> placedCells,
            IReadOnlyList<int> rowsCleared,
            IReadOnlyList<GridPos> petrifiedCells,
            IReadOnlyList<CreatureEvent> rescuedCreatures,
            IReadOnlyList<CreatureEvent> lostCreatures,
            IReadOnlyList<CreatureEvent> spawnedCreatures,
            int drainAmount,
            bool tideRose,
            int waterDelta,
            IReadOnlyList<TrayPiece> dealtPieces,
            ScoreBreakdown scoring,
            GameStatus statusAfter)
        {
            PlacedCells = placedCells ?? throw new ArgumentNullException(nameof(placedCells));
            RowsCleared = rowsCleared ?? throw new ArgumentNullException(nameof(rowsCleared));
            PetrifiedCells = petrifiedCells ?? throw new ArgumentNullException(nameof(petrifiedCells));
            RescuedCreatures = rescuedCreatures ?? throw new ArgumentNullException(nameof(rescuedCreatures));
            LostCreatures = lostCreatures ?? throw new ArgumentNullException(nameof(lostCreatures));
            SpawnedCreatures = spawnedCreatures ?? throw new ArgumentNullException(nameof(spawnedCreatures));
            DrainAmount = drainAmount;
            TideRose = tideRose;
            WaterDelta = waterDelta;
            DealtPieces = dealtPieces ?? throw new ArgumentNullException(nameof(dealtPieces));
            Scoring = scoring ?? throw new ArgumentNullException(nameof(scoring));
            StatusAfter = statusAfter;
        }
    }
}
