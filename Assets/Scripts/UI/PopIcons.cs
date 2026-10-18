using UnityEngine;

namespace Riptide.UI
{
    /// <summary>GDD §15 Pets &amp; Tides icons (gift, heart), in the PopSprites 128px grid.</summary>
    public static partial class PopSprites
    {
        private static bool DrawMore(string id, float[,] a)
        {
            const float c = 63.5f;
            switch (id)
            {
                case "gift":
                    MenuSprites.RoundedBlock(a, 20f, 10f, 108f, 68f, 8f);
                    MenuSprites.RoundedBlock(a, 12f, 74f, 116f, 96f, 8f);
                    CutRect(a, 57f, 10f, 71f, 96f);
                    MenuSprites.Ring(a, 46f, 108f, 14f, 9f);
                    MenuSprites.Ring(a, 82f, 108f, 14f, 9f);
                    return true;
                case "heart":
                    MenuSprites.Disc(a, 42f, 80f, 30f);
                    MenuSprites.Disc(a, 86f, 80f, 30f);
                    Poly(a, new[] { new Vector2(14f, 72f), new Vector2(114f, 72f), new Vector2(c, 12f) });
                    return true;
                default:
                    return false;
            }
        }
    }
}
