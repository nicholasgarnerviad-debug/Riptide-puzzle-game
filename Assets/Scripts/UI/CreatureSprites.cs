using System.Collections.Generic;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// Sprites for the board creatures (GDD §2.5) and every Tideling form (GDD §16),
    /// painted by <see cref="CreatureArt"/>: eyes open, a blink / happy frame (eyes
    /// closed in a smile) and a white silhouette for the unknown (tint it at use).
    /// 256px, mipmapped, PPU 256 so one sprite = one board cell. Cached per form.
    ///
    /// Board ids match creatures.json: 0 Crab · 1 Starfish · 2 Seahorse · 3 Octopus ·
    /// 4 Turtle · 5 Pufferfish · 6 Jellyfish · 7 Axolotl (rare) — each drawn as its
    /// Tideling's Baby form.
    /// </summary>
    public static class CreatureSprites
    {
        public const int Size = CreatureArt.Size;
        public const float PixelsPerUnit = CreatureArt.Size;

        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>The board species, eyes open, full size.</summary>
        public static Sprite For(int speciesId) => Get(BoardKey(speciesId), 1, CreatureArt.Variant.Open, false, false);

        /// <summary>The board species' blink / happy frame.</summary>
        public static Sprite Blink(int speciesId) => Get(BoardKey(speciesId), 1, CreatureArt.Variant.Closed, false, false);

        /// <summary>The board species' silhouette — tint it for the unmet.</summary>
        public static Sprite Silhouette(int speciesId) =>
            Get(BoardKey(speciesId), 1, CreatureArt.Variant.Silhouette, false, false);

        /// <summary>
        /// A Tideling form by its pals.json key, stage (1 Baby … 3 Grand) and Sparkle; the
        /// stage also sets its size (Babies small, Grand full) so growing up shows.
        /// </summary>
        public static Sprite Tideling(string key, int stage, bool sparkle = false, bool closed = false) =>
            Get(key, stage, closed ? CreatureArt.Variant.Closed : CreatureArt.Variant.Open, sparkle, true);

        public static Sprite TidelingSilhouette(string key, int stage) =>
            Get(key, stage, CreatureArt.Variant.Silhouette, false, true);

        private static string BoardKey(int speciesId) =>
            CreatureArt.BoardKeys[((speciesId % CreatureArt.BoardKeys.Length) + CreatureArt.BoardKeys.Length)
                % CreatureArt.BoardKeys.Length];

        private static Sprite Get(string key, int stage, CreatureArt.Variant variant, bool sparkle, bool grow)
        {
            string id = $"{key}/{stage}/{(int)variant}/{(sparkle ? 1 : 0)}/{(grow ? 1 : 0)}";
            // A sprite made in one play session is destroyed when it ends (editor with
            // domain reload off) — Unity's null check catches that and we repaint.
            if (!cache.TryGetValue(id, out Sprite sprite) || sprite == null)
            {
                Texture2D tex = CreatureArt.Render(key, stage, variant, sparkle, grow);
                sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
                cache[id] = sprite;
            }

            return sprite;
        }
    }
}
