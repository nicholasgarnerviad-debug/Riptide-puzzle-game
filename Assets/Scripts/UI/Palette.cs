using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// UI spec §1.1: every game-screen color resolves through ui_theme.json tokens
    /// (4-UI-c retired the hardcoded constants this class used to hold). The class
    /// survives as the block/water vocabulary the world-space views share.
    /// </summary>
    public static class Palette
    {
        public static Color Background => ThemeRuntime.Color("bg.abyss");

        /// <summary>cyan, teal, coral pink, amber, violet, ice — §7.1 order.</summary>
        private static readonly string[] BlockKeys =
        {
            "block.cyan", "block.teal", "block.coralPink", "block.amber", "block.violet", "block.ice",
        };

        /// <summary>The block palette as resolved colors (tests assert order/wrap).</summary>
        public static Color[] Blocks
        {
            get
            {
                var colors = new Color[BlockKeys.Length];
                for (int i = 0; i < BlockKeys.Length; i++)
                {
                    colors[i] = ThemeRuntime.Color(BlockKeys[i]);
                }

                return colors;
            }
        }

        public static Color Coral => ThemeRuntime.Color("block.dead");
        public static Color EmptyCell => ThemeRuntime.Color("board.emptyCell");

        public static Color WaterDeep => ThemeRuntime.Color("water.calm.btm");
        public static Color WaterShallow => ThemeRuntime.Color("water.calm.top");
        public static Color FoamLine => ThemeRuntime.Color("water.foamLine");
        public static Color DangerPulse => ThemeRuntime.Color("danger");

        public static Color GhostValid => ThemeRuntime.Color("ghost.legal");
        public static Color GhostInvalid => ThemeRuntime.Color("ghost.illegal");

        public static Color MeterFilled => ThemeRuntime.Color("accent.primary");
        public static Color MeterEmpty => ThemeRuntime.Color("meter.track");
        public static Color MeterDanger => ThemeRuntime.Color("danger");

        public static Color BlockColor(byte colorId) =>
            ThemeRuntime.Color(BlockKeys[colorId % BlockKeys.Length]);

        /// <summary>Placeholder creature tints (provenance: generated art, not theme tokens).</summary>
        public static Color CreatureColor(byte creatureId) =>
            Color.HSVToRGB((0.08f + creatureId * 0.13f) % 1f, 0.45f, 1f);
    }
}
