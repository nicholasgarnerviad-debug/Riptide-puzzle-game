using System.Collections.Generic;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// Tidal Pop sprite set (DECISIONS 2026-10-18) — still 100% code-generated.
    /// High-res mipmapped rounded/circle masks so chunky 60px corners stay crisp,
    /// the genre's sticker icons (filled masks + dilated outline masks), stars,
    /// sunburst and bubbles. Grayscale/alpha only: hue always arrives by tint
    /// from theme tokens, keeping the single-source color rule.
    /// </summary>
    public static class PopSprites
    {
        /// <summary>Sprite PPU = the canvas reference PPU, so slice borders are in ref-px.</summary>
        public const float PixelsPerUnit = 100f;

        /// <summary>Corner radius baked into <see cref="Rounded"/> (source px).</summary>
        public const int RoundedCorner = 96;

        private static Sprite? rounded;
        private static Sprite? circle;
        private static Sprite? sunburst;
        private static Sprite? bubble;
        private static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Sprite> outlines = new Dictionary<string, Sprite>();

        /// <summary>9-sliced rounded rect; pair with <see cref="CornerMultiplier"/>.</summary>
        public static Sprite Rounded()
        {
            if (rounded == null)
            {
                const int size = 256;
                Texture2D tex = MipTex(size, size);
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float a = RoundedCoverage(x, y, size, RoundedCorner);
                        px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                    }
                }

                tex.SetPixels32(px);
                tex.Apply(true);
                int b = RoundedCorner + 1;
                rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                    PixelsPerUnit, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            }

            return rounded;
        }

        private static readonly Dictionary<int, Sprite> worldRounded = new Dictionary<int, Sprite>();

        /// <summary>
        /// The rounded mask as a 9-sliced WORLD sprite whose corner radius is
        /// <paramref name="cornerWorld"/> units (SpriteRenderer drawMode Sliced).
        /// </summary>
        public static Sprite RoundedWorld(float cornerWorld)
        {
            int key = Mathf.RoundToInt(cornerWorld * 1000f);
            if (!worldRounded.TryGetValue(key, out Sprite sprite))
            {
                Sprite source = Rounded();
                float ppu = (RoundedCorner + 1) / Mathf.Max(0.02f, cornerWorld);
                int b = RoundedCorner + 1;
                sprite = Sprite.Create(source.texture, source.rect, new Vector2(0.5f, 0.5f), ppu, 0,
                    SpriteMeshType.FullRect, new Vector4(b, b, b, b));
                worldRounded[key] = sprite;
            }

            return sprite;
        }

        /// <summary>Image.pixelsPerUnitMultiplier that renders <see cref="Rounded"/> at a ref-px radius.</summary>
        public static float CornerMultiplier(float cornerRefPx) => RoundedCorner / Mathf.Max(4f, cornerRefPx);

        public static Sprite Circle()
        {
            if (circle == null)
            {
                const int size = 256;
                Texture2D tex = MipTex(size, size);
                var px = new Color32[size * size];
                float half = (size - 1) * 0.5f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half));
                        float a = Mathf.Clamp01(half - d + 0.5f);
                        px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                    }
                }

                tex.SetPixels32(px);
                tex.Apply(true);
                circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            }

            return circle;
        }

        /// <summary>Alternating soft rays fading out radially — the win-moment burst.</summary>
        public static Sprite Sunburst()
        {
            if (sunburst == null)
            {
                const int size = 256;
                Texture2D tex = MipTex(size, size);
                var px = new Color32[size * size];
                float half = (size - 1) * 0.5f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x - half;
                        float dy = y - half;
                        float r = Mathf.Sqrt(dx * dx + dy * dy) / half;
                        float ang = Mathf.Atan2(dy, dx);
                        float ray = Mathf.Clamp01(Mathf.Sin(ang * 14f) * 3f + 0.5f);
                        float fade = Mathf.Clamp01(1f - r) * Mathf.Clamp01(r * 4f);
                        px[y * size + x] = new Color32(255, 255, 255, (byte)(ray * fade * 255f));
                    }
                }

                tex.SetPixels32(px);
                tex.Apply(true);
                sunburst = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            }

            return sunburst;
        }

        /// <summary>Hollow bubble: ring plus a glint — rises through menus.</summary>
        public static Sprite Bubble()
        {
            if (bubble == null)
            {
                const int s = 64;
                float[,] a = new float[s, s];
                MenuSprites.Ring(a, 31.5f, 31.5f, 27f, 5f);
                MenuSprites.Disc(a, 22f, 42f, 5f);
                bubble = FromMask(a, mip: true);
            }

            return bubble;
        }

        private static readonly Dictionary<int, Sprite> waveLines = new Dictionary<int, Sprite>();

        /// <summary>Long horizontal sine stroke (512×96) at a stroke width — the wordmark's tide line.</summary>
        public static Sprite WaveLine(int strokePx)
        {
            if (!waveLines.TryGetValue(strokePx, out Sprite sprite))
            {
                const int w = 512;
                const int h = 96;
                var a = new float[w, h];
                float prevX = 14f;
                float prevY = 48f;
                for (int i = 1; i <= 96; i++)
                {
                    float x = 14f + i * (484f / 96f);
                    float y = 48f + Mathf.Sin(i / 96f * Mathf.PI * 2f * 3.5f) * 16f;
                    MenuSprites.Seg(a, prevX, prevY, x, y, strokePx);
                    prevX = x;
                    prevY = y;
                }

                sprite = FromMask(a, mip: true);
                waveLines[strokePx] = sprite;
            }

            return sprite;
        }

        /// <summary>Filled icon mask. Falls back to the legacy MenuSprites set for its ids.</summary>
        public static Sprite Icon(string id)
        {
            if (!icons.TryGetValue(id, out Sprite sprite))
            {
                float[,]? mask = Draw(id);
                sprite = mask != null ? FromMask(mask, mip: true) : MenuSprites.Icon(id);
                icons[id] = sprite;
            }

            return sprite;
        }

        /// <summary>The icon's mask dilated by <paramref name="px"/> — the sticker outline behind it.</summary>
        public static Sprite IconOutline(string id, int px = 9)
        {
            string key = id + "@" + px;
            if (!outlines.TryGetValue(key, out Sprite sprite))
            {
                float[,] mask = Draw(id) ?? MaskOf(MenuSprites.Icon(id));
                sprite = FromMask(Dilate(mask, px), mip: true);
                outlines[key] = sprite;
            }

            return sprite;
        }

        // ------------------------------------------------------------------ icons

        private static float[,]? Draw(string id)
        {
            const int s = 128;
            var a = new float[s, s];
            const float c = 63.5f;
            switch (id)
            {
                case "star":
                    Poly(a, StarPoints(c, c - 2f, 58f, 25f));
                    break;
                case "play":
                    Poly(a, new[] { new Vector2(40f, 22f), new Vector2(40f, 106f), new Vector2(108f, 64f) });
                    break;
                case "home":
                    Poly(a, new[] { new Vector2(10f, 62f), new Vector2(64f, 112f), new Vector2(118f, 62f) });
                    MenuSprites.RoundedBlock(a, 24f, 14f, 104f, 70f, 8f);
                    CutRect(a, 52f, 14f, 76f, 46f);
                    break;
                case "retry":
                    Arc(a, c, c, 38f, 15f, 40f, 330f);
                    Poly(a, new[] { new Vector2(86f, 112f), new Vector2(118f, 84f), new Vector2(80f, 72f) });
                    break;
                case "map":
                    Poly(a, new[] { new Vector2(12f, 26f), new Vector2(12f, 100f), new Vector2(44f, 112f), new Vector2(44f, 38f) });
                    Poly(a, new[] { new Vector2(50f, 38f), new Vector2(50f, 112f), new Vector2(78f, 100f), new Vector2(78f, 26f) });
                    Poly(a, new[] { new Vector2(84f, 26f), new Vector2(84f, 100f), new Vector2(116f, 112f), new Vector2(116f, 38f) });
                    break;
                case "lock":
                    MenuSprites.RoundedBlock(a, 26f, 10f, 102f, 70f, 12f);
                    MenuSprites.HandleArc(a, c, 70f, 24f, 14f, 66f);
                    MenuSprites.Seg(a, c - 24f, 66f, c - 24f, 76f, 14f);
                    MenuSprites.Seg(a, c + 24f, 66f, c + 24f, 76f, 14f);
                    MenuSprites.Cut(a, c, 44f, 8f);
                    CutRect(a, c - 4f, 22f, c + 4f, 42f);
                    break;
                case "check":
                    MenuSprites.Seg(a, 20f, 66f, 50f, 34f, 20f);
                    MenuSprites.Seg(a, 50f, 34f, 108f, 96f, 20f);
                    break;
                case "plus":
                    MenuSprites.Seg(a, c, 18f, c, 110f, 24f);
                    MenuSprites.Seg(a, 18f, c, 110f, c, 24f);
                    break;
                case "close":
                    MenuSprites.Seg(a, 26f, 26f, 102f, 102f, 22f);
                    MenuSprites.Seg(a, 26f, 102f, 102f, 26f, 22f);
                    break;
                case "chevronLeft":
                    MenuSprites.Seg(a, 82f, 108f, 40f, c, 22f);
                    MenuSprites.Seg(a, 40f, c, 82f, 20f, 22f);
                    break;
                case "chevronRight":
                    MenuSprites.Seg(a, 46f, 108f, 88f, c, 22f);
                    MenuSprites.Seg(a, 88f, c, 46f, 20f, 22f);
                    break;
                case "drop":
                    MenuSprites.Disc(a, c, 48f, 38f);
                    Poly(a, new[] { new Vector2(28f, 58f), new Vector2(c, 120f), new Vector2(99f, 58f) });
                    MenuSprites.Cut(a, 48f, 44f, 9f);
                    break;
                case "bomb":
                    MenuSprites.Disc(a, 56f, 52f, 42f);
                    MenuSprites.Seg(a, 82f, 82f, 100f, 100f, 12f);
                    MenuSprites.Disc(a, 108f, 108f, 12f);
                    MenuSprites.Cut(a, 40f, 66f, 9f);
                    break;
                case "reroll":
                    Arc(a, c, c, 40f, 14f, 20f, 160f);
                    Arc(a, c, c, 40f, 14f, 200f, 340f);
                    Poly(a, new[] { new Vector2(6f, 66f), new Vector2(42f, 66f), new Vector2(24f, 90f) });
                    Poly(a, new[] { new Vector2(86f, 62f), new Vector2(122f, 62f), new Vector2(104f, 38f) });
                    break;
                case "swap":
                    MenuSprites.Seg(a, 18f, 86f, 94f, 86f, 16f);
                    Poly(a, new[] { new Vector2(86f, 110f), new Vector2(118f, 86f), new Vector2(86f, 62f) });
                    MenuSprites.Seg(a, 34f, 42f, 110f, 42f, 16f);
                    Poly(a, new[] { new Vector2(42f, 66f), new Vector2(10f, 42f), new Vector2(42f, 18f) });
                    break;
                case "crown":
                    Poly(a, new[]
                    {
                        new Vector2(14f, 30f), new Vector2(8f, 96f), new Vector2(40f, 68f), new Vector2(64f, 112f),
                        new Vector2(88f, 68f), new Vector2(120f, 96f), new Vector2(114f, 30f),
                    });
                    MenuSprites.RoundedBlock(a, 14f, 12f, 114f, 28f, 6f);
                    break;
                case "flame":
                    // Three licking tongues over a round base, hollowed core —
                    // nothing like the (smooth, single-point) water drop.
                    MenuSprites.Disc(a, c, 44f, 38f);
                    Poly(a, new[]
                    {
                        new Vector2(26f, 50f), new Vector2(30f, 92f), new Vector2(46f, 74f), new Vector2(60f, 124f),
                        new Vector2(76f, 86f), new Vector2(90f, 104f), new Vector2(102f, 50f),
                    });
                    MenuSprites.Cut(a, c, 38f, 15f);
                    MenuSprites.Cut(a, c, 54f, 10f);
                    break;
                case "calendar":
                    MenuSprites.RoundedBlock(a, 14f, 10f, 114f, 104f, 14f);
                    CutRect(a, 24f, 20f, 104f, 76f);
                    MenuSprites.RoundedBlock(a, 34f, 30f, 56f, 50f, 4f);
                    MenuSprites.Seg(a, 40f, 96f, 40f, 120f, 12f);
                    MenuSprites.Seg(a, 88f, 96f, 88f, 120f, 12f);
                    break;
                case "trophy":
                    Poly(a, new[] { new Vector2(26f, 112f), new Vector2(102f, 112f), new Vector2(88f, 62f), new Vector2(40f, 62f) });
                    MenuSprites.Disc(a, c, 66f, 26f);
                    MenuSprites.Ring(a, 26f, 92f, 14f, 9f);
                    MenuSprites.Ring(a, 102f, 92f, 14f, 9f);
                    MenuSprites.RoundedBlock(a, 56f, 26f, 72f, 50f, 2f);
                    MenuSprites.RoundedBlock(a, 34f, 10f, 94f, 28f, 6f);
                    break;
                case "sound":
                    Poly(a, new[] { new Vector2(10f, 44f), new Vector2(10f, 84f), new Vector2(36f, 84f), new Vector2(66f, 110f), new Vector2(66f, 18f), new Vector2(36f, 44f) });
                    Arc(a, 66f, c, 26f, 10f, -50f, 50f);
                    Arc(a, 66f, c, 46f, 10f, -50f, 50f);
                    break;
                case "music":
                    MenuSprites.Disc(a, 36f, 28f, 20f);
                    MenuSprites.Disc(a, 96f, 40f, 20f);
                    MenuSprites.Seg(a, 52f, 30f, 52f, 108f, 12f);
                    MenuSprites.Seg(a, 112f, 42f, 112f, 118f, 12f);
                    Poly(a, new[] { new Vector2(46f, 96f), new Vector2(46f, 118f), new Vector2(118f, 124f), new Vector2(118f, 104f) });
                    break;
                case "vibrate":
                    MenuSprites.RoundedBlock(a, 40f, 12f, 88f, 116f, 10f);
                    CutRect(a, 48f, 26f, 80f, 100f);
                    MenuSprites.Seg(a, 18f, 44f, 18f, 84f, 10f);
                    MenuSprites.Seg(a, 110f, 44f, 110f, 84f, 10f);
                    break;
                case "shield":
                    Poly(a, new[] { new Vector2(c, 120f), new Vector2(112f, 100f), new Vector2(104f, 46f), new Vector2(c, 8f), new Vector2(24f, 46f), new Vector2(16f, 100f) });
                    break;
                case "doc":
                    MenuSprites.RoundedBlock(a, 24f, 8f, 104f, 120f, 12f);
                    CutRect(a, 40f, 84f, 88f, 94f);
                    CutRect(a, 40f, 60f, 88f, 70f);
                    CutRect(a, 40f, 36f, 72f, 46f);
                    break;
                case "share":
                    MenuSprites.Seg(a, 34f, c, 94f, 98f, 12f);
                    MenuSprites.Seg(a, 34f, c, 94f, 30f, 12f);
                    MenuSprites.Disc(a, 32f, c, 20f);
                    MenuSprites.Disc(a, 96f, 100f, 20f);
                    MenuSprites.Disc(a, 96f, 28f, 20f);
                    break;
                case "coin":
                    MenuSprites.Disc(a, c, c, 56f);
                    MenuSprites.Ring(a, c, c, 38f, 7f);
                    break;
                case "rows":
                    MenuSprites.RoundedBlock(a, 12f, 84f, 116f, 112f, 10f);
                    MenuSprites.RoundedBlock(a, 12f, 50f, 116f, 78f, 10f);
                    MenuSprites.RoundedBlock(a, 12f, 16f, 116f, 44f, 10f);
                    break;
                case "pause":
                    MenuSprites.RoundedBlock(a, 30f, 18f, 56f, 110f, 9f);
                    MenuSprites.RoundedBlock(a, 72f, 18f, 98f, 110f, 9f);
                    break;
                case "motion":
                    MenuSprites.Wave(a, 40f, 12f);
                    MenuSprites.Wave(a, 86f, 12f);
                    break;
                default:
                    return null;
            }

            return a;
        }

        private static Vector2[] StarPoints(float cx, float cy, float outer, float inner)
        {
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float ang = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? outer : inner;
                pts[i] = new Vector2(cx + Mathf.Cos(ang) * r, cy + Mathf.Sin(ang) * r);
            }

            return pts;
        }

        // ------------------------------------------------------------ rasterizers

        /// <summary>Anti-aliased polygon fill (4×4 supersampled even-odd test).</summary>
        private static void Poly(float[,] a, Vector2[] pts)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (Vector2 p in pts)
            {
                minX = Mathf.Min(minX, p.x);
                minY = Mathf.Min(minY, p.y);
                maxX = Mathf.Max(maxX, p.x);
                maxY = Mathf.Max(maxY, p.y);
            }

            for (int y = (int)minY - 1; y <= maxY + 1; y++)
            {
                for (int x = (int)minX - 1; x <= maxX + 1; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < 4; sy++)
                    {
                        for (int sx = 0; sx < 4; sx++)
                        {
                            if (Inside(pts, x + (sx + 0.5f) / 4f - 0.5f, y + (sy + 0.5f) / 4f - 0.5f))
                            {
                                hits++;
                            }
                        }
                    }

                    if (hits > 0)
                    {
                        MenuSprites.Blend(a, x, y, hits / 16f);
                    }
                }
            }
        }

        private static bool Inside(Vector2[] pts, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
            {
                if ((pts[i].y > y) != (pts[j].y > y)
                    && x < (pts[j].x - pts[i].x) * (y - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>Thick ring arc between two angles (degrees, CCW from +x).</summary>
        private static void Arc(float[,] a, float cx, float cy, float r, float w, float fromDeg, float toDeg)
        {
            int steps = Mathf.Max(8, (int)(Mathf.Abs(toDeg - fromDeg) / 6f));
            float prevX = cx + Mathf.Cos(fromDeg * Mathf.Deg2Rad) * r;
            float prevY = cy + Mathf.Sin(fromDeg * Mathf.Deg2Rad) * r;
            for (int i = 1; i <= steps; i++)
            {
                float ang = Mathf.Lerp(fromDeg, toDeg, (float)i / steps) * Mathf.Deg2Rad;
                float x = cx + Mathf.Cos(ang) * r;
                float y = cy + Mathf.Sin(ang) * r;
                MenuSprites.Seg(a, prevX, prevY, x, y, w);
                prevX = x;
                prevY = y;
            }
        }

        private static void CutRect(float[,] a, float x1, float y1, float x2, float y2)
        {
            for (int y = (int)y1; y < y2; y++)
            {
                for (int x = (int)x1; x < x2; x++)
                {
                    if (x >= 0 && y >= 0 && x < a.GetLength(0) && y < a.GetLength(1))
                    {
                        a[x, y] = 0f;
                    }
                }
            }
        }

        private static float[,] Dilate(float[,] src, int r)
        {
            int w = src.GetLength(0);
            int h = src.GetLength(1);
            int pad = r + 2;
            // Grow the canvas so the outline never clips at the icon's edge; the
            // sprite keeps the same footprint by scaling at the use site.
            var dst = new float[w + 2 * pad, h + 2 * pad];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float v = src[x, y];
                    if (v <= 0f)
                    {
                        continue;
                    }

                    for (int dy = -r - 1; dy <= r + 1; dy++)
                    {
                        for (int dx = -r - 1; dx <= r + 1; dx++)
                        {
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            float cov = Mathf.Clamp01(r - d + 0.5f) * v;
                            int tx = x + dx + pad;
                            int ty = y + dy + pad;
                            if (cov > dst[tx, ty])
                            {
                                dst[tx, ty] = cov;
                            }
                        }
                    }
                }
            }

            return dst;
        }

        private static float[,] MaskOf(Sprite sprite)
        {
            Texture2D tex = sprite.texture;
            var mask = new float[tex.width, tex.height];
            Color32[] px = tex.GetPixels32();
            for (int y = 0; y < tex.height; y++)
            {
                for (int x = 0; x < tex.width; x++)
                {
                    mask[x, y] = px[y * tex.width + x].a / 255f;
                }
            }

            return mask;
        }

        /// <summary>Padding (source px per side) Dilate adds — callers scale outlines up by this.</summary>
        public static float OutlineScale(int px, int iconSize = 128) => (iconSize + 2f * (px + 2)) / iconSize;

        private static Sprite FromMask(float[,] a, bool mip)
        {
            int w = a.GetLength(0);
            int h = a.GetLength(1);
            Texture2D tex = mip ? MipTex(w, h) : MenuSprites.NewTex(w, h);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a[x, y]) * 255f));
                }
            }

            tex.SetPixels32(px);
            tex.Apply(mip);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        }

        private static Texture2D MipTex(int w, int h) =>
            new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

        private static float RoundedCoverage(int x, int y, int size, int corner)
        {
            float cx = Mathf.Clamp(x, corner, size - 1 - corner);
            float cy = Mathf.Clamp(y, corner, size - 1 - corner);
            float dx = x - cx;
            float dy = y - cy;
            return Mathf.Clamp01(corner - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
        }
    }
}
