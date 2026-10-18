using System.Collections.Generic;
using NUnit.Framework;
using Riptide.Core;
using Riptide.Game;
using Riptide.UI;
using UnityEngine;

namespace Riptide.PlayMode.Tests
{
    /// <summary>
    /// GDD §16 art: every Tideling in pals.json draws all three stages and its Sparkle
    /// as a real shape (not empty, not a block), each form distinct from its others, and
    /// a key with no model yet still draws (the round placeholder) — so adding a
    /// Tideling to content can never crash or blank a screen.
    /// </summary>
    public sealed class TidelingArtTests
    {
        [Test]
        public void EveryTideling_DrawsThreeDistinctStages_AndASparkle()
        {
            PalsConfig pals = RuntimeContent.LoadPals(RuntimeContent.LoadCreatures().Count);
            foreach (PalLine line in pals.Lines)
            {
                var signatures = new HashSet<string>();
                for (int stage = 1; stage <= PalsLoader.StageCount; stage++)
                {
                    Texture2D tex = CreatureSprites.Tideling(line.Key, stage).texture;
                    float coverage = Coverage(tex);
                    Assert.That(coverage, Is.InRange(0.05f, 0.85f), $"{line.Key} stage {stage} coverage {coverage:0.00}");
                    signatures.Add(Signature(tex));
                }

                Assert.That(signatures.Count, Is.EqualTo(3), $"{line.Key}: Baby, Grown and Grand must look different");
                Assert.That(Coverage(CreatureSprites.Tideling(line.Key, 3, sparkle: true).texture), Is.GreaterThan(0.05f),
                    $"{line.Key} Sparkle draws");
                Assert.That(Tint(CreatureSprites.Tideling(line.Key, 1, sparkle: true).texture),
                    Is.Not.EqualTo(Tint(CreatureSprites.Tideling(line.Key, 1).texture)), $"{line.Key} Sparkle recolors");
            }
        }

        [Test]
        public void AKeyWithoutArtYet_StillDraws()
        {
            Texture2D tex = CreatureSprites.Tideling("not-drawn-yet", 2).texture;
            Assert.That(Coverage(tex), Is.GreaterThan(0.05f));
        }

        private static float Coverage(Texture2D tex)
        {
            int opaque = 0;
            Color[] pixels = tex.GetPixels();
            foreach (Color p in pixels)
            {
                opaque += p.a > 0.5f ? 1 : 0;
            }

            return (float)opaque / pixels.Length;
        }

        /// <summary>The average opaque color, bucketed — a coarse palette fingerprint.</summary>
        private static string Tint(Texture2D tex)
        {
            Color sum = Color.clear;
            int n = 0;
            foreach (Color p in tex.GetPixels())
            {
                if (p.a > 0.5f)
                {
                    sum += p;
                    n++;
                }
            }

            Color avg = n > 0 ? sum / n : Color.clear;
            return $"{Mathf.RoundToInt(avg.r * 10)}/{Mathf.RoundToInt(avg.g * 10)}/{Mathf.RoundToInt(avg.b * 10)}";
        }

        private static string Signature(Texture2D tex)
        {
            Color[] pixels = tex.GetPixels();
            var sb = new System.Text.StringBuilder(256);
            int cell = tex.width / 16;
            for (int gy = 0; gy < 16; gy++)
            {
                for (int gx = 0; gx < 16; gx++)
                {
                    int filled = 0;
                    for (int y = 0; y < cell; y++)
                    {
                        for (int x = 0; x < cell; x++)
                        {
                            filled += pixels[(gy * cell + y) * tex.width + gx * cell + x].a > 0.5f ? 1 : 0;
                        }
                    }

                    sb.Append(filled > cell * cell / 2 ? '#' : '.');
                }
            }

            return sb.ToString();
        }
    }
}
