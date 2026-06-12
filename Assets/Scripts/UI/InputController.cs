using System.Collections;
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
        private bool flyingBack;
        private Vector2 pressScreenStart;
        private float lastShimmerTime = float.NegativeInfinity;
        private readonly List<SpriteRenderer> carrySprites = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> ghostSprites = new List<SpriteRenderer>();

        /// <summary>Spec §4.3: the carried piece scales up 1.08 while lifted.</summary>
        public const float CarryScale = 1.08f;

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
                if (flyingBack)
                {
                    return; // the rejected piece is mid-flight; input resumes after.
                }

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
            // Spec §4.3: the tray slot leaves a ghost, not a hole.
            tray.SetSlotGhost(dragSlot, true);
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

            if (valid)
            {
                MaybeShimmerTell(col, row, mask);
            }
        }

        /// <summary>
        /// Spec §4.3 "you see it too": when the ghost would leave a row one cell
        /// short, the missing cell's column shimmers once — at most once per
        /// t.shimmerCooldown.
        /// </summary>
        private void MaybeShimmerTell(int col, int row, IReadOnlyList<PieceCell> mask)
        {
            if (Time.realtimeSinceStartup - lastShimmerTime < ThemeRuntime.Seconds("t.shimmerCooldown"))
            {
                return;
            }

            var ghostRows = new HashSet<int>();
            foreach (PieceCell c in mask)
            {
                ghostRows.Add(row + c.Dy);
            }

            foreach (int r in ghostRows)
            {
                if (r < 0 || r >= BoardSpec.Height)
                {
                    continue;
                }

                int missingCol = -1;
                int empty = 0;
                for (int x = 0; x < BoardSpec.Width; x++)
                {
                    bool filled = store.State.CellAt(x, r).Kind != CellKind.Empty;
                    if (!filled)
                    {
                        bool ghosted = false;
                        foreach (PieceCell c in mask)
                        {
                            if (col + c.Dx == x && row + c.Dy == r)
                            {
                                ghosted = true;
                                break;
                            }
                        }

                        if (!ghosted)
                        {
                            empty++;
                            missingCol = x;
                        }
                    }
                }

                if (empty == 1)
                {
                    lastShimmerTime = Time.realtimeSinceStartup;
                    StartCoroutine(Shimmer(missingCol, r));
                    return;
                }
            }
        }

        private IEnumerator Shimmer(int col, int row)
        {
            var go = new GameObject("shimmer");
            go.transform.SetParent(transform, false);
            go.transform.position = BoardLayout.CellToWorld(col, row);
            go.transform.localScale = Vector3.one * 0.94f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Cell();
            Color tint = ThemeRuntime.Color("accent.primary");
            sr.sortingOrder = 85;

            float t = 0f;
            float life = ThemeRuntime.Seconds("t.fast");
            while (t < life)
            {
                t += Time.deltaTime;
                float pulse = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / life));
                sr.color = new Color(tint.r, tint.g, tint.b, 0.35f * pulse);
                yield return null;
            }

            Destroy(go);
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
                // Spec §4.3: release elsewhere → the piece flies back to its slot
                // over t.base, no penalty.
                StartCoroutine(FlyBack(dragSlot));
                return;
            }

            TearDownDrag();
        }

        private IEnumerator FlyBack(int slot)
        {
            flyingBack = true;
            dragging = false;
            Vector3 target = BoardLayout.TraySlotCenter(slot);
            var starts = new List<Vector3>(carrySprites.Count);
            Vector3 centroid = Vector3.zero;
            foreach (SpriteRenderer sr in carrySprites)
            {
                starts.Add(sr.transform.position);
                centroid += sr.transform.position;
            }

            centroid /= Mathf.Max(1, carrySprites.Count);
            foreach (SpriteRenderer sr in ghostSprites)
            {
                sr.enabled = false;
            }

            float t = 0f;
            float life = Mathf.Max(0.01f, ThemeRuntime.Seconds("t.base"));
            while (t < life)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / life);
                float eased = 1f - (1f - u) * (1f - u);
                Vector3 delta = Vector3.Lerp(Vector3.zero, target - centroid, eased);
                float scale = Mathf.Lerp(CarryScale, TrayMiniScale, eased);
                for (int i = 0; i < carrySprites.Count; i++)
                {
                    carrySprites[i].transform.position = starts[i] + delta;
                    carrySprites[i].transform.localScale = Vector3.one * (0.94f * scale);
                }

                yield return null;
            }

            tray.SetSlotGhost(slot, false);
            flyingBack = false;
            TearDownDrag();
        }

        /// <summary>TrayView's mini silhouette scale — the fly-back shrinks into it.</summary>
        private const float TrayMiniScale = TrayView.MiniScale;

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
                // Spec §4.3: lifted pieces scale to 1.08.
                carry.transform.localScale = Vector3.one * (0.94f * CarryScale);
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
