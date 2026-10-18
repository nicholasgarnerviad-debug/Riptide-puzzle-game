using System.Collections.Generic;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// The Tidelings as cute chibi "models" (GDD §16), 100% code: each line is a list
    /// of layered parts (shapes that can inflate for the sticker outline), painted with
    /// top-lit shading, a gloss highlight and a shared face (big shiny eyes, rosy
    /// cheeks, a smile — or happy closed eyes for blinks). Three stages: Baby (the base
    /// model), Grown (the line's flourish), Grand (flourish + a crown and an aura).
    /// Sparkle swaps the palette and adds glints. Colors come from pet.{key}.* tokens.
    /// Lines are keyed by their pals.json key, so a new Tideling is a model + a palette;
    /// one without a model still draws (a round placeholder pal).
    /// </summary>
    internal static partial class CreatureArt
    {
        public const int Size = 256;
        private const float Outline = 9f;

        public enum Variant
        {
            Open,
            Closed,
            Silhouette,
        }

        /// <summary>The board species' line keys, in creatures.json order.</summary>
        public static readonly string[] BoardKeys =
            { "crab", "starfish", "seahorse", "octopus", "turtle", "puffer", "jelly", "axolotl" };

        // ------------------------------------------------------------- shapes

        private abstract class Shape
        {
            public abstract void Draw(float[,] m, float inflate);
        }

        private sealed class DiscS : Shape
        {
            private readonly float x, y, r;
            public DiscS(float x, float y, float r) { this.x = x; this.y = y; this.r = r; }
            public override void Draw(float[,] m, float inflate) => MenuSprites.Disc(m, x, y, r + inflate);
        }

        private sealed class OvalS : Shape
        {
            private readonly float x, y, rx, ry;
            public OvalS(float x, float y, float rx, float ry) { this.x = x; this.y = y; this.rx = rx; this.ry = ry; }
            public override void Draw(float[,] m, float inflate) => MenuSprites.Oval(m, x, y, rx + inflate, ry + inflate);
        }

        private sealed class SegS : Shape
        {
            private readonly float x1, y1, x2, y2, w;
            public SegS(float x1, float y1, float x2, float y2, float w) { this.x1 = x1; this.y1 = y1; this.x2 = x2; this.y2 = y2; this.w = w; }
            public override void Draw(float[,] m, float inflate) => MenuSprites.Seg(m, x1, y1, x2, y2, w + 2f * inflate);
        }

        private sealed class ArcS : Shape
        {
            private readonly float x, y, r, w, from, to;
            public ArcS(float x, float y, float r, float w, float from, float to) { this.x = x; this.y = y; this.r = r; this.w = w; this.from = from; this.to = to; }
            public override void Draw(float[,] m, float inflate) => PopSprites.Arc(m, x, y, r, w + 2f * inflate, from, to);
        }

        private sealed class PolyS : Shape
        {
            private readonly Vector2[] pts;
            public PolyS(params Vector2[] pts) { this.pts = pts; }

            public override void Draw(float[,] m, float inflate)
            {
                PopSprites.Poly(m, pts);
                if (inflate <= 0f)
                {
                    return;
                }

                for (int i = 0; i < pts.Length; i++)
                {
                    Vector2 a = pts[i];
                    Vector2 b = pts[(i + 1) % pts.Length];
                    MenuSprites.Seg(m, a.x, a.y, b.x, b.y, 2f * inflate);
                }
            }
        }

        private enum Role
        {
            Base,
            Light,
            Dark,
            Accent,
            Gold,
            Coral,
            White,
        }

        private sealed class Part
        {
            public readonly Shape[] Shapes;
            public readonly Role Role;
            public readonly float Alpha;
            public readonly bool Outlined;

            public Part(Role role, bool outlined, float alpha, params Shape[] shapes)
            {
                Role = role;
                Outlined = outlined;
                Alpha = alpha;
                Shapes = shapes;
            }
        }

        private sealed class Model
        {
            public readonly List<Part> Parts = new List<Part>();
            public Vector2 LeftEye;
            public Vector2 RightEye;
            public float EyeRadius;
            public Vector2 Gloss;
            public Vector2 GlossSize;

            /// <summary>Where a Grand Tideling's crown sits, and how big it is.</summary>
            public Vector2 HeadTop = new Vector2(128, 210);
            public float CrownScale = 1f;

            public Model Body(Role role, params Shape[] shapes)
            {
                Parts.Add(new Part(role, true, 1f, shapes));
                return this;
            }

            public Model Detail(Role role, float alpha, params Shape[] shapes)
            {
                Parts.Add(new Part(role, false, alpha, shapes));
                return this;
            }
        }

        private static void Face(Model m, float lx, float rx, float y, float r, float gx, float gy, float gw, float gh)
        {
            m.LeftEye = new Vector2(lx, y);
            m.RightEye = new Vector2(rx, y);
            m.EyeRadius = r;
            m.Gloss = new Vector2(gx, gy);
            m.GlossSize = new Vector2(gw, gh);
        }

        private static Vector2[] StarPoints(float cx, float cy, float outer, float inner, int points = 5)
        {
            var pts = new Vector2[points * 2];
            for (int i = 0; i < pts.Length; i++)
            {
                float ang = Mathf.PI / 2f + i * Mathf.PI / points;
                float r = i % 2 == 0 ? outer : inner;
                pts[i] = new Vector2(cx + Mathf.Cos(ang) * r, cy + Mathf.Sin(ang) * r);
            }

            return pts;
        }

        // ------------------------------------------------------------- models

        private static Model Build(string key, int stage)
        {
            var m = new Model();
            if (!BuildBoard(m, key, stage) && !BuildDeep(m, key, stage))
            {
                // A Tideling added in content before its art: a friendly round pal.
                m.Body(Role.Base, new OvalS(128, 118, 86, 78));
                m.Detail(Role.Light, 1f, new OvalS(128, 92, 50, 30));
                Face(m, 100, 156, 132, 24, 96, 170, 26, 12);
                m.HeadTop = new Vector2(128, 200);
            }

            if (stage >= 3)
            {
                AddCrown(m);
            }

            return m;
        }

        /// <summary>The Grand stage's crown: three points on a band, coral jewels.</summary>
        private static void AddCrown(Model m)
        {
            float s = m.CrownScale;
            Vector2 c = m.HeadTop;
            m.Body(Role.Gold,
                new PolyS(
                    new Vector2(c.x - 34 * s, c.y - 4 * s), new Vector2(c.x - 40 * s, c.y + 30 * s),
                    new Vector2(c.x - 18 * s, c.y + 14 * s), new Vector2(c.x, c.y + 38 * s),
                    new Vector2(c.x + 18 * s, c.y + 14 * s), new Vector2(c.x + 40 * s, c.y + 30 * s),
                    new Vector2(c.x + 34 * s, c.y - 4 * s)));
            m.Detail(Role.Coral, 1f, new DiscS(c.x, c.y + 6 * s, 6 * s), new DiscS(c.x - 22 * s, c.y + 4 * s, 4.5f * s),
                new DiscS(c.x + 22 * s, c.y + 4 * s, 4.5f * s));
            m.Detail(Role.White, 0.7f, new DiscS(c.x - 40 * s, c.y + 30 * s, 4 * s), new DiscS(c.x, c.y + 38 * s, 4 * s),
                new DiscS(c.x + 40 * s, c.y + 30 * s, 4 * s));
        }

        // ------------------------------------------------------------ painter

        /// <summary>
        /// Paints one form. With <paramref name="grow"/> the stage also sets the size —
        /// a Baby is drawn small, a Grand fills the frame (the Tideling screens); the
        /// board draws every creature full size.
        /// </summary>
        public static Texture2D Render(string key, int stage, Variant variant, bool sparkle, bool grow = false)
        {
            Color[] px = Paint(key, stage, variant, sparkle);
            float scale = !grow ? 1f : stage <= 1 ? 0.82f : stage == 2 ? 0.91f : 1f;
            return ToTexture(Shrink(px, scale));
        }

        private static Color[] Paint(string key, int stage, Variant variant, bool sparkle)
        {
            Model model = Build(key, Mathf.Clamp(stage, 1, 3));
            Color baseC = Token($"pet.{key}.base", Color.gray);
            Color dark = Token($"pet.{key}.dark", Color.black);
            Color light = Token($"pet.{key}.light", Color.white);
            if (sparkle)
            {
                Color glint = Token($"pet.{key}.sparkle", baseC);
                baseC = glint;
                dark = Color.Lerp(glint, Color.black, 0.5f);
                light = Color.Lerp(glint, Color.white, 0.55f);
            }

            Color accent = Token($"pet.{key}.accent", light);
            Color gold = Token("pet.crown", Color.yellow);
            Color coral = Token("pop.coral", Color.red);
            Color white = Token("pet.eye", Color.white);
            var px = new Color[Size * Size];

            // A Grand Tideling glows: a soft halo behind everything.
            if (stage >= 3 && variant != Variant.Silhouette)
            {
                SoftGlow(px, model.HeadTop.x, (model.HeadTop.y + 40f) * 0.5f, 120f, light, 0.55f);
            }

            // Sticker outline: every outlined part, inflated, in the line's dark.
            float[,] outline = new float[Size, Size];
            float[,] body = new float[Size, Size];
            foreach (Part part in model.Parts)
            {
                if (!part.Outlined)
                {
                    continue;
                }

                foreach (Shape shape in part.Shapes)
                {
                    shape.Draw(outline, Outline);
                    shape.Draw(body, 0f);
                }
            }

            if (variant == Variant.Silhouette)
            {
                Paint(px, outline, Color.white, Color.white, 1f);
                return px;
            }

            Paint(px, outline, dark, dark, 1f);
            foreach (Part part in model.Parts)
            {
                float[,] mask = new float[Size, Size];
                foreach (Shape shape in part.Shapes)
                {
                    shape.Draw(mask, 0f);
                }

                Color c = part.Role switch
                {
                    Role.Light => light,
                    Role.Dark => dark,
                    Role.Accent => accent,
                    Role.Gold => gold,
                    Role.Coral => coral,
                    Role.White => white,
                    _ => baseC,
                };

                // Top-lit form: a touch brighter up top, a touch deeper below.
                Color top = Color.Lerp(c, Color.white, 0.16f);
                Color bottom = Color.Lerp(c, part.Role == Role.Gold ? Token("pet.crownDark", dark) : dark, 0.22f);
                Paint(px, mask, top, bottom, part.Alpha);
            }

            // Gloss highlight, clipped to the body.
            float[,] gloss = new float[Size, Size];
            MenuSprites.Oval(gloss, model.Gloss.x, model.Gloss.y, model.GlossSize.x, model.GlossSize.y);
            Clip(gloss, body);
            Paint(px, gloss, Color.white, Color.white, 0.5f);

            DrawFace(px, model, variant == Variant.Closed);
            if (sparkle)
            {
                float[,] glints = new float[Size, Size];
                foreach (Vector2 at in new[] { new Vector2(34, 200), new Vector2(222, 176), new Vector2(206, 44) })
                {
                    PopSprites.Poly(glints, StarPoints(at.x, at.y, 15f, 4f, 4));
                }

                Paint(px, glints, white, white, 1f);
            }

            return px;
        }

        /// <summary>Scales the painted form toward the bottom-center (bilinear, premultiplied).</summary>
        private static Color[] Shrink(Color[] src, float scale)
        {
            if (scale >= 0.999f)
            {
                return src;
            }

            const float ax = Size * 0.5f;
            const float ay = 14f;
            var dst = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                float sy = ay + (y - ay) / scale;
                for (int x = 0; x < Size; x++)
                {
                    dst[y * Size + x] = Sample(src, ax + (x - ax) / scale, sy);
                }
            }

            return dst;
        }

        private static Color Sample(Color[] src, float fx, float fy)
        {
            int x0 = Mathf.FloorToInt(fx);
            int y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0;
            float ty = fy - y0;
            Color a = Premul(src, x0, y0), b = Premul(src, x0 + 1, y0);
            Color c = Premul(src, x0, y0 + 1), d = Premul(src, x0 + 1, y0 + 1);
            Color p = Color.Lerp(Color.Lerp(a, b, tx), Color.Lerp(c, d, tx), ty);
            return p.a <= 0.0001f ? Color.clear : new Color(p.r / p.a, p.g / p.a, p.b / p.a, p.a);
        }

        private static Color Premul(Color[] src, int x, int y)
        {
            if (x < 0 || y < 0 || x >= Size || y >= Size)
            {
                return Color.clear;
            }

            Color c = src[y * Size + x];
            return new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
        }

        private static Color Token(string key, Color fallback) =>
            ThemeRuntime.Theme.Colors.ContainsKey(key) ? ThemeRuntime.Color(key) : fallback;

        private static void DrawFace(Color[] px, Model m, bool closed)
        {
            Color eye = ThemeRuntime.Color("pet.eye");
            Color pupil = ThemeRuntime.Color("pet.pupil");
            Color cheek = ThemeRuntime.Color("pet.cheek");
            float r = m.EyeRadius;
            float midX = (m.LeftEye.x + m.RightEye.x) * 0.5f;

            // Rosy cheeks under and outside each eye.
            float[,] cheeks = new float[Size, Size];
            MenuSprites.Oval(cheeks, m.LeftEye.x - r * 0.55f, m.LeftEye.y - r * 1.15f, r * 0.62f, r * 0.38f);
            MenuSprites.Oval(cheeks, m.RightEye.x + r * 0.55f, m.RightEye.y - r * 1.15f, r * 0.62f, r * 0.38f);
            Paint(px, cheeks, cheek, cheek, cheek.a);

            foreach (Vector2 e in new[] { m.LeftEye, m.RightEye })
            {
                if (closed)
                {
                    // Happy closed eyes: a little upturned arc (^).
                    float[,] lid = new float[Size, Size];
                    PopSprites.Arc(lid, e.x, e.y - r * 0.35f, r * 0.62f, r * 0.26f, 25f, 155f);
                    Paint(px, lid, pupil, pupil, 1f);
                    continue;
                }

                float[,] white = new float[Size, Size];
                MenuSprites.Disc(white, e.x, e.y, r);
                Paint(px, white, eye, eye, 1f);
                float[,] dot = new float[Size, Size];
                MenuSprites.Disc(dot, e.x + r * 0.08f, e.y - r * 0.12f, r * 0.66f);
                Paint(px, dot, pupil, pupil, 1f);
                float[,] shine = new float[Size, Size];
                MenuSprites.Disc(shine, e.x + r * 0.28f, e.y + r * 0.22f, r * 0.27f);
                MenuSprites.Disc(shine, e.x - r * 0.18f, e.y - r * 0.32f, r * 0.12f);
                Paint(px, shine, eye, eye, 1f);
            }

            // Little smile.
            float[,] mouth = new float[Size, Size];
            PopSprites.Arc(mouth, midX, m.LeftEye.y - r * 0.55f, r * 0.42f, r * 0.2f, 205f, 335f);
            Paint(px, mouth, pupil, pupil, 1f);
        }

        /// <summary>A soft radial glow (the Grand aura), straight-alpha over.</summary>
        private static void SoftGlow(Color[] px, float cx, float cy, float radius, Color color, float strength)
        {
            float[,] mask = new float[Size, Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / radius;
                    mask[x, y] = d >= 1f ? 0f : Mathf.Pow(1f - d, 1.6f);
                }
            }

            Paint(px, mask, color, color, strength);
        }

        private static void Clip(float[,] mask, float[,] by)
        {
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    mask[x, y] = Mathf.Min(mask[x, y], by[x, y]);
                }
            }
        }

        /// <summary>Straight-alpha "over" composite of a coverage mask with a vertical gradient.</summary>
        private static void Paint(Color[] px, float[,] mask, Color top, Color bottom, float alpha)
        {
            for (int y = 0; y < Size; y++)
            {
                Color c = Color.Lerp(bottom, top, (float)y / (Size - 1));
                for (int x = 0; x < Size; x++)
                {
                    float a = Mathf.Clamp01(mask[x, y]) * alpha;
                    if (a <= 0f)
                    {
                        continue;
                    }

                    int i = y * Size + x;
                    Color d = px[i];
                    float outA = a + d.a * (1f - a);
                    if (outA <= 0f)
                    {
                        continue;
                    }

                    px[i] = new Color(
                        (c.r * a + d.r * d.a * (1f - a)) / outA,
                        (c.g * a + d.g * d.a * (1f - a)) / outA,
                        (c.b * a + d.b * d.a * (1f - a)) / outA,
                        outA);
                }
            }
        }

        private static Texture2D ToTexture(Color[] px)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }
    }
}
