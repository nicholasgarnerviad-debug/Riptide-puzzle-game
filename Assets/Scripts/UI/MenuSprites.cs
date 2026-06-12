using System.Collections.Generic;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// Visual-quality pass (Nick: "build something you could genuinely call a
    /// mobile game"): depth, light and iconography — still 100% code-generated
    /// (DECISIONS: no hand-authored assets). Grayscale luminance is baked into
    /// textures; HUE always arrives by tint from theme tokens, preserving the
    /// single-source-of-truth color rule.
    /// </summary>
    public static class MenuSprites
    {
        public const float PixelsPerUnit = 64f;

        private static Sprite? softGlow;
        private static Sprite? capsuleGradient;
        private static Sprite? panelGradient;
        private static Sprite? lightRay;
        private static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();

        /// <summary>Smooth radial falloff — glows, drop shadows, bokeh snow.</summary>
        public static Sprite SoftGlow()
        {
            if (softGlow == null)
            {
                const int size = 128;
                Texture2D tex = NewTex(size, size);
                float half = (size - 1) * 0.5f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dist = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                        float a = Mathf.Pow(Mathf.Clamp01(1f - dist), 2.4f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }

                tex.Apply();
                softGlow = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            }

            return softGlow;
        }

        /// <summary>
        /// 9-sliced rounded rect with a baked vertical luminance gradient and top
        /// highlight — tinted with an accent it reads as a lit, modern CTA.
        /// </summary>
        public static Sprite CapsuleGradient()
        {
            if (capsuleGradient == null)
            {
                capsuleGradient = BuildGradientSliced(64, 22, bottomLum: 0.62f, topLum: 1f, highlight: true);
            }

            return capsuleGradient;
        }

        /// <summary>9-sliced rounded panel with a subtle top-lit gradient for cards.</summary>
        public static Sprite PanelGradient()
        {
            if (panelGradient == null)
            {
                panelGradient = BuildGradientSliced(64, 14, bottomLum: 0.78f, topLum: 1.06f, highlight: false);
            }

            return panelGradient;
        }

        /// <summary>Vertical god-ray streak: bright at top, soft sides, fades out.</summary>
        public static Sprite LightRay()
        {
            if (lightRay == null)
            {
                const int w = 64;
                const int h = 256;
                Texture2D tex = NewTex(w, h);
                float cx = (w - 1) * 0.5f;
                for (int y = 0; y < h; y++)
                {
                    float v = Mathf.Pow((float)y / (h - 1), 1.6f); // top = 1
                    for (int x = 0; x < w; x++)
                    {
                        float s = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(x - cx) / cx), 1.8f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, v * s));
                    }
                }

                tex.Apply();
                lightRay = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 1f), PixelsPerUnit);
            }

            return lightRay;
        }

        private static Sprite? dunes;

        /// <summary>
        /// Horizontally tileable seabed silhouette: filled below a layered sine
        /// horizon. Tint dark for far dunes, lighter for near (Tidepool diorama).
        /// </summary>
        public static Sprite Dunes()
        {
            if (dunes == null)
            {
                const int w = 512;
                const int h = 128;
                Texture2D tex = NewTex(w, h);
                tex.wrapMode = TextureWrapMode.Repeat;
                for (int x = 0; x < w; x++)
                {
                    float horizon = 46f
                        + 22f * Mathf.Sin(2f * Mathf.PI * x / w * 3f + 1.3f)
                        + 11f * Mathf.Sin(2f * Mathf.PI * x / w * 7f);
                    for (int y = 0; y < h; y++)
                    {
                        float a = Mathf.Clamp01(horizon - y + 0.5f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }

                tex.Apply();
                dunes = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), PixelsPerUnit);
            }

            return dunes;
        }

        /// <summary>Geometric white icon marks, tinted by theme tokens at use site.
        /// Ids: compass · sun · waves · fish · bag · gear · chest · pause · noAds
        /// · coins1-3 · kelp · rocks.</summary>
        public static Sprite Icon(string id)
        {
            if (!icons.TryGetValue(id, out Sprite sprite))
            {
                sprite = BuildIcon(id);
                icons[id] = sprite;
            }

            return sprite;
        }

        // ------------------------- builders -------------------------

        private static Sprite BuildGradientSliced(int size, int corner, float bottomLum, float topLum, bool highlight)
        {
            Texture2D tex = NewTex(size, size);
            for (int y = 0; y < size; y++)
            {
                float t = (float)y / (size - 1);
                float lum = Mathf.Lerp(bottomLum, topLum, t);
                if (highlight && t > 0.82f)
                {
                    lum += 0.10f * ((t - 0.82f) / 0.18f);
                }

                lum = Mathf.Clamp01(lum);
                for (int x = 0; x < size; x++)
                {
                    float alpha = RoundedAlpha(x, y, size, corner);
                    tex.SetPixel(x, y, new Color(lum, lum, lum, alpha));
                }
            }

            tex.Apply();
            int border = corner + 2;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                PixelsPerUnit, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        private static Sprite BuildIcon(string id)
        {
            const int s = 128;
            float[,] a = new float[s, s];
            const float c = 63.5f;
            switch (id)
            {
                case "compass":
                    Ring(a, c, c, 46f, 6f);
                    Seg(a, c, c + 34f, c - 9f, c, 6f);
                    Seg(a, c, c + 34f, c + 9f, c, 6f);
                    Seg(a, c - 9f, c, c, c - 34f, 6f);
                    Seg(a, c + 9f, c, c, c - 34f, 6f);
                    Disc(a, c, c, 6f);
                    break;
                case "sun":
                    Disc(a, c, c, 20f);
                    for (int i = 0; i < 8; i++)
                    {
                        float ang = i * Mathf.PI / 4f;
                        Seg(a, c + Mathf.Cos(ang) * 30f, c + Mathf.Sin(ang) * 30f,
                            c + Mathf.Cos(ang) * 46f, c + Mathf.Sin(ang) * 46f, 5.5f);
                    }

                    break;
                case "waves":
                    Wave(a, 34f, 5.5f);
                    Wave(a, 63f, 5.5f);
                    Wave(a, 92f, 5.5f);
                    break;
                case "fish":
                    Oval(a, c + 6f, c, 34f, 22f);
                    Seg(a, c - 26f, c, c - 44f, c + 16f, 7f);
                    Seg(a, c - 26f, c, c - 44f, c - 16f, 7f);
                    Cut(a, c + 20f, c + 6f, 4.5f);
                    break;
                case "bag":
                    RoundedBlock(a, 24f, 18f, 104f, 78f, 14f);
                    HandleArc(a, c, 84f, 20f, 6f, 78f);
                    break;
                case "gear":
                    Ring(a, c, c, 32f, 13f);
                    for (int i = 0; i < 8; i++)
                    {
                        float ang = i * Mathf.PI / 4f + Mathf.PI / 8f;
                        Seg(a, c + Mathf.Cos(ang) * 38f, c + Mathf.Sin(ang) * 38f,
                            c + Mathf.Cos(ang) * 50f, c + Mathf.Sin(ang) * 50f, 8f);
                    }

                    break;
                case "chest":
                    RoundedBlock(a, 22f, 26f, 106f, 92f, 12f);
                    CutLine(a, 70f, 22f, 106f, 3f);
                    Cut(a, c, 52f, 7f);
                    break;
                case "pause":
                    RoundedBlock(a, 42f, 34f, 58f, 94f, 7f);
                    RoundedBlock(a, 70f, 34f, 86f, 94f, 7f);
                    break;
                case "noAds":
                    Ring(a, c, c, 46f, 7f);
                    Seg(a, c - 30f, c + 30f, c + 30f, c - 30f, 7f);
                    break;
                case "coins1":
                    Coin(a, c, c, 30f);
                    break;
                case "coins2":
                    Coin(a, c - 16f, c + 14f, 26f);
                    Coin(a, c + 16f, c - 14f, 26f);
                    break;
                case "coins3":
                    Coin(a, c - 22f, c - 16f, 24f);
                    Coin(a, c + 22f, c - 16f, 24f);
                    Coin(a, c, c + 20f, 24f);
                    break;
                case "kelp":
                    Strand(a, c - 28f, 86f);
                    Strand(a, c, 118f);
                    Strand(a, c + 28f, 64f);
                    break;
                case "rocks":
                    Disc(a, c - 24f, 30f, 28f);
                    Disc(a, c + 22f, 26f, 22f);
                    Disc(a, c - 2f, 44f, 17f);
                    break;
                default:
                    Disc(a, c, c, 40f);
                    break;
            }

            Texture2D tex = NewTex(s, s);
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a[x, y])));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        }

        // ------------------------- rasterizers -------------------------

        private static Texture2D NewTex(int w, int h) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };

        private static float RoundedAlpha(int x, int y, int size, int corner)
        {
            float qx = Mathf.Max(Mathf.Max(corner - x, x - (size - 1 - corner)), 0f);
            float qy = Mathf.Max(Mathf.Max(corner - y, y - (size - 1 - corner)), 0f);
            float d = Mathf.Sqrt(qx * qx + qy * qy);
            return Mathf.Clamp01(corner - d + 0.5f) * (corner > 0 ? 1f : 1f);
        }

        private static void Blend(float[,] a, int x, int y, float v)
        {
            if (x >= 0 && y >= 0 && x < a.GetLength(0) && y < a.GetLength(1))
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
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    Blend(a, x, y, Mathf.Clamp01(r - d + 0.5f));
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
                    float d = Mathf.Sqrt(nx * nx + ny * ny);
                    Blend(a, x, y, Mathf.Clamp01((1f - d) * Mathf.Min(rx, ry) + 0.5f));
                }
            }
        }

        private static void Ring(float[,] a, float cx, float cy, float r, float w)
        {
            for (int y = (int)(cy - r - w - 2); y <= cy + r + w + 2; y++)
            {
                for (int x = (int)(cx - r - w - 2); x <= cx + r + w + 2; x++)
                {
                    float d = Mathf.Abs(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r);
                    Blend(a, x, y, Mathf.Clamp01(w * 0.5f - d + 0.5f));
                }
            }
        }

        /// <summary>Upper arc of a ring only — the bag handle.</summary>
        private static void HandleArc(float[,] a, float cx, float cy, float r, float w, float minY)
        {
            for (int y = (int)minY; y <= cy + r + w + 2; y++)
            {
                for (int x = (int)(cx - r - w - 2); x <= cx + r + w + 2; x++)
                {
                    float d = Mathf.Abs(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r);
                    Blend(a, x, y, Mathf.Clamp01(w * 0.5f - d + 0.5f));
                }
            }
        }

        private static void Seg(float[,] a, float x1, float y1, float x2, float y2, float w)
        {
            var p1 = new Vector2(x1, y1);
            var d = new Vector2(x2 - x1, y2 - y1);
            float len2 = Mathf.Max(0.001f, d.sqrMagnitude);
            for (int y = (int)(Mathf.Min(y1, y2) - w - 2f); y <= Mathf.Max(y1, y2) + w + 2f; y++)
            {
                for (int x = (int)(Mathf.Min(x1, x2) - w - 2f); x <= Mathf.Max(x1, x2) + w + 2f; x++)
                {
                    var p = new Vector2(x, y);
                    float t = Mathf.Clamp01(Vector2.Dot(p - p1, d) / len2);
                    float dist = Vector2.Distance(p, p1 + d * t);
                    Blend(a, x, y, Mathf.Clamp01(w * 0.5f - dist + 0.5f));
                }
            }
        }

        /// <summary>A wavy kelp strand rising from the icon's base.</summary>
        private static void Strand(float[,] a, float baseX, float height)
        {
            float prevX = baseX;
            float prevY = 6f;
            const int steps = 14;
            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                float y = 6f + t * height;
                float x = baseX + Mathf.Sin(t * Mathf.PI * 2.2f) * 9f;
                Seg(a, prevX, prevY, x, y, Mathf.Lerp(6.5f, 3.5f, t));
                prevX = x;
                prevY = y;
            }
        }

        /// <summary>A coin: solid disc with a slot-line glint cut across it.</summary>
        private static void Coin(float[,] a, float cx, float cy, float r)
        {
            Disc(a, cx, cy, r);
            CutLine(a, cy, cx - r * 0.55f, cx + r * 0.55f, 2.4f);
        }

        private static void Wave(float[,] a, float cy, float w)
        {
            float prevX = 16f;
            float prevY = cy;
            for (int i = 1; i <= 24; i++)
            {
                float x = 16f + i * (96f / 24f);
                float y = cy + Mathf.Sin(i / 24f * Mathf.PI * 2f) * 11f;
                Seg(a, prevX, prevY, x, y, w);
                prevX = x;
                prevY = y;
            }
        }

        private static void RoundedBlock(float[,] a, float x1, float y1, float x2, float y2, float r)
        {
            for (int y = (int)y1 - 2; y <= y2 + 2; y++)
            {
                for (int x = (int)x1 - 2; x <= x2 + 2; x++)
                {
                    float qx = Mathf.Max(Mathf.Max(x1 + r - x, x - (x2 - r)), 0f);
                    float qy = Mathf.Max(Mathf.Max(y1 + r - y, y - (y2 - r)), 0f);
                    float d = Mathf.Sqrt(qx * qx + qy * qy);
                    Blend(a, x, y, Mathf.Clamp01(r - d + 0.5f));
                }
            }
        }

        private static void Cut(float[,] a, float cx, float cy, float r)
        {
            for (int y = (int)(cy - r - 2); y <= cy + r + 2; y++)
            {
                for (int x = (int)(cx - r - 2); x <= cx + r + 2; x++)
                {
                    if (x >= 0 && y >= 0 && x < a.GetLength(0) && y < a.GetLength(1))
                    {
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        a[x, y] = Mathf.Min(a[x, y], Mathf.Clamp01(d - r + 0.5f));
                    }
                }
            }
        }

        private static void CutLine(float[,] a, float cy, float x1, float x2, float halfH)
        {
            for (int y = (int)(cy - halfH - 1); y <= cy + halfH + 1; y++)
            {
                for (int x = (int)x1; x <= x2; x++)
                {
                    if (x >= 0 && y >= 0 && x < a.GetLength(0) && y < a.GetLength(1))
                    {
                        float d = Mathf.Abs(y - cy);
                        a[x, y] = Mathf.Min(a[x, y], Mathf.Clamp01(d - halfH + 0.5f));
                    }
                }
            }
        }
    }
}
