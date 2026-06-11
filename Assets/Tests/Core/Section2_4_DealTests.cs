using System.Collections.Generic;
using NUnit.Framework;
using Riptide.Core;

namespace Riptide.Core.Tests
{
    /// <summary>
    /// GDD 2.4 — dealing, Phase 1 scope: deterministic uniform deal on the state RNG.
    /// The weighted bag and refill guarantee land in Phase 2 (master prompt 2B).
    /// </summary>
    [TestFixture]
    public sealed class Section2_4_DealTests
    {
        [Test]
        public void NewGame_DealsAnInitialTrayOfThree()
        {
            GameState state = GameState.NewGame(TestKit.Config(), seed: 99);

            Assert.That(state.TrayPieceCount, Is.EqualTo(3));
            Assert.That(state.TraysDealt, Is.EqualTo(1));
        }

        [Test]
        public void Deal_IsDeterministic_SameSeedSameTray()
        {
            GameState a = GameState.NewGame(TestKit.Config(), seed: 123);
            GameState b = GameState.NewGame(TestKit.Config(), seed: 123);

            for (int i = 0; i < BoardSpec.TraySize; i++)
            {
                Assert.That(a.TrayAt(i), Is.EqualTo(b.TrayAt(i)), $"slot {i}");
            }

            Assert.That(a.ComputeHash(), Is.EqualTo(b.ComputeHash()));
        }

        [Test]
        public void Deal_AdvancesTheRngState()
        {
            GameState state = GameState.NewGame(TestKit.Config(), seed: 5);

            Assert.That(state.Rng, Is.Not.EqualTo(DeterministicRng.FromSeed(5)),
                "dealing consumed RNG state (GDD 2.4: draws stay on the reproducible stream)");
        }

        [Test]
        public void Deal_ColorsStayWithinThePalette()
        {
            for (ulong seed = 0; seed < 40; seed++)
            {
                GameState state = GameState.NewGame(TestKit.Config(colorCount: 6), seed);
                for (int i = 0; i < BoardSpec.TraySize; i++)
                {
                    Assert.That(state.TrayAt(i)!.Value.ColorId, Is.LessThan(6), $"seed {seed} slot {i}");
                }
            }
        }

        [Test]
        public void Deal_ReachesEveryPieceInTheCatalog()
        {
            var seen = new HashSet<PieceId>();
            for (ulong seed = 0; seed < 200 && seen.Count < PieceCatalog.PieceCount; seed++)
            {
                GameState state = GameState.NewGame(TestKit.Config(), seed);
                for (int i = 0; i < BoardSpec.TraySize; i++)
                {
                    seen.Add(state.TrayAt(i)!.Value.Piece);
                }
            }

            Assert.That(seen.Count, Is.EqualTo(PieceCatalog.PieceCount),
                "uniform deal must be able to produce all 20 masks (GDD 2.3 enumeration; see DECISIONS.md)");
        }
    }
}
