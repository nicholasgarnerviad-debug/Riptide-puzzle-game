using Riptide.Core;
using System.Collections;
using TMPro;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// Spec §5.2 — the most important UI element. A 132 ref-px filled ring at the
    /// tray card's left end: fill = tideCounter/tideInterval clockwise from 12
    /// o'clock, wave glyph + placements-remaining numeral inside (grandma-test
    /// redundancy), cyan → amber at 2 remaining → danger at 1 (single pulse +
    /// light haptic on entering), flash-and-empty on rise. Drain never touches it:
    /// the ring is about WHEN, the water is about HOW HIGH.
    /// </summary>
    public sealed class TideMeterRing : MonoBehaviour
    {
        private const int Segments = 60;
        private const float DiameterRefPx = 132f; // §5.2

        private readonly SpriteRenderer[] segments = new SpriteRenderer[Segments];
        private TextMeshPro numeral = null!;
        private TextMeshPro waveGlyph = null!;
        private int totalSegments = -1;
        private int filledSegments;
        private float shownFill;
        private int lastRemaining = int.MaxValue;
        private float radius;

        public int TotalCount => totalSegments;
        public int FilledCount => filledSegments;
        public int RemainingUntilRise => totalSegments - filledSegments;

        public static TideMeterRing Create(Transform parent)
        {
            var go = new GameObject("TideMeterRing");
            go.transform.SetParent(parent, false);
            go.transform.position = BoardLayout.TrayRingCenter;
            var ring = go.AddComponent<TideMeterRing>();
            ring.Build();
            return ring;
        }

        private void Build()
        {
            radius = ThemeRuntime.WorldFromRefPx(DiameterRefPx) * 0.5f;

            // Visual pass: the flagship element gets presence — a soft inner glow
            // disc behind the ring so it reads as the instrument it is (§5.2).
            var glowGo = new GameObject("innerGlow");
            glowGo.transform.SetParent(transform, false);
            glowGo.transform.localScale = Vector3.one * (radius * 2.6f / 2f);
            var glow = glowGo.AddComponent<SpriteRenderer>();
            glow.sprite = MenuSprites.SoftGlow();
            glow.color = ThemeRuntime.Color("glow.primary");
            glow.sortingOrder = 33;

            float segLength = 2f * Mathf.PI * radius / Segments;
            for (int i = 0; i < Segments; i++)
            {
                // Clockwise from 12 o'clock (§5.2).
                float angleDeg = 90f - (i + 0.5f) * (360f / Segments);
                float angleRad = angleDeg * Mathf.Deg2Rad;
                var go = new GameObject($"seg_{i}");
                go.transform.SetParent(transform, false);
                go.transform.localPosition =
                    new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0f) * radius;
                go.transform.localRotation = Quaternion.Euler(0f, 0f, angleDeg + 90f);
                go.transform.localScale = new Vector3(segLength * 1.25f, radius * 0.22f, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Solid();
                sr.color = ThemeRuntime.Color("meter.track");
                sr.sortingOrder = 35;
                segments[i] = sr;
            }

            waveGlyph = UiText.CreateWorld(transform, "wave", "~", "caption", "accent.primary", 36);
            waveGlyph.transform.localPosition = new Vector3(0f, radius * 0.34f, 0f);
            numeral = UiText.CreateWorld(transform, "remaining", "", "micro", "text.primary", 36);
            numeral.transform.localPosition = new Vector3(0f, -radius * 0.18f, 0f);
        }

        /// <summary>State-driven render: fill from tideCounter, color from remaining.</summary>
        public void Render(GameState state)
        {
            int interval = EscalationRules.EffectiveTideInterval(state.Config, state.Goals.TidesSurvived);
            if (interval != totalSegments)
            {
                totalSegments = interval;
                shownFill = 0f;
            }

            filledSegments = Mathf.Clamp(state.TideCounter, 0, totalSegments);
            numeral.text = RemainingUntilRise.ToString();

            // §5.2: single strong pulse + light haptic when ENTERING danger (1 left).
            int remaining = RemainingUntilRise;
            if (remaining == 1 && lastRemaining > 1)
            {
                StartCoroutine(DangerEntryPulse());
                Haptics.Light();
            }

            lastRemaining = remaining;
        }

        /// <summary>§5.2 on rise: the ring completes, flashes, and empties with the surge.</summary>
        public void PlayRiseFlash()
        {
            StartCoroutine(RiseFlash());
        }

        private Color FillColor()
        {
            int remaining = RemainingUntilRise;
            if (remaining <= 1)
            {
                return ThemeRuntime.Color("danger");
            }

            return remaining == 2 ? ThemeRuntime.Color("warning") : ThemeRuntime.Color("accent.primary");
        }

        private void Update()
        {
            if (totalSegments <= 0)
            {
                return;
            }

            // Fill sweep animates at t.fast toward the truth; counts are already exact.
            float target = (float)filledSegments / totalSegments;
            float speed = 1f / Mathf.Max(0.01f, ThemeRuntime.Seconds("t.fast"));
            shownFill = Mathf.MoveTowards(shownFill, target, Time.deltaTime * speed);

            Color fill = FillColor();
            Color track = ThemeRuntime.Color("meter.track");
            float litSegments = shownFill * Segments;
            for (int i = 0; i < Segments; i++)
            {
                segments[i].color = i + 0.5f <= litSegments ? fill : track;
            }
        }

        private IEnumerator DangerEntryPulse()
        {
            float t = 0f;
            float life = ThemeRuntime.Seconds("t.base");
            Vector3 baseScale = Vector3.one;
            while (t < life)
            {
                t += Time.deltaTime;
                float pulse = 1f + 0.18f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / life));
                transform.localScale = baseScale * pulse;
                yield return null;
            }

            transform.localScale = baseScale;
        }

        private IEnumerator RiseFlash()
        {
            float t = 0f;
            float life = ThemeRuntime.Seconds("t.fast");
            Color flash = ThemeRuntime.Color("text.primary");
            while (t < life)
            {
                t += Time.deltaTime;
                float u = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / life));
                foreach (SpriteRenderer sr in segments)
                {
                    sr.color = Color.Lerp(sr.color, flash, u);
                }

                yield return null;
            }

            shownFill = 0f;
        }
    }
}
