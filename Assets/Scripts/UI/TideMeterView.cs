using System.Collections.Generic;
using Riptide.Core;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// THE most important UI element (GDD 9): a ring of dots around the tray showing
    /// placements-until-rise. Filled dots = tide progress; everything readable at a
    /// glance; pulses when one placement remains. Rebuilds when escalation shrinks
    /// the interval.
    /// </summary>
    public sealed class TideMeterView : MonoBehaviour
    {
        private const float Radius = 1.85f;
        private readonly List<SpriteRenderer> dots = new List<SpriteRenderer>();
        private int totalSegments = -1;
        private int filledSegments;
        private float pulseTime;

        public int TotalCount => totalSegments;
        public int FilledCount => filledSegments;
        public int RemainingUntilRise => totalSegments - filledSegments;

        public static TideMeterView Create(Transform parent)
        {
            var go = new GameObject("TideMeterView");
            go.transform.SetParent(parent, false);
            go.transform.position = BoardLayout.TrayCenter;
            return go.AddComponent<TideMeterView>();
        }

        public void Render(GameState state)
        {
            int interval = EscalationRules.EffectiveTideInterval(state.Config, state.Goals.TidesSurvived);
            if (interval != totalSegments)
            {
                Rebuild(interval);
            }

            filledSegments = Mathf.Clamp(state.TideCounter, 0, totalSegments);
            for (int i = 0; i < dots.Count; i++)
            {
                bool filled = i < filledSegments;
                dots[i].color = filled ? Palette.MeterFilled : Palette.MeterEmpty;
                dots[i].transform.localScale = Vector3.one * (filled ? 0.5f : 0.34f);
            }
        }

        private void Rebuild(int segments)
        {
            foreach (SpriteRenderer dot in dots)
            {
                if (dot != null)
                {
                    Destroy(dot.gameObject);
                }
            }

            dots.Clear();
            totalSegments = segments;
            for (int i = 0; i < segments; i++)
            {
                // Clockwise from 12 o'clock around the tray.
                float angle = (90f - i * (360f / segments)) * Mathf.Deg2Rad;
                var go = new GameObject($"meter_{i}");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * Radius;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Dot();
                sr.sortingOrder = 35;
                dots.Add(sr);
            }
        }

        private void Update()
        {
            // One placement from a rise: the whole ring pulses toward danger color.
            if (totalSegments > 0 && RemainingUntilRise <= 1)
            {
                pulseTime += Time.deltaTime;
                float pulse = 0.5f + 0.5f * Mathf.Sin(pulseTime * 7f);
                for (int i = 0; i < filledSegments && i < dots.Count; i++)
                {
                    dots[i].color = Color.Lerp(Palette.MeterFilled, Palette.MeterDanger, pulse);
                }
            }
        }
    }
}
