using NUnit.Framework;
using Riptide.Core;

namespace Riptide.Core.Tests
{
    /// <summary>
    /// GDD 8.3 determinism contract: pure ApplyMove, reproducible streams,
    /// identical (levelDef, seed, moves) -> identical end-state hash.
    /// </summary>
    [TestFixture]
    public sealed class DeterminismTests
    {
        private static LevelConfig RunnerConfig() => TestKit.Config(
            startWater: 1,
            minWater: 1,
            tideInterval: 5,
            spawnEveryTrays: 3,
            awardTideSurvival: true);

        [Test]
        public void ApplyMove_DoesNotMutateTheInputState()
        {
            Cell[] cells = TestKit.EmptyBoard();
            TestKit.FillRow(cells, 5, holes: 4);
            GameState state = TestKit.Build(cells: cells);
            ulong before = state.ComputeHash();

            MoveResult result = TestKit.Place(state, 0, 4, 5);

            Assert.That(state.ComputeHash(), Is.EqualTo(before), "GDD 8.2: ApplyMove is pure");
            Assert.That(state.CellAt(0, 5).Kind, Is.EqualTo(CellKind.Block), "input board untouched");
            Assert.That(state.TrayAt(0).HasValue, Is.True, "input tray untouched");
            Assert.That(result.Next, Is.Not.SameAs(state));
        }

        [Test]
        public void SameSeedAndMoves_Produce_IdenticalEndStateHashes_Across1000RandomGames()
        {
            int finished = 0;
            for (ulong seed = 0; seed < 1000; seed++)
            {
                ulong first = PlayRandomGame(seed, out GameStatus statusA);
                ulong second = PlayRandomGame(seed, out GameStatus statusB);

                Assert.That(second, Is.EqualTo(first), $"seed {seed} diverged");
                Assert.That(statusB, Is.EqualTo(statusA));
                if (statusA != GameStatus.InProgress)
                {
                    finished++;
                }
            }

            Assert.That(finished, Is.GreaterThan(0), "sanity: some games must reach a terminal state");
        }

        [Test]
        public void DifferentSeeds_DivergeFromTheFirstDeal()
        {
            GameState a = GameState.NewGame(RunnerConfig(), seed: 1);
            GameState b = GameState.NewGame(RunnerConfig(), seed: 2);

            Assert.That(a.ComputeHash(), Is.Not.EqualTo(b.ComputeHash()));
        }

        [Test]
        public void RngStream_YieldsTheSameSequence_ForTheSameSeed()
        {
            DeterministicRng first = DeterministicRng.FromSeed(7);
            DeterministicRng second = DeterministicRng.FromSeed(7);
            for (int i = 0; i < 8; i++)
            {
                RngDraw drawA = first.NextUInt64();
                RngDraw drawB = second.NextUInt64();
                Assert.That(drawB.Value, Is.EqualTo(drawA.Value), $"draw {i}");
                first = drawA.Rng;
                second = drawB.Rng;
            }
        }

        [Test]
        public void Restore_RoundTrip_PreservesTheHash()
        {
            GameState state = GameState.NewGame(RunnerConfig(), seed: 77);
            DeterministicRng testRng = DeterministicRng.FromSeed(1234);
            for (int i = 0; i < 5 && state.Status == GameStatus.InProgress; i++)
            {
                PlaceMove? move = PickRandomLegal(state, ref testRng);
                Assert.That(move, Is.Not.Null);
                state = SimEngine.ApplyMove(state, move!).Next;
            }

            var cells = new Cell[BoardSpec.CellCount];
            for (int row = 0; row < BoardSpec.Height; row++)
            {
                for (int col = 0; col < BoardSpec.Width; col++)
                {
                    cells[BoardSpec.IndexOf(col, row)] = state.CellAt(col, row);
                }
            }

            var tray = new TrayPiece?[BoardSpec.TraySize];
            for (int i = 0; i < BoardSpec.TraySize; i++)
            {
                tray[i] = state.TrayAt(i);
            }

            GameState restored = GameState.Restore(
                state.Config, cells, tray, state.WaterLevel, state.TideCounter, state.Score,
                state.ComboChain, state.RescueStreak, state.MoveCount, state.TraysDealt, state.Rng,
                new GoalState(state.Config.Goals, state.Goals.Rescued, state.Goals.RowsCleared, state.Goals.TidesSurvived),
                state.Status);

            Assert.That(restored.ComputeHash(), Is.EqualTo(state.ComputeHash()),
                "save-load (Phase 6) depends on lossless state round-trips");
        }

        private static ulong PlayRandomGame(ulong seed, out GameStatus finalStatus)
        {
            GameState state = GameState.NewGame(RunnerConfig(), seed);
            DeterministicRng testRng = DeterministicRng.FromSeed(seed ^ 0xA5A5A5A5UL);
            int moves = 0;
            while (state.Status == GameStatus.InProgress && moves < 250)
            {
                PlaceMove? move = PickRandomLegal(state, ref testRng);
                if (move == null)
                {
                    Assert.Fail($"seed {seed}: InProgress state with no legal move — the stuck check (GDD 2.3) failed");
                }

                state = SimEngine.ApplyMove(state, move!).Next;
                moves++;
            }

            finalStatus = state.Status;
            return state.ComputeHash();
        }

        private static PlaceMove? PickRandomLegal(GameState state, ref DeterministicRng rng)
        {
            for (int attempt = 0; attempt < 64; attempt++)
            {
                RngIntDraw slotDraw = rng.NextInt(BoardSpec.TraySize);
                rng = slotDraw.Rng;
                RngIntDraw colDraw = rng.NextInt(BoardSpec.Width);
                rng = colDraw.Rng;
                RngIntDraw rowDraw = rng.NextInt(BoardSpec.Height);
                rng = rowDraw.Rng;

                TrayPiece? piece = state.TrayAt(slotDraw.Value);
                if (!piece.HasValue || rowDraw.Value < state.WaterLevel)
                {
                    continue;
                }

                var pos = new GridPos(colDraw.Value, rowDraw.Value);
                if (PlacementValidator.CanPlace(state, piece.Value.Piece, pos))
                {
                    return new PlaceMove(slotDraw.Value, pos);
                }
            }

            // Deterministic fallback: first legal placement in scan order.
            for (int slot = 0; slot < BoardSpec.TraySize; slot++)
            {
                TrayPiece? piece = state.TrayAt(slot);
                if (!piece.HasValue)
                {
                    continue;
                }

                for (int row = state.WaterLevel; row < BoardSpec.Height; row++)
                {
                    for (int col = 0; col < BoardSpec.Width; col++)
                    {
                        var pos = new GridPos(col, row);
                        if (PlacementValidator.CanPlace(state, piece.Value.Piece, pos))
                        {
                            return new PlaceMove(slot, pos);
                        }
                    }
                }
            }

            return null;
        }
    }
}
