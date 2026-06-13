using System;
using NUnit.Framework;

namespace Riptide.Core.Tests
{
    /// <summary>
    /// Audit B1 dual-pipeline proof: replays the fuzz games pinned by
    /// Tools/DeterminismFuzz (which passed a 10k-game double-replay on CoreCLR)
    /// and asserts every per-move StateHash. This same file compiles under the
    /// dotnet shim AND Unity's Mono — agreement across both runtimes is the bar,
    /// because Mono/CoreCLR divergence has caught real bugs here (DECISIONS.md).
    /// </summary>
    [TestFixture]
    public sealed class FuzzPinTests
    {
        [Test]
        public void PinnedFuzzGames_ReplayIdentically_OnThisRuntime()
        {
            EconomyConfig economy = EconomyLoader.Load(CrossPipelineFuzzPins.EconomyJson, "pins-economy");
            LevelConfig endless = ModeFactory.Endless(economy, CrossPipelineFuzzPins.RosterCount);
            LevelConfig daily = ModeFactory.Daily(economy, CrossPipelineFuzzPins.RosterCount);

            Assert.That(CrossPipelineFuzzPins.Games.Length, Is.GreaterThanOrEqualTo(16),
                "pin set sanity — the emitter writes at least this many");

            foreach ((ulong seed, bool isDaily, string encoded, ulong[] hashes)
                in CrossPipelineFuzzPins.Games)
            {
                GameState state = GameState.NewGame(isDaily ? daily : endless, seed);
                string[] tokens = encoded.Length == 0
                    ? Array.Empty<string>()
                    : encoded.Split(';');
                Assert.That(tokens.Length, Is.EqualTo(hashes.Length), $"seed {seed}: pin shape");

                for (int i = 0; i < tokens.Length; i++)
                {
                    state = SimEngine.ApplyMove(state, Decode(tokens[i])).Next;
                    Assert.That(StateHash.Compute(state), Is.EqualTo(hashes[i]),
                        $"seed {seed} ({(isDaily ? "daily" : "endless")}), move {i} '{tokens[i]}': " +
                        "per-move hash diverged from the CoreCLR fuzz — cross-pipeline determinism break");
                }
            }
        }

        private static Move Decode(string token)
        {
            switch (token[0])
            {
                case 'P':
                {
                    string[] parts = token.Substring(1).Split(',');
                    return new PlaceMove(int.Parse(parts[0]),
                        new GridPos(int.Parse(parts[1]), int.Parse(parts[2])));
                }

                case 'B':
                {
                    string[] parts = token.Substring(1).Split(',');
                    return new BubblePopMove(new GridPos(int.Parse(parts[0]), int.Parse(parts[1])));
                }

                case 'D':
                    return new DrainPumpMove();
                case 'N':
                    return new NewTideMove();
                case 'S':
                    return new PieceSwapMove(int.Parse(token.Substring(1)));
                case 'C':
                    return new ContinueMove();
                default:
                    throw new InvalidOperationException($"bad pin token '{token}'");
            }
        }
    }
}
