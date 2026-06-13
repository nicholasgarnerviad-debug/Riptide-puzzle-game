using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Riptide.Core;

namespace Riptide.Tools.DeterminismFuzz
{
    /// <summary>
    /// Usage: dotnet run --project Tools/DeterminismFuzz -c Release --
    ///        [--games 10000] [--pins 32] [--content Assets/Resources/Content]
    ///        [--pinsOut Assets/Tests/Core/Golden/CrossPipelineFuzzPins.cs]
    ///
    /// Audit B1. Each game: a deterministic chaos policy plays random LEGAL moves
    /// (placements, all four boosters, the continue) against an Endless or Daily
    /// config; the per-move StateHash sequence is recorded, then the move list is
    /// replayed twice from NewGame and every hash compared. Any divergence is a
    /// determinism-contract failure. The first --pins games are emitted as a
    /// committed constants file so the identical replays run under Unity Mono.
    /// </summary>
    public static class Program
    {
        private const int MaxMoves = 400;

        public static int Main(string[] args)
        {
            int games = int.Parse(Arg(args, "--games") ?? "10000", CultureInfo.InvariantCulture);
            int pins = int.Parse(Arg(args, "--pins") ?? "32", CultureInfo.InvariantCulture);
            string contentRoot = Arg(args, "--content") ?? Path.Combine("Assets", "Resources", "Content");
            string pinsOut = Arg(args, "--pinsOut")
                ?? Path.Combine("Assets", "Tests", "Core", "Golden", "CrossPipelineFuzzPins.cs");

            string economyJson = File.ReadAllText(Path.Combine(contentRoot, "economy.json"));
            EconomyConfig economy = EconomyLoader.Load(economyJson, "economy.json");
            CreatureRoster roster = CreatureLoader.Load(
                File.ReadAllText(Path.Combine(contentRoot, "creatures.json")), "creatures.json");
            LevelConfig endless = ModeFactory.Endless(economy, roster.Count);
            LevelConfig daily = ModeFactory.Daily(economy, roster.Count);

            int divergences = 0;
            int illegalMoves = 0;
            long totalMoves = 0;
            var firstFailures = new List<string>();
            var failureLock = new object();

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var pinned = new (ulong seed, bool dailyMode, string moves, ulong[] hashes)[Math.Min(pins, games)];

            Parallel.For(0, games, i =>
            {
                ulong seed = Mix((ulong)i + 1);
                bool dailyMode = (i & 1) == 1;
                LevelConfig config = dailyMode ? daily : endless;

                (List<Move> moves, List<ulong> hashes, bool illegal) = PlayChaos(config, seed);
                if (illegal)
                {
                    Interlocked.Increment(ref illegalMoves);
                }

                Interlocked.Add(ref totalMoves, moves.Count);

                for (int replay = 0; replay < 2; replay++)
                {
                    GameState state = GameState.NewGame(config, seed);
                    for (int m = 0; m < moves.Count; m++)
                    {
                        state = SimEngine.ApplyMove(state, moves[m]).Next;
                        if (StateHash.Compute(state) != hashes[m])
                        {
                            Interlocked.Increment(ref divergences);
                            lock (failureLock)
                            {
                                if (firstFailures.Count < 10)
                                {
                                    firstFailures.Add(
                                        $"seed={seed} daily={dailyMode} replay={replay} move={m} ({moves[m]})");
                                }
                            }

                            return;
                        }
                    }
                }

                if (i < pinned.Length)
                {
                    pinned[i] = (seed, dailyMode, Encode(moves), hashes.ToArray());
                }
            });
            sw.Stop();

            Console.WriteLine($"FUZZ games={games} totalMoves={totalMoves} divergences={divergences} " +
                $"illegalMoveFindings={illegalMoves} elapsed={sw.Elapsed.TotalSeconds:0.0}s");
            foreach (string failure in firstFailures)
            {
                Console.WriteLine($"  DIVERGENCE {failure}");
            }

            if (divergences == 0 && pins > 0)
            {
                File.WriteAllText(pinsOut, EmitPins(pinned, games, economyJson, roster.Count));
                Console.WriteLine($"PINS written: {pinsOut} ({pinned.Length} games)");
            }

            return divergences == 0 && illegalMoves == 0 ? 0 : 1;
        }

        /// <summary>Deterministic chaos: legal placements plus occasional boosters
        /// (when the config allows) and a continue after the first drown.</summary>
        private static (List<Move>, List<ulong>, bool illegal) PlayChaos(LevelConfig config, ulong seed)
        {
            var moves = new List<Move>(MaxMoves);
            var hashes = new List<ulong>(MaxMoves);
            GameState state = GameState.NewGame(config, seed);
            DeterministicRng chaos = DeterministicRng.FromSeed(seed ^ 0x5EEDF00DUL);
            bool illegal = false;

            for (int step = 0; step < MaxMoves; step++)
            {
                Move? move = null;
                if (state.Status.IsTerminal())
                {
                    // The continue is the only legal terminal move (drown-only, once).
                    if (state.Status == GameStatus.LostDrowned && !state.ContinueUsed
                        && config.BoostersAllowed && NextPercent(ref chaos) < 60)
                    {
                        move = new ContinueMove();
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    int roll = NextPercent(ref chaos);
                    if (config.BoostersAllowed && roll < 4)
                    {
                        move = new DrainPumpMove();
                    }
                    else if (config.BoostersAllowed && roll < 8)
                    {
                        move = PickBubblePop(state, ref chaos);
                    }
                    else if (config.BoostersAllowed && roll < 11)
                    {
                        move = new NewTideMove();
                    }
                    else if (config.BoostersAllowed && roll < 14)
                    {
                        move = PickSwap(state, ref chaos);
                    }

                    move ??= PickPlacement(state, ref chaos);
                }

                if (move == null)
                {
                    break;
                }

                try
                {
                    state = SimEngine.ApplyMove(state, move).Next;
                }
                catch (InvalidMoveException)
                {
                    // A guard above was wrong — that's itself an audit finding.
                    illegal = true;
                    break;
                }

                moves.Add(move);
                hashes.Add(StateHash.Compute(state));
            }

            return (moves, hashes, illegal);
        }

        private static Move? PickPlacement(GameState state, ref DeterministicRng chaos)
        {
            for (int attempt = 0; attempt < 80; attempt++)
            {
                int slot = NextInt(ref chaos, BoardSpec.TraySize);
                int col = NextInt(ref chaos, BoardSpec.Width);
                int row = NextInt(ref chaos, BoardSpec.Height);
                TrayPiece? piece = state.TrayAt(slot);
                if (!piece.HasValue || row < state.WaterLevel)
                {
                    continue;
                }

                var pos = new GridPos(col, row);
                if (PlacementValidator.CanPlace(state, piece.Value.Piece, pos))
                {
                    return new PlaceMove(slot, pos);
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

        private static Move? PickBubblePop(GameState state, ref DeterministicRng chaos)
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                int col = NextInt(ref chaos, BoardSpec.Width);
                int row = NextInt(ref chaos, BoardSpec.Height);
                Cell cell = state.CellAt(col, row);
                if (cell.Kind == CellKind.Block || cell.Kind == CellKind.Coral)
                {
                    return new BubblePopMove(new GridPos(col, row));
                }
            }

            return null;
        }

        private static Move? PickSwap(GameState state, ref DeterministicRng chaos)
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                int slot = NextInt(ref chaos, BoardSpec.TraySize);
                if (state.TrayAt(slot).HasValue)
                {
                    return new PieceSwapMove(slot);
                }
            }

            return null;
        }

        // ------------------------- pin emission -------------------------

        private static string Encode(List<Move> moves)
        {
            var sb = new StringBuilder(moves.Count * 6);
            foreach (Move move in moves)
            {
                if (sb.Length > 0)
                {
                    sb.Append(';');
                }

                sb.Append(move switch
                {
                    PlaceMove p => $"P{p.TraySlot},{p.Target.Col},{p.Target.Row}",
                    DrainPumpMove => "D",
                    BubblePopMove b => $"B{b.Target.Col},{b.Target.Row}",
                    NewTideMove => "N",
                    PieceSwapMove s => $"S{s.TraySlot}",
                    ContinueMove => "C",
                    _ => throw new InvalidOperationException(move.GetType().Name),
                });
            }

            return sb.ToString();
        }

        private static string EmitPins((ulong seed, bool daily, string moves, ulong[] hashes)[] pins,
            int fuzzCount, string economyJson, int rosterCount)
        {
            var sb = new StringBuilder(1 << 16);
            sb.AppendLine("// AUTO-GENERATED by Tools/DeterminismFuzz — do not edit by hand.");
            sb.AppendLine($"// Cross-pipeline determinism pins: {pins.Length} fuzzed games drawn from a");
            sb.AppendLine($"// {fuzzCount}-game double-replay fuzz that passed with zero divergences on");
            sb.AppendLine("// CoreCLR. FuzzPinTests replays them under whichever runtime compiles this");
            sb.AppendLine("// file (dotnet shim AND Unity Mono) — divergence between the two pipelines");
            sb.AppendLine("// has caught real bugs before (DECISIONS.md).");
            sb.AppendLine("namespace Riptide.Core.Tests");
            sb.AppendLine("{");
            sb.AppendLine("    public static class CrossPipelineFuzzPins");
            sb.AppendLine("    {");
            sb.AppendLine("        /// <summary>The exact economy.json the fuzz configs were built from —");
            sb.AppendLine("        /// the pins are meaningless against a drifted config.</summary>");
            sb.AppendLine("        public const string EconomyJson = @\"" + economyJson.Replace("\"", "\"\"") + "\";");
            sb.AppendLine();
            sb.AppendLine($"        public const int RosterCount = {rosterCount};");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>seed · isDaily · encoded moves · expected per-move StateHash.</summary>");
            sb.AppendLine("        public static readonly (ulong Seed, bool Daily, string Moves, ulong[] Hashes)[] Games =");
            sb.AppendLine("        {");
            foreach ((ulong seed, bool daily, string moves, ulong[] hashes) in pins)
            {
                sb.Append($"            ({seed}UL, {(daily ? "true" : "false")}, \"{moves}\", new ulong[] {{ ");
                for (int i = 0; i < hashes.Length; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(", ");
                    }

                    sb.Append(hashes[i]).Append("UL");
                }

                sb.AppendLine(" }),");
            }

            sb.AppendLine("        };");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        // ------------------------- rng plumbing -------------------------

        private static ulong Mix(ulong x)
        {
            x ^= x >> 33;
            x *= 0xFF51AFD7ED558CCDUL;
            x ^= x >> 33;
            return x == 0 ? 1UL : x;
        }

        private static int NextInt(ref DeterministicRng rng, int maxExclusive)
        {
            RngIntDraw draw = rng.NextInt(maxExclusive);
            rng = draw.Rng;
            return draw.Value;
        }

        private static int NextPercent(ref DeterministicRng rng) => NextInt(ref rng, 100);

        private static string? Arg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
