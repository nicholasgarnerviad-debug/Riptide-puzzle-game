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
