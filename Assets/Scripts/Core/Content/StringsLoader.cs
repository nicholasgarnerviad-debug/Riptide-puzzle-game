using System;
using System.Collections.Generic;

namespace Riptide.Core
{
    /// <summary>Flat string table (GDD 12: strings.json from day 1, EN only in v1).</summary>
    public sealed class StringTable
    {
        private readonly Dictionary<string, string> entries;

        internal StringTable(Dictionary<string, string> entries)
        {
            this.entries = entries;
        }

        public int Count => entries.Count;

        /// <summary>Missing keys throw — UI literals must never silently appear (contract 5A).</summary>
        public string Get(string key) =>
            entries.TryGetValue(key, out string? value)
                ? value
                : throw new KeyNotFoundException($"strings.json has no entry '{key}'");

        public bool Has(string key) => entries.ContainsKey(key);
    }

    public static class StringsLoader
    {
        public static StringTable Load(string json, string sourceLabel)
        {
            try
            {
                JsonObject root = JsonParser.Parse(json).AsObject();
                var entries = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (string key in root.MemberNames)
                {
                    JsonValue node = root.Require(key);
                    string text = node.AsString();
                    if (text.Length == 0)
                    {
                        throw new JsonParseException($"'{key}' must not be empty", node.Line, node.Column);
                    }

                    entries[key] = text;
                }

                if (entries.Count == 0)
                {
                    throw new JsonParseException("strings.json must not be empty", root.Line, root.Column);
                }

                return new StringTable(entries);
            }
            catch (JsonParseException ex)
            {
                throw new ContentException(sourceLabel, ex.Message);
            }
        }
    }
}
