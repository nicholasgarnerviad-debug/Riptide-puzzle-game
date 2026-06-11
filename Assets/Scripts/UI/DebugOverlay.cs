#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Riptide.Game;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Riptide.UI
{
    /// <summary>
    /// Contract 4E: editor-only debug overlay — state hash, waterLevel, tideCounter,
    /// seed, fps. Toggle with D (Input System keyboard).
    /// </summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
        private GameStore store = null!;
        private AnalyticsService? analytics;
        private ulong seed;
        private bool visible = true;
        private bool eventsVisible;
        private float fpsSmoothed;

        public static DebugOverlay Create(Transform parent, GameStore store, ulong seed,
            AnalyticsService? analytics = null)
        {
            var go = new GameObject("DebugOverlay");
            go.transform.SetParent(parent, false);
            var overlay = go.AddComponent<DebugOverlay>();
            overlay.store = store;
            overlay.seed = seed;
            overlay.analytics = analytics;
            return overlay;
        }

        private void Update()
        {
            float fps = Time.deltaTime > 0f ? 1f / Time.deltaTime : 0f;
            fpsSmoothed = fpsSmoothed <= 0f ? fps : Mathf.Lerp(fpsSmoothed, fps, 0.08f);
            if (Keyboard.current != null && Keyboard.current.dKey.wasPressedThisFrame)
            {
                visible = !visible;
            }

            // Contract 7D: the last-20-events debug surface (toggle E).
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                eventsVisible = !eventsVisible;
            }
        }

        private void OnGUI()
        {
            if (!visible || store == null)
            {
                return;
            }

            var state = store.State;
            string text =
                $"seed  {seed}\n" +
                $"hash  {state.ComputeHash():X16}\n" +
                $"water {state.WaterLevel}  tide {state.TideCounter}\n" +
                $"moves {state.MoveCount}  score {state.Score}\n" +
                $"status {state.Status}  fps {fpsSmoothed:F0}";
            GUI.Label(new Rect(8, 8, 360, 110), text);

            if (eventsVisible && analytics != null)
            {
                var sb = new System.Text.StringBuilder("-- last events (E) --\n");
                foreach (string entry in analytics.LastEvents)
                {
                    sb.AppendLine(entry);
                }

                GUI.Label(new Rect(8, 130, 700, 460), sb.ToString());
            }
        }
    }
}
#endif
