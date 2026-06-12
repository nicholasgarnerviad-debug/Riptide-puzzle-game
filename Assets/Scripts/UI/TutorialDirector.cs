using Riptide.Core;
using Riptide.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// GDD 9 tutorial (levels 1–5): event-triggered hints, dismissed by DOING —
    /// never by reading. Every teaching beat logs a tutorial_step funnel event
    /// (target ≥85% reach-L6, GDD 9/14).
    /// </summary>
    public sealed class TutorialDirector : MonoBehaviour
    {
        private GameFlow flow = null!;
        private Text banner = null!;
        private int levelIndex;
        private bool sawPlacement;
        private bool sawClear;
        private bool pumpHintShown;

        // UI spec §6.3: hand-cursor ghost replays the drag after 4s of idle.
        private const float IdleReplaySeconds = 4f;
        private float idleTime;
        private SpriteRenderer? ghostHand;

        public static TutorialDirector Create(RectTransform canvasRoot, GameFlow flow)
        {
            RectTransform root = UiKit.Container(canvasRoot, "TutorialDirector");
            UiKit.Stretch(root);
            var director = root.gameObject.AddComponent<TutorialDirector>();
            director.flow = flow;

            RectTransform panel = UiKit.Panel(root, "banner", new Color(0.05f, 0.10f, 0.16f, 0.92f));
            UiKit.Place(panel, new Vector2(0.5f, 0.845f), new Vector2(940f, 96f), Vector2.zero);
            director.banner = UiKit.Label(panel, "text", "", 36, Palette.MeterFilled);
            UiKit.Stretch(director.banner.rectTransform, 12f);
            panel.gameObject.SetActive(false);

            flow.RunStarted += director.OnRunStarted;
            flow.ScreenChanged += director.OnScreenChanged;
            if (flow.Store != null)
            {
                flow.Store.MoveApplied += director.OnMove;
            }

            director.OnRunStarted();
            return director;
        }

        /// <summary>Gate feedback (huh.png): a hint bled onto the Tidepool — hints
        /// belong to play and hide the moment the player leaves the board.</summary>
        private void OnScreenChanged(FlowScreen screen)
        {
            if (screen != FlowScreen.Playing)
            {
                Hide();
            }
        }

        private void OnDestroy()
        {
            if (flow != null)
            {
                flow.RunStarted -= OnRunStarted;
                flow.ScreenChanged -= OnScreenChanged;
            }

            if (flow?.Store != null)
            {
                flow.Store.MoveApplied -= OnMove;
            }
        }

        private bool InTutorial => flow.Mode == GameMode.Voyage && flow.CurrentLevel != null
            && flow.CurrentLevel.Zone == 1 && levelIndex >= 1 && levelIndex <= 5;

        private void OnRunStarted()
        {
            levelIndex = 0;
            if (flow.Mode == GameMode.Voyage && flow.CurrentLevel != null && flow.CurrentLevel.Zone == 1)
            {
                string id = flow.CurrentLevel.Id;
                int l = id.LastIndexOf('l');
                int.TryParse(id.Substring(l + 1), out levelIndex);
            }

            sawPlacement = false;
            sawClear = false;
            pumpHintShown = false;

            switch (levelIndex)
            {
                case 1:
                    Show("tutorial.l1.drag");
                    flow.Analytics.LogTutorialStep(1);
                    break;
                case 2:
                    Show("tutorial.l2.meter");
                    flow.Analytics.LogTutorialStep(4);
                    break;
                case 3:
                    Show("tutorial.l3.rescue");
                    flow.Analytics.LogTutorialStep(5);
                    break;
                case 4:
                    flow.GrantTutorialDrainPump();
                    flow.Analytics.LogTutorialStep(6);
                    Hide();
                    break;
                case 5:
                    Show("tutorial.l5.go");
                    flow.Analytics.LogTutorialStep(7);
                    break;
                default:
                    Hide();
                    break;
            }
        }

        private void OnMove(Move move, MoveResult result)
        {
            if (!InTutorial)
            {
                return;
            }

            switch (levelIndex)
            {
                case 1:
                    if (!sawPlacement && result.Events.PlacedCells.Count > 0)
                    {
                        sawPlacement = true;
                        flow.Analytics.LogTutorialStep(2);
                        Show("tutorial.l1.clear"); // next beat: fill a row
                    }

                    if (!sawClear && result.Events.RowsCleared.Count > 0)
                    {
                        sawClear = true;
                        flow.Analytics.LogTutorialStep(3);
                        Hide(); // dismissed by doing (GDD 9)
                    }

                    break;
                case 2:
                    if (result.Events.TideRose || result.Next.MoveCount >= 3)
                    {
                        Hide();
                    }

                    break;
                case 3:
                    if (result.Events.RescuedCreatures.Count > 0)
                    {
                        Hide();
                    }

                    break;
                case 5:
                    if (result.Events.PlacedCells.Count > 0)
                    {
                        Hide();
                    }

                    break;
            }
        }

        private void Update()
        {
            // L4: the drown-threat beat fires when the water actually threatens.
            if (InTutorial && levelIndex == 4 && !pumpHintShown && flow.Store != null
                && flow.Store.State.WaterLevel >= 4)
            {
                pumpHintShown = true;
                Show("tutorial.l4.pump");
            }

            if (InTutorial && levelIndex == 4 && pumpHintShown && flow.Store != null
                && flow.Store.State.WaterLevel < 4 && banner.transform.parent.gameObject.activeSelf)
            {
                Hide(); // they used the pump (or cleared their way out) — dismissed by doing
            }

            UpdateGhostReplay();
        }

        /// <summary>
        /// §6.3: after 4s idle on the drag hint, a ghost hand replays the gesture
        /// (tray slot → board) on a 2s loop until the player touches anything.
        /// </summary>
        private void UpdateGhostReplay()
        {
            bool dragBeat = InTutorial && levelIndex == 1 && !sawPlacement
                && banner.transform.parent.gameObject.activeSelf;
            var pointer = UnityEngine.InputSystem.Pointer.current;
            bool pressing = pointer != null && pointer.press.isPressed;

            if (!dragBeat || pressing)
            {
                idleTime = 0f;
                if (ghostHand != null)
                {
                    ghostHand.enabled = false;
                }

                return;
            }

            idleTime += Time.deltaTime;
            if (idleTime < IdleReplaySeconds)
            {
                return;
            }

            if (ghostHand == null)
            {
                var go = new GameObject("tutorialGhostHand");
                go.transform.SetParent(transform, false);
                ghostHand = go.AddComponent<SpriteRenderer>();
                ghostHand.sprite = SpriteFactory.Dot();
                ghostHand.sortingOrder = 96;
                go.transform.localScale = Vector3.one * 0.55f;
            }

            float loop = Mathf.Repeat(idleTime - IdleReplaySeconds, 2f);
            float travel = Mathf.Clamp01(loop / 1.4f);
            float eased = 1f - (1f - travel) * (1f - travel);
            Vector3 from = BoardLayout.TraySlotCenter(0);
            Vector3 to = BoardLayout.CellToWorld(BoardSpec.Width / 2, 4);
            ghostHand.enabled = true;
            ghostHand.transform.position = Vector3.Lerp(from, to, eased);
            Color tint = ThemeRuntime.Color("accent.primary");
            ghostHand.color = new Color(tint.r, tint.g, tint.b, 0.55f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(loop / 2f)));
        }

        private void Show(string key)
        {
            banner.text = flow.Strings.Get(key);
            banner.transform.parent.gameObject.SetActive(true);
        }

        private void Hide() => banner.transform.parent.gameObject.SetActive(false);
    }
}
