using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Riptide.Core
{
    /// <summary>
    /// Thrown for malformed JSON or schema violations, always carrying the
    /// offending line and column (master prompt 2C: "schema violations throw
    /// with file/line").
    /// </summary>
    public sealed class JsonParseException : Exception
    {
        public int Line { get; }
        public int Column { get; }

        public JsonParseException(string message, int line, int column)
            : base($"{message} (line {line}, col {column})")
        {
            Line = line;
            Column = column;
        }
    }

    /// <summary>A parsed JSON node. Every node remembers where it started in the source.</summary>
    public abstract class JsonValue
    {
        public int Line { get; }
        public int Column { get; }

        private protected JsonValue(int line, int column)
        {
            Line = line;
            Column = column;
        }

        public abstract string KindName { get; }

        public JsonObject AsObject() => this as JsonObject ?? throw Mismatch("an object");

        public JsonArray AsArray() => this as JsonArray ?? throw Mismatch("an array");

        public string AsString() => this is JsonString s ? s.Value : throw Mismatch("a string");

        public bool AsBool() => this is JsonBool b ? b.Value : throw Mismatch("a boolean");

        public long AsLong() =>
            this is JsonNumber n && n.IsIntegral ? n.LongValue : throw Mismatch("an integer");

        public int AsInt()
        {
            long value = AsLong();
            if (value < int.MinValue || value > int.MaxValue)
            {
                throw new JsonParseException($"Integer {value} is out of 32-bit range", Line, Column);
            }

            return (int)value;
        }

        public double AsDouble() => this is JsonNumber n ? n.DoubleValue : throw Mismatch("a number");

        public bool IsNull => this is JsonNull;

        private JsonParseException Mismatch(string expected) =>
            new JsonParseException($"Expected {expected} but found {KindName}", Line, Column);
    }

    public sealed class JsonObject : JsonValue
    {
        private readonly Dictionary<string, JsonValue> members;
        private readonly List<string> order;

        internal JsonObject(Dictionary<string, JsonValue> members, List<string> order, int line, int column)
            : base(line, column)
        {
            this.members = members;
            this.order = order;
        }

        public override string KindName => "object";

        public int Count => members.Count;

        public IReadOnlyList<string> MemberNames => order;

        public bool TryGet(string name, out JsonValue value) => members.TryGetValue(name, out value!);

        public JsonValue Require(string name) =>
            members.TryGetValue(name, out JsonValue? value)
                ? value
                : throw new JsonParseException($"Missing required member '{name}'", Line, Column);

        public JsonValue? Optional(string name) =>
            members.TryGetValue(name, out JsonValue? value) && !value.IsNull ? value : null;
    }

    public sealed class JsonArray : JsonValue
    {
        public IReadOnlyList<JsonValue> Items { get; }

        internal JsonArray(IReadOnlyList<JsonValue> items, int line, int column)
            : base(line, column)
        {
            Items = items;
        }

        public override string KindName => "array";

        public int Count => Items.Count;
    }

    public sealed class JsonString : JsonValue
    {
        public string Value { get; }

        internal JsonString(string value, int line, int column)
            : base(line, column)
        {
            Value = value;
        }

        public override string KindName => "string";
    }

    public sealed class JsonNumber : JsonValue
    {
        public double DoubleValue { get; }
        public long LongValue { get; }
        public bool IsIntegral { get; }

        internal JsonNumber(double doubleValue, long longValue, bool isIntegral, int line, int column)
            : base(line, column)
        {
            DoubleValue = doubleValue;
            LongValue = longValue;
            IsIntegral = isIntegral;
        }

        public override string KindName => "number";
    }

    public sealed class JsonBool : JsonValue
    {
        public bool Value { get; }

        internal JsonBool(bool value, int line, int column)
            : base(line, column)
        {
            Value = value;
        }

        public override string KindName => "boolean";
    }

    public sealed class JsonNull : JsonValue
    {
        internal JsonNull(int line, int column)
            : base(line, column)
        {
        }

        public override string KindName => "null";
    }

    /// <summary>
    /// Minimal strict JSON parser (objects, arrays, strings with escapes, numbers,
    /// true/false/null). Hand-rolled so Core stays dependency-free in both compile
    /// pipelines and every node carries line/column for precise validator errors
    /// (DECISIONS.md 2026-06-11). Duplicate object keys are rejected.
    /// </summary>
    public static class JsonParser
    {
        public static JsonValue Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var parser = new Cursor(text);
            JsonValue value = ParseValue(parser);
            parser.SkipWhitespace();
            if (!parser.AtEnd)
            {
                throw parser.Error("Trailing content after the JSON document");
            }

            return value;
        }

        private static JsonValue ParseValue(Cursor p)
        {
            p.SkipWhitespace();
            if (p.AtEnd)
            {
                throw p.Error("Unexpected end of JSON");
            }

            int line = p.Line;
            int col = p.Col;
            char c = p.Peek;
            switch (c)
            {
                case '{': return ParseObject(p, line, col);
                case '[': return ParseArray(p, line, col);
                case '"': return new JsonString(ParseStringLiteral(p), line, col);
                case 't': p.ExpectKeyword("true"); return new JsonBool(true, line, col);
                case 'f': p.ExpectKeyword("false"); return new JsonBool(false, line, col);
                case 'n': p.ExpectKeyword("null"); return new JsonNull(line, col);
                default:
                    if (c == '-' || (c >= '0' && c <= '9'))
                    {
                        return ParseNumber(p, line, col);
                    }

                    throw p.Error($"Unexpected character '{c}'");
            }
        }

        private static JsonObject ParseObject(Cursor p, int line, int col)
        {
            p.Next(); // {
            var members = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
            var order = new List<string>();
            p.SkipWhitespace();
            if (!p.AtEnd && p.Peek == '}')
            {
                p.Next();
                return new JsonObject(members, order, line, col);
            }

            while (true)
            {
                p.SkipWhitespace();
                if (p.AtEnd || p.Peek != '"')
                {
                    throw p.Error("Expected a string member name");
                }

                int keyLine = p.Line;
                int keyCol = p.Col;
                string key = ParseStringLiteral(p);
                if (members.ContainsKey(key))
                {
                    throw new JsonParseException($"Duplicate member '{key}'", keyLine, keyCol);
                }

                p.SkipWhitespace();
                if (p.AtEnd || p.Peek != ':')
                {
                    throw p.Error("Expected ':' after member name");
                }

                p.Next();
                members[key] = ParseValue(p);
                order.Add(key);

                p.SkipWhitespace();
                if (p.AtEnd)
                {
                    throw p.Error("Unterminated object");
                }

                char next = p.Next();
                if (next == '}')
                {
                    return new JsonObject(members, order, line, col);
                }

                if (next != ',')
                {
                    throw p.Error("Expected ',' or '}' in object");
                }
            }
        }

        private static JsonArray ParseArray(Cursor p, int line, int col)
        {
            p.Next(); // [
            var items = new List<JsonValue>();
            p.SkipWhitespace();
            if (!p.AtEnd && p.Peek == ']')
            {
                p.Next();
                return new JsonArray(items, line, col);
            }

            while (true)
            {
                items.Add(ParseValue(p));
                p.SkipWhitespace();
                if (p.AtEnd)
                {
                    throw p.Error("Unterminated array");
                }

                char next = p.Next();
                if (next == ']')
                {
                    return new JsonArray(items, line, col);
                }

                if (next != ',')
                {
                    throw p.Error("Expected ',' or ']' in array");
                }
            }
        }

        private static string ParseStringLiteral(Cursor p)
        {
            p.Next(); // opening quote
            var sb = new StringBuilder();
            while (true)
            {
                if (p.AtEnd)
                {
                    throw p.Error("Unterminated string");
                }

                char c = p.Next();
                if (c == '"')
                {
                    return sb.ToString();
                }

                if (c == '\\')
                {
                    if (p.AtEnd)
                    {
                        throw p.Error("Unterminated escape sequence");
                    }

                    char esc = p.Next();
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            int code = 0;
                            for (int i = 0; i < 4; i++)
                            {
                                if (p.AtEnd)
                                {
                                    throw p.Error("Unterminated \\u escape");
                                }

                                char h = p.Next();
                                int digit = h switch
                                {
                                    >= '0' and <= '9' => h - '0',
                                    >= 'a' and <= 'f' => h - 'a' + 10,
                                    >= 'A' and <= 'F' => h - 'A' + 10,
                                    _ => throw p.Error($"Invalid hex digit '{h}' in \\u escape"),
                                };
                                code = (code << 4) | digit;
                            }

                            sb.Append((char)code);
                            break;
                        default:
                            throw p.Error($"Invalid escape '\\{esc}'");
                    }

                    continue;
                }

                if (c < ' ')
                {
                    throw p.Error("Raw control character in string");
                }

                sb.Append(c);
            }
        }

        private static JsonNumber ParseNumber(Cursor p, int line, int col)
        {
            var sb = new StringBuilder();
            bool integral = true;
            if (!p.AtEnd && p.Peek == '-')
            {
                sb.Append(p.Next());
            }

            if (p.AtEnd || p.Peek < '0' || p.Peek > '9')
            {
                throw p.Error("Malformed number");
            }

            while (!p.AtEnd && p.Peek >= '0' && p.Peek <= '9')
            {
                sb.Append(p.Next());
            }

            if (!p.AtEnd && p.Peek == '.')
            {
                integral = false;
                sb.Append(p.Next());
                if (p.AtEnd || p.Peek < '0' || p.Peek > '9')
                {
                    throw p.Error("Malformed number: digits required after '.'");
                }

                while (!p.AtEnd && p.Peek >= '0' && p.Peek <= '9')
                {
                    sb.Append(p.Next());
                }
            }

            if (!p.AtEnd && (p.Peek == 'e' || p.Peek == 'E'))
            {
                integral = false;
                sb.Append(p.Next());
                if (!p.AtEnd && (p.Peek == '+' || p.Peek == '-'))
                {
                    sb.Append(p.Next());
                }

                if (p.AtEnd || p.Peek < '0' || p.Peek > '9')
                {
                    throw p.Error("Malformed number: digits required in exponent");
                }

                while (!p.AtEnd && p.Peek >= '0' && p.Peek <= '9')
                {
                    sb.Append(p.Next());
                }
            }

            string repr = sb.ToString();
            if (integral && long.TryParse(repr, NumberStyles.Integer, CultureInfo.InvariantCulture, out long longValue))
            {
                return new JsonNumber(longValue, longValue, true, line, col);
            }

            if (double.TryParse(repr, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleValue))
            {
                return new JsonNumber(doubleValue, 0, false, line, col);
            }

            throw new JsonParseException($"Malformed number '{repr}'", line, col);
        }

        private sealed class Cursor
        {
            private readonly string text;
            private int pos;

            public int Line { get; private set; } = 1;
            public int Col { get; private set; } = 1;

            public Cursor(string text)
            {
                this.text = text;
            }

            public bool AtEnd => pos >= text.Length;

            public char Peek => text[pos];

            public char Next()
            {
                char c = text[pos++];
                if (c == '\n')
                {
                    Line++;
                    Col = 1;
                }
                else
                {
                    Col++;
                }

                return c;
            }

            public void SkipWhitespace()
            {
                while (!AtEnd && (Peek == ' ' || Peek == '\t' || Peek == '\r' || Peek == '\n'))
                {
                    Next();
                }
            }

            public void ExpectKeyword(string keyword)
            {
                for (int i = 0; i < keyword.Length; i++)
                {
                    if (AtEnd || Peek != keyword[i])
                    {
                        throw Error($"Malformed literal (expected '{keyword}')");
                    }

                    Next();
                }
            }

            public JsonParseException Error(string message) => new JsonParseException(message, Line, Col);
        }
    }
}
