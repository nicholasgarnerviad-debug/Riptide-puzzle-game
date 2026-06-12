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

        /// <summary>Booster rail world X — over the board's right edge (board half-width 4.5).</summary>
        private const float RailX = 3.1f;
        private const float FreeAdX = 4.15f;

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

            hud.coins = UiKit.Label(safe, "coins", "", 38, ThemeRuntime.Color("coin"), TextAnchor.UpperCenter);
            UiKit.Place(hud.coins.rectTransform, new Vector2(0.5f, 0.925f), new Vector2(400f, 60f), new Vector2(0f, -30f));

            // Spec §4.3 item 4: booster rail right-aligned ABOVE the tray strip —
            // world-pinned over the board's right edge so it tracks the camera fit
            // on every aspect (normalized anchors drifted off the board).
            hud.drainButton = UiKit.TextButton(root, "drain", "", 28, () => hud.UseSimpleBooster(BoosterKind.DrainPump));
            PinRail(hud.drainButton, new Vector2(250f, 80f), RailX, -2.1f);
            hud.popButton = UiKit.TextButton(root, "pop", "", 28, hud.TogglePopMode);
            PinRail(hud.popButton, new Vector2(250f, 80f), RailX, -3.0f);
            hud.rerollButton = UiKit.TextButton(root, "reroll", "", 28, () => hud.UseSimpleBooster(BoosterKind.NewTide));
            PinRail(hud.rerollButton, new Vector2(250f, 80f), RailX, -3.9f);
            hud.swapButton = UiKit.TextButton(root, "swap", "", 28, hud.ToggleSwapMode);
            PinRail(hud.swapButton, new Vector2(250f, 80f), RailX, -4.8f);

            // ROADMAP M4: endless milestone pop — over the board's lower half.
            hud.milestoneLabel = UiKit.Label(root, "milestone", "", 34, Palette.MeterFilled);
            hud.milestoneLabel.rectTransform.sizeDelta = new Vector2(800f, 60f);
            WorldAnchor.Pin(hud.milestoneLabel.rectTransform, new Vector3(0f, -3.5f, 0f));
            hud.milestoneLabel.gameObject.SetActive(false);
            flow.MilestoneReached += hud.OnMilestone;

            // GDD 5.3: one free Drain Pump and one free New Tide per game via rewarded ad.
            hud.freeDrain = UiKit.TextButton(root, "freeDrain", "▶ ad", 24,
                () => { flow.TryFreeBoosterViaAd(BoosterKind.DrainPump); hud.RefreshFromState(); });
            PinRail(hud.freeDrain, new Vector2(90f, 80f), FreeAdX, -2.1f);
            hud.freeReroll = UiKit.TextButton(root, "freeReroll", "▶ ad", 24,
                () => { flow.TryFreeBoosterViaAd(BoosterKind.NewTide); hud.RefreshFromState(); });
            PinRail(hud.freeReroll, new Vector2(90f, 80f), FreeAdX, -3.9f);

            hud.popHint = UiKit.Label(safe, "popHint", flow.Strings.Get("booster.popHint"), 32, Palette.MeterDanger);
            UiKit.Place(hud.popHint.rectTransform, new Vector2(0.5f, 0.91f), new Vector2(800f, 60f), Vector2.zero);
            hud.popHint.gameObject.SetActive(false);

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

        private void OnMove(Move move, MoveResult result) => RefreshFromState();

        private void OnReset(GameState state)
        {
            popArmed = false;
            swapArmed = false;
            popHint.gameObject.SetActive(false);
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
    }
}
