using Riptide.Core;
using Riptide.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// GDD §15 in-run pets: the equipped buddy perched on the tray card's corner
    /// (blinks idly, hops with a happy face on every rescue, wiggles on clears) and
    /// the creature-power popups ("Crab Pinch!") rising from the rescued
    /// creature's cell on the queue's "power" beat. Reads flow state only.
    /// </summary>
    public sealed class HudPets : MonoBehaviour
    {
        private const int PopupPool = 4;
        private const float PopupLife = 1.3f;
        private const float CheerLife = 0.8f;

        private GameFlow flow = null!;
        private AnimationDriver? driver;
        private RectTransform buddyRoot = null!;
        private RectTransform buddyBody = null!;
        private Image buddyImage = null!;
        private TextMeshProUGUI buddyLevel = null!;
        private int buddy = -1;
        private float nextBlink;
        private float blinkUntil;
        private float cheerT = -1f;
        private float wiggleT = -1f;
        private readonly Popup[] popups = new Popup[PopupPool];
        private int nextPopup;

        private sealed class Popup
        {
            public WorldAnchor Anchor = null!;
            public RectTransform Body = null!;
            public TextMeshProUGUI Label = null!;
            public float T = -1f;
        }

        /// <summary>The equipped buddy species shown in-run (−1 when none). Test surface.</summary>
        public int Buddy => buddyRoot.gameObject.activeSelf ? buddy : -1;

        public static HudPets Create(HudOverlay hud, GameFlow flow, AnimationDriver? driver)
        {
            RectTransform root = UiComponents.Rect((RectTransform)hud.transform, "HudPets", Vector2.zero);
            UiComponents.Stretch(root);
            var pets = root.gameObject.AddComponent<HudPets>();
            pets.flow = flow;
            pets.driver = driver;
            pets.BuildBuddy(root);
            pets.BuildPopups(root);

            if (flow.Store != null)
            {
                flow.Store.MoveApplied += pets.OnMove;
            }

            flow.RunStarted += pets.RefreshBuddy;
            if (driver != null)
            {
                driver.BeatStarted += pets.OnBeat;
            }

            pets.RefreshBuddy();
            return pets;
        }

        private void OnDestroy()
        {
            if (flow?.Store != null)
            {
                flow.Store.MoveApplied -= OnMove;
            }

            if (flow != null)
            {
                flow.RunStarted -= RefreshBuddy;
            }

            if (driver != null)
            {
                driver.BeatStarted -= OnBeat;
            }
        }

        private void BuildBuddy(RectTransform root)
        {
            // Perched on the tray card's top-left corner, mirroring the coin pill.
            buddyRoot = UiComponents.Rect(root, "buddy", new Vector2(132f, 132f));
            WorldAnchor.Pin(buddyRoot, BoardLayout.TrayCenter
                + new Vector3(-3.66f, BoardLayout.TrayCardHeight * 0.5f + 0.4f, 0f));
            buddyBody = UiComponents.Rect(buddyRoot, "body", new Vector2(132f, 132f));
            UiComponents.Place(buddyBody, new Vector2(0.5f, 0.5f), new Vector2(132f, 132f));
            PopKit.Panel(buddyBody, "disc", new Vector2(132f, 132f), PopTone.White, 66f, 7f);
            buddyImage = PopKit.Img(buddyBody, "pet", CreatureSprites.For(0), "pet.eye");
            UiComponents.Place(buddyImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(120f, 120f));
            buddyImage.rectTransform.anchoredPosition = new Vector2(0f, 6f);

            RectTransform tag = PopKit.Panel(buddyBody, "level", new Vector2(96f, 44f), PopTone.Coral, 22f, 5f);
            tag.anchorMin = tag.anchorMax = new Vector2(0.5f, 0f);
            tag.anchoredPosition = new Vector2(0f, 2f);
            buddyLevel = UiText.CreateOutlined(tag, "label", "", "micro", "text.primary", "pop.coral.edge");
            UiComponents.Stretch(buddyLevel.rectTransform);
        }

        private void BuildPopups(RectTransform root)
        {
            for (int i = 0; i < PopupPool; i++)
            {
                RectTransform holder = UiComponents.Rect(root, $"powerPopup{i}", new Vector2(640f, 90f));
                WorldAnchor anchor = WorldAnchor.Pin(holder, Vector3.zero);
                RectTransform body = UiComponents.Rect(holder, "body", new Vector2(640f, 90f));
                UiComponents.Place(body, new Vector2(0.5f, 0.5f), new Vector2(640f, 90f));
                TextMeshProUGUI label = UiText.CreateOutlined(body, "label", "", "heading", "pop.yellow.top",
                    "pop.purple.edge", 0.3f);
                UiComponents.Stretch(label.rectTransform);
                holder.gameObject.SetActive(false);
                popups[i] = new Popup { Anchor = anchor, Body = body, Label = label };
            }
        }

        private void RefreshBuddy()
        {
            buddy = flow.LeaderSpecies;
            bool shown = buddy >= 0 && buddy < flow.Roster.Count && flow.PetLevel(buddy) > 0;
            buddyRoot.gameObject.SetActive(shown);
            if (!shown)
            {
                return;
            }

            buddyImage.sprite = CreatureSprites.For(buddy);
            buddyLevel.text = string.Format(flow.Strings.Get("pet.level"), flow.PetLevel(buddy));
            nextBlink = Time.time + 2f;
        }

        private void OnMove(Move move, MoveResult result)
        {
            // Animated runs react on the queue's beats; instant boots (tests) right away.
            if (driver != null && !driver.InstantMode)
            {
                return;
            }

            if (result.Events.RescuedCreatures.Count > 0)
            {
                Cheer();
            }

            ShowPowers(result);
        }

        private void OnBeat(string beat, MoveResult result)
        {
            switch (beat)
            {
                case "rescue":
                    Cheer();
                    break;
                case "clear":
                    wiggleT = 0f;
                    break;
                case "power":
                    ShowPowers(result);
                    break;
            }
        }

        /// <summary>A happy hop — public as test surface.</summary>
        public void Cheer() => cheerT = 0f;

        private void ShowPowers(MoveResult result)
        {
            for (int k = 0; k < result.Events.Powers.Count; k++)
            {
                PowerEvent power = result.Events.Powers[k];
                Popup popup = popups[nextPopup++ % PopupPool];
                popup.Label.text = ExpansionText.PowerPopup(flow, power.CreatureId);
                Vector3 world = BoardLayout.CellToWorld(power.Pos.Col, power.Pos.Row);
                world.x = Mathf.Clamp(world.x, -2f, 2f);
                popup.Anchor.gameObject.SetActive(true);
                popup.Anchor.MoveTo(world + new Vector3(0f, 0.35f, 0f));
                popup.T = -0.22f * k; // several powers in one move pop in turn
                popup.Body.localScale = Vector3.zero;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            UpdatePopups(dt);
            if (!buddyRoot.gameObject.activeSelf)
            {
                return;
            }

            bool reduced = ThemeRuntime.ReducedMotion;
            bool happy = false;
            Vector2 offset = Vector2.zero;
            float angle = 0f;
            if (cheerT >= 0f)
            {
                cheerT += dt;
                float u = Mathf.Clamp01(cheerT / CheerLife);
                happy = u < 1f;
                if (!reduced)
                {
                    offset.y = 30f * Mathf.Abs(Mathf.Sin(u * Mathf.PI * 2f)) * (1f - u);
                }

                if (u >= 1f)
                {
                    cheerT = -1f;
                }
            }
            else if (wiggleT >= 0f)
            {
                wiggleT += dt;
                float u = Mathf.Clamp01(wiggleT / 0.4f);
                angle = reduced ? 0f : 9f * Mathf.Sin(u * Mathf.PI * 3f) * (1f - u);
                if (u >= 1f)
                {
                    wiggleT = -1f;
                }
            }

            // Idle blink every few seconds — the cheapest "alive" signal there is.
            if (Time.time > nextBlink)
            {
                blinkUntil = Time.time + 0.14f;
                nextBlink = Time.time + Random.Range(2.4f, 5f);
            }

            buddyBody.anchoredPosition = offset;
            buddyBody.localRotation = Quaternion.Euler(0f, 0f, angle);
            buddyImage.sprite = happy || Time.time < blinkUntil
                ? CreatureSprites.Blink(buddy)
                : CreatureSprites.For(buddy);
        }

        private void UpdatePopups(float dt)
        {
            foreach (Popup popup in popups)
            {
                if (!popup.Anchor.gameObject.activeSelf)
                {
                    continue;
                }

                popup.T += dt;
                if (popup.T < 0f)
                {
                    continue;
                }

                float u = Mathf.Clamp01(popup.T / PopupLife);
                float pop = Mathf.Clamp01(popup.T / 0.18f);
                float overshoot = 1f + 0.25f * Mathf.Sin(pop * Mathf.PI);
                popup.Body.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, pop) * (pop < 1f ? overshoot : 1f);
                popup.Body.anchoredPosition = new Vector2(0f, ThemeRuntime.ReducedMotion ? 0f : 90f * u);
                popup.Label.alpha = 1f - Mathf.Clamp01((u - 0.7f) / 0.3f);
                if (u >= 1f)
                {
                    popup.Anchor.gameObject.SetActive(false);
                }
            }
        }
    }
}
