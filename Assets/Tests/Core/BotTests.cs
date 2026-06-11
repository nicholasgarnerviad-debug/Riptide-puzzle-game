using NUnit.Framework;
using Riptide.Core;

namespace Riptide.Core.Tests
{
    /// <summary>GDD 4 / master prompt 3A — the three bot policies.</summary>
    [TestFixture]
    public sealed class BotTests
    {
        private static GreedyHeuristicPolicy Heuristic() =>
            new GreedyHeuristicPolicy(TestKit.Economy().GreedyHeuristic);

        [Test]
        public void GreedyClear_TakesTheClearingMove()
        {
            Cell[] cells = TestKit.EmptyBoard();
            TestKit.FillRow(cells, 5, holes: 4);
            GameState state = TestKit.Build(cells: cells);

            BotDecision decision = new GreedyClearPolicy().Choose(state, DeterministicRng.FromSeed(1));

            Assert.That(decision.Move, Is.Not.Null);
            Assert.That(decision.Move!.Target, Is.EqualTo(new GridPos(4, 5)), "the only clearing placement");
        }

        [Test]
        public void GreedyClear_FallsBackToFirstLegal_WhenNothingClears()
        {
            GameState state = TestKit.Build();

            BotDecision decision = new GreedyClearPolicy().Choose(state, DeterministicRng.FromSeed(1));

            Assert.That(decision.Move, Is.Not.Null);
            Assert.That(decision.Move!.TraySlot, Is.EqualTo(0));
            Assert.That(decision.Move.Target, Is.EqualTo(new GridPos(0, 1)), "scan order: first legal above the waterline");
        }

        [Test]
        public void GreedyHeuristic_TakesTheClearThatAvoidsDrowning()
        {
            // Water 9, the tide ticks this move. Any non-clearing placement drowns;
            // clearing row 9 drains first and survives (GDD 2.2 example).
            Cell[] cells = TestKit.EmptyBoard();
            TestKit.FillRow(cells, 9, holes: 4);
            GameState state = TestKit.Build(
                config: TestKit.Config(startWater: 9, minWater: 1, tideInterval: 1),
                cells: cells,
                water: 9);

            BotDecision decision = Heuristic().Choose(state, DeterministicRng.FromSeed(1));

            Assert.That(decision.Move!.Target, Is.EqualTo(new GridPos(4, 9)), "the survival move");
            MoveResult applied = SimEngine.ApplyMove(state, decision.Move);
            Assert.That(applied.Next.Status, Is.EqualTo(GameStatus.InProgress));
        }

        [Test]
        public void GreedyHeuristic_PrefersTheRescueClear()
        {
            // Two single-hole rows; row 6's clear also rescues a creature.
            Cell[] cells = TestKit.EmptyBoard();
            TestKit.FillRow(cells, 5, holes: 4);
            TestKit.FillRow(cells, 6, holes: 7);
            cells[BoardSpec.IndexOf(2, 6)] = Cell.Creature(3);
            GameState state = TestKit.Build(cells: cells);

            BotDecision decision = Heuristic().Choose(state, DeterministicRng.FromSeed(1));

            Assert.That(decision.Move!.Target, Is.EqualTo(new GridPos(7, 6)), "rescue outweighs a plain clear");
        }

        [Test]
        public void GreedyHeuristic_TakesAWinningMove_Unconditionally()
        {
            var goals = new GoalSet(null, clearRowsTarget: 1, null, null);
            Cell[] cells = TestKit.EmptyBoard();
            TestKit.FillRow(cells, 8, holes: 0);
            GameState state = TestKit.Build(config: TestKit.Config(goals: goals), cells: cells);

            BotDecision decision = Heuristic().Choose(state, DeterministicRng.FromSeed(1));

            MoveResult applied = SimEngine.ApplyMove(state, decision.Move!);
            Assert.That(applied.Next.Status, Is.EqualTo(GameStatus.Won));
        }

        [Test]
        public void GreedyPolicies_AreDeterministic()
        {
            Cell[] cells = TestKit.EmptyBoard();
            TestKit.FillRow(cells, 5, 2, 7);
            GameState state = TestKit.Build(cells: cells);

            PlaceMove first = Heuristic().Choose(state, DeterministicRng.FromSeed(9)).Move!;
            PlaceMove second = Heuristic().Choose(state, DeterministicRng.FromSeed(123)).Move!;
            Assert.That(second.TraySlot, Is.EqualTo(first.TraySlot), "greedy ignores its rng");
            Assert.That(second.Target, Is.EqualTo(first.Target));

            PlaceMove clearA = new GreedyClearPolicy().Choose(state, DeterministicRng.FromSeed(9)).Move!;
            PlaceMove clearB = new GreedyClearPolicy().Choose(state, DeterministicRng.FromSeed(123)).Move!;
            Assert.That(clearB.Target, Is.EqualTo(clearA.Target));
        }

        [Test]
        public void RandomLegal_IsDeterministic_GivenTheSameRng()
        {
            GameState state = TestKit.Build();

            BotDecision a = new RandomLegalPolicy().Choose(state, DeterministicRng.FromSeed(77));
            BotDecision b = new RandomLegalPolicy().Choose(state, DeterministicRng.FromSeed(77));

            Assert.That(b.Move!.TraySlot, Is.EqualTo(a.Move!.TraySlot));
            Assert.That(b.Move.Target, Is.EqualTo(a.Move.Target));
            Assert.That(b.Rng, Is.EqualTo(a.Rng));
        }

        [Test]
        public void Policies_ReturnNull_WhenNoLegalMoveExists()
        {
            var cells = new Cell[BoardSpec.CellCount];
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = Cell.Coral;
            }

            GameState state = TestKit.Build(cells: cells, water: 0);

            Assert.That(new GreedyClearPolicy().Choose(state, DeterministicRng.FromSeed(1)).Move, Is.Null);
            Assert.That(Heuristic().Choose(state, DeterministicRng.FromSeed(1)).Move, Is.Null);
            Assert.That(new RandomLegalPolicy().Choose(state, DeterministicRng.FromSeed(1)).Move, Is.Null);
        }
    }
}
