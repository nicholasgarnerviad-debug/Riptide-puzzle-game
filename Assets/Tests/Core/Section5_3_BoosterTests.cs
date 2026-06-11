using NUnit.Framework;
using Riptide.Core;

namespace Riptide.Core.Tests
{
    /// <summary>
    /// GDD 5.3 boosters as recorded moves: deterministic effects, replay-exact,
    /// rejected in daily mode (contract 6A/6B).
    /// </summary>
    [TestFixture]
    public sealed class Section5_3_BoosterTests
    {
        [Test]
        public void DrainPump_LowersWaterByTwo_FlooredAtMin()
        {
            GameState state = TestKit.Build(config: TestKit.Config(startWater: 5, minWater: 1), water: 5);

            MoveResult result = SimEngine.ApplyMove(state, new DrainPumpMove());
            Assert.That(result.Next.WaterLevel, Is.EqualTo(3));
            Assert.That(result.Events.DrainAmount, Is.EqualTo(2));

            GameState nearFloor = TestKit.Build(config: TestKit.Config(startWater: 2, minWater: 1), water: 2);
            Assert.That(SimEngine.ApplyMove(nearFloor, new DrainPumpMove()).Next.WaterLevel, Is.EqualTo(1),
                "GDD 5.3: floored at minWaterLevel");
        }

        [Test]
        public void DrainPump_NeverTicksTheTide_OrMoveCount_OrCombo()
        {
            GameState state = TestKit.Build(
                config: TestKit.Config(startWater: 5, minWater: 1, tideInterval: 3),
                water: 5, tide: 2, comboChain: 2, moveCount: 7);

            MoveResult result = SimEngine.ApplyMove(state, new DrainPumpMove());

            Assert.That(result.Next.TideCounter, Is.EqualTo(2), "boosters are not placements (DECISIONS)");
            Assert.That(result.Next.MoveCount, Is.EqualTo(7), "MoveCount counts placements only");
            Assert.That(result.Next.ComboChain, Is.EqualTo(2), "combo untouched");
            Assert.That(result.Events.TideRose, Is.False);
        }

        [Test]
        public void BubblePop_RemovesBlockOrCoral_EvenSubmerged()
        {
            Cell[] cells = TestKit.EmptyBoard();
            cells[BoardSpec.IndexOf(3, 5)] = Cell.Block(2);
            cells[BoardSpec.IndexOf(4, 1)] = Cell.Coral;
            GameState state = TestKit.Build(config: TestKit.Config(startWater: 2, minWater: 2), cells: cells, water: 2);

            MoveResult popBlock = SimEngine.ApplyMove(state, new BubblePopMove(new GridPos(3, 5)));
            Assert.That(popBlock.Next.CellAt(3, 5).IsEmpty, Is.True);
            Assert.That(popBlock.Events.RemovedCells, Is.EqualTo(new[] { new GridPos(3, 5) }));

            MoveResult popCoral = SimEngine.ApplyMove(popBlock.Next, new BubblePopMove(new GridPos(4, 1)));
            Assert.That(popCoral.Next.CellAt(4, 1).IsEmpty, Is.True,
                "submerged coral is a legal target — the death-spiral valve (GDD 13)");
        }

        [Test]
        public void BubblePop_RejectsEmptyAndCreatureTargets()
        {
            Cell[] cells = TestKit.EmptyBoard();
            cells[BoardSpec.IndexOf(5, 5)] = Cell.Creature(1);
            GameState state = TestKit.Build(cells: cells);

            Assert.Throws<InvalidMoveException>(() => SimEngine.ApplyMove(state, new BubblePopMove(new GridPos(0, 8))),
                "empty cell");
            Assert.Throws<InvalidMoveException>(() => SimEngine.ApplyMove(state, new BubblePopMove(new GridPos(5, 5))),
                "creatures are never targets");
        }

        [Test]
        public void NewTide_RerollsDeterministically_AndCountsAsADeal()
        {
            GameState state = GameState.NewGame(TestKit.Config(spawnEveryTrays: 2), seed: 31);

            MoveResult a = SimEngine.ApplyMove(state, new NewTideMove());
            MoveResult b = SimEngine.ApplyMove(state, new NewTideMove());

            Assert.That(a.Next.ComputeHash(), Is.EqualTo(b.Next.ComputeHash()), "same state, same reroll");
            Assert.That(a.Next.TraysDealt, Is.EqualTo(2), "a reroll is a dealt tray");
            Assert.That(a.Events.DealtPieces.Count, Is.EqualTo(3), "full fresh tray (DECISIONS)");
            Assert.That(a.Events.SpawnedCreatures.Count, Is.EqualTo(1), "tray #2 hits the spawn cadence");
            Assert.That(a.Next.Rng, Is.Not.EqualTo(state.Rng), "draws consumed from the state stream");
        }

        [Test]
        public void Boosters_AreRejected_InDailyMode()
        {
            LevelConfig daily = ModeFactory.Daily(TestKit.Economy(), 8);
            GameState state = GameState.NewGame(daily, seed: 9);

            Assert.Throws<InvalidMoveException>(() => SimEngine.ApplyMove(state, new DrainPumpMove()),
                "GDD 5.3: zero boosters in the daily (contract 6B)");
            Assert.Throws<InvalidMoveException>(() => SimEngine.ApplyMove(state, new NewTideMove()));
            Assert.Throws<InvalidMoveException>(() => SimEngine.ApplyMove(state, new BubblePopMove(new GridPos(0, 0))));
        }

        [Test]
        public void BoosteredGame_ReplaysExactly_FromTheMoveList()
        {
            // Contract 6A: record a mixed game, replay the list, hashes must match.
            LevelConfig config = TestKit.Config(startWater: 3, minWater: 1, tideInterval: 4);
            var moves = new System.Collections.Generic.List<Move>();
            GameState state = GameState.NewGame(config, seed: 77);
            var policy = new GreedyClearPolicy();
            DeterministicRng rng = DeterministicRng.FromSeed(5);

            for (int i = 0; i < 6; i++)
            {
                BotDecision decision = policy.Choose(state, rng);
                rng = decision.Rng;
                moves.Add(decision.Move!);
                state = SimEngine.ApplyMove(state, decision.Move!).Next;
            }

            var pump = new DrainPumpMove();
            moves.Add(pump);
            state = SimEngine.ApplyMove(state, pump).Next;

            var reroll = new NewTideMove();
            moves.Add(reroll);
            state = SimEngine.ApplyMove(state, reroll).Next;

            for (int i = 0; i < 4 && state.Status == GameStatus.InProgress; i++)
            {
                BotDecision decision = policy.Choose(state, rng);
                rng = decision.Rng;
                moves.Add(decision.Move!);
                state = SimEngine.ApplyMove(state, decision.Move!).Next;
            }

            ulong recorded = state.ComputeHash();

            GameState replay = GameState.NewGame(config, seed: 77);
            foreach (Move move in moves)
            {
                replay = SimEngine.ApplyMove(replay, move).Next;
            }

            Assert.That(replay.ComputeHash(), Is.EqualTo(recorded),
                "GDD 5.3: booster use is recorded in the move list, so replays stay deterministic");
        }

        [Test]
        public void BoosterPrices_LoadFromEconomy()
        {
            BoosterPrices prices = TestKit.Economy().Boosters;

            Assert.That(prices.DrainPump, Is.EqualTo(150), "GDD 5.3");
            Assert.That(prices.BubblePop, Is.EqualTo(100));
            Assert.That(prices.NewTide, Is.EqualTo(120));
        }
    }
}
