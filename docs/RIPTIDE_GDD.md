# RIPTIDE — Game Design Document & Build Basis
**Version 1.0 — June 2026**
**Platform:** Unity 6 · Mobile · Portrait · Android first, iOS later
**Workflow:** Fable 5 via Claude Code (single-agent), Nick as visual acceptance gate
**Status:** Pre-production lock candidate

---

## 1. Vision

**One line:** Block Blast with a heartbeat — a zen block puzzle where the ocean is slowly coming for you.

Riptide is a turn-based block puzzle on a vertical grid. You place polyomino pieces and clear rows, exactly like the most-downloaded game on Earth — but water rises from the bottom of the board on a fixed turn cadence. Clearing rows pushes the tide back. Blocks the water swallows turn to coral and are lost forever. Sea creatures trapped in the grid must be rescued before the water reaches them.

The pressure is **turn-based, not real-time**. There is no clock. You can stare at the board for an hour. But every placement ticks the tide forward, so every move is a spend. This preserves the zen, no-timer feel that makes block puzzles a top-3 grossing genre while adding the strategic tension and goal variety that pure Block Blast clones lack.

### Design pillars (every decision checks against these)
1. **Learn in 5 seconds, threatened in 30.** The core is the world's most understood mechanic. The twist is visible, not explained.
2. **No clocks, ever.** All pressure is turn-economy. Deterministic, replayable, fair.
3. **The water is the villain and the spectacle.** It should be beautiful and menacing. The drain moment (tide pushed back) is the dopamine hit.
4. **Deterministic to the bone.** Same seed → same boards, same trays, same outcome for the same inputs. This enables the daily mode, fair retries, headless testing, and AI-driven balancing.
5. **Everything testable without eyes.** The sim core never touches UnityEngine. If a rule can't be asserted in a unit test, the rule is wrong.

### Why this game, why now
- Block puzzle is the highest-download core loop in mobile (Block Blast: 830M+ lifetime installs) with famously cheap CPIs and universal comprehension.
- The market leader is pure hypercasual: no meta, no goals, no narrative hook. The 2025–26 hybrid-casual playbook (simple core + light meta-progression) is the highest-growth monetization pattern in mobile, and Block Blast hasn't adopted it.
- Riptide = proven core + pressure twist + rescue/collection meta + daily ritual mode. That is the hybrid-casual formula applied to the biggest core loop available.
- The entire game is deterministic 2D logic with a JSON content pipeline — the maximum-leverage shape for an AI-built project.

---

## 2. Core Mechanics — Exact Rules

These rules are the contract. The sim core implements these and nothing else. Any change is a versioned design decision, not a code-level improvisation.

### 2.1 The board
- Grid: **9 columns × 12 rows**. Row 0 is the bottom (seabed), row 11 is the top (surface/deck).
- Cell states: `Empty`, `Block(colorId)`, `Coral` (petrified block — permanent, unclearable), `Creature(creatureId)` (occupies one cell, acts as a block for row-completion purposes), `Submerged` variants of the above.
- Portrait fit: 9 wide leaves comfortable margins on a 9:16+ screen with the tray below.

### 2.2 Water
- `waterLevel` (int, 0–12) = number of fully submerged rows, counted from row 0 upward. Water always occupies rows `0..waterLevel-1` contiguously. There is never floating water.
- Levels define `startWaterLevel` (typically 1–3). Endless starts at 1.
- **Tide tick:** placing a piece increments `tideCounter`. When `tideCounter >= tideInterval`, water rises: `waterLevel += 1`, `tideCounter = 0`. The rise resolves **after** the placement's clears resolve (clears can save you on the same move).
- **Drain:** every cleared row reduces `waterLevel` by 1, floored at the level's `minWaterLevel` (default = `startWaterLevel`). A 3-row clear drains 3. Drain resolves as part of the clear step, before the tide tick check.
- **Submersion:** when water rises into a row, every `Block` in that row becomes `Coral`. Coral never clears, never drains away, and counts as filled for nothing — a coral row can never be completed, making submerged rows permanently dead. A `Creature` in a rising row is **lost** (triggers level fail if it was a rescue target; score penalty in Endless).
- **Game over (drown):** `waterLevel >= 10` (water reaches row 10 of 12, leaving fewer than 2 playable rows). UI shows a danger line at row 9 with escalating warning states at water levels 7/8/9.

### 2.3 Pieces and placement
- Piece set (18 shapes): monomino; domino (2 rotations); triominoes I3 (2) and L3 (4); tetrominoes O, I4 (2), S, Z, L, J, T; plus 3×3 square and 1×5 / 5×1 at higher difficulty bands. Each shape is a fixed cell mask — **no player rotation** (Block Blast convention; placement-only keeps decisions about position, not orientation).
- **Tray:** 3 pieces dealt at once. All 3 must be placed (in any order) before a refill. No discards.
- **Placement validity:** every cell of the piece must land on an `Empty` cell at or above `waterLevel` (you cannot build underwater). No gravity — pieces stay where placed; nothing falls when rows clear (Block Blast convention, deliberately kept: simpler mental model, simpler sim, proven fun).
- **Row clear:** after each placement, every fully filled row above the waterline clears simultaneously. Cleared `Creature` cells = rescued. Multi-row clears in one placement are a **combo** (score multiplier + bonus drain already inherent in per-row drain).
- **Columns do not clear.** Rows only. The water theme makes horizontal-only feel natural ("bailing out a layer"), halves the clear-logic complexity, and differentiates from Block Blast. (Locked for v1; column clears are a v2 experiment behind a sim flag.)
- **Game over (stuck):** if no piece remaining in the tray can be legally placed anywhere, the game ends. Checked after every placement and every refill.

### 2.4 Deal fairness (anti-frustration, pro-determinism)
- Piece selection uses a **weighted bag** seeded RNG (xorshift128+ wrapped in `DeterministicRng`). Weights per difficulty band (see §6).
- **Refill guarantee:** at refill time, the generator verifies at least one of the 3 dealt pieces has a legal placement on the current board. If not, it redraws the offending piece(s) up to 5 times deterministically (redraws consume RNG state, so the sequence stays reproducible). If the board is so full that nothing fits after redraws, the deal stands and the player is legitimately stuck — that's a fair loss, not a bad deal.
- Mid-tray (pieces 2 and 3 of a tray) gets **no** guarantee — placing piece 1 badly can strand pieces 2–3. That's player skill expression.

### 2.5 Creatures (rescue mechanic)
- Levels embed creatures at fixed cells (from level JSON). A creature cell counts as filled for row completion. Clearing its row rescues it (it swims up and off-board in the view layer).
- Endless mode spawns a creature deterministically every `creatureSpawnInterval` trays into a random empty cell in rows `waterLevel+1 .. waterLevel+3` — always near the danger, creating rescue urgency.
- Creatures lost to submersion: level fail (if rescue goal) or −250 score and a streak-of-rescues reset (Endless).
- v1 creature roster (8): Crab, Starfish, Seahorse, Octopus, Turtle, Pufferfish, Jellyfish, Axolotl (rare/special). Purely cosmetic differences in v1 — same rules for all. (Per-species abilities are a v2 lever.)

### 2.6 Move resolution order (canonical)
For one placement: **1)** validate & commit piece cells → **2)** detect & clear full rows, rescue creatures in them, apply drain (−1 water per row, floor `minWaterLevel`), score the clears → **3)** increment `tideCounter`; if `>= tideInterval`, raise water, petrify newly submerged blocks, resolve creature losses → **4)** check drown game-over → **5)** if tray empty, refill with guarantee → **6)** check stuck game-over.
This exact order is encoded in one pure function: `SimEngine.ApplyMove(state, move) → MoveResult`. The order is unit-tested explicitly (e.g., "a clear on the tide-tick move prevents the rise if it drains below threshold" — it does, because drain precedes the tick check... locked: drain happens in step 2, tick in step 3, so a clear reduces water *before* the rise lands on the new level).

---

## 3. Game Modes

### 3.1 Voyage (level campaign) — the retention spine
- **200 levels at launch**, generated by tooling, curated by difficulty score, hand-spot-checked by you.
- Goal types (mixable per level): `RescueAll` (rescue N embedded creatures), `ClearRows(n)`, `SurviveTides(n)` (endure n water rises without drowning), `Score(n)`.
- Level params: grid preset (pre-placed blocks/coral), `startWaterLevel`, `minWaterLevel`, `tideInterval`, piece weight band, goals, move budget (optional, for star rating only — running out of moves never fails a level in v1; stars do the pressure).
- **Stars:** 3-star = goal met within par moves; 2-star = within par ×1.4; 1-star = goal met. Stars gate nothing in v1 (no key-gating dark pattern) but feed completionist replay and a coin bonus.
- World structure: 10 zones × 20 levels (Tidepool → Reef → Kelp Forest → Shipwreck → Open Sea → Storm → Trench → Ice Floe → Volcanic Vents → The Abyss). Zones are a palette/backdrop swap + difficulty band — zero new mechanics per zone in v1.

### 3.2 Endless Tide (survival) — the score chase
- Starts gentle: `tideInterval = 7`, shrinking by 1 every 4 tides until floor of 3.
- Difficulty also escalates via piece weights (bigger pieces more frequent) every 25 placements.
- Run ends on drown or stuck. Result screen: score, max combo, creatures rescued, tides survived, personal best.
- This is the interstitial-ad workhorse mode and the long-tail engagement mode.

### 3.3 Daily Riptide — the ritual & share engine
- Seed = `hash(yyyy-MM-dd + "riptide-daily-v1")`. Same board preset, same tray sequence, same creature spawns worldwide.
- Format: fixed-length challenge — survive **20 tides** with the highest score, or drown trying. Everyone's run is comparable.
- **One attempt per day.** One retry available via rewarded ad (or 100 coins). Streak counter with the same forgiveness rule as Star Ladder (1 streak-freeze earnable per week).
- Share card (emoji grid, text-only, no image dependency):
  ```
  Riptide #142 🌊
  🟦🟦🟦🟦🟦🟦⬛⬛⬛⬛  ← final water height bar
  🦀⭐🐢 rescued · 20/20 tides
  Score 14,250 · 🔥 streak 9
  riptide.game/d/142
  ```
- Daily is the cross-promo surface with Star Ladder (house ad slot each direction).

---

## 4. Difficulty, Balance & the Bot (how an AI balances a game it can't play)

Balance is done **empirically with a headless autoplay harness**, not by feel:

- `RiptideBot` plays the sim with selectable policies: `RandomLegal`, `GreedyClear` (maximizes immediate rows cleared), `GreedyHeuristic` (weighted: clears, water distance, board flatness, rescue proximity). The heuristic bot approximates a decent human.
- `BalanceRunner` (console app, no Unity): runs N=10,000 seeded games per config, outputs CSV: survival turns distribution, drown vs stuck ratio, rescue rate, score percentiles.
- **Tuning targets (lock these, tune params to hit them):**
  - Endless, GreedyHeuristic bot: median survival 45–70 placements; ≤15% of losses are "stuck" (drowning should be the dominant death — it's the thematic one).
  - Voyage band 1 (levels 1–20): GreedyClear bot 3-stars ≥80% of levels (tutorial-easy). Band 10 (181–200): GreedyHeuristic completes 30–50%.
  - Daily: GreedyHeuristic survives 20 tides 40–60% of the time at launch config.
- Every balance change = rerun the suite, diff the CSVs. This is the loop where Fable is genuinely superhuman: propose param change → 10k simulations → read the curve → iterate. No Simulator needed.

### Difficulty bands (initial values — bot-tune before lock)
| Band | Levels | tideInterval | startWater | Piece weights | Notes |
|---|---|---|---|---|---|
| 1 | 1–20 | 8 | 1 | small-heavy (1–3 cells 60%) | Tutorial arc lives here |
| 2 | 21–40 | 7 | 1 | small 50% | First multi-goal levels |
| 3 | 41–60 | 7 | 2 | balanced | Coral pre-placed appears |
| 4 | 61–80 | 6 | 2 | balanced | Survive-tides goals appear |
| 5 | 81–100 | 6 | 2 | big 35% | First 1×5 / 3×3 pieces |
| 6–10 | 101–200 | 6→5 | 2–3 | big up to 45% | Combined goals, tighter pars |

---

## 5. Meta & Economy

### 5.1 The Tidepool (collection meta)
- A separate scene: a horizontally scrolling tidepool diorama. Every creature species you've rescued at least once lives here; a counter shows lifetime rescues per species. Tap a creature → small idle animation + fact line (flavor text, Fable-generated, 3 variants per species).
- **Decorations** purchasable with coins (rocks, kelp, treasure chest, anchor, etc. — 20 items at launch). Pure cosmetic. This is the coin sink that makes the soft currency mean something without touching gameplay fairness.
- Rationale: hybrid-casual meta at minimum viable scope. No energy, no gacha, no power creep. If retention data later justifies it, the Tidepool is the expansion surface (creature levels, daily gifts).

### 5.2 Currency: Coins (single soft currency)
**Sources:** level complete 20–60 (by band/stars) · daily challenge complete 75 · daily streak milestones (7/30/100 days: 200/750/2000) · rewarded ad "coin chest" (50, capped 3/day) · Endless personal best beaten 50.
**Sinks:** boosters (below) · daily retry 100 · Tidepool decorations 200–2,000 · streak freeze 300.
Target: a free player earns ~150–250/day with normal play; booster habit costs more than income → gentle pressure toward rewarded ads → toward IAP. No hard currency in v1.

### 5.3 Boosters (3 at launch — usable mid-game, all deterministic)
| Booster | Effect | Coin cost | Rewarded-ad alternative |
|---|---|---|---|
| **Drain Pump** | waterLevel −2 (floor minWater) | 150 | 1 free per game via ad |
| **Bubble Pop** | Remove any 1 block or coral cell | 100 | — |
| **New Tide** | Reroll entire tray (deterministic redraw) | 120 | 1 free per game via ad |
Booster use is recorded in the move list, so replays/shares stay deterministic. Daily Riptide allows **zero boosters** (purity of comparison — locked).

---

## 6. Monetization (mirrors the Star Ladder stack — reuse everything)

- **Interstitials (AdMob):** after Voyage level end or Endless run end only. Caps: none before level 8, min 150s between interstitials, max 6/day, never after a daily challenge (protect the ritual). Reuse the Star Ladder ad-gap logic — including the Classic-mode gap fix from the audit.
- **Rewarded (AdMob):** placements = daily retry · free Drain Pump · free New Tide · coin chest (×3/day) · double coins on level complete. Rewarded is the primary monetization for the first 30 days of a player's life.
- **Remove Ads IAP — $4.99:** kills interstitials forever; rewarded placements remain (player-positive). Single most important IAP; surface it after the 3rd interstitial.
- **Coin packs:** $1.99 / 1,200 · $4.99 / 3,500 · $9.99 / 8,000. Soft launch can ship without these; add once booster sink data exists.
- **Consent:** UMP flow before any ad init, identical to the Star Ladder implementation. COPPA: mark as not-child-directed but with neutral age screen — creature aesthetic will skew young in store screenshots; follow the same Play Console declarations as Star Ladder.
- AdMob: new app ID + 2 interstitial / 2 rewarded ad units (prod + test pairs), wired through the same threading-safe wrapper from the Star Ladder audit fix (main-thread callback marshaling).

---

## 7. Presentation

### 7.1 Visual direction
- **Backdrop:** near-black deep ocean (`#0A0E14` base — keeps your house dark-game brand) with a subtle vertical gradient toward the zone accent color and slow drifting particle "marine snow."
- **Blocks:** bioluminescent palette — cyan `#3EE6E0`, teal `#2BB59A`, coral pink `#FF6B81`, amber `#FFB341`, violet `#8C7BFF` (a quiet nod to Direction B), ice `#BFE8FF`. Rounded 4px corners, soft inner glow, 1px luminous edge. Coral (dead) cells: desaturated bone `#5A5F66` with a porous texture — visibly *dead*.
- **Water:** translucent layered quads (2 sine-offset scrolling layers + caustic noise texture, simple shader graph — no physics, no realtime sim). Water rise = 350ms ease-in surge with foam line. **Drain = the hero moment:** 450ms recede with sparkle burst and a deep "whoosh-glug" — overdeliver here.
- **Creatures:** flat vector style, big eyes, 2-frame idle bob. 8 species × 1 sprite + rescued swim-away animation. This is the only real art demand in the game; budget one focused asset session (AI-generated → your curation), same pipeline as Star Ladder art.
- **Font:** Rungo, all four weights. UI chrome and HUD conventions ported from Star Ladder for brand consistency.

### 7.2 Audio (12 SFX + 2 loops, v1)
Place piece (soft thunk, pitch varies by piece size) · row clear (rising chime, +pitch per combo) · drain (whoosh-glug) · tide rise (low swell + warning tone at danger) · rescue (happy chirp per species family) · creature lost (muted sad tone — brief, not punishing) · game over drown (submerge muffle on the music) · button taps · star awards · streak. Music: one calm ambient loop (Voyage/Endless) + one slightly tenser variant that crossfades in at water ≥7. Loudness-safe, all optional via settings.

### 7.3 Game feel checklist (your visual acceptance gate, since no agent can see it)
Drag piece → board ghost preview with valid/invalid tint · magnetic snap within 0.6 cell · piece lifts 90px above finger (visibility under thumb) · clear = blocks pop with 30ms per-cell stagger · combo = screen-edge glow pulse · haptics: light on place, medium on clear, heavy on tide rise · danger state: water edge pulses red, music tensifies. **Each item above is a named checkbox in the Phase 8 acceptance list for you to verify on device.**

---

## 8. Architecture (Fable + Claude Code workflow)

### 8.1 Assembly layout — the load-bearing decision
```
Riptide.Core      → pure C#. ZERO UnityEngine references (enforced by asmdef).
                    SimEngine, GameState, Move, Piece, WaterSystem, Rng,
                    LevelDef, Goals, Scoring, Bot policies.
Riptide.Core.Tests→ NUnit. Runs headless via Unity Test Runner CLI *and*
                    as a plain dotnet test project (same sources, csproj shim)
                    so Fable can run the full suite in any terminal.
Riptide.Game      → Unity glue: state store, dispatch, save, ads, analytics,
                    consent, IAP. References Core.
Riptide.UI        → Views, input, animation, scenes. References Game.
Riptide.Tools     → Console apps: LevelGenerator, BalanceRunner, DailyVerifier.
```
**Why:** every gameplay rule lives where Fable can execute and verify it directly. The Unity layers are thin rendering/IO shells. This is the same Core/UI discipline as Star Ladder, taken one step further with the dotnet-runnable test shim.

### 8.2 State pattern (your house style)
- `GameState` — immutable record: board cells, tray, waterLevel, tideCounter, score, combo, goals progress, rngState, moveCount, status. 
- `SimEngine.ApplyMove(GameState, Move) → (GameState next, MoveEvents events)` — pure, allocation-light. `MoveEvents` (rows cleared, cells petrified, creatures rescued/lost, water delta, scoring breakdown) is what the view layer animates from; the view never re-derives rules.
- Dispatch/store on the Unity side mirrors Star Ladder's pattern. A full game = `(levelDef, seed, List<Move>)` — replayable, sharable, debuggable. Bug reports can attach the move list and Fable reproduces them headlessly.

### 8.3 Determinism contract
- One `DeterministicRng` (xorshift128+), state embedded in `GameState`. No `UnityEngine.Random`, no `System.Random`, no time-based anything in Core (lint rule + test).
- `DailyVerifier` tool: given a date range, generates every daily seed and bot-verifies each is completable (bot survives 20 tides on at least one policy) before content lock. Run for 365 days ahead at each release.

### 8.4 Content pipeline
- `levels/zone{N}.json` — array of LevelDefs. Generated by `LevelGenerator` (takes band params + seed, emits levels with computed par via bot), then human-curated: you play-spot-check ~3 per zone.
- `creatures.json`, `decorations.json`, `economy.json` (all tunables: costs, rewards, ad caps — **no balance numbers hardcoded in C#**).
- Save: versioned JSON (schema `v1`), local only. Same persistence wrapper as Star Ladder including the coin-persistence audit fix. Cloud save deferred to v1.1.

### 8.5 Analytics events (Firebase, same setup as Star Ladder)
`level_start/level_end {zone, level, result, moves, stars, maxWater, rescues}` · `endless_end {placements, tides, score, deathType}` · `daily_attempt {result, score, retryUsed}` · `booster_used {type, source}` · `ad_impression {format, placement}` · `iap_purchase` · `tidepool_purchase` · `tutorial_step`. The death-type split (drown vs stuck) and maxWater are the key balance telemetry.

---

## 9. UX Flow & Screens

**Screens (8):** Title/Home (Voyage continue, Endless, Daily with streak flame, Tidepool, settings gear) · Zone map (vertical scroll, 20 nodes per zone) · Game board · Results (win/lose variants, stars, coins, double-coin rewarded button, next/retry) · Daily results + share · Tidepool · Settings (audio, haptics, consent re-open, restore purchases, privacy/ToS links) · Shop sheet (Remove Ads + coin packs, modal not screen).

**Tutorial (levels 1–5, zero modal text walls):**
L1: place pieces, clear one row (water rises only once, can't lose). L2: introduces tide meter UI with a single pulse callout. L3: first creature rescue (goal arrow points at it). L4: first real drown threat + free Drain Pump demo. L5: full rules live. Each teaching beat is an event-triggered hint, dismissed by doing, never by reading. Tutorial completion is an analytics funnel — target ≥85% reach-L6.

**HUD during play:** water level implicit (it's visible), tide meter = circular ring around the tray showing placements-until-rise (the single most important UI element — it must be readable at a glance; this is a named item on your visual gate), goal chips top-left, score top-right, booster rail bottom-right.

---

## 10. Scoring

- Place piece: +1/cell.
- Row clear: `80 × rowsCleared × comboMultiplier`. Combo = consecutive placements that clear (×1, ×1.5, ×2, ×2.5 cap). 
- Rescue: +250. Survive a tide rise (Endless/Daily): +30, escalating +5 per tide.
- All scoring lives in `economy.json` → bot-tunable.

---

## 11. Build Plan — Phased for Fable/Claude Code

Single-agent phasing: each phase ends in something **provable headlessly** or a named **visual gate** for you. No phase starts until the previous phase's acceptance bullets are green. Fable holds design judgment within a phase; cross-phase rule changes get logged in `DECISIONS.md`.

**Phase 0 — Scaffold (½ day)**
Repo, Unity 6 project, asmdef structure per §8.1, dotnet test shim, CI-style local script `run_all_tests.sh`, `DECISIONS.md`, this GDD checked in.
✅ Empty test passes in both Unity CLI and `dotnet test`. Core asmdef provably cannot reference UnityEngine (compile guard).

**Phase 1 — Sim core (2–3 days)**
GameState, pieces (all 18 masks), placement validation, row clear, water/tide/drain, petrify, creatures, both game-over paths, scoring, full §2.6 resolution order.
✅ ≥60 unit tests including: drain-before-tick ordering, petrify on rise, rescue on clear, stuck detection, floor clamps, determinism (same seed+moves → identical end state hash, 1,000 random games).

**Phase 2 — Generation & content schema (1–2 days)**
DeterministicRng, weighted bag, refill guarantee with deterministic redraw, LevelDef JSON schema + loader, daily seed derivation.
✅ Refill guarantee test: 10k adversarial near-full boards, dealt tray always has a legal piece or redraw exhaustion is flagged. Seed stability test pinned (golden-file trays for 5 known seeds — protects against accidental RNG changes forever).

**Phase 3 — Bot & balance tooling (1–2 days)**
RiptideBot (3 policies), BalanceRunner CSV output, LevelGenerator with par computation, DailyVerifier.
✅ 10k-game endless run completes < 60s; survival curves output; initial band params tuned to §4 targets; 200 levels generated; 365 daily seeds verified completable.

**Phase 4 — Board rendering & input (2–3 days)** *(first Unity-visual phase)*
Board view from GameState, drag-place with ghost preview + snap, water visual, clear/petrify/rescue animations driven purely by MoveEvents.
🎯 **Visual gate #1 (you, on device):** input feel checklist from §7.3 items 1–4; water rise/drain reads instantly; 60fps on your test device.

**Phase 5 — Modes & flow (2–3 days)**
Voyage (zone map, level load, goals, stars, results), Endless, Daily (seed, single attempt, streak, share card text), all 8 screens wired.
✅ Headless: goal evaluation + star logic unit-tested; daily share string golden-tested. 🎯 **Visual gate #2:** full loop playthrough — home → L1–L10 → endless run → daily run → share.

**Phase 6 — Meta & economy (2 days)**
Coins (sources/sinks per §5.2 from economy.json), boosters incl. deterministic recording, Tidepool scene, decorations, save/load versioned.
✅ Economy unit tests (caps, floors, booster determinism in replay). Save migration test harness seeded with a v1 fixture. 🎯 **Visual gate #3:** Tidepool scene review.

**Phase 7 — Ads, consent, analytics, IAP (2 days)**
UMP consent → AdMob init (threading-safe wrapper port), interstitial caps per §6, rewarded placements, Remove Ads IAP, Firebase events per §8.5.
✅ Ad-cap logic unit-tested (it's pure: time/count rules). 🎯 **Visual gate #4:** consent flow + test ads on device, every rewarded placement pays out exactly once (audit-fix regression check).

**Phase 8 — Polish, audio, tutorial, store (2–3 days)**
Juice checklist §7.3 complete, 12 SFX + 2 loops, haptics, tutorial levels 1–5 with funnel events, app icon, store listing assets, privacy policy page.
🎯 **Visual gate #5 (ship gate):** full §7.3 checklist signed off by you; tutorial run with a fresh save; closed-track build uploaded.

**Realistic calendar: 3–4 weeks part-time to closed testing**, given the Star Ladder ports (ads, consent, save, share, fonts, UI conventions) are lift-and-adapt rather than new builds.

---

## 12. Out of Scope v1 (written down so it stays out)
Multiplayer/leaderboards · cloud save (v1.1, with the Star Ladder solution) · iOS (after Android validates) · creature abilities · column clears · hard currency/gacha · events/live-ops calendar · localization beyond EN (strings table from day 1 though — `strings.json`, no literals in UI code) · notifications (v1.1: daily reminder + streak-at-risk, port Star Ladder plan).

## 13. Risks & honest mitigations
- **Crowded core genre.** Mitigation: the twist is visible in a 10-second screen recording (water surging/draining), daily mode + Star Ladder cross-promo provide non-paid acquisition, and rescue meta differentiates store listing. This is still the biggest risk — soft-launch metrics (D1 ≥35%, D7 ≥12%, tutorial completion ≥85%) decide whether to push or park.
- **No-gravity + coral death spiral may feel unfair.** Mitigation: bot data on drown-vs-stuck ratio; Drain Pump exists precisely as the player's pressure valve; tune `minWaterLevel` floors.
- **Tide meter readability** is the one UI element the whole design leans on — it gets its own visual gate item and a literal grandma test (someone unfamiliar must explain when water will rise after 30 seconds of play).
- **Art (creatures) is the only non-AI-native asset class.** Mitigation: 8 sprites + 1 animation each is one curated generation session; style is deliberately flat/simple.

## 14. KPI targets (soft launch, closed track → open)
Tutorial completion ≥85% · D1 ≥35% · D7 ≥12% (top-25 casual benchmark ~14.9%) · sessions/day ≥2.2 · daily-mode participation ≥25% of DAU by day 14 · rewarded engagement ≥30% of DAU · crash-free ≥99.5%.

---

*Companion docs to produce next: `RIPTIDE_MASTER_PROMPT.md` (Claude Code operating rules + shared context block + Phase 0–1 task breakdown in house style) and `economy.json` / `levels/zone1.json` starter fixtures.*

---

## 15. Amendment — the Pets & Tides expansion (2026-10-18, Nick-approved: all four of four proposed)

Supersedes §2.5's "purely cosmetic differences in v1" and §12's out-of-scope entries for *creature abilities* and *events*. Everything here is data in `Assets/Resources/Content/expansion.json`; rulings in DECISIONS.md "Pets & Tides expansion".

### 15.1 Creature Powers (rules change)
When a rescued creature's row clears (§2.6 step 2), its species power fires — in rescue order (row ascending, then column), after the clear's drain, before the step-2 win check. Powers apply in **Voyage, Endless and Event Tide only — never the Daily** (purity of comparison; its pinned boards and share cards stay valid).

| Species | Power | Effect (tunables in expansion.json) |
|---|---|---|
| Crab | Pinch | removes the coral cell nearest the crab (Manhattan; ties → lower row, then lower column) |
| Starfish | Wish | +starfishScore points |
| Seahorse | Steady | the tide counter is relieved by seahorseTideRelief this move (floor 0) |
| Octopus | Ink | removes up to octopusCoral coral cells, lowest rows first, left→right |
| Turtle | Calm | water −turtleDrain, floored at minWaterLevel |
| Pufferfish | Puff | pops the block/coral cells orthogonally adjacent to where it sat (never creatures) |
| Jellyfish | Glow | the combo chain extends by 1 (a stronger multiplier on the next clear) |
| Axolotl (rare) | Regrow | water −axolotlDrain (floored) and the tide counter resets to 0 |

Deterministic: no RNG draws, so the deal stream is untouched. The sim emits `PowerEvent`s; the view animates them. A config without powers (`LevelConfig.Powers == null`) behaves byte-identically to before — every existing golden and cross-pipeline pin stays valid.

### 15.2 Pet Pals (meta)
- Each species levels 1–5 from its lifetime rescue count (`pets.levelThresholds`); unmet = level 0.
- **Daily pet gift:** once per calendar day, coins = Σ pet levels × giftCoinsPerLevel, capped.
- **Buddy:** one met species can be equipped; it rides on the in-game HUD and adds buddyBonusPercentPerLevel × level % to every run's coin award (all modes — coins are not part of score comparison).

### 15.3 Daily Quests & Badges (meta)
- 3 quests per calendar day, drawn deterministically from the pool by the date (same for everyone, like the Daily). Progress resets daily; each completed quest is claimed once for coins.
- Badges: lifetime milestones over tracked stats (rescues, species met, rows, best single clear, tides, stars, endless best, streak, decorations, powers, quests, events, pet levels); each pays once when claimed.

### 15.4 Weekly Event Tide (mode)
- One event per ISO-ish week (`CivilDate.WeekIndex`), rotating through `events.rotation`. Endless ruleset + per-event overrides (start water, tide interval, spawn cadence, featured species) + one goal. Seed derives from the week, so the week's board is shared; attempts are unlimited.
- First completion each week pays the event's coins and unlocks its event-only decoration (one-time). Boosters and the continue are allowed (it's a Voyage-like challenge, not a comparison mode). Powers are on.

---

## 16. Amendment — Tidelings: eggs, growing up & the Pod (2026-10-18, Nick-approved direction — paraphrased: more depth for the pets; eggs; unique sea creatures that grow as you collect more or rarer ones; perks that depend on which you pick. Originality review the same day: keep clear of other creature-collection games.)

Supersedes §15.2 (pet levels and the flat buddy coin bonus). The board's 8 rescue species (§2.5) and their in-run powers (§15.1) are unchanged — Tidelings are a **meta layer**: the sim never sees a growth stage, and the Daily never sees a perk. All numbers are data in `Assets/Resources/Content/pals.json` (code says "pal" internally; players only ever see **Tidelings**).

### 16.1 The Tidelings — 16 lines × 3 stages, in four tiers
Every creature is a **line** of three named, visually distinct stages: **Baby → Grown → Grand**. Tidelings come in four **tiers**, and the tier decides both how often one arrives and how strong its ability is (Nick, 2026-10-18: rare ones come from eggs, not the board, with abilities split by tier):

- **Common** — the six everyday board creatures. Befriended by rescuing them on the board (or by Call). Abilities: small coin perks.
- **Rare** — run helpers. **Epic** — strong run perks. **Legendary** — signature, run-shaping abilities. Rare, Epic and Legendary Tidelings arrive **only by Call** (§16.3). The Jellyfish and Axolotl still appear on the board as rescue targets (the sim is unchanged), but rescuing one leaves **eggs**, not the Tideling.

| # | Line | Tier | Baby | Grown | Grand | Ability (Baby / Grown / Grand) |
|---|---|---|---|---|---|---|
| 0 | Crab | Common | Pinchy | Clawdette | Lord Pincerton | **Treasure Pincers** — +5 / 8 / 12 % run coins |
| 1 | Starfish | Common | Twinkle | Stellina | Supernova | **Lucky Star** — +3 / 5 / 8 coins per Voyage star |
| 2 | Seahorse | Common | Ripple | Galloper | Tidecharger | **Quest Gallop** — quest coins +10 / 15 / 25 % |
| 3 | Octopus | Common | Inky | Octavia | Krakenling | **Ink Discount** — boosters −5 / 10 / 15 % |
| 4 | Turtle | Common | Peanut | Mossback | Elder Atoll | **Gift Wrap** — daily pet gift +20 / 35 / 50 % |
| 5 | Pufferfish | Common | Puff | Spikeball | Blowfin Boss | **Rescue Reward** — +2 / 3 / 5 coins per creature rescued |
| 6 | Jellyfish | Rare | Glim | Glowbell | Aurora Bell | **Warm Glow** — eggs come +25 / 50 / 75 % sooner |
| 7 | Narwhal | Rare | Tusky | Unicorna | Polaris | **Steady Current** — the tide's starting interval +1 / +1 / +2 |
| 8 | Manta Ray | Rare | Flapjack | Wavecloak | Grand Mantle | **Puff Shield** — 1 / 1 / 2 free Bubble Pops per run |
| 9 | Sea Otter | Rare | Pebbles | Driftwood | Kelp Captain | **Fresh Catch** — 1 / 1 / 2 free Rerolls per run |
| 10 | Axolotl | Epic | Lotl | Gillian | Axolord | **Second Wind** — 1 / 1 / 2 free continues per day |
| 11 | Anglerfish | Epic | Flicker | Lurelight | Abyss Beacon | **Deep Luck** — Sparkle odds ×2 / 3 / 4 · Legendary pity −5 / 10 / 15 calls |
| 12 | Nautilus | Epic | Swirl | Spiralla | Chamber Lord | **Safe Harbor** — start water −1 / −1 / −2 (floored) |
| 13 | Sea Dragon | Legendary | Leafy | Ribbontail | Tidewyrm | **Old Tides** — the Pod earns bond ×1.5 / 2 / 3 |
| 14 | Giant Squid | Legendary | Inklet | Longarm | The Colossus | **Ink Storm** — 1 / 2 / 3 free Drain Pumps per run |
| 15 | Great Whale | Legendary | Splash | Songkeeper | Leviathan | **Whale Song** — tide +1 / 2 / 2 *and* start water −1 / −1 / −2 |

### 16.2 Owning, bond and growing up
- A Common is **owned** from its first rescue (or its first Call); everything rarer is owned from its first Call. Owned Tidelings start as Babies.
- **Bond** grows per Tideling: +1 per board rescue of its species (every mode, the Daily included — bond is collection, not competition); +1 per finished run for each Pod member (+2 for the leader); a **duplicate Call** adds bond by tier (Common 3 · Rare 6 · Epic 10 · Legendary 15). Old Tides multiplies run and rescue bond (not duplicates).
- **Growing up** is the player's moment: when bond reaches the next threshold a *Grow up!* button lights up; tapping plays the celebration and the Tideling becomes its next stage. Thresholds (bond to reach Grown / Grand) by tier: Common 15 / 50 · Rare 10 / 30 · Epic 8 / 24 · Legendary 6 / 18 — rarer Tidelings arrive less often, so they need less.
- **Sparkle Tidelings:** each Call has a 1-in-64 chance (Deep Luck raises it) to arrive as a Sparkle — an alternate palette with a glint. Per line, purely cosmetic (toggle on the Tideling card).

### 16.3 Eggs and the Call
**Eggs are a currency you collect, and spending them is how Tidelings arrive.** Eggs are **earned only — never sold for coins or real money** — so a Call is never a paid random draw (no loot boxes); the odds are always shown on the Call screen.

- **The Call** (the Conch): **5 eggs** for one Tideling, or **45 eggs for ten** (one free). Tier odds: **Common 70 % · Rare 22 % · Epic 6.5 % · Legendary 1.5 %**, split evenly among the Tidelings of a tier (each odds line is on screen).
- **Guarantees (pity):** an Epic-or-better arrives by the **20th** Call since the last one; a Legendary by the **60th** since the last (Deep Luck shortens it). A ten-Call always holds at least one Rare-or-better. The screen shows "Legendary guaranteed in N calls".
- **Collecting eggs:** **one egg every 5 boards** you finish — any mode, win or lose, never a quit (Warm Glow speeds it; Nick: steady, not annoying) · a **Welcome gift of 10 eggs** · Voyage first clears of each zone's level 5 (2), 10 (3), 15 (2), 20 (5; 8 in zones 6–10) · a Daily **win** (2) · claiming all of the day's quests (3) · the week's first **Event Tide** win (10) · every 20 tides in one Endless run (1, max 3 per run) · rescuing a Jellyfish or Axolotl on the board (1).
- Calls are deterministic from the save's call counter and a per-save seed (Core purity: no `System.Random`), so every Call can be replayed and tested.
- **The roster will grow.** Adding a Tideling is data: a `pals.json` entry (tier, three stage names through the originality gate, an ability) plus its art model; tier odds re-split automatically, and pity, the Journal and the Pod need no change.

### 16.4 The Pod and abilities
- The **Pod** is up to three Tidelings you bring along: slot 1 is always open (the **leader** — on Home and perched on the tray), slot 2 opens at 4 Tidelings owned, slot 3 at 8.
- Every Pod member's ability applies at its current stage; the tier sets the ability's weight class (Common coin perks → Legendary run-shapers). **Abilities never apply in the Daily** (comparison purity, like powers). Abilities that touch the board (Steady Current, Safe Harbor, Whale Song) apply through the run config; the rest are meta (coins, boosters, continues, eggs, gifts, bond).
- The **daily pet gift** (§15.2) becomes: Σ stages of all owned Tidelings × 3 coins, capped at 120, × Gift Wrap.

### 16.5 The Tide Journal
A collection book of all 48 forms (16 lines × 3 stages) plus each line's Sparkle, sorted by tier. Unseen forms are silhouettes with "???". Completing a line (all three stages) pays 100 coins once. New badges: *First Call* (1 Call) · *Conch Keeper* (100 Calls) · *Full Pod* (own 8 Tidelings) · *Tide Journal* (own all 16) · *Legend* (own a Legendary) · *Sparkle!* (own a Sparkle); *All Grown Up* means a Grand-stage Tideling.

### 16.6 Screens (UI direction mocked for sign-off before it is built)
- The Tidepool tab becomes **Tidelings**: the Pod (leader + two slots with abilities), the collection/Journal grid by tier, the **Conch** (egg count, row progress to the next egg, Call ×1 / ×10, odds, pity counter), and the existing Tidepool diorama.
- **Call** and **Grow up** are full-screen celebrations in Riptide's own sea language (see §16.8): the conch sounds, a wave rolls in and the Tideling pops out of a bubble burst in its tier's color (a Legendary gets a whirlpool and a rainbow wake); a growing Tideling spins up in a whirlpool of bubbles and lands as its new form with confetti — never a white-silhouette flash-morph.
- Home's stage shows your egg count; Results show a quiet "3 / 5 boards to your next egg" pip row and any eggs earned. Egg news is never a popup that interrupts play (Nick: "we don't wanna be annoying").

### 16.7 Migration (save v5)
Bond starts at each species' lifetime rescues; every species met under the old rules is owned as a Baby — including a Jellyfish or Axolotl (grandfathered) — and can grow up on first look if its bond already qualifies; the v4 buddy becomes the Pod leader; the Welcome gift grants 10 eggs.

### 16.8 Originality guardrails (creature-collection genre)
Genre mechanics — collecting, eggs, growth stages, rarity, alternate colors, a collection book — are common to many games and not ownable; **names, characters, catchphrases, specific presentations and patented mechanics are**. Rules for this game, enforced where a machine can:
- **Names:** every Tideling name is original and is checked by ContentCheck against a denylist of existing sea-creature character names from well-known creature-collection franchises (edit distance ≤ 2 fails the build). No compound names built the way those franchises build theirs for the same animal.
- **Words never used in player-facing text** (ContentCheck scans strings.json): another franchise's name, its book ("…dex"), "shiny", "evolve/evolution", "trainer", "gotta catch". We say Tidelings, Tide Journal, Sparkle, grow up, leader.
- **Mechanics we do not build:** throwing or aiming an item to capture creatures, capture-rate rolls, summoning creatures from a thrown item, riding creatures, creature battles, elemental type charts, creatures stored in ball-like items. (Several of these are the subject of active patent litigation in the genre.) Tidelings are befriended by rescue or hatched — that's all.
- **Presentation:** chibi real-world sea animals in Riptide's sticker style; eggs are sea objects (wave-banded, pearl-sheened), not white eggs with colored spots; the Call is a conch, never a ball or capsule; celebrations use waves, bubbles and confetti, never the white-silhouette morph.
- Before store launch, the Tidelings name and the 36 form names get a formal trademark search (the in-repo checks are a guardrail, not legal clearance).
