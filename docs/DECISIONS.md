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
