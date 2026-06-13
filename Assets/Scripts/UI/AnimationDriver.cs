using System;
using System.Collections;
using System.Collections.Generic;
using Riptide.Core;
using Riptide.Game;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// Animates exclusively from MoveEvents (GDD 8.2: views never re-derive rules)
    /// through a UiEventQueue (spec §6.1): place → clear (budgeted 30ms stagger) →
    /// rescue → drain → rise (with bottom-up petrify sweep + ring flash) — then
    /// renders the truth from the new state. Beat names are juice-table keys;
    /// JuiceDirector and tests subscribe to BeatStarted.
    /// InstantMode (tests) skips straight to the truth render.
    /// </summary>
    public sealed class AnimationDriver : MonoBehaviour
    {
        private GameStore store = null!;
        private BoardView board = null!;
        private WaterView water = null!;
        private TrayView tray = null!;
        private TideMeterRing meter = null!;
        private BoardChromeView chrome = null!;
        private GameState lastState = null!;
        private UiEventQueue? activeQueue;

        public bool InstantMode { get; set; }

        public bool IsAnimating => activeQueue != null && activeQueue.IsResolving;

        /// <summary>(beatName, result) for every beat as it starts — juice + tests.</summary>
        public event Action<string, MoveResult>? BeatStarted;

        /// <summary>Planned blocking seconds of the last move's queue (budget tests).</summary>
        public float LastPlannedSeconds { get; private set; }

        /// <summary>Beat names of the last move's queue in play order (ordering tests).</summary>
        public IReadOnlyList<string> LastBeats { get; private set; } = Array.Empty<string>();

        public static AnimationDriver Create(Transform parent, GameStore store, BoardView board,
            WaterView water, TrayView tray, TideMeterRing meter, BoardChromeView chrome)
        {
            var go = new GameObject("AnimationDriver");
            go.transform.SetParent(parent, false);
            var driver = go.AddComponent<AnimationDriver>();
            driver.store = store;
            driver.board = board;
            driver.water = water;
            driver.tray = tray;
            driver.meter = meter;
            driver.chrome = chrome;
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
            water.SetDanger(DangerRule.IsDanger(state.WaterLevel));
            tray.Render(state);
            meter.Render(state);
            chrome.Render(state);
            lastState = state;
        }

        private void OnGameReset(GameState state)
        {
            StopAllCoroutines();
            activeQueue = null;
            RenderAll(state);
        }

        /// <summary>Tests inject synthetic results to exercise the queue without a sim move.</summary>
        public void DriveForTest(Move move, MoveResult result) => OnMoveApplied(move, result);

        private void OnMoveApplied(Move move, MoveResult result)
        {
            if (InstantMode)
            {
                RenderAll(result.Next);
                return;
            }

            UiEventQueue queue = BuildQueue(move, result);
            activeQueue = queue;
            LastBeats = queue.PlannedBeats;
            queue.BeatStarted += name => BeatStarted?.Invoke(name, result);
            StartCoroutine(PlayQueue(queue, result));
        }

        private IEnumerator PlayQueue(UiEventQueue queue, MoveResult result)
        {
            yield return queue.Play();
            RenderAll(result.Next);
            if (activeQueue == queue)
            {
                activeQueue = null;
            }
        }

        private UiEventQueue BuildQueue(Move move, MoveResult result)
        {
            MoveEvents events = result.Events;
            GameState before = lastState;
            var queue = new UiEventQueue();

            bool drains = events.DrainAmount > 0;
            bool multi = events.DrainAmount > 1;
            bool rises = events.TideRose;
            int clearCells = events.RowsCleared.Count * BoardSpec.Width;
            float stagger = UiEventQueue.ClearStagger(clearCells, drains, multi, rises);
            // The clear beat BLOCKS for at most its budget share; pop visuals may
            // bleed into the drain beat (§1.4 overlap) so the lock window holds.
            float clearSeconds = events.RowsCleared.Count > 0
                ? Mathf.Min(clearCells * stagger + 0.12f, UiEventQueue.ClearBudgetSeconds(drains, multi, rises))
                : 0f;
            LastPlannedSeconds = clearSeconds
                + (drains ? UiEventQueue.DrainSeconds(multi) : 0f)
                + (rises ? UiEventQueue.RiseSeconds() : 0f);

            // 1) Commit: placed cells appear immediately (snap ≤ 80ms, spec §4.3).
            queue.AddInstant("place");
            byte colorId = 0;
            if (move is PlaceMove place)
            {
                TrayPiece? piece = before.TrayAt(place.TraySlot);
                colorId = piece?.ColorId ?? 0;
            }

            ShowPlacedCells(events, move, colorId);

            // 2) Clear pop with the budgeted stagger; combo flourish rides along.
            if (events.RowsCleared.Count > 0)
            {
                if (events.Scoring.ComboHalves >= 3)
                {
                    queue.AddInstant("deepClear");
                }

                float clearBlock = clearSeconds;
                queue.Add("clear", () => ClearBeat(events, stagger, clearBlock));
            }

            // 3) Rescues swim off without blocking the queue.
            if (events.RescuedCreatures.Count > 0)
            {
                queue.AddInstant("rescue");
            }

            // 4) Drain — the over-delivered relief beat (§5.1).
            if (drains)
            {
                queue.Add("drain", () => water.AnimateDrain(before.WaterLevel - events.DrainAmount, events.DrainAmount));
            }

            // 5) Rise: lost creatures fade, blocks petrify bottom-up, ring flashes, water surges.
            if (rises)
            {
                if (events.LostCreatures.Count > 0)
                {
                    queue.AddInstant("lost");
                }

                queue.Add("rise", () => RiseBeat(events, result));
            }

            // 6) Terminal beats (juice only; screens react via flow).
            if (result.Next.Status == GameStatus.LostDrowned)
            {
                queue.AddInstant("drown");
            }
            else if (result.Next.Status == GameStatus.Won)
            {
                queue.AddInstant("star");
            }

            return queue;
        }

        private void ShowPlacedCells(MoveEvents events, Move move, byte colorId)
        {
            if (move is PlaceMove place)
            {
                tray.SetSlotVisible(place.TraySlot, false);
            }

            foreach (GridPos pos in events.PlacedCells)
            {
                SpriteRenderer sr = board.RendererAt(pos.Col, pos.Row);
                sr.sprite = SpriteFactory.Cell();
                sr.color = Palette.BlockColor(colorId);

                // Juice (research: amplified, eased feedback per input): a tiny
                // settle-pop on every placed cell — the thunk made visible.
                if (!InstantMode)
                {
                    AddSettle(sr);
                }
            }
        }

        // Audit C5: placement is STEADY-STATE, so the settle pop runs on a pooled
        // ticker instead of a coroutine per cell (§9 zero-alloc-per-move budget).
        private struct Settle
        {
            public SpriteRenderer Renderer;
            public float T;
        }

        private readonly Settle[] settles = new Settle[32];
        private int settleCount;

        private void AddSettle(SpriteRenderer sr)
        {
            if (settleCount < settles.Length)
            {
                settles[settleCount].Renderer = sr;
                settles[settleCount].T = 0f;
                settleCount++;
            }
        }

        private void Update()
        {
            if (settleCount == 0)
            {
                return;
            }

            float life = Mathf.Max(0.01f, ThemeRuntime.MotionSeconds("t.instant") * 2f);
            Vector3 baseScale = Vector3.one * 0.94f;
            for (int i = settleCount - 1; i >= 0; i--)
            {
                settles[i].T += Time.deltaTime;
                SpriteRenderer sr = settles[i].Renderer;
                float u = Mathf.Clamp01(settles[i].T / life);
                if (sr != null)
                {
                    float ease = 1f - (1f - u) * (1f - u);
                    sr.transform.localScale = baseScale * Mathf.Lerp(1.16f, 1f, ease);
                }

                if (u >= 1f)
                {
                    settles[i] = settles[settleCount - 1];
                    settleCount--;
                }
            }
        }

        private IEnumerator ClearBeat(MoveEvents events, float stagger, float blockSeconds)
        {
            int cellIndex = 0;
            foreach (int row in events.RowsCleared)
            {
                for (int col = 0; col < BoardSpec.Width; col++)
                {
                    StartCoroutine(PopCell(board.RendererAt(col, row), cellIndex * stagger));
                    cellIndex++;
                }
            }

            foreach (CreatureEvent rescue in events.RescuedCreatures)
            {
                StartCoroutine(SwimOff(rescue));
            }

            if (events.Scoring.ComboHalves >= 3)
            {
                StartCoroutine(ComboEdgeGlow());
            }

            yield return new WaitForSeconds(blockSeconds);
        }

        private IEnumerator RiseBeat(MoveEvents events, MoveResult result)
        {
            StartCoroutine(PetrifySweep(events.PetrifiedCells));
            foreach (CreatureEvent lost in events.LostCreatures)
            {
                StartCoroutine(FadeLost(lost));
            }

            meter.PlayRiseFlash();
            water.SetDanger(DangerRule.IsDanger(result.Next.WaterLevel));
            yield return water.AnimateRise(result.Next.WaterLevel);
        }

        /// <summary>§5.1: petrify desaturation sweeps bottom-up across 200ms.</summary>
        private IEnumerator PetrifySweep(IReadOnlyList<GridPos> cells)
        {
            if (cells.Count == 0)
            {
                yield break;
            }

            int minRow = int.MaxValue;
            int maxRow = int.MinValue;
            foreach (GridPos pos in cells)
            {
                minRow = Mathf.Min(minRow, pos.Row);
                maxRow = Mathf.Max(maxRow, pos.Row);
            }

            const float sweep = 0.2f;
            float rowSpan = Mathf.Max(1, maxRow - minRow + 1);
            foreach (GridPos pos in cells)
            {
                float delay = sweep * (pos.Row - minRow) / rowSpan;
                StartCoroutine(PetrifyCell(board.RendererAt(pos.Col, pos.Row), delay));
            }

            yield return new WaitForSeconds(sweep);
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
            sr.sprite = CreatureSprites.For(rescue.CreatureId);
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
            sr.sprite = CreatureSprites.For(lost.CreatureId);
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

        /// <summary>GDD 7.3 / juice "deepClear": combo = screen-edge glow pulse.</summary>
        private IEnumerator ComboEdgeGlow()
        {
            var go = new GameObject("comboGlow");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            Color glow = ThemeRuntime.Color("glow.primary");
            sr.sprite = SpriteFactory.Cell();
            sr.color = new Color(glow.r, glow.g, glow.b, 0f);
            sr.sortingOrder = 95;
            go.transform.position = Vector3.zero;
            go.transform.localScale = new Vector3(26f, 26f, 1f);

            float t = 0f;
            const float life = 0.4f;
            while (t < life)
            {
                t += Time.deltaTime;
                float pulse = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / life));
                sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, glow.a * 0.5f * pulse);
                yield return null;
            }

            Destroy(go);
        }

        private IEnumerator PetrifyCell(SpriteRenderer sr, float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            Color from = sr.color;
            float t = 0f;
            const float life = 0.12f;
            while (t < life)
            {
                t += Time.deltaTime;
                sr.color = Color.Lerp(from, Palette.Coral, Mathf.Clamp01(t / life));
                yield return null;
            }
        }
    }
}
