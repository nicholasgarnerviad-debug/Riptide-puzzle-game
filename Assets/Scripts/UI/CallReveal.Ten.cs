using System;
using System.Collections;
using System.Collections.Generic;
using Riptide.Core;
using Riptide.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// The ten-Call (GDD §16.3, mockup A): ten bubbles in their tiers' colors — NEW!,
    /// SPARKLE! or the bond each gained — popping in one after another, and a
    /// spotlight on the best of them (new first, then the rarest).
    /// </summary>
    public sealed partial class CallReveal
    {
        private void BuildTen(RectTransform root)
        {
            TextMeshProUGUI title = UiText.CreateOutlined(root, "title", flow.Strings.Get("reveal.ten"), "display",
                "text.primary", "panel.edge", 0.3f);
            title.fontSize = 80f;
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title.rectTransform.sizeDelta = new Vector2(1000f, 110f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -140f);
            tenSub = UiText.CreateOutlined(root, "sub", "", "body", "text.primary", "panel.edge", 0.2f);
            tenSub.rectTransform.anchorMin = tenSub.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            tenSub.rectTransform.sizeDelta = new Vector2(1000f, 60f);
            tenSub.rectTransform.anchoredPosition = new Vector2(0f, -226f);

            tenGroup = Group(root, "block", 300f, 270f, new Vector2(1000f, TenBasis));
            for (int i = 0; i < 10; i++)
            {
                RectTransform cell = UiComponents.Rect(tenGroup, $"cell{i}", new Vector2(196f, 330f));
                cell.anchorMin = cell.anchorMax = new Vector2(0.5f, 1f);
                cell.pivot = new Vector2(0.5f, 1f);
                cell.anchoredPosition = new Vector2(((i % 5) - 2f) * 204f, -(i / 5) * 350f);
                Image glow = PopKit.Img(cell, "glow", MenuSprites.SoftGlow(), "pop.white.top");
                glow.rectTransform.anchorMin = glow.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                glow.rectTransform.sizeDelta = new Vector2(300f, 300f);
                glow.rectTransform.anchoredPosition = new Vector2(0f, -94f);
                Image ring = PopKit.Img(cell, "ring", PopSprites.Circle(), "panel.edge");
                ring.rectTransform.anchorMin = ring.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                ring.rectTransform.sizeDelta = new Vector2(176f, 176f);
                ring.rectTransform.anchoredPosition = new Vector2(0f, -94f);
                Image fill = PopKit.Img(ring.rectTransform, "fill", PopSprites.Circle(), "panel.light");
                UiComponents.Place(fill.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(150f, 150f));
                fill.gameObject.AddComponent<PopGradient>();
                Image face = PopKit.Img(ring.rectTransform, "art", CreatureSprites.Tideling("crab", 1), "pop.white.top");
                face.preserveAspect = true;
                UiComponents.Place(face.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(150f, 150f));
                TextMeshProUGUI name = UiText.CreateOutlined(cell, "name", "", "caption", "text.primary", "panel.edge", 0.2f);
                name.enableWordWrapping = true;
                name.rectTransform.anchorMin = name.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                name.rectTransform.sizeDelta = new Vector2(196f, 70f);
                name.rectTransform.anchoredPosition = new Vector2(0f, -230f);
                name.enableAutoSizing = true;
                name.fontSizeMin = 18f;
                name.fontSizeMax = 26f;
                RectTransform tag = PopKit.Panel(cell, "tag", new Vector2(176f, 52f), PopTone.Coral, 18f, 5f);
                tag.anchorMin = tag.anchorMax = new Vector2(0.5f, 1f);
                tag.anchoredPosition = new Vector2(0f, -298f);
                TextMeshProUGUI tagLabel = UiText.CreateOutlined(tag, "label", "", "caption", "text.primary", "pop.coral.edge");
                UiComponents.Stretch(tagLabel.rectTransform);
                tagLabel.rectTransform.offsetMin = new Vector2(10f, 0f);
                tagLabel.rectTransform.offsetMax = new Vector2(-10f, 0f);
                tagLabel.enableAutoSizing = true;
                tagLabel.fontSizeMin = 16f;
                tagLabel.fontSizeMax = tagLabel.fontSize;
                cells.Add(cell);
            }

            RectTransform card = UiComponents.Card(tenGroup, "spotlight", new Vector2(980f, 330f));
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0f);
            card.pivot = new Vector2(0.5f, 0f);
            card.anchoredPosition = Vector2.zero;
            RectTransform face2 = card.GetComponent<PopSurface>().Face;
            spotlight = TidelingCard.Create(face2, "card", new Vector2(220f, 290f), abilityPlate: false, null);
            spotlight.Rect.anchorMin = spotlight.Rect.anchorMax = new Vector2(0f, 0.5f);
            spotlight.Rect.anchoredPosition = new Vector2(150f, 6f);
            spotName = UiText.Create(face2, "name", "", "title", "text.ink", TextAlignmentOptions.MidlineLeft);
            spotName.rectTransform.anchorMin = new Vector2(0f, 0.55f);
            spotName.rectTransform.anchorMax = new Vector2(1f, 0.95f);
            spotName.rectTransform.offsetMin = new Vector2(290f, 0f);
            spotName.rectTransform.offsetMax = new Vector2(-30f, 0f);
            spotAbility = UiText.Create(face2, "ability", "", "body", "text.inkSoft", TextAlignmentOptions.TopLeft);
            spotAbility.enableWordWrapping = true;
            spotAbility.rectTransform.anchorMin = new Vector2(0f, 0.1f);
            spotAbility.rectTransform.anchorMax = new Vector2(1f, 0.55f);
            spotAbility.rectTransform.offsetMin = new Vector2(290f, 0f);
            spotAbility.rectTransform.offsetMax = new Vector2(-30f, 0f);

            Button done = PopKit.Button(root, "done", flow.Strings.Get("reveal.done"), PopTone.Blue, Close,
                new Vector2(980f, 150f), "title");
            var doneRt = (RectTransform)done.transform;
            doneRt.anchorMin = doneRt.anchorMax = new Vector2(0.5f, 0f);
            doneRt.pivot = new Vector2(0.5f, 0f);
            doneRt.anchoredPosition = new Vector2(0f, 110f);
        }

        /// <summary>Ten answer at once.</summary>
        public void ShowTen(IReadOnlyList<CallResult> results, Action? closed)
        {
            Results = results;
            single.gameObject.SetActive(false);
            ten.gameObject.SetActive(true);
            SetBackdrop("bg.oceanTop", "bg.deep");
            Open(closed);

            int fresh = 0;
            CallResult best = results[0];
            for (int i = 0; i < cells.Count; i++)
            {
                bool has = i < results.Count;
                cells[i].gameObject.SetActive(has);
                if (!has)
                {
                    continue;
                }

                CallResult r = results[i];
                fresh += r.IsNew ? 1 : 0;
                if (Better(r, best))
                {
                    best = r;
                }

                FillCell(cells[i], r);
            }

            tenSub.text = string.Format(flow.Strings.Get("reveal.tenSub"), fresh, results.Count - fresh);
            TidelingView t = flow.Tideling(best.Line);
            spotlight.Set(flow, t);
            spotName.text = t.FormName;
            spotAbility.text = $"{TidelingUi.AbilityName(flow, t.Line)}: {TidelingUi.AbilityDesc(flow, t.Line, t.Stage)}";
            eggsLeft.text = string.Format(flow.Strings.Get("reveal.eggsLeft"), flow.Eggs);
            StartCoroutine(PopCells(results.Count));
        }

        private static bool Better(CallResult a, CallResult b) =>
            a.IsNew != b.IsNew ? a.IsNew : a.Tier != b.Tier ? a.Tier > b.Tier : a.Sparkle && !b.Sparkle;

        private void FillCell(RectTransform cell, CallResult r)
        {
            TidelingView t = flow.Tideling(r.Line);
            PopTone tone = TidelingUi.Tone(r.Tier);
            ThemedElement.Bind(cell.Find("ring").gameObject, PopKit.Edge(tone));
            cell.Find("ring/fill").GetComponent<PopGradient>().Set("panel.light", PopKit.Top(tone));
            cell.Find("ring/art").GetComponent<Image>().sprite = TidelingUi.Art(t);
            cell.Find("glow").gameObject.SetActive(r.Tier >= PalRarity.Epic || r.Sparkle);
            cell.Find("name").GetComponent<TextMeshProUGUI>().text = t.FormName;
            var tag = cell.Find("tag").GetComponent<PopSurface>();
            tag.SetTone(r.IsNew || r.Sparkle ? PopTone.Coral : PopTone.Navy);
            TextMeshProUGUI label = cell.Find("tag/label").GetComponent<TextMeshProUGUI>();
            label.text = r.Sparkle ? flow.Strings.Get("reveal.sparkle") : r.IsNew
                ? flow.Strings.Get("reveal.new.short") : string.Format(flow.Strings.Get("reveal.bond"), r.BondGained);
            UiText.Outline(label, PopKit.Edge(tag.Tone));
        }

        private IEnumerator PopCells(int count)
        {
            if (ThemeRuntime.ReducedMotion)
            {
                yield break;
            }

            foreach (RectTransform cell in cells)
            {
                cell.localScale = Vector3.zero;
            }

            for (int i = 0; i < count && i < cells.Count; i++)
            {
                RectTransform cell = cells[i];
                Tween.Run(this, "t.base", "easeOutBack", u => cell.localScale = Vector3.one * u);
                yield return new WaitForSeconds(0.07f);
            }
        }
    }
}
