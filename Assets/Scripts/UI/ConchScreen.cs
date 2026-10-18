using Riptide.Core;
using Riptide.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// The Conch (GDD §16.3; mockup B "Sound the Conch"): the conch on its sandbar
    /// inside two guarantee rings — gold to the next sure Legendary, purple to the
    /// next sure Epic-or-better — the egg nest with the boards toward the next egg,
    /// the disclosed odds (per tier and per Tideling), and Call ×1 / ×10. Eggs are
    /// earned by playing, never sold, and the screen says so. Calls hand off to
    /// <see cref="CallReveal"/>.
    /// </summary>
    public sealed class ConchScreen : Overlay
    {
        private const float RingBasis = 760f;
        private const float RingsBottom = 1060f;

        private GameFlow flow = null!;
        private TextMeshProUGUI eggPill = null!;
        private RectTransform rings = null!;
        private Image legendaryFill = null!;
        private Image epicFill = null!;
        private TextMeshProUGUI legendaryText = null!;
        private TextMeshProUGUI epicText = null!;
        private TextMeshProUGUI nestCount = null!;
        private EggPips pips = null!;
        private TextMeshProUGUI pipText = null!;
        private Button callOne = null!;
        private Button callTen = null!;
        private CallReveal? reveal;
        private float laidOut = -1f;

        public static ConchScreen Build(RectTransform canvasRoot, GameFlow flow)
        {
            ConchScreen screen = Create<ConchScreen>(canvasRoot, "ConchScreen", "bg.oceanTop", "bg.deep");
            screen.flow = flow;
            RectTransform root = screen.Content;

            Button close = PopKit.IconButton(root, "close", "close", PopTone.White, screen.Close, new Vector2(124f, 130f));
            var closeRt = (RectTransform)close.transform;
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(0f, 1f);
            closeRt.anchoredPosition = new Vector2(100f, -100f);
            TextMeshProUGUI title = UiText.CreateOutlined(root, "title", flow.Strings.Get("conch.title"), "heading",
                "text.primary", "panel.edge", 0.26f);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title.rectTransform.sizeDelta = new Vector2(560f, 90f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -100f);
            screen.eggPill = TidelingUi.EggPill(root, "eggs", new Vector2(220f, 104f));
            var eggRt = (RectTransform)screen.eggPill.transform.parent;
            eggRt.anchorMin = eggRt.anchorMax = new Vector2(1f, 1f);
            eggRt.anchoredPosition = new Vector2(-140f, -100f);

            screen.BuildRings(root);
            screen.BuildLegends(root);
            screen.BuildNest(root);
            screen.BuildOdds(root);
            screen.BuildButtons(root);

            TextMeshProUGUI footer = UiText.CreateOutlined(root, "earned", flow.Strings.Get("conch.earned"), "caption",
                "text.primary", "panel.edge", 0.2f);
            footer.rectTransform.anchorMin = footer.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            footer.rectTransform.sizeDelta = new Vector2(980f, 50f);
            footer.rectTransform.anchoredPosition = new Vector2(0f, 50f);
            return screen;
        }

        private void BuildRings(RectTransform root)
        {
            RectTransform area = UiComponents.Rect(root, "ringArea", Vector2.zero);
            area.anchorMin = new Vector2(0f, 0f);
            area.anchorMax = new Vector2(1f, 1f);
            area.offsetMin = new Vector2(0f, RingsBottom);
            area.offsetMax = new Vector2(0f, -170f);

            rings = UiComponents.Rect(area, "rings", new Vector2(RingBasis, RingBasis));
            UiComponents.Place(rings, new Vector2(0.5f, 0.5f), new Vector2(RingBasis, RingBasis));
            Image glow = PopKit.Img(rings, "glow", MenuSprites.SoftGlow(), "ray.light");
            UiComponents.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(980f, 980f));
            Ring(rings, "legendaryTrack", RingBasis, "scrim", 1f);
            legendaryFill = Ring(rings, "legendary", RingBasis, "pop.yellow", 0f);
            Ring(rings, "epicTrack", RingBasis * 0.84f, "scrim", 1f);
            epicFill = Ring(rings, "epic", RingBasis * 0.84f, "pop.purple.top", 0f);
            Image well = PopKit.Img(rings, "well", PopSprites.Circle(), "burst");
            UiComponents.Place(well.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(RingBasis * 0.74f, RingBasis * 0.74f));

            Image shade = PopKit.Img(rings, "sandShade", PopSprites.Circle(), "sand.shade");
            UiComponents.Place(shade.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(460f, 120f));
            shade.rectTransform.anchoredPosition = new Vector2(0f, -150f);
            Image sand = PopKit.Img(rings, "sand", PopSprites.Circle(), "sand");
            UiComponents.Place(sand.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(440f, 100f));
            sand.rectTransform.anchoredPosition = new Vector2(0f, -140f);
            RectTransform conch = TidelingUi.Conch(rings, "conch", 420f);
            conch.anchoredPosition = new Vector2(0f, 10f);
            conch.localRotation = Quaternion.Euler(0f, 0f, 6f);
        }

        private static Image Ring(RectTransform parent, string name, float size, string token, float fill)
        {
            Image ring = PopKit.Img(parent, name, PopSprites.Shell("ring"), token);
            UiComponents.Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(size, size));
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = true;
            ring.fillAmount = fill;
            return ring;
        }

        private void BuildLegends(RectTransform root)
        {
            legendaryText = Legend(root, "legendaryLegend", "pop.yellow", 1010f);
            epicText = Legend(root, "epicLegend", "pop.purple.top", 930f);
        }

        private static TextMeshProUGUI Legend(RectTransform root, string name, string dotToken, float y)
        {
            RectTransform pill = PopKit.Panel(root, name, new Vector2(760f, 70f), PopTone.Navy, 35f, 6f);
            pill.anchorMin = pill.anchorMax = new Vector2(0.5f, 0f);
            pill.anchoredPosition = new Vector2(0f, y);
            Image dot = PopKit.Img(pill, "dot", PopSprites.Circle(), dotToken);
            dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            dot.rectTransform.sizeDelta = new Vector2(30f, 30f);
            dot.rectTransform.anchoredPosition = new Vector2(44f, 0f);
            TextMeshProUGUI text = UiText.Create(pill, "text", "", "caption", "text.primary", TextAlignmentOptions.MidlineLeft);
            text.fontStyle = FontStyles.Bold;
            UiComponents.Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(74f, 0f);
            text.rectTransform.offsetMax = new Vector2(-24f, 0f);
            text.enableAutoSizing = true;
            text.fontSizeMin = 20f;
            text.fontSizeMax = text.fontSize;
            return text;
        }

        private void BuildNest(RectTransform root)
        {
            RectTransform nest = UiComponents.Card(root, "nest", new Vector2(980f, 200f));
            nest.anchorMin = nest.anchorMax = new Vector2(0.5f, 0f);
            nest.pivot = new Vector2(0.5f, 0f);
            nest.anchoredPosition = new Vector2(0f, 700f);
            RectTransform face = nest.GetComponent<PopSurface>().Face;
            for (int i = 0; i < 3; i++)
            {
                RectTransform egg = TidelingUi.Egg(face, $"egg{i}", i == 1 ? 104f : 92f);
                egg.anchorMin = egg.anchorMax = new Vector2(0f, 0.5f);
                egg.anchoredPosition = new Vector2(84f + i * 54f, i == 1 ? 10f : -4f);
                egg.localRotation = Quaternion.Euler(0f, 0f, (1 - i) * 14f);
            }

            nestCount = UiText.Create(face, "count", "", "title", "text.ink", TextAlignmentOptions.MidlineLeft);
            nestCount.rectTransform.anchorMin = nestCount.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            nestCount.rectTransform.pivot = new Vector2(0f, 0.5f);
            nestCount.rectTransform.sizeDelta = new Vector2(600f, 70f);
            nestCount.rectTransform.anchoredPosition = new Vector2(250f, 30f);

            TextMeshProUGUI next = UiText.Create(face, "next", flow.Strings.Get("conch.nextEgg"), "caption", "text.inkSoft",
                TextAlignmentOptions.MidlineLeft);
            next.rectTransform.anchorMin = next.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            next.rectTransform.pivot = new Vector2(0f, 0.5f);
            next.rectTransform.sizeDelta = new Vector2(180f, 44f);
            next.rectTransform.anchoredPosition = new Vector2(250f, -34f);
            pips = TidelingUi.Pips(face, "pips", flow.Pals.Eggs.BoardsPerEgg, 34f);
            var pipsRt = (RectTransform)pips.transform;
            pipsRt.anchorMin = pipsRt.anchorMax = new Vector2(0f, 0.5f);
            pipsRt.pivot = new Vector2(0f, 0.5f);
            pipsRt.anchoredPosition = new Vector2(430f, -34f);
            pipText = UiText.Create(face, "boards", "", "caption", "text.inkSoft", TextAlignmentOptions.MidlineLeft);
            pipText.rectTransform.anchorMin = pipText.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            pipText.rectTransform.pivot = new Vector2(0f, 0.5f);
            pipText.rectTransform.sizeDelta = new Vector2(300f, 44f);
            pipText.rectTransform.anchoredPosition = new Vector2(450f + pipsRt.sizeDelta.x, -34f);
        }

        private void BuildOdds(RectTransform root)
        {
            RectTransform odds = UiComponents.Card(root, "odds", new Vector2(980f, 330f));
            odds.anchorMin = odds.anchorMax = new Vector2(0.5f, 0f);
            odds.pivot = new Vector2(0.5f, 0f);
            odds.anchoredPosition = new Vector2(0f, 340f);
            RectTransform face = odds.GetComponent<PopSurface>().Face;

            TextMeshProUGUI heading = UiText.Create(face, "heading", flow.Strings.Get("conch.odds"), "heading", "text.ink",
                TextAlignmentOptions.MidlineLeft);
            heading.rectTransform.anchorMin = heading.rectTransform.anchorMax = new Vector2(0f, 1f);
            heading.rectTransform.pivot = new Vector2(0f, 0.5f);
            heading.rectTransform.sizeDelta = new Vector2(400f, 56f);
            heading.rectTransform.anchoredPosition = new Vector2(44f, -54f);
            TextMeshProUGUI every = UiText.Create(face, "every", flow.Strings.Get("conch.oddsEvery"), "caption",
                "text.inkSoft", TextAlignmentOptions.MidlineRight);
            every.rectTransform.anchorMin = every.rectTransform.anchorMax = new Vector2(1f, 1f);
            every.rectTransform.pivot = new Vector2(1f, 0.5f);
            every.rectTransform.sizeDelta = new Vector2(400f, 56f);
            every.rectTransform.anchoredPosition = new Vector2(-44f, -54f);

            // One stacked bar, each tier its share (a rounded mask keeps the ends round).
            Image track = PopKit.Well(face, "bar", "panel.edge", 24f);
            track.rectTransform.anchorMin = track.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            track.rectTransform.sizeDelta = new Vector2(892f, 48f);
            track.rectTransform.anchoredPosition = new Vector2(0f, -116f);
            Image clip = PopKit.Well(track.rectTransform, "clip", "panel.light", 18f);
            PopKit.Inset(clip.rectTransform, 6f);
            clip.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            int[] rates = flow.Pals.Call.TierRatesPermille;
            float total = 0f;
            foreach (int r in rates)
            {
                total += r;
            }

            float x = 0f;
            var tiers = new[] { PalRarity.Common, PalRarity.Rare, PalRarity.Epic, PalRarity.Legendary };
            for (int i = 0; i < tiers.Length && i < rates.Length; i++)
            {
                float share = total > 0f ? rates[i] / total : 0f;
                Image seg = PopKit.Img(clip.rectTransform, $"seg{tiers[i]}", SpriteFactory.Solid(), PopKit.Base(TidelingUi.Tone(tiers[i])));
                seg.rectTransform.anchorMin = new Vector2(x, 0f);
                seg.rectTransform.anchorMax = new Vector2(x + share, 1f);
                seg.rectTransform.offsetMin = seg.rectTransform.offsetMax = Vector2.zero;
                x += share;

                int count = flow.Pals.LinesOfTier(tiers[i]).Count;
                float pct = rates[i] / 10f;
                string each = string.Format(flow.Strings.Get("conch.each"), FormatPct(count > 0 ? pct / count : 0f));
                RectTransform cell = UiComponents.Rect(face, $"odds{tiers[i]}", new Vector2(440f, 56f));
                cell.anchorMin = cell.anchorMax = new Vector2(i % 2 == 0 ? 0.27f : 0.73f, 1f);
                cell.anchoredPosition = new Vector2(0f, -188f - (i / 2) * 58f);
                Image dot = PopKit.Img(cell, "dot", PopSprites.Circle(), PopKit.Base(TidelingUi.Tone(tiers[i])));
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                dot.rectTransform.sizeDelta = new Vector2(30f, 30f);
                dot.rectTransform.anchoredPosition = new Vector2(18f, 0f);
                TextMeshProUGUI label = UiText.Create(cell, "label",
                    $"{TidelingUi.TierName(flow, tiers[i])} {FormatPct(pct)} <size=80%><color=#{Hex("text.inkSoft")}>{each}</color></size>",
                    "caption", "text.ink", TextAlignmentOptions.MidlineLeft);
                label.fontStyle = FontStyles.Bold;
                UiComponents.Stretch(label.rectTransform);
                label.rectTransform.offsetMin = new Vector2(44f, 0f);
                label.enableAutoSizing = true;
                label.fontSizeMin = 18f;
                label.fontSizeMax = label.fontSize;
            }
        }

        private void BuildButtons(RectTransform root)
        {
            callOne = CallButton(root, "callOne", flow.Strings.Get("conch.callOne"), flow.Pals.Call.Cost, PopTone.Blue,
                -248f, () => OnCall(ten: false));
            callTen = CallButton(root, "callTen", flow.Strings.Get("conch.callTen"), flow.Pals.Call.TenCost, PopTone.Yellow,
                248f, () => OnCall(ten: true));
            RectTransform tag = PopKit.Panel((RectTransform)callTen.transform, "free", new Vector2(250f, 60f), PopTone.Coral,
                20f, 6f);
            tag.anchorMin = tag.anchorMax = new Vector2(1f, 1f);
            tag.anchoredPosition = new Vector2(-70f, -6f);
            tag.localRotation = Quaternion.Euler(0f, 0f, -7f);
            TextMeshProUGUI tagText = UiText.CreateOutlined(tag, "label", flow.Strings.Get("conch.tenFree"), "caption",
                "text.primary", "pop.coral.edge");
            tagText.fontSize = 24f;
            UiComponents.Stretch(tagText.rectTransform);
        }

        internal static Button CallButton(RectTransform root, string name, string label, int cost, PopTone tone, float x,
            System.Action onTap)
        {
            Button button = PopKit.Tile(root, name, tone, onTap, new Vector2(470f, 200f), PopKit.Radius("r.m"));
            var rt = (RectTransform)button.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(x, 100f);
            RectTransform face = PopKit.FaceOf(button);
            TextMeshProUGUI text = PopKit.Label(face, "label", label, "title", tone);
            text.rectTransform.anchorMin = new Vector2(0f, 0.48f);
            text.rectTransform.anchorMax = new Vector2(1f, 1f);
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            text.rectTransform.anchoredPosition = new Vector2(0f, -10f);
            RectTransform costPill = PopKit.Panel(face, "cost", new Vector2(170f, 62f), PopTone.Navy, 31f, 5f);
            costPill.anchorMin = costPill.anchorMax = new Vector2(0.5f, 0.3f);
            RectTransform egg = TidelingUi.Egg(costPill, "egg", 44f);
            egg.anchorMin = egg.anchorMax = new Vector2(0f, 0.5f);
            egg.anchoredPosition = new Vector2(38f, 2f);
            TextMeshProUGUI costText = UiText.CreateOutlined(costPill, "count", cost.ToString(), "heading", "text.primary",
                "panel.edge", 0.2f);
            UiComponents.Stretch(costText.rectTransform);
            costText.rectTransform.offsetMin = new Vector2(64f, 0f);
            return button;
        }

        internal static string FormatPct(float pct) =>
            (Mathf.Abs(pct - Mathf.Round(pct)) < 0.05f ? Mathf.Round(pct).ToString("0") : pct.ToString("0.#",
                System.Globalization.CultureInfo.InvariantCulture)) + "%";

        internal static string Hex(string token) => ColorUtility.ToHtmlStringRGB(ThemeRuntime.Color(token));

        protected override void OnOpened() => Refresh();

        private void Refresh()
        {
            int eggs = flow.Eggs;
            eggPill.text = eggs.ToString();
            nestCount.text = TidelingUi.Eggs(flow, eggs);
            int per = flow.Pals.Eggs.BoardsPerEgg;
            pips.Set(flow.BoardsTowardEgg);
            pipText.text = string.Format(flow.Strings.Get("conch.boards"), flow.BoardsTowardEgg, per);

            int legendary = flow.CallsUntilLegendary;
            int epic = flow.CallsUntilEpic;
            legendaryFill.fillAmount = 1f - (legendary - 1f) / Mathf.Max(1, flow.LegendaryPity);
            epicFill.fillAmount = 1f - (epic - 1f) / Mathf.Max(1, flow.Pals.Call.EpicPity);
            legendaryText.text = string.Format(flow.Strings.Get("conch.legendary"), Calls(legendary, "pop.yellow"));
            epicText.text = string.Format(flow.Strings.Get("conch.epic"), Calls(epic, "pop.purple.top"));

            callOne.interactable = eggs >= flow.Pals.Call.Cost;
            callTen.interactable = eggs >= flow.Pals.Call.TenCost;
        }

        private string Calls(int n, string token) =>
            $"<color=#{Hex(token)}>{(n <= 1 ? flow.Strings.Get("conch.nextCall") : string.Format(flow.Strings.Get("conch.calls"), n))}</color>";

        private void OnCall(bool ten)
        {
            int cost = ten ? flow.Pals.Call.TenCost : flow.Pals.Call.Cost;
            if (flow.Eggs < cost)
            {
                GetComponentInParent<Canvas>().GetComponentInChildren<ToastManager>()?.Show(string.Format(
                    flow.Strings.Get("conch.short"), cost - flow.Eggs, flow.Pals.Eggs.BoardsPerEgg));
                return;
            }

            reveal ??= CallReveal.Build((RectTransform)transform.parent, flow);
            if (ten)
            {
                var results = flow.CallTen();
                if (results != null)
                {
                    UiJuice.Play("streak");
                    reveal.ShowTen(results, Refresh);
                }
            }
            else
            {
                CallResult? result = flow.CallOne();
                if (result.HasValue)
                {
                    UiJuice.Play("star");
                    reveal.ShowOne(result.Value, Refresh);
                }
            }

            Refresh();
        }

        private void Update()
        {
            // The rings take whatever height is left above the nest (16:9 phones are short).
            var area = (RectTransform)rings.parent;
            float fit = Mathf.Min(area.rect.width, area.rect.height);
            if (fit > 1f && !Mathf.Approximately(fit, laidOut))
            {
                laidOut = fit;
                rings.localScale = Vector3.one * Mathf.Min(1f, fit / RingBasis);
            }
        }
    }
}
