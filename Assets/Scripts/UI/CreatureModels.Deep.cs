using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// The Call-only Tidelings' models (GDD §16.1) and their Grown flourishes. Same
    /// sticker language as the board creatures: outlined layered parts, top-lit
    /// shading, the shared face. Grand adds the crown at <c>HeadTop</c>.
    /// </summary>
    internal static partial class CreatureArt
    {
        private static bool BuildDeep(Model m, string key, int stage)
        {
            bool grown = stage >= 2;
            switch (key)
            {
                case "narwhal": // Tusky → Unicorna (spots, longer tusk) → Polaris
                    m.Body(Role.Base, new SegS(46, 120, 76, 120, 26), new OvalS(26, 146, 20, 12), new OvalS(26, 94, 20, 12));
                    m.Body(Role.Base, new OvalS(130, 116, 88, 62), new OvalS(132, 62, 24, 12));
                    m.Body(Role.Accent, new PolyS(new Vector2(188, 146), new Vector2(202, 158),
                        new Vector2(grown ? 250 : 236, grown ? 214 : 198)));
                    m.Detail(Role.Dark, 0.3f, new SegS(206, 162, 214, 166, 3), new SegS(218, 176, 226, 180, 3));
                    m.Detail(Role.Light, 1f, new OvalS(140, 86, 62, 24));
                    if (grown)
                    {
                        m.Detail(Role.Dark, 0.3f, new DiscS(84, 140, 7), new DiscS(104, 156, 6), new DiscS(126, 162, 5),
                            new DiscS(70, 120, 5));
                    }

                    Face(m, 148, 184, 128, 17, 110, 160, 28, 11);
                    m.HeadTop = new Vector2(132, 172);
                    m.CrownScale = 0.75f;
                    return true;

                case "manta": // Flapjack → Wavecloak (wave marks) → Grand Mantle
                    m.Body(Role.Base, new SegS(128, 76, 128, 22, 8), new DiscS(128, 20, 6));
                    m.Body(Role.Base, new PolyS(new Vector2(10, 134), new Vector2(128, 192), new Vector2(246, 134),
                        new Vector2(128, 74)), new DiscS(18, 134, 12), new DiscS(238, 134, 12));
                    m.Body(Role.Base, new OvalS(106, 196, 12, 20), new OvalS(150, 196, 12, 20));
                    m.Detail(Role.Light, 1f, new OvalS(128, 128, 46, 34));
                    if (grown)
                    {
                        m.Detail(Role.Light, 0.75f, new SegS(44, 132, 70, 146, 6), new SegS(56, 122, 80, 134, 6),
                            new SegS(212, 132, 186, 146, 6), new SegS(200, 122, 176, 134, 6));
                    }

                    Face(m, 108, 148, 146, 17, 112, 172, 30, 10);
                    m.HeadTop = new Vector2(128, 198);
                    m.CrownScale = 0.7f;
                    return true;

                case "otter": // Pebbles → Driftwood (whiskers, a bigger shell) → Kelp Captain
                    m.Body(Role.Base, new SegS(158, 62, 212, 42, 28), new DiscS(212, 42, 14));
                    m.Body(Role.Base, new DiscS(100, 40, 16), new DiscS(156, 40, 16));
                    m.Body(Role.Base, new OvalS(128, 96, 54, 58));
                    m.Detail(Role.Light, 1f, new OvalS(128, 88, 34, 40));
                    m.Body(Role.Base, new DiscS(80, 214, 15), new DiscS(176, 214, 15));
                    m.Body(Role.Base, new DiscS(128, 170, 58));
                    m.Detail(Role.Light, 1f, new DiscS(80, 214, 7), new DiscS(176, 214, 7));
                    m.Body(Role.Accent, new DiscS(128, 104, grown ? 22 : 17));
                    m.Detail(Role.Dark, 0.35f, new SegS(118, 104, 138, 104, 3), new SegS(128, 94, 128, 114, 3));
                    m.Body(Role.Base, new SegS(90, 120, 114, 104, 18), new SegS(166, 120, 142, 104, 18));
                    m.Detail(Role.Light, 1f, new OvalS(128, 150, 32, 22));
                    m.Detail(Role.Dark, 1f, new OvalS(128, 162, 9, 6));
                    if (grown)
                    {
                        m.Detail(Role.Dark, 0.6f, new SegS(86, 152, 104, 150, 3), new SegS(86, 142, 104, 146, 3),
                            new SegS(170, 152, 152, 150, 3), new SegS(170, 142, 152, 146, 3));
                    }

                    Face(m, 106, 150, 180, 15, 110, 206, 22, 10);
                    m.HeadTop = new Vector2(128, 222);
                    m.CrownScale = 0.75f;
                    return true;

                case "angler": // Flicker → Lurelight (a brighter, bigger lure) → Abyss Beacon
                    m.Body(Role.Base, new PolyS(new Vector2(48, 124), new Vector2(10, 162), new Vector2(10, 86)));
                    m.Body(Role.Base, new SegS(118, 188, 134, 226, 8), new SegS(134, 226, 170, 234, 8));
                    m.Body(Role.Base, new OvalS(126, 118, 86, 76));
                    m.Detail(Role.Accent, grown ? 0.45f : 0.3f, new DiscS(178, 230, grown ? 36 : 28));
                    m.Body(Role.Accent, new DiscS(178, 230, grown ? 18 : 14));
                    m.Detail(Role.Dark, 1f, new OvalS(150, 80, 48, 22));
                    for (int i = 0; i < 5; i++)
                    {
                        float x = 116 + i * 17;
                        m.Detail(Role.White, 1f, new PolyS(new Vector2(x - 6, 98), new Vector2(x + 6, 98), new Vector2(x, 86)));
                    }

                    m.Detail(Role.Dark, 0.5f, new OvalS(92, 58, 16, 10));
                    Face(m, 102, 146, 146, 21, 98, 178, 28, 12);
                    m.HeadTop = new Vector2(92, 190);
                    m.CrownScale = 0.7f;
                    return true;

                case "nautilus": // Swirl → Spiralla (more stripes, a pearl) → Chamber Lord
                    for (int i = 0; i < 4; i++)
                    {
                        float x = 34 + i * 18;
                        m.Body(Role.Light, new SegS(x + 10, 72, x, 30, 12), new DiscS(x, 28, 7));
                    }

                    m.Body(Role.Base, new DiscS(148, 134, 84));
                    m.Detail(Role.Accent, 1f, new ArcS(148, 134, 70, 11, 30, 150), new ArcS(148, 134, 52, 10, 70, 230),
                        new DiscS(160, 142, 14));
                    if (grown)
                    {
                        m.Detail(Role.Accent, 1f, new ArcS(148, 134, 70, 11, 200, 280), new ArcS(148, 134, 34, 8, 150, 330));
                        m.Detail(Role.White, 1f, new DiscS(160, 142, 7));
                    }

                    m.Body(Role.Light, new OvalS(78, 98, 46, 38));
                    Face(m, 64, 96, 104, 13, 160, 190, 30, 12);
                    m.HeadTop = new Vector2(150, 214);
                    m.CrownScale = 0.8f;
                    return true;

                case "dragon": // Leafy → Ribbontail (more fronds) → Tidewyrm
                    m.Body(Role.Accent, new OvalS(70, 132, 20, 10), new OvalS(62, 98, 18, 9), new OvalS(152, 216, 10, 18),
                        new OvalS(108, 210, 9, 16), new OvalS(166, 90, 18, 9));
                    if (grown)
                    {
                        m.Body(Role.Accent, new OvalS(52, 116, 16, 8), new OvalS(184, 204, 9, 16), new OvalS(78, 70, 16, 8),
                            new OvalS(174, 118, 16, 8));
                    }

                    m.Body(Role.Base, new ArcS(100, 52, 24, 18, -40, 250), new DiscS(124, 48, 9));
                    m.Body(Role.Base, new OvalS(118, 110, 40, 48));
                    m.Body(Role.Base, new SegS(178, 166, 226, 154, 16), new OvalS(230, 154, 10, 12));
                    m.Body(Role.Base, new OvalS(140, 170, 48, 40));
                    m.Detail(Role.Light, 1f, new OvalS(128, 104, 18, 32));
                    Face(m, 124, 160, 176, 17, 124, 196, 20, 9);
                    m.HeadTop = new Vector2(140, 206);
                    m.CrownScale = 0.7f;
                    return true;

                case "squid": // Inklet → Longarm (longer arms, spots) → The Colossus
                    for (int i = 0; i < 6; i++)
                    {
                        float x0 = 92 + i * 14.4f;
                        float wobble = i % 2 == 0 ? 6f : -6f;
                        m.Body(Role.Base, new SegS(x0, 96, x0 + wobble, 58, 12), new SegS(x0 + wobble, 58, x0, 30, 10));
                    }

                    float reach = grown ? 30 : 46;
                    m.Body(Role.Base, new SegS(98, 92, 40, reach, 12), new DiscS(38, reach - 2, 11),
                        new SegS(158, 92, 216, reach, 12), new DiscS(218, reach - 2, 11));
                    m.Body(Role.Base, new PolyS(new Vector2(86, 194), new Vector2(128, 228), new Vector2(170, 194)));
                    m.Body(Role.Base, new OvalS(128, 148, 46, 62));
                    m.Detail(Role.Light, 1f, new OvalS(128, 170, 24, 30));
                    if (grown)
                    {
                        m.Detail(Role.Dark, 0.3f, new DiscS(100, 168, 6), new DiscS(156, 176, 7), new DiscS(150, 106, 5),
                            new DiscS(104, 104, 5));
                    }

                    Face(m, 108, 148, 124, 19, 112, 190, 18, 12);
                    m.HeadTop = new Vector2(128, 222);
                    m.CrownScale = 0.62f;
                    return true;

                case "whale": // Splash → Songkeeper (grooves, a bigger spout) → Leviathan
                    m.Body(Role.Base, new PolyS(new Vector2(48, 148), new Vector2(6, 186), new Vector2(34, 172),
                        new Vector2(56, 198), new Vector2(62, 150)));
                    m.Body(Role.Base, new OvalS(136, 108, 104, 66), new OvalS(124, 56, 26, 12));
                    m.Detail(Role.Light, 1f, new OvalS(150, 74, 70, 26));
                    if (grown)
                    {
                        m.Detail(Role.Dark, 0.25f, new SegS(104, 72, 196, 72, 3), new SegS(110, 62, 190, 62, 3),
                            new SegS(108, 82, 194, 82, 3));
                    }

                    float spout = grown ? 1.3f : 1f;
                    m.Body(Role.Accent, new SegS(198, 172, 198, 192, 6), new DiscS(198, 202, 10 * spout),
                        new DiscS(184, 216, 8 * spout), new DiscS(212, 216, 8 * spout));
                    Face(m, 168, 204, 116, 16, 120, 150, 40, 14);
                    m.HeadTop = new Vector2(128, 170);
                    m.CrownScale = 0.8f;
                    return true;

                default:
                    return false;
            }
        }
    }
}
