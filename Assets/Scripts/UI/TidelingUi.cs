using System;
using System.Globalization;
using Riptide.Core;
using Riptide.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// GDD §16.6 shared Tidelings UI: tier tones, form art, ability wording, the
    /// layered Conch, sea eggs, the egg pill and the egg pips. Every screen that
    /// shows a Tideling (the tab, the Conch, reveals, growing up, Home, Results)
    /// draws it through here so a new line in pals.json needs no UI change.
    /// </summary>
    public static class TidelingUi
    {
        /// <summary>The card / reveal tone for a tier: Common teal · Rare blue · Epic purple · Legendary gold.</summary>
        public static PopTone Tone(PalRarity tier) => tier switch
        {
            PalRarity.Common => PopTone.Teal,
            PalRarity.Rare => PopTone.Blue,
            PalRarity.Epic => PopTone.Purple,
            _ => PopTone.Yellow,
        };

        /// <summary>The form as owned now (stage, Sparkle if it is switched on).</summary>
        public static Sprite Art(TidelingView t, bool closed = false) =>
            CreatureSprites.Tideling(t.Line.Key, Math.Max(1, t.Stage), t.Sparkle && t.SparkleOn, closed);

        /// <summary>The unmet look: the Baby's silhouette, tinted at use.</summary>
        public static Sprite Silhouette(PalLine line) => CreatureSprites.TidelingSilhouette(line.Key, 1);

        public static string TierName(GameFlow flow, PalRarity tier) => ExpansionText.TierName(flow, tier);

        public static string StageName(GameFlow flow, int stage) =>
            flow.Strings.Get("stage." + Mathf.Clamp(stage, 1, PalsLoader.StageCount));

        public static string PerkKey(PalPerk perk)
        {
            string name = perk.ToString();
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        public static string AbilityName(GameFlow flow, PalLine line) =>
            flow.Strings.Get($"perk.{PerkKey(line.Perk)}.name");

        /// <summary>The ability at a stage, card-short ("+12% coins").</summary>
        public static string AbilityShort(GameFlow flow, PalLine line, int stage) => Ability(flow, line, stage, "short");

        /// <summary>The ability at a stage, in a sentence.</summary>
        public static string AbilityDesc(GameFlow flow, PalLine line, int stage) => Ability(flow, line, stage, "desc");

        private static string Ability(GameFlow flow, PalLine line, int stage, string kind)
        {
            int s = Mathf.Clamp(stage, 1, PalsLoader.StageCount);
            int value = line.PerkAt(s);
            string first = line.Perk == PalPerk.BondBoost
                ? (value / 100f).ToString("0.#", CultureInfo.InvariantCulture)
                : value.ToString(CultureInfo.InvariantCulture);
            return string.Format(flow.Strings.Get($"perk.{PerkKey(line.Perk)}.{kind}"), first, line.Perk2At(s));
        }

        /// <summary>"12 eggs" / "1 egg".</summary>
        public static string Eggs(GameFlow flow, int count) =>
            string.Format(flow.Strings.Get(count == 1 ? "conch.egg" : "conch.eggs"), count);

        // ------------------------------------------------------------- art pieces

        /// <summary>
        /// The Conch, stacked from its masks (outline, shell, bands, lip, inner lip,
        /// glint) in one holder <paramref name="size"/> square — the mockup's conch
        /// in theme colors.
        /// </summary>
        public static RectTransform Conch(RectTransform parent, string name, float size)
        {
            RectTransform holder = UiComponents.Rect(parent, name, new Vector2(size, size));
            UiComponents.Place(holder, new Vector2(0.5f, 0.5f), new Vector2(size, size));
            Layer(holder, "outline", PopSprites.ShellOutline("conchShell", 7), "conch.edge", size * PopSprites.ShellOutlineScale(7));
            Layer(holder, "shell", PopSprites.Shell("conchShell"), "conch", size);
            Layer(holder, "bands", PopSprites.Shell("conchBands"), "conch.shade", size);
            Layer(holder, "lipEdge", PopSprites.ShellOutline("conchLip", 5), "pop.coral.edge", size * PopSprites.ShellOutlineScale(5));
            Layer(holder, "lip", PopSprites.Shell("conchLip"), "conch.lip", size);
            Layer(holder, "lipIn", PopSprites.Shell("conchLipIn"), "conch.lipInner", size);
            Layer(holder, "glint", PopSprites.Shell("conchGlint"), "shine", size);
            return holder;
        }

        /// <summary>A wave-banded sea egg (GDD §16.8: never a white egg with spots).</summary>
        public static RectTransform Egg(RectTransform parent, string name, float size)
        {
            RectTransform holder = UiComponents.Rect(parent, name, new Vector2(size, size));
            UiComponents.Place(holder, new Vector2(0.5f, 0.5f), new Vector2(size, size));
            Layer(holder, "outline", PopSprites.IconOutline("egg", 9), "egg.edge", size * PopSprites.OutlineScale(9));
            Layer(holder, "fill", PopSprites.Icon("egg"), "egg", size);
            return holder;
        }

        /// <summary>Navy pill: a sea egg and the count. Returns the count label (its parent is the pill).</summary>
        public static TextMeshProUGUI EggPill(RectTransform parent, string name, Vector2 size)
        {
            RectTransform pill = PopKit.Panel(parent, name, size, PopTone.Navy, size.y * 0.5f, 8f);
            RectTransform egg = Egg(pill, "egg", size.y * 0.74f);
            egg.anchorMin = egg.anchorMax = new Vector2(0f, 0.5f);
            egg.anchoredPosition = new Vector2(size.y * 0.5f, 2f);
            TextMeshProUGUI count = UiText.CreateOutlined(pill, "count", "0", "heading", "text.primary", "panel.edge", 0.2f);
            UiComponents.Stretch(count.rectTransform);
            count.rectTransform.offsetMin = new Vector2(size.y * 0.86f, 0f);
            count.rectTransform.offsetMax = new Vector2(-size.y * 0.22f, 0f);
            return count;
        }

        /// <summary>A row of egg-progress pips ("3 of 5 boards") — drive it with <see cref="EggPips.Set"/>.</summary>
        public static EggPips Pips(RectTransform parent, string name, int total, float dot)
        {
            float gap = dot * 0.3f;
            RectTransform row = UiComponents.Rect(parent, name, new Vector2(total * dot + (total - 1) * gap, dot));
            var pips = row.gameObject.AddComponent<EggPips>();
            pips.Build(row, total, dot, gap);
            return pips;
        }

        internal static Image Layer(RectTransform holder, string name, Sprite sprite, string token, float size)
        {
            Image image = PopKit.Img(holder, name, sprite, token);
            UiComponents.Place(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(size, size));
            return image;
        }
    }

    /// <summary>Egg-progress pips: a navy ring per board, filled sea-green up to the boards played.</summary>
    public sealed class EggPips : MonoBehaviour
    {
        private Image[] fills = Array.Empty<Image>();

        public int Filled { get; private set; }

        internal void Build(RectTransform row, int total, float dot, float gap)
        {
            fills = new Image[total];
            for (int i = 0; i < total; i++)
            {
                Image ring = PopKit.Img(row, $"pip{i}", PopSprites.Circle(), "panel.edgeDeep");
                ring.rectTransform.anchorMin = ring.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                ring.rectTransform.pivot = new Vector2(0f, 0.5f);
                ring.rectTransform.sizeDelta = new Vector2(dot, dot);
                ring.rectTransform.anchoredPosition = new Vector2(i * (dot + gap), 0f);
                fills[i] = PopKit.Img(ring.rectTransform, "fill", PopSprites.Circle(), "pop.locked");
                UiComponents.Place(fills[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(dot * 0.62f, dot * 0.62f));
            }
        }

        public void Set(int filled)
        {
            Filled = Mathf.Clamp(filled, 0, fills.Length);
            for (int i = 0; i < fills.Length; i++)
            {
                ThemedElement.Bind(fills[i].gameObject, i < Filled ? "egg.band" : "pop.locked");
            }
        }
    }
}
