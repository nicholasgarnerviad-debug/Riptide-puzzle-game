using Riptide.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// 8-UI menu ambience: the canvas cousin of MarineSnow — faint drifting dots
    /// behind screen content, parked under reduced motion.
    /// </summary>
    public sealed class CanvasSnow : MonoBehaviour
    {
        private const int Count = 14;
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
            Color tint = ThemeRuntime.Color("text.muted");
            for (int i = 0; i < Count; i++)
            {
                var flakeGo = new GameObject($"flake_{i}", typeof(RectTransform));
                flakeGo.transform.SetParent(area, false);
                var image = flakeGo.AddComponent<Image>();
                image.sprite = SpriteFactory.Dot();
                image.color = new Color(tint.r, tint.g, tint.b, 0.10f);
                image.raycastTarget = false;
                var rt = (RectTransform)flakeGo.transform;
                rt.sizeDelta = Vector2.one * Random.Range(6f, 16f);
                rt.anchorMin = rt.anchorMax = new Vector2(Random.value, Random.value);
                flakes[i] = rt;
                speeds[i] = Random.Range(12f, 34f);
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
                anchor.y -= speeds[i] * Time.deltaTime / 2400f;
                anchor.x += Mathf.Sin(time * 0.5f + phases[i]) * 0.00006f;
                if (anchor.y < -0.02f)
                {
                    anchor = new Vector2(Random.value, 1.02f);
                }

                flakes[i].anchorMin = flakes[i].anchorMax = anchor;
            }
        }
    }

    /// <summary>
    /// 8-UI ambience (GDD 7.1): faint marine snow drifting down the board scene.
    /// Eighteen pooled dots, slow fall with a sine sway, wrapping — and parked
    /// entirely when reduced motion is on.
    /// </summary>
    public sealed class MarineSnow : MonoBehaviour
    {
        private const int Count = 18;
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
            Color tint = ThemeRuntime.Color("text.muted");
            for (int i = 0; i < Count; i++)
            {
                var flakeGo = new GameObject($"flake_{i}");
                flakeGo.transform.SetParent(transform, false);
                var sr = flakeGo.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Dot();
                sr.color = new Color(tint.r, tint.g, tint.b, 0.12f);
                sr.sortingOrder = 7; // over the frame fill, under gauge and cells
                flakeGo.transform.position = RandomStart(anywhere: true);
                flakeGo.transform.localScale = Vector3.one * Random.Range(0.05f, 0.12f);
                flakes[i] = sr;
                speeds[i] = Random.Range(0.12f, 0.35f);
                phases[i] = Random.Range(0f, Mathf.PI * 2f);
            }
        }

        private static Vector3 RandomStart(bool anywhere)
        {
            float x = Random.Range(-BoardSpec.Width * 0.5f - 1f, BoardSpec.Width * 0.5f + 1f);
            float top = BoardLayout.BoardTopY + 1.5f;
            float y = anywhere ? Random.Range(BoardLayout.BoardBottomY - 1f, top) : top;
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
                pos.y -= speeds[i] * Time.deltaTime;
                pos.x += Mathf.Sin(time * 0.6f + phases[i]) * 0.08f * Time.deltaTime;
                if (pos.y < BoardLayout.BoardBottomY - 1.5f)
                {
                    pos = RandomStart(anywhere: false);
                }

                flakes[i].transform.position = pos;
            }
        }
    }
}
