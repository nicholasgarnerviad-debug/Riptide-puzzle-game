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
        private Button drainButton = null!;
        private Button popButton = null!;
        private Button rerollButton = null!;
        private bool popArmed;
        private bool pointerWasDown;

        public static HudOverlay Create(RectTransform canvasRoot, GameFlow flow)
        {
            RectTransform root = UiKit.Container(canvasRoot, "HudOverlay");
            UiKit.Stretch(root);
            var hud = root.gameObject.AddComponent<HudOverlay>();
            hud.flow = flow;

            hud.goals = UiKit.Label(root, "goals", "", 36, UiKit.TextColor, TextAnchor.UpperLeft);
            UiKit.Place(hud.goals.rectTransform, new Vector2(0.05f, 0.97f), new Vector2(560f, 220f), new Vector2(280f, -110f));

            hud.score = UiKit.Label(root, "score", "", 44, UiKit.TextColor, TextAnchor.UpperRight);
            UiKit.Place(hud.score.rectTransform, new Vector2(0.95f, 0.97f), new Vector2(400f, 80f), new Vector2(-200f, -40f));

            Button menu = UiKit.TextButton(root, "menu", flow.Strings.Get("hud.back"), 30,
                () => flow.GoTo(FlowScreen.Home));
            UiKit.Place((RectTransform)menu.transform, new Vector2(0.5f, 0.975f), new Vector2(170f, 64f), Vector2.zero);

            hud.coins = UiKit.Label(root, "coins", "", 38, Palette.Blocks[3], TextAnchor.UpperRight);
            UiKit.Place(hud.coins.rectTransform, new Vector2(0.95f, 0.925f), new Vector2(400f, 60f), new Vector2(-200f, -30f));

            // GDD 9: booster rail bottom-right.
            hud.drainButton = UiKit.TextButton(root, "drain", "", 28, () => hud.UseSimpleBooster(BoosterKind.DrainPump));
            UiKit.Place((RectTransform)hud.drainButton.transform, new Vector2(0.86f, 0.205f), new Vector2(250f, 80f), Vector2.zero);
            hud.popButton = UiKit.TextButton(root, "pop", "", 28, hud.TogglePopMode);
            UiKit.Place((RectTransform)hud.popButton.transform, new Vector2(0.86f, 0.150f), new Vector2(250f, 80f), Vector2.zero);
            hud.rerollButton = UiKit.TextButton(root, "reroll", "", 28, () => hud.UseSimpleBooster(BoosterKind.NewTide));
            UiKit.Place((RectTransform)hud.rerollButton.transform, new Vector2(0.86f, 0.095f), new Vector2(250f, 80f), Vector2.zero);

            hud.popHint = UiKit.Label(root, "popHint", flow.Strings.Get("booster.popHint"), 32, Palette.MeterDanger);
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
            }
        }

        private void OnMove(Move move, MoveResult result) => RefreshFromState();

        private void OnReset(GameState state)
        {
            popArmed = false;
            popHint.gameObject.SetActive(false);
            RefreshFromState();
        }

        private void UseSimpleBooster(BoosterKind kind)
        {
            popArmed = false;
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
            popHint.gameObject.SetActive(popArmed);
        }

        private void Update()
        {
            if (!popArmed)
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
                if (BoardLayout.TrySnap(world, 0.5f, out int col, out int row))
                {
                    if (flow.TryUseBooster(BoosterKind.BubblePop, new GridPos(col, row)))
                    {
                        popArmed = false;
                        popHint.gameObject.SetActive(false);
                        RefreshFromState();
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
            if (boosters)
            {
                drainButton.GetComponentInChildren<Text>().text =
                    string.Format(flow.Strings.Get("booster.drainPump"), flow.BoosterCost(BoosterKind.DrainPump));
                popButton.GetComponentInChildren<Text>().text =
                    string.Format(flow.Strings.Get("booster.bubblePop"), flow.BoosterCost(BoosterKind.BubblePop));
                rerollButton.GetComponentInChildren<Text>().text =
                    string.Format(flow.Strings.Get("booster.newTide"), flow.BoosterCost(BoosterKind.NewTide));
                drainButton.interactable = flow.CanUseBooster(BoosterKind.DrainPump);
                popButton.interactable = flow.CanUseBooster(BoosterKind.BubblePop);
                rerollButton.interactable = flow.CanUseBooster(BoosterKind.NewTide);
            }

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
