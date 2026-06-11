using NUnit.Framework;
using Riptide.Core;

namespace Riptide.Core.Tests
{
    /// <summary>GDD 2.4 — weighted bag + refill guarantee with deterministic redraw.</summary>
    [TestFixture]
    public sealed class Section2_4_BagAndGuaranteeTests
    {
        private static int[] OnlyWeight(PieceId piece, int weight = 1)
        {
            var weights = new int[PieceCatalog.PieceCount];
            weights[(int)piece] = weight;
            return weights;
        }

        [Test]
        public void WeightedBag_ZeroWeightPieces_AreNeverDealt()
        {
            LevelConfig config = TestKit.Config(pieceWeights: OnlyWeight(PieceId.Mono1));
            for (ulong seed = 0; seed < 50; seed++)
            {
                GameState state = GameState.NewGame(config, seed);
                for (int i = 0; i < BoardSpec.TraySize; i++)
                {
                    Assert.That(state.TrayAt(i)!.Value.Piece, Is.EqualTo(PieceId.Mono1),
                        $"seed {seed} slot {i}: zero-weight pieces must never appear");
                }
            }
        }

        [Test]
        public void WeightedBag_BiasesTowardHeavyWeights()
        {
            var weights = new int[PieceCatalog.PieceCount];
            weights[(int)PieceId.Mono1] = 99;
            weights[(int)PieceId.I5V] = 1;
            LevelConfig config = TestKit.Config(pieceWeights: weights);

            int monoCount = 0;
            int total = 0;
            DeterministicRng rng = DeterministicRng.FromSeed(7);
            for (int deal = 0; deal < 200; deal++)
            {
                TrayDeal result = Dealer.DealTray(rng, config);
                rng = result.Rng;
                foreach (TrayPiece piece in result.Pieces)
                {
                    total++;
                    if (piece.Piece == PieceId.Mono1) monoCount++;
                }
            }

            Assert.That(total, Is.EqualTo(600));
            Assert.That(monoCount, Is.GreaterThan(560), "a 99:1 weight should dominate the deals");
        }

        [Test]
        public void WeightedBag_UniformWeights_AreBitIdenticalToPhase1Stream()
        {
            // DECISIONS.md: with uniform weights the bag must reproduce NextInt(20)
            // exactly, protecting all Phase 1 determinism results and the goldens.
            LevelConfig config = TestKit.Config();
            DeterministicRng rng = DeterministicRng.FromSeed(99);
            TrayDeal deal = Dealer.DealTray(rng, config);

            DeterministicRng replay = DeterministicRng.FromSeed(99);
            for (int i = 0; i < BoardSpec.TraySize; i++)
            {
                RngIntDraw pieceDraw = replay.NextInt(PieceCatalog.PieceCount);
                replay = pieceDraw.Rng;
                RngIntDraw colorDraw = replay.NextInt(config.DealColorCount);
                replay = colorDraw.Rng;

                Assert.That(deal.Pieces[i].Piece, Is.EqualTo((PieceId)pieceDraw.Value), $"slot {i} piece");
                Assert.That(deal.Pieces[i].ColorId, Is.EqualTo((byte)colorDraw.Value), $"slot {i} color");
            }

            Assert.That(deal.Rng, Is.EqualTo(replay), "the advanced rng state must match the manual replay");
        }

        private static Cell[] BoardWhereOnlyMonoFits()
        {
            // Coral everywhere except scattered single-cell holes that no piece
            // larger than a monomino can use.
            var cells = new Cell[BoardSpec.CellCount];
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = Cell.Coral;
            }

            cells[BoardSpec.IndexOf(2, 3)] = Cell.Empty;
            cells[BoardSpec.IndexOf(6, 7)] = Cell.Empty;
            return cells;
        }

        [Test]
        public void RefillGuarantee_RedrawsUntilSomethingFits()
        {
            LevelConfig config = TestKit.Config();
            Cell[] board = BoardWhereOnlyMonoFits();

            int dealsNeedingRedraw = 0;
            for (ulong seed = 0; seed < 200; seed++)
            {
                TrayDeal deal = Dealer.DealTrayWithGuarantee(DeterministicRng.FromSeed(seed), config, board, 0);
                if (deal.RedrawRounds > 0)
                {
                    dealsNeedingRedraw++;
                }

                if (!deal.GuaranteeExhausted)
                {
                    bool anyFits = false;
                    foreach (TrayPiece piece in deal.Pieces)
                    {
                        anyFits |= piece.Piece == PieceId.Mono1;
                    }

                    Assert.That(anyFits, Is.True, $"seed {seed}: guarantee returned without a placeable piece");
                }
            }

            Assert.That(dealsNeedingRedraw, Is.GreaterThan(50),
                "on a mono-only board most uniform deals must engage the redraw mechanism");
        }

        [Test]
        public void RefillGuarantee_IsDeterministic()
        {
            LevelConfig config = TestKit.Config();
            Cell[] board = BoardWhereOnlyMonoFits();

            TrayDeal first = Dealer.DealTrayWithGuarantee(DeterministicRng.FromSeed(1234), config, board, 0);
            TrayDeal second = Dealer.DealTrayWithGuarantee(DeterministicRng.FromSeed(1234), config, board, 0);

            Assert.That(second.RedrawRounds, Is.EqualTo(first.RedrawRounds));
            Assert.That(second.GuaranteeExhausted, Is.EqualTo(first.GuaranteeExhausted));
            Assert.That(second.Rng, Is.EqualTo(first.Rng));
            for (int i = 0; i < BoardSpec.TraySize; i++)
            {
                Assert.That(second.Pieces[i], Is.EqualTo(first.Pieces[i]), $"slot {i}");
            }
        }

        [Test]
        public void RefillGuarantee_Exhausts_WhenNothingCanEverFit()
        {
            var cells = new Cell[BoardSpec.CellCount];
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = Cell.Coral;
            }

            LevelConfig config = TestKit.Config();
            TrayDeal deal = Dealer.DealTrayWithGuarantee(DeterministicRng.FromSeed(5), config, cells, 0);

            Assert.That(deal.GuaranteeExhausted, Is.True, "GDD 2.4: after redraws the deal stands — a fair loss");
            Assert.That(deal.RedrawRounds, Is.EqualTo(Dealer.MaxRedrawRounds));
            Assert.That(deal.Pieces.Count, Is.EqualTo(3), "the final deal still has three pieces");
        }

        [Test]
        public void RefillGuarantee_NeverRedraws_OnAnOpenBoard()
        {
            LevelConfig config = TestKit.Config();
            var empty = new Cell[BoardSpec.CellCount];

            TrayDeal guaranteed = Dealer.DealTrayWithGuarantee(DeterministicRng.FromSeed(42), config, empty, 1);
            TrayDeal raw = Dealer.DealTray(DeterministicRng.FromSeed(42), config);

            Assert.That(guaranteed.RedrawRounds, Is.EqualTo(0));
            Assert.That(guaranteed.Rng, Is.EqualTo(raw.Rng), "no redraw = identical rng stream to the raw deal");
            for (int i = 0; i < BoardSpec.TraySize; i++)
            {
                Assert.That(guaranteed.Pieces[i], Is.EqualTo(raw.Pieces[i]), $"slot {i}");
            }
        }

        [Test]
        public void Adversarial_10000NearFullBoards_GuaranteeHoldsOrFlagsLegitStuck()
        {
            LevelConfig config = TestKit.Config();
            int exhausted = 0;
            int redrawn = 0;
            for (ulong boardSeed = 0; boardSeed < 10_000; boardSeed++)
            {
                DeterministicRng gen = DeterministicRng.FromSeed(boardSeed * 2654435761UL + 1);
                var cells = new Cell[BoardSpec.CellCount];
                int waterLevel = (int)(boardSeed % 7);

                // Fill 80–97% of cells; ~12% of filled cells are coral.
                int densityPercent = 80 + (int)(boardSeed % 18);
                for (int i = 0; i < cells.Length; i++)
                {
                    RngIntDraw fillRoll = gen.NextInt(100);
                    gen = fillRoll.Rng;
                    if (fillRoll.Value < densityPercent)
                    {
                        RngIntDraw kindRoll = gen.NextInt(100);
                        gen = kindRoll.Rng;
                        cells[i] = kindRoll.Value < 12 ? Cell.Coral : Cell.Block(0);
                    }
                }

                TrayDeal deal = Dealer.DealTrayWithGuarantee(
                    DeterministicRng.FromSeed(boardSeed ^ 0xBADC0FFEUL), config, cells, waterLevel);
                if (deal.RedrawRounds > 0)
                {
                    redrawn++;
                }

                bool anyPlaceable = false;
                foreach (TrayPiece piece in deal.Pieces)
                {
                    anyPlaceable |= PlacementValidator.AnyPlacementExistsRaw(cells, waterLevel, piece.Piece);
                }

                if (deal.GuaranteeExhausted)
                {
                    exhausted++;
                    Assert.That(anyPlaceable, Is.False,
                        $"board {boardSeed}: flagged exhausted but a piece fits — guarantee under-tried");
                }
                else
                {
                    Assert.That(anyPlaceable, Is.True,
                        $"board {boardSeed}: deal returned with nothing placeable and no exhaustion flag");
                }
            }

            Assert.That(redrawn, Is.GreaterThan(0), "sanity: adversarial boards must exercise redraws");
            Assert.That(exhausted, Is.GreaterThan(0), "sanity: some boards must be legitimately stuck");
        }
    }
}
