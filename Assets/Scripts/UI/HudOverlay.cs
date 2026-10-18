using Riptide.Core;
using Riptide.Game;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// GDD 9 in-game HUD, Tidal Pop edition (UI spec §13): white goal chips
    /// top-left, the sticker-letter score center (combo pill + endless best chip
    /// beside it), a yellow pause button top-right; chunky colored boosters with
    /// price tags in the band under the tray; praise / milestone / armed-booster
    /// beats over the board. Renders from store state on every applied move;
    /// never re-derives rules.
    /// </summary>
    public sealed class HudOverlay : MonoBehaviour
    {
        private GameFlow flow = null!;
        private readonly GoalChip[] goalChips = new GoalChip[4];
        private TextMeshProUGUI score = null!;
        private TextMeshProUGUI coins = null!;
        private TextMeshProUGUI popHint = null!;
        private TextMeshProUGUI milestoneLabel = null!;
        private Booster drain = null!;
        private Booster pop = null!;
        private Booster reroll = null!;
        private Booster swap = null!;
        private Button freeDrain = null!;
        private Button freeReroll = null!;
        private bool popArmed;
        private bool swapArmed;
        private bool pointerWasDown;
        private float milestoneUntil;

        // Genre pass (spec §12.4): praise flash, combo chip, endless best chip.
        private TextMeshProUGUI praise = null!;
        private RectTransform comboChip = null!;
        private TextMeshProUGUI comboLabel = null!;
        private RectTransform bestChip = null!;
        private TextMeshProUGUI bestLabel = null!;
        private float praiseUntil;
        private int lastComboChain;
        private bool bestOvertaken;

        // Booster rail world geometry (gate feedback: the rail must never cover
        // the board) — the reserved band BELOW the tray card; CameraFit holds the
        // theme's boosterRailBandRefPx open for it on every device.
        private static float RailRowY => BoardLayout.ContentBottomY - 1.32f;
        private const float DrainX = -3.45f;
        private const float PopX = -1.15f;
        private const float RerollX = 1.15f;
        private const float SwapX = 3.45f;
        private static readonly Vector2 BoosterSize = new Vector2(196f, 168f);

        private sealed class GoalChip
        {
            public RectTransform Root = null!;
            public Image Icon = null!;
            public TextMeshProUGUI Label = null!;
        }

        private sealed class Booster
        {
            public Button Button = null!;
            public PopSurface Surface = null!;
            public PopTone Tone;
            public TextMeshProUGUI Price = null!;
        }

        public static HudOverlay Create(RectTransform canvasRoot, GameFlow flow, System.Action? onPause = null)
        {
            RectTransform root = UiKit.Container(canvasRoot, "HudOverlay");
            UiKit.Stretch(root);
            var hud = root.gameObject.AddComponent<HudOverlay>();
            hud.flow = flow;

            // Universal fit: the top bar lives inside the safe area; world-tracking
            // pieces (boosters, coins, praise, milestone) pin to world positions.
            RectTransform safe = UiKit.Container(root, "safe");
            UiKit.Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();

            hud.BuildTopBar(safe, onPause);
            hud.BuildRail(root);
            hud.BuildBeats(root);

            if (flow.Store != null)
            {
                flow.Store.MoveApplied += hud.OnMove;
                flow.Store.GameReset += hud.OnReset;
            }

            flow.MilestoneReached += hud.OnMilestone;
            flow.RunStarted += hud.RefreshFromState;
            hud.RefreshFromState();
            return hud;
        }

        private void BuildTopBar(RectTransform safe, System.Action? onPause)
        {
            // Spec §4.3 top bar: goal chips LEFT, score CENTER, pause RIGHT.
            RectTransform goals = UiComponents.Rect(safe, "goals", new Vector2(330f, 360f));
            goals.anchorMin = goals.anchorMax = new Vector2(0f, 1f);
            goals.pivot = new Vector2(0f, 1f);
            goals.anchoredPosition = new Vector2(22f, -22f);
            for (int i = 0; i < goalChips.Length; i++)
            {
                RectTransform chip = PopKit.Panel(goals, $"chip{i}", new Vector2(300f, 84f), PopTone.White, 42f, 7f);
                chip.anchorMin = chip.anchorMax = new Vector2(0f, 1f);
                chip.pivot = new Vector2(0f, 1f);
                chip.anchoredPosition = new Vector2(0f, -i * 92f);
                Image icon = PopKit.Icon(chip, "icon", "fish", "pop.coral", 56f, "panel.edge", 6);
                var iconRt = (RectTransform)icon.transform.parent;
                iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 0.5f);
                iconRt.anchoredPosition = new Vector2(48f, 0f);
                TextMeshProUGUI label = UiText.Create(chip, "label", "", "heading", "text.ink",
                    TextAlignmentOptions.MidlineLeft);
                UiComponents.Stretch(label.rectTransform);
                label.rectTransform.offsetMin = new Vector2(88f, 0f);
                goalChips[i] = new GoalChip { Root = chip, Icon = icon, Label = label };
            }

            // The score is the headline number — big sticker letters, tabular.
            score = UiText.CreateOutlined(safe, "score", "0", "score", "text.primary", "panel.edge", 0.3f);
            score.fontSize = 96f;
            score.rectTransform.anchorMin = score.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            score.rectTransform.sizeDelta = new Vector2(440f, 110f);
            score.rectTransform.anchoredPosition = new Vector2(0f, -78f);

            // Endless: the personal best is the run's target (the genre's crown).
            bestChip = PopKit.Panel(safe, "best", new Vector2(250f, 64f), PopTone.Navy, 32f, 6f);
            bestChip.anchorMin = bestChip.anchorMax = new Vector2(0.5f, 1f);
            bestChip.anchoredPosition = new Vector2(0f, -160f);
            Image crown = PopKit.Icon(bestChip, "crown", "crown", "pop.yellow", 42f, "pop.yellow.edge", 6);
            ((RectTransform)crown.transform.parent).anchoredPosition = new Vector2(-82f, 2f);
            bestLabel = UiText.CreateOutlined(bestChip, "label", "", "caption", "text.primary", "panel.edge");
            UiComponents.Stretch(bestLabel.rectTransform);
            bestLabel.rectTransform.offsetMin = new Vector2(52f, 0f);
            bestChip.gameObject.SetActive(false);

            // Visible combo multiplier beside the score (inside the top band —
            // gate feedback: nothing static may sit over the board).
            comboChip = PopKit.Panel(safe, "combo", new Vector2(220f, 70f), PopTone.Coral, 26f, 6f);
            comboChip.anchorMin = comboChip.anchorMax = new Vector2(0.5f, 1f);
            comboChip.anchoredPosition = new Vector2(286f, -80f);
            comboChip.localRotation = Quaternion.Euler(0f, 0f, -6f);
            comboLabel = UiText.CreateOutlined(comboChip, "label", "", "body", "text.primary", "pop.coral.edge");
            UiComponents.Stretch(comboLabel.rectTransform);
            comboChip.gameObject.SetActive(false);

            // Spec §4.3: the top-right slot is the pause control; without a pause
            // sheet wired (CreateGame test rigs) it falls back to Home.
            Button menu = PopKit.IconButton(safe, "menu", "pause", PopTone.Yellow, () =>
            {
                if (onPause != null)
                {
                    onPause();
                }
                else
                {
                    flow.GoTo(FlowScreen.Home);
                }
            }, new Vector2(118f, 124f));
            var menuRt = (RectTransform)menu.transform;
            menuRt.anchorMin = menuRt.anchorMax = new Vector2(1f, 1f);
            menuRt.pivot = new Vector2(1f, 1f);
            menuRt.anchoredPosition = new Vector2(-24f, -22f);
        }

        private void BuildRail(RectTransform root)
        {
            drain = BuildBooster(root, "drain", "drop", PopTone.Blue, "booster.drainName", DrainX,
                () => UseSimpleBooster(BoosterKind.DrainPump));
            pop = BuildBooster(root, "pop", "bomb", PopTone.Coral, "booster.popName", PopX, TogglePopMode);
            reroll = BuildBooster(root, "reroll", "reroll", PopTone.Purple, "booster.rerollName", RerollX,
                () => UseSimpleBooster(BoosterKind.NewTide));
            swap = BuildBooster(root, "swap", "swap", PopTone.Green, "booster.swapName", SwapX, ToggleSwapMode);

            // GDD 5.3: one free Drain Pump and one free New Tide per game via
            // rewarded ad — purple stickers on their boosters' corners.
            freeDrain = FreeSticker(drain, "freeDrain", BoosterKind.DrainPump);
            freeReroll = FreeSticker(reroll, "freeReroll", BoosterKind.NewTide);

            // Coins live by the rail — that's where they're spent: a navy pill
            // stuck on the tray card's top-right corner.
            RectTransform pill = PopKit.Panel(root, "coins", new Vector2(230f, 80f), PopTone.Navy, 40f, 6f);
            WorldAnchor.Pin(pill, new Vector3(3.55f, BoardLayout.TrayCenter.y + BoardLayout.TrayCardHeight * 0.5f + 0.08f, 0f));
            Image coin = PopKit.Icon(pill, "coin", "coin", "coin", 50f, "pop.yellow.edge", 6);
            ((RectTransform)coin.transform.parent).anchoredPosition = new Vector2(-76f, 0f);
            coins = UiText.CreateOutlined(pill, "value", "", "body", "text.primary", "panel.edge");
            UiComponents.Stretch(coins.rectTransform);
            coins.rectTransform.offsetMin = new Vector2(56f, 0f);
        }

        private Booster BuildBooster(RectTransform root, string name, string icon, PopTone tone, string nameKey,
            float worldX, System.Action onClick)
        {
            Button button = PopKit.Tile(root, name, tone, onClick, BoosterSize, PopKit.Radius("r.m"));
            var rt = (RectTransform)button.transform;
            WorldAnchor.Pin(rt, new Vector3(worldX, RailRowY, 0f));
            RectTransform face = PopKit.FaceOf(button);
            Image glyph = PopKit.Icon(face, "icon", icon, "text.primary", 70f, PopKit.Edge(tone), 8);
            ((RectTransform)glyph.transform.parent).anchoredPosition = new Vector2(0f, 18f);
            TextMeshProUGUI label = UiText.CreateOutlined(face, "name", flow.Strings.Get(nameKey), "micro",
                "text.primary", PopKit.Edge(tone), 0.22f);
            UiComponents.Place(label.rectTransform, new Vector2(0.5f, 0.17f), new Vector2(180f, 32f));

            RectTransform tag = PopKit.Panel(rt, "tag", new Vector2(132f, 58f), PopTone.Navy, 29f, 5f);
            tag.anchorMin = tag.anchorMax = new Vector2(0.5f, 0f);
            tag.anchoredPosition = new Vector2(0f, -14f);
            Image coin = PopKit.Icon(tag, "coin", "coin", "coin", 34f, "pop.yellow.edge", 5);
            ((RectTransform)coin.transform.parent).anchoredPosition = new Vector2(-38f, 0f);
            TextMeshProUGUI price = UiText.CreateOutlined(tag, "price", "", "caption", "text.primary", "panel.edge");
            UiComponents.Stretch(price.rectTransform);
            price.rectTransform.offsetMin = new Vector2(30f, 0f);

            return new Booster
            {
                Button = button,
                Surface = button.GetComponent<PopSurface>(),
                Tone = tone,
                Price = price,
            };
        }

        private Button FreeSticker(Booster host, string name, BoosterKind kind)
        {
            Button sticker = PopKit.Button((RectTransform)host.Button.transform, name, flow.Strings.Get("booster.free"),
                PopTone.Purple, () => { flow.TryFreeBoosterViaAd(kind); RefreshFromState(); },
                new Vector2(140f, 74f), "micro", "play");
            var rt = (RectTransform)sticker.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-20f, 4f);
            rt.localRotation = Quaternion.Euler(0f, 0f, -8f);
            return sticker;
        }

        private void BuildBeats(RectTransform root)
        {
            // ROADMAP M4: endless milestone pop — over the board's lower half.
            milestoneLabel = UiText.CreateOutlined(root, "milestone", "", "heading", "pop.yellow.top", "panel.edge", 0.28f);
            milestoneLabel.rectTransform.sizeDelta = new Vector2(900f, 70f);
            WorldAnchor.Pin(milestoneLabel.rectTransform, new Vector3(0f, -3.5f, 0f));
            milestoneLabel.gameObject.SetActive(false);

            // Armed-booster instruction floats over the board's top rows.
            popHint = UiText.CreateOutlined(root, "popHint", flow.Strings.Get("booster.popHint"), "body",
                "text.primary", "pop.coral.edge", 0.28f);
            popHint.rectTransform.sizeDelta = new Vector2(900f, 60f);
            WorldAnchor.Pin(popHint.rectTransform, new Vector3(0f, 5.4f, 0f));
            popHint.gameObject.SetActive(false);

            // Genre pass (spec §12.4, research: Block Blast's praise loop) — big
            // sticker letters over the board's upper half on multi-row clears.
            praise = UiText.CreateOutlined(root, "praise", "", "display", "pop.yellow.top", "pop.coral.edge", 0.34f);
            praise.fontSize = 124f;
            praise.rectTransform.sizeDelta = new Vector2(1000f, 160f);
            WorldAnchor.Pin(praise.rectTransform, new Vector3(0f, 2.4f, 0f));
            praise.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (flow?.Store != null)
            {
                flow.Store.MoveApplied -= OnMove;
                flow.Store.GameReset -= OnReset;
            }

            if (flow != null)
            {
                flow.RunStarted -= RefreshFromState;
                flow.MilestoneReached -= OnMilestone;
            }
        }

        private void OnMilestone(int tides)
        {
            milestoneLabel.text = string.Format(flow.Strings.Get("hud.milestone"),
                tides, flow.Economy.Coins.EndlessMilestoneCoins);
            milestoneLabel.gameObject.SetActive(true);
            milestoneUntil = Time.realtimeSinceStartup + 2.4f;
            UiJuice.Play("streak");
        }

        private void ToggleSwapMode()
        {
            if (!flow.CanUseBooster(BoosterKind.PieceSwap))
            {
                return;
            }

            swapArmed = !swapArmed;
            popArmed = false;
            popHint.text = flow.Strings.Get(swapArmed ? "booster.swapHint" : "booster.popHint");
            popHint.gameObject.SetActive(swapArmed);
            RefreshArmed();
        }

        private void OnMove(Move move, MoveResult result)
        {
            int rows = result.Events.RowsCleared.Count;
            if (rows >= 2)
            {
                ShowPraise(rows);
            }

            RefreshFromState();
        }

        /// <summary>Spec §12.4 praise beat: pop-in with overshoot, hold, then hide.
        /// Public as test surface (PlayMode asserts copy + activation).</summary>
        public void ShowPraise(int rows)
        {
            praise.text = flow.Strings.Get(rows == 2 ? "praise.double"
                : rows == 3 ? "praise.triple" : "praise.quad");
            praise.gameObject.SetActive(true);
            praiseUntil = Time.realtimeSinceStartup + 1.1f;
            RectTransform rt = praise.rectTransform;
            rt.localScale = Vector3.one * 0.4f;
            rt.localRotation = Quaternion.Euler(0f, 0f, rows >= 4 ? -6f : 4f);
            Tween.Run(this, "t.base", "easeOutBack",
                u => rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.4f, 1f, u),
                () => rt.localScale = Vector3.one);
        }

        private void OnReset(GameState state)
        {
            popArmed = false;
            swapArmed = false;
            popHint.gameObject.SetActive(false);
            praise.gameObject.SetActive(false);
            lastComboChain = 0;
            bestOvertaken = false;
            RefreshArmed();
            RefreshFromState();
        }

        private void UseSimpleBooster(BoosterKind kind)
        {
            popArmed = false;
            swapArmed = false;
            popHint.gameObject.SetActive(false);
            flow.TryUseBooster(kind);
            RefreshArmed();
            RefreshFromState();
        }

        private void TogglePopMode()
        {
            if (!flow.CanUseBooster(BoosterKind.BubblePop))
            {
                return;
            }

            popArmed = !popArmed;
            swapArmed = false;
            popHint.text = flow.Strings.Get("booster.popHint");
            popHint.gameObject.SetActive(popArmed);
            RefreshArmed();
        }

        /// <summary>An armed booster glows yellow until it fires or is cancelled.</summary>
        private void RefreshArmed()
        {
            pop.Surface.SetTone(popArmed ? PopTone.Yellow : pop.Tone);
            swap.Surface.SetTone(swapArmed ? PopTone.Yellow : swap.Tone);
        }

        private void Update()
        {
            if (milestoneLabel.gameObject.activeSelf && Time.realtimeSinceStartup > milestoneUntil)
            {
                milestoneLabel.gameObject.SetActive(false);
            }

            if (praise.gameObject.activeSelf && Time.realtimeSinceStartup > praiseUntil)
            {
                praise.gameObject.SetActive(false);
            }

            if (!popArmed && !swapArmed)
            {
                return;
            }

            Pointer? pointer = Pointer.current;
            if (pointer == null)
            {
                return;
            }

            bool down = pointer.press.isPressed;
            if (down && !pointerWasDown)
            {
                Camera cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
                Vector2 screen = pointer.position.ReadValue();
                Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
                if (popArmed && BoardLayout.TrySnap(world, 0.5f, out int col, out int row))
                {
                    if (flow.TryUseBooster(BoosterKind.BubblePop, new GridPos(col, row)))
                    {
                        popArmed = false;
                        popHint.gameObject.SetActive(false);
                        RefreshArmed();
                        RefreshFromState();
                    }
                }
                else if (swapArmed)
                {
                    // ROADMAP M3: armed-tap a tray slot to swap that piece.
                    for (int slot = 0; slot < BoardSpec.TraySize; slot++)
                    {
                        if (Vector2.Distance(world, BoardLayout.TraySlotCenter(slot)) <= 1.1f)
                        {
                            if (flow.TryUseBooster(BoosterKind.PieceSwap, new GridPos(slot, 0)))
                            {
                                swapArmed = false;
                                popHint.gameObject.SetActive(false);
                                RefreshArmed();
                                RefreshFromState();
                            }

                            break;
                        }
                    }
                }
            }

            pointerWasDown = down;
        }

        private void RefreshFromState()
        {
            if (flow.Store == null)
            {
                return;
            }

            GameState state = flow.Store.State;
            score.text = ShareCard.GroupThousands(state.Score);
            coins.text = ShareCard.GroupThousands(flow.Meta.Coins);

            RefreshComboChip(state);
            RefreshBestChip(state);

            bool boosters = state.Config.BoostersAllowed && !state.Status.IsTerminal();
            RefreshBooster(drain, BoosterKind.DrainPump, boosters);
            RefreshBooster(pop, BoosterKind.BubblePop, boosters);
            RefreshBooster(reroll, BoosterKind.NewTide, boosters);
            RefreshBooster(swap, BoosterKind.PieceSwap, boosters);
            coins.transform.parent.gameObject.SetActive(boosters);
            freeDrain.gameObject.SetActive(boosters && flow.FreeBoosterAvailable(BoosterKind.DrainPump));
            freeReroll.gameObject.SetActive(boosters && flow.FreeBoosterAvailable(BoosterKind.NewTide));

            int chip = 0;
            GoalSet goalSet = state.Config.Goals;
            if (goalSet.RescueAllTarget.HasValue)
            {
                SetChip(chip++, "fish", "pop.coral", string.Format(flow.Strings.Get("hud.chip"),
                    state.Goals.Rescued, goalSet.RescueAllTarget.Value));
            }

            if (goalSet.ClearRowsTarget.HasValue)
            {
                SetChip(chip++, "rows", "pop.blue", string.Format(flow.Strings.Get("hud.chip"),
                    state.Goals.RowsCleared, goalSet.ClearRowsTarget.Value));
            }

            if (goalSet.SurviveTidesTarget.HasValue)
            {
                SetChip(chip++, "waves", "accent.deep", string.Format(flow.Strings.Get("hud.chip"),
                    state.Goals.TidesSurvived, goalSet.SurviveTidesTarget.Value));
            }

            if (goalSet.ScoreTarget.HasValue)
            {
                SetChip(chip++, "star", "pop.yellow", ShareCard.GroupThousands(goalSet.ScoreTarget.Value));
            }

            for (int i = chip; i < goalChips.Length; i++)
            {
                goalChips[i].Root.gameObject.SetActive(false);
            }
        }

        private void RefreshBooster(Booster booster, BoosterKind kind, bool shown)
        {
            booster.Button.gameObject.SetActive(shown);
            if (shown)
            {
                // §16.4: a Pod free use (Manta / Otter / Squid) reads FREE instead of a price.
                booster.Price.text = flow.PodFreeBoostersLeft(kind) > 0
                    ? flow.Strings.Get("booster.free")
                    : flow.BoosterCost(kind).ToString();
                booster.Button.interactable = flow.CanUseBooster(kind);
            }
        }

        private void SetChip(int index, string iconId, string iconToken, string text)
        {
            GoalChip chip = goalChips[index];
            chip.Root.gameObject.SetActive(true);
            chip.Icon.sprite = PopSprites.Icon(iconId);
            chip.Icon.color = ThemeRuntime.Color(iconToken);
            chip.Icon.transform.parent.Find("outline").GetComponent<Image>().sprite = PopSprites.IconOutline(iconId, 6);
            chip.Label.text = text;
        }

        /// <summary>Spec §12.4: the chain multiplier is visible, not just felt.</summary>
        private void RefreshComboChip(GameState state)
        {
            int chain = state.ComboChain;
            bool show = chain >= 2 && !state.Status.IsTerminal();
            comboChip.gameObject.SetActive(show);
            if (show)
            {
                ScoringConfig sc = state.Config.Scoring;
                int halves = System.Math.Min(
                    sc.ComboStartHalves + (chain - 1) * sc.ComboStepHalves, sc.ComboCapHalves);
                comboLabel.text = string.Format(flow.Strings.Get("hud.combo"), FormatHalves(halves));
                if (chain > lastComboChain)
                {
                    Pulse(comboChip);
                }
            }

            lastComboChain = chain;
        }

        /// <summary>Endless personal best in-run; flips to coin gold the moment it's beaten.</summary>
        private void RefreshBestChip(GameState state)
        {
            bool endless = flow.Mode == GameMode.Endless && flow.Meta.EndlessBest > 0;
            bestChip.gameObject.SetActive(endless);
            if (!endless)
            {
                return;
            }

            long shown = System.Math.Max(flow.Meta.EndlessBest, state.Score);
            bestLabel.text = string.Format(flow.Strings.Get("hud.best"), ShareCard.GroupThousands(shown));
            if (state.Score > flow.Meta.EndlessBest && !bestOvertaken)
            {
                bestOvertaken = true;
                bestLabel.color = ThemeRuntime.Color("coin");
                Pulse(bestChip);
            }
            else if (!bestOvertaken)
            {
                bestLabel.color = ThemeRuntime.Color("text.primary");
            }
        }

        /// <summary>"3 halves" → "1.5" (invariant culture). Public as test surface.</summary>
        public static string FormatHalves(int halves) =>
            halves % 2 == 0
                ? (halves / 2).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : (halves / 2f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

        private void Pulse(RectTransform target)
        {
            Tween.Run(this, "t.fast", "linear",
                u => target.localScale = Vector3.one * (1f + 0.18f * Mathf.Sin(u * Mathf.PI)),
                () => target.localScale = Vector3.one);
        }
    }
}
