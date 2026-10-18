using System.Collections.Generic;
using System.Text;
using Riptide.Core;
using Riptide.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// GDD §16.4 / §16.6 the living Pod (mockup B): the leader swims center stage on
    /// the sand, slots 2 and 3 beside it (each with its name and ability), every other
    /// Tideling you own idles further along the diorama; the Conch sits on the sand
    /// (its egg count, CALL); the daily pet gift rides the window's corner. Taps open
    /// the Tideling's card; blinks, happy faces and a gentle bob keep them alive.
    /// </summary>
    public sealed partial class TidepoolScreen
    {
        private sealed class Swimmer
        {
            public int Line = -1;
            public RectTransform Rt = null!;
            public Image Art = null!;
            public float BaseY;
            public float Phase;
        }

        private sealed class PodSpot
        {
            public Swimmer Swimmer = null!;
            public RectTransform Chip = null!;
            public TextMeshProUGUI Name = null!;
            public TextMeshProUGUI Ability = null!;
            public GameObject Crown = null!;
            public Image Bubble = null!;
            public GameObject Lock = null!;
        }

        private readonly PodSpot[] pod = new PodSpot[3];
        private readonly List<Swimmer> residents = new List<Swimmer>();
        private readonly Dictionary<int, float> happyUntil = new Dictionary<int, float>();
        private string residentKey = "";
        private float laidOutHeight = -1f;
        private Button giftButton = null!;
        private GameObject giftReady = null!;
        private TextMeshProUGUI conchCount = null!;
        private TidelingSheet? sheet;
        private ConchScreen? conchScreen;
        private float bobTime;
        private float nextBlink;
        private int blinking = -1;
        private float blinkUntil;

        /// <summary>The Pod line in each slot as the diorama shows it (−1 empty). Test surface.</summary>
        public int PodShown(int slot) => pod[slot].Swimmer.Line;

        /// <summary>Lines idling outside the Pod. Test surface.</summary>
        public int ResidentCount => residents.Count;

        // -------------------------------------------------------------- building

        private void BuildPod()
        {
            for (int slot = 0; slot < pod.Length; slot++)
            {
                int slotCopy = slot;
                var spot = new PodSpot();
                RectTransform rt = UiComponents.Rect(content, $"pod{slot}", new Vector2(300f, 300f));
                UiComponents.Place(rt, new Vector2(0f, 0.5f), new Vector2(300f, 300f));

                spot.Bubble = PopKit.Img(rt, "bubble", PopSprites.Bubble(), "bubble");
                UiComponents.Stretch(spot.Bubble.rectTransform);
                Image lockIcon = PopKit.Icon(rt, "lock", "lock", "panel.light", 90f, "panel.edge", 8);
                spot.Lock = lockIcon.transform.parent.gameObject;

                Image art = PopKit.Img(rt, "art", CreatureSprites.Tideling("crab", 1), "pop.white.top");
                art.preserveAspect = true;
                UiComponents.Stretch(art.rectTransform);
                art.raycastTarget = true;
                var button = rt.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => OnPodTapped(slotCopy));
                spot.Swimmer = new Swimmer { Rt = rt, Art = art, Phase = slot * 1.7f };

                spot.Chip = PopKit.Panel(content, $"podChip{slot}", new Vector2(330f, 96f), PopTone.Navy, 30f, 7f);
                spot.Chip.anchorMin = spot.Chip.anchorMax = new Vector2(0f, 0.5f);
                spot.Name = UiText.CreateOutlined(spot.Chip, "name", "", "caption", "text.primary", "panel.edge", 0.2f);
                spot.Name.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                spot.Name.rectTransform.anchorMax = new Vector2(1f, 1f);
                spot.Name.rectTransform.offsetMin = new Vector2(16f, -4f);
                spot.Name.rectTransform.offsetMax = new Vector2(-16f, -8f);
                spot.Name.enableAutoSizing = true;
                spot.Name.fontSizeMin = 20f;
                spot.Name.fontSizeMax = spot.Name.fontSize;
                spot.Ability = UiText.Create(spot.Chip, "ability", "", "caption", "pop.yellow.top");
                spot.Ability.rectTransform.anchorMin = new Vector2(0f, 0f);
                spot.Ability.rectTransform.anchorMax = new Vector2(1f, 0.5f);
                spot.Ability.rectTransform.offsetMin = new Vector2(16f, 8f);
                spot.Ability.rectTransform.offsetMax = new Vector2(-16f, 4f);
                spot.Ability.fontSize = 24f;

                Image crown = PopKit.Icon(spot.Chip, "crown", "crown", "pop.yellow", 60f, "pop.yellow.edge", 7);
                var crownRt = (RectTransform)crown.transform.parent;
                crownRt.anchorMin = crownRt.anchorMax = new Vector2(0f, 1f);
                crownRt.anchoredPosition = new Vector2(10f, 4f);
                crownRt.localRotation = Quaternion.Euler(0f, 0f, 16f);
                spot.Crown = crownRt.gameObject;
                pod[slot] = spot;
            }
        }

        private void BuildConch()
        {
            giftButton = PopKit.IconButton(frame, "gift", "gift", PopTone.Yellow, ClaimGift, new Vector2(116f, 122f),
                round: true);
            var giftRt = (RectTransform)giftButton.transform;
            giftRt.anchorMin = giftRt.anchorMax = new Vector2(0f, 1f);
            giftRt.anchoredPosition = new Vector2(100f, -92f);
            RectTransform ready = PopKit.Panel(giftRt, "ready", new Vector2(50f, 50f), PopTone.Coral, 25f, 5f);
            ready.anchorMin = ready.anchorMax = new Vector2(1f, 1f);
            ready.anchoredPosition = new Vector2(-12f, -14f);
            TextMeshProUGUI bang = UiText.CreateOutlined(ready, "label", "!", "caption", "text.primary", "pop.coral.edge");
            UiComponents.Stretch(bang.rectTransform);
            giftReady = ready.gameObject;

            // The Conch on the sand, fixed in the window's corner while the diorama scrolls.
            RectTransform spot = UiComponents.Rect(frame, "conch", new Vector2(250f, 250f));
            spot.anchorMin = spot.anchorMax = new Vector2(1f, 0f);
            spot.anchoredPosition = new Vector2(-150f, 236f);
            Image glow = PopKit.Img(spot, "glow", MenuSprites.SoftGlow(), "pop.yellow.top");
            UiComponents.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(420f, 360f));
            RectTransform shell = TidelingUi.Conch(spot, "shell", 250f);
            shell.localRotation = Quaternion.Euler(0f, 0f, 8f);
            Tappable(spot, OpenConch);

            conchCount = TidelingUi.EggPill(frame, "conchEggs", new Vector2(160f, 76f));
            var countRt = (RectTransform)conchCount.transform.parent;
            countRt.anchorMin = countRt.anchorMax = new Vector2(1f, 0f);
            countRt.anchoredPosition = new Vector2(-262f, 344f);

            Button call = PopKit.Button(frame, "call", flow.Strings.Get("tl.call"), PopTone.Yellow, OpenConch,
                new Vector2(210f, 104f), "heading");
            var callRt = (RectTransform)call.transform;
            callRt.anchorMin = callRt.anchorMax = new Vector2(1f, 0f);
            callRt.anchoredPosition = new Vector2(-150f, 64f);
        }

        /// <summary>Sizes the Pod and residents to the window's height (16:9 phones are short).</summary>
        private void LayoutPod(float h)
        {
            laidOutHeight = h;
            float width = ((RectTransform)content.parent).rect.width;
            float lead = Mathf.Min(430f, h * 0.44f);
            float side = lead * 0.7f;
            float floorY = -h * 0.5f + 150f;
            Place(pod[0], width * 0.45f, floorY + lead * 0.5f, lead);
            Place(pod[1], width * 0.17f, h * 0.16f, side);
            Place(pod[2], width * 0.7f, h * 0.2f, side);

            float size = Mathf.Min(230f, h * 0.27f);
            float step = Mathf.Min(300f, (ContentWidth - width - 160f) / Mathf.Max(1f, Mathf.Ceil(residents.Count / 2f)));
            for (int i = 0; i < residents.Count; i++)
            {
                Swimmer r = residents[i];
                r.Rt.sizeDelta = new Vector2(size, size);
                r.BaseY = i % 2 == 0 ? h * 0.17f : -h * 0.12f;
                r.Rt.anchoredPosition = new Vector2(width + 60f + (i / 2) * step + (i % 2) * step * 0.5f, r.BaseY);
            }

            foreach ((Button _, RectTransform slotRt, TextMeshProUGUI _, Image _) in decoSlots)
            {
                slotRt.anchoredPosition = new Vector2(slotRt.anchoredPosition.x, -h * 0.5f + 90f);
            }
        }

        private static void Place(PodSpot spot, float x, float y, float size)
        {
            spot.Swimmer.Rt.sizeDelta = new Vector2(size, size);
            spot.Swimmer.Rt.anchoredPosition = new Vector2(x, y);
            spot.Swimmer.BaseY = y;
            spot.Chip.anchoredPosition = new Vector2(x, y - size * 0.5f - 30f);
        }

        // ------------------------------------------------------------- behavior

        private void RefreshPod()
        {
            giftButton.gameObject.SetActive(flow.PetGiftCoins > 0 && !editMode);
            giftReady.SetActive(flow.PetGiftAvailable);
            conchCount.text = flow.Eggs.ToString();

            int open = flow.PodSlotsOpen;
            int[] slots = flow.Meta.Save.Pals.Pod;
            for (int slot = 0; slot < pod.Length; slot++)
            {
                PodSpot spot = pod[slot];
                int line = slot < slots.Length ? slots[slot] : -1;
                bool filled = line >= 0 && line < flow.Pals.Lines.Count && flow.Tideling(line).Owned;
                spot.Swimmer.Line = filled ? line : -1;
                spot.Swimmer.Art.gameObject.SetActive(filled);
                spot.Bubble.gameObject.SetActive(!filled);
                spot.Lock.SetActive(!filled && slot >= open);
                spot.Crown.SetActive(filled && slot == 0);
                if (filled)
                {
                    TidelingView t = flow.Tideling(line);
                    spot.Swimmer.Art.sprite = TidelingUi.Art(t);
                    spot.Name.text = t.FormName;
                    spot.Ability.text = TidelingUi.AbilityShort(flow, t.Line, t.Stage);
                }
                else
                {
                    spot.Name.text = string.Format(flow.Strings.Get("card.slot"), slot + 1);
                    spot.Ability.text = slot >= open
                        ? string.Format(flow.Strings.Get("card.slotLocked"), flow.Pals.PodUnlocks[slot])
                        : flow.Strings.Get("tl.slotOpen");
                }
            }

            RebuildResidents();
        }

        /// <summary>Every owned Tideling outside the Pod idles along the diorama.</summary>
        private void RebuildResidents()
        {
            var lines = new List<int>();
            var key = new StringBuilder();
            foreach (TidelingView t in flow.AllTidelings())
            {
                if (t.Owned && t.PodSlot < 0)
                {
                    lines.Add(t.Line.Id);
                    key.Append(t.Line.Id).Append(':').Append(t.Stage).Append(t.Sparkle && t.SparkleOn ? 's' : '-').Append(',');
                }
            }

            if (key.ToString() == residentKey)
            {
                return;
            }

            residentKey = key.ToString();
            foreach (Swimmer r in residents)
            {
                Destroy(r.Rt.gameObject);
            }

            residents.Clear();
            foreach (int line in lines)
            {
                int lineCopy = line;
                RectTransform rt = UiComponents.Rect(content, $"resident{line}", new Vector2(220f, 220f));
                UiComponents.Place(rt, new Vector2(0f, 0.5f), new Vector2(220f, 220f));
                Image art = PopKit.Img(rt, "art", TidelingUi.Art(flow.Tideling(line)), "pop.white.top");
                art.preserveAspect = true;
                art.raycastTarget = true;
                UiComponents.Stretch(art.rectTransform);
                var button = rt.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => OpenSheet(lineCopy));
                residents.Add(new Swimmer { Line = line, Rt = rt, Art = art, Phase = residents.Count * 0.9f });
            }

            laidOutHeight = -1f; // re-place everyone
        }

        private void OnPodTapped(int slot)
        {
            int line = pod[slot].Swimmer.Line;
            if (line >= 0)
            {
                OpenSheet(line);
            }
        }

        private RectTransform CanvasRoot => (RectTransform)GetComponentInParent<Canvas>().rootCanvas.transform;

        /// <summary>Opens a Tideling's card (Pod slots, growing up, Sparkle).</summary>
        public void OpenSheet(int line)
        {
            if (editMode)
            {
                return;
            }

            happyUntil[line] = Time.time + 0.8f;
            sheet ??= TidelingSheet.Build(CanvasRoot, flow, OpenConch);
            sheet.Show(line, Refresh);
        }

        /// <summary>Opens the Conch (egg count, odds, guarantees, Call ×1 / ×10).</summary>
        public void OpenConch()
        {
            conchScreen ??= ConchScreen.Build(CanvasRoot, flow);
            conchScreen.Open(Refresh);
        }

        private void ClaimGift()
        {
            ToastManager? toasts = GetComponentInParent<ScreenManager>()?.Toasts;
            long before = flow.Meta.Coins;
            int paid = flow.TryClaimPetGift();
            if (paid <= 0)
            {
                toasts?.Show(flow.Strings.Get("pet.giftTomorrow"));
                return;
            }

            UiJuice.Play("star");
            toasts?.Show(string.Format(flow.Strings.Get("pet.giftClaimed"), paid));
            foreach (PodSpot spot in pod)
            {
                happyUntil[spot.Swimmer.Line] = Time.time + 1f;
            }

            Refresh();
            coins.SetInstant(before);
            coins.AnimateTo(flow.Meta.Coins);
        }

        /// <summary>Layout on size change; bob, blinks and happy faces every frame.</summary>
        private void UpdatePod()
        {
            float h = ((RectTransform)content.parent).rect.height;
            if (h > 1f && !Mathf.Approximately(h, laidOutHeight))
            {
                LayoutPod(h);
            }

            if (Time.time > nextBlink)
            {
                blinking = Random.Range(0, pod.Length + residents.Count);
                blinkUntil = Time.time + 0.15f;
                nextBlink = Time.time + Random.Range(0.8f, 2.2f);
            }

            bobTime += Time.deltaTime;
            bool still = ThemeRuntime.ReducedMotion;
            for (int i = 0; i < pod.Length + residents.Count; i++)
            {
                Swimmer s = i < pod.Length ? pod[i].Swimmer : residents[i - pod.Length];
                if (s.Line < 0)
                {
                    continue;
                }

                bool happy = happyUntil.TryGetValue(s.Line, out float until) && Time.time < until;
                bool closed = happy || (i == blinking && Time.time < blinkUntil);
                Sprite face = TidelingUi.Art(flow.Tideling(s.Line), closed);
                if (s.Art.sprite != face)
                {
                    s.Art.sprite = face;
                }

                if (!still)
                {
                    Vector2 pos = s.Rt.anchoredPosition;
                    pos.y = s.BaseY + Mathf.Sin(bobTime * 1.3f + s.Phase) * 8f + (happy ? 18f : 0f);
                    s.Rt.anchoredPosition = pos;
                }
            }
        }
    }
}
