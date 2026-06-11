using NUnit.Framework;
using Riptide.Core;

namespace Riptide.Core.Tests
{
    /// <summary>GDD 3.3 / master prompt 2D — daily seed derivation. Pinned values live in RngGoldenTests.</summary>
    [TestFixture]
    public sealed class DailySeedTests
    {
        [Test]
        public void SameDate_AlwaysYieldsTheSameSeed()
        {
            Assert.That(DailySeed.For(2026, 6, 11), Is.EqualTo(DailySeed.For(2026, 6, 11)),
                "same board, same trays, worldwide (GDD 3.3)");
        }

        [Test]
        public void DifferentDates_YieldDifferentSeeds()
        {
            Assert.That(DailySeed.For(2026, 6, 11), Is.Not.EqualTo(DailySeed.For(2026, 6, 12)));
            Assert.That(DailySeed.For(2026, 6, 11), Is.Not.EqualTo(DailySeed.For(2026, 7, 11)));
            Assert.That(DailySeed.For(2026, 6, 11), Is.Not.EqualTo(DailySeed.For(2027, 6, 11)));
        }

        [Test]
        public void OutOfRangeDates_AreRejected()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => DailySeed.For(2026, 13, 1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => DailySeed.For(2026, 0, 1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => DailySeed.For(2026, 6, 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => DailySeed.For(2026, 6, 32));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => DailySeed.For(1999, 6, 11));
        }

        [Test]
        public void SeededDaily_ProducesIdenticalGames()
        {
            ulong seed = DailySeed.For(2026, 6, 11);
            GameState a = GameState.NewGame(TestKit.Config(), seed);
            GameState b = GameState.NewGame(TestKit.Config(), seed);

            Assert.That(a.ComputeHash(), Is.EqualTo(b.ComputeHash()),
                "everyone's daily run starts from the same state (GDD 3.3)");
        }
    }
}
