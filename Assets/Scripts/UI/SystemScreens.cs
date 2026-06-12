using System;
using Riptide.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>Spec §4.7 settings: toggle rows + ghost rows. Plain, fast, done.</summary>
    public sealed class SettingsScreen : MonoBehaviour, IScreenRefresh
    {
        private readonly System.Collections.Generic.List<(string key, string label, Button button)> toggles
            = new System.Collections.Generic.List<(string, string, Button)>();

        public static RectTransform Build(RectTransform parent, GameFlow flow)
        {
            RectTransform root = ScreenChrome.Root(parent, "SettingsScreen");
            var screen = root.gameObject.AddComponent<SettingsScreen>();

            TextMeshProUGUI title = UiText.Create(root, "title", flow.Strings.Get("settings.title"),
                "title", "text.primary");
            UiComponents.Place(title.rectTransform, new Vector2(0.5f, 0.92f), new Vector2(700f, 100f));

            screen.AddToggle(root, 0.80f, "settings.audio.on", flow.Strings.Get("settings.audio"));
            screen.AddToggle(root, 0.71f, "settings.music.on", flow.Strings.Get("settings.music"));
            screen.AddToggle(root, 0.62f, "settings.haptics.on", flow.Strings.Get("settings.haptics"));
            screen.AddToggle(root, 0.53f, "settings.reducedMotion.on", flow.Strings.Get("settings.reducedMotion"));

            Ghost(root, 0.42f, flow.Strings.Get("settings.consent"), () => flow.Consent?.Reopen());
            Ghost(root, 0.35f, flow.Strings.Get("settings.restore"), () => flow.Iap?.Restore());
            Ghost(root, 0.28f, flow.Strings.Get("settings.privacy"),
                () => Application.OpenURL("https://riptide.game/privacy"));
            Ghost(root, 0.21f, flow.Strings.Get("settings.terms"),
                () => Application.OpenURL("https://riptide.game/terms"));
            Ghost(root, 0.12f, flow.Strings.Get("common.back"), () => flow.GoTo(FlowScreen.Home));

            TextMeshProUGUI version = UiText.Create(root, "version",
                string.Format(flow.Strings.Get("settings.version"), Application.version),
                "micro", "text.muted");
            UiComponents.Place(version.rectTransform, new Vector2(0.5f, 0.05f), new Vector2(500f, 40f));

            screen.Refresh();
            return root;
        }

        private void AddToggle(RectTransform root, float y, string key, string label)
        {
            Button row = UiComponents.ButtonSecondary(root, key, "", () => Toggle(key));
            UiComponents.Place((RectTransform)row.transform, new Vector2(0.5f, y), new Vector2(760f, 110f));
            toggles.Add((key, label, row));
        }

        private static void Ghost(RectTransform root, float y, string label, Action onClick)
        {
            Button row = UiComponents.ButtonGhost(root, label, label, onClick);
            UiComponents.Place((RectTransform)row.transform, new Vector2(0.5f, y), new Vector2(760f, 84f));
        }

        private void Toggle(string key)
        {
            PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key, key == "settings.reducedMotion.on" ? 0 : 1) == 1 ? 0 : 1);
            PlayerPrefs.Save();
            Refresh();
        }

        public void Refresh()
        {
            foreach ((string key, string label, Button button) in toggles)
            {
                int fallback = key == "settings.reducedMotion.on" ? 0 : 1;
                bool on = PlayerPrefs.GetInt(key, fallback) == 1;
                button.GetComponentInChildren<TextMeshProUGUI>().text = $"{label} — {(on ? "ON" : "OFF")}";
            }
        }
    }

    /// <summary>
    /// Spec §4.7 shop: Remove Ads hero card (price + one-line promise) above three
    /// coin pack cards. Packs stay disabled with a caption until the IAP SDK lands
    /// (§6.2 offline/disabled rule); Remove Ads is fully wired through the seam.
    /// </summary>
    public sealed class ShopScreen : MonoBehaviour, IScreenRefresh
    {
        private GameFlow flow = null!;
        private ScreenManager manager = null!;
        private Button buy = null!;

        public static RectTransform Build(RectTransform parent, GameFlow flow, ScreenManager manager)
        {
            RectTransform root = ScreenChrome.Root(parent, "ShopScreen");
            var screen = root.gameObject.AddComponent<ShopScreen>();
            screen.flow = flow;
            screen.manager = manager;

            TextMeshProUGUI title = UiText.Create(root, "title", flow.Strings.Get("shop.title"),
                "title", "text.primary");
            UiComponents.Place(title.rectTransform, new Vector2(0.5f, 0.92f), new Vector2(700f, 100f));

            RectTransform hero = UiComponents.Card(root, "hero", new Vector2(880f, 360f));
            UiComponents.Place(hero, new Vector2(0.5f, 0.70f), new Vector2(880f, 360f));
            TextMeshProUGUI heroTitle = UiText.Create(hero, "heroTitle",
                flow.Strings.Get("shop.removeAds"), "heading", "text.primary");
            UiComponents.Place(heroTitle.rectTransform, new Vector2(0.5f, 0.78f), new Vector2(800f, 80f));
            TextMeshProUGUI promise = UiText.Create(hero, "promise",
                flow.Strings.Get("shop.promise"), "caption", "text.secondary");
            UiComponents.Place(promise.rectTransform, new Vector2(0.5f, 0.55f), new Vector2(800f, 60f));
            screen.buy = UiComponents.ButtonPrimary(hero, "buy", flow.Strings.Get("shop.removeAds"),
                screen.OnBuyRemoveAds);
            UiComponents.Place((RectTransform)screen.buy.transform, new Vector2(0.5f, 0.22f), new Vector2(620f, 120f));

            string[] packKeys = { "shop.pack.small", "shop.pack.medium", "shop.pack.large" };
            for (int i = 0; i < 3; i++)
            {
                RectTransform pack = UiComponents.Card(root, $"pack{i}", new Vector2(300f, 320f));
                UiComponents.Place(pack, new Vector2(0.20f + 0.30f * i, 0.40f), new Vector2(300f, 320f));
                TextMeshProUGUI packName = UiText.Create(pack, "name",
                    flow.Strings.Get(packKeys[i]), "body", "text.primary");
                UiComponents.Place(packName.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(280f, 110f));
                TextMeshProUGUI soon = UiText.Create(pack, "soon",
                    flow.Strings.Get("shop.comingSoon"), "micro", "text.muted");
                UiComponents.Place(soon.rectTransform, new Vector2(0.5f, 0.2f), new Vector2(280f, 80f));
            }

            Button back = UiComponents.ButtonGhost(root, "back", flow.Strings.Get("common.back"),
                () => flow.GoTo(FlowScreen.Home));
            UiComponents.Place((RectTransform)back.transform, new Vector2(0.5f, 0.08f), new Vector2(400f, 90f));

            screen.Refresh();
            return root;
        }

        private void OnBuyRemoveAds()
        {
            flow.Iap?.PurchaseRemoveAds(success =>
            {
                manager.Toasts.Show(flow.Strings.Get(success ? "shop.thanks" : "errors.purchase_failed"));
                Refresh();
            });
        }

        public void Refresh()
        {
            bool owned = flow.Iap != null && flow.Iap.RemoveAdsOwned;
            buy.interactable = !owned;
            buy.GetComponentInChildren<TextMeshProUGUI>().text =
                flow.Strings.Get(owned ? "shop.thanks" : "shop.removeAds");
        }
    }

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
            Sheet sheet = UiComponents.SheetComponent(canvasRoot, "PauseSheet", 760f);
            var pause = sheet.gameObject.AddComponent<PauseSheet>();
            pause.sheet = sheet;
            pause.flow = flow;

            RectTransform body = sheet.Body;
            TextMeshProUGUI title = UiText.Create(body, "title", flow.Strings.Get("pause.title"),
                "heading", "text.primary");
            UiComponents.Place(title.rectTransform, new Vector2(0.5f, 0.86f), new Vector2(700f, 80f));

            Button resume = UiComponents.ButtonPrimary(body, "resume", flow.Strings.Get("pause.resume"),
                pause.Dismiss);
            UiComponents.Place((RectTransform)resume.transform, new Vector2(0.5f, 0.62f), new Vector2(640f, 124f));

            pause.sound = UiComponents.ButtonSecondary(body, "sound", "", pause.ToggleSound);
            UiComponents.Place((RectTransform)pause.sound.transform, new Vector2(0.5f, 0.40f), new Vector2(640f, 110f));

            Button home = UiComponents.ButtonGhost(body, "home", flow.Strings.Get("pause.home"), () =>
            {
                pause.Dismiss();
                flow.GoTo(FlowScreen.Home);
            });
            UiComponents.Place((RectTransform)home.transform, new Vector2(0.5f, 0.18f), new Vector2(640f, 90f));

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
    /// Spec §4.7 first-run age gate: neutral year picker on bg.abyss, no game art
    /// that skews child-directed. The stored year feeds ad configuration when the
    /// real SDKs land; the (fake) consent flow itself is requested at boot.
    /// </summary>
    public sealed class ConsentAgeGate : MonoBehaviour
    {
        public const string BirthYearKey = "consent.birthYear";
        private int year = 2000;
        private TextMeshProUGUI yearLabel = null!;

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

            // One composed card instead of elements adrift on the void.
            RectTransform card = UiComponents.Card(root, "card", new Vector2(880f, 820f));
            UiComponents.Place(card, new Vector2(0.5f, 0.55f), new Vector2(880f, 820f));

            TextMeshProUGUI title = UiText.Create(card, "title",
                flow.Strings.Get("consent.age.title"), "title", "text.primary");
            UiComponents.Place(title.rectTransform, new Vector2(0.5f, 0.84f), new Vector2(800f, 100f));
            TextMeshProUGUI body = UiText.Create(card, "body",
                flow.Strings.Get("consent.age.body"), "body", "text.secondary");
            UiComponents.Place(body.rectTransform, new Vector2(0.5f, 0.70f), new Vector2(800f, 60f));

            // Year row: steppers flank the numeral, not the screen edges.
            Button minus = UiComponents.IconButton(card, "minus", "‹", () => gate.Step(-1));
            UiComponents.Place((RectTransform)minus.transform, new Vector2(0.5f, 0.50f), new Vector2(96f, 96f));
            ((RectTransform)minus.transform).anchoredPosition = new Vector2(-280f, 0f);
            gate.yearLabel = UiText.Create(card, "year", "2000", "display", "text.primary");
            UiComponents.Place(gate.yearLabel.rectTransform, new Vector2(0.5f, 0.50f), new Vector2(420f, 120f));
            Button plus = UiComponents.IconButton(card, "plus", "›", () => gate.Step(1));
            UiComponents.Place((RectTransform)plus.transform, new Vector2(0.5f, 0.50f), new Vector2(96f, 96f));
            ((RectTransform)plus.transform).anchoredPosition = new Vector2(280f, 0f);

            Button confirm = UiComponents.ButtonPrimary(card, "confirm",
                flow.Strings.Get("consent.age.confirm"), gate.Confirm);
            UiComponents.Place((RectTransform)confirm.transform, new Vector2(0.5f, 0.18f), new Vector2(640f, 130f));

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
        }
    }

    /// <summary>Shared screen scaffolding: full-bleed themed background root.</summary>
    internal static class ScreenChrome
    {
        public static RectTransform Root(RectTransform parent, string name)
        {
            RectTransform root = UiComponents.Rect(parent, name, Vector2.zero);
            UiComponents.Stretch(root);
            var bg = root.gameObject.AddComponent<Image>();
            bg.sprite = SpriteFactory.Solid();
            ThemedElement.Bind(root.gameObject, "bg.deep");
            root.gameObject.AddComponent<SafeArea>();
            return root;
        }
    }
}
