using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// Code-built uGUI primitives in the GDD 7.1 style. Legacy Text with the
    /// builtin runtime font — swapped for Rungo when Nick supplies the files
    /// (DECISIONS.md).
    /// </summary>
    public static class UiKit
    {
        private static Font? cachedFont;

        public static Font DefaultFont
        {
            get
            {
                if (cachedFont == null)
                {
                    // Same one-drop game font as TMP (UiText.CustomFont) so the
                    // legacy-UGUI HUD reskins from the same dropped .ttf; falls back
                    // to the builtin placeholder until one is provided.
                    cachedFont = UiText.CustomFont
                        ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }

                return cachedFont;
            }
        }

        public static readonly Color PanelColor = new Color(0.055f, 0.075f, 0.11f, 0.92f);
        public static readonly Color ButtonColor = new Color(0.12f, 0.22f, 0.30f, 0.95f);
        public static readonly Color ButtonAccent = new Color(0.16f, 0.55f, 0.60f, 0.95f);
        public static readonly Color TextColor = new Color(0.92f, 0.97f, 1f, 1f);
        public static readonly Color TextDim = new Color(0.65f, 0.72f, 0.78f, 1f);

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
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null)
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

        public static RectTransform Panel(RectTransform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            var image = go.AddComponent<Image>();
            image.color = color;
            return rt;
        }

        public static RectTransform Container(RectTransform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        public static Text Label(RectTransform parent, string name, string content, int size,
            Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var text = go.AddComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = content;
            return text;
        }

        public static Button TextButton(RectTransform parent, string name, string label, int size,
            Action onClick, Color? background = null)
        {
            RectTransform rt = Panel(parent, name, background ?? ButtonColor);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = rt.GetComponent<Image>();
            Text text = Label(rt, "label", label, size, TextColor);
            Stretch(text.rectTransform);
            button.onClick.AddListener(() => onClick());
            return button;
        }

        public static void Stretch(RectTransform rt, float margin = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(margin, margin);
            rt.offsetMax = new Vector2(-margin, -margin);
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 offset)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
        }
    }
}
