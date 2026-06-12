# RIPTIDE — UI SPECIFICATION v1.0
**Companion to `RIPTIDE_GDD.md` (§7, §9) and `RIPTIDE_MASTER_PROMPT.md` (Phases 4–5, 8).**
**This document is the UI contract: Fable implements exactly this; anything visual ends in a 🎯 gate for Nick. Aesthetic judgment calls not covered here go in `DECISIONS.md`.**

---

## 0. Design intent in one paragraph

Riptide's UI is **dark, bioluminescent, and quiet** — a deep-ocean instrument panel, not a casual-game candy shop. The board and the water are the show; chrome recedes to near-black glass. Light is information: anything glowing is interactive or dangerous. Motion is fluid and physical (everything eases like it's moving through water — slightly overdamped, never bouncy-cartoonish). The single most important element in the game is the **tide meter**; the single most important moment is the **drain**. Both get over-invested polish; everything else stays restrained.

**Anti-goals:** no gradients-on-everything mobile-game gloss, no yellow burst-star particles, no 9-slice wooden buttons, no red notification dots in v1, no screen clutter. If a competitor screenshot of Block Blast looks busier than ours, we're winning.

---

## 1. Design tokens (single source of truth)

All values below live in `Assets/Content/ui_theme.json`, loaded into an immutable `UiTheme` object at boot. **No color, duration, or radius literal appears in a prefab field or C# file** — prefabs reference theme keys via a `ThemedElement` component (maps key → Graphic color / etc. at load). This extends master-prompt rule 7 to presentation.

### 1.1 Color
```
// Surfaces (dark → light)
bg.abyss        #0A0E14   app background, the house near-black
bg.deep         #0E141D   screen panels, zone map background
bg.surface      #131B27   cards, sheets, modals
bg.raised       #1A2433   buttons (secondary), input rows, list items
stroke.subtle   #243140   1px hairlines, dividers
stroke.bright   #3A4D63   focused/selected outlines

// Brand & interaction
accent.primary  #3EE6E0   cyan — primary buttons, links, active states, tide meter safe
accent.deep     #1B6F8A   pressed states, secondary fills of primary elements
glow.primary    #3EE6E0 @ 35% alpha, blurred — the standard glow recipe

// Water (the antagonist)
water.calm.top  #2FA8C4 @ 55%
water.calm.btm  #15536B @ 70%
water.danger.top #C4452F @ 55%      // crossfades in at water ≥ drownLevel−2
water.danger.btm #6B1815 @ 70%
water.foamLine  #BFE8FF @ 90%, 2px

// Feedback
positive        #2BB59A   success, coins gained, stars
warning         #FFB341   2-star tier, caution copy
danger          #FF6B81   destructive confirm, creature lost, flood line
coin            #FFD27A   coin icon + counters only

// Block palette (gameplay — colorblind-checked, see §8)
block.cyan #3EE6E0 · block.teal #2BB59A · block.coralPink #FF6B81
block.amber #FFB341 · block.violet #8C7BFF · block.ice #BFE8FF
block.dead  #5A5F66   petrified coral cells (desaturated bone, porous sprite)

// Text
text.primary    #EAF2F7
text.secondary  #9FB1C1
text.muted      #5E7081
text.onAccent   #06222B   dark text on cyan buttons (contrast ≥ 7:1)
```

### 1.2 Typography — Rungo, four weights
Reference resolution 1080×2400 (see §2). Sizes in reference px.

| Token | Weight | Size / line | Use |
|---|---|---|---|
| display | Bold | 76 / 84 | results headline, "RIPTIDE" wordmark fallback |
| title | Bold | 48 / 56 | screen titles, level number |
| heading | SemiBold | 36 / 44 | card titles, section heads |
| body | Medium | 30 / 40 | default copy, buttons |
| caption | Medium | 24 / 32 | sublabels, hints, par lines |
| micro | SemiBold | 20 / 26 | HUD counters, badges, ALL-CAPS labels (tracking +8%) |
| score | Bold tabular | 44 / 48 | in-game score (tabular figures ON — no jitter while counting) |

Rules: sentence case everywhere except micro ALL-CAPS labels. Numbers animate with a count-up (180ms, easeOutCubic) only when delta matters (score, coins); static otherwise. Max two weights per screen region.

### 1.3 Spacing, radius, elevation
- Spacing scale: 4 / 8 / 12 / 16 / 24 / 32 / 48 / 64. Screen gutter: 48. Card padding: 32.
- Radius: `r.s` 12 (chips, inputs) · `r.m` 20 (buttons, cards) · `r.l` 32 (sheets, modals) · blocks 8.
- Elevation = glow, not drop shadow (we're underwater; light radiates). Three levels: none / `glow.primary` at 20% / at 35%. Modals additionally dim the scrim `#05080C @ 72%`.

### 1.4 Motion grammar
Global easing: **easeOutQuart** for entrances, **easeInCubic** for exits, **easeInOutSine** for loops. Durations:

| Token | ms | Use |
|---|---|---|
| t.instant | 90 | press feedback, toggles |
| t.fast | 180 | chips, counters, hover-equivalents |
| t.base | 260 | buttons, card reveals, screen elements |
| t.screen | 340 | full screen transitions |
| t.rise | 350 | water rise surge (GDD-locked) |
| t.drain | 450 | water drain recede (GDD-locked, the hero moment) |

Screen transition: outgoing screen fades to 0 + drifts down 24px; incoming fades in + drifts up from −24px, 60ms overlap — everything sinks and floats, on theme. Staggers: list items 40ms apart, max 6 staggered then simultaneous. **Reduced-motion setting** (Settings toggle): all durations ×0.5, staggers off, water animates by simple lerp.

---

## 2. Layout system & technical base

- **Stack: UGUI** (Canvas + TextMeshPro with Rungo SDF assets), not UI Toolkit. Rationale: juice-heavy game UI, world-space coupling with the board, mature tweening path, and consistency with Star Ladder conventions. (⚑ flagged: if Star Ladder actually shipped UI Toolkit, mirror that instead — say so before Phase 4.)
- One `Canvas` per screen, CanvasScaler: Scale With Screen Size, ~~reference 1080×2400, match = 1.0 (height)~~ **[amended 2026-06-12]** reference **1080×2347 (19.5:9 — iPhone 16 Pro Max basis), match = 0 (width)**: ref-px token values hold verbatim on every device; vertical space flexes (taller phones gain it, 16:9 loses it — screens must anchor-stack, no fixed-height compositions). The world-space game camera is fitted by `CameraFit.Solve` (width-driven, safe-area-aware, vertical fallback for tablets); its bands live in the ui_theme.json `layout` block. See §12.
- **SafeArea component** on every screen root (handles notches/punch-holes; test device list in §10). The board never enters unsafe area; HUD pins inside it.
- Tweening: a single in-house `Tween` utility over `AnimationCurve`s defined in `ui_theme.json` motion block (no third-party dependency; deterministic, testable durations). All tweens cancellable and idempotent.
- Layout anchors only — zero hardcoded positions in C#. Each screen is a prefab; screens live under one `ScreenStack` (push/pop with transition per §1.4), driven by the Game-layer store (UI dispatches intents, renders from state — house style, views never own logic).
- Sprite art: flat vector-style SVG-derived sprites in a single atlas per scene group; 9-slice only for sheets/cards.

---

## 3. Component library (build once, Phase 4–5A)

Each component = prefab + `ThemedElement` bindings + a play-mode smoke test that instantiates it and asserts theme application. States: default / pressed / disabled (+ selected where relevant). Pressed = scale 0.97 + brightness −8%, t.instant. Disabled = 40% alpha, no glow. Min touch target **120×120 ref-px (≈48dp)** even when visual is smaller.

1. **ButtonPrimary** — cyan fill, `text.onAccent`, r.m, glow level 2. One per screen max (the action).
2. **ButtonSecondary** — `bg.raised` fill, `stroke.subtle`, text.primary.
3. **ButtonGhost** — text-only + chevron, for tertiary ("Restore Purchases").
4. **ButtonReward** — secondary button variant with leading ▶︎-in-circle ad badge (amber). Always shows what you get: "Free · Watch ad".
5. **IconButton** — 96×96, for settings gear, close ✕, pause.
6. **Card** — bg.surface, r.m, optional header row. Used in results, shop, tidepool.
7. **Sheet** — bottom sheet, r.l top corners, drag-handle bar, scrim; slides up t.screen. Shop and pause live here.
8. **Modal** — centered, for confirms only (quit run, purchase). Max one button pair: Ghost + Primary/Danger.
9. **Toast** — top, auto-dismiss 2.4s, queue depth 2 ("Not enough coins", "No ad available").
10. **CoinCounter** — coin glyph + tabular number; flies +N particles toward it on gain (≤8 particles, 600ms).
11. **StarTriplet** — 3 stars, fill animation staggered 120ms, unearned = stroke-only.
12. **ProgressPips / ProgressBar** — zone map progress, tutorial steps.
13. **StreakFlame** — daily streak count with flame glyph; pulses once on increment.
14. **TideMeterRing** — §5.2, the flagship component.
15. **ShareCard** — §6.4.
16. **CreatureChip** — 88px portrait roundel; states: silhouette (unmet), normal, rescued-sparkle.

---

## 4. Screen map & navigation

```
Boot → Consent/Age (first run) → Home
Home ─ Voyage → ZoneMap → Game(level) → Results ─ next/retry/home
     ─ Endless → Game(endless) → Results
     ─ Daily → DailyIntro → Game(daily) → DailyResults(+Share)
     ─ Tidepool
     ─ Settings (push) · Shop (sheet, from anywhere coins/RemoveAds shown)
In-game: Pause (sheet) → resume / restart / quit-confirm / settings
```
Android back: pops stack; on Home = background app; in Game = open Pause. All screens reachable in ≤2 taps from Home.

### 4.1 Home
- Vertical composition, generous negative space: wordmark top (logo sprite; fallback display-type "RIPTIDE" with cyan tide-line underline that slowly laps, 4s loop), then three stacked mode cards, then bottom row: Tidepool · Coins · Settings.
- **Mode cards** (full-width, 280 ref-px tall): Voyage card shows "Zone {n} · Level {m}" progress pips + ButtonPrimary "Continue"; Endless card shows personal best; Daily card shows today's state — not-played (cyan pulse on card edge, 3s subtle), played (✓ + score), or streak flame.
- Daily card is visually distinct: thin `water.foamLine` top border. The daily is the ritual; it gets the eye.
- Ambient: marine-snow particles (≤24, slow drift), background vertical gradient bg.abyss → bg.deep.

### 4.2 Zone Map (Voyage)
- Vertical scroll, current zone centered on open. 20 level nodes per zone on a gentle S-path (precomputed anchor table, not physics).
- Node states: locked (stroke.subtle ring, muted number) · current (cyan ring + glow pulse 2s) · complete (filled, star count beneath in micro).
- Zone header card pinned top: zone name (strings.json `zones.*`), total stars "{x}/60", zone accent tint applied to background gradient (each zone's accent defined in `levels/zone{n}.json` meta — palette stays within block-palette hues).
- Scroll between zones: next zone dimmed 40% until unlocked, with "Complete Zone {n}" caption.

### 4.3 Game screen — the one that matters
Portrait composition, top to bottom inside safe area:

1. **Top HUD bar (140 ref-px):** left = goal chip(s) (icon + "{rescued}/{n}" or "Clear {x}/{y}" — live-updating, pop t.fast on change); center = score (score type, tabular); right = pause IconButton. In Daily: center shows "Tide {n}/20" instead of score-primary (score moves to a caption beneath).
2. **Board (9×12)** — width = screen minus gutters; cell size derives from width; board frame: 2px stroke.subtle, r.s corners, bg slightly lighter than screen (#0D1320). **Flood line**: dashed danger-color line with small ⚠ glyph at left margin, drawn at drownLevel row — always visible, 35% alpha when water far, 100% + 1.2s pulse when water ≥ drownLevel−2.
3. **Water overlay** (§5.1) covering submerged rows, rendered above blocks at 55–70% alpha so drowned content reads as *under* water.
4. **Tray strip (320 ref-px):** 3 piece slots on bg.surface card; **TideMeterRing** wraps the tray card border (§5.2). Booster rail: 3 compact booster buttons right-aligned above tray (hidden in Daily — enforced by state, not by view logic).
5. **Depth gauge** (left edge, 12px wide): subtle vertical track marking water level numerically every 2 rows; current level notch glows. Redundant with the water itself — readable in peripheral vision.

Drag interaction (GDD §7.3, restated as buildable spec):
- Touch piece → piece scales 1.08 and lifts **90px above finger**; tray slot leaves a ghost.
- Board ghost preview snaps when piece center within **0.6 cell** of a legal anchor; ghost = block sprites at 45% alpha, cyan-tinted when legal, danger-tinted at 30% when the hover anchor is illegal.
- Release on legal anchor: commit (snap 80ms), light haptic. Release elsewhere: piece flies back to tray t.base, no penalty.
- Row about to complete (8/9 filled by current ghost): the missing-cell column edge shimmers once — a "you see it too" tell, max once per 2s.

### 4.4 Results (Voyage win / Voyage lose / Endless over)
- Card stack center: headline (strings: `results.*` — distinct copy for drown vs stuck vs win), StarTriplet (win), stat rows (score / best streak ✦ / rescued / tides survived), coin award row with CoinCounter fly-in.
- Buttons: Primary = Next Level (win) / Retry (lose); Secondary = Retry/Home; ButtonReward "Double Coins" (win, once).
- Lose-by-drown gets a unique 600ms intro: screen tint dips into water.calm gradient top-down then settles — the board drowned, the UI acknowledges it. Lose-by-stuck: board cells flash muted once. **These two deaths must feel different.**
- Continue offer (Endless/Voyage, once per run, GDD §5): appears as a pre-results interstitial card with 5-pip countdown (no timer pressure in Daily — not offered there).

### 4.5 Daily intro & results
- Intro: date headline, "Survive 20 tides. One board. No boosters.", streak flame, ButtonPrimary "Dive". Sets the ritual tone; 1 tap to play.
- Results: tide progress bar (n/20), score, emoji ShareCard preview rendered exactly as it will paste (§6.4), ButtonPrimary "Share", secondary "Home", ButtonReward retry (if failed, once).

### 4.6 Tidepool
- Horizontal-scroll diorama (3 screens wide), parallax 2-layer background, creatures idle-bobbing at fixed slots; locked species = dark silhouette + "???".
- Tap creature → bottom card: portrait, name, lifetime rescues, flavor line (cycles 3).
- Decorations: edit mode toggle (top-right) → owned decorations tray; tap-to-place at predefined slot points (no free placement — slots avoid layout bugs and look composed). Shop entry for unowned.

### 4.7 Settings / Shop / Consent
- Settings: list rows (toggle: sound, music, haptics, reduced motion) + ghost rows (consent re-open, restore, privacy, ToS, version caption). Plain, fast, done.
- Shop sheet: Remove Ads hero card (price, one-line promise) above 3 coin pack cards in a row. Post-purchase: `shop.thanks` toast + interstitials flag off immediately.
- Consent/Age (first run): neutral year picker (wheel), then UMP flow; bg.abyss, no game art that skews child-directed.

---

## 5. The two flagship elements

### 5.1 Water rendering
- Two stacked translucent quads spanning board width: back layer scrolls a sine-displaced surface texture at 12px amplitude / 9s period; front layer offset half-phase, 8px / 6s. Foam line sprite (water.foamLine) rides the front surface.
- Caustic noise texture multiplied into submerged board area at 12% alpha, slow 16s scroll — makes submerged blocks read "underwater" instantly.
- **Rise (t.rise 350ms):** surface surges up one row with a 6px overshoot then settles; foam brightens; medium→heavy haptic; submerged blocks petrify with a desaturation sweep bottom-up 200ms.
- **Drain (t.drain 450ms) — over-deliver:** surface recedes with trailing droplet particles (≤16), brief sparkle on each newly exposed row, deep whoosh-glug SFX, foam flares; multi-row drains stretch to 550ms and add a screen-edge cyan pulse. This is the dopamine moment; it must feel like *relief*.
- Danger state (water ≥ drownLevel−2): water gradient crossfades to danger variant over 800ms, foam pulses, music tension layer in (GDD §7.2), flood line at full alpha.

### 5.2 TideMeterRing — most important UI element (named visual-gate item)
- Circular ring around a wave glyph, anchored at tray card's left end, 132 ref-px.
- Ring fills clockwise = placements toward next rise: fill fraction = tideCounter/tideInterval, animated t.fast on each placement.
- Color: cyan when ≥3 placements remain → amber at 2 → danger at 1, with a single strong pulse + light haptic when entering danger.
- On rise: ring completes, flashes, empties with the rise animation; the number inside (placements remaining, micro type) is the redundant-coding for the grandma test.
- On drain-induced reprieve (interval unchanged but water dropped): ring is untouched — the ring is about *when*, the water is about *how high*. Never conflate.
- Acceptance (Phase 4 gate): a new player must answer "when will the water rise next?" correctly within 30 seconds of play, on device, no prompting.

---

## 6. Systemic UI behaviors

### 6.1 State-driven rendering (house style, restated for UI)
Screens subscribe to the store; render functions are pure `(UiTheme, GameState|MetaState) → view props`. Animations trigger **only** from `MoveEvents` / store events, never from diffing views. A `UiEventQueue` serializes overlapping events (clear → drain → rise on one move plays as a 3-beat sequence, total ≤ 950ms, input locked during board resolution only — menus never lock).

### 6.2 Interruptions & edge states
- App backgrounded mid-run: state already persisted (move-list model); on resume show Pause sheet.
- Ad fails to load on a reward button: button → disabled 4s + toast `errors.ad_unavailable`. Never an empty click.
- Purchase pending: Modal with spinner, OS handles the rest; on fail, `errors.purchase_failed` (reassuring copy — "you were not charged").
- Offline: everything works (local game); shop shows packs disabled with caption.

### 6.3 First-run & tutorial overlay
- Tutorial hints (GDD §9) are a single `HintBubble` component: caption-type text + 24px pointer, spawns anchored to a named UI target, dismissed by performing the action (never by tapping the bubble). Hand-cursor ghost replay after 4s idle. Funnel event per step.

### 6.4 ShareCard
- Pure function `(DailyResult) → string` in Core (already golden-testable) + a rendered visual card (1080×1350 sprite composition: wordmark, date, emoji water bar, tides, score) generated via offscreen Canvas → Texture2D → native share intent with text fallback. Visual card must contain nothing the text doesn't (no spoilers either way).

---

## 7. Audio-visual sync points (wiring spec)
One `JuiceDirector` maps MoveEvents → (animation, SFX, haptic) tuples from a table in `ui_theme.json`:
place (thunk, light) · clear (chime+pitch/combo, medium, per-cell pop 30ms stagger) · deep-clear flourish · drain (whoosh-glug, —) · rise (swell, heavy) · rescue (chirp, light) · lost (muted tone, —) · drown (music muffle filter 400ms before results) · star (per star) · streak/combo milestone (edge glow pulse). All values data, none in code.

---

## 8. Accessibility & comfort
- Touch targets ≥120 ref-px; thumb-zone audit: all primary actions in bottom 60% of screen during play.
- Color is never the only signal: tide ring has the number, blocks differ in luminance steps ≥15% (verified table in ui_theme.json) and coral has a texture; tested against deuteranopia/protanopia simulation (Fable runs a palette-distance check script — automatable, no eyes needed).
- Text contrast ≥4.5:1 body, ≥3:1 large (the token table above already passes; script-verify).
- Reduced-motion toggle (§1.4). Haptics toggle. No flashing >3Hz anywhere.
- Font minimum on-device ≈ 12pt equivalent (micro at 20 ref-px clears it on 5" screens).

## 9. Performance budgets
- 60fps sustained on mid-range Android (test device §10) during worst case: 4-row deep-clear + drain + rise queued.
- ≤80 draw calls in game scene; one UI atlas + one gameplay atlas; TMP sub-meshes ≤4.
- Zero per-frame allocations in steady state (tweens pooled, particles pooled, strings cached); `JuiceDirector` allocation test in play-mode CI.
- Cold boot to Home ≤2.5s on test device.

---

## 10. Build plan (slots into master prompt Phases 4–5, 8)

**4-UI-a. Theme foundation** — `ui_theme.json`, UiTheme loader, ThemedElement, Tween utility, SafeArea, ScreenStack.
✅ Theme round-trip test (every token resolves); tween duration/easing unit tests; purity: zero literals scan extended to prefab post-processor (editor script flags hardcoded colors).

**4-UI-b. Component library** — §3 components 1–13 + 16 as prefabs with smoke tests.
✅ Play-mode instantiation tests; touch-target audit script (recursively asserts ≥120px hit rects).

**4-UI-c. Game screen** — board frame, HUD, tray, drag system, water rendering §5.1, TideMeterRing §5.2, flood line, depth gauge, JuiceDirector wiring.
✅ Scripted 20-move play-mode test asserts view==state each step; event-queue ordering test (clear→drain→rise sequence).
🎯 **VISUAL GATE A (Nick, on device):** §4.3 drag feel items · tide ring grandma test · rise/drain unmistakable and *different* from each other · danger state legible at arm's length · 60fps. END TURN.

**5-UI-a. Screens** — Home, ZoneMap, Results (3 variants), Daily intro/results+Share, Settings, Shop, Consent, Pause; transitions; toasts.
✅ Navigation state-machine unit tests (back-button matrix); share string golden; strings.json coverage test (every UI text key exists; no orphan literals).
🎯 **VISUAL GATE B:** full flow home→L1→endless→daily→share; drown-vs-stuck results read differently; one-thumb playthrough.

**8-UI. Polish pass** — §7 table complete, haptics, reduced-motion verified, Tidepool diorama, accessibility scripts green, marine snow, performance budgets met (profiler capture committed).
🎯 **VISUAL GATE C (ship):** Nick signs §7.3 GDD checklist + this doc's gates A/B re-verified on fresh install.

---

## 11. Open items for Nick (⚑)

1. **UGUI confirm** (§2) — match Star Ladder's actual stack.
2. **Wordmark/logo**: one curated AI-art session needed (same pipeline as creatures). Until then the type-based fallback ships in dev builds.
3. **Zone accent hues** for zones 2–10 (zone 1 = cyan family is locked; propose at zone-content time).
4. This spec deliberately omits decoration art direction for Tidepool items beyond "flat vector, same family" — flag if you want a board.

---

## 12. Amendments — universal screen fit + genre UI pass (2026-06-12, Nick-approved plan)

Basis device: **iPhone 16 Pro Max (1320×2868, 19.5:9)**; the game must fit every portrait phone (19.5:9 → 21:9) and degrade gracefully on 16:9/tablets.

1. **§2 canvas**: reference 1080×2347, match width (see amended bullet above).
2. **Game camera**: `CameraFit.Solve` (Core, device-matrix tested) replaces fixed framing — board + side allowance spans the width; content column top-anchors below safe area + HUD band; tablets fall back to vertical-span fit. Numbers in ui_theme.json `layout`.
3. **Safe area**: screen roots are transparent safe-padded rects; ONE shared full-bleed backdrop (bg/snow/vignette) lives behind the stack. HUD top bar safe-padded; booster rail + milestone pop world-pinned so they track the board on every aspect.
4. **Genre alignment (research: Block Blast / Woodoku conventions), theme intact**: praise text beats on multi-row clears (nautical copy, type-only, no particle burst — anti-goals hold); visible combo multiplier chip beside the score; personal-best chip in Endless; tray minis scaled up toward genre readability; new-best banner moment on Endless results.
