using Riptide.Core;
using TMPro;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// Tidal Pop progress bar: a navy pill track, a candy fill that never shrinks
    /// below its own rounded caps, and a sticker label on top ("3/5").
    /// </summary>
    public sealed class PopBar : MonoBehaviour
    {
        private const float Inset = 9f;

        private RectTransform fill = null!;
        private TextMeshProUGUI label = null!;
        private Vector2 size;

        public static PopBar Create(RectTransform parent, string name, Vector2 size, PopTone tone = PopTone.Green,
            string typeToken = "caption")
        {
            RectTransform track = PopKit.Panel(parent, name, size, PopTone.Navy, size.y * 0.5f, 7f);
            var bar = track.gameObject.AddComponent<PopBar>();
            bar.size = size;

            RectTransform clip = UiComponents.Rect(track, "clip", Vector2.zero);
            UiComponents.Stretch(clip);
            clip.offsetMin = new Vector2(Inset, Inset);
            clip.offsetMax = new Vector2(-Inset, -Inset);
            bar.fill = UiComponents.Rect(clip, "fill", Vector2.zero);
            bar.fill.anchorMin = Vector2.zero;
            bar.fill.anchorMax = new Vector2(0f, 1f);
            bar.fill.offsetMin = bar.fill.offsetMax = Vector2.zero;
            PopKit.Surface(bar.fill, tone, (size.y - 2f * Inset) * 0.5f, 0f, lip: 0f);

            bar.label = UiText.CreateOutlined(track, "label", "", typeToken, "text.primary", "panel.edge");
            UiComponents.Stretch(bar.label.rectTransform);
            return bar;
        }

        public RectTransform Rect => (RectTransform)transform;

        /// <summary>Fills to value/target and labels it (default "value/target").</summary>
        public void Set(long value, long target, string? text = null)
        {
            float fraction = target > 0 ? Mathf.Clamp01((float)value / target) : 1f;
            float inner = Mathf.Max(1f, size.x - 2f * Inset);
            float caps = Mathf.Min(1f, (size.y - 2f * Inset) / inner);
            fill.gameObject.SetActive(fraction > 0f);
            fill.anchorMax = new Vector2(Mathf.Max(caps, fraction), 1f);
            label.text = text ?? $"{ShareCard.GroupThousands(System.Math.Min(value, target))}/{ShareCard.GroupThousands(target)}";
        }
    }
}
