using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// GDD 7.1 visual identity: near-black deep ocean base, bioluminescent block
    /// palette, bone-gray dead coral. Brand constants, not balance data.
    /// </summary>
    public static class Palette
    {
        public static readonly Color Background = Hex("0A0E14");

        /// <summary>cyan, teal, coral pink, amber, violet, ice — §7.1 order.</summary>
        public static readonly Color[] Blocks =
        {
            Hex("3EE6E0"), Hex("2BB59A"), Hex("FF6B81"), Hex("FFB341"), Hex("8C7BFF"), Hex("BFE8FF"),
        };

        public static readonly Color Coral = Hex("5A5F66");
        public static readonly Color EmptyCell = new Color(1f, 1f, 1f, 0.05f);

        public static readonly Color WaterDeep = new Color(0.07f, 0.35f, 0.45f, 0.34f);
        public static readonly Color WaterShallow = new Color(0.24f, 0.90f, 0.88f, 0.22f);
        public static readonly Color FoamLine = new Color(0.75f, 1f, 0.98f, 0.85f);
        public static readonly Color DangerPulse = new Color(1f, 0.30f, 0.35f, 0.9f);

        public static readonly Color GhostValid = new Color(0.24f, 0.9f, 0.88f, 0.55f);
        public static readonly Color GhostInvalid = new Color(1f, 0.32f, 0.4f, 0.55f);

        public static readonly Color MeterFilled = Hex("3EE6E0");
        public static readonly Color MeterEmpty = new Color(1f, 1f, 1f, 0.16f);
        public static readonly Color MeterDanger = Hex("FF6B81");

        public static Color BlockColor(byte colorId) => Blocks[colorId % Blocks.Length];

        public static Color CreatureColor(byte creatureId) =>
            Color.HSVToRGB((0.08f + creatureId * 0.13f) % 1f, 0.45f, 1f);

        private static Color Hex(string rrggbb)
        {
            int r = System.Convert.ToInt32(rrggbb.Substring(0, 2), 16);
            int g = System.Convert.ToInt32(rrggbb.Substring(2, 2), 16);
            int b = System.Convert.ToInt32(rrggbb.Substring(4, 2), 16);
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }
    }
}
