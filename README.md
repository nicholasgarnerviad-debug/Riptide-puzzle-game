# Riptide

A portrait Android block puzzle where the ocean is the clock: place pieces, clear rows,
and keep your board above a tide that rises every few moves. Block Blast pacing meets a
drowning timer — clear rows to drain the water, rescue trapped sea creatures, and survive.

**Status:** all code phases complete (sim, modes, economy, monetization seams, full UI).
On-device visual gates, store SDKs, brand fonts, and Play signing are pending — see
`docs/DECISIONS.md` for every open item and every judgment call made along the way.

## Game modes

| Mode | Shape |
|---|---|
| **Voyage** | 200 hand-tuned levels across 10 zones, star ratings, goals (rescue / rows / tides / score) |
| **Endless Tide** | survival with escalating tide pressure, personal-best chase |
| **Daily Riptide** | one date-seeded board for everyone, one attempt (one rescue retry), emoji share card, streaks |

## Requirements

- **Unity 6000.4.6f1** (2D URP, Android module) — open the project, open `SampleScene`, press **Play**.
  The entire game builds itself from code at runtime; the scene file is intentionally empty,
  and the editor preview wants the **Game** view (not the Device Simulator) on a portrait aspect.
- **.NET SDK** (net10.0) for the headless test pipeline.
- No third-party assets: every sprite, sound, and font fallback is generated or OS-stock.

## Architecture

```
Assets/Scripts/Core   pure C# simulation — ZERO UnityEngine/IO/clock/System.Random
Assets/Scripts/Game   orchestration: flow, meta/save, monetization seams, analytics
Assets/Scripts/UI     all presentation: views, screens, theme system, juice
Assets/Resources/Content   every balance number lives in JSON, none in C#
Tools/                dotnet mirrors: test shim, content gate, balance/level generators
docs/                 the contracts (GDD, master prompt, UI spec) + DECISIONS.md log
```

Non-negotiables the codebase is built around:

- **Determinism** — xorshift128+ RNG inside `GameState`; identical seed ⇒ identical game,
  pinned by golden-file tests. The daily seed derives from the calendar date.
- **Pure engine** — immutable `GameState`; `SimEngine.ApplyMove(state, move)` returns the
  next state plus `MoveEvents`. Views render state and animate events; they never re-derive rules.
- **Data-driven** — piece weights, scoring, escalation, economy, creatures, levels, the whole
  UI theme (colors/type/motion/juice) load from `Assets/Resources/Content/*.json`.
- **Two test pipelines** — the same Core sources compile under dotnet (`Tools/CoreTests`) and
  run in Unity's Test Runner; divergence between Mono and CoreCLR has caught real bugs.

## Testing

```bash
./run_all_tests.sh   # 3 gates: Core purity grep → dotnet test (180) → content validation
```

The content gate also runs the accessibility audits: WCAG text contrast, block-palette
luminance steps (≥1.15), and deuteranopia/protanopia palette-distance simulation.

Unity-side suites (EditMode mirrors the 180; PlayMode adds ~42 integration tests: board
acceptance, flow smoke, monetization threading, monkey runs, event-queue ordering,
navigation matrix, strings coverage, real-click regression, perf budgets) run from the
Test Runner window — or headlessly via the trigger files below.

## Editor automation (file triggers)

The repo includes an in-editor automation layer (`Assets/Editor/RiptideAutomation`) that
polls `Temp/` so external tooling can drive a machine where auto-refresh is off:

| Drop file | Effect | Result file |
|---|---|---|
| `Temp/riptide_refresh.txt` | AssetDatabase refresh + recompile | — |
| `Temp/riptide_run_tests.txt` (`EditMode`/`PlayMode`) | run a test suite | `riptide_test_results.txt` |
| `Temp/riptide_play.txt` | enter Play (portrait Game view, boot report) | `riptide_play_result.txt` |
| `Temp/riptide_uistate.txt` | re-dump runtime state while playing | `riptide_play_result.txt` |
| `Temp/riptide_genprefabs.txt` | regenerate UI component prefabs + touch audit | `riptide_prefabs_result.txt` |
| `Temp/riptide_build.txt` (`android`) | build the AAB | `riptide_build_result.txt` |

## Building

Android App Bundle (IL2CPP/ARM64) via the trigger above or `Riptide` menu in the editor;
output lands in `Builds/` (gitignored). Upload signing, target-API confirmation, and the
ad/analytics/IAP SDK installs (`RIPTIDE_ADMOB` / `RIPTIDE_FIREBASE` / `RIPTIDE_IAP`
scripting defines activate the prewired adapters) are release-owner steps.

## The contracts

Development is governed by three documents in `docs/` — the **GDD** (game rules, the law),
the **master prompt** (phase plan and acceptance gates), and the **UI spec** (design tokens,
components, screens). Every deviation, conflict ruling, and judgment call is logged with
rationale in **`docs/DECISIONS.md`** — if something looks surprising, the explanation is there.
