using System.Collections;
using Riptide.Core;
using Riptide.Game;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// Animates exclusively from MoveEvents (GDD 8.2: views never re-derive rules)
    /// in §2.6 order: place, clear pop (30ms/cell stagger), rescue swim-off, drain
    /// recede, petrify, rise surge — then renders the truth from the new state.
    /// InstantMode (tests) skips straight to the truth render.
    /// </summary>
    public sealed class AnimationDriver : MonoBehaviour
    {
        private GameStore store = null!;
        private BoardView board = null!;
        private WaterView water = null!;
        private TrayView tray = null!;
        private TideMeterView meter = null!;
        private GameState lastState = null!;

        public bool InstantMode { get; set; }

        public bool IsAnimating { get; private set; }

        public static AnimationDriver Create(Transform parent, GameStore store, BoardView board,
            WaterView water, TrayView tray, TideMeterView meter)
        {
            var go = new GameObject("AnimationDriver");
            go.transform.SetParent(parent, false);
            var driver = go.AddComponent<AnimationDriver>();
            driver.store = store;
            driver.board = board;
            driver.water = water;
            driver.tray = tray;
            driver.meter = meter;
            driver.lastState = store.State;
            store.MoveApplied += driver.OnMoveApplied;
            store.GameReset += driver.OnGameReset;
            return driver;
        }

        private void OnDestroy()
        {
            if (store != null)
            {
                store.MoveApplied -= OnMoveApplied;
                store.GameReset -= OnGameReset;
            }
        }

        public void RenderAll(GameState state)
        {
            board.Render(state);
            water.SetLevelInstant(state.WaterLevel);
            tray.Render(state);
            meter.Render(state);
            lastState = state;
        }

        private void OnGameReset(GameState state)
        {
            StopAllCoroutines();
            IsAnimating = false;
            RenderAll(state);
        }

        private void OnMoveApplied(Move move, MoveResult result)
        {
            if (InstantMode)
            {
                RenderAll(result.Next);
                return;
            }

            StartCoroutine(PlayMoveSequence(move, result));
        }

        private IEnumerator PlayMoveSequence(Move move, MoveResult result)
        {
            IsAnimating = true;
            MoveEvents events = result.Events;
            GameState before = lastState;

            // 1) Commit: the placed cells appear immediately.
            byte colorId = 0;
            if (move is PlaceMove place)
            {
                TrayPiece? piece = before.TrayAt(place.TraySlot);
                colorId = piece?.ColorId ?? 0;
                tray.SetSlotVisible(place.TraySlot, false);
            }

            foreach (GridPos pos in events.PlacedCells)
            {
                SpriteRenderer sr = board.RendererAt(pos.Col, pos.Row);
                sr.sprite = SpriteFactory.Cell();
                sr.color = Palette.BlockColor(colorId);
            }

            // 2) Clear pop: 30ms per-cell stagger across the cleared rows (GDD 7.3),
            //    with the combo screen-edge glow pulse from x1.5 up.
            if (events.RowsCleared.Count > 0)
            {
                if (events.Scoring.ComboHalves >= 3)
                {
                    StartCoroutine(ComboEdgeGlow());
                }

                float stagger = 0.03f;
                int cellIndex = 0;
                foreach (int row in events.RowsCleared)
                {
                    for (int col = 0; col < BoardSpec.Width; col++)
                    {
                        StartCoroutine(PopCell(board.RendererAt(col, row), cellIndex * stagger));
                        cellIndex++;
                    }
                }

                yield return new WaitForSeconds(cellIndex * stagger + 0.15f);
            }

            // 3) Rescued creatures swim up and off (GDD 7.1).
            foreach (CreatureEvent rescue in events.RescuedCreatures)
            {
                StartCoroutine(SwimOff(rescue));
            }

            // 4) Drain recede — the hero moment (450ms + sparkles).
            if (events.DrainAmount > 0)
            {
                yield return water.AnimateDrain(before.WaterLevel - events.DrainAmount);
            }

            // 5) Petrify tint, then 6) rise surge (350ms ease-in).
            if (events.TideRose)
            {
                foreach (GridPos pos in events.PetrifiedCells)
                {
                    StartCoroutine(PetrifyCell(board.RendererAt(pos.Col, pos.Row)));
                }

                foreach (CreatureEvent lost in events.LostCreatures)
                {
                    StartCoroutine(FadeLost(lost));
                }

                yield return water.AnimateRise(result.Next.WaterLevel);
            }

            // 7) Truth: render everything from the new state.
            RenderAll(result.Next);
            IsAnimating = false;
        }

        private IEnumerator PopCell(SpriteRenderer sr, float delay)
        {
            yield return new WaitForSeconds(delay);
            float t = 0f;
            const float life = 0.12f;
            Vector3 baseScale = Vector3.one * 0.94f;
            while (t < life)
            {
                t += Time.deltaTime;
                float u = 1f - Mathf.Clamp01(t / life);
                sr.transform.localScale = baseScale * (0.2f + 0.8f * u);
                yield return null;
            }

            sr.transform.localScale = baseScale;
            sr.color = Palette.EmptyCell;
            sr.sprite = SpriteFactory.Cell();
        }

        private IEnumerator SwimOff(CreatureEvent rescue)
        {
            var go = new GameObject("rescued");
            go.transform.SetParent(transform, false);
            go.transform.position = BoardLayout.CellToWorld(rescue.Pos.Col, rescue.Pos.Row);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Creature();
            sr.color = Palette.CreatureColor(rescue.CreatureId);
            sr.sortingOrder = 70;

            float t = 0f;
            const float life = 0.7f;
            Vector3 start = go.transform.position;
            while (t < life)
            {
                t += Time.deltaTime;
                float u = t / life;
                go.transform.position = start + new Vector3(Mathf.Sin(u * 9f) * 0.3f, u * 4.5f, 0f);
                Color c = sr.color;
                c.a = 1f - u * u;
                sr.color = c;
                yield return null;
            }

            Destroy(go);
        }

        private IEnumerator FadeLost(CreatureEvent lost)
        {
            var go = new GameObject("lost");
            go.transform.SetParent(transform, false);
            go.transform.position = BoardLayout.CellToWorld(lost.Pos.Col, lost.Pos.Row);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Creature();
            sr.color = Palette.CreatureColor(lost.CreatureId);
            sr.sortingOrder = 70;

            float t = 0f;
            const float life = 0.45f;
            while (t < life)
            {
                t += Time.deltaTime;
                float u = t / life;
                go.transform.position += new Vector3(0f, -Time.deltaTime * 0.8f, 0f);
                Color c = Color.Lerp(sr.color, Palette.Coral, u);
                c.a = 1f - u;
                sr.color = c;
                yield return null;
            }

            Destroy(go);
        }

        /// <summary>GDD 7.3: combo = screen-edge glow pulse.</summary>
        private IEnumerator ComboEdgeGlow()
        {
            var go = new GameObject("comboGlow");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Cell();
            sr.color = new Color(Palette.MeterFilled.r, Palette.MeterFilled.g, Palette.MeterFilled.b, 0f);
            sr.sortingOrder = 95;
            go.transform.position = new Vector3(0f, 0f, 0f);
            go.transform.localScale = new Vector3(26f, 26f, 1f);

            float t = 0f;
            const float life = 0.4f;
            while (t < life)
            {
                t += Time.deltaTime;
                float pulse = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / life));
                sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 0.18f * pulse);
                yield return null;
            }

            Destroy(go);
        }

        private IEnumerator PetrifyCell(SpriteRenderer sr)
        {
            Color from = sr.color;
            float t = 0f;
            const float life = 0.3f;
            while (t < life)
            {
                t += Time.deltaTime;
                sr.color = Color.Lerp(from, Palette.Coral, Mathf.Clamp01(t / life));
                yield return null;
            }
        }
    }
}
