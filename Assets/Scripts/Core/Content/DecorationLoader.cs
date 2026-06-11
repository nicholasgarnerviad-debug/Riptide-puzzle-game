using System;
using System.Collections.Generic;

namespace Riptide.Core
{
    /// <summary>One Tidepool decoration (GDD 5.1): pure cosmetic coin sink.</summary>
    public sealed class Decoration
    {
        public string Id { get; }
        public string Name { get; }
        public int Cost { get; }
        public string Emoji { get; }

        public Decoration(string id, string name, int cost, string emoji)
        {
            Id = id;
            Name = name;
            Cost = cost;
            Emoji = emoji;
        }
    }

    public static class DecorationLoader
    {
        /// <summary>Parses decorations.json: unique ids, costs within the GDD 5.2 band (200–2000).</summary>
        public static IReadOnlyList<Decoration> Load(string json, string sourceLabel)
        {
            try
            {
                JsonObject root = JsonParser.Parse(json).AsObject();
                JsonArray items = root.Require("items").AsArray();
                if (items.Count == 0)
                {
                    throw new JsonParseException("decorations.json must list items", items.Line, items.Column);
                }

                var seen = new HashSet<string>(StringComparer.Ordinal);
                var decorations = new List<Decoration>(items.Count);
                foreach (JsonValue item in items.Items)
                {
                    JsonObject obj = item.AsObject();
                    JsonValue idNode = obj.Require("id");
                    string id = idNode.AsString();
                    if (id.Length == 0 || !seen.Add(id))
                    {
                        throw new JsonParseException($"Decoration id '{id}' is empty or duplicated", idNode.Line, idNode.Column);
                    }

                    JsonValue costNode = obj.Require("cost");
                    int cost = costNode.AsInt();
                    if (cost < 200 || cost > 2000)
                    {
                        throw new JsonParseException($"Decoration '{id}' cost {cost} is outside the GDD 5.2 band 200–2000",
                            costNode.Line, costNode.Column);
                    }

                    decorations.Add(new Decoration(id, obj.Require("name").AsString(), cost,
                        obj.Require("emoji").AsString()));
                }

                return decorations;
            }
            catch (JsonParseException ex)
            {
                throw new ContentException(sourceLabel, ex.Message);
            }
        }
    }
}
