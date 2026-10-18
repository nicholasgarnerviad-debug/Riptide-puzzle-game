using Riptide.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// Menu ambience, Tidal Pop edition: hollow bubbles rising behind screen
    /// content (the canvas cousin of <see cref="MarineSnow"/>), parked under
    /// reduced motion.
    /// </summary>
    public sealed class CanvasSnow : MonoBehaviour
    {
        private const int Count = 16;
        private readonly RectTransform[] flakes = new RectTransform[Count];
        private readonly float[] speeds = new float[Count];
        private readonly float[] phases = new float[Count];
        private RectTransform area = null!;
        private float time;

        public static CanvasSnow Create(RectTransform parent)
        {
            var go = new GameObject("CanvasSnow", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.SetSiblingIndex(1); // above the background, below content
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var snow = go.AddComponent<CanvasSnow>();
            snow.area = rt;
            snow.Build();
            return snow;
        }

        private void Build()
        {
            Color tint = ThemeRuntime.Color("bubble");
            for (int i = 0; i < Count; i++)
            {
                var flakeGo = new GameObject($"bubble_{i}", typeof(RectTransform));
                flakeGo.transform.SetParent(area, false);
                var image = flakeGo.AddComponent<Image>();
                image.sprite = PopSprites.Bubble();
                image.color = new Color(tint.r, tint.g, tint.b, tint.a * Random.Range(0.35f, 1f));
                image.raycastTarget = false;
                var rt = (RectTransform)flakeGo.transform;
                rt.sizeDelta = Vector2.one * Random.Range(16f, 52f);
                rt.anchorMin = rt.anchorMax = new Vector2(Random.value, Random.value);
                flakes[i] = rt;
                speeds[i] = Random.Range(40f, 110f);
                phases[i] = Random.Range(0f, Mathf.PI * 2f);
            }
        }

        private void Update()
        {
            bool visible = !ThemeRuntime.ReducedMotion;
            time += Time.deltaTime;
            for (int i = 0; i < Count; i++)
            {
                flakes[i].gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                Vector2 anchor = flakes[i].anchorMin;
                anchor.y += speeds[i] * Time.deltaTime / 2400f;
                anchor.x += Mathf.Sin(time * 1.2f + phases[i]) * 0.00012f;
                if (anchor.y > 1.03f)
                {
                    anchor = new Vector2(Random.value, -0.03f);
                }

                flakes[i].anchorMin = flakes[i].anchorMax = anchor;
            }
        }
    }

    /// <summary>
    /// In-game ambience: bubbles rising through the water column BEHIND the board
    /// (never over cells — clarity first). Pooled, wrapping, parked entirely when
    /// reduced motion is on.
    /// </summary>
    public sealed class MarineSnow : MonoBehaviour
    {
        private const int Count = 14;
        private readonly SpriteRenderer[] flakes = new SpriteRenderer[Count];
        private readonly float[] speeds = new float[Count];
        private readonly float[] phases = new float[Count];
        private float time;

        public static MarineSnow Create(Transform parent)
        {
            var go = new GameObject("MarineSnow");
            go.transform.SetParent(parent, false);
            var snow = go.AddComponent<MarineSnow>();
            snow.Build();
            return snow;
        }

        private void Build()
        {
            Color tint = ThemeRuntime.Color("bubble");
            for (int i = 0; i < Count; i++)
            {
                var flakeGo = new GameObject($"bubble_{i}");
                flakeGo.transform.SetParent(transform, false);
                var sr = flakeGo.AddComponent<SpriteRenderer>();
                sr.sprite = PopSprites.Bubble();
                sr.color = new Color(tint.r, tint.g, tint.b, tint.a * Random.Range(0.3f, 0.8f));
                sr.sortingOrder = -12; // behind the board frame, over the backdrop
                flakeGo.transform.position = RandomStart(anywhere: true);
                flakeGo.transform.localScale = Vector3.one * Random.Range(0.18f, 0.5f);
                flakes[i] = sr;
                speeds[i] = Random.Range(0.5f, 1.3f);
                phases[i] = Random.Range(0f, Mathf.PI * 2f);
            }
        }

        private static Vector3 RandomStart(bool anywhere)
        {
            float x = Random.Range(-BoardSpec.Width * 0.5f - 1.2f, BoardSpec.Width * 0.5f + 1.2f);
            float bottom = BoardLayout.ContentBottomY - 3f;
            float y = anywhere ? Random.Range(bottom, BoardLayout.BoardTopY + 2f) : bottom;
            return new Vector3(x, y, 0f);
        }

        private void Update()
        {
            bool visible = !ThemeRuntime.ReducedMotion;
            time += Time.deltaTime;
            for (int i = 0; i < Count; i++)
            {
                flakes[i].enabled = visible;
                if (!visible)
                {
                    continue;
                }

                Vector3 pos = flakes[i].transform.position;
                pos.y += speeds[i] * Time.deltaTime;
                pos.x += Mathf.Sin(time * 1.1f + phases[i]) * 0.15f * Time.deltaTime;
                if (pos.y > BoardLayout.BoardTopY + 3f)
                {
                    pos = RandomStart(anywhere: false);
                }

                flakes[i].transform.position = pos;
            }
        }
    }
}
