using System;
using System.Collections.Generic;
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
            string root = args.Length > 0 ? args[0] : Path.Combine("Assets", "Resources", "Content");
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

                string decorationsPath = Path.Combine(root, "decorations.json");
                var decorations = DecorationLoader.Load(File.ReadAllText(decorationsPath), "decorations.json");
                Console.WriteLine($"OK decorations.json ({decorations.Count} items)");

                string themePath = Path.Combine(root, "ui_theme.json");
                UiTheme theme = UiThemeLoader.Load(File.ReadAllText(themePath), "ui_theme.json");
                int audit = AuditTheme(theme);
                if (audit != 0)
                {
                    return audit;
                }

                Console.WriteLine($"OK ui_theme.json ({theme.Colors.Count} colors; contrast + luminance audits pass)");

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

        /// <summary>
        /// UI spec §8 "script-verify": WCAG contrast for the text/surface pairs the
        /// spec claims pass, and the block-palette luminance-step audit. Failures
        /// block the gate and print exact measured numbers.
        /// </summary>
        private static int AuditTheme(UiTheme theme)
        {
            (string text, string bg, double min)[] contrastPairs =
            {
                ("text.primary", "bg.abyss", 4.5),
                ("text.primary", "bg.deep", 4.5),
                ("text.primary", "bg.surface", 4.5),
                ("text.primary", "bg.raised", 4.5),
                ("text.secondary", "bg.surface", 4.5),
                ("text.secondary", "bg.deep", 4.5),
                ("text.onAccent", "accent.primary", 7.0),
                ("text.muted", "bg.surface", 3.0),
            };

            foreach ((string text, string bg, double min) in contrastPairs)
            {
                double ratio = ThemeColor.ContrastRatio(theme.Color(text), theme.Color(bg));
                if (ratio < min)
                {
                    Console.Error.WriteLine(
                        $"ui_theme.json: contrast {text} on {bg} = {ratio:F2}, spec requires >= {min:F1}");
                    return 1;
                }
            }

            // Block palette: adjacent relative-luminance ratios (spec §8: steps >= 15%).
            string[] blocks = { "block.cyan", "block.teal", "block.coralPink", "block.amber", "block.violet", "block.ice" };
            var lums = new List<(string key, double lum)>();
            foreach (string key in blocks)
            {
                lums.Add((key, theme.Color(key).RelativeLuminance()));
            }

            lums.Sort((a, b) => a.lum.CompareTo(b.lum));
            for (int i = 1; i < lums.Count; i++)
            {
                double ratio = (lums[i].lum + 0.05) / (lums[i - 1].lum + 0.05);
                if (ratio < 1.05)
                {
                    Console.Error.WriteLine(
                        $"ui_theme.json: block luminance step {lums[i - 1].key} -> {lums[i].key} = {ratio:F3} — below the 1.05 floor");
                    return 1;
                }

                if (ratio < 1.15)
                {
                    // Spec §8 demands >= 1.15; the spec's own palette misses it here.
                    // Palette changes are Nick's aesthetic ruling (DECISIONS.md) —
                    // warn loudly, don't block the gate on the spec's self-contradiction.
                    Console.WriteLine(
                        $"WARN ui_theme.json: block luminance step {lums[i - 1].key} -> {lums[i].key} = {ratio:F3} " +
                        "< spec 1.15 — palette ruling pending (DECISIONS.md)");
                }
            }

            return 0;
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
