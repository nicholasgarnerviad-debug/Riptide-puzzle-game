using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Riptide.Core;

namespace Riptide.Tools.LevelGenerator
{
    /// <summary>
    /// Deterministic level generator (master seed fixed): 10 zones x 20 levels,
    /// goals and presets per zone recipe, each level bot-verified into its zone's
    /// completion window and stamped with a bot-computed par (GDD 3.1/4).
    /// Usage: dotnet run --project Tools/LevelGenerator -c Release [-- contentRoot]
    /// </summary>
    public static class Program
    {
        private const ulong MasterSeed = 20260611UL;
        private const int VerifySeeds = 30;
        private const int MaxAttempts = 60;

        // GDD 4 difficulty band table (initial values, bot-verified below).
        private static readonly int[] ZoneTideInterval = { 8, 7, 7, 6, 6, 6, 5, 5, 5, 5 };
        private static readonly int[] ZoneStartWater = { 1, 1, 2, 2, 2, 2, 2, 3, 3, 3 };

        // Per-zone GH completion windows; zone 10 is the GDD 4 30–50% target band.
        private static readonly double[] ZoneMinRate = { 0.90, 0.90, 0.75, 0.75, 0.65, 0.65, 0.55, 0.55, 0.45, 0.30 };
        private static readonly double[] ZoneMaxRate = { 1.00, 1.00, 1.00, 1.00, 1.00, 1.00, 0.95, 0.90, 0.80, 0.50 };

        public static int Main(string[] args)
        {
            string contentRoot = args.Length > 0 ? args[0] : Path.Combine("Assets", "Resources", "Content");
            EconomyConfig economy = EconomyLoader.Load(File.ReadAllText(Path.Combine(contentRoot, "economy.json")), "economy.json");
            CreatureRoster roster = CreatureLoader.Load(File.ReadAllText(Path.Combine(contentRoot, "creatures.json")), "creatures.json");

            var report = new StringBuilder();
            report.AppendLine("id,zone,goals,attempts,ghRate,par,gcMedian,gc3Star");
            var bandSummaries = new List<string>();
            bool allZonesOk = true;

            for (int zone = 1; zone <= 10; zone++)
            {
                var levels = new LevelSpec[20];
                Parallel.For(0, 20, i =>
                {
                    levels[i] = GenerateLevel(zone, i + 1, economy, roster.Count);
                });

                WriteZoneFile(contentRoot, zone, levels);

                int gcThreeStars = 0;
                foreach (LevelSpec level in levels)
                {
                    if (level.GcThreeStar)
                    {
                        gcThreeStars++;
                    }

                    report.Append(level.Id).Append(',').Append(zone).Append(',').Append(level.GoalText).Append(',')
                          .Append(level.Attempts).Append(',')
                          .Append(level.GhRate.ToString("F2", CultureInfo.InvariantCulture)).Append(',')
                          .Append(level.Par).Append(',').Append(level.GcMedian).Append(',')
                          .Append(level.GcThreeStar ? 1 : 0).AppendLine();
                }

                double avgRate = levels.Average(l => l.GhRate);
                string summary = $"zone {zone}: ghRate avg {avgRate:F2} (window {ZoneMinRate[zone - 1]:F2}-{ZoneMaxRate[zone - 1]:F2}), GC 3-star {gcThreeStars}/20";
                bandSummaries.Add(summary);
                Console.WriteLine(summary);

                if (zone == 1 && gcThreeStars < 16)
                {
                    Console.WriteLine("  !! GDD 4 band-1 target missed: GreedyClear 3-stars < 80% of levels");
                    allZonesOk = false;
                }

                if (zone == 10 && (avgRate < 0.30 || avgRate > 0.50))
                {
                    Console.WriteLine("  !! GDD 4 band-10 target missed: GH completion outside 30-50%");
                    allZonesOk = false;
                }
            }

            string balanceDir = Path.Combine("docs", "balance");
            Directory.CreateDirectory(balanceDir);
            File.WriteAllText(Path.Combine(balanceDir, "voyage_levels.csv"), report.ToString());
            Console.WriteLine($"voyage CSV: {Path.Combine(balanceDir, "voyage_levels.csv")}");
            Console.WriteLine(allZonesOk ? "GENERATION OK: 200 levels verified." : "GENERATION COMPLETED WITH TARGET MISSES.");
            return allZonesOk ? 0 : 1;
        }

        private sealed class LevelSpec
        {
            public string Id = "";
            public string Json = "";
            public string GoalText = "";
            public int Attempts;
            public double GhRate;
            public int Par;
            public int GcMedian;
            public bool GcThreeStar;
        }

        private static LevelSpec GenerateLevel(int zone, int index, EconomyConfig economy, int speciesCount)
        {
            int difficultyNudge = 0;
            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                ulong genSeed = MasterSeed ^ ((ulong)zone * 1009UL) ^ ((ulong)index * 9176UL) ^ ((ulong)attempt * 7919UL);
                var rng = DeterministicRng.FromSeed(genSeed);
                Candidate candidate = BuildCandidate(zone, index, difficultyNudge, ref rng, speciesCount, economy);

                LevelConfig config;
                try
                {
                    config = candidate.Def.ToLevelConfig(economy, speciesCount);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                double ghRate = Measure(config, economy, "gh", out int[] ghMoves);
                double minRate = ZoneMinRate[zone - 1];
                double maxRate = ZoneMaxRate[zone - 1];
                if (ghRate < minRate)
                {
                    difficultyNudge--;
                    continue;
                }

                if (ghRate > maxRate)
                {
                    difficultyNudge++;
                    continue;
                }

                int par = Median(ghMoves);
                double gcRate = Measure(config, economy, "gc", out int[] gcMoves);
                int gcMedian = gcMoves.Length > 0 ? Median(gcMoves) : int.MaxValue;
                if (zone <= 2 && gcMoves.Length > 0)
                {
                    // Tutorial bands: pars must be generous for naive play (DECISIONS.md).
                    par = Math.Max(par, gcMedian);
                }

                bool gcThreeStar = gcRate >= 0.5 && gcMedian <= par;

                return new LevelSpec
                {
                    Id = candidate.Id,
                    Json = candidate.ToJson(par),
                    GoalText = candidate.GoalText,
                    Attempts = attempt,
                    GhRate = ghRate,
                    Par = par,
                    GcMedian = gcMoves.Length > 0 ? gcMedian : -1,
                    GcThreeStar = gcThreeStar,
                };
            }

            throw new InvalidOperationException($"z{zone}-l{index}: no acceptable level in {MaxAttempts} attempts");
        }

        private sealed class Candidate
        {
            public string Id = "";
            public int Zone;
            public int StartWater;
            public int TideInterval;
            public int? RescueAll;
            public int? ClearRows;
            public int? SurviveTides;
            public long? Score;
            public List<(int col, int row, string kind, int id)> Preset = new List<(int, int, string, int)>();
            public string GoalText = "";

            public LevelDef Def = null!;

            public string ToJson(int par)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("  {\n");
                sb.Append($"    \"id\": \"{Id}\",\n");
                sb.Append($"    \"zone\": {Zone},\n");
                sb.Append($"    \"startWaterLevel\": {StartWater},\n");
                sb.Append($"    \"minWaterLevel\": {StartWater},\n");
                sb.Append($"    \"tideInterval\": {TideInterval},\n");
                sb.Append($"    \"weightBand\": {Zone},\n");
                sb.Append($"    \"parMoves\": {par},\n");
                sb.Append("    \"goals\": { ");
                var goals = new List<string>();
                if (RescueAll.HasValue) goals.Add($"\"rescueAll\": {RescueAll.Value}");
                if (ClearRows.HasValue) goals.Add($"\"clearRows\": {ClearRows.Value}");
                if (SurviveTides.HasValue) goals.Add($"\"surviveTides\": {SurviveTides.Value}");
                if (Score.HasValue) goals.Add($"\"score\": {Score.Value}");
                sb.Append(string.Join(", ", goals));
                sb.Append(" },\n");
                sb.Append("    \"preset\": [");
                if (Preset.Count > 0)
                {
                    sb.Append('\n');
                    for (int i = 0; i < Preset.Count; i++)
                    {
                        (int col, int row, string kind, int id) p = Preset[i];
                        sb.Append($"      {{ \"col\": {p.col}, \"row\": {p.row}, \"cell\": \"{p.kind}\"");
                        if (p.kind != "coral")
                        {
                            sb.Append($", \"id\": {p.id}");
                        }

                        sb.Append(" }");
                        sb.Append(i < Preset.Count - 1 ? ",\n" : "\n");
                    }

                    sb.Append("    ");
                }

                sb.Append("]\n");
                sb.Append("  }");
                return sb.ToString();
            }
        }

        private static Candidate BuildCandidate(int zone, int index, int difficultyNudge,
            ref DeterministicRng rng, int speciesCount, EconomyConfig economy)
        {
            var c = new Candidate
            {
                Id = $"z{zone}-l{index}",
                Zone = zone,
                StartWater = ZoneStartWater[zone - 1],
                TideInterval = ZoneTideInterval[zone - 1],
            };

            int magnitude = Math.Max(1, (zone + 2) / 2 + difficultyNudge);

            // Goal recipe per zone progression (GDD 3.1/9: tutorial arc, then variety).
            int goalRoll;
            if (zone == 1 && index <= 5)
            {
                goalRoll = 0;
                c.ClearRows = new[] { 1, 1, 2, 2, 3 }[index - 1];
            }
            else
            {
                RngIntDraw draw = rng.NextInt(100);
                rng = draw.Rng;
                goalRoll = draw.Value;
                if (zone >= 6 && goalRoll < 25)
                {
                    c.RescueAll = Clamp(1 + magnitude / 2, 1, 4);
                    c.ClearRows = Clamp(2 + magnitude, 2, 14);
                }
                else if (zone >= 4 && goalRoll < 45)
                {
                    c.SurviveTides = Clamp(2 + magnitude, 2, 9);
                }
                else if (zone >= 3 && goalRoll < 60)
                {
                    c.Score = 200 + 90L * Math.Max(1, magnitude);
                }
                else if (goalRoll < 80 && zone >= 2)
                {
                    c.RescueAll = Clamp(1 + magnitude / 2, 1, 4);
                }
                else
                {
                    c.ClearRows = Clamp(1 + magnitude, 2, 14);
                }
            }

            // Presets: creatures for rescue goals, coral from zone 3, scattered blocks from zone 2.
            var occupied = new HashSet<int>();
            int water = c.StartWater;
            if (c.RescueAll.HasValue)
            {
                for (int i = 0; i < c.RescueAll.Value; i++)
                {
                    rng = PlacePreset(rng, c, occupied, water + 1, Math.Min(water + 4, 9), "creature",
                        speciesIdMax: speciesCount);
                }
            }

            if (zone >= 3)
            {
                int coralCount = Clamp(zone - 1 + difficultyNudge, 0, 8);
                for (int i = 0; i < coralCount; i++)
                {
                    rng = PlacePreset(rng, c, occupied, water, Math.Min(water + 5, 9), "coral", speciesIdMax: 0);
                }
            }

            if (zone >= 2)
            {
                RngIntDraw blockDraw = rng.NextInt(3 + zone / 2);
                rng = blockDraw.Rng;
                for (int i = 0; i < blockDraw.Value; i++)
                {
                    rng = PlacePreset(rng, c, occupied, water, Math.Min(water + 6, 10), "block",
                        speciesIdMax: economy.DealColorCount);
                }
            }

            var goalParts = new List<string>();
            if (c.RescueAll.HasValue) goalParts.Add($"rescue{c.RescueAll}");
            if (c.ClearRows.HasValue) goalParts.Add($"rows{c.ClearRows}");
            if (c.SurviveTides.HasValue) goalParts.Add($"tides{c.SurviveTides}");
            if (c.Score.HasValue) goalParts.Add($"score{c.Score}");
            c.GoalText = string.Join("+", goalParts);

            var preset = new List<PresetCell>();
            foreach ((int col, int row, string kind, int id) p in c.Preset)
            {
                Cell content = p.kind switch
                {
                    "coral" => Cell.Coral,
                    "creature" => Cell.Creature((byte)p.id),
                    _ => Cell.Block((byte)p.id),
                };
                preset.Add(new PresetCell(new GridPos(p.col, p.row), content));
            }

            c.Def = new LevelDef(c.Id, zone, c.StartWater, c.StartWater, c.TideInterval, null,
                new GoalSet(c.RescueAll, c.ClearRows, c.SurviveTides, c.Score),
                economy.PieceWeightBands[zone], preset);
            return c;
        }

        private static DeterministicRng PlacePreset(DeterministicRng rng, Candidate c, HashSet<int> occupied,
            int rowMin, int rowMax, string kind, int speciesIdMax)
        {
            for (int tries = 0; tries < 40; tries++)
            {
                RngIntDraw colDraw = rng.NextInt(BoardSpec.Width);
                rng = colDraw.Rng;
                RngIntDraw rowDraw = rng.NextInt(rowMax - rowMin + 1);
                rng = rowDraw.Rng;
                int col = colDraw.Value;
                int row = rowMin + rowDraw.Value;
                int idx = BoardSpec.IndexOf(col, row);
                if (occupied.Contains(idx))
                {
                    continue;
                }

                // Keep preset rows sparse: a near-complete row would clear instantly.
                int rowCount = 0;
                foreach (var p in c.Preset)
                {
                    if (p.row == row)
                    {
                        rowCount++;
                    }
                }

                if (rowCount >= 5)
                {
                    continue;
                }

                int id = 0;
                if (speciesIdMax > 0)
                {
                    RngIntDraw idDraw = rng.NextInt(speciesIdMax);
                    rng = idDraw.Rng;
                    id = idDraw.Value;
                }

                occupied.Add(idx);
                c.Preset.Add((col, row, kind, id));
                return rng;
            }

            return rng;
        }

        private static double Measure(LevelConfig config, EconomyConfig economy, string policyName, out int[] completedMoves)
        {
            var moves = new List<int>();
            int completed = 0;
            for (ulong seed = 1; seed <= VerifySeeds; seed++)
            {
                IBotPolicy policy = policyName == "gh"
                    ? new GreedyHeuristicPolicy(economy.GreedyHeuristic)
                    : (IBotPolicy)new GreedyClearPolicy();
                GameState state = GameState.NewGame(config, seed * 7717UL);
                DeterministicRng botRng = DeterministicRng.FromSeed(seed ^ 0xB07UL);
                int steps = 0;
                while (state.Status == GameStatus.InProgress && steps < 400)
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

                if (state.Status == GameStatus.Won)
                {
                    completed++;
                    moves.Add(state.MoveCount);
                }
            }

            completedMoves = moves.ToArray();
            return (double)completed / VerifySeeds;
        }

        private static void WriteZoneFile(string contentRoot, int zone, LevelSpec[] levels)
        {
            var sb = new StringBuilder();
            sb.Append("[\n");
            for (int i = 0; i < levels.Length; i++)
            {
                sb.Append(levels[i].Json);
                sb.Append(i < levels.Length - 1 ? ",\n" : "\n");
            }

            sb.Append("]\n");
            string path = Path.Combine(contentRoot, "levels", $"zone{zone}.json");
            File.WriteAllText(path, sb.ToString());
            Console.WriteLine($"wrote {path}");
        }

        private static int Median(int[] values)
        {
            if (values.Length == 0)
            {
                return 1;
            }

            int[] copy = (int[])values.Clone();
            Array.Sort(copy);
            return copy[copy.Length / 2];
        }

        private static int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max);
    }
}
