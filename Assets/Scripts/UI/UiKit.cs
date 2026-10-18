using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// Canvas plumbing shared by every code-built screen: the scaled overlay
    /// canvas, the input-armed EventSystem, bare containers. (The legacy-Text
    /// label/button helpers retired with the Tidal Pop overhaul — all copy is
    /// TextMeshPro through <see cref="UiText"/> now.)
    /// </summary>
    public static class UiKit
    {
        public static Canvas CreateCanvas(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Spec §2 as amended (universal fit): reference 1080×2347 — the
            // iPhone 16 Pro Max 19.5:9 basis at the 1080 token width — matched on
            // WIDTH, so ref-px token sizes hold everywhere and vertical space
            // flexes per device. Values live in the theme layout block.
            Riptide.Core.LayoutSpec layout = ThemeRuntime.Theme.Layout;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(layout.CanvasRefWidth, layout.CanvasRefHeight);
            scaler.matchWidthOrHeight = 0f;
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();
            // A runtime-added module has NO actions asset — without this call no
            // pointer event ever reaches the UI (every button dead; found at the
            // first human session, invisible to flow-driven tests).
            module.AssignDefaultActions();
        }

        public static RectTransform Container(RectTransform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        public static void Stretch(RectTransform rt, float margin = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(margin, margin);
            rt.offsetMax = new Vector2(-margin, -margin);
        }
    }
}
