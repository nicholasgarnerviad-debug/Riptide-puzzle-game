using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// A full-screen moment over everything — the Conch, a Call reveal, growing up,
    /// a Tideling's card (GDD §16.6): its own two-token backdrop, a safe-area
    /// content root, a quick fade-and-rise in and out. Android back closes the top
    /// one first (<see cref="DismissTop"/>, asked by the ScreenManager).
    /// </summary>
    public abstract class Overlay : MonoBehaviour
    {
        private static readonly List<Overlay> open = new List<Overlay>();

        private CanvasGroup group = null!;
        private PopGradient backdrop = null!;
        private Action? onClosed;

        /// <summary>Safe-area padded content parent.</summary>
        public RectTransform Content { get; private set; } = null!;

        public bool IsOpen => gameObject.activeSelf && open.Contains(this);

        /// <summary>True while any overlay is up (taps and back belong to it).</summary>
        public static bool AnyOpen
        {
            get
            {
                Purge();
                return open.Count > 0;
            }
        }

        /// <summary>Closes the top overlay. False when none is open.</summary>
        public static bool DismissTop()
        {
            Purge();
            if (open.Count == 0)
            {
                return false;
            }

            open[open.Count - 1].Close();
            return true;
        }

        /// <param name="opaque">Full-screen moments sit on solid water; a card sheet lets its screen show through the scrim.</param>
        protected static T Create<T>(RectTransform canvasRoot, string name, string topToken, string bottomToken,
            bool opaque = true) where T : Overlay
        {
            RectTransform root = UiComponents.Rect(canvasRoot, name, Vector2.zero);
            UiComponents.Stretch(root);
            var bg = root.gameObject.AddComponent<Image>();
            bg.sprite = SpriteFactory.Solid();
            bg.raycastTarget = true; // nothing beneath takes a tap
            if (opaque)
            {
                ThemedElement.Bind(root.gameObject, "bg.deep");
            }
            else
            {
                bg.color = new Color(0f, 0f, 0f, 0f);
            }

            T overlay = root.gameObject.AddComponent<T>();
            Image tint = PopKit.Img(root, "backdrop", SpriteFactory.Solid(), "pop.white.top");
            UiComponents.Stretch(tint.rectTransform);
            overlay.backdrop = tint.gameObject.AddComponent<PopGradient>();
            overlay.backdrop.Set(topToken, bottomToken);
            overlay.group = root.gameObject.AddComponent<CanvasGroup>();

            RectTransform content = UiComponents.Rect(root, "content", Vector2.zero);
            UiComponents.Stretch(content);
            content.gameObject.AddComponent<SafeArea>();
            overlay.Content = content;
            root.gameObject.SetActive(false);
            return overlay;
        }

        /// <summary>Re-tints the backdrop (a reveal takes its Tideling's tier colors).</summary>
        protected void SetBackdrop(string topToken, string bottomToken) => backdrop.Set(topToken, bottomToken);

        /// <summary>Shows it on top of everything; <paramref name="closed"/> runs once it has gone.</summary>
        public void Open(Action? closed = null)
        {
            onClosed = closed;
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            open.Remove(this);
            open.Add(this);
            group.alpha = 0f;
            group.interactable = true;
            Content.localScale = Vector3.one * 0.96f;
            Tween.Run(this, "t.base", "easeOutQuart", u =>
            {
                group.alpha = u;
                Content.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, u);
            });
            OnOpened();
        }

        /// <summary>Runs as it opens — re-read the flow here.</summary>
        protected virtual void OnOpened()
        {
        }

        public void Close()
        {
            if (!open.Remove(this) || !gameObject.activeInHierarchy)
            {
                return;
            }

            group.interactable = false;
            Tween.Run(this, "t.fast", "easeInCubic", u => group.alpha = 1f - u, () =>
            {
                gameObject.SetActive(false);
                Action? done = onClosed;
                onClosed = null;
                done?.Invoke();
            });
        }

        private static void Purge()
        {
            for (int i = open.Count - 1; i >= 0; i--)
            {
                if (open[i] == null || !open[i].gameObject.activeInHierarchy)
                {
                    open.RemoveAt(i);
                }
            }
        }

        private void OnDestroy() => open.Remove(this);
    }
}
