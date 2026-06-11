using Riptide.Core;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// Pure board geometry: cell-world conversions used by views, input and tests.
    /// One world unit = one cell. Board is x-centered; the tray lives below it.
    /// </summary>
    public static class BoardLayout
    {
        public const float CellSize = 1f;

        /// <summary>World position of the center of cell (0,0) — seabed, left column.</summary>
        public static readonly Vector2 Origin = new Vector2(-(BoardSpec.Width - 1) * 0.5f, -4.6f);

        public static Vector3 CellToWorld(int col, int row) =>
            new Vector3(Origin.x + col * CellSize, Origin.y + row * CellSize, 0f);

        /// <summary>Continuous (unrounded) cell coordinates for a world point.</summary>
        public static Vector2 WorldToCellContinuous(Vector3 world) =>
            new Vector2((world.x - Origin.x) / CellSize, (world.y - Origin.y) / CellSize);

        /// <summary>
        /// Magnetic snap (GDD 7.3): the nearest cell if the continuous position is
        /// within <paramref name="snapRadiusCells"/> of its center; false otherwise.
        /// </summary>
        public static bool TrySnap(Vector3 world, float snapRadiusCells, out int col, out int row)
        {
            Vector2 continuous = WorldToCellContinuous(world);
            col = Mathf.RoundToInt(continuous.x);
            row = Mathf.RoundToInt(continuous.y);
            float distance = Vector2.Distance(continuous, new Vector2(col, row));
            return distance <= snapRadiusCells && BoardSpec.InBounds(col, row);
        }

        public static float BoardBottomY => Origin.y - CellSize * 0.5f;

        public static float BoardTopY => Origin.y + (BoardSpec.Height - 1) * CellSize + CellSize * 0.5f;

        public static float WaterlineY(float waterLevel) => BoardBottomY + waterLevel * CellSize;

        public static Vector3 TrayCenter => new Vector3(0f, BoardBottomY - 2.0f, 0f);

        public static Vector3 TraySlotCenter(int slot) =>
            TrayCenter + new Vector3((slot - 1) * 2.6f, 0f, 0f);
    }
}
