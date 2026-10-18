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

        // In-flight exit tweens per root: a screen re-shown before its exit
        // finishes must not be deactivated by that stale exit's completion.
        private readonly Dictionary<RectTransform, (TweenHandle handle, Vector2 home)> exits
            = new Dictionary<RectTransform, (TweenHandle, Vector2)>();

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

        /// <summary>
        /// Removes the top screen instantly with no reveal (tab-to-tab switches: the
        /// next push floats in over it). False at the root.
        /// </summary>
        public bool DropTop()
        {
            if (stack.Count <= 1)
            {
                return false;
            }

            (string _, RectTransform root, CanvasGroup group) = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            root.gameObject.SetActive(false);
            group.alpha = 1f;
            group.interactable = true;
            return true;
        }

        private void AnimateIn(RectTransform root, CanvasGroup group)
        {
            if (exits.TryGetValue(root, out (TweenHandle handle, Vector2 home) stale))
            {
                // Restore the resting position the cancelled exit was drifting from.
                stale.handle.Cancel();
                root.anchoredPosition = stale.home;
                exits.Remove(root);
            }

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
            TweenHandle exit = Tween.Run(this, "t.screen", "easeInCubic", u =>
            {
                screen.group.alpha = 1f - u;
                screen.root.anchoredPosition = home + new Vector2(0f, -24f * u);
            }, () =>
            {
                exits.Remove(screen.root);
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
            exits[screen.root] = (exit, home);
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
