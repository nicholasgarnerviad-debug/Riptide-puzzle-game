using System.Collections.Generic;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// The 8 rescue species as cute chibi "models" (Nick, 2026-10-18: "make the
    /// pets cuter … give them some model like the crab"). Still 100% code: each
    /// species is a list of layered parts (shapes that can inflate for the sticker
    /// outline), painted with top-lit shading, a gloss highlight and a shared face
    /// (big shiny eyes, rosy cheeks, a smile — or happy closed eyes for blinks).
    /// Colors come from the pet.* theme tokens.
    /// </summary>
    internal static class CreatureArt
    {
        public const int Size = 256;
        private const float Outline = 9f;

        public enum Variant
        {
            Open,
            Closed,
            Silhouette,
        }

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

        // ------------------------------------------------------------ species

        private static Model Species(int id)
        {
            var m = new Model();
            switch (id)
            {
                case 0: // Crab — the template: wide body, waving claws, little legs.
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float sx = side;
                        m.Body(Role.Base,
                            new SegS(128 + sx * 62, 96, 128 + sx * 98, 76, 13),
                            new SegS(128 + sx * 58, 80, 128 + sx * 90, 52, 13),
                            new SegS(128 + sx * 50, 68, 128 + sx * 72, 36, 13));
                        m.Body(Role.Base,
                            new SegS(128 + sx * 66, 128, 128 + sx * 86, 160, 18),
                            new DiscS(128 + sx * 88, 172, 30));
                        m.Detail(Role.Dark, 1f, new OvalS(128 + sx * 88, 196, 7, 15));
                    }

                    m.Body(Role.Base, new OvalS(128, 112, 82, 60));
                    m.Detail(Role.Light, 1f, new OvalS(128, 88, 52, 24));
                    Face(m, 100, 156, 124, 25, 92, 148, 24, 12);
                    break;

                case 1: // Starfish — plump rounded star, freckles.
                {
                    Vector2[] pts = StarPoints(128, 116, 110, 54);
                    var shapes = new List<Shape> { new PolyS(pts) };
                    for (int i = 0; i < 10; i += 2)
                    {
                        Vector2 tip = Vector2.Lerp(new Vector2(128, 116), pts[i], 0.86f);
                        shapes.Add(new DiscS(tip.x, tip.y, 20));
                    }

                    m.Body(Role.Base, shapes.ToArray());
                    for (int i = 0; i < 10; i += 2)
                    {
                        Vector2 f = Vector2.Lerp(new Vector2(128, 116), pts[i], 0.66f);
                        m.Detail(Role.Light, 1f, new DiscS(f.x, f.y, 7));
                    }

                    Face(m, 106, 150, 124, 20, 104, 150, 18, 9);
                    break;
                }

                case 2: // Seahorse — big round head, crest, snout, curly tail.
                    m.Body(Role.Light, new DiscS(98, 214, 12), new DiscS(124, 226, 13), new DiscS(152, 222, 12));
                    m.Body(Role.Base, new ArcS(104, 50, 26, 20, -40, 250), new DiscS(130, 46, 10));
                    m.Body(Role.Light, new OvalS(70, 112, 18, 28));
                    m.Body(Role.Base, new OvalS(122, 104, 46, 50));
                    m.Body(Role.Base, new SegS(180, 158, 220, 150, 22), new OvalS(224, 150, 11, 15));
                    m.Body(Role.Base, new OvalS(132, 170, 62, 54));
                    m.Detail(Role.Light, 1f, new OvalS(134, 100, 22, 38));
                    m.Detail(Role.Dark, 0.3f, new SegS(118, 84, 150, 84, 4), new SegS(116, 100, 152, 100, 4),
                        new SegS(118, 116, 150, 116, 4));
                    Face(m, 112, 154, 174, 21, 108, 196, 22, 11);
                    break;

                case 3: // Octopus — dome head, four curly legs, spots.
                    for (int i = 0; i < 4; i++)
                    {
                        float x0 = 70 + i * 39;
                        float lean = (i - 1.5f) * 8f;
                        m.Body(Role.Base,
                            new SegS(x0, 104, x0 + lean * 0.5f - 4, 72, 28),
                            new SegS(x0 + lean * 0.5f - 4, 72, x0 + lean + 4, 46, 24),
                            new SegS(x0 + lean + 4, 46, x0 + lean + 18, 34, 18),
                            new DiscS(x0 + lean + 20, 38, 10));
                        m.Detail(Role.Light, 1f, new DiscS(x0 + lean * 0.4f - 2, 70, 5), new DiscS(x0 + lean + 4, 50, 4));
                    }

                    m.Body(Role.Base, new OvalS(128, 150, 78, 72));
                    m.Detail(Role.Light, 1f, new DiscS(92, 196, 10), new DiscS(162, 204, 8), new DiscS(176, 178, 6));
                    Face(m, 100, 156, 146, 25, 94, 186, 24, 12);
                    break;

                case 4: // Turtle — head on top of a big plated shell, flippers.
                    for (int side = -1; side <= 1; side += 2)
                    {
                        m.Body(Role.Light, new SegS(128 + side * 66, 112, 128 + side * 104, 92, 30),
                            new SegS(128 + side * 50, 66, 128 + side * 68, 44, 24));
                    }

                    m.Body(Role.Light, new DiscS(128, 186, 48));
                    m.Body(Role.Base, new OvalS(128, 104, 88, 60));
                    m.Detail(Role.Dark, 0.55f, new OvalS(128, 112, 28, 20), new OvalS(78, 100, 18, 16),
                        new OvalS(178, 100, 18, 16), new OvalS(104, 74, 16, 12), new OvalS(152, 74, 16, 12));
                    Face(m, 108, 148, 194, 18, 82, 140, 26, 10);
                    break;

                case 5: // Pufferfish — round, spiky, tiny fins and tail.
                {
                    var spikes = new List<Shape>();
                    for (int i = 0; i < 14; i++)
                    {
                        float ang = i * Mathf.PI * 2f / 14f + 0.11f;
                        var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                        var perp = new Vector2(-dir.y, dir.x);
                        Vector2 c = new Vector2(128, 124);
                        spikes.Add(new PolyS(c + dir * 78 + perp * 11, c + dir * 106, c + dir * 78 - perp * 11));
                    }

                    m.Body(Role.Base, spikes.ToArray());
                    m.Body(Role.Base, new PolyS(new Vector2(196, 124), new Vector2(238, 150), new Vector2(238, 98)));
                    m.Body(Role.Base, new DiscS(124, 124, 84));
                    m.Detail(Role.Light, 1f, new OvalS(124, 92, 62, 40), new OvalS(44, 124, 14, 24));
                    Face(m, 98, 152, 136, 25, 90, 166, 24, 12);
                    break;
                }

                case 6: // Jellyfish — scalloped bell, wavy tentacles.
                    for (int i = 0; i < 5; i++)
                    {
                        float x = 76 + i * 26;
                        float wobble = i % 2 == 0 ? 8f : -8f;
                        m.Body(Role.Light, new SegS(x, 104, x + wobble, 72, 12), new SegS(x + wobble, 72, x - wobble * 0.5f, 40, 11),
                            new SegS(x - wobble * 0.5f, 40, x + wobble * 0.5f, 20, 9));
                    }

                    var bell = new List<Shape> { new OvalS(128, 152, 84, 70) };
                    for (int i = 0; i < 7; i++)
                    {
                        bell.Add(new DiscS(56 + i * 24, 100, 16));
                    }

                    m.Body(Role.Base, bell.ToArray());
                    m.Detail(Role.Light, 0.7f, new OvalS(128, 168, 56, 38));
                    Face(m, 102, 154, 146, 22, 92, 186, 22, 11);
                    break;

                default: // Axolotl (rare) — frilly gills, wide smiley head.
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float sx = side;
                        m.Body(Role.Dark,
                            new SegS(128 + sx * 62, 172, 128 + sx * 100, 200, 18), new DiscS(128 + sx * 102, 202, 11),
                            new SegS(128 + sx * 70, 152, 128 + sx * 112, 162, 18), new DiscS(128 + sx * 114, 162, 11),
                            new SegS(128 + sx * 62, 132, 128 + sx * 100, 118, 18), new DiscS(128 + sx * 102, 116, 11));
                    }

                    m.Body(Role.Base, new SegS(168, 66, 214, 44, 28), new DiscS(214, 44, 14));
                    m.Body(Role.Base, new OvalS(128, 70, 50, 36), new SegS(98, 54, 82, 32, 16), new SegS(158, 54, 174, 32, 16));
                    m.Body(Role.Base, new OvalS(128, 146, 84, 62));
                    m.Detail(Role.Light, 1f, new OvalS(128, 62, 30, 20));
                    Face(m, 96, 160, 152, 23, 86, 184, 26, 12);
                    break;
            }

            return m;
        }

        private static void Face(Model m, float lx, float rx, float y, float r, float gx, float gy, float gw, float gh)
        {
            m.LeftEye = new Vector2(lx, y);
            m.RightEye = new Vector2(rx, y);
            m.EyeRadius = r;
            m.Gloss = new Vector2(gx, gy);
            m.GlossSize = new Vector2(gw, gh);
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

        // ------------------------------------------------------------ painter

        public static Texture2D Render(int id, Variant variant)
        {
            Model model = Species(id);
            Color baseC = ThemeRuntime.Color($"pet.{id}.base");
            Color dark = ThemeRuntime.Color($"pet.{id}.dark");
            Color light = ThemeRuntime.Color($"pet.{id}.light");
            var px = new Color[Size * Size];

            // Sticker outline: every outlined part, inflated, in the species' dark.
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
                return ToTexture(px);
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
                    _ => baseC,
                };

                // Top-lit form: a touch brighter up top, a touch deeper below.
                Color top = Color.Lerp(c, Color.white, 0.16f);
                Color bottom = Color.Lerp(c, dark, 0.22f);
                Paint(px, mask, top, bottom, part.Alpha);
            }

            // Gloss highlight, clipped to the body.
            float[,] gloss = new float[Size, Size];
            MenuSprites.Oval(gloss, model.Gloss.x, model.Gloss.y, model.GlossSize.x, model.GlossSize.y);
            Clip(gloss, body);
            Paint(px, gloss, Color.white, Color.white, 0.5f);

            DrawFace(px, model, variant == Variant.Closed);
            return ToTexture(px);
        }

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
