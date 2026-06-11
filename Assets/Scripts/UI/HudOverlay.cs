using System.Text;
using Riptide.Core;
using Riptide.Game;
using UnityEngine;
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

        private void OnReset(GameState state) => RefreshFromState();

        private void RefreshFromState()
        {
            if (flow.Store == null)
            {
                return;
            }

            GameState state = flow.Store.State;
            score.text = ShareCard.GroupThousands(state.Score);

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
