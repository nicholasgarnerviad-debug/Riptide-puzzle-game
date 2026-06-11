using NUnit.Framework;
using Riptide.Core;

namespace Riptide.Core.Tests
{
    /// <summary>GDD 3.2 — endless escalation: shrinking tide interval, heavier big pieces.</summary>
    [TestFixture]
    public sealed class Section3_2_EscalationTests
    {
        private static EscalationConfig Esc(int shrinkEvery = 4, int floor = 3, int weightEvery = 25,
            int bigBonus = 2, int maxSteps = 8) =>
            new EscalationConfig(shrinkEvery, floor, weightEvery, bigBonus, maxSteps);

        [Test]
        public void Interval_ShrinksEveryNTides_AndFloors()
        {
            LevelConfig config = TestKit.Config(tideInterval: 7, escalation: Esc(shrinkEvery: 4, floor: 3));

            Assert.That(EscalationRules.EffectiveTideInterval(config, 0), Is.EqualTo(7));
            Assert.That(EscalationRules.EffectiveTideInterval(config, 3), Is.EqualTo(7));
            Assert.That(EscalationRules.EffectiveTideInterval(config, 4), Is.EqualTo(6), "GDD 3.2: shrink by 1 every 4 tides");
            Assert.That(EscalationRules.EffectiveTideInterval(config, 8), Is.EqualTo(5));
            Assert.That(EscalationRules.EffectiveTideInterval(config, 12), Is.EqualTo(4));
            Assert.That(EscalationRules.EffectiveTideInterval(config, 16), Is.EqualTo(3));
            Assert.That(EscalationRules.EffectiveTideInterval(config, 40), Is.EqualTo(3), "GDD 3.2: floor of 3");
        }

        [Test]
        public void Weights_GainBigPieceBonus_PerStep_WithCap()
        {
            LevelConfig config = TestKit.Config(escalation: Esc(weightEvery: 25, bigBonus: 2, maxSteps: 8));

            int[] atStart = EscalationRules.EffectiveWeights(config, 0, out int totalStart);
            Assert.That(atStart[(int)PieceId.Sq3], Is.EqualTo(1), "uniform base");
            Assert.That(totalStart, Is.EqualTo(20));

            int[] atStep2 = EscalationRules.EffectiveWeights(config, 50, out int totalStep2);
            Assert.That(atStep2[(int)PieceId.Sq3], Is.EqualTo(5), "base 1 + 2 bonus x 2 steps");
            Assert.That(atStep2[(int)PieceId.I5H], Is.EqualTo(5));
            Assert.That(atStep2[(int)PieceId.I5V], Is.EqualTo(5));
            Assert.That(atStep2[(int)PieceId.Mono1], Is.EqualTo(1), "small pieces untouched");
            Assert.That(totalStep2, Is.EqualTo(17 + 3 * 5));

            int[] capped = EscalationRules.EffectiveWeights(config, 100000, out _);
            Assert.That(capped[(int)PieceId.Sq3], Is.EqualTo(1 + 2 * 8), "max 8 steps");
        }

        [Test]
        public void NoEscalation_PassesBaseValuesThrough_WithoutAllocating()
        {
            LevelConfig config = TestKit.Config(tideInterval: 8);

            Assert.That(EscalationRules.EffectiveTideInterval(config, 99), Is.EqualTo(8));
            int[] weights = EscalationRules.EffectiveWeights(config, 99999, out int total);
            Assert.That(ReferenceEquals(weights, EscalationRules.EffectiveWeights(config, 0, out _)), Is.True,
                "no escalation must return the same base array, not a copy");
            Assert.That(total, Is.EqualTo(20));
        }

        [Test]
        public void Engine_RisesFaster_AsTidesPass()
        {
            // interval 3, shrink every tide, floor 1: rises land on moves 3, 5, 6, 7...
            int[] monoOnly = new int[PieceCatalog.PieceCount];
            monoOnly[(int)PieceId.Mono1] = 1;
            LevelConfig config = TestKit.Config(
                tideInterval: 3,
                escalation: Esc(shrinkEvery: 1, floor: 1, weightEvery: 1000, bigBonus: 0),
                pieceWeights: monoOnly);
            GameState state = GameState.NewGame(config, seed: 3);

            var waterByMove = new System.Collections.Generic.List<int>();
            (int col, int row)[] spots =
            {
                (0, 5), (2, 5), (4, 5), (6, 5), (8, 5), (0, 7), (2, 7), (4, 7),
            };
            for (int i = 0; i < spots.Length; i++)
            {
                int slot = FindOccupiedSlot(state);
                state = TestKit.Place(state, slot, spots[i].col, spots[i].row).Next;
                waterByMove.Add(state.WaterLevel);
            }

            Assert.That(waterByMove[2], Is.EqualTo(2), "first rise after 3 placements (interval 3)");
            Assert.That(waterByMove[4], Is.EqualTo(3), "second rise 2 placements later (interval 2)");
            Assert.That(waterByMove[5], Is.EqualTo(4), "third rise 1 placement later (interval 1)");
            Assert.That(waterByMove[6], Is.EqualTo(5), "interval stays at the floor of 1");
        }

        [Test]
        public void Engine_DealsEscalatedWeights_AtRefill()
        {
            // Big pieces get +100 weight after the very first placement; the refill
            // on move 3 must then deal almost exclusively big pieces.
            int[] monoOnly = new int[PieceCatalog.PieceCount];
            monoOnly[(int)PieceId.Mono1] = 1;
            LevelConfig config = TestKit.Config(
                tideInterval: 50,
                escalation: Esc(shrinkEvery: 100, floor: 1, weightEvery: 1, bigBonus: 100, maxSteps: 1),
                pieceWeights: monoOnly);
            GameState state = GameState.NewGame(config, seed: 8);

            for (int i = 0; i < BoardSpec.TraySize; i++)
            {
                Assert.That(state.TrayAt(i)!.Value.Piece, Is.EqualTo(PieceId.Mono1),
                    "initial deal at moveCount 0 uses base weights");
            }

            state = TestKit.Place(state, 0, 0, 5).Next;
            state = TestKit.Place(state, 1, 2, 5).Next;
            MoveResult refill = TestKit.Place(state, 2, 4, 5);

            Assert.That(refill.Events.DealtPieces.Count, Is.EqualTo(3));
            int bigCount = 0;
            foreach (TrayPiece piece in refill.Events.DealtPieces)
            {
                if (PieceCatalog.CellCountOf(piece.Piece) >= 5)
                {
                    bigCount++;
                }
            }

            Assert.That(bigCount, Is.GreaterThanOrEqualTo(1),
                "with 300:1 odds the escalated refill must include big pieces (deterministic for this seed)");
        }

        private static int FindOccupiedSlot(GameState state)
        {
            for (int i = 0; i < BoardSpec.TraySize; i++)
            {
                if (state.TrayAt(i).HasValue)
                {
                    return i;
                }
            }

            Assert.Fail("no occupied tray slot");
            return -1;
        }
    }
}
