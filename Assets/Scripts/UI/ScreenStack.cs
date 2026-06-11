using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// Spec §1.4/§2 screen container: push/pop with the sink-and-float transition
    /// (outgoing fades + drifts down 24px; incoming fades + drifts up from −24px,
    /// 60ms overlap). Android back pops. Screens are built lazily by factories;
    /// in 5-UI-a the existing screens migrate onto this stack.
    /// </summary>
    public sealed class ScreenStack : MonoBehaviour
    {
        private readonly List<(string id, RectTransform root, CanvasGroup group)> stack
            = new List<(string, RectTransform, CanvasGroup)>();

        public int Depth => stack.Count;

        public string? TopId => stack.Count > 0 ? stack[stack.Count - 1].id : null;

        public event Action<string?>? TopChanged;

        public static ScreenStack Create(Transform parent)
        {
            var go = new GameObject("ScreenStack");
            go.transform.SetParent(parent, false);
            return go.AddComponent<ScreenStack>();
        }

        public void Push(string id, RectTransform screenRoot)
        {
            var group = screenRoot.gameObject.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = screenRoot.gameObject.AddComponent<CanvasGroup>();
            }

            if (stack.Count > 0)
            {
                AnimateOut(stack[stack.Count - 1], deactivate: true);
            }

            stack.Add((id, screenRoot, group));
            screenRoot.gameObject.SetActive(true);
            AnimateIn(screenRoot, group);
            TopChanged?.Invoke(id);
        }

        /// <summary>Pops the top screen; false when the stack is at its root (Android back → background app).</summary>
        public bool Pop()
        {
            if (stack.Count <= 1)
            {
                return false;
            }

            (string _, RectTransform root, CanvasGroup group) top = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            AnimateOut((TopId ?? "", top.root, top.group), deactivate: true, destroyAfter: false);

            (string id, RectTransform root, CanvasGroup group) revealed = stack[stack.Count - 1];
            revealed.root.gameObject.SetActive(true);
            AnimateIn(revealed.root, revealed.group);
            TopChanged?.Invoke(revealed.id);
            return true;
        }

        private void AnimateIn(RectTransform root, CanvasGroup group)
        {
            float drift = 24f;
            Vector2 home = root.anchoredPosition;
            root.anchoredPosition = home + new Vector2(0f, -drift);
            group.alpha = 0f;
            group.interactable = false;
            Tween.Run(this, "t.screen", "easeOutQuart", u =>
            {
                group.alpha = u;
                root.anchoredPosition = home + new Vector2(0f, -drift * (1f - u));
            }, () =>
            {
                group.interactable = true;
                root.anchoredPosition = home;
            });
        }

        private void AnimateOut((string id, RectTransform root, CanvasGroup group) screen,
            bool deactivate, bool destroyAfter = false)
        {
            Vector2 home = screen.root.anchoredPosition;
            screen.group.interactable = false;
            Tween.Run(this, "t.screen", "easeInCubic", u =>
            {
                screen.group.alpha = 1f - u;
                screen.root.anchoredPosition = home + new Vector2(0f, -24f * u);
            }, () =>
            {
                screen.root.anchoredPosition = home;
                if (deactivate)
                {
                    screen.root.gameObject.SetActive(false);
                }

                if (destroyAfter)
                {
                    Destroy(screen.root.gameObject);
                }
            });
        }

        private void Update()
        {
            // Android back: pop; at root the OS backgrounds the app (spec §4).
            if (UnityEngine.InputSystem.Keyboard.current != null
                && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Pop();
            }
        }
    }
}
