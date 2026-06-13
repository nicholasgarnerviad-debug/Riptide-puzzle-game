using System.Collections.Generic;
using NUnit.Framework;
using Riptide.UI;
using UnityEngine;

namespace Riptide.PlayMode.Tests
{
    /// <summary>
    /// The 8 rescue species (GDD §2.5) must render as DISTINCT silhouettes, not
    /// the one eyed-circle placeholder — proven by per-sprite pixel-coverage
    /// signatures. Also a cache-identity check (each id returns a stable sprite).
    /// </summary>
    public sealed class CreatureSpriteTests
    {
        [Test]
        public void EightSpecies_AreNonNull_Cached_AndDistinct()
        {
            var signatures = new HashSet<string>();
            for (int id = 0; id < 8; id++)
            {
                Sprite sprite = CreatureSprites.For(id);
                Assert.That(sprite, Is.Not.Null, $"species {id} sprite");
                Assert.That(CreatureSprites.For(id), Is.SameAs(sprite), $"species {id} is cached");

                signatures.Add(Signature(sprite));
            }

            Assert.That(signatures.Count, Is.EqualTo(8),
                "all 8 species silhouettes must be visually distinct (no shared placeholder)");
        }

        [Test]
        public void Sprites_HaveOpaqueBodyAndTransparentMargin()
        {
            // A real silhouette: meaningful filled coverage, but not a full square.
            for (int id = 0; id < 8; id++)
            {
                Texture2D tex = CreatureSprites.For(id).texture;
                int opaque = 0;
                int total = tex.width * tex.height;
                Color[] pixels = tex.GetPixels();
                foreach (Color p in pixels)
                {
                    if (p.a > 0.5f)
                    {
                        opaque++;
                    }
                }

                float coverage = (float)opaque / total;
                Assert.That(coverage, Is.InRange(0.10f, 0.80f),
                    $"species {id} coverage {coverage:0.00} — should read as a shape, not empty or a full block");
            }
        }

        private static string Signature(Sprite sprite)
        {
            Texture2D tex = sprite.texture;
            Color[] pixels = tex.GetPixels();
            // Coarse 8x8 alpha-coverage grid — stable, distinct per silhouette.
            var sb = new System.Text.StringBuilder(64);
            int cell = tex.width / 8;
            for (int gy = 0; gy < 8; gy++)
            {
                for (int gx = 0; gx < 8; gx++)
                {
                    int filled = 0;
                    for (int y = 0; y < cell; y++)
                    {
                        for (int x = 0; x < cell; x++)
                        {
                            if (pixels[(gy * cell + y) * tex.width + (gx * cell + x)].a > 0.5f)
                            {
                                filled++;
                            }
                        }
                    }

                    sb.Append(filled > cell * cell / 2 ? '#' : '.');
                }
            }

            return sb.ToString();
        }
    }
}
