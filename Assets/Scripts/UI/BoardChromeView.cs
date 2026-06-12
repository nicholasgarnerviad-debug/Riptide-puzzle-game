using System.Collections.Generic;
using Riptide.Core;
using TMPro;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// One shared danger read for every game-screen system (water tint, flood line,
    /// foam, music tension). GDD 7.2/7.3 sets the danger experience at water ≥ 7;
    /// the UI spec §5.1 says drownLevel−2 (= 8). GDD is law, so 7 stands —
    /// conflict flagged in DECISIONS.md for Nick.
    /// </summary>
    public static class DangerRule
    {
        public const int Threshold = BoardSpec.DrownWaterLevel - 3;

        public static bool IsDanger(int waterLevel) => waterLevel >= Threshold;
    }

    /// <summary>
    /// Spec §4.3 board chrome: frame (board.frameBg fill + 2px stroke.subtle,
    /// r.s corners), the dashed danger flood line with warning marker at the drown
    /// row (35% alpha far, 100% + 1.2s pulse in danger), and the left-edge depth
    /// gauge (numerals every 2 rows, glowing notch at the current level).
    /// </summary>
    public sealed class BoardChromeView : MonoBehaviour
    {
        private const int FrameSorting = 5;
        private const int GaugeSorting = 8;
        private const int FloodSorting = 56;
        private const float FloodFarAlpha = 0.35f;
        private const float FloodPulseSeconds = 1.2f;

        private readonly List<SpriteRenderer> floodDashes = new List<SpriteRenderer>();
        private SpriteRenderer floodMarkerDot = null!;
        private TextMeshPro floodMarkerText = null!;
        private SpriteRenderer notch = null!;
        private bool danger;
        private float pulseTime;
        private int shownWaterLevel = -1;
        private float floodLineY;

        /// <summary>Tests: the flood line's world row (must equal the drown threshold).</summary>
        public float FloodLineLevel { get; private set; } = -1f;

        /// <summary>Tests: water level the gauge notch is marking.</summary>
        public int NotchLevel => shownWaterLevel;

        /// <summary>Tests: danger presentation active (flood at full alpha + pulse).</summary>
        public bool DangerShown => danger;

        public static BoardChromeView Create(Transform parent)
        {
            var go = new GameObject("BoardChromeView");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<BoardChromeView>();
            view.Build();
            return view;
        }

        private void Build()
        {
            BuildFrame();
            BuildFloodLine();
            BuildDepthGauge();
        }

        private void BuildFrame()
        {
            float pad = BoardLayout.FramePad;
            float width = BoardSpec.Width + 2f * pad;
            float height = BoardSpec.Height + 2f * pad;
            float centerY = (BoardLayout.BoardBottomY + BoardLayout.BoardTopY) * 0.5f;

            var fillGo = new GameObject("frameFill");
            fillGo.transform.SetParent(transform, false);
            fillGo.transform.position = new Vector3(0f, centerY, 0f);
            var fill = fillGo.AddComponent<SpriteRenderer>();
            fill.sprite = SpriteFactory.RoundedFill();
            fill.drawMode = SpriteDrawMode.Sliced;
            fill.size = new Vector2(width, height);
            fill.color = ThemeRuntime.Color("board.frameBg");
            fill.sortingOrder = FrameSorting;

            var strokeGo = new GameObject("frameStroke");
            strokeGo.transform.SetParent(transform, false);
            strokeGo.transform.position = new Vector3(0f, centerY, 0f);
            var stroke = strokeGo.AddComponent<SpriteRenderer>();
            stroke.sprite = SpriteFactory.RoundedStroke();
            stroke.drawMode = SpriteDrawMode.Sliced;
            stroke.size = new Vector2(width, height);
            stroke.color = ThemeRuntime.Color("stroke.subtle");
            stroke.sortingOrder = FrameSorting + 1;
        }

        private void BuildFloodLine()
        {
            FloodLineLevel = BoardSpec.DrownWaterLevel;
            floodLineY = BoardLayout.WaterlineY(BoardSpec.DrownWaterLevel);
            Color dashColor = ThemeRuntime.Color("danger");

            const int dashCount = 12;
            float span = BoardSpec.Width + 0.6f;
            float dashWidth = span / (dashCount * 2f - 1f);
            float leftX = -span * 0.5f;
            for (int i = 0; i < dashCount; i++)
            {
                var go = new GameObject($"floodDash_{i}");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(leftX + dashWidth * (2f * i + 0.5f), floodLineY, 0f);
                go.transform.localScale = new Vector3(dashWidth, 0.07f, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Solid();
                sr.color = dashColor;
                sr.sortingOrder = FloodSorting;
                floodDashes.Add(sr);
            }

            // Warning marker at the left margin. LiberationSans has no U+26A0, so
            // a danger dot + "!" stands in until the branded glyph set lands.
            // Universal fit: everything left of the board must stay inside the
            // theme's boardSideAllowance — the camera shows exactly that much.
            float markerX = leftX - 0.38f;
            var markerGo = new GameObject("floodMarker");
            markerGo.transform.SetParent(transform, false);
            markerGo.transform.position = new Vector3(markerX, floodLineY, 0f);
            floodMarkerDot = markerGo.AddComponent<SpriteRenderer>();
            floodMarkerDot.sprite = SpriteFactory.Dot();
            floodMarkerDot.color = dashColor;
            floodMarkerDot.sortingOrder = FloodSorting;
            markerGo.transform.localScale = Vector3.one * 0.45f;

            floodMarkerText = UiText.CreateWorld(transform, "floodMarkerText", "!",
                "micro", "text.onAccent", FloodSorting + 1);
            floodMarkerText.transform.position = new Vector3(markerX, floodLineY, 0f);
        }

        private void BuildDepthGauge()
        {
            float trackWidth = ThemeRuntime.WorldFromRefPx(12f);
            float gaugeX = BoardLayout.Origin.x - 1.05f;
            float bottom = BoardLayout.BoardBottomY;
            float top = BoardLayout.WaterlineY(BoardSpec.MaxWaterLevel);

            var trackGo = new GameObject("gaugeTrack");
            trackGo.transform.SetParent(transform, false);
            trackGo.transform.position = new Vector3(gaugeX, (bottom + top) * 0.5f, 0f);
            trackGo.transform.localScale = new Vector3(trackWidth, top - bottom, 1f);
            var track = trackGo.AddComponent<SpriteRenderer>();
            track.sprite = SpriteFactory.Solid();
            track.color = ThemeRuntime.Color("stroke.subtle");
            track.sortingOrder = GaugeSorting;

            for (int level = 0; level <= BoardSpec.MaxWaterLevel; level += 2)
            {
                var tickGo = new GameObject($"gaugeTick_{level}");
                tickGo.transform.SetParent(transform, false);
                tickGo.transform.position = new Vector3(gaugeX, BoardLayout.WaterlineY(level), 0f);
                tickGo.transform.localScale = new Vector3(trackWidth * 2.6f, 0.045f, 1f);
                var tick = tickGo.AddComponent<SpriteRenderer>();
                tick.sprite = SpriteFactory.Solid();
                tick.color = ThemeRuntime.Color("stroke.bright");
                tick.sortingOrder = GaugeSorting + 1;

                TextMeshPro label = UiText.CreateWorld(transform, $"gaugeLabel_{level}",
                    level.ToString(), "micro", "text.muted", GaugeSorting + 1);
                // Inside the side allowance (universal fit) — peripheral, not clipped.
                label.transform.position = new Vector3(gaugeX - 0.30f, BoardLayout.WaterlineY(level), 0f);
            }

            var notchGo = new GameObject("gaugeNotch");
            notchGo.transform.SetParent(transform, false);
            notchGo.transform.localScale = new Vector3(trackWidth * 3.4f, 0.12f, 1f);
            notch = notchGo.AddComponent<SpriteRenderer>();
            notch.sprite = SpriteFactory.Solid();
            notch.color = ThemeRuntime.Color("accent.primary");
            notch.sortingOrder = GaugeSorting + 2;
        }

        /// <summary>State-driven render (§6.1): notch position + danger presentation.</summary>
        public void Render(GameState state)
        {
            shownWaterLevel = state.WaterLevel;
            notch.transform.position = new Vector3(
                BoardLayout.Origin.x - 1.05f, BoardLayout.WaterlineY(state.WaterLevel), 0f);
            danger = DangerRule.IsDanger(state.WaterLevel);
            if (!danger)
            {
                pulseTime = 0f;
                ApplyFloodAlpha(FloodFarAlpha);
            }
        }

        private void Update()
        {
            if (!danger)
            {
                return;
            }

            // Spec §4.3: full alpha with a 1.2s pulse while the water is in danger range.
            pulseTime += Time.deltaTime;
            float pulse = 0.82f + 0.18f * Mathf.Sin(pulseTime * (2f * Mathf.PI / FloodPulseSeconds));
            ApplyFloodAlpha(pulse);
        }

        private void ApplyFloodAlpha(float alpha)
        {
            foreach (SpriteRenderer dash in floodDashes)
            {
                Color c = dash.color;
                c.a = alpha;
                dash.color = c;
            }

            Color mc = floodMarkerDot.color;
            mc.a = Mathf.Clamp01(alpha + 0.2f);
            floodMarkerDot.color = mc;
        }
    }
}
