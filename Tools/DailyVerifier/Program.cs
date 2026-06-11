using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Riptide.Core;

namespace Riptide.Tools.DailyVerifier
{
    /// <summary>
    /// Verifies N consecutive daily seeds are completable: GreedyHeuristic first,
    /// then GreedyClear, then 20 RandomLegal attempts (GDD 8.3: "at least one
    /// policy"). Usage: dotnet run --project Tools/DailyVerifier -c Release --
    ///                  [--start yyyy-MM-dd] [--days 365] [--content Assets/Resources/Content]
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            string startText = Arg(args, "--start") ?? "2026-06-11";
            int days = int.Parse(Arg(args, "--days") ?? "365", CultureInfo.InvariantCulture);
            string contentRoot = Arg(args, "--content") ?? Path.Combine("Assets", "Resources", "Content");

            EconomyConfig economy = EconomyLoader.Load(File.ReadAllText(Path.Combine(contentRoot, "economy.json")), "economy.json");
            CreatureRoster roster = CreatureLoader.Load(File.ReadAllText(Path.Combine(contentRoot, "creatures.json")), "creatures.json");
            LevelConfig config = ModeFactory.Daily(economy, roster.Count);

            DateTime start = DateTime.ParseExact(startText, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var results = new string[days];
            int[] failures = { 0 };

            Parallel.For(0, days, i =>
            {
                DateTime date = start.AddDays(i);
                ulong seed = DailySeed.For(date.Year, date.Month, date.Day);
                (string verifiedBy, int moves, long score) = Verify(config, economy, seed);
                if (verifiedBy == "NONE")
                {
                    System.Threading.Interlocked.Increment(ref failures[0]);
                }

                results[i] = $"{date:yyyy-MM-dd},{seed},{verifiedBy},{moves},{score}";
            });

            var csv = new StringBuilder();
            csv.AppendLine("date,seed,verifiedBy,moves,score");
            foreach (string line in results)
            {
                csv.AppendLine(line);
            }

            string outPath = Path.Combine("docs", "balance", "daily_verification.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
            File.WriteAllText(outPath, csv.ToString());

            Console.WriteLine($"verified {days - failures[0]}/{days} dailies from {startText}; csv={outPath}");
            if (failures[0] > 0)
            {
                Console.WriteLine("DAILY VERIFICATION FAILED — unplayable seeds exist.");
                return 1;
            }

            Console.WriteLine("DAILY VERIFICATION OK.");
            return 0;
        }

        private static (string verifiedBy, int moves, long score) Verify(LevelConfig config, EconomyConfig economy, ulong seed)
        {
            (bool won, int moves, long score) = Play(config, new GreedyHeuristicPolicy(economy.GreedyHeuristic), seed, 0xB07UL);
            if (won)
            {
                return ("gh", moves, score);
            }

            (won, moves, score) = Play(config, new GreedyClearPolicy(), seed, 0xB07UL);
            if (won)
            {
                return ("gc", moves, score);
            }

            // GH is deterministic — one line per seed. Completability needs trajectory
            // search: randomize the first 1-3 moves, then play GH. Each success is a
            // reproducible witness line (DECISIONS.md 2026-06-11).
            for (ulong attempt = 1; attempt <= 200; attempt++)
            {
                (won, moves, score) = PlayMixed(config, economy, seed, attempt);
                if (won)
                {
                    return ($"gh-r{attempt}", moves, score);
                }
            }

            return ("NONE", 0, 0);
        }

        private static (bool won, int moves, long score) PlayMixed(LevelConfig config, EconomyConfig economy,
            ulong seed, ulong attempt)
        {
            GameState state = GameState.NewGame(config, seed);
            DeterministicRng botRng = DeterministicRng.FromSeed(seed ^ (attempt * 0x9E3779B97F4A7C15UL));
            var random = new RandomLegalPolicy();
            var greedy = new GreedyHeuristicPolicy(economy.GreedyHeuristic);
            int randomOpeners = 1 + (int)(attempt % 5);
            int steps = 0;
            while (state.Status == GameStatus.InProgress && steps < 600)
            {
                IBotPolicy policy = steps < randomOpeners ? (IBotPolicy)random : greedy;
                BotDecision decision = policy.Choose(state, botRng);
                botRng = decision.Rng;
                if (decision.Move == null)
                {
                    break;
                }

                state = SimEngine.ApplyMove(state, decision.Move).Next;
                steps++;
            }

            return (state.Status == GameStatus.Won, state.MoveCount, state.Score);
        }

        private static (bool won, int moves, long score) Play(LevelConfig config, IBotPolicy policy, ulong seed, ulong botSeed)
        {
            GameState state = GameState.NewGame(config, seed);
            DeterministicRng botRng = DeterministicRng.FromSeed(seed ^ botSeed);
            int steps = 0;
            while (state.Status == GameStatus.InProgress && steps < 600)
            {
                BotDecision decision = policy.Choose(state, botRng);
                botRng = decision.Rng;
                if (decision.Move == null)
                {
                    break;
                }

                state = SimEngine.ApplyMove(state, decision.Move).Next;
                steps++;
            }

            return (state.Status == GameStatus.Won, state.MoveCount, state.Score);
        }

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
