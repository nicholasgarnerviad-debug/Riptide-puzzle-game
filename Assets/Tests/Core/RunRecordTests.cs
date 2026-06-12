using System.Collections.Generic;
using NUnit.Framework;

namespace Riptide.Core.Tests
{
    /// <summary>
    /// Mid-run save & resume, Core half (SAVE_RESUME_DESIGN.md): the RunRecord
    /// round-trips every move type, replay reproduces the exact state hash, and
    /// every malformation/divergence lands on the graceful-discard path.
    /// </summary>
    [TestFixture]
    public sealed class RunRecordTests
    {
        private static RunRecord Record(ulong seed, IReadOnlyList<Move> moves, ulong hash,
            string mode = "Endless", int zone = 0, int level = 0, long epochDay = 0) =>
            new RunRecord(mode, zone, level, epochDay, seed, moves, hash);

        [Test]
        public void Serialize_RoundTrips_EveryMoveType()
        {
            var moves = new List<Move>
            {
                new PlaceMove(1, new GridPos(4, 2)),
                new DrainPumpMove(),
                new BubblePopMove(new GridPos(0, 1)),
                new NewTideMove(),
                new PieceSwapMove(2),
                new ContinueMove(),
            };
            RunRecord original = Record(123456789UL, moves, 42UL, "Voyage", 3, 4, 0);

            RunRecord parsed = RunRecord.Parse(original.Serialize());

            Assert.That(parsed.Mode, Is.EqualTo("Voyage"));
            Assert.That(parsed.Zone, Is.EqualTo(3));
            Assert.That(parsed.Level, Is.EqualTo(4));
            Assert.That(parsed.Seed, Is.EqualTo(123456789UL));
            Assert.That(parsed.StateHashAfterMoves, Is.EqualTo(42UL));
            Assert.That(parsed.Moves.Count, Is.EqualTo(6));
            Assert.That(parsed.Moves[0], Is.InstanceOf<PlaceMove>());
            var place = (PlaceMove)parsed.Moves[0];
            Assert.That((place.TraySlot, (int)place.Target.Col, (int)place.Target.Row),
                Is.EqualTo((1, 4, 2)));
            Assert.That(parsed.Moves[1], Is.InstanceOf<DrainPumpMove>());
            var pop = (BubblePopMove)parsed.Moves[2];
            Assert.That(((int)pop.Target.Col, (int)pop.Target.Row), Is.EqualTo((0, 1)));
            Assert.That(parsed.Moves[3], Is.InstanceOf<NewTideMove>());
            Assert.That(((PieceSwapMove)parsed.Moves[4]).TraySlot, Is.EqualTo(2));
            Assert.That(parsed.Moves[5], Is.InstanceOf<ContinueMove>());
        }

        [Test]
        public void Serialize_UlongsAboveLongRange_Survive()
        {
            // FNV-1a 64 daily seeds and state hashes routinely exceed long.MaxValue.
            RunRecord original = Record(ulong.MaxValue - 1, new List<Move>(), ulong.MaxValue,
                "Daily", 0, 0, 20618);

            RunRecord parsed = RunRecord.Parse(original.Serialize());

            Assert.That(parsed.Seed, Is.EqualTo(ulong.MaxValue - 1));
            Assert.That(parsed.StateHashAfterMoves, Is.EqualTo(ulong.MaxValue));
            Assert.That(parsed.EpochDay, Is.EqualTo(20618L));
        }

        [Test]
        public void Parse_Malformations_ThrowContentException()
        {
            RunRecord valid = Record(7UL, new List<Move> { new DrainPumpMove() }, 9UL);
            string json = valid.Serialize();

            Assert.Throws<ContentException>(() => RunRecord.Parse("not json at all"));
            Assert.Throws<ContentException>(() => RunRecord.Parse(json.Replace("\"drain\"", "\"timeTravel\"")),
                "unknown move type");
            Assert.Throws<ContentException>(() => RunRecord.Parse(json.Replace("\"schema\": 1", "\"schema\": 99")),
                "newer schema than this build");
            Assert.Throws<ContentException>(() => RunRecord.Parse(json.Replace("\"seed\": \"7\"", "\"seed\": \"banana\"")),
                "non-numeric ulong");
        }

        [Test]
        public void Replay_Rebuilds_TheExactState()
        {
            LevelConfig config = TestKit.Config(tideInterval: 6);
            ulong seed = 424242UL;
            GameState state = GameState.NewGame(config, seed);
            var moves = new List<Move>();
            DeterministicRng rng = DeterministicRng.FromSeed(99);
            for (int i = 0; i < 25 && !state.Status.IsTerminal(); i++)
            {
                Move? move = PickRandomLegal(state, ref rng);
                if (move == null)
                {
                    break;
                }

                state = SimEngine.ApplyMove(state, move).Next;
                moves.Add(move);
            }

            Assert.That(moves.Count, Is.GreaterThan(10), "fixture sanity: a real run happened");
            RunRecord record = Record(seed, moves, StateHash.Compute(state));

            RunReplayResult result = RunReplay.Rebuild(config, record);

            Assert.That(result.Status, Is.EqualTo(RunReplayStatus.Ok));
            Assert.That(StateHash.Compute(result.State!), Is.EqualTo(StateHash.Compute(state)));
            Assert.That(result.State!.Score, Is.EqualTo(state.Score));
            Assert.That(result.State!.WaterLevel, Is.EqualTo(state.WaterLevel));
        }

        [Test]
        public void Replay_WithBoosters_Rebuilds()
        {
            LevelConfig config = TestKit.Config(tideInterval: 5, startWater: 2);
            ulong seed = 777UL;
            GameState state = GameState.NewGame(config, seed);
            var moves = new List<Move>();
            DeterministicRng rng = DeterministicRng.FromSeed(5);

            void Apply(Move move)
            {
                state = SimEngine.ApplyMove(state, move).Next;
                moves.Add(move);
            }

            Move? first = PickRandomLegal(state, ref rng);
            Apply(first!);
            Apply(new DrainPumpMove());
            Apply(new NewTideMove());
            Move? second = PickRandomLegal(state, ref rng);
            Apply(second!);

            RunRecord record = Record(seed, moves, StateHash.Compute(state));
            RunReplayResult result = RunReplay.Rebuild(config, record);

            Assert.That(result.Status, Is.EqualTo(RunReplayStatus.Ok));
            Assert.That(StateHash.Compute(result.State!), Is.EqualTo(record.StateHashAfterMoves));
        }

        [Test]
        public void Replay_HashMismatch_ReportsDiverged()
        {
            LevelConfig config = TestKit.Config();
            GameState state = GameState.NewGame(config, 1UL);
            DeterministicRng rng = DeterministicRng.FromSeed(2);
            Move move = PickRandomLegal(state, ref rng)!;
            state = SimEngine.ApplyMove(state, move).Next;

            RunRecord lying = Record(1UL, new List<Move> { move },
                StateHash.Compute(state) ^ 0xDEADUL);

            Assert.That(RunReplay.Rebuild(config, lying).Status,
                Is.EqualTo(RunReplayStatus.Diverged));
        }

        [Test]
        public void Replay_IllegalMoveMidList_ReportsIllegal_NeverThrows()
        {
            LevelConfig config = TestKit.Config();
            // A continue on a fresh, very-much-alive board is illegal by rule.
            RunRecord broken = Record(1UL, new List<Move> { new ContinueMove() }, 0UL);

            Assert.That(RunReplay.Rebuild(config, broken).Status,
                Is.EqualTo(RunReplayStatus.IllegalMove));
        }

        private static Move? PickRandomLegal(GameState state, ref DeterministicRng rng)
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

            for (int slot = 0; slot < BoardSpec.TraySize; slot++)
            {
                TrayPiece? piece = state.TrayAt(slot);
                if (!piece.HasValue)
                {
                    continue;
                }

                for (int col = 0; col < BoardSpec.Width; col++)
                {
                    for (int row = state.WaterLevel; row < BoardSpec.Height; row++)
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
