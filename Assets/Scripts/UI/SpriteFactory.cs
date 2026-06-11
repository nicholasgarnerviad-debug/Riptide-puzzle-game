using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// All Phase 4 sprites are generated in code (DECISIONS.md: no hand-authored
    /// assets). Rounded cells with a soft inner glow + luminous edge per GDD 7.1;
    /// eyed circles stand in for creature art until the Phase 8 asset session.
    /// </summary>
    public static class SpriteFactory
    {
        private static Sprite? cell;
        private static Sprite? creature;
        private static Sprite? solid;
        private static Sprite? dot;
        private static Sprite? caustic;
        private static Sprite? roundedFill;
        private static Sprite? roundedStroke;

        public const float PixelsPerUnit = 64f;

        /// <summary>9-sliced rounded-rect fill for the board frame (spec §4.3 r.s corners).</summary>
        public static Sprite RoundedFill()
        {
            if (roundedFill == null)
            {
                roundedFill = BuildRoundedSliced(64, 12, strokeOnly: false, strokePx: 0);
            }

            return roundedFill;
        }

        /// <summary>9-sliced rounded-rect outline for the frame's stroke (spec §4.3 2px stroke).</summary>
        public static Sprite RoundedStroke()
        {
            if (roundedStroke == null)
            {
                roundedStroke = BuildRoundedSliced(64, 12, strokeOnly: true, strokePx: 3);
            }

            return roundedStroke;
        }

        private static Sprite BuildRoundedSliced(int size, int corner, bool strokeOnly, int strokePx)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alpha = RoundedAlpha(x, y, size, corner);
                    if (strokeOnly && alpha > 0f)
                    {
                        bool interior = RoundedAlpha(x + strokePx, y, size, corner) >= 1f
                            && RoundedAlpha(x - strokePx, y, size, corner) >= 1f
                            && RoundedAlpha(x, y + strokePx, size, corner) >= 1f
                            && RoundedAlpha(x, y - strokePx, size, corner) >= 1f;
                        if (interior)
                        {
                            alpha = 0f;
                        }
                    }

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            int border = corner + strokePx + 2;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                PixelsPerUnit, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        public static Sprite Cell()
        {
            if (cell == null)
            {
                cell = BuildRounded(64, 10, glow: true);
            }

            return cell;
        }

        public static Sprite Creature()
        {
            if (creature == null)
            {
                creature = BuildCreature(64);
            }

            return creature;
        }

        public static Sprite Solid()
        {
            if (solid == null)
            {
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                Color[] pixels = new Color[16];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = Color.white;
                }

                tex.SetPixels(pixels);
                tex.Apply();
                solid = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            }

            return solid;
        }

        public static Sprite Dot()
        {
            if (dot == null)
            {
                dot = BuildCircleSprite(24);
            }

            return dot;
        }

        /// <summary>Tileable caustic noise for the water layers (§7.1).</summary>
        public static Sprite Caustic(int size = 256)
        {
            if (caustic == null)
            {
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear,
                };
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float n1 = Mathf.PerlinNoise(x * 0.045f, y * 0.045f);
                        float n2 = Mathf.PerlinNoise(x * 0.11f + 31.7f, y * 0.11f + 17.3f);
                        float v = Mathf.Clamp01(0.55f + 0.45f * (n1 * 0.65f + n2 * 0.35f));
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, v));
                    }
                }

                tex.Apply();
                caustic = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 1f), PixelsPerUnit);
            }

            return caustic;
        }

        private static Sprite BuildRounded(int size, int corner, bool glow)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alpha = RoundedAlpha(x, y, size, corner);
                    float brightness = 1f;
                    if (glow && alpha > 0f)
                    {
                        float dist = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                        brightness = 1f - 0.22f * Mathf.Clamp01(dist);
                        bool nearEdge = x <= 2 || y <= 2 || x >= size - 3 || y >= size - 3
                            || RoundedAlpha(x + 2, y, size, corner) <= 0f || RoundedAlpha(x - 2, y, size, corner) <= 0f
                            || RoundedAlpha(x, y + 2, size, corner) <= 0f || RoundedAlpha(x, y - 2, size, corner) <= 0f;
                        if (nearEdge)
                        {
                            brightness = 1.18f;
                        }
                    }

                    tex.SetPixel(x, y, new Color(brightness, brightness, brightness, alpha));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        }

        private static float RoundedAlpha(int x, int y, int size, int corner)
        {
            if (x < 0 || y < 0 || x >= size || y >= size)
            {
                return 0f;
            }

            int cx = Mathf.Clamp(x, corner, size - 1 - corner);
            int cy = Mathf.Clamp(y, corner, size - 1 - corner);
            float dx = x - cx;
            float dy = y - cy;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01(corner - dist + 0.5f) >= 0.5f ? 1f : Mathf.Clamp01(corner - dist + 0.5f) * 2f;
        }

        private static Sprite BuildCircleSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half));
                    float alpha = Mathf.Clamp01(half - dist + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 24f);
        }

        private static Sprite BuildCreature(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half));
                    float alpha = Mathf.Clamp01(half - 2f - dist + 0.5f);
                    float brightness = 1f - 0.15f * Mathf.Clamp01(dist / half);
                    tex.SetPixel(x, y, new Color(brightness, brightness, brightness, alpha));
                }
            }

            // Big friendly eyes (GDD 7.1), baked dark so any tint keeps them readable.
            DrawEye(tex, size, (int)(size * 0.36f), (int)(size * 0.60f));
            DrawEye(tex, size, (int)(size * 0.64f), (int)(size * 0.60f));
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        }

        private static void DrawEye(Texture2D tex, int size, int ex, int ey)
        {
            int radius = Mathf.Max(3, size / 10);
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (x * x + y * y <= radius * radius)
                    {
                        tex.SetPixel(ex + x, ey + y, new Color(0.06f, 0.08f, 0.12f, 1f));
                    }
                }
            }

            int glint = Mathf.Max(1, radius / 3);
            for (int y = 0; y < glint; y++)
            {
                for (int x = 0; x < glint; x++)
                {
                    tex.SetPixel(ex + radius / 3 + x, ey + radius / 3 + y, Color.white);
                }
            }
        }
    }
}
