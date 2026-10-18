using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Riptide.Core;

namespace Riptide.Tools.ContentCheck
{
    /// <summary>
    /// GDD §16.8 originality guardrail — a build gate, not legal clearance. Tideling
    /// form names must not sit within a small edit distance of (or contain) an existing
    /// sea-creature character name from the big creature-collection franchise, and no
    /// player-facing string may use that franchise's vocabulary. Real animal names
    /// (the line names: Crab, Sea Dragon…) are exempt: they are not anyone's characters.
    /// </summary>
    internal static class OriginalityGate
    {
        /// <summary>Existing franchise characters that are (or live with) sea creatures.</summary>
        private static readonly string[] Denylist =
        {
            "krabby", "kingler", "staryu", "starmie", "horsea", "seadra", "kingdra", "octillery", "remoraid",
            "squirtle", "wartortle", "blastoise", "shellder", "cloyster", "qwilfish", "overqwil", "tentacool",
            "tentacruel", "frillish", "jellicent", "wooper", "quagsire", "clodsire", "mantine", "mantyke",
            "chinchou", "lanturn", "finneon", "lumineon", "skrelp", "dragalge", "clauncher", "clawitzer",
            "corphish", "crawdaunt", "klawf", "tirtouga", "carracosta", "lapras", "wailmer", "wailord", "corsola",
            "cursola", "luvdisc", "relicanth", "wishiwashi", "pyukumuku", "mareanie", "toxapex", "dhelmise",
            "bruxish", "wiglett", "wugtrio", "finizen", "palafin", "tatsugiri", "dondozo", "veluza", "arrokuda",
            "barraskewda", "goldeen", "seaking", "magikarp", "gyarados", "slowpoke", "seel", "dewgong", "kabuto",
            "omanyte", "omastar", "totodile", "marill", "azumarill", "mudkip", "carvanha", "sharpedo", "spheal",
            "sealeo", "walrein", "clamperl", "huntail", "gorebyss", "buizel", "floatzel", "shellos", "gastrodon",
            "piplup", "empoleon", "oshawott", "samurott", "basculin", "alomomola", "froakie", "binacle",
            "barbaracle", "popplio", "primarina", "sobble", "inteleon", "quaxly", "kyogre", "manaphy", "phione",
            "vaporeon", "feebas", "milotic", "chewtle", "drednaw", "cramorant", "pincurchin", "dracovish",
            "arctovish", "wingull", "pelipper", "lotad", "lombre", "ludicolo", "barboach", "whiscash", "starly",
        };

        /// <summary>Franchise vocabulary that never appears in player-facing text (whole words).</summary>
        private static readonly string[] BannedWords =
        {
            "pokemon", "pokémon", "poke", "poké", "pokedex", "pokédex", "dex", "shiny", "shinies", "evolve",
            "evolves", "evolved", "evolving", "evolution", "trainer", "trainers", "pal", "pals",
        };

        public static List<string> Check(PalsConfig pals, JsonObject strings)
        {
            var problems = new List<string>();
            foreach (PalLine line in pals.Lines)
            {
                foreach (string stage in line.Stages)
                {
                    string ours = Normalize(stage);
                    foreach (string theirs in Denylist)
                    {
                        int limit = Math.Min(ours.Length, theirs.Length) >= 6 ? 2 : 1;
                        bool contains = (theirs.Length >= 5 && ours.Contains(theirs))
                            || (ours.Length >= 5 && theirs.Contains(ours));
                        if (contains || Distance(ours, theirs) <= limit)
                        {
                            problems.Add($"pals.json: '{stage}' ({line.Key}) is too close to an existing character name.");
                        }
                    }

                    problems.AddRange(BannedIn($"pals.json '{stage}'", stage));
                }
            }

            foreach (string key in strings.MemberNames)
            {
                problems.AddRange(BannedIn($"strings.json '{key}'", strings.Require(key).AsString()));
            }

            return problems;
        }

        private static IEnumerable<string> BannedIn(string where, string text)
        {
            string lower = text.ToLowerInvariant();
            return BannedWords
                .Where(word => Regex.IsMatch(lower, $@"(?<![\p{{L}}]){Regex.Escape(word)}(?![\p{{L}}])"))
                .Select(word => $"{where}: uses the word '{word}' (GDD §16.8).");
        }

        private static string Normalize(string name) =>
            new string(name.ToLowerInvariant().Where(char.IsLetter).ToArray());

        private static int Distance(string a, string b)
        {
            var d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++)
            {
                d[i, 0] = i;
            }

            for (int j = 0; j <= b.Length; j++)
            {
                d[0, j] = j;
            }

            for (int i = 1; i <= a.Length; i++)
            {
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }
            }

            return d[a.Length, b.Length];
        }
    }
}
