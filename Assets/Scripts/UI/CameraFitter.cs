using Riptide.Core;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// Applies CameraFit.Solve (Core, device-matrix tested) to the game camera and
    /// re-applies when the resolution or safe area changes. Replaces the fixed
    /// orthoSize 8.7 that clipped the board's outer columns on every modern phone.
    /// </summary>
    public sealed class CameraFitter : MonoBehaviour
    {
        private Camera cam = null!;
        private int appliedWidth;
        private int appliedHeight;
        private Rect appliedSafe;

        public static CameraFitter Attach(Camera camera)
        {
            CameraFitter fitter = camera.GetComponent<CameraFitter>();
            if (fitter == null)
            {
                fitter = camera.gameObject.AddComponent<CameraFitter>();
            }

            fitter.cam = camera;
            fitter.Apply();
            return fitter;
        }

        private void Update()
        {
            if (Screen.width != appliedWidth || Screen.height != appliedHeight
                || Screen.safeArea != appliedSafe)
            {
                Apply();
            }
        }

        public void Apply()
        {
            appliedWidth = Screen.width;
            appliedHeight = Screen.height;
            appliedSafe = Screen.safeArea;

            LayoutSpec layout = ThemeRuntime.Theme.Layout;
            float halfWidth = BoardLayout.BoardHalfWidth
                + ThemeRuntime.WorldFromRefPx(layout.BoardSideAllowanceRefPx);

            CameraFitResult fit = CameraFit.Solve(new CameraFitInput(
                appliedWidth,
                appliedHeight,
                appliedHeight - appliedSafe.yMax,
                appliedSafe.yMin,
                halfWidth,
                BoardLayout.ContentTopY,
                BoardLayout.ContentBottomY,
                layout.HudBandRefPx + layout.BoardTopGapRefPx,
                layout.TrayBottomInsetRefPx + layout.BoosterRailBandRefPx,
                layout.CanvasRefWidth));

            cam.orthographicSize = fit.OrthoSize;
            cam.transform.position = new Vector3(0f, fit.CameraY, -10f);
        }
    }

    /// <summary>
    /// Pins a screen-space (canvas) element to a world position — HUD pieces that
    /// must track world objects (booster rail over the board's right edge, the
    /// milestone pop over the board) stay aligned on every aspect the camera fit
    /// produces. Re-anchors whenever the camera framing changes.
    /// </summary>
    public sealed class WorldAnchor : MonoBehaviour
    {
        public Vector3 World;

        private RectTransform rt = null!;
        private float appliedOrtho;
        private Vector3 appliedCamPos;

        public static WorldAnchor Pin(RectTransform target, Vector3 world)
        {
            var anchor = target.gameObject.AddComponent<WorldAnchor>();
            anchor.rt = target;
            anchor.World = world;
            anchor.Apply();
            return anchor;
        }

        private void OnEnable()
        {
            if (rt != null)
            {
                Apply();
            }
        }

        private void LateUpdate()
        {
            Camera? cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            if (!Mathf.Approximately(cam.orthographicSize, appliedOrtho)
                || cam.transform.position != appliedCamPos)
            {
                Apply();
            }
        }

        private void Apply()
        {
            Camera? cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            appliedOrtho = cam.orthographicSize;
            appliedCamPos = cam.transform.position;

            Vector3 viewport = cam.WorldToViewportPoint(World);
            var anchor = new Vector2(viewport.x, viewport.y);
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.anchoredPosition = Vector2.zero;
        }
    }
}
