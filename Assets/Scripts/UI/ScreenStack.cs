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

            // Screen roots are transparent (the ambient backdrop is shared, behind
            // the whole stack), so the outgoing screen is HIDDEN immediately rather
            // than cross-faded — a fade-out would briefly layer two see-through
            // screens over the backdrop (the "double exposure" the capture sweep
            // caught). The incoming float-in (it surfaces from the deep) is the
            // motion that reads; deactivation is synchronous so a screen can never
            // be left stale-active beneath a transparent top.
            if (stack.Count > 0)
            {
                HideNow(stack[stack.Count - 1]);
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

            (string id, RectTransform root, CanvasGroup group) top = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            HideNow(top);

            (string id, RectTransform root, CanvasGroup group) revealed = stack[stack.Count - 1];
            revealed.root.gameObject.SetActive(true);
            AnimateIn(revealed.root, revealed.group);
            TopChanged?.Invoke(revealed.id);
            return true;
        }

        private static void HideNow((string id, RectTransform root, CanvasGroup group) screen)
        {
            screen.group.alpha = 1f;
            screen.group.interactable = false;
            screen.root.gameObject.SetActive(false);
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

        /// <summary>The screen ids bottom→top (BackRouter derives "previous" from this).</summary>
        public IReadOnlyList<string> Ids
        {
            get
            {
                var ids = new List<string>(stack.Count);
                foreach ((string id, _, _) in stack)
                {
                    ids.Add(id);
                }

                return ids;
            }
        }

        // Android back is routed by ScreenManager through BackRouter (5-UI-a):
        // one nav authority, so no Escape handling here.
    }
}
