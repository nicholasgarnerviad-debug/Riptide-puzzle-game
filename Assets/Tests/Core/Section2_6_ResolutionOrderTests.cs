using NUnit.Framework;
using Riptide.Core;

namespace Riptide.Core.Tests
{
    /// <summary>
    /// GDD 2.6 — the canonical move resolution order:
    /// 1 commit, 2 clear+rescue+drain+score, 3 tide tick, 4 drown check, 5 refill, 6 stuck check.
    /// </summary>
    [TestFixture]
    public sealed class Section2_6_ResolutionOrderTests
    {
        [Test]
        public void Step1_ThePlacedPiece_ParticipatesInClearDetection()
        {
            Cell[] cells = TestKit.EmptyBoard();
            TestKit.FillRow(cells, 5, holes: 4);
            GameState state = TestKit.Build(cells: cells);

            MoveResult result = TestKit.Place(state, 0, 4, 5);

            Assert.That(result.Events.RowsCleared, Is.EqualTo(new[] { 5 }), "commit happens before clear detection");
            Assert.That(result.Next.CellAt(4, 5).IsEmpty, Is.True, "the just-placed cell cleared with its row");
        }

        [Test]
        public void Step2Drain_PrecedesStep3Tick_RiseStillFires()
        {
            // One-row clear on the tick move: drain 5->4, then the rise lands 4->5.
            // The rise is never skipped; it lands on the drained level (GDD 2.6 lock).
            Cell[] cells = TestKit.EmptyBoard();
            TestKit.FillRow(cells, 6, holes: 4);
            GameState state = TestKit.Build(
                config: TestKit.Config(startWater: 5, minWater: 1, tideInterval: 3),
                cells: cells,
                water: 5,
                tide: 2);

            MoveResult result = TestKit.Place(state, 0, 4, 6);

            Assert.That(result.Events.DrainAmount, Is.EqualTo(1));
            Assert.That(result.Events.TideRose, Is.True);
            Assert.That(result.Next.WaterLevel, Is.EqualTo(5), "5 -1 drain +1 rise");
            Assert.That(result.Events.WaterDelta, Is.EqualTo(0));
        }

        [Test]
        public void Step2Rescue_PrecedesStep3Loss_CreatureInClearingRowIsSaved()
        {
            Cell[] cells = TestKit.EmptyBoard();
            TestKit.FillRow(cells, 1, holes: 7);
            cells[BoardSpec.IndexOf(3, 1)] = Cell.Creature(5);
            GameState state = TestKit.Build(
                config: TestKit.Config(startWater: 1, tideInterval: 1),
                cells: cells);

            MoveResult result = TestKit.Place(state, 0, 7, 1);

            Assert.That(result.Events.RescuedCreatures.Count, Is.EqualTo(1), "rescued in step 2");
            Assert.That(result.Events.LostCreatures, Is.Empty, "the step-3 rise found an already-cleared row");
            Assert.That(result.Next.WaterLevel, Is.EqualTo(2), "the tide still rose");
        }

        [Test]
        public void WinAtStep2_SkipsTheTideTick()
        {
            var goals = new GoalSet(null, clearRowsTarget: 1, null, null);
            Cell[] cells = TestKit.EmptyBoard();
            TestKit.FillRow(cells, 5, holes: 4);
            GameState state = TestKit.Build(
                config: TestKit.Config(startWater: 1, tideInterval: 1, goals: goals),
                cells: cells);

            MoveResult result = TestKit.Place(state, 0, 4, 5);

            Assert.That(result.Next.Status, Is.EqualTo(GameStatus.Won));
            Assert.That(result.Events.TideRose, Is.False, "DECISIONS.md: the win at step 2 ends the move before the tick");
            Assert.That(result.Next.WaterLevel, Is.EqualTo(1));
            Assert.That(result.Next.TideCounter, Is.EqualTo(0), "counter frozen, not incremented");
        }

        [Test]
        public void Step3CreatureFail_PrecedesStep4DrownCheck()
        {
            var goals = new GoalSet(rescueAllTarget: 1, null, null, null);
            Cell[] cells = TestKit.EmptyBoard();
            cells[BoardSpec.IndexOf(4, 9)] = Cell.Creature(0);
            GameState state = TestKit.Build(
                config: TestKit.Config(startWater: 9, minWater: 1, tideInterval: 1, goals: goals),
                cells: cells,
                water: 9);

            MoveResult result = TestKit.Place(state, 0, 0, 10);

            Assert.That(result.Next.WaterLevel, Is.EqualTo(10), "the rise that kills the target also reaches drown depth");
            Assert.That(result.Next.Status, Is.EqualTo(GameStatus.LostCreature),
                "DECISIONS.md: the step-3 outcome wins over the step-4 drown");
        }

        [Test]
        public void Step4Drown_PrecedesStep5Refill()
        {
            GameState state = TestKit.Build(
                config: TestKit.Config(startWater: 9, minWater: 1, tideInterval: 1),
                water: 9,
                tray: TestKit.Tray(PieceId.Mono1));

            MoveResult result = TestKit.Place(state, 0, 0, 10);

            Assert.That(result.Next.Status, Is.EqualTo(GameStatus.LostDrowned));
            Assert.That(result.Events.DealtPieces, Is.Empty, "no refill for a drowned run despite the empty tray");
        }

        [Test]
        public void Step5Refill_OnlyWhenTrayIsEmpty()
        {
            GameState state = TestKit.Build(tray: TestKit.Tray(PieceId.Mono1, PieceId.Mono1));

            MoveResult result = TestKit.Place(state, 0, 0, 5);

            Assert.That(result.Events.DealtPieces, Is.Empty, "a piece remains — no refill");
            Assert.That(result.Next.TraysDealt, Is.EqualTo(1));
        }

        [Test]
        public void Step6Stuck_IsChecked_EvenWithPiecesRemaining()
        {
            // Coral on every row prevents clears; after the mono fills the only hole,
            // the remaining I5H fits nowhere — stuck with a non-empty tray (GDD 2.3).
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

            Assert.That(result.Events.DealtPieces, Is.Empty, "tray not empty — no refill");
            Assert.That(result.Next.Status, Is.EqualTo(GameStatus.LostStuck));
        }

        [Test]
        public void SurviveTidesGoal_CompletesAtStep4_OnASurvivedRise()
        {
            var goals = new GoalSet(null, null, surviveTidesTarget: 1, null);
            GameState state = TestKit.Build(config: TestKit.Config(startWater: 1, tideInterval: 1, goals: goals));

            MoveResult result = TestKit.Place(state, 0, 0, 5);

            Assert.That(result.Events.TideRose, Is.True);
            Assert.That(result.Next.Goals.TidesSurvived, Is.EqualTo(1));
            Assert.That(result.Next.Status, Is.EqualTo(GameStatus.Won), "survival is credited after the drown check passes");
        }

        [Test]
        public void TidesSurvived_NotCredited_OnTheDrowningRise()
        {
            var goals = new GoalSet(null, null, surviveTidesTarget: 1, null);
            GameState state = TestKit.Build(
                config: TestKit.Config(startWater: 9, minWater: 1, tideInterval: 1, goals: goals),
                water: 9);

            MoveResult result = TestKit.Place(state, 0, 0, 10);

            Assert.That(result.Next.Status, Is.EqualTo(GameStatus.LostDrowned), "you do not 'survive' the rise that drowns you");
            Assert.That(result.Next.Goals.TidesSurvived, Is.EqualTo(0));
        }

        [Test]
        public void ScoreGoal_CanComplete_AtStep2_WithoutAnyClear()
        {
            var goals = new GoalSet(null, null, null, scoreTarget: 3);
            GameState state = TestKit.Build(
                config: TestKit.Config(goals: goals),
                tray: TestKit.Tray(PieceId.I3H));

            MoveResult result = TestKit.Place(state, 0, 0, 5);

            Assert.That(result.Events.RowsCleared, Is.Empty);
            Assert.That(result.Next.Score, Is.EqualTo(3), "3 cells x 1 point");
            Assert.That(result.Next.Status, Is.EqualTo(GameStatus.Won), "placement points alone can satisfy Score(n)");
        }
    }
}
