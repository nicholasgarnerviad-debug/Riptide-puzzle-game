using System.Collections.Generic;
using Riptide.Core;
using Riptide.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// Growing up (GDD §16.2, §16.6; mockup C): the Tideling spins up out of a
    /// whirlpool of bubbles in its tier's colors and lands as its new form's card with
    /// confetti — "Mossback grew up!", the before → after, its ability going up, and
    /// the Journal's line reward when all three forms are seen. Never a white
    /// silhouette morph (§16.8).
    /// </summary>
    public sealed class GrowUpCelebration : Overlay
    {
        private const float CardBasis = 800f;
        private const float BottomStack = 860f;

        private static readonly string[] ConfettiTokens =
            { "pop.coral", "pop.yellow", "pop.blue.top", "pop.green.top", "pop.purple.top", "pop.white.top" };

        private GameFlow flow = null!;
        private RectTransform swirl = null!;
        private RectTransform cardArea = null!;
        private RectTransform holder = null!;
        private TidelingCard card = null!;
        private TextMeshProUGUI ribbonLabel = null!;
        private Image beforeArt = null!;
        private Image afterArt = null!;
        private TextMeshProUGUI beforeName = null!;
        private TextMeshProUGUI afterName = null!;
        private TextMeshProUGUI abilityName = null!;
        private TextMeshProUGUI abilityChange = null!;
        private GameObject linePill = null!;
        private TextMeshProUGUI lineLabel = null!;
        private readonly List<(RectTransform rt, float speed, float spin)> confetti =
            new List<(RectTransform, float, float)>();
        private float laidOut = -1f;

        /// <summary>The line shown growing up. Test surface.</summary>
        public int Line { get; private set; } = -1;

        public static GrowUpCelebration Build(RectTransform canvasRoot, GameFlow flow)
        {
            GrowUpCelebration grow = Create<GrowUpCelebration>(canvasRoot, "GrowUp", "pop.teal.top", "pop.teal.edge");
            grow.flow = flow;
            RectTransform root = grow.Content;

            grow.swirl = UiComponents.Rect(root, "swirl", new Vector2(1700f, 1700f));
            grow.swirl.anchorMin = grow.swirl.anchorMax = new Vector2(0.5f, 1f);
            grow.swirl.anchoredPosition = new Vector2(0f, -640f);
            Image swirlImage = PopKit.Img(grow.swirl, "arms", PopSprites.Icon("swirl"), "burst");
            UiComponents.Stretch(swirlImage.rectTransform);
            for (int i = 0; i < 18; i++)
            {
                float t = i / 18f;
                float ang = t * 4.4f + 0.5f;
                float r = 160f + t * 640f;
                Image b = PopKit.Img(grow.swirl, $"b{i}", PopSprites.Bubble(), "bubble");
                UiComponents.Place(b.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(30f + t * 60f, 30f + t * 60f));
                b.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);
            }

            uint seed = 7u;
            for (int i = 0; i < 44; i++)
            {
                seed = seed * 1664525u + 1013904223u;
                float x = (seed % 1000u) / 1000f;
                seed = seed * 1664525u + 1013904223u;
                float y = (seed % 1000u) / 1000f;
                Image piece = PopKit.Img(root, "confetti", SpriteFactory.Solid(), ConfettiTokens[i % ConfettiTokens.Length]);
                piece.rectTransform.anchorMin = piece.rectTransform.anchorMax = new Vector2(x, 1f);
                piece.rectTransform.sizeDelta = i % 3 == 0 ? new Vector2(22f, 22f) : new Vector2(30f, 14f);
                piece.rectTransform.anchoredPosition = new Vector2(0f, -y * 1500f);
                piece.rectTransform.localRotation = Quaternion.Euler(0f, 0f, (seed % 180u));
                grow.confetti.Add((piece.rectTransform, 120f + (seed % 140u), (seed % 2u == 0 ? 1f : -1f) * (60f + seed % 120u)));
            }

            RectTransform ribbon = PopKit.Ribbon(root, "ribbon", "", PopTone.Coral, new Vector2(820f, 120f), "title");
            ribbon.anchorMin = ribbon.anchorMax = new Vector2(0.5f, 1f);
            ribbon.anchoredPosition = new Vector2(0f, -130f);
            grow.ribbonLabel = ribbon.Find("band/face/label").GetComponent<TextMeshProUGUI>();
            grow.ribbonLabel.enableAutoSizing = true;
            grow.ribbonLabel.fontSizeMin = 30f;
            grow.ribbonLabel.fontSizeMax = grow.ribbonLabel.fontSize;

            grow.cardArea = UiComponents.Rect(root, "cardArea", Vector2.zero);
            grow.cardArea.anchorMin = Vector2.zero;
            grow.cardArea.anchorMax = Vector2.one;
            grow.cardArea.offsetMin = new Vector2(0f, BottomStack);
            grow.cardArea.offsetMax = new Vector2(0f, -220f);
            var cardSize = new Vector2(CardBasis * 0.75f, CardBasis);
            grow.holder = UiComponents.Rect(grow.cardArea, "holder", cardSize);
            UiComponents.Place(grow.holder, new Vector2(0.5f, 0.5f), cardSize);
            grow.card = TidelingCard.Create(grow.holder, "card", cardSize, abilityPlate: false, null);
            UiComponents.Place(grow.card.Rect, new Vector2(0.5f, 0.5f), cardSize);

            grow.BuildChange(root);

            Button yay = PopKit.Button(root, "yay", flow.Strings.Get("grow.yay"), PopTone.Green, grow.Close,
                new Vector2(980f, 150f), "title");
            var yayRt = (RectTransform)yay.transform;
            yayRt.anchorMin = yayRt.anchorMax = new Vector2(0.5f, 0f);
            yayRt.pivot = new Vector2(0.5f, 0f);
            yayRt.anchoredPosition = new Vector2(0f, 110f);
            return grow;
        }

        private void BuildChange(RectTransform root)
        {
            RectTransform pill = PopKit.Panel(root, "change", new Vector2(820f, 120f), PopTone.Navy, 60f, 8f);
            pill.anchorMin = pill.anchorMax = new Vector2(0.5f, 0f);
            pill.anchoredPosition = new Vector2(0f, 760f);
            beforeArt = Portrait(pill, "before", -340f);
            beforeName = Caption(pill, "beforeName", -260f, 250f);
            Image arrow = PopKit.Icon(pill, "arrow", "chevronRight", "pop.yellow", 56f, "pop.yellow.edge", 6);
            ((RectTransform)arrow.transform.parent).anchoredPosition = new Vector2(0f, 0f);
            afterArt = Portrait(pill, "after", 80f);
            afterName = Caption(pill, "afterName", 160f, 240f);

            RectTransform ability = UiComponents.Card(root, "ability", new Vector2(980f, 230f));
            ability.anchorMin = ability.anchorMax = new Vector2(0.5f, 0f);
            ability.pivot = new Vector2(0.5f, 0f);
            ability.anchoredPosition = new Vector2(0f, 420f);
            RectTransform face = ability.GetComponent<PopSurface>().Face;
            abilityName = UiText.Create(face, "name", "", "heading", "text.ink", TextAlignmentOptions.MidlineLeft);
            abilityName.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            abilityName.rectTransform.anchorMax = new Vector2(0.45f, 1f);
            abilityName.rectTransform.offsetMin = new Vector2(44f, -10f);
            abilityName.rectTransform.offsetMax = new Vector2(0f, -24f);
            abilityName.enableAutoSizing = true;
            abilityName.fontSizeMin = 24f;
            abilityName.fontSizeMax = abilityName.fontSize;
            abilityChange = UiText.Create(face, "change", "", "title", "pop.green.edge", TextAlignmentOptions.MidlineRight);
            abilityChange.rectTransform.anchorMin = new Vector2(0.4f, 0.12f);
            abilityChange.rectTransform.anchorMax = new Vector2(1f, 0.92f);
            abilityChange.rectTransform.offsetMin = new Vector2(0f, 0f);
            abilityChange.rectTransform.offsetMax = new Vector2(-40f, 0f);
            abilityChange.enableAutoSizing = true;
            abilityChange.fontSizeMin = 24f;
            abilityChange.fontSizeMax = abilityChange.fontSize;

            RectTransform line = PopKit.Panel(root, "lineDone", new Vector2(900f, 84f), PopTone.Yellow, 26f, 6f);
            line.anchorMin = line.anchorMax = new Vector2(0.5f, 0f);
            line.anchoredPosition = new Vector2(0f, 334f);
            lineLabel = UiText.CreateOutlined(line, "label", "", "body", "text.primary", "pop.yellow.edge");
            UiComponents.Stretch(lineLabel.rectTransform);
            linePill = line.gameObject;
        }

        private static Image Portrait(RectTransform pill, string name, float x)
        {
            Image ring = PopKit.Img(pill, name, PopSprites.Circle(), "panel.light");
            ring.rectTransform.anchorMin = ring.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            ring.rectTransform.sizeDelta = new Vector2(96f, 96f);
            ring.rectTransform.anchoredPosition = new Vector2(x, 0f);
            Image art = PopKit.Img(ring.rectTransform, "art", CreatureSprites.Tideling("crab", 1), "pop.white.top");
            art.preserveAspect = true;
            UiComponents.Place(art.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(90f, 90f));
            return art;
        }

        private static TextMeshProUGUI Caption(RectTransform pill, string name, float x, float width)
        {
            TextMeshProUGUI text = UiText.CreateOutlined(pill, name, "", "caption", "text.primary", "panel.edge", 0.2f,
                TextAlignmentOptions.MidlineLeft);
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            text.rectTransform.pivot = new Vector2(0f, 0.5f);
            text.rectTransform.sizeDelta = new Vector2(width, 80f);
            text.rectTransform.anchoredPosition = new Vector2(x, 0f);
            text.enableAutoSizing = true;
            text.fontSizeMin = 18f;
            text.fontSizeMax = text.fontSize;
            return text;
        }

        /// <summary>Plays the moment for a Tideling that just grew from <paramref name="fromStage"/>.</summary>
        public void Show(int line, int fromStage, bool lineReward)
        {
            Line = line;
            TidelingView t = flow.Tideling(line);
            PopTone tone = TidelingUi.Tone(t.Line.Rarity);
            SetBackdrop(PopKit.Top(tone), PopKit.Edge(tone));
            Open();

            string before = t.Line.Stages[Mathf.Clamp(fromStage - 1, 0, t.Line.Stages.Count - 1)];
            ribbonLabel.text = string.Format(flow.Strings.Get("grow.title"), before);
            card.Set(flow, t);
            beforeArt.sprite = CreatureSprites.Tideling(t.Line.Key, Mathf.Max(1, fromStage), t.Sparkle && t.SparkleOn);
            afterArt.sprite = TidelingUi.Art(t);
            beforeName.text = before;
            afterName.text = t.FormName;
            abilityName.text = TidelingUi.AbilityName(flow, t.Line);
            ThemedElement.Bind(abilityName.gameObject, PopKit.Edge(tone));
            abilityChange.text =
                $"<s><color=#{ConchScreen.Hex("text.inkSoft")}>{TidelingUi.AbilityShort(flow, t.Line, fromStage)}</color></s>  ›  "
                + TidelingUi.AbilityShort(flow, t.Line, t.Stage);
            linePill.SetActive(lineReward);
            lineLabel.text = string.Format(flow.Strings.Get("grow.lineDone"), flow.Pals.LineCompleteCoins);
            UiJuice.Play("streak");

            if (!ThemeRuntime.ReducedMotion)
            {
                RectTransform rt = card.Rect;
                Tween.Run(this, "t.screen", "easeOutBack", u =>
                {
                    rt.localScale = Vector3.one * u;
                    rt.localRotation = Quaternion.Euler(0f, 0f, (1f - u) * 200f);
                });
            }
        }

        private void Update()
        {
            float fit = cardArea.rect.height;
            if (fit > 1f && !Mathf.Approximately(fit, laidOut))
            {
                // The card takes the height left above the bottom stack (16:9 phones are short).
                laidOut = fit;
                holder.localScale = Vector3.one * Mathf.Min(1f, fit / CardBasis);
            }

            if (ThemeRuntime.ReducedMotion)
            {
                return;
            }

            swirl.localRotation = Quaternion.Euler(0f, 0f, Time.time * 30f);
            float height = Content.rect.height + 100f;
            foreach ((RectTransform rt, float speed, float spin) in confetti)
            {
                Vector2 p = rt.anchoredPosition;
                p.y -= speed * Time.deltaTime;
                if (p.y < -height)
                {
                    p.y = 40f;
                }

                rt.anchoredPosition = p;
                rt.Rotate(0f, 0f, spin * Time.deltaTime);
            }
        }
    }
}
