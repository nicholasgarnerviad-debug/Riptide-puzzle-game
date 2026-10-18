using System.Collections.Generic;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// GDD §16.6 Tidelings screen art in the PopSprites 128px grid: the Conch as
    /// stackable layers (shell, bands, lip, inner lip, glint — tint each and stack
    /// them in one holder), the pity ring, a four-point Sparkle glint and the
    /// grow-up whirlpool. Shapes come from the approved mockup's 120×100 conch.
    /// </summary>
    public static partial class PopSprites
    {
        /// <summary>Crisp-edged size for art shown large (the Conch, the pity rings): 3× the icon grid.</summary>
        public const int ShellSize = 384;

        private const int ShellScale = ShellSize / 128;
        private static readonly Dictionary<string, Sprite> shells = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Sprite> shellOutlines = new Dictionary<string, Sprite>();
        private static float k = 1f;

        /// <summary>A shell layer drawn at <see cref="ShellSize"/> (crisp at the Conch's sizes).</summary>
        public static Sprite Shell(string id)
        {
            if (!shells.TryGetValue(id, out Sprite sprite))
            {
                sprite = FromMask(BigMask(id), mip: true);
                shells[id] = sprite;
            }

            return sprite;
        }

        /// <summary>A shell layer's outline, <paramref name="px"/> icon-grid pixels wide.</summary>
        public static Sprite ShellOutline(string id, int px)
        {
            string key = id + "@" + px;
            if (!shellOutlines.TryGetValue(key, out Sprite sprite))
            {
                sprite = FromMask(Dilate(BigMask(id), px * ShellScale), mip: true);
                shellOutlines[key] = sprite;
            }

            return sprite;
        }

        public static float ShellOutlineScale(int px) => OutlineScale(px * ShellScale, ShellSize);

        private static float[,] BigMask(string id)
        {
            var a = new float[ShellSize, ShellSize];
            k = ShellScale;
            try
            {
                DrawShell(id, a);
            }
            finally
            {
                k = 1f;
            }

            return a;
        }

        private static bool DrawShell(string id, float[,] a)
        {
            switch (id)
            {
                case "conchShell":
                    Poly(a, Path(new[] { 38f, 36f, 26f, 36f, 15f, 43f, 5f, 50f, 15f, 57f, 26f, 64f, 38f, 64f }, closed: false));
                    Poly(a, Body());
                    foreach ((float x, float y, float r) in new[] { (50f, 21f, 5.5f), (66f, 15f, 6f), (83f, 16f, 5.5f) })
                    {
                        MenuSprites.Disc(a, Gx(x), Gy(y), r * k);
                    }

                    return true;
                case "conchBands":
                    Stroke(a, new[] { 48f, 26f, 42f, 40f, 42f, 62f, 50f, 80f }, 3.6f);
                    Stroke(a, new[] { 62f, 19f, 56f, 38f, 56f, 64f, 62f, 86f }, 3.6f);
                    Stroke(a, new[] { 22f, 41f, 25f, 47f, 25f, 53f, 22f, 59f }, 3f);
                    Stroke(a, new[] { 13f, 45f, 15f, 48f, 15f, 52f, 13f, 55f }, 3f);
                    return true;
                case "conchLip":
                    Poly(a, Curve(new[] { 72f, 38f, 90f, 28f, 114f, 40f, 112f, 62f, 110f, 80f, 92f, 90f, 74f, 84f, 86f, 72f, 84f, 52f, 72f, 38f }));
                    return true;
                case "conchLipIn":
                    Poly(a, Curve(new[] { 80f, 46f, 93f, 43f, 103f, 53f, 101f, 64f, 99f, 74f, 90f, 79f, 84f, 77f, 89f, 68f, 88f, 55f, 80f, 46f }));
                    return true;
                case "conchGlint":
                    MenuSprites.Oval(a, Gx(52f), Gy(34f), 9f * k, 4.5f * k);
                    return true;
                case "tideling":
                    // The Tidelings tab: a round little sea friend with stalks and a smile.
                    MenuSprites.Seg(a, 44f, 78f, 34f, 98f, 9f);
                    MenuSprites.Seg(a, 83f, 78f, 93f, 98f, 9f);
                    MenuSprites.Disc(a, 34f, 100f, 11f);
                    MenuSprites.Disc(a, 93f, 100f, 11f);
                    MenuSprites.Oval(a, 63.5f, 52f, 54f, 42f);
                    MenuSprites.Cut(a, 46f, 58f, 8f);
                    MenuSprites.Cut(a, 81f, 58f, 8f);
                    for (float x = 53f; x <= 74f; x += 1f)
                    {
                        MenuSprites.Cut(a, x, 34f + (x - 63.5f) * (x - 63.5f) / 22f, 2.6f);
                    }

                    return true;
                case "ring":
                    MenuSprites.Ring(a, 63.5f * k, 63.5f * k, 58f * k, 7f * k);
                    return true;
                case "glint":
                    Poly(a, Glint(63.5f, 63.5f, 60f, 17f));
                    return true;
                case "swirl":
                    // Three spiral arms of a whirlpool, thickening outward.
                    for (int arm = 0; arm < 3; arm++)
                    {
                        float prevX = 63.5f;
                        float prevY = 63.5f;
                        for (int i = 1; i <= 60; i++)
                        {
                            float t = i / 60f;
                            float ang = arm * 120f * Mathf.Deg2Rad + t * 4.4f;
                            float x = 63.5f + Mathf.Cos(ang) * t * 58f;
                            float y = 63.5f + Mathf.Sin(ang) * t * 58f;
                            MenuSprites.Seg(a, prevX, prevY, x, y, 2f + t * 9f);
                            prevX = x;
                            prevY = y;
                        }
                    }

                    return true;
                default:
                    return false;
            }
        }

        // The mockup draws in a y-down 120×100 box; the mask grid is y-up 128².
        private static float Gx(float x) => (x + 4f) * k;

        private static float Gy(float y) => (114f - y) * k;

        private static Vector2[] Body() =>
            Curve(new[] { 32f, 50f, 32f, 28f, 54f, 14f, 78f, 17f, 100f, 20f, 114f, 38f, 111f, 58f, 108f, 78f, 88f, 90f, 66f, 88f, 46f, 86f, 32f, 72f, 32f, 50f });

        /// <summary>Straight polyline points (x, y pairs) in mockup space.</summary>
        private static Vector2[] Path(float[] xy, bool closed)
        {
            var pts = new List<Vector2>(xy.Length / 2);
            for (int i = 0; i + 1 < xy.Length; i += 2)
            {
                pts.Add(new Vector2(Gx(xy[i]), Gy(xy[i + 1])));
            }

            if (closed && pts.Count > 0)
            {
                pts.Add(pts[0]);
            }

            return pts.ToArray();
        }

        /// <summary>A chain of cubic Béziers (start, then 3 points per segment), sampled.</summary>
        private static Vector2[] Curve(float[] xy)
        {
            Vector2[] p = Path(xy, closed: false);
            var pts = new List<Vector2> { p[0] };
            for (int i = 1; i + 2 < p.Length; i += 3)
            {
                for (int s = 1; s <= 12; s++)
                {
                    float t = s / 12f;
                    float u = 1f - t;
                    pts.Add(u * u * u * p[i - 1] + 3f * u * u * t * p[i] + 3f * u * t * t * p[i + 1] + t * t * t * p[i + 2]);
                }
            }

            return pts.ToArray();
        }

        private static void Stroke(float[,] a, float[] xy, float width)
        {
            Vector2[] pts = Curve(xy);
            for (int i = 1; i < pts.Length; i++)
            {
                MenuSprites.Seg(a, pts[i - 1].x, pts[i - 1].y, pts[i].x, pts[i].y, width * k);
            }
        }

        private static Vector2[] Glint(float cx, float cy, float r, float waist)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i < 4; i++)
            {
                float tip = i * 90f * Mathf.Deg2Rad;
                float mid = tip + 45f * Mathf.Deg2Rad;
                pts.Add(new Vector2(cx + Mathf.Cos(tip) * r, cy + Mathf.Sin(tip) * r));
                pts.Add(new Vector2(cx + Mathf.Cos(mid) * waist, cy + Mathf.Sin(mid) * waist));
            }

            return pts.ToArray();
        }
    }
}
