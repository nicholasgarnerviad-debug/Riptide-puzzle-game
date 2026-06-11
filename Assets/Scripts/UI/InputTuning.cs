using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// Drag-and-place feel thresholds (contract 4B: "all thresholds in a config SO").
    /// Defaults are the GDD 7.3 numbers; visual-gate feedback tunes the instance.
    /// </summary>
    public sealed class InputTuning : ScriptableObject
    {
        [Tooltip("GDD 7.3: magnetic snap radius in cells")]
        public float snapRadiusCells = 0.6f;

        [Tooltip("GDD 7.3: piece rides this many screen pixels above the finger")]
        public float liftPixels = 90f;

        [Tooltip("Pixels of movement before a press becomes a drag")]
        public float dragStartPixels = 6f;

        [Tooltip("World radius around a tray slot that starts a drag")]
        public float grabRadiusWorld = 1.1f;

        public static InputTuning CreateDefault() => CreateInstance<InputTuning>();
    }
}
