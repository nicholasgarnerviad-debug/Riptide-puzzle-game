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

        /// <summary>
        /// Spec §4.3 item 4, Tidal Pop: the slots sit in wells on a white chunky
        /// card (navy outline + lip); the ring wraps its left end.
        /// </summary>
        private void BuildCard()
        {
            float width = BoardSpec.Width + 1f;
            float height = BoardLayout.TrayCardHeight;
            Vector3 center = BoardLayout.TrayCenter;
            const float outline = 0.1f;
            const float corner = 0.45f;
            Slab("trayLip", center + new Vector3(0f, -0.16f, 0f), new Vector2(width + 2f * outline, height + 2f * outline),
                corner + outline, "panel.edge", 22);
            Slab("trayEdge", center, new Vector2(width + 2f * outline, height + 2f * outline), corner + outline,
                "panel.edge", 23);
            Slab("trayShade", center, new Vector2(width, height), corner, "panel.shade", 24);
            Slab("trayCard", center + new Vector3(0f, 0.06f, 0f), new Vector2(width, height - 0.12f), corner,
                "panel.light", 25);
            for (int slot = 0; slot < BoardSpec.TraySize; slot++)
            {
                Slab($"slotWell_{slot}", BoardLayout.TraySlotCenter(slot), new Vector2(1.85f, 2.05f), 0.32f,
                    "panel.well", 26);
            }
        }

        private void Slab(string name, Vector3 position, Vector2 size, float corner, string colorToken, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PopSprites.RoundedWorld(corner);
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            sr.color = ThemeRuntime.Color(colorToken);
            sr.sortingOrder = order;
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
                foreach (SpriteRenderer sr in go.GetComponentsInChildren<SpriteRenderer>())
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

                var gloss = new GameObject("gloss");
                gloss.transform.SetParent(go.transform, false);
                var glossSr = gloss.AddComponent<SpriteRenderer>();
                glossSr.sprite = SpriteFactory.CellGloss();
                glossSr.color = ThemeRuntime.Color("panel.light");
                glossSr.sortingOrder = 31;
            }
        }
    }
}
