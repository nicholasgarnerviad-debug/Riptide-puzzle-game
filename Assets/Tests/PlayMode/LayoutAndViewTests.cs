using NUnit.Framework;
using Riptide.Core;
using Riptide.UI;
using UnityEngine;

namespace Riptide.PlayMode.Tests
{
    /// <summary>View math that needs no frames: layout round-trips, snap radius, palette.</summary>
    public sealed class LayoutAndViewTests
    {
        [Test]
        public void CellToWorld_RoundTrips_ThroughSnap()
        {
            for (int row = 0; row < BoardSpec.Height; row += 3)
            {
                for (int col = 0; col < BoardSpec.Width; col += 2)
                {
                    Vector3 world = BoardLayout.CellToWorld(col, row);
                    bool snapped = BoardLayout.TrySnap(world, 0.6f, out int outCol, out int outRow);
                    Assert.That(snapped, Is.True, $"({col},{row})");
                    Assert.That(outCol, Is.EqualTo(col));
                    Assert.That(outRow, Is.EqualTo(row));
                }
            }
        }

        [Test]
        public void Snap_RespectsTheRadius()
        {
            Vector3 center = BoardLayout.CellToWorld(4, 6);

            Assert.That(BoardLayout.TrySnap(center + new Vector3(0.5f, 0f, 0f), 0.6f, out _, out _), Is.True,
                "0.5 cells away, inside the 0.6 radius (GDD 7.3)");
            Assert.That(BoardLayout.TrySnap(center + new Vector3(0.5f, 0.5f, 0f), 0.6f, out _, out _), Is.False,
                "0.707 cells away, outside the radius");
        }

        [Test]
        public void Waterline_TracksTheLevel()
        {
            Assert.That(BoardLayout.WaterlineY(0f), Is.EqualTo(BoardLayout.BoardBottomY).Within(0.001f));
            Assert.That(BoardLayout.WaterlineY(3f) - BoardLayout.WaterlineY(2f), Is.EqualTo(BoardLayout.CellSize).Within(0.001f));
        }

        [Test]
        public void Palette_HasSixBlockColors_PerGdd71()
        {
            Assert.That(Palette.Blocks.Length, Is.EqualTo(6));
            Assert.That(Palette.BlockColor(0), Is.EqualTo(Palette.Blocks[0]));
            Assert.That(Palette.BlockColor(7), Is.EqualTo(Palette.Blocks[1]), "color ids wrap the palette");
        }

        [Test]
        public void TraySlots_AreBelowTheBoard()
        {
            for (int slot = 0; slot < BoardSpec.TraySize; slot++)
            {
                Assert.That(BoardLayout.TraySlotCenter(slot).y, Is.LessThan(BoardLayout.BoardBottomY));
            }
        }
    }
}
