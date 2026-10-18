using System.Collections.Generic;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// The eight board creatures' Tideling models (Baby = the base chibi) and their
    /// Grown flourishes. Grand adds the crown at <c>HeadTop</c> (CreatureArt).
    /// </summary>
    internal static partial class CreatureArt
    {
        private static bool BuildBoard(Model m, string key, int stage)
        {
            bool grown = stage >= 2;
            switch (key)
            {
                case "crab": // Pinchy → Clawdette (pearl necklace) → Lord Pincerton
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float sx = side;
                        m.Body(Role.Base,
                            new SegS(128 + sx * 62, 96, 128 + sx * 98, 76, 13),
                            new SegS(128 + sx * 58, 80, 128 + sx * 90, 52, 13),
                            new SegS(128 + sx * 50, 68, 128 + sx * 72, 36, 13));
                        m.Body(Role.Base,
                            new SegS(128 + sx * 66, 128, 128 + sx * 86, 160, 18),
                            new DiscS(128 + sx * 88, 172, grown ? 34 : 30));
                        m.Detail(Role.Dark, 1f, new OvalS(128 + sx * 88, 196, 7, 15));
                    }

                    m.Body(Role.Base, new OvalS(128, 112, 82, 60));
                    m.Detail(Role.Light, 1f, new OvalS(128, 88, 52, 24));
                    if (grown)
                    {
                        m.Detail(Role.White, 1f, new DiscS(98, 74, 7), new DiscS(114, 68, 7), new DiscS(128, 66, 8),
                            new DiscS(142, 68, 7), new DiscS(158, 74, 7));
                    }

                    Face(m, 100, 156, 124, 25, 92, 148, 24, 12);
                    m.HeadTop = new Vector2(128, 168);
                    m.CrownScale = 0.85f;
                    return true;

                case "starfish": // Twinkle → Stellina (a star clip) → Supernova
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

                    if (grown)
                    {
                        m.Body(Role.Coral, new PolyS(StarPoints(196, 150, 20, 9)));
                    }

                    Face(m, 106, 150, 124, 20, 104, 150, 18, 9);
                    m.HeadTop = new Vector2(128, 200);
                    m.CrownScale = 0.7f;
                    return true;
                }

                case "seahorse": // Ripple → Galloper (a tall crest) → Tidecharger
                    m.Body(Role.Light, new DiscS(98, 214, 12), new DiscS(124, 226, 13), new DiscS(152, 222, 12));
                    if (grown)
                    {
                        m.Body(Role.Light, new DiscS(80, 196, 14), new DiscS(110, 238, 11), new DiscS(170, 210, 12));
                    }

                    m.Body(Role.Base, new ArcS(104, 50, 26, 20, -40, 250), new DiscS(130, 46, 10));
                    m.Body(Role.Light, new OvalS(70, 112, 18, 28));
                    m.Body(Role.Base, new OvalS(122, 104, 46, 50));
                    m.Body(Role.Base, new SegS(180, 158, 220, 150, 22), new OvalS(224, 150, 11, 15));
                    m.Body(Role.Base, new OvalS(132, 170, 62, 54));
                    m.Detail(Role.Light, 1f, new OvalS(134, 100, 22, 38));
                    m.Detail(Role.Dark, 0.3f, new SegS(118, 84, 150, 84, 4), new SegS(116, 100, 152, 100, 4),
                        new SegS(118, 116, 150, 116, 4));
                    Face(m, 112, 154, 174, 21, 108, 196, 22, 11);
                    m.HeadTop = new Vector2(134, 214);
                    m.CrownScale = 0.75f;
                    return true;

                case "octopus": // Inky → Octavia (a bow) → Krakenling
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
                    if (grown)
                    {
                        m.Body(Role.Coral, new OvalS(160, 214, 17, 12), new OvalS(192, 214, 17, 12), new DiscS(176, 214, 8));
                    }

                    Face(m, 100, 156, 146, 25, 94, 186, 24, 12);
                    m.HeadTop = new Vector2(122, 212);
                    m.CrownScale = 0.8f;
                    return true;

                case "turtle": // Peanut → Mossback (moss + a flower) → Elder Atoll
                    for (int side = -1; side <= 1; side += 2)
                    {
                        m.Body(Role.Light, new SegS(128 + side * 66, 112, 128 + side * 104, 92, 30),
                            new SegS(128 + side * 50, 66, 128 + side * 68, 44, 24));
                    }

                    m.Body(Role.Light, new DiscS(128, 186, 48));
                    m.Body(Role.Base, new OvalS(128, 104, 88, 60));
                    m.Detail(Role.Dark, 0.55f, new OvalS(128, 112, 28, 20), new OvalS(78, 100, 18, 16),
                        new OvalS(178, 100, 18, 16), new OvalS(104, 74, 16, 12), new OvalS(152, 74, 16, 12));
                    if (grown)
                    {
                        m.Detail(Role.Light, 0.85f, new DiscS(84, 136, 14), new DiscS(104, 146, 12), new DiscS(66, 124, 10));
                        m.Detail(Role.Accent, 1f, new DiscS(170, 140, 7), new DiscS(180, 146, 7), new DiscS(170, 152, 7),
                            new DiscS(160, 146, 7));
                        m.Detail(Role.Gold, 1f, new DiscS(170, 146, 5));
                    }

                    Face(m, 108, 148, 194, 18, 82, 140, 26, 10);
                    m.HeadTop = new Vector2(128, 230);
                    m.CrownScale = 0.7f;
                    return true;

                case "puffer": // Puff → Spikeball (stripes) → Blowfin Boss
                {
                    var spikes = new List<Shape>();
                    int count = grown ? 18 : 14;
                    for (int i = 0; i < count; i++)
                    {
                        float ang = i * Mathf.PI * 2f / count + 0.11f;
                        var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                        var perp = new Vector2(-dir.y, dir.x);
                        var c = new Vector2(128, 124);
                        spikes.Add(new PolyS(c + dir * 78 + perp * 11, c + dir * 106, c + dir * 78 - perp * 11));
                    }

                    m.Body(Role.Base, spikes.ToArray());
                    m.Body(Role.Base, new PolyS(new Vector2(196, 124), new Vector2(238, 150), new Vector2(238, 98)));
                    m.Body(Role.Base, new DiscS(124, 124, 84));
                    m.Detail(Role.Light, 1f, new OvalS(124, 92, 62, 40), new OvalS(44, 124, 14, 24));
                    if (grown)
                    {
                        m.Detail(Role.Dark, 0.35f, new ArcS(124, 124, 70, 8, 30, 70), new ArcS(124, 124, 70, 8, 110, 150),
                            new ArcS(124, 124, 56, 7, 40, 80));
                    }

                    Face(m, 98, 152, 136, 25, 90, 166, 24, 12);
                    m.HeadTop = new Vector2(124, 200);
                    m.CrownScale = 0.85f;
                    return true;
                }

                case "jelly": // Glim → Glowbell (glow spots, longer tentacles) → Aurora Bell
                    for (int i = 0; i < 5; i++)
                    {
                        float x = 76 + i * 26;
                        float wobble = i % 2 == 0 ? 8f : -8f;
                        m.Body(Role.Light, new SegS(x, 104, x + wobble, 72, 12), new SegS(x + wobble, 72, x - wobble * 0.5f, 40, 11),
                            new SegS(x - wobble * 0.5f, 40, x + wobble * 0.5f, grown ? 8 : 20, 9));
                    }

                    var bell = new List<Shape> { new OvalS(128, 152, 84, 70) };
                    for (int i = 0; i < 7; i++)
                    {
                        bell.Add(new DiscS(56 + i * 24, 100, 16));
                    }

                    m.Body(Role.Base, bell.ToArray());
                    m.Detail(Role.Light, 0.7f, new OvalS(128, 168, 56, 38));
                    if (grown)
                    {
                        m.Detail(Role.White, 0.65f, new DiscS(74, 140, 7), new DiscS(186, 136, 8), new DiscS(170, 196, 6),
                            new DiscS(82, 186, 6));
                    }

                    Face(m, 102, 154, 146, 22, 92, 186, 22, 11);
                    m.HeadTop = new Vector2(128, 214);
                    m.CrownScale = 0.85f;
                    return true;

                case "axolotl": // Lotl → Gillian (bigger frills, spots) → Axolord
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float sx = side;
                        m.Body(Role.Dark,
                            new SegS(128 + sx * 62, 172, 128 + sx * 100, 200, 18), new DiscS(128 + sx * 102, 202, 11),
                            new SegS(128 + sx * 70, 152, 128 + sx * 112, 162, 18), new DiscS(128 + sx * 114, 162, 11),
                            new SegS(128 + sx * 62, 132, 128 + sx * 100, 118, 18), new DiscS(128 + sx * 102, 116, 11));
                        if (grown)
                        {
                            m.Body(Role.Dark, new SegS(128 + sx * 56, 186, 128 + sx * 84, 226, 16),
                                new DiscS(128 + sx * 86, 228, 10));
                        }
                    }

                    m.Body(Role.Base, new SegS(168, 66, 214, 44, 28), new DiscS(214, 44, 14));
                    m.Body(Role.Base, new OvalS(128, 70, 50, 36), new SegS(98, 54, 82, 32, 16), new SegS(158, 54, 174, 32, 16));
                    m.Body(Role.Base, new OvalS(128, 146, 84, 62));
                    m.Detail(Role.Light, 1f, new OvalS(128, 62, 30, 20));
                    if (grown)
                    {
                        m.Detail(Role.Dark, 0.35f, new DiscS(78, 168, 6), new DiscS(184, 172, 7), new DiscS(170, 112, 5),
                            new DiscS(88, 114, 5));
                    }

                    Face(m, 96, 160, 152, 23, 86, 184, 26, 12);
                    m.HeadTop = new Vector2(128, 206);
                    m.CrownScale = 0.85f;
                    return true;

                default:
                    return false;
            }
        }
    }
}
