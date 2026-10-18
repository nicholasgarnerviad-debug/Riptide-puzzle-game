using System;
using Riptide.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// Home's "Buddy Stage" (Nick's pick of five Home directions, 2026-10-18): your
    /// buddy pet center stage on a sunlit sandbar — a speech bubble that says what's
    /// next (gift ready → Daily ready → next level; taps cycle cheers and its power),
    /// the daily pet-gift box, floating hearts, idle blinks and breathing, a happy
    /// hop on taps and gifts. Sizes itself to whatever height sits between the top
    /// bar and the name pill, so 16:9 and tall phones both fit.
    /// </summary>
    public sealed class BuddyStage : MonoBehaviour
    {
        private const float Floor = 70f;
        private const float HopLife = 0.7f;

        private GameFlow flow = null!;
        private Action? coinsChanged;
        private RectTransform zone = null!;
        private RectTransform spot = null!;
        private RectTransform buddyRt = null!;
        private Image buddy = null!;
        private RectTransform bubble = null!;
        private TextMeshProUGUI line = null!;
        private RectTransform gift = null!;
        private GameObject giftReady = null!;
        private RectTransform heartA = null!;
        private RectTransform heartB = null!;
        private Vector2 heartABase;
        private Vector2 heartBBase;
        private int species;
        private int lineIndex;
        private float size = 400f;
        private float laidOutHeight = -1f;
        private float idle;
        private float nextBlink;
        private float hopT = -1f;
        private float popT = -1f;

        public RectTransform Rect => zone;

        /// <summary>The species standing on the stage (the buddy, or the crab until one is picked).</summary>
        public int Species => species;

        /// <summary>What the buddy is saying right now. Test surface.</summary>
        public string Line => line.text;

        public static BuddyStage Build(RectTransform root, GameFlow flow, float bottomRefPx, float topInsetRefPx,
            Action? coinsChanged)
        {
            RectTransform zone = UiComponents.Rect(root, "buddyStage", Vector2.zero);
            zone.anchorMin = Vector2.zero;
            zone.anchorMax = Vector2.one;
            zone.offsetMin = new Vector2(0f, bottomRefPx);
            zone.offsetMax = new Vector2(0f, -topInsetRefPx);
            var stage = zone.gameObject.AddComponent<BuddyStage>();
            stage.flow = flow;
            stage.zone = zone;
            stage.coinsChanged = coinsChanged;
            stage.BuildScene();
            return stage;
        }

        private void BuildScene()
        {
            spot = Bottom(PopKit.Img(zone, "spotlight", MenuSprites.SoftGlow(), "ray.light").rectTransform);

            foreach (float side in new[] { -1f, 1f })
            {
                Image kelp = PopKit.Img(zone, side < 0 ? "kelpL" : "kelpR", MenuSprites.Icon("kelp"), "kelp");
                RectTransform rt = kelp.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(side < 0 ? 0f : 1f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.sizeDelta = new Vector2(250f, 300f);
                rt.anchoredPosition = new Vector2(-side * 70f, Floor - 20f);
            }

            Image shade = PopKit.Img(zone, "sandShade", PopSprites.Circle(), "sand.shade");
            Bottom(shade.rectTransform).sizeDelta = new Vector2(820f, 150f);
            shade.rectTransform.anchoredPosition = new Vector2(0f, Floor - 75f);
            Image sand = PopKit.Img(zone, "sand", PopSprites.Circle(), "sand");
            Bottom(sand.rectTransform).sizeDelta = new Vector2(790f, 128f);
            sand.rectTransform.anchoredPosition = new Vector2(0f, Floor - 58f);

            buddy = PopKit.Img(zone, "buddy", CreatureSprites.For(0), "pet.eye");
            buddy.raycastTarget = true;
            buddyRt = Bottom(buddy.rectTransform);
            var tap = buddy.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(OnBuddyTapped);

            heartA = (RectTransform)PopKit.Icon(zone, "heartA", "heart", "pop.coral.top", 64f, "pop.coral.edge", 7)
                .transform.parent;
            heartB = (RectTransform)PopKit.Icon(zone, "heartB", "heart", "pop.coral.top", 44f, "pop.coral.edge", 6)
                .transform.parent;
            Bottom(heartA);
            Bottom(heartB);

            BuildBubble();
            BuildGift();
        }

        private void BuildBubble()
        {
            bubble = UiComponents.Rect(zone, "speech", new Vector2(560f, 200f));
            bubble.anchorMin = bubble.anchorMax = new Vector2(1f, 0f);
            bubble.pivot = new Vector2(1f, 0f);

            // The tail sits behind the bubble: only its lower point pokes out.
            RectTransform tail = PopKit.Panel(bubble, "tail", new Vector2(52f, 52f), PopTone.White, 8f, 7f);
            tail.anchorMin = tail.anchorMax = new Vector2(0f, 0f);
            tail.anchoredPosition = new Vector2(96f, 4f);
            tail.localRotation = Quaternion.Euler(0f, 0f, 45f);
            RectTransform body = PopKit.Panel(bubble, "body", Vector2.zero, PopTone.White, 44f, 7f);
            UiComponents.Stretch(body);

            line = UiText.Create(bubble, "line", "", "heading", "text.ink");
            UiComponents.Stretch(line.rectTransform);
            line.rectTransform.offsetMin = new Vector2(34f, 18f);
            line.rectTransform.offsetMax = new Vector2(-34f, -18f);
            line.enableWordWrapping = true;
            line.enableAutoSizing = true;
            line.fontSizeMin = 30f;
            line.fontSizeMax = line.fontSize;
        }

        private void BuildGift()
        {
            Button button = PopKit.IconButton(zone, "petGift", "gift", PopTone.Yellow, ClaimGift,
                new Vector2(140f, 150f));
            gift = (RectTransform)button.transform;
            gift.anchorMin = gift.anchorMax = new Vector2(0f, 0f);
            gift.localRotation = Quaternion.Euler(0f, 0f, -6f);

            RectTransform ready = PopKit.Panel(gift, "ready", new Vector2(54f, 54f), PopTone.Coral, 27f, 5f);
            ready.anchorMin = ready.anchorMax = new Vector2(1f, 1f);
            ready.anchoredPosition = new Vector2(-10f, -12f);
            TextMeshProUGUI bang = UiText.CreateOutlined(ready, "label", "!", "caption", "text.primary",
                "pop.coral.edge");
            UiComponents.Stretch(bang.rectTransform);
            giftReady = ready.gameObject;

            TextMeshProUGUI label = UiText.CreateOutlined(gift, "label", flow.Strings.Get("home.petGift"), "caption",
                "text.primary", "panel.edge", 0.24f);
            UiComponents.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(220f, 44f));
            label.rectTransform.anchoredPosition = new Vector2(0f, -30f);
        }

        private static RectTransform Bottom(RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            return rt;
        }

        // ---------------------------------------------------------------- state

        public void Refresh()
        {
            int chosen = flow.LeaderSpecies;
            species = chosen >= 0 && chosen < flow.Roster.Count ? chosen : 0;
            buddy.sprite = CreatureSprites.For(species);
            gift.gameObject.SetActive(flow.PetGiftCoins > 0);
            giftReady.SetActive(flow.PetGiftAvailable);
            lineIndex = 0;
            Say(PrimaryLine());
            laidOutHeight = -1f;
        }

        /// <summary>The headline: a ready gift, then the Daily, then the next level.</summary>
        private string PrimaryLine()
        {
            if (flow.PetGiftAvailable)
            {
                return flow.Strings.Get("buddy.gift");
            }

            if (flow.Meta.CanAttemptDailyToday())
            {
                return flow.Strings.Get("buddy.daily");
            }

            (int _, int index) = flow.Meta.Voyage.NextLevel();
            return string.Format(flow.Strings.Get("buddy.next"), index);
        }

        private void OnBuddyTapped()
        {
            lineIndex = (lineIndex + 1) % 5;
            Say(lineIndex switch
            {
                1 => flow.Strings.Get("buddy.cheer1"),
                2 => string.Format(flow.Strings.Get("buddy.power"), ExpansionText.PowerName(flow, species)),
                3 => flow.Strings.Get("buddy.cheer2"),
                4 => flow.Strings.Get("buddy.cheer3"),
                _ => PrimaryLine(),
            });
            Hop();
        }

        private void ClaimGift()
        {
            ToastManager? toasts = GetComponentInParent<ScreenManager>()?.Toasts;
            int paid = flow.TryClaimPetGift();
            if (paid <= 0)
            {
                toasts?.Show(flow.Strings.Get("pet.giftTomorrow"));
                return;
            }

            UiJuice.Play("star");
            toasts?.Show(string.Format(flow.Strings.Get("pet.giftClaimed"), paid));
            giftReady.SetActive(false);
            Say(PrimaryLine());
            Hop();
            coinsChanged?.Invoke();
        }

        private void Say(string text)
        {
            line.text = text;
            popT = 0f;
        }

        /// <summary>A happy hop with the closed-eye smile. Public as test surface.</summary>
        public void Hop() => hopT = 0f;

        // --------------------------------------------------------------- layout

        private void LayOut()
        {
            float h = zone.rect.height;
            laidOutHeight = h;
            size = Mathf.Clamp(h - 240f, 300f, 620f);
            buddyRt.sizeDelta = new Vector2(size, size);
            buddyRt.anchoredPosition = new Vector2(-40f, Floor);
            spot.sizeDelta = new Vector2(size * 2.1f, size * 1.9f);
            spot.anchoredPosition = new Vector2(-40f, Floor - size * 0.4f);
            heartABase = new Vector2(-40f + size * 0.44f, Floor + size * 0.78f);
            heartBBase = new Vector2(-40f + size * 0.56f, Floor + size * 0.92f);
            heartA.anchoredPosition = heartABase;
            heartB.anchoredPosition = heartBBase;
            bubble.anchoredPosition = new Vector2(-30f, Mathf.Min(Floor + size * 0.9f, h - 210f));
            gift.anchoredPosition = new Vector2(120f, Floor + size * 0.42f);
        }

        private void Update()
        {
            if (!Mathf.Approximately(zone.rect.height, laidOutHeight))
            {
                LayOut();
            }

            float dt = Time.deltaTime;
            bool reduced = ThemeRuntime.ReducedMotion;
            idle += dt;

            bool happy = false;
            float lift = 0f;
            if (hopT >= 0f)
            {
                hopT += dt;
                float u = Mathf.Clamp01(hopT / HopLife);
                happy = u < 1f;
                lift = reduced ? 0f : 70f * Mathf.Abs(Mathf.Sin(u * Mathf.PI * 2f)) * (1f - u);
                if (u >= 1f)
                {
                    hopT = -1f;
                }
            }

            // Idle life: a soft breath (squash and stretch), blinks every few seconds.
            float breath = reduced ? 0f : 0.025f * Mathf.Sin(idle * 2.4f);
            buddyRt.localScale = new Vector3(1f + breath, 1f - breath, 1f);
            buddyRt.anchoredPosition = new Vector2(-40f, Floor + lift);
            if (Time.time > nextBlink + 0.14f)
            {
                nextBlink = Time.time + UnityEngine.Random.Range(2.4f, 5f);
            }

            bool blinking = Time.time > nextBlink && Time.time < nextBlink + 0.14f;
            buddy.sprite = happy || blinking ? CreatureSprites.Blink(species) : CreatureSprites.For(species);

            if (!reduced)
            {
                heartA.anchoredPosition = heartABase + new Vector2(0f, 10f * Mathf.Sin(idle * 1.8f));
                heartB.anchoredPosition = heartBBase + new Vector2(0f, 8f * Mathf.Sin(idle * 2.1f + 1f));
                gift.localRotation = Quaternion.Euler(0f, 0f, -6f + (giftReady.activeSelf ? 5f * Mathf.Sin(idle * 6f) : 0f));
            }

            if (popT >= 0f)
            {
                popT += dt;
                float u = Mathf.Clamp01(popT / 0.25f);
                float back = 1f + 2.2f * Mathf.Pow(u - 1f, 3f) + 1.2f * Mathf.Pow(u - 1f, 2f); // easeOutBack
                float s = reduced ? 1f : Mathf.LerpUnclamped(0.7f, 1f, back);
                bubble.localScale = Vector3.one * s;
                if (u >= 1f)
                {
                    bubble.localScale = Vector3.one;
                    popT = -1f;
                }
            }
        }
    }
}
