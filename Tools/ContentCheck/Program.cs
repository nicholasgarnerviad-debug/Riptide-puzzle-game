using System;
using System.IO;
using Riptide.Core;

namespace Riptide.Tools.ContentCheck
{
    /// <summary>
    /// Validates the shipped content fixtures through the Core loaders.
    /// Usage: dotnet run --project Tools/ContentCheck [-- contentRoot]
    /// Exits non-zero on the first invalid file, printing the precise error.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            string root = args.Length > 0 ? args[0] : Path.Combine("Assets", "Content");
            if (!Directory.Exists(root))
            {
                Console.Error.WriteLine($"ContentCheck: content root '{root}' not found (run from the repo root).");
                return 2;
            }

            try
            {
                string economyPath = Path.Combine(root, "economy.json");
                EconomyConfig economy = EconomyLoader.Load(File.ReadAllText(economyPath), "economy.json");
                Console.WriteLine($"OK economy.json ({economy.PieceWeightBands.Count} weight bands, {economy.DealColorCount} colors)");

                string creaturesPath = Path.Combine(root, "creatures.json");
                CreatureRoster roster = CreatureLoader.Load(File.ReadAllText(creaturesPath), "creatures.json");
                Console.WriteLine($"OK creatures.json ({roster.Count} species)");

                string stringsPath = Path.Combine(root, "strings.json");
                ValidateStrings(File.ReadAllText(stringsPath));
                Console.WriteLine("OK strings.json");

                string levelsDir = Path.Combine(root, "levels");
                string[] zoneFiles = Directory.Exists(levelsDir)
                    ? Directory.GetFiles(levelsDir, "*.json")
                    : Array.Empty<string>();
                if (zoneFiles.Length == 0)
                {
                    Console.Error.WriteLine("ContentCheck: no zone files under levels/.");
                    return 1;
                }

                Array.Sort(zoneFiles, StringComparer.Ordinal);
                int levelTotal = 0;
                foreach (string zoneFile in zoneFiles)
                {
                    string label = Path.GetFileName(zoneFile);
                    var levels = LevelDefLoader.LoadZone(File.ReadAllText(zoneFile), label, economy);
                    foreach (LevelDef level in levels)
                    {
                        // Cross-checks the loaders cannot do alone: creature ids vs the
                        // roster, then full sim-side validation via LevelConfig.
                        foreach (PresetCell preset in level.Preset)
                        {
                            if (preset.Content.Kind == CellKind.Creature && preset.Content.Id >= roster.Count)
                            {
                                Console.Error.WriteLine(
                                    $"{label}: level '{level.Id}' uses creature id {preset.Content.Id}, roster has {roster.Count} species.");
                                return 1;
                            }
                        }

                        level.ToLevelConfig(economy, roster.Count);
                        levelTotal++;
                    }

                    Console.WriteLine($"OK {label} ({levels.Count} levels)");
                }

                Console.WriteLine($"CONTENT OK: {levelTotal} levels validated.");
                return 0;
            }
            catch (ContentException ex)
            {
                Console.Error.WriteLine($"ContentCheck FAILED: {ex.Message}");
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ContentCheck FAILED: {ex.GetType().Name}: {ex.Message}");
                return 1;
            }
        }

        private static void ValidateStrings(string json)
        {
            JsonObject root;
            try
            {
                root = JsonParser.Parse(json).AsObject();
            }
            catch (JsonParseException ex)
            {
                throw new ContentException("strings.json", ex.Message);
            }

            foreach (string key in root.MemberNames)
            {
                JsonValue node = root.Require(key);
                if (!(node is JsonString text) || text.Value.Length == 0)
                {
                    throw new ContentException("strings.json",
                        $"'{key}' must be a non-empty string (line {node.Line}, col {node.Column})");
                }
            }
        }
    }
}
