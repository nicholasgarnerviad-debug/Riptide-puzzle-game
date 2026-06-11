using System.Collections;
using Riptide.Core;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// GDD 7.1 water: two translucent caustic layers drifting at different rates,
    /// a foam line at the waterline, rise surge (350ms ease-in) and the hero drain
    /// recede (450ms ease-out + sparkles). Danger pulse at water >= 7 (GDD 2.2/7.3).
    /// </summary>
    public sealed class WaterView : MonoBehaviour
    {
        private SpriteRenderer layerDeep = null!;
        private SpriteRenderer layerShallow = null!;
        private SpriteRenderer foam = null!;
        private float currentLevel;
        private float driftTime;

        /// <summary>The level the view is showing right now (animated float; tests read this).</summary>
        public float CurrentLevel => currentLevel;

        public static WaterView Create(Transform parent, float startLevel)
        {
            var go = new GameObject("WaterView");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<WaterView>();
            view.Build(startLevel);
            return view;
        }

        private void Build(float startLevel)
        {
            layerDeep = BuildLayer("deep", Palette.WaterDeep, 51);
            layerShallow = BuildLayer("shallow", Palette.WaterShallow, 52);

            var foamGo = new GameObject("foam");
            foamGo.transform.SetParent(transform, false);
            foam = foamGo.AddComponent<SpriteRenderer>();
            foam.sprite = SpriteFactory.Solid();
            foam.color = Palette.FoamLine;
            foam.sortingOrder = 53;
            foamGo.transform.localScale = new Vector3(BoardSpec.Width + 4f, 0.06f, 1f);

            SetLevelInstant(startLevel);
        }

        private SpriteRenderer BuildLayer(string name, Color tint, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Caustic();
            sr.color = tint;
            sr.sortingOrder = order;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(BoardSpec.Width + 6f, BoardSpec.MaxWaterLevel + 2f);
            return sr;
        }

        public void SetLevelInstant(float level)
        {
            currentLevel = level;
            ApplyLevel();
        }

        private void ApplyLevel()
        {
            float waterlineY = BoardLayout.WaterlineY(currentLevel);
            layerDeep.transform.position = new Vector3(0f, waterlineY, 0f);
            layerShallow.transform.position = new Vector3(0.3f, waterlineY, 0f);
            foam.transform.position = new Vector3(0f, waterlineY, 0f);
            bool visible = currentLevel > 0.01f;
            layerDeep.enabled = visible;
            layerShallow.enabled = visible;
            foam.enabled = visible;
        }

        private void Update()
        {
            driftTime += Time.deltaTime;
            float driftA = Mathf.Sin(driftTime * 0.31f) * 0.45f;
            float driftB = Mathf.Sin(driftTime * 0.21f + 2.1f) * 0.7f;
            Vector3 deepPos = layerDeep.transform.position;
            layerDeep.transform.position = new Vector3(driftA, deepPos.y, 0f);
            Vector3 shallowPos = layerShallow.transform.position;
            layerShallow.transform.position = new Vector3(0.3f + driftB, shallowPos.y, 0f);

            // GDD 2.2/7.3: escalating danger read at water 7+ — the foam pulses red.
            if (currentLevel >= 7f)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(driftTime * 6f);
                foam.color = Color.Lerp(Palette.FoamLine, Palette.DangerPulse, pulse);
            }
            else
            {
                foam.color = Palette.FoamLine;
            }
        }

        /// <summary>GDD 7.1: rise surge, 350ms ease-in.</summary>
        public IEnumerator AnimateRise(float toLevel)
        {
            yield return AnimateTo(toLevel, 0.35f, easeIn: true);
        }

        /// <summary>GDD 7.1: the hero drain — 450ms recede + sparkle burst.</summary>
        public IEnumerator AnimateDrain(float toLevel)
        {
            SpawnSparkles();
            yield return AnimateTo(toLevel, 0.45f, easeIn: false);
        }

        private IEnumerator AnimateTo(float toLevel, float duration, bool easeIn)
        {
            float from = currentLevel;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / duration);
                float eased = easeIn ? u * u : 1f - (1f - u) * (1f - u);
                currentLevel = Mathf.Lerp(from, toLevel, eased);
                ApplyLevel();
                yield return null;
            }

            currentLevel = toLevel;
            ApplyLevel();
        }

        private void SpawnSparkles()
        {
            float waterlineY = BoardLayout.WaterlineY(currentLevel);
            for (int i = 0; i < 8; i++)
            {
                var go = new GameObject("sparkle");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Dot();
                sr.color = Palette.FoamLine;
                sr.sortingOrder = 60;
                float x = (i - 3.5f) * 1.15f + Random.Range(-0.3f, 0.3f);
                go.transform.position = new Vector3(x, waterlineY + Random.Range(-0.1f, 0.25f), 0f);
                go.transform.localScale = Vector3.one * Random.Range(0.25f, 0.5f);
                StartCoroutine(SparkleLife(go, sr));
            }
        }

        private IEnumerator SparkleLife(GameObject go, SpriteRenderer sr)
        {
            float t = 0f;
            const float life = 0.5f;
            Vector3 start = go.transform.position;
            while (t < life)
            {
                t += Time.deltaTime;
                float u = t / life;
                go.transform.position = start + new Vector3(0f, u * 0.6f, 0f);
                Color c = sr.color;
                c.a = 1f - u;
                sr.color = c;
                yield return null;
            }

            Destroy(go);
        }
    }
}
