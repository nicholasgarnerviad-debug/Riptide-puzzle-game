using Riptide.Core;
using Riptide.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>Home (GDD 9): Voyage continue, Endless, Daily + streak flame, Tidepool, settings gear.</summary>
    public sealed class HomeScreen : MonoBehaviour, IScreenRefresh
    {
        private GameFlow flow = null!;
        private Text voyageLabel = null!;
        private Text streakLabel = null!;

        public static RectTransform Build(RectTransform parent, GameFlow flow)
        {
            RectTransform root = UiKit.Panel(parent, "HomeScreen", UiKit.PanelColor);
            UiKit.Stretch(root);
            var screen = root.gameObject.AddComponent<HomeScreen>();
            screen.flow = flow;

            Text title = UiKit.Label(root, "title", flow.Strings.Get("app.title"), 110, UiKit.TextColor);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 0.86f), new Vector2(900f, 140f), Vector2.zero);

            Button voyage = UiKit.TextButton(root, "voyage", flow.Strings.Get("home.voyage"), 52,
                () => flow.GoTo(FlowScreen.ZoneMap), UiKit.ButtonAccent);
            UiKit.Place((RectTransform)voyage.transform, new Vector2(0.5f, 0.62f), new Vector2(640f, 130f), Vector2.zero);
            screen.voyageLabel = voyage.GetComponentInChildren<Text>();

            Button endless = UiKit.TextButton(root, "endless", flow.Strings.Get("home.endless"), 52,
                flow.StartEndless);
            UiKit.Place((RectTransform)endless.transform, new Vector2(0.5f, 0.50f), new Vector2(640f, 130f), Vector2.zero);

            Button daily = UiKit.TextButton(root, "daily", flow.Strings.Get("home.daily"), 52,
                () => flow.StartDaily());
            UiKit.Place((RectTransform)daily.transform, new Vector2(0.5f, 0.38f), new Vector2(640f, 130f), Vector2.zero);

            screen.streakLabel = UiKit.Label(root, "streak", "", 40, Palette.MeterDanger);
            UiKit.Place(screen.streakLabel.rectTransform, new Vector2(0.5f, 0.315f), new Vector2(640f, 60f), Vector2.zero);

            Button tidepool = UiKit.TextButton(root, "tidepool", flow.Strings.Get("home.tidepool"), 44,
                () => flow.GoTo(FlowScreen.Tidepool));
            UiKit.Place((RectTransform)tidepool.transform, new Vector2(0.5f, 0.22f), new Vector2(640f, 110f), Vector2.zero);

            Button settings = UiKit.TextButton(root, "settings", flow.Strings.Get("home.settings"), 36,
                () => flow.GoTo(FlowScreen.Settings));
            UiKit.Place((RectTransform)settings.transform, new Vector2(0.5f, 0.10f), new Vector2(400f, 90f), Vector2.zero);

            screen.Refresh();
            return root;
        }

        public void Refresh()
        {
            (int zone, int index) = flow.Meta.Voyage.NextLevel();
            voyageLabel.text = flow.Meta.Voyage.TotalStars == 0
                ? flow.Strings.Get("home.voyage")
                : string.Format(flow.Strings.Get("home.voyageContinue"), zone, index);

            int streak = flow.Meta.Streak.Current;
            streakLabel.text = streak > 0 ? string.Format(flow.Strings.Get("home.dailyStreak"), streak) : "";
        }
    }

    /// <summary>Zone map (GDD 9): vertical scroll, 10 zones x 20 nodes with stars and locks.</summary>
    public sealed class ZoneMapScreen : MonoBehaviour, IScreenRefresh
    {
        private GameFlow flow = null!;
        private readonly System.Collections.Generic.List<(int zone, int index, Button button, Text label)> nodes
            = new System.Collections.Generic.List<(int, int, Button, Text)>();

        public static RectTransform Build(RectTransform parent, GameFlow flow)
        {
            RectTransform root = UiKit.Panel(parent, "ZoneMapScreen", UiKit.PanelColor);
            UiKit.Stretch(root);
            var screen = root.gameObject.AddComponent<ZoneMapScreen>();
            screen.flow = flow;

            Button back = UiKit.TextButton(root, "back", flow.Strings.Get("common.back"), 36,
                () => flow.GoTo(FlowScreen.Home));
            UiKit.Place((RectTransform)back.transform, new Vector2(0.12f, 0.955f), new Vector2(180f, 70f), Vector2.zero);

            RectTransform scrollRoot = UiKit.Panel(root, "scroll", new Color(0f, 0f, 0f, 0.25f));
            scrollRoot.anchorMin = new Vector2(0.04f, 0.02f);
            scrollRoot.anchorMax = new Vector2(0.96f, 0.92f);
            scrollRoot.offsetMin = Vector2.zero;
            scrollRoot.offsetMax = Vector2.zero;
            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scrollRoot.gameObject.AddComponent<RectMask2D>();

            RectTransform content = UiKit.Container(scrollRoot, "content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;

            const float headerH = 90f;
            const float cell = 180f;
            const float pad = 14f;
            float y = 0f;
            for (int zone = 1; zone <= 10; zone++)
            {
                Text header = UiKit.Label(content, $"zone{zone}",
                    string.Format(flow.Strings.Get("zone.title"), zone), 48, UiKit.TextColor);
                UiKit.Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(600f, headerH),
                    new Vector2(0f, -y - headerH * 0.5f));
                y += headerH;

                for (int index = 1; index <= 20; index++)
                {
                    int col = (index - 1) % 5;
                    int row = (index - 1) / 5;
                    int zoneCopy = zone;
                    int indexCopy = index;
                    Button node = UiKit.TextButton(content, $"z{zone}l{index}", index.ToString(), 40,
                        () => screen.OnNode(zoneCopy, indexCopy));
                    UiKit.Place((RectTransform)node.transform, new Vector2(0.5f, 1f),
                        new Vector2(cell - pad, cell - pad),
                        new Vector2((col - 2) * cell, -y - row * cell - cell * 0.5f));
                    screen.nodes.Add((zone, index, node, node.GetComponentInChildren<Text>()));
                }

                y += 4 * cell + 30f;
            }

            content.sizeDelta = new Vector2(0f, y + 40f);
            screen.Refresh();
            return root;
        }

        private void OnNode(int zone, int index)
        {
            if (flow.Meta.Voyage.IsUnlocked(zone, index))
            {
                flow.StartVoyageLevel(zone, index);
            }
        }

        public void Refresh()
        {
            foreach ((int zone, int index, Button button, Text label) in nodes)
            {
                bool unlocked = flow.Meta.Voyage.IsUnlocked(zone, index);
                int stars = flow.Meta.Voyage.StarsFor(VoyageProgress.LevelId(zone, index));
                button.interactable = unlocked;
                label.text = unlocked
                    ? (stars > 0 ? $"{index}\n{new string('★', stars)}" : index.ToString())
                    : "🔒";
                label.color = unlocked ? UiKit.TextColor : UiKit.TextDim;
            }
        }
    }

    /// <summary>Settings (GDD 9): audio/haptics toggles; consent, restore, policy links stubbed for P7/P8.</summary>
    public sealed class SettingsScreen : MonoBehaviour, IScreenRefresh
    {
        private GameFlow flow = null!;
        private Text audioLabel = null!;
        private Text hapticsLabel = null!;

        public static RectTransform Build(RectTransform parent, GameFlow flow)
        {
            RectTransform root = UiKit.Panel(parent, "SettingsScreen", UiKit.PanelColor);
            UiKit.Stretch(root);
            var screen = root.gameObject.AddComponent<SettingsScreen>();
            screen.flow = flow;

            Text title = UiKit.Label(root, "title", flow.Strings.Get("settings.title"), 64, UiKit.TextColor);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 0.9f), new Vector2(700f, 100f), Vector2.zero);

            Button audio = UiKit.TextButton(root, "audio", "", 44, () => screen.Toggle("settings.audio.on"));
            UiKit.Place((RectTransform)audio.transform, new Vector2(0.5f, 0.72f), new Vector2(640f, 110f), Vector2.zero);
            screen.audioLabel = audio.GetComponentInChildren<Text>();

            Button haptics = UiKit.TextButton(root, "haptics", "", 44, () => screen.Toggle("settings.haptics.on"));
            UiKit.Place((RectTransform)haptics.transform, new Vector2(0.5f, 0.60f), new Vector2(640f, 110f), Vector2.zero);
            screen.hapticsLabel = haptics.GetComponentInChildren<Text>();

            Button consent = UiKit.TextButton(root, "consent", flow.Strings.Get("settings.consent"), 38, () => { });
            consent.interactable = false;
            UiKit.Place((RectTransform)consent.transform, new Vector2(0.5f, 0.48f), new Vector2(640f, 100f), Vector2.zero);

            Button restore = UiKit.TextButton(root, "restore", flow.Strings.Get("settings.restore"), 38, () => { });
            restore.interactable = false;
            UiKit.Place((RectTransform)restore.transform, new Vector2(0.5f, 0.37f), new Vector2(640f, 100f), Vector2.zero);

            Button privacy = UiKit.TextButton(root, "privacy", flow.Strings.Get("settings.privacy"), 38,
                () => Application.OpenURL("https://riptide.game/privacy"));
            UiKit.Place((RectTransform)privacy.transform, new Vector2(0.5f, 0.26f), new Vector2(640f, 100f), Vector2.zero);

            Button terms = UiKit.TextButton(root, "terms", flow.Strings.Get("settings.terms"), 38,
                () => Application.OpenURL("https://riptide.game/terms"));
            UiKit.Place((RectTransform)terms.transform, new Vector2(0.5f, 0.15f), new Vector2(640f, 100f), Vector2.zero);

            Button back = UiKit.TextButton(root, "back", flow.Strings.Get("common.back"), 40,
                () => flow.GoTo(FlowScreen.Home));
            UiKit.Place((RectTransform)back.transform, new Vector2(0.5f, 0.05f), new Vector2(400f, 90f), Vector2.zero);

            screen.Refresh();
            return root;
        }

        private void Toggle(string key)
        {
            PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key, 1) == 1 ? 0 : 1);
            PlayerPrefs.Save();
            Refresh();
        }

        public void Refresh()
        {
            audioLabel.text = $"{flow.Strings.Get("settings.audio")}: {OnOff("settings.audio.on")}";
            hapticsLabel.text = $"{flow.Strings.Get("settings.haptics")}: {OnOff("settings.haptics.on")}";
        }

        private static string OnOff(string key) => PlayerPrefs.GetInt(key, 1) == 1 ? "ON" : "OFF";
    }

    /// <summary>Shop sheet (GDD 9) — modal stub until Phase 7 wires IAP.</summary>
    public static class ShopSheet
    {
        public static RectTransform Build(RectTransform parent, GameFlow flow)
        {
            RectTransform root = UiKit.Panel(parent, "ShopSheet", new Color(0f, 0f, 0f, 0.6f));
            UiKit.Stretch(root);

            RectTransform sheet = UiKit.Panel(root, "sheet", UiKit.PanelColor);
            UiKit.Place(sheet, new Vector2(0.5f, 0.5f), new Vector2(820f, 700f), Vector2.zero);

            Text title = UiKit.Label(sheet, "title", flow.Strings.Get("shop.title"), 56, UiKit.TextColor);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 0.85f), new Vector2(700f, 90f), Vector2.zero);

            Button removeAds = UiKit.TextButton(sheet, "removeAds", flow.Strings.Get("shop.removeAds"), 42, () => { });
            removeAds.interactable = false;
            UiKit.Place((RectTransform)removeAds.transform, new Vector2(0.5f, 0.6f), new Vector2(680f, 110f), Vector2.zero);

            Text soon = UiKit.Label(sheet, "soon", flow.Strings.Get("shop.comingSoon"), 34, UiKit.TextDim);
            UiKit.Place(soon.rectTransform, new Vector2(0.5f, 0.4f), new Vector2(700f, 120f), Vector2.zero);

            Button close = UiKit.TextButton(sheet, "close", flow.Strings.Get("common.close"), 40,
                () => flow.GoTo(FlowScreen.Home));
            UiKit.Place((RectTransform)close.transform, new Vector2(0.5f, 0.14f), new Vector2(360f, 90f), Vector2.zero);

            return root;
        }
    }

    /// <summary>Tidepool door (GDD 9) — the scene itself is Phase 6.</summary>
    public static class TidepoolStubScreen
    {
        public static RectTransform Build(RectTransform parent, GameFlow flow)
        {
            RectTransform root = UiKit.Panel(parent, "TidepoolScreen", UiKit.PanelColor);
            UiKit.Stretch(root);

            Text title = UiKit.Label(root, "title", flow.Strings.Get("home.tidepool"), 72, UiKit.TextColor);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 0.72f), new Vector2(800f, 110f), Vector2.zero);

            Text body = UiKit.Label(root, "body", flow.Strings.Get("tidepool.placeholder"), 40, UiKit.TextDim);
            UiKit.Place(body.rectTransform, new Vector2(0.5f, 0.52f), new Vector2(860f, 160f), Vector2.zero);

            Button back = UiKit.TextButton(root, "back", flow.Strings.Get("common.back"), 40,
                () => flow.GoTo(FlowScreen.Home));
            UiKit.Place((RectTransform)back.transform, new Vector2(0.5f, 0.16f), new Vector2(400f, 90f), Vector2.zero);

            return root;
        }
    }
}
