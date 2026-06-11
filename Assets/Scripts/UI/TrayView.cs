using System.Collections.Generic;
using Riptide.Core;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>Renders the 3 dealt pieces below the board as mini silhouettes.</summary>
    public sealed class TrayView : MonoBehaviour
    {
        private const float MiniScale = 0.3f;
        private readonly List<GameObject>[] slotSprites = { new List<GameObject>(), new List<GameObject>(), new List<GameObject>() };
        private readonly PieceId?[] shown = new PieceId?[BoardSpec.TraySize];

        public static TrayView Create(Transform parent)
        {
            var go = new GameObject("TrayView");
            go.transform.SetParent(parent, false);
            return go.AddComponent<TrayView>();
        }

        public void Render(GameState state)
        {
            for (int slot = 0; slot < BoardSpec.TraySize; slot++)
            {
                ClearSlot(slot);
                TrayPiece? piece = state.TrayAt(slot);
                shown[slot] = piece?.Piece;
                if (!piece.HasValue)
                {
                    continue;
                }

                BuildMini(slot, piece.Value);
            }
        }

        public PieceId? ShownPieceAt(int slot) => shown[slot];

        public void SetSlotVisible(int slot, bool visible)
        {
            foreach (GameObject go in slotSprites[slot])
            {
                go.SetActive(visible);
            }
        }

        private void ClearSlot(int slot)
        {
            foreach (GameObject go in slotSprites[slot])
            {
                Destroy(go);
            }

            slotSprites[slot].Clear();
        }

        private void BuildMini(int slot, TrayPiece piece)
        {
            IReadOnlyList<PieceCell> mask = PieceCatalog.MaskOf(piece.Piece);
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (PieceCell c in mask)
            {
                minX = Mathf.Min(minX, c.Dx);
                maxX = Mathf.Max(maxX, c.Dx);
                minY = Mathf.Min(minY, c.Dy);
                maxY = Mathf.Max(maxY, c.Dy);
            }

            Vector3 center = BoardLayout.TraySlotCenter(slot);
            var maskCenter = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            foreach (PieceCell c in mask)
            {
                var go = new GameObject($"tray_{slot}_{c.Dx}_{c.Dy}");
                go.transform.SetParent(transform, false);
                go.transform.position = center + new Vector3(
                    (c.Dx - maskCenter.x) * MiniScale,
                    (c.Dy - maskCenter.y) * MiniScale,
                    0f);
                go.transform.localScale = Vector3.one * (MiniScale * 0.92f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Cell();
                sr.color = Palette.BlockColor(piece.ColorId);
                sr.sortingOrder = 30;
                slotSprites[slot].Add(go);
            }
        }
    }
}
