using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Riptide.Core;

namespace Riptide.Tools.BalanceRunner
{
    /// <summary>
    /// Usage: dotnet run --project Tools/BalanceRunner -c Release --
    ///        --mode endless|daily|event --games 10000 --policy gh|gc|rl [--out path.csv] [--content Assets/Resources/Content]
    ///        [--powers] (GDD §15.1 creature powers on) [--event id] (event mode: which rotation entry)
    /// Plays N seeded games headlessly and writes a raw per-game CSV plus a
    /// summary block on stdout (master prompt 3B).
    /// </summary>
    public static class Program
    {
        private sealed class GameRow
        {
            public ulong Seed;
            public int Placements;
            public string Result = "";
            public long Score;
            public int Tides;
            public int MaxWater;
            public int Spawned;
            public int Rescued;
            public int Lost;
        }

        public static int Main(string[] args)
        {
            string mode = Arg(args, "--mode") ?? "endless";
            int games = int.Parse(Arg(args, "--games") ?? "10000", CultureInfo.InvariantCulture);
            string policyName = Arg(args, "--policy") ?? "gh";
            string contentRoot = Arg(args, "--content") ?? Path.Combine("Assets", "Resources", "Content");
            string outPath = Arg(args, "--out") ?? Path.Combine("docs", "balance", $"{mode}_{policyName}_{games}.csv");

            EconomyConfig economy = EconomyLoader.Load(File.ReadAllText(Path.Combine(contentRoot, "economy.json")), "economy.json");
            CreatureRoster roster = CreatureLoader.Load(File.ReadAllText(Path.Combine(contentRoot, "creatures.json")), "creatures.json");

            ExpansionConfig expansion = ExpansionLoader.Load(
                File.ReadAllText(Path.Combine(contentRoot, "expansion.json")), "expansion.json", roster.Count);
            bool powers = Array.IndexOf(args, "--powers") >= 0;
            if (mode == "voyage")
            {
                return RunVoyage(contentRoot, economy, roster, powers ? expansion.Powers : null, games);
            }

            LevelConfig config = mode switch
            {
                "endless" => ModeFactory.Endless(economy, roster.Count),
                "daily" => ModeFactory.Daily(economy, roster.Count),
                "event" => EventSchedule.BuildConfig(FindEvent(expansion, Arg(args, "--event")), economy, roster.Count,
                    null),
                _ => throw new ArgumentException($"unknown mode '{mode}'"),
            };
            if (powers)
            {
                config = config.WithCreaturePowers(expansion.Powers);
            }

            var rows = new GameRow[games];
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Parallel.For(0, games, i =>
            {
                rows[i] = PlayOne(config, (ulong)i, policyName, economy);
            });
            sw.Stop();

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
            var csv = new StringBuilder();
            csv.AppendLine("seed,policy,mode,placements,result,score,tides,maxWater,spawned,rescued,lostCreatures");
            foreach (GameRow row in rows)
            {
                csv.Append(row.Seed).Append(',').Append(policyName).Append(',').Append(mode).Append(',')
                   .Append(row.Placements).Append(',').Append(row.Result).Append(',').Append(row.Score).Append(',')
                   .Append(row.Tides).Append(',').Append(row.MaxWater).Append(',').Append(row.Spawned).Append(',')
                   .Append(row.Rescued).Append(',').Append(row.Lost).AppendLine();
            }

            File.WriteAllText(outPath, csv.ToString());

            PrintSummary(rows, mode, policyName, games, sw.ElapsedMilliseconds, outPath);
            return 0;
        }

        /// <summary>
        /// GDD §4 Voyage targets re-measured from the SHIPPED zone files (audit
        /// follow-up): per level, N seeds under GreedyClear (3-star share) and
        /// GreedyHeuristic (completion share); reported per band.
        /// </summary>
        private static int RunVoyage(string contentRoot, EconomyConfig economy, CreatureRoster roster,
            CreaturePowersConfig? powers, int gamesPerLevel)
        {
            string[] zoneFiles = Directory.GetFiles(Path.Combine(contentRoot, "levels"), "zone*.json");
            Array.Sort(zoneFiles, (a, b) => ZoneNumber(a).CompareTo(ZoneNumber(b)));
            Console.WriteLine($"SUMMARY mode=voyage powers={(powers != null ? "on" : "off")} gamesPerLevel={gamesPerLevel}");
            foreach (string file in zoneFiles)
            {
                var levels = LevelDefLoader.LoadZone(File.ReadAllText(file), Path.GetFileName(file), economy);
                double ghSum = 0;
                int gc3StarLevels = 0;
                int rescueLevels = 0;
                foreach (LevelDef level in levels)
                {
                    LevelConfig cfg = level.ToLevelConfig(economy, roster.Count).WithCreaturePowers(powers);
                    int ghWins = 0;
                    int gcThree = 0;
                    var outcomes = new (bool ghWon, bool gcThree)[gamesPerLevel];
                    Parallel.For(0, gamesPerLevel, i =>
                    {
                        GameRow gh = PlayOne(cfg, (ulong)i, "gh", economy);
                        GameRow gc = PlayOne(cfg, (ulong)i, "gc", economy);
                        bool three = gc.Result == "won" && level.ParMoves.HasValue
                            && StarRating.For(gc.Placements, level.ParMoves.Value) == 3;
                        outcomes[i] = (gh.Result == "won", three);
                    });
                    foreach ((bool won, bool three) in outcomes)
                    {
                        ghWins += won ? 1 : 0;
                        gcThree += three ? 1 : 0;
                    }

                    ghSum += (double)ghWins / gamesPerLevel;
                    gc3StarLevels += gcThree * 2 >= gamesPerLevel ? 1 : 0;
                    rescueLevels += level.Goals.HasRescueGoal ? 1 : 0;
                }

                Console.WriteLine($"  zone {ZoneNumber(file),2}: ghComplete={(100.0 * ghSum / levels.Count).ToString("F1", CultureInfo.InvariantCulture)}%" +
                    $"  gc3StarLevels={gc3StarLevels}/{levels.Count}  rescueLevels={rescueLevels}");
            }

            return 0;
        }

        private static int ZoneNumber(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            return int.TryParse(name.Substring(4), out int n) ? n : 0;
        }

        private static EventDef FindEvent(ExpansionConfig expansion, string? id)
        {
            foreach (EventDef ev in expansion.Events)
            {
                if (id == null || ev.Id == id)
                {
                    return ev;
                }
            }

            throw new ArgumentException($"unknown event '{id}'");
        }

        private static GameRow PlayOne(LevelConfig config, ulong seed, string policyName, EconomyConfig economy)
        {
            IBotPolicy policy = policyName switch
            {
                "gh" => new GreedyHeuristicPolicy(economy.GreedyHeuristic),
                "gc" => new GreedyClearPolicy(),
                "rl" => new RandomLegalPolicy(),
                _ => throw new ArgumentException($"unknown policy '{policyName}'"),
            };

            GameState state = GameState.NewGame(config, seed);
            DeterministicRng botRng = DeterministicRng.FromSeed(seed ^ 0xB07B07UL);
            var row = new GameRow { Seed = seed, MaxWater = state.WaterLevel };
            while (state.Status == GameStatus.InProgress && row.Placements < 2000)
            {
                BotDecision decision = policy.Choose(state, botRng);
                botRng = decision.Rng;
                if (decision.Move == null)
                {
                    row.Result = "noMoveBug";
                    break;
                }

                MoveResult result = SimEngine.ApplyMove(state, decision.Move);
                state = result.Next;
                row.Placements++;
                row.Spawned += result.Events.SpawnedCreatures.Count;
                row.Rescued += result.Events.RescuedCreatures.Count;
                row.Lost += result.Events.LostCreatures.Count;
                if (state.WaterLevel > row.MaxWater)
                {
                    row.MaxWater = state.WaterLevel;
                }
            }

            row.Score = state.Score;
            row.Tides = state.Goals.TidesSurvived;
            if (row.Result.Length == 0)
            {
                row.Result = state.Status switch
                {
                    GameStatus.LostDrowned => "drown",
                    GameStatus.LostStuck => "stuck",
                    GameStatus.LostCreature => "creature",
                    GameStatus.Won => "won",
                    _ => "cap",
                };
            }

            return row;
        }

        private static void PrintSummary(GameRow[] rows, string mode, string policy, int games,
            long elapsedMs, string outPath)
        {
            var placements = new int[games];
            int drown = 0, stuck = 0, won = 0, cap = 0, creature = 0;
            long spawned = 0, rescued = 0;
            var scores = new long[games];
            for (int i = 0; i < games; i++)
            {
                placements[i] = rows[i].Placements;
                scores[i] = rows[i].Score;
                spawned += rows[i].Spawned;
                rescued += rows[i].Rescued;
                switch (rows[i].Result)
                {
                    case "drown": drown++; break;
                    case "stuck": stuck++; break;
                    case "won": won++; break;
                    case "cap": cap++; break;
                    case "creature": creature++; break;
                }
            }

            Array.Sort(placements);
            Array.Sort(scores);
            int losses = drown + stuck + creature;
            double stuckShare = losses > 0 ? 100.0 * stuck / losses : 0;
            Console.WriteLine($"SUMMARY mode={mode} policy={policy} games={games} elapsed={elapsedMs}ms");
            Console.WriteLine($"  placements p10={P(placements, 10)} p25={P(placements, 25)} median={P(placements, 50)} p75={P(placements, 75)} p90={P(placements, 90)}");
            Console.WriteLine($"  results won={won} drown={drown} stuck={stuck} creature={creature} cap={cap}");
            Console.WriteLine($"  stuckShareOfLosses={stuckShare.ToString("F1", CultureInfo.InvariantCulture)}%  winRate={(100.0 * won / games).ToString("F1", CultureInfo.InvariantCulture)}%");
            Console.WriteLine($"  rescueRate={(spawned > 0 ? (100.0 * rescued / spawned).ToString("F1", CultureInfo.InvariantCulture) : "n/a")}% (rescued {rescued} of {spawned} spawned)");
            Console.WriteLine($"  score median={P(scores, 50)} p90={P(scores, 90)}");
            Console.WriteLine($"  csv={outPath}");
        }

        private static T P<T>(T[] sorted, int percentile) =>
            sorted[Math.Min(sorted.Length - 1, sorted.Length * percentile / 100)];

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
