using System.Collections.Generic;
using Riptide.Core;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>Renders the 3 dealt pieces below the board as mini silhouettes.</summary>
    public sealed class TrayView : MonoBehaviour
    {
        /// <summary>
        /// Genre pass (spec §12.4): tray minis read at a glance like the genre's
        /// (~0.6 of board scale is the Block Blast/Woodoku norm; 0.42 is our max —
        /// a 5-cell piece must fit the 2.25-world slot pitch). Single source —
        /// InputController's fly-back shrinks to this same value.
        /// </summary>
        public const float MiniScale = 0.42f;
        private readonly List<GameObject>[] slotSprites = { new List<GameObject>(), new List<GameObject>(), new List<GameObject>() };
        private readonly PieceId?[] shown = new PieceId?[BoardSpec.TraySize];

        public static TrayView Create(Transform parent)
        {
            var go = new GameObject("TrayView");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<TrayView>();
            view.BuildCard();
            return view;
        }

        /// <summary>Spec §4.3 item 4: the slots sit on a bg.surface card; the ring wraps its left end.</summary>
        private void BuildCard()
        {
            var cardGo = new GameObject("trayCard");
            cardGo.transform.SetParent(transform, false);
            cardGo.transform.position = BoardLayout.TrayCenter;
            var card = cardGo.AddComponent<SpriteRenderer>();
            card.sprite = SpriteFactory.RoundedFill();
            card.drawMode = SpriteDrawMode.Sliced;
            card.size = new Vector2(BoardSpec.Width + 1f, BoardLayout.TrayCardHeight);
            card.color = ThemeRuntime.Color("bg.surface");
            card.sortingOrder = 25;

            var strokeGo = new GameObject("trayCardStroke");
            strokeGo.transform.SetParent(transform, false);
            strokeGo.transform.position = BoardLayout.TrayCenter;
            var stroke = strokeGo.AddComponent<SpriteRenderer>();
            stroke.sprite = SpriteFactory.RoundedStroke();
            stroke.drawMode = SpriteDrawMode.Sliced;
            stroke.size = new Vector2(BoardSpec.Width + 1f, BoardLayout.TrayCardHeight);
            stroke.color = ThemeRuntime.Color("stroke.subtle");
            stroke.sortingOrder = 26;
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

        /// <summary>Spec §4.3: while dragging, the slot keeps a dim ghost of the piece.</summary>
        public void SetSlotGhost(int slot, bool ghosted)
        {
            foreach (GameObject go in slotSprites[slot])
            {
                go.SetActive(true);
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    Color c = sr.color;
                    c.a = ghosted ? 0.3f : 1f;
                    sr.color = c;
                }
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
