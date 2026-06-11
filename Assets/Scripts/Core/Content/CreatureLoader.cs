using System;
using System.Collections.Generic;

namespace Riptide.Core
{
    /// <summary>One species from creatures.json (GDD 2.5 v1 roster of 8).</summary>
    public sealed class CreatureSpecies
    {
        public int Id { get; }
        public string Name { get; }
        public bool Rare { get; }

        /// <summary>Share-card emoji (GDD 3.3); content data so the goldens stay data-pinned.</summary>
        public string Emoji { get; }

        /// <summary>GDD 5.1: three Tidepool fact lines per species.</summary>
        public IReadOnlyList<string> Flavor { get; }

        public CreatureSpecies(int id, string name, bool rare, string emoji, IReadOnlyList<string> flavor)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Rare = rare;
            Emoji = emoji ?? throw new ArgumentNullException(nameof(emoji));
            Flavor = flavor ?? throw new ArgumentNullException(nameof(flavor));
        }
    }

    public sealed class CreatureRoster
    {
        public IReadOnlyList<CreatureSpecies> Species { get; }
        public int Count => Species.Count;

        public CreatureRoster(IReadOnlyList<CreatureSpecies> species)
        {
            Species = species ?? throw new ArgumentNullException(nameof(species));
        }
    }

    public static class CreatureLoader
    {
        /// <summary>Parses creatures.json. Ids must be 0..n-1, contiguous and unique.</summary>
        public static CreatureRoster Load(string json, string sourceLabel)
        {
            try
            {
                JsonObject root = JsonParser.Parse(json).AsObject();
                JsonArray speciesArray = root.Require("species").AsArray();
                if (speciesArray.Count < 1)
                {
                    throw new JsonParseException("species must not be empty", speciesArray.Line, speciesArray.Column);
                }

                var seen = new HashSet<int>();
                var species = new List<CreatureSpecies>(speciesArray.Count);
                foreach (JsonValue item in speciesArray.Items)
                {
                    JsonObject obj = item.AsObject();
                    JsonValue idNode = obj.Require("id");
                    int id = idNode.AsInt();
                    if (id < 0 || id >= speciesArray.Count)
                    {
                        throw new JsonParseException($"Species id {id} must be in 0..{speciesArray.Count - 1}",
                            idNode.Line, idNode.Column);
                    }

                    if (!seen.Add(id))
                    {
                        throw new JsonParseException($"Duplicate species id {id}", idNode.Line, idNode.Column);
                    }

                    JsonValue nameNode = obj.Require("name");
                    string name = nameNode.AsString();
                    if (name.Length == 0)
                    {
                        throw new JsonParseException("Species name must not be empty", nameNode.Line, nameNode.Column);
                    }

                    JsonValue emojiNode = obj.Require("emoji");
                    string emoji = emojiNode.AsString();
                    if (emoji.Length == 0)
                    {
                        throw new JsonParseException("Species emoji must not be empty", emojiNode.Line, emojiNode.Column);
                    }

                    JsonValue flavorNode = obj.Require("flavor");
                    JsonArray flavorArray = flavorNode.AsArray();
                    if (flavorArray.Count != 3)
                    {
                        throw new JsonParseException($"Species '{name}' needs exactly 3 flavor lines (GDD 5.1), got {flavorArray.Count}",
                            flavorNode.Line, flavorNode.Column);
                    }

                    var flavor = new List<string>(3);
                    foreach (JsonValue line in flavorArray.Items)
                    {
                        string text = line.AsString();
                        if (text.Length == 0)
                        {
                            throw new JsonParseException("Flavor lines must not be empty", line.Line, line.Column);
                        }

                        flavor.Add(text);
                    }

                    bool rare = obj.Optional("rare")?.AsBool() ?? false;
                    species.Add(new CreatureSpecies(id, name, rare, emoji, flavor));
                }

                species.Sort((a, b) => a.Id.CompareTo(b.Id));
                return new CreatureRoster(species);
            }
            catch (JsonParseException ex)
            {
                throw new ContentException(sourceLabel, ex.Message);
            }
        }
    }
}
