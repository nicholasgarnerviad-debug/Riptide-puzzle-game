using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Riptide.Core
{
    /// <summary>
    /// GDD §16 Tidelings save state (save v5), its own JSON object inside the save so
    /// SaveData stays readable. Per line: bond, stage (0 = not owned), Sparkle owned /
    /// shown, Dex line reward paid. Eggs: the nest queue, the incubating egg and its
    /// warmth (hundredths of a row). The Pod, egg-source keys, once-a-day guards, the
    /// per-save hatch seed and counters.
    /// </summary>
    public sealed class PalSave
    {
        public const int MaxLines = 32;
        public const int MaxNest = 16;
        public const int MaxPod = 3;
        public const int MaxEggKeys = 256;

        public int[] Bond = new int[0];
        public int[] Stage = new int[0];
        public bool[] Sparkle = new bool[0];
        public bool[] SparkleOn = new bool[0];
        public bool[] LineRewarded = new bool[0];
        public List<int> Nest = new List<int>();
        public int Incubating = -1;
        public int Warmth;
        public int[] Pod = { -1, -1, -1 };
        public List<string> EggKeys = new List<string>();
        public long DailyEggDay = -1;
        public long QuestEggDay = -1;
        public int EventEggWeek = -1;
        public long ContinueDay = -1;
        public int ContinuesUsed;
        public int EggsHatched;
        public int EggsGranted;
        public ulong Seed;
        public bool WelcomeGiven;

        public int Leader => Pod[0];

        /// <summary>Grows the per-line arrays to the content's line count (never shrinks).</summary>
        public void EnsureLines(int count)
        {
            if (Stage.Length >= count)
            {
                return;
            }

            Bond = Grow(Bond, count);
            Stage = Grow(Stage, count);
            Sparkle = Grow(Sparkle, count);
            SparkleOn = Grow(SparkleOn, count);
            LineRewarded = Grow(LineRewarded, count);
        }

        public bool Owns(int line) => line >= 0 && line < Stage.Length && Stage[line] > 0;

        public int StageOf(int line) => line >= 0 && line < Stage.Length ? Stage[line] : 0;

        public int BondOf(int line) => line >= 0 && line < Bond.Length ? Bond[line] : 0;

        public int OwnedCount()
        {
            int owned = 0;
            foreach (int stage in Stage)
            {
                owned += stage > 0 ? 1 : 0;
            }

            return owned;
        }

        public string Serialize()
        {
            var sb = new StringBuilder(256);
            sb.Append('{');
            WriteInts(sb, "bond", Bond);
            WriteInts(sb, "stage", Stage);
            WriteBools(sb, "sparkle", Sparkle);
            WriteBools(sb, "sparkleOn", SparkleOn);
            WriteBools(sb, "lineRewarded", LineRewarded);
            WriteInts(sb, "nest", Nest.ToArray());
            WriteInts(sb, "pod", Pod);
            sb.Append("\"eggKeys\": [");
            for (int i = 0; i < EggKeys.Count; i++)
            {
                sb.Append(i > 0 ? ", " : "").Append('"').Append(EggKeys[i].Replace("\\", "").Replace("\"", "")).Append('"');
            }

            sb.Append("], ");
            sb.Append($"\"incubating\": {Incubating}, \"warmth\": {Warmth}, ");
            sb.Append($"\"days\": [{DailyEggDay}, {QuestEggDay}, {EventEggWeek}, {ContinueDay}, {ContinuesUsed}], ");
            sb.Append($"\"counts\": [{EggsHatched}, {EggsGranted}], ");
            sb.Append($"\"seed\": \"{Seed.ToString(CultureInfo.InvariantCulture)}\", ");
            sb.Append($"\"welcome\": {(WelcomeGiven ? "true" : "false")}");
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>Throws (JsonParseException / ArgumentException) on any malformation — the save treats it as corrupt.</summary>
        public static PalSave Parse(JsonObject o)
        {
            var pals = new PalSave
            {
                Bond = IntArray(o, "bond", MaxLines),
                Stage = IntArray(o, "stage", MaxLines),
                Sparkle = BoolArray(o, "sparkle"),
                SparkleOn = BoolArray(o, "sparkleOn"),
                LineRewarded = BoolArray(o, "lineRewarded"),
                Incubating = o.Require("incubating").AsInt(),
                Warmth = Math.Max(0, o.Require("warmth").AsInt()),
                WelcomeGiven = o.Require("welcome").AsBool(),
            };

            int lines = pals.Stage.Length;
            if (pals.Bond.Length != lines || pals.Sparkle.Length != lines || pals.SparkleOn.Length != lines
                || pals.LineRewarded.Length != lines || pals.Incubating < -1)
            {
                throw new ArgumentException("pal arrays disagree");
            }

            foreach (int stage in pals.Stage)
            {
                if (stage < 0 || stage > PalsLoader.StageCount)
                {
                    throw new ArgumentException("pal stage out of range");
                }
            }

            pals.Nest.AddRange(IntArray(o, "nest", MaxNest));
            int[] pod = IntArray(o, "pod", MaxPod, allowNegativeOne: true);
            if (pod.Length != MaxPod)
            {
                throw new ArgumentException("pod must have 3 slots");
            }

            pals.Pod = pod;
            JsonArray keys = o.Require("eggKeys").AsArray();
            if (keys.Count > MaxEggKeys)
            {
                throw new ArgumentException("too many egg keys");
            }

            foreach (JsonValue key in keys.Items)
            {
                pals.EggKeys.Add(key.AsString());
            }

            JsonArray days = o.Require("days").AsArray();
            JsonArray counts = o.Require("counts").AsArray();
            if (days.Count != 5 || counts.Count != 2)
            {
                throw new ArgumentException("pal day/count arrays malformed");
            }

            pals.DailyEggDay = days.Items[0].AsLong();
            pals.QuestEggDay = days.Items[1].AsLong();
            pals.EventEggWeek = days.Items[2].AsInt();
            pals.ContinueDay = days.Items[3].AsLong();
            pals.ContinuesUsed = Math.Max(0, days.Items[4].AsInt());
            pals.EggsHatched = Math.Max(0, counts.Items[0].AsInt());
            pals.EggsGranted = Math.Max(0, counts.Items[1].AsInt());
            if (!ulong.TryParse(o.Require("seed").AsString(), NumberStyles.None, CultureInfo.InvariantCulture,
                    out pals.Seed))
            {
                throw new ArgumentException("pal seed malformed");
            }

            return pals;
        }

        private static void WriteInts(StringBuilder sb, string name, int[] values)
        {
            sb.Append('"').Append(name).Append("\": [");
            for (int i = 0; i < values.Length; i++)
            {
                sb.Append(i > 0 ? ", " : "").Append(values[i]);
            }

            sb.Append("], ");
        }

        private static void WriteBools(StringBuilder sb, string name, bool[] values)
        {
            sb.Append('"').Append(name).Append("\": [");
            for (int i = 0; i < values.Length; i++)
            {
                sb.Append(i > 0 ? ", " : "").Append(values[i] ? "true" : "false");
            }

            sb.Append("], ");
        }

        private static int[] IntArray(JsonObject o, string name, int max, bool allowNegativeOne = false)
        {
            JsonArray array = o.Require(name).AsArray();
            if (array.Count > max)
            {
                throw new ArgumentException($"{name} too long");
            }

            var result = new int[array.Count];
            for (int i = 0; i < array.Count; i++)
            {
                result[i] = array.Items[i].AsInt();
                if (result[i] < (allowNegativeOne ? -1 : 0))
                {
                    throw new ArgumentException($"{name} has a negative entry");
                }
            }

            return result;
        }

        private static bool[] BoolArray(JsonObject o, string name)
        {
            JsonArray array = o.Require(name).AsArray();
            if (array.Count > MaxLines)
            {
                throw new ArgumentException($"{name} too long");
            }

            var result = new bool[array.Count];
            for (int i = 0; i < array.Count; i++)
            {
                result[i] = array.Items[i].AsBool();
            }

            return result;
        }

        private static T[] Grow<T>(T[] source, int count)
        {
            var grown = new T[count];
            System.Array.Copy(source, grown, source.Length);
            return grown;
        }
    }
}
