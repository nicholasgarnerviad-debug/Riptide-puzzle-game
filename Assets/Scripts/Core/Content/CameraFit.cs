using System;

namespace Riptide.Core
{
    /// <summary>Everything the orthographic-camera fit depends on, in plain numbers.</summary>
    public readonly struct CameraFitInput
    {
        /// <summary>Device screen size in pixels (portrait: width &lt; height).</summary>
        public float ScreenWidthPx { get; }
        public float ScreenHeightPx { get; }

        /// <summary>Safe-area insets in pixels (notch / home indicator).</summary>
        public float SafeTopPx { get; }
        public float SafeBottomPx { get; }

        /// <summary>Half the world width the view must show: board half-width + side allowance.</summary>
        public float HalfWidthWorld { get; }

        /// <summary>World-Y extents of the game content (board frame top … tray card bottom).</summary>
        public float ContentTopWorld { get; }
        public float ContentBottomWorld { get; }

        /// <summary>UI bands reserved above/below the content, in canvas ref-px
        /// (scaled by screenWidth / canvasRefWidth — the canvas matches width).</summary>
        public float TopUiRefPx { get; }
        public float BottomUiRefPx { get; }
        public float CanvasRefWidth { get; }

        public CameraFitInput(float screenWidthPx, float screenHeightPx, float safeTopPx,
            float safeBottomPx, float halfWidthWorld, float contentTopWorld,
            float contentBottomWorld, float topUiRefPx, float bottomUiRefPx, float canvasRefWidth)
        {
            ScreenWidthPx = screenWidthPx;
            ScreenHeightPx = screenHeightPx;
            SafeTopPx = safeTopPx;
            SafeBottomPx = safeBottomPx;
            HalfWidthWorld = halfWidthWorld;
            ContentTopWorld = contentTopWorld;
            ContentBottomWorld = contentBottomWorld;
            TopUiRefPx = topUiRefPx;
            BottomUiRefPx = bottomUiRefPx;
            CanvasRefWidth = canvasRefWidth;
        }
    }

    public readonly struct CameraFitResult
    {
        public float OrthoSize { get; }
        public float CameraY { get; }

        public CameraFitResult(float orthoSize, float cameraY)
        {
            OrthoSize = orthoSize;
            CameraY = cameraY;
        }
    }

    /// <summary>
    /// Universal screen fit for the world-space game view (basis device:
    /// iPhone 16 Pro Max, 19.5:9 — but the solve is closed-form for any portrait
    /// screen). Width drives the fit: the board plus its side allowance must span
    /// the screen; if the screen is too wide/short for that to leave vertical room
    /// (tablets, 16:9), the camera zooms out until the content column fits between
    /// the HUD band and the bottom inset instead. The board is then top-anchored
    /// below the HUD; spare depth falls below the tray, which keeps the tray in
    /// the bottom thumb zone on tall phones.
    /// </summary>
    public static class CameraFit
    {
        public static CameraFitResult Solve(in CameraFitInput input)
        {
            float w = Math.Max(1f, input.ScreenWidthPx);
            float h = Math.Max(1f, input.ScreenHeightPx);
            float aspect = w / h;

            // 1) Width-driven candidate: show exactly the board + allowance.
            float orthoWidth = input.HalfWidthWorld / aspect;

            // 2) Vertical feasibility. UI pads scale with the match-width canvas
            //    (ref-px × w/canvasRefWidth) and convert to world via 2·ortho/h,
            //    so the pad fraction of the view height is ortho-independent:
            //    need 2·ortho·(1 − padFrac) ≥ contentSpan.
            float uiScale = w / Math.Max(1f, input.CanvasRefWidth);
            float topPadPx = input.SafeTopPx + input.TopUiRefPx * uiScale;
            float bottomPadPx = input.SafeBottomPx + input.BottomUiRefPx * uiScale;
            float padFrac = Math.Min(0.8f, (topPadPx + bottomPadPx) / h);
            float span = input.ContentTopWorld - input.ContentBottomWorld;
            float orthoHeight = span / (2f * (1f - padFrac));

            float ortho = Math.Max(orthoWidth, orthoHeight);

            // 3) Top-anchor: content top sits exactly below safe area + HUD band.
            float worldPerPx = 2f * ortho / h;
            float cameraY = input.ContentTopWorld + topPadPx * worldPerPx - ortho;

            return new CameraFitResult(ortho, cameraY);
        }
    }
}
