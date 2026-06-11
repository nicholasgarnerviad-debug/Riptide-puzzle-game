using NUnit.Framework;
using Riptide.Core;

namespace Riptide.Core.Tests
{
    /// <summary>GDD 2.2 — water: tide tick, drain, submersion, drown.</summary>
    [TestFixture]
    public sealed class Section2_2_WaterTests
    {
        [Test]
        public void NewGame_StartsAtConfiguredWaterLevel()
        {
            GameState state = GameState.NewGame(TestKit.Config(startWater: 2, minWater: 2), seed: 7);

            Assert.That(state.WaterLevel, Is.EqualTo(2));
            Assert.That(state.TideCounter, Is.EqualTo(0));
        }

        [Test]
        public void TideCounter_IncrementsOnEachPlacement()
        {
            GameState state = TestKit.Build(config: TestKit.Config(tideInterval: 8));

            MoveResult first = TestKit.Place(state, 0, 0, 5);
            Assert.That(first.Next.TideCounter, Is.EqualTo(1));

            MoveResult second = TestKit.Place(first.Next, 1, 2, 5);
            Assert.That(second.Next.TideCounter, Is.EqualTo(2));
        }

        [Test]
        public void Water_RisesAtInterval_AndCounterResets()
        {
            GameState state = TestKit.Build(config: TestKit.Config(tideInterval: 2));

            MoveResult first = TestKit.Place(state, 0, 0, 5);
            Assert.That(first.Next.WaterLevel, Is.EqualTo(1), "no rise before the interval");
            Assert.That(first.Events.TideRose, Is.False);

            MoveResult second = TestKit.Place(first.Next, 1, 2, 5);
            Assert.That(second.Events.TideRose, Is.True);
            Assert.That(second.Next.WaterLevel, Is.EqualTo(2));
            Assert.That(second.Next.TideCounter, Is.EqualTo(0), "counter resets after a rise");
        }

        [Test]
        public void Drain_OneLevelPerClearedRow()
        {
            Cell[] cells = TestKit.EmptyBoard();
            TestKit.FillRow(cells, 6, holes: 4);
            GameState state = TestKit.Build(config: TestKit.Config(startWater: 4, minWater: 1), cells: cells, water: 4);

            MoveResult result = TestKit.Place(state, 0, 4, 6);

            Assert.That(result.Events.RowsCleared, Is.EqualTo(new[] { 6 }));
            Assert.That(result.Events.DrainAmount, Is.EqualTo(1));
            Assert.That(result.Next.WaterLevel, Is.EqualTo(3));
        }

        [Test]
        public void Drain_MultiRowClear_DrainsPerRow()
        {
            Cell[] cells = TestKit.EmptyBoard();
            TestKit.FillRow(cells, 6, holes: 4);
            TestKit.FillRow(cells, 7, holes: 4);
            GameState state = TestKit.Build(
                config: TestKit.Config(startWater: 4, minWater: 1),
                cells: cells,
                water: 4,
                tray: TestKit.Tray(PieceId.DominoV, PieceId.Mono1));

            MoveResult result = TestKit.Place(state, 0, 4, 6);

            Assert.That(result.Events.RowsCleared, Is.EqualTo(new[] { 6, 7 }));
            Assert.That(result.Events.DrainAmount, Is.EqualTo(2));
            Assert.That(result.Next.WaterLevel, Is.EqualTo(2));
        }

        [Test]
        public void Drain_FloorsAtMinWaterLevel()
        {
            Cell[] cells = TestKit.EmptyBoard();
            TestKit.FillRow(cells, 5, holes: 0);
            GameState state = TestKit.Build(config: TestKit.Config(startWater: 2, minWater: 2), cells: cells, water: 2);

            MoveResult result = TestKit.Place(state, 0, 0, 5);

            Assert.That(result.Events.RowsCleared.Count, Is.EqualTo(1));
            Assert.That(result.Events.DrainAmount, Is.EqualTo(0), "already at the floor");
            Assert.That(result.Next.WaterLevel, Is.EqualTo(2));
        }

        [Test]
        public void ClearOnTickMove_DrainsFirst_RiseLandsOnDrainedLevel()
        {
            // GDD 2.6 locked example: drain in step 2, tick in step 3 —
            // the rise lands on the new (drained) level, it is not skipped.
            Cell[] cells = TestKit.EmptyBoard();
            TestKit.FillRow(cells, 6, holes: 4);
            TestKit.FillRow(cells, 7, holes: 4);
            GameState state = TestKit.Build(
                config: TestKit.Config(startWater: 5, minWater: 1, tideInterval: 3),
                cells: cells,
                water: 5,
                tide: 2,
                tray: TestKit.Tray(PieceId.DominoV, PieceId.Mono1));

            MoveResult result = TestKit.Place(state, 0, 4, 6);

            Assert.That(result.Events.DrainAmount, Is.EqualTo(2), "drain resolves first (5 -> 3)");
            Assert.That(result.Events.TideRose, Is.True, "the tick still fires");
            Assert.That(result.Next.WaterLevel, Is.EqualTo(4), "rise lands on the drained level (3 -> 4)");
            Assert.That(result.Events.WaterDelta, Is.EqualTo(-1));
            Assert.That(result.Next.TideCounter, Is.EqualTo(0));
        }

        [Test]
        public void Clear_CanSaveFromDrowning_OnTheSameMove()
        {
            // GDD 2.2: "clears can save you on the same move".
            Cell[] cells = TestKit.EmptyBoard();
            TestKit.FillRow(cells, 9, holes: 4);
            GameState state = TestKit.Build(
                config: TestKit.Config(startWater: 9, minWater: 1, tideInterval: 1),
                cells: cells,
                water: 9);

            MoveResult result = TestKit.Place(state, 0, 4, 9);

            Assert.That(result.Events.DrainAmount, Is.EqualTo(1), "9 -> 8 from the clear");
            Assert.That(result.Next.WaterLevel, Is.EqualTo(9), "8 + 1 from the rise");
            Assert.That(result.Next.Status, Is.EqualTo(GameStatus.InProgress), "alive at 9 — the clear saved the run");
        }

        [Test]
        public void Rise_PetrifiesBlocksInNewlySubmergedRow()
        {
            Cell[] cells = TestKit.EmptyBoard();
            cells[BoardSpec.IndexOf(2, 1)] = Cell.Block(4);
            cells[BoardSpec.IndexOf(7, 1)] = Cell.Block(1);
            GameState state = TestKit.Build(config: TestKit.Config(startWater: 1, tideInterval: 1), cells: cells);

            MoveResult result = TestKit.Place(state, 0, 0, 5);

            Assert.That(result.Next.WaterLevel, Is.EqualTo(2));
            Assert.That(result.Next.CellAt(2, 1).Kind, Is.EqualTo(CellKind.Coral), "block petrified to coral");
            Assert.That(result.Next.CellAt(7, 1).Kind, Is.EqualTo(CellKind.Coral));
            Assert.That(result.Events.PetrifiedCells.Count, Is.EqualTo(2));
        }

        [Test]
        public void RowWithCoral_CanNeverComplete()
        {
            // GDD 2.2: coral "counts as filled for nothing — a coral row can never be completed".
            Cell[] cells = TestKit.EmptyBoard();
            TestKit.FillRow(cells, 5, holes: 8);
            cells[BoardSpec.IndexOf(0, 5)] = Cell.Coral;
            GameState state = TestKit.Build(cells: cells);

            MoveResult result = TestKit.Place(state, 0, 8, 5);

            Assert.That(result.Events.RowsCleared, Is.Empty, "row holds coral; filling every Empty cell must not clear it");
            Assert.That(result.Next.CellAt(0, 5).Kind, Is.EqualTo(CellKind.Coral));
        }

        [Test]
        public void Coral_SurvivesDrain_WhenExposedAboveWater()
        {
            // GDD 2.2: coral never drains away.
            Cell[] cells = TestKit.EmptyBoard();
            cells[BoardSpec.IndexOf(3, 2)] = Cell.Coral;   // submerged at water 3
            TestKit.FillRow(cells, 6, holes: 4);
            GameState state = TestKit.Build(config: TestKit.Config(startWater: 3, minWater: 1), cells: cells, water: 3);

            MoveResult result = TestKit.Place(state, 0, 4, 6);

            Assert.That(result.Next.WaterLevel, Is.EqualTo(2), "row 2 is exposed now");
            Assert.That(result.Next.CellAt(3, 2).Kind, Is.EqualTo(CellKind.Coral), "exposed coral stays coral");
        }

        [Test]
        public void Rise_LosesCreatureInRisingRow()
        {
            Cell[] cells = TestKit.EmptyBoard();
            cells[BoardSpec.IndexOf(5, 1)] = Cell.Creature(3);
            GameState state = TestKit.Build(config: TestKit.Config(startWater: 1, tideInterval: 1), cells: cells);

            MoveResult result = TestKit.Place(state, 0, 0, 5);

            Assert.That(result.Events.LostCreatures.Count, Is.EqualTo(1));
            Assert.That(result.Events.LostCreatures[0].CreatureId, Is.EqualTo(3));
            Assert.That(result.Next.CellAt(5, 1).IsEmpty, Is.True, "lost creature leaves the board");
        }

        [Test]
        public void GameOver_DrownsAtWaterLevel10()
        {
            GameState state = TestKit.Build(config: TestKit.Config(startWater: 9, minWater: 1, tideInterval: 1), water: 9);

            MoveResult result = TestKit.Place(state, 0, 0, 10);

            Assert.That(result.Next.WaterLevel, Is.EqualTo(10));
            Assert.That(result.Next.Status, Is.EqualTo(GameStatus.LostDrowned));
        }

        [Test]
        public void NoDrown_AtWaterLevel9()
        {
            GameState state = TestKit.Build(config: TestKit.Config(startWater: 8, minWater: 1, tideInterval: 1), water: 8);

            MoveResult result = TestKit.Place(state, 0, 0, 9);

            Assert.That(result.Next.WaterLevel, Is.EqualTo(9));
            Assert.That(result.Next.Status, Is.EqualTo(GameStatus.InProgress), "danger line, not death (drown is >= 10)");
        }
    }
}
