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
