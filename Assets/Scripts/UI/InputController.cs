using System.Collections.Generic;
using Riptide.Core;
using Riptide.Game;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Riptide.UI
{
    /// <summary>
    /// GDD 7.3 drag-place: grab a tray piece, it rides 90px above the finger,
    /// a ghost shows the snapped anchor with valid/invalid tint, release places.
    /// Uses the Input System (the project runs activeInputHandler=1).
    /// </summary>
    public sealed class InputController : MonoBehaviour
    {
        private GameStore store = null!;
        private TrayView tray = null!;
        private AnimationDriver driver = null!;
        private Camera cam = null!;
        private InputTuning tuning = null!;

        private int dragSlot = -1;
        private TrayPiece dragPiece;
        private bool dragging;
        private Vector2 pressScreenStart;
        private readonly List<SpriteRenderer> carrySprites = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> ghostSprites = new List<SpriteRenderer>();

        public static InputController Create(Transform parent, GameStore store, TrayView tray,
            AnimationDriver driver, Camera cam, InputTuning tuning)
        {
            var go = new GameObject("InputController");
            go.transform.SetParent(parent, false);
            var input = go.AddComponent<InputController>();
            input.store = store;
            input.tray = tray;
            input.driver = driver;
            input.cam = cam;
            input.tuning = tuning;
            return input;
        }

        private void Update()
        {
            Pointer? pointer = Pointer.current;
            if (pointer == null)
            {
                return;
            }

            Vector2 screenPos = pointer.position.ReadValue();
            bool pressed = pointer.press.isPressed;

            if (!dragging)
            {
                if (pressed && dragSlot < 0)
                {
                    TryBeginPress(screenPos);
                }
                else if (pressed && dragSlot >= 0)
                {
                    if (Vector2.Distance(screenPos, pressScreenStart) >= tuning.dragStartPixels)
                    {
                        BeginDrag();
                    }
                }
                else if (!pressed)
                {
                    dragSlot = -1;
                }

                return;
            }

            if (pressed)
            {
                UpdateDrag(screenPos);
            }
            else
            {
                EndDrag(screenPos);
            }
        }

        private void TryBeginPress(Vector2 screenPos)
        {
            if (driver.IsAnimating || store.State.Status.IsTerminal())
            {
                return;
            }

            Vector3 world = ScreenToWorld(screenPos);
            for (int slot = 0; slot < BoardSpec.TraySize; slot++)
            {
                TrayPiece? piece = store.State.TrayAt(slot);
                if (!piece.HasValue)
                {
                    continue;
                }

                if (Vector2.Distance(world, BoardLayout.TraySlotCenter(slot)) <= tuning.grabRadiusWorld)
                {
                    dragSlot = slot;
                    dragPiece = piece.Value;
                    pressScreenStart = screenPos;
                    return;
                }
            }
        }

        private void BeginDrag()
        {
            dragging = true;
            tray.SetSlotVisible(dragSlot, false);
            BuildCarryAndGhost();
        }

        private void UpdateDrag(Vector2 screenPos)
        {
            Vector3 lifted = LiftedWorld(screenPos);
            IReadOnlyList<PieceCell> mask = PieceCatalog.MaskOf(dragPiece.Piece);
            Vector2 maskCenter = MaskCenter(mask);

            // The carried piece rides centered above the finger.
            for (int i = 0; i < mask.Count; i++)
            {
                carrySprites[i].transform.position = lifted + new Vector3(
                    (mask[i].Dx - maskCenter.x) * BoardLayout.CellSize,
                    (mask[i].Dy - maskCenter.y) * BoardLayout.CellSize,
                    0f);
            }

            // Ghost: snap the anchor cell (mask (0,0)) to the nearest cell within radius.
            Vector3 anchorWorld = lifted - new Vector3(maskCenter.x * BoardLayout.CellSize, maskCenter.y * BoardLayout.CellSize, 0f);
            bool snapped = BoardLayout.TrySnap(anchorWorld, tuning.snapRadiusCells, out int col, out int row);
            if (!snapped)
            {
                SetGhostVisible(false);
                return;
            }

            bool valid = PlacementValidator.CanPlace(store.State, dragPiece.Piece, SafePos(col, row, mask));
            for (int i = 0; i < mask.Count; i++)
            {
                ghostSprites[i].enabled = true;
                ghostSprites[i].transform.position = BoardLayout.CellToWorld(col + mask[i].Dx, row + mask[i].Dy);
                ghostSprites[i].color = valid ? Palette.GhostValid : Palette.GhostInvalid;
            }
        }

        private void EndDrag(Vector2 screenPos)
        {
            Vector3 lifted = LiftedWorld(screenPos);
            IReadOnlyList<PieceCell> mask = PieceCatalog.MaskOf(dragPiece.Piece);
            Vector2 maskCenter = MaskCenter(mask);
            Vector3 anchorWorld = lifted - new Vector3(maskCenter.x * BoardLayout.CellSize, maskCenter.y * BoardLayout.CellSize, 0f);

            bool placed = false;
            if (BoardLayout.TrySnap(anchorWorld, tuning.snapRadiusCells, out int col, out int row))
            {
                GridPos target = SafePos(col, row, mask);
                if (PlacementValidator.CanPlace(store.State, dragPiece.Piece, target))
                {
                    placed = store.TryDispatch(new PlaceMove(dragSlot, target));
                }
            }

            if (!placed)
            {
                tray.SetSlotVisible(dragSlot, true);
            }

            TearDownDrag();
        }

        /// <summary>Mask cells can leave the grid; GridPos refuses out-of-bounds, so clamp for the query.</summary>
        private static GridPos SafePos(int col, int row, IReadOnlyList<PieceCell> mask)
        {
            int maxDx = 0;
            int maxDy = 0;
            foreach (PieceCell c in mask)
            {
                maxDx = Mathf.Max(maxDx, c.Dx);
                maxDy = Mathf.Max(maxDy, c.Dy);
            }

            int clampedCol = Mathf.Clamp(col, 0, BoardSpec.Width - 1 - maxDx);
            int clampedRow = Mathf.Clamp(row, 0, BoardSpec.Height - 1 - maxDy);
            return new GridPos(clampedCol, clampedRow);
        }

        private void TearDownDrag()
        {
            foreach (SpriteRenderer sr in carrySprites)
            {
                Destroy(sr.gameObject);
            }

            foreach (SpriteRenderer sr in ghostSprites)
            {
                Destroy(sr.gameObject);
            }

            carrySprites.Clear();
            ghostSprites.Clear();
            dragging = false;
            dragSlot = -1;
        }

        private void BuildCarryAndGhost()
        {
            IReadOnlyList<PieceCell> mask = PieceCatalog.MaskOf(dragPiece.Piece);
            for (int i = 0; i < mask.Count; i++)
            {
                var carry = new GameObject("carry");
                carry.transform.SetParent(transform, false);
                carry.transform.localScale = Vector3.one * 0.94f;
                var carrySr = carry.AddComponent<SpriteRenderer>();
                carrySr.sprite = SpriteFactory.Cell();
                carrySr.color = Palette.BlockColor(dragPiece.ColorId);
                carrySr.sortingOrder = 90;
                carrySprites.Add(carrySr);

                var ghost = new GameObject("ghost");
                ghost.transform.SetParent(transform, false);
                ghost.transform.localScale = Vector3.one * 0.94f;
                var ghostSr = ghost.AddComponent<SpriteRenderer>();
                ghostSr.sprite = SpriteFactory.Cell();
                ghostSr.color = Palette.GhostValid;
                ghostSr.sortingOrder = 80;
                ghostSr.enabled = false;
                ghostSprites.Add(ghostSr);
            }
        }

        private void SetGhostVisible(bool visible)
        {
            foreach (SpriteRenderer sr in ghostSprites)
            {
                sr.enabled = visible;
            }
        }

        private Vector3 LiftedWorld(Vector2 screenPos)
        {
            // GDD 7.3: the piece rides liftPixels above the finger for thumb visibility.
            Vector3 world = ScreenToWorld(screenPos + new Vector2(0f, tuning.liftPixels));
            world.z = 0f;
            return world;
        }

        private Vector3 ScreenToWorld(Vector2 screenPos)
        {
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
            world.z = 0f;
            return world;
        }

        private static Vector2 MaskCenter(IReadOnlyList<PieceCell> mask)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (PieceCell c in mask)
            {
                minX = Mathf.Min(minX, c.Dx);
                maxX = Mathf.Max(maxX, c.Dx);
                minY = Mathf.Min(minY, c.Dy);
                maxY = Mathf.Max(maxY, c.Dy);
            }

            return new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        }
    }
}
