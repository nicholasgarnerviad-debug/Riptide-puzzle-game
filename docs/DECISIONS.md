# Riptide — Decision Log

One line per judgment call not literally specified in `RIPTIDE_GDD.md` (master prompt rule 9).
Format: date · decision · rationale. Reviewed by Nick at every visual gate.

---

- 2026-06-11 · `Riptide.UI.asmdef` references `Riptide.Core` in addition to `Riptide.Game` (GDD §8.1 lists only Game) · asmdef references are not transitive; views render from Core's `GameState`/`MoveEvents` (§8.2), so UI cannot compile against Game alone.
- 2026-06-11 · Purity-guard regex covers `DateTime.UtcNow`, not only the contract-named `DateTime.Now` · GDD §8.3 bans "time-based anything" in Core; UtcNow is the same hazard.
- 2026-06-11 · Purity guard scans `Assets/Tests/Core` as well as `Assets/Scripts/Core` (0B names only Scripts/Core) · master prompt rule 4 requires Core.Tests purity too.
- 2026-06-11 · dotnet shim targets `net10.0`, `LangVersion 9`, NUnit 3.14 · only the .NET 10 SDK/runtime is installed on the build machine; LangVersion and NUnit pinned to Unity 6's C# level and Unity Test Framework's NUnit 3 API so the identical sources compile in both pipelines.
- 2026-06-11 · `CoreInfo` placeholder type in Riptide.Core · keeps the assembly non-empty so asmdef + shim + trivial test are provable end-to-end in Phase 0; superseded by real sim types in Phase 1.
- 2026-06-11 · `RIPTIDE_MASTER_PROMPT.md` lives in `/docs` next to the GDD · the master prompt's repo layout reserves `/docs` for project documents; repo root stays clean.
- 2026-06-11 · `Assets/Content/.gitkeep` placeholder · git cannot track the empty `/Content` folder from the contract layout; removed when Phase 2 fixtures land.
- 2026-06-11 · `nullable enable` applied to all four assemblies via per-asmdef `csc.rsp` · master prompt context block mandates "C# 9+, nullable enabled"; csc.rsp is Unity's per-assembly mechanism for it.

## Phase 1

- 2026-06-11 · **FLAG FOR NICK** — piece set implemented as the 20 masks the GDD §2.3 list enumerates, though the headline says "18 shapes" · the enumeration (mono 1, domino 2, I3 2, L3 4, O/I4×2/S/Z/L/J/T 8, 3×3 1, 1×5+5×1 2) sums to 20 and no reading yields 18; masks are data, trivially trimmed once Nick rules — must resolve before Phase 2 deal weights lock.
- 2026-06-11 · goal evaluation runs after §2.6 step 2 (clears) and again after steps 3–4 (tide + drown) · §2.6 doesn't place a win check; evaluating right after clears is player-favorable ("clears can save you on the same move"), and SurviveTides goals can only complete at the tide step.
- 2026-06-11 · terminal-status precedence = §2.6 step order: creature-loss fail (step 3) beats drown (step 4); a rise that completes SurviveTides but kills a rescue target = fail · goals are AND-ed, a lost target makes RescueAll unsatisfiable.
- 2026-06-11 · submersion is derived (`row < waterLevel`), not stored per-cell · §2.2's contiguity rule makes waterLevel the single source of truth; "Submerged variants" of §2.1 are a view-layer presentation concern.
- 2026-06-11 · `DeterministicRng` + a simple uniform dealer land in Phase 1, not 2 · `GameState.rngState` (§8.2) and §2.6 step 5 (refill) require them; weighted bag, refill guarantee, and golden-file pins stay in Phase 2 per master prompt.
- 2026-06-11 · xorshift128+ = Vigna reference variant (shifts 23/18/5), seeded via splitmix64, all-zero state guarded · concrete algorithm choice within the contract's "xorshift128+"; Phase 2 golden files will pin the stream forever.
- 2026-06-11 · canonical fixed orientations chosen for S/Z/L/J/T masks · GDD names the shapes but draws no masks; they're data and swappable on visual veto.
- 2026-06-11 · grid 9×12, drown at waterLevel≥10, tray size 3 are compile-time constants in Core; tideInterval/scoring/spawn-interval/goals are injected via LevelConfig · §2 fixes the former as exact rules; rule 7 ("no balance numbers in C#") governs the latter.
- 2026-06-11 · no C# `record`/`init` syntax in Core; classic immutable classes · avoids IsExternalInit divergence between Unity's compiler and the net10 dotnet shim compiling identical sources.
- 2026-06-11 · endless creature spawn skips silently when rows waterLevel+1..+3 have no empty cell · GDD silent on a full band; skipping beats spawning out-of-band.
- 2026-06-11 · creature loss fails the level only when a RescueAll goal exists; otherwise −250 + rescue-streak reset in any mode · reading of §2.5's "level fail (if rescue goal) or −250 …(Endless)".
- 2026-06-11 · `rescueStreak` tracked in GameState and reset on creature loss per §2.5; no scoring effect wired · v1 GDD defines the reset but no bonus; hook kept for balance work.
- 2026-06-11 · score may go negative via −250 penalties · GDD defines no floor; clamping would be an invented rule — bot data in Phase 3 will show if it matters.
- 2026-06-11 · block colorIds rolled from the deal RNG (6 palette colors), creatures carry creatureId · §2.1 `Block(colorId)` needs a deterministic color source; cosmetic only.

## Phase 2

- 2026-06-11 · hand-rolled minimal JSON parser in Core (objects/arrays/strings/numbers/bool/null, strict, line+column on every node) instead of Newtonsoft/System.Text.Json · 2C requires "schema violations throw with file/line"; zero external deps keeps the Unity and dotnet compiles of identical sources identical; level JSON is generated by our own tooling.
- 2026-06-11 · golden files are committed C# constant files (`Golden/`), not data files read at test time · master prompt rule 4 bans IO in Core.Tests; a constants source file is still a committed, diffable golden artifact.
- 2026-06-11 · piece weights live on LevelConfig (resolved at load time); levels reference a `weightBand` from economy.json or carry inline `pieceWeights` · §3.1 lists "piece weight band" as a level param; bands are balance data (rule 7) so they live in economy.json for the Phase 3 bot to tune.
- 2026-06-11 · weighted draw = NextInt(totalWeight) + cumulative walk; with uniform weights the stream is bit-identical to Phase 1's NextInt(20) · protects Phase 1 determinism results and makes the goldens stable across the upgrade.
- 2026-06-11 · refill-guarantee semantics: up to 5 redraw ROUNDS; each round redraws every currently-unplaceable piece; stop as soon as any piece fits; after 5 rounds the deal stands (legit stuck) · §2.4 says "redraws the offending piece(s) up to 5 times" without fixing round structure; this reading is deterministic and minimal.
- 2026-06-11 · the refill guarantee also applies to the initial NewGame deal · §2.4 says "at refill time" and the initial deal is tray #1 with a stuck check; near-empty boards never redraw so goldens/Phase-1 streams are unaffected.
- 2026-06-11 · redraw activity is not surfaced in MoveEvents · views render the final tray; redraws are invisible mechanics (§2.4 anti-frustration).
- 2026-06-11 · content fixture files on disk are validated by `Tools/ContentCheck` (gate 3 of run_all_tests.sh), loaders are unit-tested against embedded JSON strings · keeps Core.Tests IO-free while still gating the real files.
- 2026-06-11 · initial band weight arrays follow GDD §4 qualitative targets (band 1 small≈60%, bands 1–4 zero big pieces, band 5 introduces 1×5/3×3 at ≈35% big, bands 6–10 ramp to ≈45%) · §4 marks these "initial values — bot-tune before lock"; Phase 3 owns the final numbers.
- 2026-06-11 · endless `creatureSpawnIntervalTrays` initial value 4 in economy.json · §2.5 names the param but no number; Phase 3 bot tuning owns it.
- 2026-06-11 · daily seed = FNV-1a 64 over UTF-8 of "yyyy-MM-dd" + "riptide-daily-v1" · §3.3 specifies hash() without naming one; FNV-1a 64 is platform-stable and already the project's hash; pinned by golden tests forever.
- 2026-06-11 · proceeding with the 20-mask catalog while the 18-vs-20 ruling is open · band arrays are data; a trim is a data edit, but the ruling must land before Phase 3 locks tuned weights.

## Phase 3

- 2026-06-11 · GreedyHeuristic features = clears, rescues, water headroom, bumpiness (column-height flatness), creatures-in-danger, almost-full completable rows, game-over penalty; winning moves taken unconditionally · realizes §4's "(weighted: clears, water distance, board flatness, rescue proximity)"; all weights in economy.json `bot.greedyHeuristic`.
- 2026-06-11 · bots evaluate candidates via `PeekEvaluator`, an allocation-free mirror of §2.6 steps 1–4, drift-guarded by `PeekDriftTests` against the real engine across three config flavors · full ApplyMove per candidate would dominate BalanceRunner runtime; the drift tests make divergence impossible to miss.
- 2026-06-11 · **endless final params** (10k-game evidence in `docs/balance/endless_gh_10k.csv`): band **11** (new small-heavy data band 20/12/12/8/8/6×4 + tiny mediums, no bigs), startTideInterval **6**, shrink every **2** tides, floor **2**, big-weight bonus 1 × max 1 step, spawn every 4 trays → GH median survival **54** (target 45–70 ✓), stuck **14.8%** of losses (target ≤15% ✓), drown-dominant 85% · §3.2's stated numbers (7 / every-4 / floor-3, medium band) produced **100% stuck deaths at water 2–3 for every policy** — the coral spiral + tray-fit failure structurally preempts drowning; treated §3.2's values as the "initial values" §4 marks for bot-tuning under the 3C mandate.
- 2026-06-11 · the §4 "drowning dominant" outcome requires small-heavy late trays · big/medium pieces strand mid-tray on fragmented boards (no guarantee mid-tray per §2.4) and stuck fires before water ever reaches 10; small-piece dominance keeps trays placeable while the shrinking interval + coral spiral drive water to the drown line.
- 2026-06-11 · **daily final params** (`docs/balance/daily_gh_10k.csv`): own tuning block — band 11, start 7, shrink every 2, floor 2, spawn 4 → GH survives 20 tides **44.0%** (target 40–60% ✓) · the endless curve made tide-20 unreachable (GH median death 54 < tide-20 ≈ move 60); daily needed an independent pressure curve, so DailyTuning grew the full parameter set.
- 2026-06-11 · level pars = GH median moves over 30 play-seeds; zones 1–2 use max(GH, GC) medians · tutorial pars must be generous for naive play (§4 band-1 target is GreedyClear-based).
- 2026-06-11 · "GC 3-stars a level" = GreedyClear completes ≥50% of 30 runs AND its median moves ≤ par · §4 names the target but not the per-level criterion.
- 2026-06-11 · zone completion windows for generation: z1–2 ≥0.90, z3–4 ≥0.75, z5–6 ≥0.65, z7 0.55–0.95, z8 0.55–0.90, z9 0.45–0.80, z10 0.30–0.50 (the §4 band-10 target) · measured results: 0.96/0.96/0.89/0.90/0.76/0.79/0.73/0.71/0.63/**0.38**; band-1 GC 3-star 20/20 (target ≥80% ✓).
- 2026-06-11 · **known content-quality gap**: the ≥0.90 window collapsed zone-1 variety to ClearRows(2) repeats after the tutorial arc · §4 targets met; flag for Nick's curve review — a recipe pass (e.g., relaxing rescue-level verification in z1) plus regen is cheap if variety matters now.
- 2026-06-11 · daily verification = deterministic GH line, then GC, then up to 200 reproducible "randomized 1–5 opening moves + GH" witness lines · GH alone verifies only ~56% by design (the 44% win-rate target); a witness line is a deterministic completability proof; all 365 dailies from 2026-06-11 verified (`docs/balance/daily_verification.csv`).

## Phase 4

- 2026-06-11 · content moved to `Assets/Resources/Content` · runtime loading via Resources.Load (Addressables are out of scope §12); Tools' default content root updated to match.
- 2026-06-11 · the entire view layer is constructed at runtime from code — no hand-authored scenes, prefabs, materials, or shadergraphs · rule 3 ("prove, don't describe"): code is reviewable and play-mode-testable; asset YAML with GUID cross-references is neither; `GameBootstrap` auto-spawns in SampleScene via RuntimeInitializeOnLoadMethod and the acceptance test instantiates its own.
- 2026-06-11 · water visual = two code-scrolled translucent unlit quads with a generated Perlin caustic texture (§7.1's "simple shader, no physics") · realized with the built-in URP Unlit shader + UV offset animation; no hand-written shader assets to drift.
- 2026-06-11 · input thresholds (snap 0.6 cell, 90px lift, drag deadzone) live on an `InputTuning` ScriptableObject with §7.3 defaults in code · contract 4B requires the SO; the instance is code-created until visual-gate feedback tunes values.
- 2026-06-11 · `AnimationDriver` exposes `InstantMode` for tests (tweens skipped, board re-rendered from state immediately) · the acceptance test asserts view==sim truth; animation timing is what the visual gate judges.
- 2026-06-11 · blocks/creatures use generated placeholder sprites (rounded cells, eyed-circle creatures) · the curated art session is Phase 8 scope; visual gate 1 evaluates input feel and water readability per the contract.
- 2026-06-11 · InputController targets the Input System exclusively · ProjectSettings runs activeInputHandler=1 (new backend only); legacy UnityEngine.Input would throw at runtime.
- 2026-06-11 · Core.Tests must stay on Core's public surface — the dotnet shim merges Core+Tests into one assembly and hides `internal` boundaries that Unity's separate asmdefs enforce · caught live: the adversarial test compiled in the shim but failed in Unity (CS0117 on an internal); the Unity compile is the boundary truth.
- 2026-06-11 · **VISUAL GATE 1: OPEN, deferred by Nick** — "go" received without checklist results · proceeding per the owner's explicit instruction; every gate-1 item (input feel, tide meter, water reads, 60fps) is re-covered inside gate 2's full playthrough and must be signed there.

## Phase 5

- 2026-06-11 · star thresholds: 3★ = moves ≤ par, 2★ = moves ≤ ceil(par × 1.4), 1★ = completed · §3.1 says "within par ×1.4"; ceil is the player-generous reading.
- 2026-06-11 · level coin award = base + perBand×(zone−1) + perStar×(stars−1), all values in economy.json `coins` · §5.2 specifies "20–60 by band/stars" without a formula; chosen values span 20 (z1 1★) to 57 (z10 3★).
- 2026-06-11 · Core date math = days-from-civil (Hinnant algorithm) on plain ints; week index = (epochDays+3)/7 (Monday-aligned) · streak/daily logic needs date arithmetic and Core cannot touch DateTime (§8.3); the Game layer supplies today's date.
- 2026-06-11 · streak rules: +1 per consecutive day; a 1-day gap consumes a held freeze instead of resetting; otherwise reset to 1; at most one freeze held; freeze acquisition limited to one per calendar week (coin sink wired in Phase 6) · §3.3/§5.2 name the freeze and its 300-coin price but not mechanics; mirrors the stated Star Ladder convention.
- 2026-06-11 · daily number #N = days since economy.json `daily.epochDate` (2026-06-11 = #1) · §3.3's share card shows "#142" without defining the origin.
- 2026-06-11 · share card: water bar = 10 squares (🟦×level + ⬛×rest, drown line scale), up to 3 distinct rescued species in rescue order, emoji per species from creatures.json, score with comma grouping, fixed URL riptide.game/d/N · golden-tested verbatim per contract 5D.
- 2026-06-11 · Rungo font files are not in the repo — UI uses Unity's LegacyRuntime.ttf until Nick provides the licensed Rungo assets from Star Ladder · flagged for visual gate 2; font swap is one line in UiKit.
- 2026-06-11 · daily attempt-lock and streak persist via PlayerPrefs JSON in Phase 5; the versioned save file (Phase 6) absorbs them · contract 5D needs working streaks one phase before the save system exists.
- 2026-06-11 · screens are code-built uGUI (legacy Text, no TMP) following the Phase 4 no-authored-assets rule · TMP would drag in asset dependencies; legacy Text ships in com.unity.ugui.
- 2026-06-11 · string assertions over emoji must be ordinal · caught live: Unity's Mono runs culture-sensitive IndexOf in NUnit's Does.Contain and treats emoji as ignorable — a "not contains 🐙" check passed on CoreCLR and failed on Mono; ordinal IndexOf is exact on both.
- 2026-06-11 · **VISUAL GATE 2: OPEN, deferred by Nick** ("go" without checklist) · gates 1+2 now both ride on gate 3; the playthrough debt is accumulating and is called out in the phase report.

## Phase 6

- 2026-06-11 · `GameState.MoveCount` counts PLACEMENTS only; booster moves never increment it · stars/par (§3.1) and weight escalation (§3.2 "every 25 placements") are placement-semantics, and the Phase 2 golden hashes stay valid without a rule-5 STOP; the replay move list itself carries total length.
- 2026-06-11 · booster moves never advance the tide counter and never touch the combo chain · §2.2 ticks "placing a piece"; boosters are the pressure valve (§13), and punishing combo or tide for using one defeats the purpose.
- 2026-06-11 · Bubble Pop may target any Block or Coral cell, including submerged coral; never a creature or empty cell · §5.3 says "any 1 block or coral cell"; freeing drowned rows is exactly the §13 death-spiral valve.
- 2026-06-11 · New Tide deals a full fresh tray of 3 (not just unplaced slots) through the §2.4 guarantee at current escalation weights · "reroll entire tray" read player-positively; the deal can exhaust → legitimate stuck, checked.
- 2026-06-11 · coins live OUTSIDE GameState — the sim validates booster legality (config `BoostersAllowed`), the Game layer checks/spends the wallet before dispatch · replays of recorded move lists must reproduce without a wallet (§5.3 "recorded in the move list, so replays stay deterministic").
- 2026-06-11 · booster prices live in economy.json `boosters` block (drainPump 150, bubblePop 100, newTide 120 per §5.3) · rule 7.
- 2026-06-11 · save file v1: single JSON at persistentDataPath, atomic temp-file swap, any parse/shape error → fresh save + pending analytics flag, never a crash · contract 6D; the serializer/parser pair is pure Core (dotnet-testable), IO is a thin Game wrapper.
- 2026-06-11 · Phase 5 PlayerPrefs meta (voyage/streak/best/daily-lock) imports into the save file on first run, then PlayerPrefs is abandoned for meta · keeps testers' progress across the upgrade.
- 2026-06-11 · creatures.json gains 3 `flavor` lines per species (24 written); decorations.json ships 20 items costed 200–2000 per §5.2 · content data validated by loaders + ContentCheck.
- 2026-06-11 · rewarded coin-chest CAP logic (3/day) ships now in the save (claims+day), the ad behind it is Phase 7 · cap rules are pure and testable today; the button stays stubbed.

## Phase 7

- 2026-06-11 · **VISUAL GATE 3: OPEN, deferred by Nick** ("begin phase 7") · gates 1–3 all outstanding; gate 4 (consent/test-ads/sandbox purchase) cannot itself be deferred — it requires Nick's accounts and device.
- 2026-06-11 · ads/analytics/IAP are built against seam interfaces with fakes; real adapters (`GoogleMobileAdsAdapter`, `FirebaseAnalyticsAdapter`, `UnityIapAdapter`) are written behind `RIPTIDE_ADMOB`/`RIPTIDE_FIREBASE`/`RIPTIDE_IAP` defines · the SDKs are not installed and need Nick's app IDs/credentials; the contract's acceptance is explicitly fakes-based, and the adapters compile+verify when the SDKs land (gate 4 prerequisite).
- 2026-06-11 · "none before level 8" (§6) = interstitials unlock once 8 voyage levels are completed · §6 names a level without defining the predicate; completion count is robust to replays.
- 2026-06-11 · interstitial cap numbers live in economy.json `ads` (minLevelCompletions 8, minGapSeconds 150, maxPerDay 6) · §6 values under rule 7; cap STATE (last-shown time, daily count) lives in the save.
- 2026-06-11 · save schema bumps to v2 (adds interstitial cap state); v1 files migrate with defaults — the committed v1 fixture now exercises the 6D migration harness for real · adding required fields to v1 would have orphaned every existing save.
- 2026-06-11 · rewarded payouts go through a `RewardedGate` latch: one payout per show, duplicate/late SDK reward callbacks ignored · the Star Ladder audit regression (§6/§7B "each pays exactly once").
- 2026-06-11 · every ad-SDK callback is marshaled through a main-thread dispatcher before touching game state · the Star Ladder threading audit fix (§6); regression-tested by firing fake callbacks from worker threads.
- 2026-06-11 · free-via-ad boosters (§5.3: Drain Pump and New Tide, one each per game) are per-run flags in GameFlow, not save state · "per game" scope dies with the run.
- 2026-06-11 · the §8.5 event list is encoded as verbatim constants with a pinning test; the 7D "debug screen" is the dev-build overlay's event tail (last 20, toggle E) · a dev surface, not a player screen.

## Phase 8

- 2026-06-11 · **VISUAL GATE 4: OPEN** — SDKs not yet installed by Nick; the fakes path is what "go" continued from · gates 1–4 ride on gate 5, which is the ship gate and cannot be deferred.
- 2026-06-11 · all 12 SFX + 2 music loops are procedurally synthesized in code (sine/noise envelopes) — no third-party audio · zero licensing burden, fits the no-authored-assets pattern; `Assets/Audio/CREDITS.md` documents provenance; swap for licensed audio is a data change later.
- 2026-06-11 · haptics v1 = `Handheld.Vibrate()` uniformly for light/medium/heavy on Android · intensity tiers need a native plugin (out of v1 scope); hooks are tiered in code so the upgrade is a one-class swap.
- 2026-06-11 · tutorial levels: the generator special-cases z1 L1–5 to the GDD 9 authored arc (rows-1 intro, rows-2, first rescue w/ preset creature, drown-threat level with a free tutorial Drain Pump, full-rules level) · the generated zone-1 recipe was contractually easy but pedagogically empty; pars still bot-computed.
- 2026-06-11 · the tutorial's free Drain Pump is a flow grant (no ad, no coins, analytics source "tutorial") consumed on use · GDD 9: "first real drown threat + free Drain Pump demo".
- 2026-06-11 · monkey test = 50 full runs through the real app stack (flow, screens, views rendering, store dispatch, random boosters) with instant animations; Unity's test runner fails on any exception/error log · "drives the real UI" interpreted as the full live stack; synthetic pointer-event injection would test the OS input path, not the game.
- 2026-06-11 · Android build: IL2CPP + ARM64, minSdk 24, **targetSdk = Auto (highest installed)** — Play's current target-API floor must be confirmed by Nick at upload ("verify, don't assume", and I cannot browse) · bundle id placeholder `com.riptide.game` and 0.1.0/code 1; the AAB is debug-signed — Play upload signing is Nick's keystore step.
- 2026-06-11 · app icon = generated wave-glyph PNG written into Assets and assigned via the build script (the "icon hook"); replaced by branded art whenever it exists · same provenance rule as audio.

## UI Spec v1.0 (arrived post-Phase-8; staged rebuild per its own build plan)

- 2026-06-11 · `RIPTIDE_UI_SPEC.md` adopted as the UI contract; work proceeds per its slices (4-UI-a/b/c, 5-UI-a, 8-UI) with gates A/B/C · it supersedes the Phase 4–5 placeholder UI where they conflict; the sim, modes, economy, and monetization layers are untouched.
- 2026-06-11 · **FLAG FOR NICK** — spec §4.4 "Continue offer … (GDD §5)": the GDD defines no continue mechanic in §5 or anywhere · not implementing until ruled (it's a monetization mechanic, not an aesthetic call); everything else in §4.4 proceeds.
- 2026-06-11 · **FLAG FOR NICK** — spec §1.2/§2 typography requires Rungo SDF assets; the Rungo TTFs are still not in the repo (flagged since Phase 5) · TMP migration proceeds with a builtin-font SDF placeholder wired through the type tokens; the swap is one asset set when the fonts land. ⚑1 (UGUI confirm) also stands open.
- 2026-06-11 · spec §2 "each screen is a prefab": prefabs are **generated by editor scripts** (PrefabUtility from theme-driven builders), never hand-authored YAML · honors the prefab contract and the zero-literal rule while keeping the no-hand-authored-assets provability that every prior phase relied on.
- 2026-06-11 · ui_theme.json lives at `Assets/Resources/Content/ui_theme.json` (the spec's `Assets/Content/` path predates the Phase-4 Resources move) · runtime loading parity with all other content.
- 2026-06-11 · spec §2 sprite atlases "SVG-derived": no SVG sources exist; generated sprites remain the placeholders inside the atlas structure · provenance consistent with audio/icon.
- 2026-06-11 · TideMeterRing per spec §5.2 (filled ring + wave glyph + remaining-count numeral, cyan→amber→danger steps) replaces the Phase-4 dot ring · the spec is explicit; the dot ring retires.
- 2026-06-11 · spec accessibility/contrast/luminance checks implemented as headless Core tests over ui_theme.json (contrast ratios, block-luminance steps, duration table) · "script-verify" made into gate-blocking tests.
- 2026-06-11 · **FLAG FOR NICK** — the §8 audit found the spec's own palette violating its own rule: block.coralPink → block.teal adjacent luminance ratio measures **1.066** vs the required ≥1.15 (and that pair is the classic deuteranopia collision) · all text-contrast pairs PASS; the block check warns (floor 1.05 enforced) pending your ruling: brighten coralPink / darken teal / or relax the threshold.
- 2026-06-11 · 4-UI-b verification round, three fixes the editor compile/test loop forced: (1) `Riptide.UI.asmdef` +`Unity.TextMeshPro` (TMP types live in their own assembly — CS0246); (2) `Riptide.EditorAutomation.asmdef` +`Riptide.Core` (AutoPrefabGen touches `UiTheme` through `ThemeRuntime.Theme` — CS0012); while either assembly failed, Unity blocked every domain reload, which is why earlier EditMode runs reported a stale 173 · with both fixed: EditMode 178/178 == dotnet shim, PlayMode 27/27, 13 prefabs generated, 0 touch-target violations.
- 2026-06-11 · **TMP Essential Resources imported** (39 assets under `Assets/TextMesh Pro/`, extracted from the com.unity.ugui package's own .unitypackage) · TMP is unusable without its settings asset — `TMP_Settings.defaultFontAsset` dereferences a null instance (NRE'd every text-creating test and prefab gen); this is the standard Unity setup step, and LiberationSans SDF is now the live placeholder ahead of Rungo. `UiText` additionally guards the missing-settings case so the font fallback chain can't throw.
- 2026-06-11 · `easeOutQuart` re-keyed in ui_theme.json: a single Hermite segment with outTangent 3.6 overshoots ~1.005 then dips (monotonicity test caught it — tangents must stay ≤3× the segment secant, Fritsch–Carlson) · now 3 keyframes sampled from the true 1−(1−t)⁴ with the middle outTangent clamped to the monotone bound; shape preserved, reversal impossible.
- 2026-06-11 · **FLAG FOR NICK** — danger threshold conflict: GDD 7.2/7.3 sets the danger experience (tense music, warning tone, foam pulse) at water ≥ 7, while UI-spec §5.1/§4.3 says drownLevel−2 (= 8 with drown at 10) · GDD is law → 7 stands everywhere, centralized in `DangerRule` (one constant feeds water tint, flood line, foam, music); flip the constant if you rule for the spec.
- 2026-06-11 · 4-UI-c keeps the world-space sprite board (UGUI canvas stays for HUD/screens): spec ref-px measurements convert through `ThemeRuntime.WorldFromRefPx` (1080-wide reference §2, gutter from theme, 9 columns) · ring 132px, gauge 12px, water amplitudes etc. all derive from it.
- 2026-06-11 · §5.1's physical constants (12px/9s + 8px/6s sine layers, 6px overshoot, ≤16 droplets, caustic 12%/16s) live as named consts citing the spec — the theme token tables stay color/type/spacing/radius/motion · durations that ARE motion (t.resolveBudget 950, t.dangerFade 800, t.shimmerCooldown 2000) were added as tokens instead.
- 2026-06-11 · §6.1 budget precedence: when clear+drain+rise would exceed t.resolveBudget, the 30ms/cell pop stagger compresses (to zero if needed) and the clear beat's BLOCKING time clamps to the budget remainder — pop visuals bleed into the drain beat (§1.4 overlap grammar) · GDD-locked t.drain/t.drainMulti/t.rise are never shortened; worst case lands exactly on 950ms.
- 2026-06-11 · flood-line marker: LiberationSans SDF has no U+26A0 ⚠ → danger dot + "!" placeholder (same provenance rule as all generated art); swaps with the branded glyph set/Rungo.
- 2026-06-11 · juice routing: AnimationDriver fires named beats (juice-table keys) through UiEventQueue; JuiceDirector maps beat → (SFX, haptic) from ui_theme.json with the GDD 7.2 pitch grammar; AudioDirector shrank to music layers + danger-warning tone + drown muffle · one event path, no double-wiring, InstantMode (tests) stays silent.
- 2026-06-11 · HUD §4.3: layout pass only this slice (goals left / score center / pause slot right, coin token color, booster rail moved right-aligned above the tray) · the TMP/component re-skin and the real Pause sheet belong to 5-UI-a — until then the top-right button keeps its Home action.
- 2026-06-11 · TideMeterRing landed per §5.2 (filled 60-segment ring, wave glyph + remaining numeral in micro type, cyan→amber@2→danger@1 with single entry pulse + light haptic, rise flash-and-empty, drain never touches it); tray slots shifted right (`TraySlotCenter`) so the ring owns the card's left end · the §5.2 grandma-test acceptance is Gate A's on-device item.
