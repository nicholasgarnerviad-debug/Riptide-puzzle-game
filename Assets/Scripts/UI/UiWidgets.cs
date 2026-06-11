using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>§3.7 bottom sheet: slides up t.screen over a scrim; Dismiss() reverses.</summary>
    public sealed class Sheet : MonoBehaviour
    {
        private RectTransform body = null!;
        private float height;

        public bool Shown { get; private set; }

        public RectTransform Body => body;

        internal void Init(RectTransform sheetBody, float sheetHeight)
        {
            body = sheetBody;
            height = sheetHeight;
            body.anchoredPosition = new Vector2(0f, -height);
            gameObject.SetActive(false);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            Shown = true;
            Tween.Run(this, "t.screen", "easeOutQuart",
                u => body.anchoredPosition = new Vector2(0f, -height * (1f - u)));
        }

        public void Dismiss()
        {
            Tween.Run(this, "t.screen", "easeInCubic",
                u => body.anchoredPosition = new Vector2(0f, -height * u),
                () =>
                {
                    Shown = false;
                    gameObject.SetActive(false);
                });
        }
    }

    /// <summary>§3.9 toast: top, auto-dismiss 2.4s, queue depth 2.</summary>
    public sealed class ToastManager : MonoBehaviour
    {
        public const float SecondsVisible = 2.4f;
        public const int QueueDepth = 2;

        private readonly Queue<string> queue = new Queue<string>();
        private RectTransform canvasRoot = null!;
        private bool showing;

        public int Pending => queue.Count;

        public static ToastManager Create(RectTransform canvasRoot)
        {
            var go = new GameObject("ToastManager", typeof(RectTransform));
            go.transform.SetParent(canvasRoot, false);
            var manager = go.AddComponent<ToastManager>();
            manager.canvasRoot = canvasRoot;
            return manager;
        }

        public void Show(string message)
        {
            if (queue.Count >= QueueDepth)
            {
                return;
            }

            queue.Enqueue(message);
            if (!showing)
            {
                StartCoroutine(Pump());
            }
        }

        private IEnumerator Pump()
        {
            showing = true;
            while (queue.Count > 0)
            {
                string message = queue.Dequeue();
                RectTransform card = UiComponents.Card(canvasRoot, "toast", new Vector2(760f, 110f));
                card.anchorMin = new Vector2(0.5f, 1f);
                card.anchorMax = new Vector2(0.5f, 1f);
                card.anchoredPosition = new Vector2(0f, -140f);
                TextMeshProUGUI text = UiText.Create(card, "msg", message, "body", "text.primary");
                UiComponents.Stretch(text.rectTransform);

                var group = card.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                Tween.Run(this, "t.fast", "easeOutQuart", u => group.alpha = u);
                yield return new WaitForSeconds(SecondsVisible);
                bool gone = false;
                Tween.Run(this, "t.fast", "easeInCubic", u => group.alpha = 1f - u, () => gone = true);
                while (!gone)
                {
                    yield return null;
                }

                Destroy(card.gameObject);
            }

            showing = false;
        }
    }

    /// <summary>§3.10: coin glyph + tabular number; ≤8 fly-in particles on gain.</summary>
    public sealed class CoinCounter : MonoBehaviour
    {
        private TextMeshProUGUI label = null!;
        private long shown;

        public long Shown => shown;

        internal void Build(RectTransform root)
        {
            TextMeshProUGUI glyph = UiText.Create(root, "glyph", "●", "heading", "coin");
            UiComponents.Place(glyph.rectTransform, new Vector2(0.12f, 0.5f), new Vector2(64f, 64f));
            label = UiText.Create(root, "value", "0", "score", "text.primary", TextAlignmentOptions.MidlineLeft);
            UiComponents.Place(label.rectTransform, new Vector2(0.62f, 0.5f), new Vector2(220f, 64f));
        }

        public void SetInstant(long value)
        {
            shown = value;
            label.text = Riptide.Core.ShareCard.GroupThousands(value);
        }

        /// <summary>§1.2: count-up only when the delta matters; §3.10 particles fly toward the counter.</summary>
        public void AnimateTo(long value)
        {
            long from = shown;
            shown = value;
            if (value == from)
            {
                return;
            }

            Tween.Run(this, "t.countUp", "easeOutCubic",
                u => label.text = Riptide.Core.ShareCard.GroupThousands(from + (long)((value - from) * u)));
            if (value > from)
            {
                StartCoroutine(FlyParticles(Mathf.Min(8, (int)System.Math.Min(8, value - from))));
            }
        }

        private IEnumerator FlyParticles(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("coinFly", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = new Vector2(28f, 28f);
                var image = go.AddComponent<Image>();
                image.sprite = SpriteFactory.Dot();
                image.raycastTarget = false;
                ThemedElement.Bind(go, "coin");
                Vector2 start = new Vector2(Random.Range(-260f, -120f), Random.Range(-160f, 160f));
                rt.anchoredPosition = start;
                TweenHandle handle = Tween.RunSeconds(this, 0.6f, ThemeRuntime.Curve("easeOutQuart"),
                    u => rt.anchoredPosition = Vector2.Lerp(start, Vector2.zero, u),
                    () => Destroy(go));
                yield return new WaitForSeconds(0.05f);
            }
        }
    }

    /// <summary>§3.11: three stars, fill staggered 120ms, unearned = stroke-only (dim).</summary>
    public sealed class StarTriplet : MonoBehaviour
    {
        private readonly Image[] stars = new Image[3];

        public int Filled { get; private set; }

        internal void Build(RectTransform root)
        {
            for (int i = 0; i < 3; i++)
            {
                RectTransform star = UiComponents.Rect(root, $"star{i}", new Vector2(110f, 110f));
                UiComponents.Place(star, new Vector2(0.2f + 0.3f * i, 0.5f), new Vector2(110f, 110f));
                stars[i] = star.gameObject.AddComponent<Image>();
                stars[i].sprite = SpriteFactory.Dot();
                ThemedElement.Bind(star.gameObject, "stroke.bright");
            }
        }

        public void Show(int earned)
        {
            Filled = Mathf.Clamp(earned, 0, 3);
            StartCoroutine(FillSequence());
        }

        private IEnumerator FillSequence()
        {
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                if (index < Filled)
                {
                    yield return new WaitForSeconds(0.12f * index);
                    stars[index].color = ThemeRuntime.Color("positive");
                    Vector3 baseScale = stars[index].transform.localScale;
                    Tween.Run(this, "t.fast", "easeOutQuart",
                        u => stars[index].transform.localScale = baseScale * (0.6f + 0.4f * u));
                }
                else
                {
                    stars[index].color = ThemeRuntime.Color("stroke.bright");
                }
            }
        }
    }

    /// <summary>§3.12 pips: filled count over a total.</summary>
    public sealed class ProgressPips : MonoBehaviour
    {
        private readonly List<Image> pips = new List<Image>();

        public int Total => pips.Count;

        internal void Build(RectTransform root, int count)
        {
            for (int i = 0; i < count; i++)
            {
                RectTransform pip = UiComponents.Rect(root, $"pip{i}", new Vector2(20f, 20f));
                UiComponents.Place(pip, new Vector2((i + 0.5f) / count, 0.5f), new Vector2(20f, 20f));
                var image = pip.gameObject.AddComponent<Image>();
                image.sprite = SpriteFactory.Dot();
                ThemedElement.Bind(pip.gameObject, "stroke.subtle");
                pips.Add(image);
            }
        }

        public void SetFilled(int filled)
        {
            for (int i = 0; i < pips.Count; i++)
            {
                pips[i].color = ThemeRuntime.Color(i < filled ? "accent.primary" : "stroke.subtle");
            }
        }
    }

    /// <summary>§3.13: streak count with flame glyph; PulseOnce on increment.</summary>
    public sealed class StreakFlame : MonoBehaviour
    {
        private TextMeshProUGUI label = null!;

        internal void Build(RectTransform root)
        {
            label = UiText.Create(root, "label", "🔥 0", "heading", "danger");
            UiComponents.Stretch(label.rectTransform);
        }

        public void Set(int streak, bool pulse)
        {
            label.text = $"🔥 {streak}";
            if (pulse)
            {
                Vector3 baseScale = label.transform.localScale;
                Tween.Run(this, "t.fast", "easeOutQuart",
                    u => label.transform.localScale = baseScale * (1f + 0.25f * Mathf.Sin(Mathf.PI * u)));
            }
        }
    }

    /// <summary>§3.16 creature roundel: silhouette / normal / rescued-sparkle.</summary>
    public sealed class CreatureChip : MonoBehaviour
    {
        public enum State
        {
            Silhouette,
            Normal,
            RescuedSparkle,
        }

        private Image portrait = null!;
        private byte creatureId;

        public State Current { get; private set; } = State.Silhouette;

        internal void Build(RectTransform root, byte id)
        {
            creatureId = id;
            portrait = root.gameObject.AddComponent<Image>();
            portrait.sprite = SpriteFactory.Creature();
            Apply(State.Silhouette);
        }

        public void Apply(State state)
        {
            Current = state;
            portrait.color = state == State.Silhouette
                ? ThemeRuntime.Color("bg.raised")
                : Palette.CreatureColor(creatureId);
            if (state == State.RescuedSparkle)
            {
                Vector3 baseScale = portrait.transform.localScale;
                Tween.Run(this, "t.fast", "easeOutQuart",
                    u => portrait.transform.localScale = baseScale * (1f + 0.15f * Mathf.Sin(Mathf.PI * u)));
            }
        }
    }
}
