using System.Text;
using Riptide.Core;
using Riptide.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// GDD 9 in-game HUD: goal chips top-left, score top-right, menu button.
    /// Renders from store state on every applied move; never re-derives rules.
    /// </summary>
    public sealed class HudOverlay : MonoBehaviour
    {
        private GameFlow flow = null!;
        private Text goals = null!;
        private Text score = null!;
        private Text coins = null!;
        private Text popHint = null!;
        private Text milestoneLabel = null!;
        private Button drainButton = null!;
        private Button popButton = null!;
        private Button rerollButton = null!;
        private Button swapButton = null!;
        private Button freeDrain = null!;
        private Button freeReroll = null!;
        private bool popArmed;
        private bool swapArmed;
        private bool pointerWasDown;
        private float milestoneUntil;

        // Genre pass (spec §12.4): praise flash, combo chip, endless best chip.
        private Text praise = null!;
        private Text comboChip = null!;
        private Text bestChip = null!;
        private float praiseUntil;
        private int lastComboChain;
        private bool bestOvertaken;

        // Booster rail world geometry (gate feedback: the rail must never cover
        // the board) — a reserved band BELOW the tray card; CameraFit holds the
        // theme's boosterRailBandRefPx open for it on every device.
        private const float RailRowY = -9.55f;
        private const float ChipRowY = -8.85f;
        private const float DrainX = -3.45f;
        private const float PopX = -1.15f;
        private const float RerollX = 1.15f;
        private const float SwapX = 3.45f;

        private static void PinRail(Button button, Vector2 sizeRefPx, float worldX, float worldY)
        {
            var rt = (RectTransform)button.transform;
            rt.sizeDelta = sizeRefPx;
            WorldAnchor.Pin(rt, new Vector3(worldX, worldY, 0f));
        }

        public static HudOverlay Create(RectTransform canvasRoot, GameFlow flow, System.Action? onPause = null)
        {
            RectTransform root = UiKit.Container(canvasRoot, "HudOverlay");
            UiKit.Stretch(root);
            var hud = root.gameObject.AddComponent<HudOverlay>();
            hud.flow = flow;

            // Universal fit: the top bar lives inside the safe area (notches /
            // punch-holes); world-tracking elements (booster rail, milestone) pin
            // to world positions in full-screen space instead — the camera fit
            // already keeps those out of unsafe territory.
            RectTransform safe = UiKit.Container(root, "safe");
            UiKit.Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();

            // Spec §4.3 top bar: goal chips LEFT, score CENTER, pause/menu RIGHT.
            hud.goals = UiKit.Label(safe, "goals", "", 36, UiKit.TextColor, TextAnchor.UpperLeft);
            UiKit.Place(hud.goals.rectTransform, new Vector2(0.05f, 0.97f), new Vector2(560f, 220f), new Vector2(280f, -110f));

            hud.score = UiKit.Label(safe, "score", "", 44, UiKit.TextColor, TextAnchor.UpperCenter);
            UiKit.Place(hud.score.rectTransform, new Vector2(0.5f, 0.97f), new Vector2(400f, 80f), new Vector2(0f, -40f));

            // Spec §4.3: the top-right slot is the pause control; without a pause
            // sheet wired (CreateGame test rigs) it falls back to Home.
            Button menu = UiKit.TextButton(safe, "menu", flow.Strings.Get("hud.back"), 30,
                () =>
                {
                    if (onPause != null)
                    {
                        onPause();
                    }
                    else
                    {
                        flow.GoTo(FlowScreen.Home);
                    }
                });
            UiKit.Place((RectTransform)menu.transform, new Vector2(0.92f, 0.97f), new Vector2(170f, 64f), new Vector2(0f, -32f));

            // Coins live by the rail — that's where they're spent (gate feedback:
            // the old 0.925-height label landed inside the board frame).
            hud.coins = UiKit.Label(root, "coins", "", 34, ThemeRuntime.Color("coin"), TextAnchor.MiddleCenter);
            hud.coins.rectTransform.sizeDelta = new Vector2(280f, 54f);
            WorldAnchor.Pin(hud.coins.rectTransform, new Vector3(SwapX, ChipRowY, 0f));

            // Booster rail: one row in the reserved band BELOW the tray (gate
            // feedback — the old pins covered the board's right edge).
            hud.drainButton = UiKit.TextButton(root, "drain", "", 28, () => hud.UseSimpleBooster(BoosterKind.DrainPump));
            PinRail(hud.drainButton, new Vector2(230f, 80f), DrainX, RailRowY);
            hud.popButton = UiKit.TextButton(root, "pop", "", 28, hud.TogglePopMode);
            PinRail(hud.popButton, new Vector2(230f, 80f), PopX, RailRowY);
            hud.rerollButton = UiKit.TextButton(root, "reroll", "", 28, () => hud.UseSimpleBooster(BoosterKind.NewTide));
            PinRail(hud.rerollButton, new Vector2(230f, 80f), RerollX, RailRowY);
            hud.swapButton = UiKit.TextButton(root, "swap", "", 28, hud.ToggleSwapMode);
            PinRail(hud.swapButton, new Vector2(230f, 80f), SwapX, RailRowY);

            // ROADMAP M4: endless milestone pop — over the board's lower half.
            hud.milestoneLabel = UiKit.Label(root, "milestone", "", 34, Palette.MeterFilled);
            hud.milestoneLabel.rectTransform.sizeDelta = new Vector2(800f, 60f);
            WorldAnchor.Pin(hud.milestoneLabel.rectTransform, new Vector3(0f, -3.5f, 0f));
            hud.milestoneLabel.gameObject.SetActive(false);
            flow.MilestoneReached += hud.OnMilestone;

            // GDD 5.3: one free Drain Pump and one free New Tide per game via
            // rewarded ad — mini chips directly above their boosters.
            hud.freeDrain = UiKit.TextButton(root, "freeDrain", "▶ ad", 24,
                () => { flow.TryFreeBoosterViaAd(BoosterKind.DrainPump); hud.RefreshFromState(); });
            PinRail(hud.freeDrain, new Vector2(90f, 60f), DrainX, ChipRowY);
            hud.freeReroll = UiKit.TextButton(root, "freeReroll", "▶ ad", 24,
                () => { flow.TryFreeBoosterViaAd(BoosterKind.NewTide); hud.RefreshFromState(); });
            PinRail(hud.freeReroll, new Vector2(90f, 60f), RerollX, ChipRowY);

            // Armed-booster instruction floats over the board's top rows —
            // contextual, transient, and out of the (full) top bar.
            hud.popHint = UiKit.Label(root, "popHint", flow.Strings.Get("booster.popHint"), 32, Palette.MeterDanger);
            hud.popHint.rectTransform.sizeDelta = new Vector2(800f, 60f);
            WorldAnchor.Pin(hud.popHint.rectTransform, new Vector3(0f, 5.4f, 0f));
            hud.popHint.gameObject.SetActive(false);

            // Genre pass (spec §12.4, research: Block Blast's praise loop) — type
            // only, accent-cyan, over the board's upper half; no particle bursts
            // (anti-goals hold). Shown for multi-row clears.
            hud.praise = UiKit.Label(root, "praise", "", 64, ThemeRuntime.Color("accent.primary"));
            hud.praise.fontStyle = FontStyle.Bold;
            hud.praise.rectTransform.sizeDelta = new Vector2(900f, 120f);
            WorldAnchor.Pin(hud.praise.rectTransform, new Vector3(0f, 2.4f, 0f));
            hud.praise.gameObject.SetActive(false);

            // Visible combo multiplier beside the score (inside the top band —
            // gate feedback: nothing static may sit over the board).
            hud.comboChip = UiKit.Label(safe, "combo", "", 30, ThemeRuntime.Color("accent.primary"));
            UiKit.Place(hud.comboChip.rectTransform, new Vector2(0.5f, 0.97f), new Vector2(220f, 46f), new Vector2(260f, -40f));
            hud.comboChip.gameObject.SetActive(false);

            // Endless: the personal best is the run's target (the genre's crown).
            hud.bestChip = UiKit.Label(safe, "best", "", 28, UiKit.TextDim, TextAnchor.UpperLeft);
            UiKit.Place(hud.bestChip.rectTransform, new Vector2(0.05f, 0.97f), new Vector2(560f, 60f), new Vector2(280f, -30f));
            hud.bestChip.gameObject.SetActive(false);

            if (flow.Store != null)
            {
                flow.Store.MoveApplied += hud.OnMove;
                flow.Store.GameReset += hud.OnReset;
            }

            flow.RunStarted += hud.RefreshFromState;
            hud.RefreshFromState();
            return hud;
        }

        private void OnDestroy()
        {
            if (flow?.Store != null)
            {
                flow.Store.MoveApplied -= OnMove;
                flow.Store.GameReset -= OnReset;
            }

            if (flow != null)
            {
                flow.RunStarted -= RefreshFromState;
                flow.MilestoneReached -= OnMilestone;
            }
        }

        private void OnMilestone(int tides)
        {
            milestoneLabel.text = string.Format(flow.Strings.Get("hud.milestone"),
                tides, flow.Economy.Coins.EndlessMilestoneCoins);
            milestoneLabel.gameObject.SetActive(true);
            milestoneUntil = Time.realtimeSinceStartup + 2.4f;
            UiJuice.Play("streak");
        }

        private void ToggleSwapMode()
        {
            if (!flow.CanUseBooster(BoosterKind.PieceSwap))
            {
                return;
            }

            swapArmed = !swapArmed;
            popArmed = false;
            popHint.text = flow.Strings.Get(swapArmed ? "booster.swapHint" : "booster.popHint");
            popHint.gameObject.SetActive(swapArmed);
        }

        private void OnMove(Move move, MoveResult result)
        {
            int rows = result.Events.RowsCleared.Count;
            if (rows >= 2)
            {
                ShowPraise(rows);
            }

            RefreshFromState();
        }

        /// <summary>Spec §12.4 praise beat: scale-in (t.base), hold, then hide.
        /// Public as test surface (PlayMode asserts copy + activation).</summary>
        public void ShowPraise(int rows)
        {
            praise.text = flow.Strings.Get(rows == 2 ? "praise.double"
                : rows == 3 ? "praise.triple" : "praise.quad");
            praise.gameObject.SetActive(true);
            praiseUntil = Time.realtimeSinceStartup + 1.1f;
            RectTransform rt = praise.rectTransform;
            rt.localScale = Vector3.one * 0.7f;
            Tween.Run(this, "t.base", "easeOutQuart",
                u => rt.localScale = Vector3.one * (0.7f + 0.3f * u),
                () => rt.localScale = Vector3.one);
        }

        private void OnReset(GameState state)
        {
            popArmed = false;
            swapArmed = false;
            popHint.gameObject.SetActive(false);
            praise.gameObject.SetActive(false);
            lastComboChain = 0;
            bestOvertaken = false;
            RefreshFromState();
        }

        private void UseSimpleBooster(BoosterKind kind)
        {
            popArmed = false;
            swapArmed = false;
            popHint.gameObject.SetActive(false);
            flow.TryUseBooster(kind);
            RefreshFromState();
        }

        private void TogglePopMode()
        {
            if (!flow.CanUseBooster(BoosterKind.BubblePop))
            {
                return;
            }

            popArmed = !popArmed;
            swapArmed = false;
            popHint.text = flow.Strings.Get("booster.popHint");
            popHint.gameObject.SetActive(popArmed);
        }

        private void Update()
        {
            if (milestoneLabel.gameObject.activeSelf && Time.realtimeSinceStartup > milestoneUntil)
            {
                milestoneLabel.gameObject.SetActive(false);
            }

            if (praise.gameObject.activeSelf && Time.realtimeSinceStartup > praiseUntil)
            {
                praise.gameObject.SetActive(false);
            }

            if (!popArmed && !swapArmed)
            {
                return;
            }

            Pointer? pointer = Pointer.current;
            if (pointer == null)
            {
                return;
            }

            bool down = pointer.press.isPressed;
            if (down && !pointerWasDown)
            {
                Camera cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
                Vector2 screen = pointer.position.ReadValue();
                Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
                if (popArmed && BoardLayout.TrySnap(world, 0.5f, out int col, out int row))
                {
                    if (flow.TryUseBooster(BoosterKind.BubblePop, new GridPos(col, row)))
                    {
                        popArmed = false;
                        popHint.gameObject.SetActive(false);
                        RefreshFromState();
                    }
                }
                else if (swapArmed)
                {
                    // ROADMAP M3: armed-tap a tray slot to swap that piece.
                    for (int slot = 0; slot < BoardSpec.TraySize; slot++)
                    {
                        if (Vector2.Distance(world, BoardLayout.TraySlotCenter(slot)) <= 1.1f)
                        {
                            if (flow.TryUseBooster(BoosterKind.PieceSwap, new GridPos(slot, 0)))
                            {
                                swapArmed = false;
                                popHint.gameObject.SetActive(false);
                                RefreshFromState();
                            }

                            break;
                        }
                    }
                }
            }

            pointerWasDown = down;
        }

        private void RefreshFromState()
        {
            if (flow.Store == null)
            {
                return;
            }

            GameState state = flow.Store.State;
            score.text = ShareCard.GroupThousands(state.Score);
            coins.text = string.Format(flow.Strings.Get("hud.coins"), ShareCard.GroupThousands(flow.Meta.Coins));

            RefreshComboChip(state);
            RefreshBestChip(state);

            bool boosters = state.Config.BoostersAllowed && !state.Status.IsTerminal();
            drainButton.gameObject.SetActive(boosters);
            popButton.gameObject.SetActive(boosters);
            rerollButton.gameObject.SetActive(boosters);
            swapButton.gameObject.SetActive(boosters);
            if (boosters)
            {
                drainButton.GetComponentInChildren<Text>().text =
                    string.Format(flow.Strings.Get("booster.drainPump"), flow.BoosterCost(BoosterKind.DrainPump));
                popButton.GetComponentInChildren<Text>().text =
                    string.Format(flow.Strings.Get("booster.bubblePop"), flow.BoosterCost(BoosterKind.BubblePop));
                rerollButton.GetComponentInChildren<Text>().text =
                    string.Format(flow.Strings.Get("booster.newTide"), flow.BoosterCost(BoosterKind.NewTide));
                swapButton.GetComponentInChildren<Text>().text =
                    string.Format(flow.Strings.Get("booster.pieceSwap"), flow.BoosterCost(BoosterKind.PieceSwap));
                drainButton.interactable = flow.CanUseBooster(BoosterKind.DrainPump);
                popButton.interactable = flow.CanUseBooster(BoosterKind.BubblePop);
                rerollButton.interactable = flow.CanUseBooster(BoosterKind.NewTide);
                swapButton.interactable = flow.CanUseBooster(BoosterKind.PieceSwap);
            }

            freeDrain.gameObject.SetActive(boosters && flow.FreeBoosterAvailable(BoosterKind.DrainPump));
            freeReroll.gameObject.SetActive(boosters && flow.FreeBoosterAvailable(BoosterKind.NewTide));

            var sb = new StringBuilder();
            GoalSet goalSet = state.Config.Goals;
            if (goalSet.RescueAllTarget.HasValue)
            {
                sb.AppendLine(string.Format(flow.Strings.Get("hud.goal.rescue"),
                    state.Goals.Rescued, goalSet.RescueAllTarget.Value));
            }

            if (goalSet.ClearRowsTarget.HasValue)
            {
                sb.AppendLine(string.Format(flow.Strings.Get("hud.goal.rows"),
                    state.Goals.RowsCleared, goalSet.ClearRowsTarget.Value));
            }

            if (goalSet.SurviveTidesTarget.HasValue)
            {
                sb.AppendLine(string.Format(flow.Strings.Get("hud.goal.tides"),
                    state.Goals.TidesSurvived, goalSet.SurviveTidesTarget.Value));
            }

            if (goalSet.ScoreTarget.HasValue)
            {
                sb.AppendLine(string.Format(flow.Strings.Get("hud.goal.score"),
                    ShareCard.GroupThousands(goalSet.ScoreTarget.Value)));
            }

            goals.text = sb.ToString();
        }

        /// <summary>Spec §12.4: the chain multiplier is visible, not just felt.</summary>
        private void RefreshComboChip(GameState state)
        {
            int chain = state.ComboChain;
            bool show = chain >= 2 && !state.Status.IsTerminal();
            comboChip.gameObject.SetActive(show);
            if (show)
            {
                ScoringConfig sc = state.Config.Scoring;
                int halves = System.Math.Min(
                    sc.ComboStartHalves + (chain - 1) * sc.ComboStepHalves, sc.ComboCapHalves);
                comboChip.text = string.Format(flow.Strings.Get("hud.combo"), FormatHalves(halves));
                if (chain > lastComboChain)
                {
                    Pulse(comboChip.rectTransform);
                }
            }

            lastComboChain = chain;
        }

        /// <summary>Endless personal best in-run; flips to coin color the moment it's beaten.</summary>
        private void RefreshBestChip(GameState state)
        {
            bool endless = flow.Mode == GameMode.Endless && flow.Meta.EndlessBest > 0;
            bestChip.gameObject.SetActive(endless);
            if (!endless)
            {
                return;
            }

            long shown = System.Math.Max(flow.Meta.EndlessBest, state.Score);
            bestChip.text = string.Format(flow.Strings.Get("hud.best"), ShareCard.GroupThousands(shown));
            if (state.Score > flow.Meta.EndlessBest && !bestOvertaken)
            {
                bestOvertaken = true;
                bestChip.color = ThemeRuntime.Color("coin");
                Pulse(bestChip.rectTransform);
            }
            else if (!bestOvertaken)
            {
                bestChip.color = UiKit.TextDim;
            }
        }

        /// <summary>"3 halves" → "1.5" (invariant culture). Public as test surface.</summary>
        public static string FormatHalves(int halves) =>
            halves % 2 == 0
                ? (halves / 2).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : (halves / 2f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

        private void Pulse(RectTransform target)
        {
            Tween.Run(this, "t.fast", "linear",
                u => target.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(u * Mathf.PI)),
                () => target.localScale = Vector3.one);
        }
    }
}
