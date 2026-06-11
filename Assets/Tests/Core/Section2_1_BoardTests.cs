using NUnit.Framework;
using Riptide.Core;

namespace Riptide.Core.Tests
{
    /// <summary>GDD 2.1 — the board.</summary>
    [TestFixture]
    public sealed class Section2_1_BoardTests
    {
        [Test]
        public void Grid_Is9Columns_By12Rows_108Cells()
        {
            Assert.That(BoardSpec.Width, Is.EqualTo(9));
            Assert.That(BoardSpec.Height, Is.EqualTo(12));
            Assert.That(BoardSpec.CellCount, Is.EqualTo(108));
        }

        [Test]
        public void CellStates_RoundTripThroughBoard()
        {
            Cell[] cells = TestKit.EmptyBoard();
            cells[BoardSpec.IndexOf(2, 5)] = Cell.Block(3);
            cells[BoardSpec.IndexOf(4, 5)] = Cell.Coral;
            cells[BoardSpec.IndexOf(6, 5)] = Cell.Creature(2);
            GameState state = TestKit.Build(cells: cells, water: 0);

            Assert.That(state.CellAt(2, 5).Kind, Is.EqualTo(CellKind.Block));
            Assert.That(state.CellAt(2, 5).Id, Is.EqualTo(3));
            Assert.That(state.CellAt(4, 5).Kind, Is.EqualTo(CellKind.Coral));
            Assert.That(state.CellAt(6, 5).Kind, Is.EqualTo(CellKind.Creature));
            Assert.That(state.CellAt(6, 5).Id, Is.EqualTo(2));
            Assert.That(state.CellAt(0, 0).IsEmpty, Is.True);
        }

        [Test]
        public void Row0_IsSeabed_PainterMapsTopDownCorrectly()
        {
            Cell[] cells = TestKit.Paint(
                "........b",   // row 11 (surface)
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                ".........",
                "#........"); // row 0 (seabed)
            GameState state = TestKit.Build(cells: cells, water: 0);

            Assert.That(state.CellAt(0, 0).Kind, Is.EqualTo(CellKind.Coral), "seabed row is row 0");
            Assert.That(state.CellAt(8, 11).Kind, Is.EqualTo(CellKind.Block), "surface row is row 11");
        }

        [Test]
        public void SubmergedRows_AreDerivedFromWaterLevel()
        {
            GameState state = TestKit.Build(config: TestKit.Config(startWater: 3), water: 3);

            Assert.That(state.IsSubmergedRow(0), Is.True);
            Assert.That(state.IsSubmergedRow(2), Is.True);
            Assert.That(state.IsSubmergedRow(3), Is.False, "row at waterLevel is the first dry row");
            Assert.That(state.IsSubmergedRow(11), Is.False);
        }

        [Test]
        public void Restore_RejectsLiveCellsBelowWaterline()
        {
            Cell[] cells = TestKit.EmptyBoard();
            cells[BoardSpec.IndexOf(0, 0)] = Cell.Block(0);

            Assert.Throws<System.ArgumentException>(() => TestKit.Build(cells: cells, water: 1));
        }
    }
}
