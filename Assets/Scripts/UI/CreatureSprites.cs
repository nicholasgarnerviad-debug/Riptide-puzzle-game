using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// Distinct procedural silhouettes for the 8 rescue species (GDD §2.5 / §7.1:
    /// "flat vector style, big eyes" — the one real art demand in the game). Still
    /// 100% code-generated (DECISIONS: no hand-authored assets): each is a 64px
    /// grayscale shape with a soft top-light gradient and baked dark eyes, tinted
    /// per-species by Palette.CreatureColor at the use site so the bioluminescent
    /// hue still flows from the theme. Replaces the single eyed-circle placeholder.
    ///
    /// Id order matches creatures.json: 0 Crab · 1 Starfish · 2 Seahorse ·
    /// 3 Octopus · 4 Turtle · 5 Pufferfish · 6 Jellyfish · 7 Axolotl (rare).
    /// </summary>
    public static class CreatureSprites
    {
        public const int Size = 64;
        public const float PixelsPerUnit = 64f;
        private const float C = (Size - 1) * 0.5f;

        private static readonly Sprite?[] cache = new Sprite?[8];

        public static Sprite For(int speciesId)
        {
            int id = ((speciesId % 8) + 8) % 8;
            if (cache[id] == null)
            {
                cache[id] = Build(id);
            }

            return cache[id]!;
        }

        private static Sprite Build(int id)
        {
            float[,] a = new float[Size, Size];
            switch (id)
            {
                case 0: Crab(a); break;
                case 1: Starfish(a); break;
                case 2: Seahorse(a); break;
                case 3: Octopus(a); break;
                case 4: Turtle(a); break;
                case 5: Pufferfish(a); break;
                case 6: Jellyfish(a); break;
                default: Axolotl(a); break;
            }

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int y = 0; y < Size; y++)
            {
                // Soft top-light: brighter near the top, so the flat shape gets form.
                float lum = Mathf.Lerp(0.72f, 1f, (float)y / (Size - 1));
                for (int x = 0; x < Size; x++)
                {
                    float shape = Mathf.Clamp01(a[x, y]);
                    float l = a[x, y] < 0f ? 0.10f : lum; // negative = baked-dark eye
                    tex.SetPixel(x, y, new Color(l, l, l, Mathf.Clamp01(Mathf.Abs(shape))));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        }

        // ------------------------- species -------------------------

        private static void Crab(float[,] a)
        {
            Oval(a, C, C - 4f, 22f, 14f);              // wide flat carapace
            Disc(a, C - 24f, C + 12f, 9f);             // left claw
            Disc(a, C + 24f, C + 12f, 9f);             // right claw
            Seg(a, C - 20f, C - 6f, C - 26f, C + 6f, 4f);
            Seg(a, C + 20f, C - 6f, C + 26f, C + 6f, 4f);
            for (int i = 0; i < 3; i++)                // legs each side
            {
                float ly = C - 8f + i * 8f;
                Seg(a, C - 16f, ly, C - 30f, ly + 4f, 3f);
                Seg(a, C + 16f, ly, C + 30f, ly + 4f, 3f);
            }

            Eye(a, C - 7f, C + 2f);
            Eye(a, C + 7f, C + 2f);
        }

        private static void Starfish(float[,] a)
        {
            Disc(a, C, C, 12f);
            for (int i = 0; i < 5; i++)
            {
                float ang = Mathf.PI / 2f + i * Mathf.PI * 2f / 5f;
                float tx = C + Mathf.Cos(ang) * 28f;
                float ty = C + Mathf.Sin(ang) * 28f;
                Tri(a, C + Mathf.Cos(ang + 0.5f) * 12f, C + Mathf.Sin(ang + 0.5f) * 12f,
                    C + Mathf.Cos(ang - 0.5f) * 12f, C + Mathf.Sin(ang - 0.5f) * 12f, tx, ty);
            }

            Eye(a, C - 6f, C + 1f);
            Eye(a, C + 6f, C + 1f);
        }

        private static void Seahorse(float[,] a)
        {
            // Curved S spine.
            float px = C - 2f, py = C - 26f;
            for (int i = 1; i <= 20; i++)
            {
                float t = i / 20f;
                float x = C - 2f + Mathf.Sin(t * Mathf.PI * 1.6f) * 12f;
                float y = C - 26f + t * 46f;
                Seg(a, px, py, x, y, Mathf.Lerp(9f, 4f, t));
                px = x;
                py = y;
            }

            Disc(a, C, C - 22f, 10f);                 // head
            Tri(a, C + 6f, C - 26f, C + 6f, C - 18f, C + 18f, C - 24f); // snout
            Seg(a, C - 2f, C - 12f, C - 8f, C - 6f, 3f); // dorsal fin hint
            Eye(a, C + 3f, C - 22f);
        }

        private static void Octopus(float[,] a)
        {
            Disc(a, C, C - 6f, 20f);                  // mantle
            Oval(a, C, C - 14f, 20f, 12f);            // head widen
            for (int i = 0; i < 5; i++)               // tentacles
            {
                float ox = C - 18f + i * 9f;
                float px = ox, py = C + 8f;
                for (int s = 1; s <= 6; s++)
                {
                    float t = s / 6f;
                    float x = ox + Mathf.Sin(t * Mathf.PI * 2f + i) * 4f;
                    float y = C + 8f + t * 20f;
                    Seg(a, px, py, x, y, Mathf.Lerp(5f, 2f, t));
                    px = x;
                    py = y;
                }
            }

            Eye(a, C - 8f, C - 8f);
            Eye(a, C + 8f, C - 8f);
        }

        private static void Turtle(float[,] a)
        {
            Disc(a, C, C - 2f, 20f);                  // shell
            Disc(a, C, C + 16f, 9f);                  // head (top)
            Oval(a, C - 20f, C + 2f, 8f, 5f);         // flippers
            Oval(a, C + 20f, C + 2f, 8f, 5f);
            Oval(a, C - 14f, C - 18f, 7f, 5f);
            Oval(a, C + 14f, C - 18f, 7f, 5f);
            // Shell plates carved slightly darker.
            Ring(a, C, C - 2f, 11f, 2f, 0.55f);
            Eye(a, C - 4f, C + 18f);
            Eye(a, C + 4f, C + 18f);
        }

        private static void Pufferfish(float[,] a)
        {
            for (int i = 0; i < 12; i++)              // spikes
            {
                float ang = i * Mathf.PI * 2f / 12f;
                Tri(a, C + Mathf.Cos(ang + 0.18f) * 16f, C + Mathf.Sin(ang + 0.18f) * 16f,
                    C + Mathf.Cos(ang - 0.18f) * 16f, C + Mathf.Sin(ang - 0.18f) * 16f,
                    C + Mathf.Cos(ang) * 28f, C + Mathf.Sin(ang) * 28f);
            }

            Disc(a, C, C, 18f);                       // round body
            Eye(a, C - 7f, C + 3f);
            Eye(a, C + 7f, C + 3f);
            Arc(a, C, C - 6f, 6f);                    // little frown-mouth
        }

        private static void Jellyfish(float[,] a)
        {
            Dome(a, C, C + 4f, 20f);                  // bell
            for (int i = 0; i < 5; i++)               // wavy tentacles
            {
                float ox = C - 14f + i * 7f;
                float px = ox, py = C + 4f;
                for (int s = 1; s <= 7; s++)
                {
                    float t = s / 7f;
                    float x = ox + Mathf.Sin(t * Mathf.PI * 3f + i) * 5f;
                    float y = C + 4f + t * 26f;
                    Seg(a, px, py, x, y, Mathf.Lerp(4f, 1.5f, t));
                    px = x;
                    py = y;
                }
            }

            Eye(a, C - 7f, C + 8f);
            Eye(a, C + 7f, C + 8f);
        }

        private static void Axolotl(float[,] a)
        {
            Oval(a, C, C - 2f, 16f, 11f);             // body
            Disc(a, C, C + 12f, 12f);                 // big head (top)
            for (int i = 0; i < 3; i++)               // external gill frills
            {
                float fy = C + 14f + i * 2f;
                Seg(a, C - 9f, fy, C - 22f, fy + 6f - i * 3f, 3.2f);
                Seg(a, C + 9f, fy, C + 22f, fy + 6f - i * 3f, 3.2f);
            }

            Oval(a, C - 14f, C - 8f, 6f, 4f);         // legs
            Oval(a, C + 14f, C - 8f, 6f, 4f);
            Eye(a, C - 5f, C + 13f);
            Eye(a, C + 5f, C + 13f);
            Arc(a, C, C + 7f, 5f);                    // signature smile
        }

        // ------------------------- rasterizer -------------------------

        private static void Blend(float[,] a, int x, int y, float v)
        {
            if (x >= 0 && y >= 0 && x < Size && y < Size && v > 0f && a[x, y] >= 0f)
            {
                a[x, y] = Mathf.Max(a[x, y], v);
            }
        }

        private static void Disc(float[,] a, float cx, float cy, float r)
        {
            for (int y = (int)(cy - r - 2); y <= cy + r + 2; y++)
            {
                for (int x = (int)(cx - r - 2); x <= cx + r + 2; x++)
                {
                    Blend(a, x, y, Mathf.Clamp01(r - Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) + 0.5f));
                }
            }
        }

        private static void Dome(float[,] a, float cx, float cy, float r)
        {
            for (int y = (int)(cy - r - 2); y <= cy + r + 2; y++)
            {
                for (int x = (int)(cx - r - 2); x <= cx + r + 2; x++)
                {
                    if (y >= cy)
                    {
                        Blend(a, x, y, Mathf.Clamp01(r - Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) + 0.5f));
                    }
                }
            }
        }

        private static void Oval(float[,] a, float cx, float cy, float rx, float ry)
        {
            for (int y = (int)(cy - ry - 2); y <= cy + ry + 2; y++)
            {
                for (int x = (int)(cx - rx - 2); x <= cx + rx + 2; x++)
                {
                    float nx = (x - cx) / rx;
                    float ny = (y - cy) / ry;
                    Blend(a, x, y, Mathf.Clamp01((1f - Mathf.Sqrt(nx * nx + ny * ny)) * Mathf.Min(rx, ry) + 0.5f));
                }
            }
        }

        private static void Ring(float[,] a, float cx, float cy, float r, float w, float strength)
        {
            for (int y = (int)(cy - r - w - 2); y <= cy + r + w + 2; y++)
            {
                for (int x = (int)(cx - r - w - 2); x <= cx + r + w + 2; x++)
                {
                    float d = Mathf.Abs(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r);
                    float v = Mathf.Clamp01(w * 0.5f - d + 0.5f) * strength;
                    if (x >= 0 && y >= 0 && x < Size && y < Size && v > 0f && a[x, y] > 0f)
                    {
                        a[x, y] = Mathf.Min(a[x, y], 1f - v); // darken plates
                    }
                }
            }
        }

        private static void Seg(float[,] a, float x1, float y1, float x2, float y2, float w)
        {
            var p1 = new Vector2(x1, y1);
            var d = new Vector2(x2 - x1, y2 - y1);
            float len2 = Mathf.Max(0.001f, d.sqrMagnitude);
            for (int y = (int)(Mathf.Min(y1, y2) - w - 2); y <= Mathf.Max(y1, y2) + w + 2; y++)
            {
                for (int x = (int)(Mathf.Min(x1, x2) - w - 2); x <= Mathf.Max(x1, x2) + w + 2; x++)
                {
                    var p = new Vector2(x, y);
                    float t = Mathf.Clamp01(Vector2.Dot(p - p1, d) / len2);
                    Blend(a, x, y, Mathf.Clamp01(w * 0.5f - Vector2.Distance(p, p1 + d * t) + 0.5f));
                }
            }
        }

        private static void Tri(float[,] a, float ax, float ay, float bx, float by, float cx, float cy)
        {
            int minX = (int)Mathf.Min(ax, Mathf.Min(bx, cx)) - 1;
            int maxX = (int)Mathf.Max(ax, Mathf.Max(bx, cx)) + 1;
            int minY = (int)Mathf.Min(ay, Mathf.Min(by, cy)) - 1;
            int maxY = (int)Mathf.Max(ay, Mathf.Max(by, cy)) + 1;
            float area = Edge(ax, ay, bx, by, cx, cy);
            if (Mathf.Abs(area) < 0.001f)
            {
                return;
            }

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float w0 = Edge(bx, by, cx, cy, x, y) / area;
                    float w1 = Edge(cx, cy, ax, ay, x, y) / area;
                    float w2 = Edge(ax, ay, bx, by, x, y) / area;
                    if (w0 >= -0.02f && w1 >= -0.02f && w2 >= -0.02f)
                    {
                        Blend(a, x, y, 1f);
                    }
                }
            }
        }

        private static float Edge(float ax, float ay, float bx, float by, float px, float py)
            => (px - ax) * (by - ay) - (py - ay) * (bx - ax);

        private static void Eye(float[,] a, float cx, float cy)
        {
            const float r = 3.6f;
            for (int y = (int)(cy - r - 1); y <= cy + r + 1; y++)
            {
                for (int x = (int)(cx - r - 1); x <= cx + r + 1; x++)
                {
                    if (x >= 0 && y >= 0 && x < Size && y < Size
                        && (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r)
                    {
                        a[x, y] = -1f; // baked dark, survives any tint
                    }
                }
            }

            // glint
            int gx = (int)(cx + 1f);
            int gy = (int)(cy + 1f);
            if (gx >= 0 && gy >= 0 && gx < Size && gy < Size)
            {
                a[gx, gy] = 1f;
            }
        }

        private static void Arc(float[,] a, float cx, float cy, float r)
        {
            for (float t = 0.15f; t <= 0.85f; t += 0.04f)
            {
                float ang = Mathf.PI * t;
                int x = (int)(cx - Mathf.Cos(ang) * r);
                int y = (int)(cy - Mathf.Sin(ang) * r * 0.7f);
                if (x >= 0 && y >= 0 && x < Size && y < Size)
                {
                    a[x, y] = -1f;
                }
            }
        }
    }
}
