using System;
using Riptide.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// Pause sheet (§4.3 pause slot, §6.2 resume-from-background): resume primary,
    /// quick sound toggle, quit-to-home. Never auto-abandons the run.
    /// </summary>
    public sealed class PauseSheet : MonoBehaviour
    {
        private Sheet sheet = null!;
        private GameFlow flow = null!;
        private Button sound = null!;

        public bool Shown => sheet != null && sheet.Shown;

        public static PauseSheet Build(RectTransform canvasRoot, GameFlow flow)
        {
            Sheet sheet = UiComponents.SheetComponent(canvasRoot, "PauseSheet", 880f);
            var pause = sheet.gameObject.AddComponent<PauseSheet>();
            pause.sheet = sheet;
            pause.flow = flow;

            RectTransform body = sheet.Body;
            TextMeshProUGUI title = UiText.Create(body, "title", flow.Strings.Get("pause.title"),
                "title", "text.ink");
            UiComponents.Place(title.rectTransform, new Vector2(0.5f, 0.86f), new Vector2(700f, 80f));

            Button resume = UiComponents.ButtonPrimary(body, "resume", flow.Strings.Get("pause.resume"),
                pause.Dismiss);
            UiComponents.Place((RectTransform)resume.transform, new Vector2(0.5f, 0.69f), new Vector2(680f, 150f));

            // Gate feedback: in-game had NO route to the level map — pause now
            // exits to it (same abandon semantics as quit-to-home).
            Button map = UiComponents.ButtonSecondary(body, "map", flow.Strings.Get("pause.map"), () =>
            {
                pause.Dismiss();
                flow.GoTo(FlowScreen.ZoneMap);
            });
            UiComponents.Place((RectTransform)map.transform, new Vector2(0.5f, 0.53f), new Vector2(680f, 130f));

            pause.sound = UiComponents.ButtonSecondary(body, "sound", "", pause.ToggleSound);
            UiComponents.Place((RectTransform)pause.sound.transform, new Vector2(0.5f, 0.38f), new Vector2(680f, 130f));

            Button home = UiComponents.ButtonGhost(body, "home", flow.Strings.Get("pause.home"), () =>
            {
                pause.Dismiss();
                flow.GoTo(FlowScreen.Home);
            });
            UiComponents.Place((RectTransform)home.transform, new Vector2(0.5f, 0.23f), new Vector2(640f, 96f));

            return pause;
        }

        public void Show()
        {
            RefreshSound();
            sheet.Show();
        }

        public void Dismiss() => sheet.Dismiss();

        private void ToggleSound()
        {
            PlayerPrefs.SetInt("settings.audio.on", PlayerPrefs.GetInt("settings.audio.on", 1) == 1 ? 0 : 1);
            PlayerPrefs.Save();
            RefreshSound();
        }

        private void RefreshSound()
        {
            bool on = PlayerPrefs.GetInt("settings.audio.on", 1) == 1;
            sound.GetComponentInChildren<TextMeshProUGUI>().text =
                $"{flow.Strings.Get("settings.audio")} — {(on ? "ON" : "OFF")}";
        }
    }

    /// <summary>
    /// ROADMAP M2: the continue offer — a pre-results interstitial sheet over the
    /// drowned board. Five-second pip countdown; rewarded ad or coins revives,
    /// "Let go" (or the timer) concedes to results. Once per run, never Daily —
    /// the flow and the sim both enforce the rules; this is just the face.
    /// </summary>
    public sealed class ContinueSheet : MonoBehaviour
    {
        private const int CountdownSeconds = 5;

        private Sheet sheet = null!;
        private GameFlow flow = null!;
        private TextMeshProUGUI body = null!;
        private ProgressPips pips = null!;
        private Button adButton = null!;
        private Button coinButton = null!;
        private float deadline;
        private bool resolved;

        public bool Shown => sheet != null && sheet.Shown;

        public static ContinueSheet Build(RectTransform canvasRoot, GameFlow flow)
        {
            Sheet sheet = UiComponents.SheetComponent(canvasRoot, "ContinueSheet", 960f);
            var offer = sheet.gameObject.AddComponent<ContinueSheet>();
            offer.sheet = sheet;
            offer.flow = flow;

            RectTransform bodyRt = sheet.Body;
            TextMeshProUGUI title = UiText.Create(bodyRt, "title", flow.Strings.Get("continue.title"),
                "title", "text.ink");
            UiComponents.Place(title.rectTransform, new Vector2(0.5f, 0.88f), new Vector2(800f, 90f));

            offer.body = UiText.Create(bodyRt, "body", "", "body", "text.inkSoft");
            UiComponents.Place(offer.body.rectTransform, new Vector2(0.5f, 0.77f), new Vector2(820f, 60f));

            offer.pips = UiComponents.ProgressPipsComponent(bodyRt, CountdownSeconds);
            UiComponents.Place((RectTransform)offer.pips.transform, new Vector2(0.5f, 0.69f), new Vector2(300f, 40f));

            offer.adButton = UiComponents.ButtonReward(bodyRt, "continueAd",
                flow.Strings.Get("continue.ad"), offer.OnAd);
            UiComponents.Place((RectTransform)offer.adButton.transform, new Vector2(0.5f, 0.55f), new Vector2(700f, 140f));

            offer.coinButton = UiComponents.ButtonSecondary(bodyRt, "continueCoins",
                string.Format(flow.Strings.Get("continue.coins"), flow.Economy.Coins.ContinueCost), offer.OnCoins);
            UiComponents.Place((RectTransform)offer.coinButton.transform, new Vector2(0.5f, 0.40f), new Vector2(700f, 130f));

            Button decline = UiComponents.ButtonGhost(bodyRt, "decline",
                flow.Strings.Get("continue.decline"), offer.OnDecline);
            UiComponents.Place((RectTransform)decline.transform, new Vector2(0.5f, 0.25f), new Vector2(420f, 96f));

            return offer;
        }

        public void Show()
        {
            resolved = false;
            deadline = Time.realtimeSinceStartup + CountdownSeconds;
            adButton.interactable = flow.ContinueAdAvailable;
            coinButton.interactable = flow.Meta.CanAfford(flow.Economy.Coins.ContinueCost);
            sheet.Show();
        }

        private void Update()
        {
            if (!Shown || resolved)
            {
                return;
            }

            float remaining = deadline - Time.realtimeSinceStartup;
            int seconds = Mathf.Max(0, Mathf.CeilToInt(remaining));
            body.text = string.Format(flow.Strings.Get("continue.body"), seconds);
            pips.SetFilled(seconds);
            if (remaining <= 0f)
            {
                OnDecline();
            }
        }

        private void OnAd()
        {
            if (flow.TryContinueViaAd())
            {
                Resolve();
            }
        }

        private void OnCoins()
        {
            if (flow.TryContinueWithCoins())
            {
                Resolve();
            }
        }

        private void OnDecline()
        {
            flow.DeclineContinue();
            Resolve();
        }

        private void Resolve()
        {
            resolved = true;
            sheet.Dismiss();
        }

        /// <summary>Test/driver hook: dismiss as if "Let go" was tapped.</summary>
        public void DismissForDriver() => Resolve();
    }

    /// <summary>
    /// Spec §4.7 first-run age gate: neutral year picker on bg.abyss, no game art
    /// that skews child-directed. The stored year feeds ad configuration when the
    /// real SDKs land; the (fake) consent flow itself is requested at boot.
    /// </summary>
    public sealed class ConsentAgeGate : MonoBehaviour
    {
        public const string BirthYearKey = "consent.birthYear";
        private int year = 2000;
        private TextMeshProUGUI yearLabel = null!;
        private GameFlow flow = null!;

        public bool IsOpen => gameObject.activeSelf;

        public static bool Required => !PlayerPrefs.HasKey(BirthYearKey);

        public static ConsentAgeGate Build(RectTransform canvasRoot, GameFlow flow)
        {
            RectTransform root = UiComponents.Rect(canvasRoot, "ConsentAgeGate", Vector2.zero);
            UiComponents.Stretch(root);
            var bg = root.gameObject.AddComponent<Image>();
            bg.sprite = SpriteFactory.Solid();
            ThemedElement.Bind(root.gameObject, "bg.abyss");
            var gate = root.gameObject.AddComponent<ConsentAgeGate>();
            gate.flow = flow;

            // One composed card instead of elements adrift on the void.
            RectTransform card = UiComponents.Card(root, "card", new Vector2(880f, 820f));
            UiComponents.Place(card, new Vector2(0.5f, 0.55f), new Vector2(880f, 820f));

            TextMeshProUGUI title = UiText.Create(card, "title",
                flow.Strings.Get("consent.age.title"), "title", "text.ink");
            UiComponents.Place(title.rectTransform, new Vector2(0.5f, 0.84f), new Vector2(800f, 100f));
            TextMeshProUGUI body = UiText.Create(card, "body",
                flow.Strings.Get("consent.age.body"), "body", "text.inkSoft");
            UiComponents.Place(body.rectTransform, new Vector2(0.5f, 0.70f), new Vector2(800f, 60f));

            // Year row: steppers flank the numeral, not the screen edges.
            Button minus = UiComponents.IconButton(card, "minus", "‹", () => gate.Step(-1));
            UiComponents.Place((RectTransform)minus.transform, new Vector2(0.5f, 0.50f), new Vector2(120f, 126f));
            ((RectTransform)minus.transform).anchoredPosition = new Vector2(-280f, 0f);
            gate.yearLabel = UiText.Create(card, "year", "2000", "display", "text.ink");
            UiComponents.Place(gate.yearLabel.rectTransform, new Vector2(0.5f, 0.50f), new Vector2(420f, 120f));
            Button plus = UiComponents.IconButton(card, "plus", "›", () => gate.Step(1));
            UiComponents.Place((RectTransform)plus.transform, new Vector2(0.5f, 0.50f), new Vector2(120f, 126f));
            ((RectTransform)plus.transform).anchoredPosition = new Vector2(280f, 0f);

            Button confirm = UiComponents.ButtonPrimary(card, "confirm",
                flow.Strings.Get("consent.age.confirm"), gate.Confirm);
            UiComponents.Place((RectTransform)confirm.transform, new Vector2(0.5f, 0.18f), new Vector2(680f, 160f));

            gate.Step(0);
            return gate;
        }

        private void Step(int delta)
        {
            year = Mathf.Clamp(year + delta, 1920, DateTimeYearNow());
            yearLabel.text = year.ToString();
        }

        private static int DateTimeYearNow() => System.DateTime.Now.Year;

        private void Confirm()
        {
            PlayerPrefs.SetInt(BirthYearKey, year);
            PlayerPrefs.Save();
            gameObject.SetActive(false);

            // ROADMAP M1: a virgin profile dives straight into the tutorial board —
            // the funnel is decided in the first 30 seconds, not on a menu.
            if (flow.Meta.Voyage.CompletedCount == 0 && flow.Screen == FlowScreen.Home)
            {
                flow.StartVoyageLevel(1, 1);
            }
        }
    }

    /// <summary>
    /// Mid-run resume prompt (SAVE_RESUME_DESIGN.md §5): a sheet over Home when a
    /// pending run record survived a process death. Resume replays to the exact
    /// state; Abandon discards (Voyage/Endless lose nothing; a Daily attempt was
    /// already consumed at its StartDaily).
    /// </summary>
    public sealed class ResumeSheet : MonoBehaviour
    {
        private Sheet sheet = null!;

        public bool Shown => sheet != null && sheet.Shown;

        public static ResumeSheet Build(RectTransform canvasRoot, GameFlow flow, ToastManager toasts)
        {
            Sheet sheet = UiComponents.SheetComponent(canvasRoot, "ResumeSheet", 720f);
            var resume = sheet.gameObject.AddComponent<ResumeSheet>();
            resume.sheet = sheet;

            RectTransform body = sheet.Body;
            TextMeshProUGUI title = UiText.Create(body, "title", flow.Strings.Get("resume.title"),
                "title", "text.ink");
            UiComponents.Place(title.rectTransform, new Vector2(0.5f, 0.84f), new Vector2(700f, 80f));

            Riptide.Core.RunRecord? record = flow.PendingRun;
            string bodyText = record == null ? "" : record.Mode switch
            {
                "Voyage" => string.Format(flow.Strings.Get("resume.body.voyage"),
                    record.Zone, record.Level, record.Moves.Count),
                "Daily" => string.Format(flow.Strings.Get("resume.body.daily"), record.Moves.Count),
                _ => string.Format(flow.Strings.Get("resume.body.endless"), record.Moves.Count),
            };
            TextMeshProUGUI info = UiText.Create(body, "body", bodyText, "body", "text.inkSoft");
            UiComponents.Place(info.rectTransform, new Vector2(0.5f, 0.64f), new Vector2(780f, 60f));

            Button go = UiComponents.ButtonPrimary(body, "resume", flow.Strings.Get("resume.continue"),
                () =>
                {
                    resume.sheet.Dismiss();
                    if (!flow.ResumeRun())
                    {
                        toasts.Show(flow.Strings.Get("resume.expired"));
                    }
                });
            UiComponents.Place((RectTransform)go.transform, new Vector2(0.5f, 0.47f), new Vector2(680f, 150f));

            Button abandon = UiComponents.ButtonGhost(body, "abandon", flow.Strings.Get("resume.abandon"),
                () =>
                {
                    flow.AbandonPendingRun();
                    resume.sheet.Dismiss();
                });
            UiComponents.Place((RectTransform)abandon.transform, new Vector2(0.5f, 0.26f), new Vector2(640f, 96f));

            return resume;
        }

        public void Show() => sheet.Show();
    }

    /// <summary>
    /// Shared screen scaffolding. Universal-fit pass: each screen root is now a
    /// TRANSPARENT safe-area-padded rect (content never under a notch), while the
    /// backdrop (bg + snow + vignette) is ONE shared full-bleed layer behind the
    /// stack (ScreenBackdrop) — it must extend under notches, and one snow system
    /// beats eight (§9 budgets).
    /// </summary>
    internal static class ScreenChrome
    {
        /// <summary>Header band height (ref-px) every Tidal Pop screen reserves at the top.</summary>
        public const float HeaderRefPx = 200f;

        /// <summary>Content floor (ref-px above the safe bottom) for screens under the tab bar.</summary>
        public const float TabFloorRefPx = TabBar.HeightRefPx + 70f;

        /// <summary>
        /// Tidal Pop header: optional chunky back button (left), the title in
        /// sticker letters (center), optional coin pill (right; its plus opens
        /// the shop). Returns the coin counter when one is shown.
        /// </summary>
        public static CoinCounter? Header(RectTransform root, GameFlow flow, string title, Action? onBack,
            bool coins, bool coinPlus = true)
        {
            RectTransform header = UiComponents.Rect(root, "header", Vector2.zero);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.offsetMin = new Vector2(0f, -HeaderRefPx);
            header.offsetMax = Vector2.zero;

            TextMeshProUGUI titleText = UiText.CreateOutlined(header, "title", title, "title",
                "text.primary", "panel.edge", 0.26f);
            UiComponents.Place(titleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(600f, 100f));

            if (onBack != null)
            {
                Button back = PopKit.IconButton(header, "back", "chevronLeft", PopTone.Blue, onBack,
                    new Vector2(124f, 130f));
                var backRt = (RectTransform)back.transform;
                backRt.anchorMin = backRt.anchorMax = new Vector2(0f, 0.5f);
                backRt.anchoredPosition = new Vector2(112f, 0f);
            }

            CoinCounter? counter = null;
            if (coins)
            {
                counter = PopKit.CoinPill(header, coinPlus ? () => flow.GoTo(FlowScreen.Shop) : null);
                var pill = (RectTransform)counter.transform.parent;
                pill.anchorMin = pill.anchorMax = new Vector2(1f, 0.5f);
                pill.anchoredPosition = new Vector2(-205f, 0f);
            }

            return counter;
        }

        /// <summary>
        /// A vertical scroll column between the header and a floor (ref-px above the
        /// safe bottom). Content stacks top-down via <see cref="Column.Add"/>; short
        /// screens never scroll, 16:9 phones scroll instead of clipping.
        /// </summary>
        public static Column ScrollColumn(RectTransform root, float floorRefPx)
        {
            RectTransform viewport = UiComponents.Rect(root, "column", Vector2.zero);
            UiComponents.Stretch(viewport);
            viewport.offsetMin = new Vector2(0f, floorRefPx);
            viewport.offsetMax = new Vector2(0f, -HeaderRefPx);
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.sprite = SpriteFactory.Solid();
            hit.color = new Color(0f, 0f, 0f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            RectTransform content = UiComponents.Rect(viewport, "content", Vector2.zero);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            scroll.content = content;
            return new Column(content);
        }

        /// <summary>Top-down stacking inside a scroll column.</summary>
        public sealed class Column
        {
            private float y = 24f;

            public Column(RectTransform content)
            {
                Content = content;
            }

            public RectTransform Content { get; }

            /// <summary>Blank space before the next item (e.g. room for a ribbon above a card).</summary>
            public void Space(float px)
            {
                y += px;
                Content.sizeDelta = new Vector2(0f, y);
            }

            public RectTransform Add(RectTransform item, float height, float gapAfter = 40f)
            {
                item.anchorMin = item.anchorMax = new Vector2(0.5f, 1f);
                item.pivot = new Vector2(0.5f, 1f);
                item.anchoredPosition = new Vector2(0f, -y);
                y += height + gapAfter;
                Content.sizeDelta = new Vector2(0f, y);
                return item;
            }
        }

        public static RectTransform Root(RectTransform parent, string name)
        {
            RectTransform root = UiComponents.Rect(parent, name, Vector2.zero);
            UiComponents.Stretch(root);

            // Screens stay opaque to input even though their pixels come from the
            // shared backdrop — stray taps must not reach lower stack entries.
            var blocker = root.gameObject.AddComponent<Image>();
            blocker.sprite = SpriteFactory.Solid();
            blocker.color = new Color(0f, 0f, 0f, 0f);

            root.gameObject.AddComponent<SafeArea>();
            return root;
        }
    }

    /// <summary>
    /// The single full-bleed menu backdrop living behind the screen stack.
    /// Visual pass: layered depth — base color, brighter water above, god rays
    /// from the surface, drifting bokeh snow, vignette. We are UNDER the ocean;
    /// the backdrop finally says so.
    /// </summary>
    internal static class ScreenBackdrop
    {
        public static RectTransform Create(RectTransform parent)
        {
            RectTransform root = UiComponents.Rect(parent, "Backdrop", Vector2.zero);
            UiComponents.Stretch(root);
            var bg = root.gameObject.AddComponent<Image>();
            bg.sprite = SpriteFactory.Solid();
            ThemedElement.Bind(root.gameObject, "bg.deep");

            // Tidal Pop: a bright, sunlit ocean — turquoise at the surface, deep
            // blue at the floor (DECISIONS 2026-10-18).
            Band(root, "surfaceGlow", 0.38f, 1f, "bg.oceanTop", fadeDown: true);
            Band(root, "floorShade", 0f, 0.42f, "bg.abyss", fadeDown: false);

            // Soft white rays and caustic light patches near the surface.
            BuildRay(root, x: 0.22f, tiltDeg: 16f, widthRefPx: 300f);
            BuildRay(root, x: 0.55f, tiltDeg: -8f, widthRefPx: 220f);
            BuildRay(root, x: 0.84f, tiltDeg: 20f, widthRefPx: 160f);
            Caustic(root, new Vector2(0.18f, 0.93f), 640f);
            Caustic(root, new Vector2(0.86f, 0.78f), 480f);

            CanvasSnow.Create(root);
            root.SetAsFirstSibling();
            return root;
        }

        private static void Band(RectTransform root, string name, float yMin, float yMax, string token, bool fadeDown)
        {
            RectTransform band = UiComponents.Rect(root, name, Vector2.zero);
            band.anchorMin = new Vector2(0f, yMin);
            band.anchorMax = new Vector2(1f, yMax);
            band.offsetMin = Vector2.zero;
            band.offsetMax = Vector2.zero;
            if (fadeDown)
            {
                band.localScale = new Vector3(1f, -1f, 1f); // fade points down from the top
            }

            var image = band.gameObject.AddComponent<Image>();
            image.sprite = SpriteFactory.VerticalFade();
            image.raycastTarget = false;
            ThemedElement.Bind(band.gameObject, token);
        }

        private static void Caustic(RectTransform root, Vector2 anchor, float size)
        {
            RectTransform patch = UiComponents.Rect(root, "caustic", new Vector2(size, size * 0.55f));
            patch.anchorMin = patch.anchorMax = anchor;
            var image = patch.gameObject.AddComponent<Image>();
            image.sprite = MenuSprites.SoftGlow();
            image.raycastTarget = false;
            ThemedElement.Bind(patch.gameObject, "ray.light");
        }

        private static void BuildRay(RectTransform root, float x, float tiltDeg, float widthRefPx)
        {
            RectTransform ray = UiComponents.Rect(root, "ray", new Vector2(widthRefPx, 2000f));
            ray.anchorMin = new Vector2(x, 1f);
            ray.anchorMax = new Vector2(x, 1f);
            ray.pivot = new Vector2(0.5f, 1f);
            ray.anchoredPosition = new Vector2(0f, 80f);
            ray.localRotation = Quaternion.Euler(0f, 0f, tiltDeg);
            var image = ray.gameObject.AddComponent<Image>();
            image.sprite = MenuSprites.LightRay();
            image.raycastTarget = false;
            ThemedElement.Bind(ray.gameObject, "ray.light");
        }
    }
}
