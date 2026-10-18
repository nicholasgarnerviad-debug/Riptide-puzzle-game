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
                case "egg":
                    // GDD §16.8: a sea egg — wave-banded, never a white egg with spots.
                    MenuSprites.Oval(a, c, 56f, 44f, 54f);
                    for (float x = 22f; x <= 106f; x += 2f)
                    {
                        MenuSprites.Cut(a, x, 62f + 7f * Mathf.Sin(x / 8f), 4.5f);
                    }

                    return true;
                case "conch":
                    // The Call's conch: a spiral shell with a flared lip.
                    Poly(a, new[]
                    {
                        new Vector2(18f, 52f), new Vector2(46f, 22f), new Vector2(98f, 34f), new Vector2(116f, 76f),
                        new Vector2(100f, 112f), new Vector2(66f, 116f), new Vector2(34f, 92f),
                    });
                    for (float t = 0f; t < 1f; t += 0.04f)
                    {
                        MenuSprites.Cut(a, 66f + 30f * t * Mathf.Cos(t * 9f), 72f + 30f * t * Mathf.Sin(t * 9f), 3.2f);
                    }

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
