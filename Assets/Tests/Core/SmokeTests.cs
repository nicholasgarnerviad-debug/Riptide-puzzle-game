using NUnit.Framework;
using Riptide.Core;

namespace Riptide.Core.Tests
{
    /// <summary>
    /// Phase 0 trivial test: proves the Core assembly, the test assembly, and
    /// both test pipelines (Unity Test Runner + dotnet shim) are wired.
    /// </summary>
    [TestFixture]
    public sealed class SmokeTests
    {
        [Test]
        public void CoreAssembly_IsAliveAndPure()
        {
            Assert.That(CoreInfo.AssemblyName, Is.EqualTo("Riptide.Core"));
        }
    }
}
