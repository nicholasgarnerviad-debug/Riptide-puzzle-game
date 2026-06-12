using System.Collections;
using System.Collections.Generic;
using Riptide.Core;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// Spec §5.1 water: a translucent body under two sine-displaced surface layers
    /// (12px/9s back, 8px/6s front at half phase) with a foam line riding the front,
    /// caustic noise over the submerged area (12%, 16s scroll), rise surge with 6px
    /// overshoot, the over-delivered drain (droplets, per-row sparkle, foam flare,
    /// multi-row edge pulse) and an 800ms calm→danger gradient crossfade.
    /// Geometry constants cite §5.1; colors/durations come from ui_theme tokens.
    /// </summary>
    public sealed class WaterView : MonoBehaviour
    {
        private const int SurfaceColumns = 18;
        private const float BackAmpRefPx = 12f;   // §5.1
        private const float BackPeriod = 9f;      // §5.1
        private const float FrontAmpRefPx = 8f;   // §5.1
        private const float FrontPeriod = 6f;     // §5.1
        private const float CausticAlpha = 0.12f; // §5.1
        private const float CausticPeriod = 16f;  // §5.1
        private const float OvershootRefPx = 6f;  // §5.1 rise overshoot
        private const int MaxDroplets = 16;       // §5.1 drain droplets

        private SpriteRenderer body = null!;
        private SpriteRenderer caustic = null!;
        private readonly SpriteRenderer[] backCols = new SpriteRenderer[SurfaceColumns];
        private readonly SpriteRenderer[] frontCols = new SpriteRenderer[SurfaceColumns];
        private readonly SpriteRenderer[] foamCols = new SpriteRenderer[SurfaceColumns];

        private float currentLevel;
        private float driftTime;
        private bool danger;
        private float dangerBlend;
        private float foamBoost;

        /// <summary>The level the view is showing right now (animated float; tests read this).</summary>
        public float CurrentLevel => currentLevel;

        /// <summary>Tests: 1 = fully in the danger gradient, 0 = calm.</summary>
        public float DangerBlend => dangerBlend;

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
            var bodyGo = new GameObject("body");
            bodyGo.transform.SetParent(transform, false);
            body = bodyGo.AddComponent<SpriteRenderer>();
            body.sprite = SpriteFactory.Solid();
            body.sortingOrder = 51;

            var causticGo = new GameObject("caustic");
            causticGo.transform.SetParent(transform, false);
            caustic = causticGo.AddComponent<SpriteRenderer>();
            caustic.sprite = SpriteFactory.Caustic();
            caustic.drawMode = SpriteDrawMode.Tiled;
            caustic.sortingOrder = 52;

            float colWidth = (BoardSpec.Width + 1f) / SurfaceColumns;
            for (int i = 0; i < SurfaceColumns; i++)
            {
                backCols[i] = BuildColumn($"back_{i}", colWidth, 0.34f, 53);
                frontCols[i] = BuildColumn($"front_{i}", colWidth, 0.26f, 54);
                foamCols[i] = BuildColumn($"foam_{i}", colWidth * 0.92f, 0.055f, 55);
            }

            SetLevelInstant(startLevel);
            ApplyColors();
        }

        private SpriteRenderer BuildColumn(string name, float width, float height, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(width, height, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Solid();
            sr.sortingOrder = order;
            return sr;
        }

        public void SetLevelInstant(float level)
        {
            currentLevel = level;
            ApplyLevel();
        }

        /// <summary>Driver-wired (§6.1 state-driven): danger gradient target from state.</summary>
        public void SetDanger(bool inDanger)
        {
            danger = inDanger;
        }

        private void ApplyLevel()
        {
            float bottom = BoardLayout.BoardBottomY;
            float waterline = BoardLayout.WaterlineY(currentLevel);
            float depth = Mathf.Max(0f, waterline - bottom);
            bool visible = currentLevel > 0.01f;

            body.enabled = visible;
            caustic.enabled = visible;
            if (visible)
            {
                body.transform.position = new Vector3(0f, bottom + depth * 0.5f, 0f);
                body.transform.localScale = new Vector3(BoardSpec.Width + 1f, depth, 1f);
                caustic.size = new Vector2(BoardSpec.Width + 1f, depth);
                float scroll = Mathf.Repeat(driftTime / CausticPeriod, 1f);
                caustic.transform.position = new Vector3(scroll - 0.5f, waterline, 0f);
            }

            float colWidth = (BoardSpec.Width + 1f) / SurfaceColumns;
            float backAmp = ThemeRuntime.WorldFromRefPx(BackAmpRefPx);
            float frontAmp = ThemeRuntime.WorldFromRefPx(FrontAmpRefPx);
            for (int i = 0; i < SurfaceColumns; i++)
            {
                float x = -(BoardSpec.Width + 1f) * 0.5f + colWidth * (i + 0.5f);
                float phase = i * 0.7f;
                float backY = waterline + backAmp * Mathf.Sin(driftTime * (2f * Mathf.PI / BackPeriod) + phase);
                float frontY = waterline + frontAmp * Mathf.Sin(driftTime * (2f * Mathf.PI / FrontPeriod) + phase + Mathf.PI);
                backCols[i].transform.position = new Vector3(x, backY, 0f);
                frontCols[i].transform.position = new Vector3(x, frontY, 0f);
                foamCols[i].transform.position = new Vector3(x, frontY + 0.03f, 0f);
                backCols[i].enabled = visible;
                frontCols[i].enabled = visible;
                foamCols[i].enabled = visible;
            }
        }

        private void ApplyColors()
        {
            Color calmTop = ThemeRuntime.Color("water.calm.top");
            Color calmBtm = ThemeRuntime.Color("water.calm.btm");
            Color dangerTop = ThemeRuntime.Color("water.danger.top");
            Color dangerBtm = ThemeRuntime.Color("water.danger.btm");
            Color top = Color.Lerp(calmTop, dangerTop, dangerBlend);
            Color btm = Color.Lerp(calmBtm, dangerBtm, dangerBlend);

            body.color = btm;
            caustic.color = new Color(1f, 1f, 1f, CausticAlpha);
            Color foam = ThemeRuntime.Color("water.foamLine");
            foam = Color.Lerp(foam, ThemeRuntime.Color("danger"), dangerBlend * 0.6f);
            foam.a = Mathf.Clamp01(foam.a * (1f + foamBoost));

            foreach (SpriteRenderer sr in backCols)
            {
                sr.color = new Color(top.r, top.g, top.b, top.a * 0.8f);
            }

            foreach (SpriteRenderer sr in frontCols)
            {
                sr.color = top;
            }

            foreach (SpriteRenderer sr in foamCols)
            {
                sr.color = foam;
            }
        }

        private void Update()
        {
            driftTime += Time.deltaTime;

            // §5.1 danger crossfade over t.dangerFade; foam pulses while in danger.
            float fadeSeconds = Mathf.Max(0.01f, ThemeRuntime.Seconds("t.dangerFade"));
            dangerBlend = Mathf.MoveTowards(dangerBlend, danger ? 1f : 0f, Time.deltaTime / fadeSeconds);
            if (danger)
            {
                foamBoost = Mathf.Max(foamBoost, 0.25f + 0.25f * Mathf.Sin(driftTime * 4f));
            }

            foamBoost = Mathf.MoveTowards(foamBoost, 0f, Time.deltaTime * 1.5f);
            ApplyLevel();
            ApplyColors();
        }

        /// <summary>§5.1 rise: t.rise surge up one row, 6px overshoot, settle; foam brightens.
        /// Reduced motion (§1.4): simple lerp — no overshoot.</summary>
        public IEnumerator AnimateRise(float toLevel)
        {
            foamBoost = 0.5f;
            float overshoot = ThemeRuntime.ReducedMotion
                ? 0f
                : ThemeRuntime.WorldFromRefPx(OvershootRefPx) / BoardLayout.CellSize;
            yield return AnimateTo(toLevel, UiEventQueue.RiseSeconds(), easeIn: true, overshoot);
        }

        /// <summary>§5.1 drain: recede + droplets + per-row sparkle; multi-row stretches and edge-pulses.
        /// Reduced motion (§1.4): simple lerp — no particles, no edge pulse.</summary>
        public IEnumerator AnimateDrain(float toLevel, int rowsDrained)
        {
            bool multi = rowsDrained > 1;
            float seconds = UiEventQueue.DrainSeconds(multi);
            if (!ThemeRuntime.ReducedMotion)
            {
                SpawnDroplets(Mathf.Min(MaxDroplets, 6 + rowsDrained * 5));
                for (int row = 0; row < rowsDrained; row++)
                {
                    SpawnRowSparkles(currentLevel - row - 0.5f);
                }

                if (multi)
                {
                    StartCoroutine(EdgePulse());
                }
            }

            foamBoost = 0.6f;
            yield return AnimateTo(toLevel, seconds, easeIn: false, 0f);
        }

        private IEnumerator AnimateTo(float toLevel, float duration, bool easeIn, float overshoot)
        {
            float from = currentLevel;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / duration);
                float eased = easeIn ? u * u : 1f - (1f - u) * (1f - u);
                currentLevel = Mathf.Lerp(from, toLevel, eased)
                    + overshoot * Mathf.Sin(Mathf.PI * Mathf.Clamp01(u));
                ApplyLevel();
                yield return null;
            }

            currentLevel = toLevel;
            ApplyLevel();
        }

        // §9: particles pooled — drains reuse renderers instead of allocating.
        private readonly Stack<SpriteRenderer> particlePool = new Stack<SpriteRenderer>();

        private SpriteRenderer RentParticle()
        {
            if (particlePool.Count > 0)
            {
                SpriteRenderer pooled = particlePool.Pop();
                pooled.gameObject.SetActive(true);
                return pooled;
            }

            var go = new GameObject("waterParticle");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Dot();
            sr.sortingOrder = 60;
            return sr;
        }

        private void ReturnParticle(SpriteRenderer sr)
        {
            sr.gameObject.SetActive(false);
            particlePool.Push(sr);
        }

        private void SpawnDroplets(int count)
        {
            float waterlineY = BoardLayout.WaterlineY(currentLevel);
            for (int i = 0; i < count; i++)
            {
                SpriteRenderer sr = RentParticle();
                float x = Random.Range(-BoardSpec.Width * 0.5f, BoardSpec.Width * 0.5f);
                sr.transform.position = new Vector3(x, waterlineY + Random.Range(0f, 0.2f), 0f);
                sr.transform.localScale = Vector3.one * Random.Range(0.12f, 0.24f);
                sr.color = ThemeRuntime.Color("water.foamLine");
                StartCoroutine(DropletLife(sr, Random.Range(0.35f, 0.55f)));
            }
        }

        private IEnumerator DropletLife(SpriteRenderer sr, float life)
        {
            float t = 0f;
            Vector3 start = sr.transform.position;
            float vx = Random.Range(-0.4f, 0.4f);
            while (t < life)
            {
                t += Time.deltaTime;
                float u = t / life;
                sr.transform.position = start + new Vector3(vx * u, -2.2f * u * u, 0f);
                Color c = sr.color;
                c.a = 1f - u;
                sr.color = c;
                yield return null;
            }

            ReturnParticle(sr);
        }

        private void SpawnRowSparkles(float atLevel)
        {
            float y = BoardLayout.WaterlineY(Mathf.Max(0f, atLevel));
            for (int i = 0; i < 6; i++)
            {
                SpriteRenderer sr = RentParticle();
                sr.color = ThemeRuntime.Color("water.foamLine");
                sr.transform.position = new Vector3((i - 2.5f) * 1.5f + Random.Range(-0.4f, 0.4f),
                    y + Random.Range(-0.1f, 0.25f), 0f);
                sr.transform.localScale = Vector3.one * Random.Range(0.2f, 0.42f);
                StartCoroutine(DropletLife(sr, 0.5f));
            }
        }

        /// <summary>§5.1 multi-row drain: brief cyan pulse along the screen edges.</summary>
        private IEnumerator EdgePulse()
        {
            Color tint = ThemeRuntime.Color("accent.primary");
            var strips = new SpriteRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject($"edgePulse_{i}");
                go.transform.SetParent(transform, false);
                float x = (i == 0 ? -1f : 1f) * (BoardSpec.Width * 0.5f + 0.9f);
                go.transform.position = new Vector3(x, (BoardLayout.BoardBottomY + BoardLayout.BoardTopY) * 0.5f, 0f);
                go.transform.localScale = new Vector3(0.5f, BoardSpec.Height + 3f, 1f);
                strips[i] = go.AddComponent<SpriteRenderer>();
                strips[i].sprite = SpriteFactory.Solid();
                strips[i].sortingOrder = 95;
            }

            float t = 0f;
            float life = ThemeRuntime.Seconds("t.drainMulti");
            while (t < life)
            {
                t += Time.deltaTime;
                float pulse = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / life));
                foreach (SpriteRenderer sr in strips)
                {
                    sr.color = new Color(tint.r, tint.g, tint.b, 0.22f * pulse);
                }

                yield return null;
            }

            foreach (SpriteRenderer sr in strips)
            {
                Destroy(sr.gameObject);
            }
        }
    }
}
