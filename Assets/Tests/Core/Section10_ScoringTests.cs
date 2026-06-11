using NUnit.Framework;
using Riptide.Core;

namespace Riptide.Core.Tests
{
    /// <summary>GDD 10 — scoring: placement, clears, combo ladder, survival, penalties.</summary>
    [TestFixture]
    public sealed class Section10_ScoringTests
    {
        [Test]
        public void Placement_ScoresOnePointPerCell()
        {
            GameState state = TestKit.Build(tray: TestKit.Tray(PieceId.I5H));

            MoveResult result = TestKit.Place(state, 0, 0, 5);

            Assert.That(result.Events.Scoring.PlacementPoints, Is.EqualTo(5));
            Assert.That(result.Next.Score, Is.EqualTo(5));
        }

        [Test]
        public void RowClear_Is80PerRow_AtBaseMultiplier()
        {
            Cell[] cells = TestKit.EmptyBoard();
            TestKit.FillRow(cells, 5, holes: 4);
            GameState state = TestKit.Build(cells: cells);

            MoveResult result = TestKit.Place(state, 0, 4, 5);

            Assert.That(result.Events.Scoring.ClearPoints, Is.EqualTo(80), "80 x 1 row x x1");
            Assert.That(result.Events.Scoring.ComboHalves, Is.EqualTo(2), "x1 multiplier");
            Assert.That(result.Next.ComboChain, Is.EqualTo(1));
        }

        [Test]
        public void Combo_SecondConsecutiveClear_PaysX1_5()
        {
            Cell[] cells = TestKit.EmptyBoard();
            TestKit.FillRow(cells, 5, holes: 4);
            TestKit.FillRow(cells, 6, holes: 4);
            GameState state = TestKit.Build(cells: cells);

            MoveResult first = TestKit.Place(state, 0, 4, 5);
            Assert.That(first.Events.Scoring.ClearPoints, Is.EqualTo(80));

            MoveResult second = TestKit.Place(first.Next, 1, 4, 6);
            Assert.That(second.Next.ComboChain, Is.EqualTo(2));
            Assert.That(second.Events.Scoring.ComboHalves, Is.EqualTo(3), "x1.5");
            Assert.That(second.Events.Scoring.ClearPoints, Is.EqualTo(120), "80 x 1 x 1.5");
        }

        [Test]
        public void Combo_ThirdPaysX2_FourthPaysX2_5()
        {
            Cell[] cells3 = TestKit.EmptyBoard();
            TestKit.FillRow(cells3, 5, holes: 4);
            GameState chain2 = TestKit.Build(cells: cells3, comboChain: 2);
            MoveResult third = TestKit.Place(chain2, 0, 4, 5);
            Assert.That(third.Events.Scoring.ComboHalves, Is.EqualTo(4), "x2");
            Assert.That(third.Events.Scoring.ClearPoints, Is.EqualTo(160));

            Cell[] cells4 = TestKit.EmptyBoard();
            TestKit.FillRow(cells4, 5, holes: 4);
            GameState chain3 = TestKit.Build(cells: cells4, comboChain: 3);
            MoveResult fourth = TestKit.Place(chain3, 0, 4, 5);
            Assert.That(fourth.Events.Scoring.ComboHalves, Is.EqualTo(5), "x2.5");
            Assert.That(fourth.Events.Scoring.ClearPoints, Is.EqualTo(200));
        }

        [Test]
        public void Combo_CapsAtX2_5()
        {
            Cell[] cells = TestKit.EmptyBoard();
            TestKit.FillRow(cells, 5, holes: 4);
            GameState deepChain = TestKit.Build(cells: cells, comboChain: 9);

            MoveResult result = TestKit.Place(deepChain, 0, 4, 5);

            Assert.That(result.Events.Scoring.ComboHalves, Is.EqualTo(5), "GDD 10: x2.5 cap");
            Assert.That(result.Events.Scoring.ClearPoints, Is.EqualTo(200));
        }

        [Test]
        public void Combo_ResetsOnANonClearingPlacement()
        {
            Cell[] cells = TestKit.EmptyBoard();
            TestKit.FillRow(cells, 5, holes: 4);
            TestKit.FillRow(cells, 6, holes: 4);
            GameState state = TestKit.Build(cells: cells);

            MoveResult clear1 = TestKit.Place(state, 0, 4, 5);
            Assert.That(clear1.Next.ComboChain, Is.EqualTo(1));

            MoveResult quiet = TestKit.Place(clear1.Next, 1, 0, 10);
            Assert.That(quiet.Next.ComboChain, Is.EqualTo(0), "GDD 10: combo = consecutive placements that clear");

            MoveResult clear2 = TestKit.Place(quiet.Next, 2, 4, 6);
            Assert.That(clear2.Events.Scoring.ComboHalves, Is.EqualTo(2), "back to x1 after the reset");
            Assert.That(clear2.Events.Scoring.ClearPoints, Is.EqualTo(80));
        }

        [Test]
        public void TideSurvival_Escalates_30_35_40_WhenEnabled()
        {
            GameState state = TestKit.Build(
                config: TestKit.Config(startWater: 1, minWater: 1, tideInterval: 1, awardTideSurvival: true));

            MoveResult first = TestKit.Place(state, 0, 0, 5);
            Assert.That(first.Events.Scoring.TideSurvivalPoints, Is.EqualTo(30), "GDD 10: +30 for the first tide");

            MoveResult second = TestKit.Place(first.Next, 1, 2, 5);
            Assert.That(second.Events.Scoring.TideSurvivalPoints, Is.EqualTo(35), "escalating +5 per tide");

            MoveResult third = TestKit.Place(second.Next, 2, 4, 5);
            Assert.That(third.Events.Scoring.TideSurvivalPoints, Is.EqualTo(40));
        }

        [Test]
        public void TideSurvival_NotAwarded_WhenDisabled()
        {
            GameState state = TestKit.Build(config: TestKit.Config(startWater: 1, tideInterval: 1, awardTideSurvival: false));

            MoveResult result = TestKit.Place(state, 0, 0, 5);

            Assert.That(result.Events.TideRose, Is.True);
            Assert.That(result.Events.Scoring.TideSurvivalPoints, Is.EqualTo(0), "Voyage mode: no survival points (GDD 10)");
        }

        [Test]
        public void ScoreBreakdown_TotalMatchesTheStateScoreDelta()
        {
            Cell[] cells = TestKit.EmptyBoard();
            TestKit.FillRow(cells, 5, holes: 4);
            cells[BoardSpec.IndexOf(2, 5)] = Cell.Creature(1);
            GameState state = TestKit.Build(cells: cells, score: 500);

            MoveResult result = TestKit.Place(state, 0, 4, 5);

            Assert.That(result.Events.Scoring.Total, Is.EqualTo(result.Next.Score - 500),
                "the breakdown is the single source the view sums from (GDD 8.2)");
            Assert.That(result.Events.Scoring.Total, Is.EqualTo(1 + 80 + 250), "placement + clear + rescue");
        }

        [Test]
        public void Score_MayGoNegative_OnPenalties()
        {
            Cell[] cells = TestKit.EmptyBoard();
            cells[BoardSpec.IndexOf(4, 1)] = Cell.Creature(0);
            GameState state = TestKit.Build(
                config: TestKit.Config(startWater: 1, tideInterval: 1),
                cells: cells,
                score: 0);

            MoveResult result = TestKit.Place(state, 0, 0, 5);

            Assert.That(result.Next.Score, Is.EqualTo(1 - 250),
                "GDD defines no floor; clamping would be an invented rule (DECISIONS.md)");
        }
    }
}
