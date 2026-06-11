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
