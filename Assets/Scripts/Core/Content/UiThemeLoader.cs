using System;
using System.Collections.Generic;

namespace Riptide.Core
{
    /// <summary>An sRGB color token (floats 0..1).</summary>
    public readonly struct ThemeColor
    {
        public float R { get; }
        public float G { get; }
        public float B { get; }
        public float A { get; }

        public ThemeColor(float r, float g, float b, float a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        /// <summary>WCAG relative luminance (ignores alpha).</summary>
        public double RelativeLuminance()
        {
            return 0.2126 * Linear(R) + 0.7152 * Linear(G) + 0.0722 * Linear(B);
        }

        private static double Linear(double channel) =>
            channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);

        /// <summary>WCAG contrast ratio between two opaque colors.</summary>
        public static double ContrastRatio(ThemeColor a, ThemeColor b)
        {
            double la = a.RelativeLuminance();
            double lb = b.RelativeLuminance();
            double lighter = Math.Max(la, lb);
            double darker = Math.Min(la, lb);
            return (lighter + 0.05) / (darker + 0.05);
        }
    }

    public sealed class TypeStyle
    {
        public int Size { get; }
        public int Line { get; }
        public string Weight { get; }
        public float Tracking { get; }
        public bool Tabular { get; }
        public bool AllCaps { get; }

        public TypeStyle(int size, int line, string weight, float tracking, bool tabular, bool allCaps)
        {
            Size = size;
            Line = line;
            Weight = weight;
            Tracking = tracking;
            Tabular = tabular;
            AllCaps = allCaps;
        }
    }

    /// <summary>One easing keyframe: time, value, inTangent, outTangent.</summary>
    public readonly struct EasingKey
    {
        public float Time { get; }
        public float Value { get; }
        public float InTangent { get; }
        public float OutTangent { get; }

        public EasingKey(float time, float value, float inTangent, float outTangent)
        {
            Time = time;
            Value = value;
            InTangent = inTangent;
            OutTangent = outTangent;
        }
    }

    public sealed class JuiceEntry
    {
        public string Sfx { get; }
        public string Haptic { get; }
        public string Anim { get; }

        public JuiceEntry(string sfx, string haptic, string anim)
        {
            Sfx = sfx;
            Haptic = haptic;
            Anim = anim;
        }
    }

    /// <summary>
    /// UI spec §1: the immutable design-token set. No color, duration, or radius
    /// literal may appear in UI code — everything resolves through here.
    /// </summary>
    public sealed class UiTheme
    {
        public IReadOnlyDictionary<string, ThemeColor> Colors { get; }
        public IReadOnlyDictionary<string, TypeStyle> Type { get; }
        public IReadOnlyList<int> SpacingScale { get; }
        public int Gutter { get; }
        public int CardPadding { get; }
        public IReadOnlyDictionary<string, int> Radius { get; }
        public IReadOnlyDictionary<string, int> DurationsMs { get; }
        public IReadOnlyDictionary<string, IReadOnlyList<EasingKey>> Easings { get; }
        public IReadOnlyDictionary<string, JuiceEntry> Juice { get; }
        public float ReducedMotionScale { get; }
        public int MinTouchTargetRefPx { get; }

        public UiTheme(
            IReadOnlyDictionary<string, ThemeColor> colors,
            IReadOnlyDictionary<string, TypeStyle> type,
            IReadOnlyList<int> spacingScale,
            int gutter,
            int cardPadding,
            IReadOnlyDictionary<string, int> radius,
            IReadOnlyDictionary<string, int> durationsMs,
            IReadOnlyDictionary<string, IReadOnlyList<EasingKey>> easings,
            IReadOnlyDictionary<string, JuiceEntry> juice,
            float reducedMotionScale,
            int minTouchTargetRefPx)
        {
            Colors = colors;
            Type = type;
            SpacingScale = spacingScale;
            Gutter = gutter;
            CardPadding = cardPadding;
            Radius = radius;
            DurationsMs = durationsMs;
            Easings = easings;
            Juice = juice;
            ReducedMotionScale = reducedMotionScale;
            MinTouchTargetRefPx = minTouchTargetRefPx;
        }

        /// <summary>Missing tokens throw — a typo'd key must never silently render magenta.</summary>
        public ThemeColor Color(string key) =>
            Colors.TryGetValue(key, out ThemeColor color)
                ? color
                : throw new KeyNotFoundException($"ui_theme.json has no color '{key}'");

        public int Duration(string key) =>
            DurationsMs.TryGetValue(key, out int ms)
                ? ms
                : throw new KeyNotFoundException($"ui_theme.json has no duration '{key}'");

        public TypeStyle TypeStyle(string key) =>
            Type.TryGetValue(key, out TypeStyle? style)
                ? style
                : throw new KeyNotFoundException($"ui_theme.json has no type style '{key}'");

        public IReadOnlyList<EasingKey> Easing(string key) =>
            Easings.TryGetValue(key, out IReadOnlyList<EasingKey>? keys)
                ? keys
                : throw new KeyNotFoundException($"ui_theme.json has no easing '{key}'");
    }

    public static class UiThemeLoader
    {
        /// <summary>Required by the spec; load fails loudly if any is missing.</summary>
        public static readonly string[] RequiredColors =
        {
            "bg.abyss", "bg.deep", "bg.surface", "bg.raised", "stroke.subtle", "stroke.bright",
            "accent.primary", "accent.deep", "glow.primary",
            "water.calm.top", "water.calm.btm", "water.danger.top", "water.danger.btm", "water.foamLine",
            "positive", "warning", "danger", "coin",
            "block.cyan", "block.teal", "block.coralPink", "block.amber", "block.violet", "block.ice", "block.dead",
            "text.primary", "text.secondary", "text.muted", "text.onAccent", "scrim",
        };

        public static readonly string[] RequiredType =
            { "display", "title", "heading", "body", "caption", "micro", "score" };

        public static readonly string[] RequiredDurations =
            { "t.instant", "t.fast", "t.base", "t.screen", "t.rise", "t.drain" };

        public static UiTheme Load(string json, string sourceLabel)
        {
            try
            {
                JsonObject root = JsonParser.Parse(json).AsObject();

                JsonObject colorsObj = root.Require("colors").AsObject();
                var colors = new Dictionary<string, ThemeColor>(StringComparer.Ordinal);
                foreach (string key in colorsObj.MemberNames)
                {
                    JsonObject entry = colorsObj.Require(key).AsObject();
                    JsonValue hexNode = entry.Require("hex");
                    string hex = hexNode.AsString();
                    if (hex.Length != 6)
                    {
                        throw new JsonParseException($"Color '{key}' hex must be RRGGBB", hexNode.Line, hexNode.Column);
                    }

                    float alpha = entry.Optional("alpha") is JsonValue alphaNode ? (float)alphaNode.AsDouble() : 1f;
                    colors[key] = new ThemeColor(
                        Convert.ToInt32(hex.Substring(0, 2), 16) / 255f,
                        Convert.ToInt32(hex.Substring(2, 2), 16) / 255f,
                        Convert.ToInt32(hex.Substring(4, 2), 16) / 255f,
                        alpha);
                }

                foreach (string required in RequiredColors)
                {
                    if (!colors.ContainsKey(required))
                    {
                        throw new JsonParseException($"Missing required color token '{required}'", root.Line, root.Column);
                    }
                }

                JsonObject typeObj = root.Require("type").AsObject();
                var type = new Dictionary<string, TypeStyle>(StringComparer.Ordinal);
                foreach (string key in typeObj.MemberNames)
                {
                    JsonObject entry = typeObj.Require(key).AsObject();
                    type[key] = new TypeStyle(
                        entry.Require("size").AsInt(),
                        entry.Require("line").AsInt(),
                        entry.Require("weight").AsString(),
                        (float)entry.Require("tracking").AsDouble(),
                        entry.Require("tabular").AsBool(),
                        entry.Require("allCaps").AsBool());
                }

                foreach (string required in RequiredType)
                {
                    if (!type.ContainsKey(required))
                    {
                        throw new JsonParseException($"Missing required type style '{required}'", root.Line, root.Column);
                    }
                }

                JsonObject spacing = root.Require("spacing").AsObject();
                JsonArray scaleArray = spacing.Require("scale").AsArray();
                var scale = new List<int>(scaleArray.Count);
                foreach (JsonValue item in scaleArray.Items)
                {
                    scale.Add(item.AsInt());
                }

                JsonObject radiusObj = root.Require("radius").AsObject();
                var radius = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (string key in radiusObj.MemberNames)
                {
                    radius[key] = radiusObj.Require(key).AsInt();
                }

                JsonObject motion = root.Require("motion").AsObject();
                JsonObject durationsObj = motion.Require("durationsMs").AsObject();
                var durations = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (string key in durationsObj.MemberNames)
                {
                    durations[key] = durationsObj.Require(key).AsInt();
                }

                foreach (string required in RequiredDurations)
                {
                    if (!durations.ContainsKey(required))
                    {
                        throw new JsonParseException($"Missing required duration '{required}'", motion.Line, motion.Column);
                    }
                }

                // GDD-locked values (spec §1.4): rule-5-grade — these can never drift.
                if (durations["t.rise"] != 350 || durations["t.drain"] != 450)
                {
                    throw new JsonParseException("t.rise must be 350 and t.drain 450 (GDD-locked)", motion.Line, motion.Column);
                }

                JsonObject easingsObj = motion.Require("easings").AsObject();
                var easings = new Dictionary<string, IReadOnlyList<EasingKey>>(StringComparer.Ordinal);
                foreach (string key in easingsObj.MemberNames)
                {
                    JsonArray keyArray = easingsObj.Require(key).AsArray();
                    if (keyArray.Count < 2)
                    {
                        throw new JsonParseException($"Easing '{key}' needs at least 2 keyframes", keyArray.Line, keyArray.Column);
                    }

                    var keys = new List<EasingKey>(keyArray.Count);
                    foreach (JsonValue frame in keyArray.Items)
                    {
                        JsonArray quad = frame.AsArray();
                        if (quad.Count != 4)
                        {
                            throw new JsonParseException($"Easing '{key}' keyframes are [t,v,inTan,outTan]", quad.Line, quad.Column);
                        }

                        keys.Add(new EasingKey(
                            (float)quad.Items[0].AsDouble(),
                            (float)quad.Items[1].AsDouble(),
                            (float)quad.Items[2].AsDouble(),
                            (float)quad.Items[3].AsDouble()));
                    }

                    easings[key] = keys;
                }

                JsonObject juiceObj = root.Require("juice").AsObject();
                var juice = new Dictionary<string, JuiceEntry>(StringComparer.Ordinal);
                foreach (string key in juiceObj.MemberNames)
                {
                    JsonObject entry = juiceObj.Require(key).AsObject();
                    juice[key] = new JuiceEntry(
                        entry.Require("sfx").AsString(),
                        entry.Require("haptic").AsString(),
                        entry.Require("anim").AsString());
                }

                JsonObject accessibility = root.Require("accessibility").AsObject();

                return new UiTheme(
                    colors,
                    type,
                    scale,
                    spacing.Require("gutter").AsInt(),
                    spacing.Require("cardPadding").AsInt(),
                    radius,
                    durations,
                    easings,
                    juice,
                    (float)motion.Require("reducedMotionScale").AsDouble(),
                    accessibility.Require("minTouchTargetRefPx").AsInt());
            }
            catch (JsonParseException ex)
            {
                throw new ContentException(sourceLabel, ex.Message);
            }
        }
    }
}
