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
                if (ratio < 1.15)
                {
                    // Spec §8 floor, gate-blocking since the delegated palette
                    // ruling (DECISIONS.md 2026-06-11): teal was retuned so the
                    // whole chain clears 1.15 — keep it that way.
                    Console.Error.WriteLine(
                        $"ui_theme.json: block luminance step {lums[i - 1].key} -> {lums[i].key} = {ratio:F3} — below the spec §8 1.15 floor");
                    return 1;
                }
            }

            return AuditCvd(theme, blocks);
        }

        /// <summary>
        /// Spec §8: "tested against deuteranopia/protanopia simulation … a
        /// palette-distance check script — automatable, no eyes needed."
        /// Machado et al. (2009) severity-1.0 matrices in linear RGB; pairwise
        /// distances measured back in gamma space. Hard floor blocks; the spec's
        /// own known collision (coralPink↔teal) warns pending Nick's ruling.
        /// </summary>
        private static int AuditCvd(UiTheme theme, string[] blocks)
        {
            double[][] protan =
            {
                new[] { 0.152286, 1.052583, -0.204868 },
                new[] { 0.114503, 0.786281, 0.099216 },
                new[] { -0.003882, -0.048116, 1.051998 },
            };
            double[][] deutan =
            {
                new[] { 0.367322, 0.860646, -0.227968 },
                new[] { 0.280085, 0.672501, 0.047413 },
                new[] { -0.011820, 0.042940, 0.968881 },
            };

            int result = 0;
            foreach ((string simName, double[][] m) in new[] { ("protanopia", protan), ("deuteranopia", deutan) })
            {
                var simulated = new List<(string key, double r, double g, double b)>();
                foreach (string key in blocks)
                {
                    ThemeColor c = theme.Color(key);
                    double lr = SrgbToLinear(c.R);
                    double lg = SrgbToLinear(c.G);
                    double lb = SrgbToLinear(c.B);
                    double sr = m[0][0] * lr + m[0][1] * lg + m[0][2] * lb;
                    double sg = m[1][0] * lr + m[1][1] * lg + m[1][2] * lb;
                    double sb = m[2][0] * lr + m[2][1] * lg + m[2][2] * lb;
                    simulated.Add((key, LinearToSrgb(sr), LinearToSrgb(sg), LinearToSrgb(sb)));
                }

                double worst = double.MaxValue;
                string worstPair = "";
                for (int i = 0; i < simulated.Count; i++)
                {
                    for (int j = i + 1; j < simulated.Count; j++)
                    {
                        double dr = simulated[i].r - simulated[j].r;
                        double dg = simulated[i].g - simulated[j].g;
                        double db = simulated[i].b - simulated[j].b;
                        double dist = Math.Sqrt(dr * dr + dg * dg + db * db);
                        if (dist < worst)
                        {
                            worst = dist;
                            worstPair = $"{simulated[i].key} <-> {simulated[j].key}";
                        }

                        if (dist < 0.04)
                        {
                            Console.Error.WriteLine(
                                $"ui_theme.json: {simName} collision {simulated[i].key} <-> {simulated[j].key} " +
                                $"distance {dist:F3} < 0.04 hard floor");
                            result = 1;
                        }
                        else if (dist < 0.10)
                        {
                            Console.WriteLine(
                                $"WARN ui_theme.json: {simName} near-collision {simulated[i].key} <-> " +
                                $"{simulated[j].key} distance {dist:F3} < 0.10 — palette ruling pending (DECISIONS.md)");
                        }
                    }
                }

                Console.WriteLine($"OK CVD {simName}: min pair distance {worst:F3} ({worstPair})");
            }

            return result;
        }

        private static double SrgbToLinear(double c) =>
            c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

        private static double LinearToSrgb(double c)
        {
            c = Math.Max(0.0, Math.Min(1.0, c));
            return c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1.0 / 2.4) - 0.055;
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
