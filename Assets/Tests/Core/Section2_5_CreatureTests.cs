using NUnit.Framework;
using Riptide.Core;

namespace Riptide.Core.Tests
{
    /// <summary>GDD 2.5 — creatures: rescue, loss, spawning.</summary>
    [TestFixture]
    public sealed class Section2_5_CreatureTests
    {
        private static Cell[] RowWithCreature(int row, int creatureCol, int holeCol)
        {
            Cell[] cells = TestKit.EmptyBoard();
            TestKit.FillRow(cells, row, holeCol);
            cells[BoardSpec.IndexOf(creatureCol, row)] = Cell.Creature(2);
            return cells;
        }

        [Test]
        public void Creature_CountsAsFilled_ForRowCompletion()
        {
            GameState state = TestKit.Build(cells: RowWithCreature(5, creatureCol: 3, holeCol: 7));

            MoveResult result = TestKit.Place(state, 0, 7, 5);

            Assert.That(result.Events.RowsCleared, Is.EqualTo(new[] { 5 }),
                "GDD 2.5: a creature cell acts as a block for row completion");
        }

        [Test]
        public void ClearingItsRow_RescuesTheCreature()
        {
            GameState state = TestKit.Build(cells: RowWithCreature(5, creatureCol: 3, holeCol: 7));

            MoveResult result = TestKit.Place(state, 0, 7, 5);

            Assert.That(result.Events.RescuedCreatures.Count, Is.EqualTo(1));
            Assert.That(result.Events.RescuedCreatures[0].CreatureId, Is.EqualTo(2));
            Assert.That(result.Next.CellAt(3, 5).IsEmpty, Is.True, "rescued creature leaves the board");
            Assert.That(result.Next.Goals.Rescued, Is.EqualTo(1));
        }

        [Test]
        public void Rescue_AwardsRescuePoints()
        {
            GameState state = TestKit.Build(cells: RowWithCreature(5, creatureCol: 3, holeCol: 7));

            MoveResult result = TestKit.Place(state, 0, 7, 5);

            Assert.That(result.Events.Scoring.RescuePoints, Is.EqualTo(250), "GDD 10: rescue +250");
        }

        [Test]
        public void Rescue_IncrementsRescueStreak()
        {
            GameState state = TestKit.Build(cells: RowWithCreature(5, creatureCol: 3, holeCol: 7), rescueStreak: 2);

            MoveResult result = TestKit.Place(state, 0, 7, 5);

            Assert.That(result.Next.RescueStreak, Is.EqualTo(3));
        }

        [Test]
        public void CreatureLoss_FailsTheLevel_WhenRescueGoalExists()
        {
            var goals = new GoalSet(rescueAllTarget: 1, clearRowsTarget: null, surviveTidesTarget: null, scoreTarget: null);
            Cell[] cells = TestKit.EmptyBoard();
            cells[BoardSpec.IndexOf(4, 1)] = Cell.Creature(0);
            GameState state = TestKit.Build(
                config: TestKit.Config(startWater: 1, tideInterval: 1, goals: goals),
                cells: cells);

            MoveResult result = TestKit.Place(state, 0, 0, 5);

            Assert.That(result.Next.Status, Is.EqualTo(GameStatus.LostCreature),
                "GDD 2.2: losing a rescue target fails the level");
        }

        [Test]
        public void CreatureLoss_AppliesPenalty_WhenNoRescueGoal()
        {
            Cell[] cells = TestKit.EmptyBoard();
            cells[BoardSpec.IndexOf(4, 1)] = Cell.Creature(0);
            GameState state = TestKit.Build(
                config: TestKit.Config(startWater: 1, tideInterval: 1),
                cells: cells,
                score: 1000);

            MoveResult result = TestKit.Place(state, 0, 0, 5);

            Assert.That(result.Next.Status, Is.EqualTo(GameStatus.InProgress));
            Assert.That(result.Events.Scoring.PenaltyPoints, Is.EqualTo(250), "GDD 2.5: -250 per lost creature");
            Assert.That(result.Next.Score, Is.EqualTo(1000 + 1 - 250), "previous + placement point - penalty");
        }

        [Test]
        public void CreatureLoss_ResetsTheRescueStreak()
        {
            Cell[] cells = TestKit.EmptyBoard();
            cells[BoardSpec.IndexOf(4, 1)] = Cell.Creature(0);
            GameState state = TestKit.Build(
                config: TestKit.Config(startWater: 1, tideInterval: 1),
                cells: cells,
                rescueStreak: 5);

            MoveResult result = TestKit.Place(state, 0, 0, 5);

            Assert.That(result.Next.RescueStreak, Is.EqualTo(0), "GDD 2.5: streak-of-rescues reset");
        }

        [Test]
        public void EndlessSpawn_HappensEveryNTrays_InTheDangerBand()
        {
            // spawnEvery 2: tray #1 (NewGame) no spawn, tray #2 (first refill) spawns.
            GameState state = GameState.NewGame(
                TestKit.Config(startWater: 1, tideInterval: 50, spawnEveryTrays: 2),
                seed: 11);
            Assert.That(CountCreatures(state), Is.EqualTo(0), "1 % 2 != 0 — no spawn on the first tray");

            // Place all three pieces wherever they fit high on the board, forcing a refill.
            GameState current = state;
            MoveResult last = default;
            for (int slot = 0; slot < 3; slot++)
            {
                last = PlaceAnywhere(current, slot);
                current = last.Next;
            }

            Assert.That(last.Events.DealtPieces.Count, Is.EqualTo(3), "third placement refilled");
            Assert.That(last.Events.SpawnedCreatures.Count, Is.EqualTo(1), "tray #2 hits the spawn cadence");

            CreatureEvent spawned = last.Events.SpawnedCreatures[0];
            int water = current.WaterLevel;
            Assert.That(spawned.Pos.Row, Is.InRange(water + 1, water + 3),
                "GDD 2.5: spawns land in rows waterLevel+1..waterLevel+3");
            Assert.That(current.CellAt(spawned.Pos.Col, spawned.Pos.Row).Kind, Is.EqualTo(CellKind.Creature));
        }

        [Test]
        public void EndlessSpawn_SkipsSilently_WhenTheBandIsFull()
        {
            // Coral floods rows 2..4 (the band for water 1) — nothing can spawn there.
            Cell[] cells = TestKit.EmptyBoard();
            for (int row = 2; row <= 4; row++)
            {
                for (int col = 0; col < BoardSpec.Width; col++)
                {
                    cells[BoardSpec.IndexOf(col, row)] = Cell.Coral;
                }
            }

            GameState state = TestKit.Build(
                config: TestKit.Config(startWater: 1, tideInterval: 50, spawnEveryTrays: 2),
                cells: cells,
                tray: TestKit.Tray(PieceId.Mono1),
                traysDealt: 1);

            MoveResult result = TestKit.Place(state, 0, 0, 6);

            Assert.That(result.Events.DealtPieces.Count, Is.EqualTo(3), "refill still happens");
            Assert.That(result.Events.SpawnedCreatures, Is.Empty, "DECISIONS.md: spawn skips when the band is full");
        }

        private static int CountCreatures(GameState state)
        {
            int count = 0;
            for (int row = 0; row < BoardSpec.Height; row++)
            {
                for (int col = 0; col < BoardSpec.Width; col++)
                {
                    if (state.CellAt(col, row).Kind == CellKind.Creature) count++;
                }
            }

            return count;
        }

        private static MoveResult PlaceAnywhere(GameState state, int slot)
        {
            TrayPiece? piece = state.TrayAt(slot);
            Assert.That(piece.HasValue, $"slot {slot} should hold a piece");
            for (int row = BoardSpec.Height - 1; row >= state.WaterLevel; row--)
            {
                for (int col = 0; col < BoardSpec.Width; col++)
                {
                    if (PlacementValidator.CanPlace(state, piece!.Value.Piece, new GridPos(col, row)))
                    {
                        return TestKit.Place(state, slot, col, row);
                    }
                }
            }

            Assert.Fail($"no legal placement for slot {slot}");
            throw new System.InvalidOperationException("unreachable");
        }
    }
}
