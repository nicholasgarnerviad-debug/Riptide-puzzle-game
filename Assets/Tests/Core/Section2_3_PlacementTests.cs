using NUnit.Framework;
using Riptide.Core;

namespace Riptide.Core.Tests
{
    /// <summary>GDD 2.3 — pieces, placement, row clears, tray, stuck.</summary>
    [TestFixture]
    public sealed class Section2_3_PlacementTests
    {
        [Test]
        public void Placement_RequiresEmptyCells()
        {
            Cell[] cells = TestKit.EmptyBoard();
            cells[BoardSpec.IndexOf(4, 5)] = Cell.Block(0);
            GameState state = TestKit.Build(cells: cells);

            Assert.That(PlacementValidator.CanPlace(state, PieceId.Mono1, new GridPos(4, 5)), Is.False);
            Assert.Throws<InvalidMoveException>(() => TestKit.Place(state, 0, 4, 5));
        }

        [Test]
        public void Placement_RejectedBelowWaterline_AllowedAtWaterline()
        {
            GameState state = TestKit.Build(config: TestKit.Config(startWater: 2), water: 2);

            Assert.That(PlacementValidator.CanPlace(state, PieceId.Mono1, new GridPos(0, 1)), Is.False, "row 1 is underwater");
            Assert.That(PlacementValidator.CanPlace(state, PieceId.Mono1, new GridPos(0, 2)), Is.True, "GDD 2.3: at or above waterLevel");
        }

        [Test]
        public void Placement_RejectedWhenMaskLeavesBoard()
        {
            GameState state = TestKit.Build(water: 0);

            Assert.That(PlacementValidator.CanPlace(state, PieceId.I4H, new GridPos(6, 0)), Is.False, "cols 6..9 leave the board");
            Assert.That(PlacementValidator.CanPlace(state, PieceId.I4H, new GridPos(5, 0)), Is.True);
            Assert.That(PlacementValidator.CanPlace(state, PieceId.I4V, new GridPos(0, 9)), Is.False, "rows 9..12 leave the board");
            Assert.That(PlacementValidator.CanPlace(state, PieceId.I4V, new GridPos(0, 8)), Is.True);
        }

        [Test]
        public void Placement_CommitsAllMaskCells_WithTrayColor()
        {
            GameState state = TestKit.Build(
                tray: new TrayPiece?[] { new TrayPiece(PieceId.L3MissingNE, 4), null, null },
                water: 0);

            MoveResult result = TestKit.Place(state, 0, 3, 3);

            // L3MissingNE mask: (0,0) (1,0) (0,1)
            Assert.That(result.Next.CellAt(3, 3).Kind, Is.EqualTo(CellKind.Block));
            Assert.That(result.Next.CellAt(4, 3).Kind, Is.EqualTo(CellKind.Block));
            Assert.That(result.Next.CellAt(3, 4).Kind, Is.EqualTo(CellKind.Block));
            Assert.That(result.Next.CellAt(3, 3).Id, Is.EqualTo(4), "dealt color travels onto the board");
            Assert.That(result.Events.PlacedCells.Count, Is.EqualTo(3));
        }

        [Test]
        public void Tray_AllThreeMustBePlaced_BeforeRefill()
        {
            GameState state = TestKit.Build();

            MoveResult first = TestKit.Place(state, 0, 0, 5);
            Assert.That(first.Events.DealtPieces, Is.Empty);
            Assert.That(first.Next.TrayPieceCount, Is.EqualTo(2));

            MoveResult second = TestKit.Place(first.Next, 1, 2, 5);
            Assert.That(second.Events.DealtPieces, Is.Empty);
            Assert.That(second.Next.TrayPieceCount, Is.EqualTo(1));

            MoveResult third = TestKit.Place(second.Next, 2, 4, 5);
            Assert.That(third.Events.DealtPieces.Count, Is.EqualTo(3), "refill only after all three are placed");
            Assert.That(third.Next.TrayPieceCount, Is.EqualTo(3));
            Assert.That(third.Next.TraysDealt, Is.EqualTo(2));
        }

        [Test]
        public void NoDiscards_PlacingFromEmptySlotThrows()
        {
            GameState state = TestKit.Build(tray: TestKit.Tray(PieceId.Mono1, PieceId.Mono1));

            Assert.Throws<InvalidMoveException>(() => TestKit.Place(state, 2, 0, 5), "slot 2 was never dealt");
        }

        [Test]
        public void RowClears_WhenFullyFilled_AboveWaterline()
        {
            Cell[] cells = TestKit.EmptyBoard();
            TestKit.FillRow(cells, 4, holes: 7);
            GameState state = TestKit.Build(cells: cells);

            MoveResult result = TestKit.Place(state, 0, 7, 4);

            Assert.That(result.Events.RowsCleared, Is.EqualTo(new[] { 4 }));
            for (int col = 0; col < BoardSpec.Width; col++)
            {
                Assert.That(result.Next.CellAt(col, 4).IsEmpty, Is.True, $"col {col} cleared");
            }
        }

        [Test]
        public void MultiRowClear_IsSimultaneous_AndCountsAsCombo()
        {
            Cell[] cells = TestKit.EmptyBoard();
            TestKit.FillRow(cells, 3, holes: 2);
            TestKit.FillRow(cells, 4, holes: 2);
            GameState state = TestKit.Build(
                cells: cells,
                tray: TestKit.Tray(PieceId.DominoV, PieceId.Mono1),
                water: 0);

            MoveResult result = TestKit.Place(state, 0, 2, 3);

            Assert.That(result.Events.RowsCleared, Is.EqualTo(new[] { 3, 4 }), "both rows clear in the same step");
            Assert.That(result.Events.Scoring.ClearPoints, Is.EqualTo(160), "80 x 2 rows x x1 multiplier (GDD 10)");
        }

        [Test]
        public void NoGravity_CellsAboveAClearedRow_StayPut()
        {
            Cell[] cells = TestKit.EmptyBoard();
            TestKit.FillRow(cells, 4, holes: 7);
            cells[BoardSpec.IndexOf(0, 7)] = Cell.Block(2);
            cells[BoardSpec.IndexOf(5, 9)] = Cell.Block(3);
            GameState state = TestKit.Build(cells: cells);

            MoveResult result = TestKit.Place(state, 0, 7, 4);

            Assert.That(result.Events.RowsCleared, Is.EqualTo(new[] { 4 }));
            Assert.That(result.Next.CellAt(0, 7).Id, Is.EqualTo(2), "GDD 2.3: nothing falls when rows clear");
            Assert.That(result.Next.CellAt(5, 9).Id, Is.EqualTo(3));
            Assert.That(result.Next.CellAt(0, 6).IsEmpty, Is.True, "no compaction either");
        }

        [Test]
        public void Columns_NeverClear()
        {
            // GDD 2.3: rows only. Fill column 0 across every playable row.
            Cell[] cells = TestKit.EmptyBoard();
            for (int row = 1; row < BoardSpec.Height; row++)
            {
                cells[BoardSpec.IndexOf(0, row)] = Cell.Block(0);
            }

            GameState state = TestKit.Build(cells: cells);

            MoveResult result = TestKit.Place(state, 0, 4, 5);

            Assert.That(result.Events.RowsCleared, Is.Empty);
            for (int row = 1; row < BoardSpec.Height; row++)
            {
                Assert.That(result.Next.CellAt(0, row).Kind, Is.EqualTo(CellKind.Block), $"column intact at row {row}");
            }
        }

        [Test]
        public void GameOver_Stuck_WhenNoTrayPieceFits()
        {
            // Every row carries coral so nothing can ever clear (GDD 2.2);
            // one hole remains for the mono, then the I5H can go nowhere.
            Cell[] cells = TestKit.Paint(
                "#bbbbbbbb",
                "b#bbbbbbb",
                "bb#bbbbbb",
                "bbb#bbbbb",
                "bbbb#bbbb",
                "bbbbb#bbb",
                "bbbbbb#bb",
                "bbbbbbb#b",
                "bbbbbbbb#",
                "#bbbbbbb.",
                "b#bbbbbbb",
                "#########");
            GameState state = TestKit.Build(
                cells: cells,
                tray: TestKit.Tray(PieceId.Mono1, PieceId.I5H),
                water: 0);

            MoveResult result = TestKit.Place(state, 0, 8, 2);

            Assert.That(result.Events.RowsCleared, Is.Empty);
            Assert.That(result.Next.Status, Is.EqualTo(GameStatus.LostStuck), "GDD 2.3: no remaining piece fits anywhere");
        }

        [Test]
        public void StuckCheck_RunsAfterRefill_Too()
        {
            // Tray empties into the last hole; the forced refill deals onto a
            // coral-locked full board — stuck is detected on the same move (GDD 2.3/2.6).
            Cell[] cells = TestKit.Paint(
                "#bbbbbbbb",
                "b#bbbbbbb",
                "bb#bbbbbb",
                "bbb#bbbbb",
                "bbbb#bbbb",
                "bbbbb#bbb",
                "bbbbbb#bb",
                "bbbbbbb#b",
                "bbbbbbbb#",
                "#bbbbbbb.",
                "b#bbbbbbb",
                "#########");
            GameState state = TestKit.Build(
                cells: cells,
                tray: TestKit.Tray(PieceId.Mono1),
                water: 0);

            MoveResult result = TestKit.Place(state, 0, 8, 2);

            Assert.That(result.Events.DealtPieces.Count, Is.EqualTo(3), "refill happened (step 5)");
            Assert.That(result.Next.Status, Is.EqualTo(GameStatus.LostStuck), "then stuck was detected (step 6)");
        }
    }
}
