using NUnit.Framework;
using Riptide.Core;

namespace Riptide.Core.Tests
{
    /// <summary>
    /// Phase 0 wiring proof: the Core assembly, the test assembly, and both test
    /// pipelines (Unity Test Runner + dotnet shim) compile and run the same sources.
    /// </summary>
    [TestFixture]
    public sealed class SmokeTests
    {
        [Test]
        public void CoreAssembly_IsAliveAndPure()
        {
            Assert.That(BoardSpec.Width, Is.EqualTo(9));
            Assert.That(BoardSpec.Height, Is.EqualTo(12));
            Assert.That(PieceCatalog.PieceCount, Is.EqualTo(20));
        }
    }
}
