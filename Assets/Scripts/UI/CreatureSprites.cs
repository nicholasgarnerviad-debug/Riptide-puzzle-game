using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// The 8 rescue species' sprites (GDD §2.5 / §15): full-color chibi models
    /// painted by <see cref="CreatureArt"/> — open eyes, a blink frame (happy
    /// closed eyes) and a white silhouette for locked species (tint it at use).
    /// 256px, mipmapped, PPU 256 so one sprite = one board cell. Cached per variant.
    ///
    /// Id order matches creatures.json: 0 Crab · 1 Starfish · 2 Seahorse ·
    /// 3 Octopus · 4 Turtle · 5 Pufferfish · 6 Jellyfish · 7 Axolotl (rare).
    /// </summary>
    public static class CreatureSprites
    {
        public const int Size = CreatureArt.Size;
        public const float PixelsPerUnit = CreatureArt.Size;

        private static readonly Sprite?[,] cache = new Sprite?[8, 3];

        /// <summary>The species, eyes open.</summary>
        public static Sprite For(int speciesId) => Get(speciesId, CreatureArt.Variant.Open);

        /// <summary>The blink / happy frame: eyes closed in a smile.</summary>
        public static Sprite Blink(int speciesId) => Get(speciesId, CreatureArt.Variant.Closed);

        /// <summary>White outline-shape silhouette — tint it for unmet species.</summary>
        public static Sprite Silhouette(int speciesId) => Get(speciesId, CreatureArt.Variant.Silhouette);

        private static Sprite Get(int speciesId, CreatureArt.Variant variant)
        {
            int id = ((speciesId % 8) + 8) % 8;
            int v = (int)variant;
            if (cache[id, v] == null)
            {
                Texture2D tex = CreatureArt.Render(id, variant);
                cache[id, v] = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            }

            return cache[id, v]!;
        }
    }
}
