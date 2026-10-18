using System.Collections.Generic;
using Riptide.Core;
using Riptide.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// GDD §16.5 the Tide Journal card under the diorama (mockups B + C): its totals
    /// ("12/16 met · 21/48 forms"), one tab per tier ("Epic 2/3") and that tier's
    /// tier-framed cards in a sideways strip — met forms with their ability, the rest
    /// as silhouettes saying how to meet them. A card opens the Tideling's card sheet.
    /// </summary>
    public sealed partial class TidepoolScreen
    {
        private const float JournalCardWidth = 262f;
        private const float JournalCardGap = 22f;

        private RectTransform journal = null!;
        private TextMeshProUGUI journalCount = null!;
        private RectTransform strip = null!;
        private ScrollRect stripScroll = null!;
        private readonly List<(PalRarity tier, Button tab, TextMeshProUGUI label)> tierTabs
            = new List<(PalRarity, Button, TextMeshProUGUI)>();
        private readonly List<TidelingCard> journalCards = new List<TidelingCard>();
        private PalRarity shownTier = PalRarity.Common;

        /// <summary>The tier the Journal shows. Test surface.</summary>
        public PalRarity JournalTier => shownTier;

        private void BuildJournal(RectTransform root)
        {
            journal = BottomCard(root, "journal");

            TextMeshProUGUI heading = UiText.Create(journal, "heading", flow.Strings.Get("tl.journal"), "heading",
                "text.ink", TextAlignmentOptions.MidlineLeft);
            heading.rectTransform.anchorMin = heading.rectTransform.anchorMax = new Vector2(0f, 1f);
            heading.rectTransform.pivot = new Vector2(0f, 0.5f);
            heading.rectTransform.sizeDelta = new Vector2(440f, 60f);
            heading.rectTransform.anchoredPosition = new Vector2(44f, -60f);

            journalCount = UiText.Create(journal, "count", "", "caption", "text.inkSoft", TextAlignmentOptions.MidlineRight);
            journalCount.rectTransform.anchorMin = journalCount.rectTransform.anchorMax = new Vector2(1f, 1f);
            journalCount.rectTransform.pivot = new Vector2(1f, 0.5f);
            journalCount.rectTransform.sizeDelta = new Vector2(520f, 60f);
            journalCount.rectTransform.anchoredPosition = new Vector2(-44f, -60f);

            PalRarity[] tiers = { PalRarity.Common, PalRarity.Rare, PalRarity.Epic, PalRarity.Legendary };
            for (int i = 0; i < tiers.Length; i++)
            {
                PalRarity tier = tiers[i];
                Button tab = PopKit.Button(journal, $"tier{tier}", "", PopTone.White, () => SelectTier(tier),
                    new Vector2(222f, 100f), "caption");
                var tabRt = (RectTransform)tab.transform;
                tabRt.anchorMin = tabRt.anchorMax = new Vector2(0.5f, 1f);
                tabRt.anchoredPosition = new Vector2((i - 1.5f) * 232f, -158f);
                TextMeshProUGUI label = PopKit.FaceOf(tab).Find("label").GetComponent<TextMeshProUGUI>();
                label.enableAutoSizing = true;
                label.fontSizeMin = 18f;
                label.fontSizeMax = label.fontSize;
                label.rectTransform.offsetMin = new Vector2(14f, 0f);
                label.rectTransform.offsetMax = new Vector2(-14f, 0f);
                tierTabs.Add((tier, tab, label));
            }

            // The tier's cards, a sideways strip (tiers will grow as Tidelings are added).
            RectTransform viewport = UiComponents.Rect(journal, "strip", Vector2.zero);
            UiComponents.Stretch(viewport);
            viewport.offsetMin = new Vector2(PopKit.Border + 14f, PopKit.Lip + 14f);
            viewport.offsetMax = new Vector2(-(PopKit.Border + 14f), -214f);
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.sprite = SpriteFactory.Solid();
            hit.color = new Color(0f, 0f, 0f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            stripScroll = viewport.gameObject.AddComponent<ScrollRect>();
            stripScroll.horizontal = true;
            stripScroll.vertical = false;
            stripScroll.movementType = ScrollRect.MovementType.Clamped;
            strip = UiComponents.Rect(viewport, "content", Vector2.zero);
            strip.anchorMin = new Vector2(0f, 0f);
            strip.anchorMax = new Vector2(0f, 1f);
            strip.pivot = new Vector2(0f, 0.5f);
            stripScroll.content = strip;

            for (int line = 0; line < flow.Pals.Lines.Count; line++)
            {
                int lineCopy = line;
                TidelingCard card = TidelingCard.Create(strip, $"card_{flow.Pals.Lines[line].Key}",
                    new Vector2(JournalCardWidth, 368f), abilityPlate: true, () => OpenSheet(lineCopy));
                card.Rect.anchorMin = card.Rect.anchorMax = new Vector2(0f, 0.5f);
                card.Rect.pivot = new Vector2(0f, 0.5f);
                journalCards.Add(card);
            }
        }

        private void SelectTier(PalRarity tier)
        {
            if (shownTier != tier)
            {
                shownTier = tier;
                strip.anchoredPosition = Vector2.zero;
            }

            RefreshJournal();
        }

        private void RefreshJournal()
        {
            (int owned, int forms) = flow.JournalCounts();
            int lines = flow.Pals.Lines.Count;
            journalCount.text = string.Format(flow.Strings.Get("tl.journalCount"), owned, lines, forms,
                lines * PalsLoader.StageCount);

            foreach ((PalRarity tier, Button tab, TextMeshProUGUI label) in tierTabs)
            {
                IReadOnlyList<int> ofTier = flow.Pals.LinesOfTier(tier);
                int met = 0;
                foreach (int line in ofTier)
                {
                    met += flow.Tideling(line).Owned ? 1 : 0;
                }

                bool selected = tier == shownTier;
                PopTone tone = selected ? TidelingUi.Tone(tier) : PopTone.White;
                tab.GetComponent<PopSurface>().SetTone(tone);
                label.text = $"{TidelingUi.TierName(flow, tier)} {met}/{ofTier.Count}";
                ThemedElement.Bind(label.gameObject, PopKit.LabelColor(tone));
                if (tone == PopTone.White)
                {
                    label.fontSharedMaterial = label.font.material;
                }
                else
                {
                    UiText.Outline(label, PopKit.Edge(tone));
                }
            }

            int shown = 0;
            foreach (TidelingCard card in journalCards)
            {
                int line = journalCards.IndexOf(card);
                TidelingView t = flow.Tideling(line);
                bool inTier = t.Line.Rarity == shownTier;
                card.gameObject.SetActive(inTier);
                if (!inTier)
                {
                    continue;
                }

                card.Set(flow, t);
                card.Rect.anchoredPosition = new Vector2(shown * (JournalCardWidth + JournalCardGap), 0f);
                shown++;
            }

            strip.sizeDelta = new Vector2(Mathf.Max(0f, shown * (JournalCardWidth + JournalCardGap) - JournalCardGap), 0f);
        }
    }
}
