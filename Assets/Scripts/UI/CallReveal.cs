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
    /// The Call's answer (GDD §16.6, §16.8): one Tideling pops out of a bubble burst
    /// in its tier's colors under turning rays (mockup B) — NEW! or the bond it gained,
    /// its ability, Put in Pod / Again — or ten arrive on a wave as ten bubbles with a
    /// spotlight on the best of them (mockup A). Waves, bubbles and glints only; never
    /// a silhouette flash. Everything lands in its final state at once, so reduced
    /// motion (and tests) see the result immediately; motion is decoration on top.
    /// </summary>
    public sealed partial class CallReveal : Overlay
    {
        private const float SingleBasis = 860f;
        private const float TenBasis = 1080f;

        private GameFlow flow = null!;
        private RectTransform rays = null!;
        private RectTransform single = null!;
        private RectTransform ten = null!;
        private RectTransform singleGroup = null!;
        private RectTransform tenGroup = null!;

        // single
        private Image bubble = null!;
        private RectTransform burst = null!;
        private TextMeshProUGUI ribbonLabel = null!;
        private PopSurface ribbonBand = null!;
        private RectTransform ribbonRoot = null!;
        private Image art = null!;
        private TextMeshProUGUI nameLabel = null!;
        private RectTransform tierChip = null!;
        private TextMeshProUGUI tierLabel = null!;
        private TextMeshProUGUI kindLabel = null!;
        private TextMeshProUGUI abilityName = null!;
        private TextMeshProUGUI abilityDesc = null!;
        private TextMeshProUGUI abilityFoot = null!;
        private Button primary = null!;
        private TextMeshProUGUI primaryLabel = null!;
        private Button again = null!;

        // ten
        private TextMeshProUGUI tenSub = null!;
        private readonly List<RectTransform> cells = new List<RectTransform>();
        private TidelingCard spotlight = null!;
        private TextMeshProUGUI spotName = null!;
        private TextMeshProUGUI spotAbility = null!;
        private TextMeshProUGUI eggsLeft = null!;

        private CallResult shown;
        private bool canPod;

        /// <summary>The results on screen. Test surface.</summary>
        public IReadOnlyList<CallResult> Results { get; private set; } = Array.Empty<CallResult>();

        public bool ShowingTen => ten.gameObject.activeSelf;

        public static CallReveal Build(RectTransform canvasRoot, GameFlow flow)
        {
            CallReveal reveal = Create<CallReveal>(canvasRoot, "CallReveal", "bg.oceanTop", "bg.deep");
            reveal.flow = flow;
            RectTransform root = reveal.Content;

            reveal.rays = UiComponents.Rect(root, "rays", new Vector2(2000f, 2000f));
            reveal.rays.anchorMin = reveal.rays.anchorMax = new Vector2(0.5f, 1f);
            reveal.rays.anchoredPosition = new Vector2(0f, -640f);
            Image rayImage = PopKit.Img(reveal.rays, "burst", PopSprites.Sunburst(), "burst");
            UiComponents.Stretch(rayImage.rectTransform);

            reveal.single = UiComponents.Rect(root, "single", Vector2.zero);
            UiComponents.Stretch(reveal.single);
            reveal.BuildSingle(reveal.single);
            reveal.ten = UiComponents.Rect(root, "ten", Vector2.zero);
            UiComponents.Stretch(reveal.ten);
            reveal.BuildTen(reveal.ten);

            reveal.eggsLeft = UiText.CreateOutlined(root, "eggsLeft", "", "caption", "text.primary", "panel.edge", 0.2f);
            reveal.eggsLeft.rectTransform.anchorMin = reveal.eggsLeft.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            reveal.eggsLeft.rectTransform.sizeDelta = new Vector2(900f, 50f);
            reveal.eggsLeft.rectTransform.anchoredPosition = new Vector2(0f, 46f);
            return reveal;
        }

        // ------------------------------------------------------------ building

        private void BuildSingle(RectTransform root)
        {
            RectTransform ribbon = PopKit.Ribbon(root, "ribbon", "", PopTone.Coral, new Vector2(700f, 120f), "title");
            ribbon.anchorMin = ribbon.anchorMax = new Vector2(0.5f, 1f);
            ribbon.anchoredPosition = new Vector2(0f, -150f);
            ribbonRoot = ribbon;
            ribbonBand = ribbon.Find("band").GetComponent<PopSurface>();
            ribbonLabel = ribbon.Find("band/face/label").GetComponent<TextMeshProUGUI>();

            singleGroup = Group(root, "stage", 660f, 230f, new Vector2(1000f, SingleBasis));
            RectTransform group = singleGroup;
            burst = UiComponents.Rect(group, "bubbles", new Vector2(900f, 900f));
            burst.anchorMin = burst.anchorMax = new Vector2(0.5f, 1f);
            burst.anchoredPosition = new Vector2(0f, -320f);
            for (int i = 0; i < 16; i++)
            {
                float ang = i * 22.5f * Mathf.Deg2Rad;
                float r = 380f + (i % 3) * 40f;
                float size = 40f + (i * 37 % 5) * 14f;
                Image b = PopKit.Img(burst, $"b{i}", PopSprites.Bubble(), "bubble");
                UiComponents.Place(b.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(size, size));
                b.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r * 0.9f);
            }

            foreach ((float x, float y, float s) in new[] { (-330f, 170f, 70f), (300f, 230f, 54f), (290f, -200f, 40f), (-300f, -180f, 34f) })
            {
                Image g = PopKit.Img(burst, "glint", PopSprites.Icon("glint"), "pop.white.top");
                UiComponents.Place(g.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(s, s));
                g.rectTransform.anchoredPosition = new Vector2(x, y);
            }

            bubble = PopKit.Img(group, "bubble", PopSprites.Bubble(), "bubble");
            bubble.rectTransform.anchorMin = bubble.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            bubble.rectTransform.sizeDelta = new Vector2(680f, 680f);
            bubble.rectTransform.anchoredPosition = new Vector2(0f, -320f);

            art = PopKit.Img(group, "art", CreatureSprites.Tideling("crab", 1), "pop.white.top");
            art.preserveAspect = true;
            art.rectTransform.anchorMin = art.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            art.rectTransform.sizeDelta = new Vector2(640f, 640f);
            art.rectTransform.anchoredPosition = new Vector2(0f, -320f);

            nameLabel = UiText.CreateOutlined(group, "name", "", "display", "text.primary", "panel.edge", 0.3f);
            nameLabel.rectTransform.anchorMin = nameLabel.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            nameLabel.rectTransform.sizeDelta = new Vector2(980f, 120f);
            nameLabel.rectTransform.anchoredPosition = new Vector2(0f, -690f);

            tierChip = PopKit.Panel(group, "tier", new Vector2(220f, 70f), PopTone.Purple, 35f, 6f);
            tierChip.anchorMin = tierChip.anchorMax = new Vector2(0.5f, 1f);
            tierChip.anchoredPosition = new Vector2(-190f, -800f);
            tierLabel = UiText.CreateOutlined(tierChip, "label", "", "caption", "text.primary", "pop.purple.edge");
            UiComponents.Stretch(tierLabel.rectTransform);
            RectTransform kind = PopKit.Panel(group, "kind", new Vector2(400f, 70f), PopTone.Navy, 35f, 6f);
            kind.anchorMin = kind.anchorMax = new Vector2(0.5f, 1f);
            kind.anchoredPosition = new Vector2(130f, -800f);
            kindLabel = UiText.CreateOutlined(kind, "label", "", "caption", "text.primary", "panel.edge", 0.2f);
            UiComponents.Stretch(kindLabel.rectTransform);

            RectTransform card = UiComponents.Card(root, "ability", new Vector2(980f, 290f));
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0f);
            card.pivot = new Vector2(0.5f, 0f);
            card.anchoredPosition = new Vector2(0f, 330f);
            abilityName = Line(card, "name", "heading", "text.ink", -64f, 56f);
            abilityDesc = Line(card, "desc", "body", "text.ink", -128f, 50f);
            abilityFoot = Line(card, "foot", "caption", "text.inkSoft", -194f, 44f);

            primary = PopKit.Button(root, "primary", "", PopTone.Green, OnPrimary, new Vector2(560f, 170f), "title");
            var primaryRt = (RectTransform)primary.transform;
            primaryRt.anchorMin = primaryRt.anchorMax = new Vector2(0.5f, 0f);
            primaryRt.pivot = new Vector2(0.5f, 0f);
            primaryRt.anchoredPosition = new Vector2(-200f, 110f);
            primaryLabel = PopKit.FaceOf(primary).Find("label").GetComponent<TextMeshProUGUI>();
            again = ConchScreen.CallButton(root, "again", flow.Strings.Get("reveal.again"), flow.Pals.Call.Cost,
                PopTone.Blue, 290f, OnAgain);
            var againRt = (RectTransform)again.transform;
            againRt.sizeDelta = new Vector2(380f, 170f);
            againRt.anchoredPosition = new Vector2(290f, 110f);
        }

        /// <summary>A fixed-size group centered in the room between a bottom and top inset; Update scales it to fit.</summary>
        private static RectTransform Group(RectTransform root, string name, float bottom, float top, Vector2 size)
        {
            RectTransform room = UiComponents.Rect(root, name, Vector2.zero);
            room.anchorMin = Vector2.zero;
            room.anchorMax = Vector2.one;
            room.offsetMin = new Vector2(0f, bottom);
            room.offsetMax = new Vector2(0f, -top);
            RectTransform group = UiComponents.Rect(room, "group", size);
            UiComponents.Place(group, new Vector2(0.5f, 0.5f), size);
            return group;
        }

        private static void Fit(RectTransform group, float basis)
        {
            float room = ((RectTransform)group.parent).rect.height;
            if (room > 1f)
            {
                group.localScale = Vector3.one * Mathf.Min(1f, room / basis);
            }
        }

        private static TextMeshProUGUI Line(RectTransform parent, string name, string type, string token, float y, float h)
        {
            TextMeshProUGUI text = UiText.Create(parent, name, "", type, token);
            text.rectTransform.anchorMin = new Vector2(0f, 1f);
            text.rectTransform.anchorMax = new Vector2(1f, 1f);
            text.rectTransform.offsetMin = new Vector2(44f, y - h * 0.5f);
            text.rectTransform.offsetMax = new Vector2(-44f, y + h * 0.5f);
            text.enableAutoSizing = true;
            text.fontSizeMin = 20f;
            text.fontSizeMax = text.fontSize;
            return text;
        }

        // ------------------------------------------------------------- showing

        /// <summary>One Tideling answers the Conch.</summary>
        public void ShowOne(CallResult result, Action? closed)
        {
            Results = new[] { result };
            single.gameObject.SetActive(true);
            ten.gameObject.SetActive(false);
            if (!IsOpen)
            {
                Open(closed);
            }

            Present(result);
        }

        private void Present(CallResult r)
        {
            shown = r;
            TidelingView t = flow.Tideling(r.Line);
            PopTone tone = TidelingUi.Tone(r.Tier);
            SetBackdrop(PopKit.Top(tone), PopKit.Edge(tone));

            PopTone ribbonTone = r.IsNew || r.Sparkle ? PopTone.Coral : PopTone.Navy;
            ribbonBand.SetTone(ribbonTone);
            foreach (string tail in new[] { "tailL", "tailR" })
            {
                PopSurface tailSurface = ribbonRoot.Find(tail).GetComponent<PopSurface>();
                tailSurface.SetTone(ribbonTone);
                tailSurface.Gradient.Set(PopKit.Edge(ribbonTone), PopKit.Edge(ribbonTone));
            }

            ribbonLabel.text = r.Sparkle ? flow.Strings.Get("reveal.sparkle")
                : r.IsNew ? flow.Strings.Get("reveal.new") : string.Format(flow.Strings.Get("reveal.bond"), r.BondGained);
            UiText.Outline(ribbonLabel, PopKit.Edge(ribbonBand.Tone));
            art.sprite = TidelingUi.Art(t);
            nameLabel.text = t.FormName;
            UiText.Outline(nameLabel, PopKit.Edge(tone), 0.3f);
            tierChip.GetComponent<PopSurface>().SetTone(tone);
            tierLabel.text = TidelingUi.TierName(flow, r.Tier);
            UiText.Outline(tierLabel, PopKit.Edge(tone));
            kindLabel.text = $"{t.Line.Name} · {TidelingUi.StageName(flow, t.Stage)}";

            abilityName.text = TidelingUi.AbilityName(flow, t.Line);
            ThemedElement.Bind(abilityName.gameObject, PopKit.Edge(tone));
            abilityDesc.text = TidelingUi.AbilityDesc(flow, t.Line, t.Stage);
            abilityFoot.text = t.NextBond.HasValue
                ? $"{flow.Strings.Get("tl.notDaily")}  ·  {string.Format(flow.Strings.Get("reveal.growsAt"), t.NextBond.Value)}"
                : flow.Strings.Get("tl.notDaily");

            canPod = r.IsNew && t.PodSlot < 0 && FreeSlot() >= 0;
            primaryLabel.text = flow.Strings.Get(canPod ? "reveal.toPod" : "reveal.done");
            again.interactable = flow.Eggs >= flow.Pals.Call.Cost;
            eggsLeft.text = string.Format(flow.Strings.Get("reveal.eggsLeft"), flow.Eggs);
            StartCoroutine(Burst());
        }

        private int FreeSlot()
        {
            int[] podSlots = flow.Meta.Save.Pals.Pod;
            for (int slot = 0; slot < flow.PodSlotsOpen && slot < podSlots.Length; slot++)
            {
                if (podSlots[slot] < 0)
                {
                    return slot;
                }
            }

            return -1;
        }

        /// <summary>The bubble wobbles, pops, the bubbles fly and the Tideling bounces out.</summary>
        private IEnumerator Burst()
        {
            bubble.gameObject.SetActive(true);
            bubble.rectTransform.localScale = Vector3.one;
            bubble.color = ThemeRuntime.Color("bubble");
            if (ThemeRuntime.ReducedMotion)
            {
                bubble.gameObject.SetActive(false);
                yield break;
            }

            art.rectTransform.localScale = Vector3.zero;
            burst.localScale = Vector3.one * 0.4f;
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                float wobble = 1f + Mathf.Sin(t * 40f) * 0.04f;
                bubble.rectTransform.localScale = new Vector3(wobble, 2f - wobble, 1f);
                yield return null;
            }

            Color c = bubble.color;
            Tween.Run(this, "t.fast", "easeOutQuart", u =>
            {
                bubble.rectTransform.localScale = Vector3.one * (1f + u * 0.5f);
                bubble.color = new Color(c.r, c.g, c.b, c.a * (1f - u));
            }, () => bubble.gameObject.SetActive(false));
            Tween.Run(this, "t.rise", "easeOutQuart", u => burst.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, u));
            Tween.Run(this, "t.screen", "easeOutBack", u => art.rectTransform.localScale = Vector3.one * u);
        }

        private void OnPrimary()
        {
            if (canPod)
            {
                int slot = FreeSlot();
                if (slot >= 0 && flow.TrySetPod(slot, shown.Line))
                {
                    UiJuice.Play("star");
                }
            }

            Close();
        }

        private void OnAgain()
        {
            CallResult? next = flow.CallOne();
            if (next.HasValue)
            {
                UiJuice.Play("star");
                ShowOne(next.Value, null);
            }
        }

        private void Update()
        {
            Fit(singleGroup, SingleBasis);
            Fit(tenGroup, TenBasis);
            rays.position = single.gameObject.activeSelf ? art.rectTransform.position : tenGroup.position;
            if (!ThemeRuntime.ReducedMotion)
            {
                rays.localRotation = Quaternion.Euler(0f, 0f, Time.time * -8f);
            }
        }
    }
}
