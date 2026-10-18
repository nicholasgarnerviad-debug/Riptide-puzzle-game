using System.Collections.Generic;
using Riptide.Core;
using Riptide.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// GDD §15.2 Pet Pals on the Tidepool: level pills on every met creature, the
    /// info card's level / progress / power / Make Buddy row, the daily pet-gift
    /// button in the header, and the creatures' idle blinks and happy taps.
    /// </summary>
    public sealed partial class TidepoolScreen
    {
        private Image infoPortrait = null!;
        private TextMeshProUGUI infoName = null!;
        private TextMeshProUGUI infoCount = null!;
        private TextMeshProUGUI infoFlavor = null!;
        private PopBar infoProgress = null!;
        private RectTransform powerRow = null!;
        private TextMeshProUGUI powerName = null!;
        private TextMeshProUGUI powerDesc = null!;
        private Button buddyButton = null!;
        private TextMeshProUGUI buddyBonus = null!;
        private Button giftButton = null!;
        private GameObject giftReady = null!;

        private readonly List<(GameObject pill, TextMeshProUGUI label, GameObject heart)> petPills
            = new List<(GameObject, TextMeshProUGUI, GameObject)>();
        private readonly Dictionary<int, float> happyUntil = new Dictionary<int, float>();
        private float nextBlink;
        private int blinking = -1;
        private float blinkUntil;

        // -------------------------------------------------------------- building

        private void BuildGiftButton(RectTransform root)
        {
            giftButton = PopKit.IconButton(root, "gift", "gift", PopTone.Yellow, ClaimGift, new Vector2(116f, 122f),
                round: true);
            var giftRt = (RectTransform)giftButton.transform;
            giftRt.anchorMin = giftRt.anchorMax = new Vector2(0f, 1f);
            giftRt.anchoredPosition = new Vector2(330f, -ScreenChrome.HeaderRefPx * 0.5f);
            RectTransform ready = PopKit.Panel(giftRt, "ready", new Vector2(50f, 50f), PopTone.Coral, 25f, 5f);
            ready.anchorMin = ready.anchorMax = new Vector2(1f, 1f);
            ready.anchoredPosition = new Vector2(-12f, -14f);
            TextMeshProUGUI bang = UiText.CreateOutlined(ready, "label", "!", "caption", "text.primary", "pop.coral.edge");
            UiComponents.Stretch(bang.rectTransform);
            giftReady = ready.gameObject;
        }

        /// <summary>A level pill (and buddy heart) under a creature's well.</summary>
        private void BuildPetPill(RectTransform well)
        {
            RectTransform pill = PopKit.Panel(well, "level", new Vector2(92f, 44f), PopTone.Coral, 22f, 5f);
            pill.anchorMin = pill.anchorMax = new Vector2(1f, 0f);
            pill.anchoredPosition = new Vector2(-10f, 6f);
            TextMeshProUGUI label = UiText.CreateOutlined(pill, "label", "", "micro", "text.primary", "pop.coral.edge");
            UiComponents.Stretch(label.rectTransform);

            Image heart = PopKit.Icon(well, "buddy", "heart", "pop.coral", 50f, "panel.edge", 6);
            var heartRt = (RectTransform)heart.transform.parent;
            heartRt.anchorMin = heartRt.anchorMax = new Vector2(0f, 1f);
            heartRt.anchoredPosition = new Vector2(12f, -10f);
            petPills.Add((pill.gameObject, label, heartRt.gameObject));
        }

        private void BuildInfoCard(RectTransform root)
        {
            infoCard = BottomCard(root, "infoCard");

            RectTransform portraitWell = PopKit.Panel(infoCard, "portraitWell", new Vector2(230f, 230f),
                PopTone.Blue, 115f, 9f);
            portraitWell.anchorMin = portraitWell.anchorMax = new Vector2(0f, 1f);
            portraitWell.anchoredPosition = new Vector2(160f, -150f);
            infoPortrait = PopKit.Img(portraitWell, "portrait", CreatureSprites.Silhouette(0), "pop.navy");
            UiComponents.Place(infoPortrait.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(190f, 190f));

            infoName = UiText.Create(infoCard, "name", "", "title", "text.ink", TextAlignmentOptions.MidlineLeft);
            Pin(infoName.rectTransform, 310f, -80f, 640f, 80f);
            infoCount = UiText.Create(infoCard, "count", "", "body", "text.inkSoft", TextAlignmentOptions.MidlineLeft);
            Pin(infoCount.rectTransform, 310f, -148f, 640f, 56f);
            infoProgress = PopBar.Create(infoCard, "progress", new Vector2(640f, 58f), PopTone.Coral);
            Pin(infoProgress.Rect, 310f, -222f, 640f, 58f);

            powerRow = UiComponents.Rect(infoCard, "power", new Vector2(900f, 90f));
            powerRow.anchorMin = powerRow.anchorMax = new Vector2(0.5f, 1f);
            powerRow.anchoredPosition = new Vector2(0f, -324f);
            RectTransform pill = PopKit.Panel(powerRow, "name", new Vector2(210f, 70f), PopTone.Purple, 35f, 6f);
            pill.anchorMin = pill.anchorMax = new Vector2(0f, 0.5f);
            pill.anchoredPosition = new Vector2(110f, 0f);
            powerName = UiText.CreateOutlined(pill, "label", "", "body", "text.primary", "pop.purple.edge");
            UiComponents.Stretch(powerName.rectTransform);
            powerDesc = UiText.Create(powerRow, "desc", "", "body", "text.ink", TextAlignmentOptions.MidlineLeft);
            UiComponents.Stretch(powerDesc.rectTransform);
            powerDesc.rectTransform.offsetMin = new Vector2(240f, 0f);
            powerDesc.enableWordWrapping = true;
            powerDesc.enableAutoSizing = true;
            powerDesc.fontSizeMin = 26f;
            powerDesc.fontSizeMax = powerDesc.fontSize;

            infoFlavor = UiText.Create(infoCard, "flavor", "", "body", "text.inkSoft");
            infoFlavor.enableWordWrapping = true;
            infoFlavor.rectTransform.anchorMin = infoFlavor.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            infoFlavor.rectTransform.sizeDelta = new Vector2(880f, 90f);
            infoFlavor.rectTransform.anchoredPosition = new Vector2(0f, -420f);

            buddyButton = PopKit.Button(infoCard, "makeBuddy", "", PopTone.Green, MakeBuddy,
                new Vector2(380f, 110f), "heading", "heart");
            var buddyRt = (RectTransform)buddyButton.transform;
            buddyRt.anchorMin = buddyRt.anchorMax = new Vector2(1f, 0f);
            buddyRt.pivot = new Vector2(1f, 0f);
            buddyRt.anchoredPosition = new Vector2(-40f, 36f);
            buddyBonus = UiText.Create(infoCard, "bonus", "", "body", "text.inkSoft", TextAlignmentOptions.MidlineLeft);
            buddyBonus.rectTransform.anchorMin = buddyBonus.rectTransform.anchorMax = new Vector2(0f, 0f);
            buddyBonus.rectTransform.pivot = new Vector2(0f, 0.5f);
            buddyBonus.rectTransform.sizeDelta = new Vector2(500f, 60f);
            buddyBonus.rectTransform.anchoredPosition = new Vector2(44f, 96f);
        }

        // ------------------------------------------------------------- behavior

        private void OnSpeciesTapped(int speciesId)
        {
            if (editMode)
            {
                return;
            }

            CreatureSpecies species = flow.Roster.Species[speciesId];
            flavorCursor = shownSpecies == speciesId ? (flavorCursor + 1) % species.Flavor.Count : 0;
            shownSpecies = speciesId;
            happyUntil[speciesId] = Time.time + 0.7f;
            ShowSpecies();
        }

        /// <summary>Re-reads the info card for the shown species (or the tap hint).</summary>
        private void ShowSpecies()
        {
            bool any = shownSpecies >= 0;
            bool met = any && flow.Meta.Save.RescuesFor(shownSpecies) > 0;
            bool owned = any && flow.PetLevel(shownSpecies) > 0;
            infoProgress.gameObject.SetActive(owned);
            powerRow.gameObject.SetActive(met);
            buddyButton.gameObject.SetActive(owned);
            buddyBonus.gameObject.SetActive(met);
            if (!any)
            {
                infoName.text = "";
                infoCount.text = "";
                // An empty card is a dead card — invite the tap instead.
                infoFlavor.text = flow.Strings.Get("tidepool.tapHint");
                infoPortrait.sprite = CreatureSprites.Silhouette(0);
                ThemedElement.Bind(infoPortrait.gameObject, "pop.navy");
                return;
            }

            CreatureSpecies species = flow.Roster.Species[shownSpecies];
            int rescues = flow.Meta.Save.RescuesFor(shownSpecies);
            infoPortrait.sprite = met ? CreatureSprites.For(shownSpecies) : CreatureSprites.Silhouette(shownSpecies);
            ThemedElement.Bind(infoPortrait.gameObject, met ? "pet.eye" : "pop.navy");
            infoName.text = met ? species.Name : flow.Strings.Get("tidepool.unknown");
            infoFlavor.text = met ? species.Flavor[flavorCursor] : "";
            if (!met)
            {
                infoCount.text = flow.Strings.Get("tidepool.never");
                return;
            }

            powerName.text = ExpansionText.PowerName(flow, shownSpecies);
            powerDesc.text = ExpansionText.PowerDescription(flow, shownSpecies);
            int line = flow.Pals.LineForSpecies(shownSpecies);
            TidelingView tideling = flow.Tideling(line);
            buddyBonus.text = owned
                ? string.Format(flow.Strings.Get("pet.tierLine"), ExpansionText.TierName(flow, tideling.Line.Rarity))
                : flow.Strings.Get("pet.callOnly");
            infoCount.text = string.Format(flow.Strings.Get("tidepool.rescued"), rescues)
                + (owned ? "  ·  " + string.Format(flow.Strings.Get("pet.level"), tideling.Stage) : "");
            if (!owned)
            {
                return;
            }

            infoName.text = $"{tideling.FormName} · {species.Name}";
            if (tideling.NextBond.HasValue)
            {
                infoProgress.Set(tideling.Bond, tideling.NextBond.Value, tideling.CanGrow
                    ? flow.Strings.Get("pet.ready")
                    : string.Format(flow.Strings.Get("pet.next"), tideling.Bond, tideling.NextBond.Value));
            }
            else
            {
                infoProgress.Set(1, 1, flow.Strings.Get("pet.maxed"));
            }

            // One button, the most useful action: grow up when ready, else lead the Pod.
            bool isLeader = tideling.PodSlot == 0;
            string key = tideling.CanGrow ? "pet.growUp" : isLeader ? "pet.isBuddy" : "pet.makeBuddy";
            PopTone tone = tideling.CanGrow ? PopTone.Coral : isLeader ? PopTone.Yellow : PopTone.Green;
            buddyButton.GetComponent<PopSurface>().SetTone(tone);
            TextMeshProUGUI label = PopKit.FaceOf(buddyButton).Find("label").GetComponent<TextMeshProUGUI>();
            label.text = flow.Strings.Get(key);
            UiText.Outline(label, PopKit.Edge(tone));
        }

        private void MakeBuddy()
        {
            if (shownSpecies < 0)
            {
                return;
            }

            int line = flow.Pals.LineForSpecies(shownSpecies);
            if (flow.Tideling(line).CanGrow)
            {
                if (flow.TryGrow(line))
                {
                    happyUntil[shownSpecies] = Time.time + 1.4f;
                    UiJuice.Play("streak");
                    Refresh();
                }

                return;
            }

            if (flow.LeaderSpecies != shownSpecies && flow.SetBuddy(shownSpecies))
            {
                happyUntil[shownSpecies] = Time.time + 1f;
                UiJuice.Play("star");
                Refresh();
            }
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
            foreach ((int id, CreatureChip _, TextMeshProUGUI _, RectTransform _, float _) in chips)
            {
                happyUntil[id] = Time.time + 1f;
            }

            Refresh();
            coins.SetInstant(before);
            coins.AnimateTo(flow.Meta.Coins);
        }

        private void RefreshPets()
        {
            giftButton.gameObject.SetActive(flow.PetGiftCoins > 0 && !editMode);
            giftReady.SetActive(flow.PetGiftAvailable);
            for (int i = 0; i < chips.Count && i < petPills.Count; i++)
            {
                int id = chips[i].id;
                bool owned = flow.PetLevel(id) > 0;
                (GameObject pill, TextMeshProUGUI label, GameObject heart) = petPills[i];
                pill.SetActive(owned);
                heart.SetActive(owned && flow.LeaderSpecies == id);
                label.text = string.Format(flow.Strings.Get("pet.level"), flow.PetLevel(id));
            }

            if (!editMode)
            {
                ShowSpecies();
            }
        }

        /// <summary>Idle blinks (one creature at a time) and happy faces after taps.</summary>
        private void UpdatePets()
        {
            if (Time.time > nextBlink)
            {
                blinking = chips.Count > 0 ? chips[Random.Range(0, chips.Count)].id : -1;
                blinkUntil = Time.time + 0.15f;
                nextBlink = Time.time + Random.Range(0.8f, 2.2f);
            }

            foreach ((int id, CreatureChip chip, TextMeshProUGUI _, RectTransform _, float _) in chips)
            {
                bool happy = happyUntil.TryGetValue(id, out float until) && Time.time < until;
                chip.SetEyesClosed(happy || (id == blinking && Time.time < blinkUntil));
            }

            if (shownSpecies >= 0 && flow.Meta.Save.RescuesFor(shownSpecies) > 0)
            {
                bool happy = happyUntil.TryGetValue(shownSpecies, out float until) && Time.time < until;
                infoPortrait.sprite = happy ? CreatureSprites.Blink(shownSpecies) : CreatureSprites.For(shownSpecies);
            }
        }
    }
}
