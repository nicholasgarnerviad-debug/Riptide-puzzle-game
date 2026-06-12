# Riptide

A portrait Android block puzzle where the ocean is the clock: place pieces, clear rows,
and keep your board above a tide that rises every few moves. Block Blast pacing meets a
drowning timer — clear rows to drain the water, rescue trapped sea creatures, and survive.

**Status:** all code phases complete (sim, modes, economy, monetization seams, full UI),
plus the post-gate product passes: universal screen fit (iPhone 16 Pro Max 19.5:9 basis,
camera + canvas solved for every portrait phone), mid-run save & resume (kill the app,
resume the exact board), and a full visual overhaul of every screen — glossy beveled
blocks, god-ray atmosphere, gradient capsule CTAs, icon navigation, real settings
switches, a merchandised shop, and a Tidepool diorama with an actual seabed.
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
  The entire game builds itself from code at runtime; the scene file is intentionally empty.
  Preview in a portrait **Game** view or the **Device Simulator** (layout adapts to any
  portrait device); note the Simulator only routes input while it is the focused view.
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
./run_all_tests.sh   # 3 gates: Core purity grep → dotnet test (218) → content validation
```

The content gate also runs the accessibility audits: WCAG text contrast, block-palette
luminance steps (≥1.15), and deuteranopia/protanopia palette-distance simulation.

Unity-side suites (EditMode mirrors the 218; PlayMode adds 57 integration tests: board
acceptance, flow smoke, monetization threading, monkey runs, event-queue ordering,
navigation matrix, strings coverage, real-click regression, perf budgets, kill-and-resume
round trips, safe-area math) run from the Test Runner window — or headlessly via the
trigger files below.

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
components, screens — §12 holds the universal-fit and genre-pass amendments). Feature
designs get their own docs (**`docs/SAVE_RESUME_DESIGN.md`** for mid-run resume). Every
deviation, conflict ruling, and judgment call is logged with rationale in
**`docs/DECISIONS.md`** — if something looks surprising, the explanation is there.
The product backlog lives in **`docs/ROADMAP.md`**.

## Working on this repo with an AI agent

This game was built by an AI agent operating under the contracts above. These meta-prompts
keep any future session (Claude Code or otherwise) productive instead of destructive.

### Ground rules — paste at the start of any session

```
You are working on Riptide. Non-negotiables:
1. docs/RIPTIDE_GDD.md is law for game rules; docs/RIPTIDE_UI_SPEC.md for UI. If a change
   conflicts with either, STOP and flag it — log rulings in docs/DECISIONS.md with rationale.
2. Riptide.Core stays pure: no UnityEngine/UnityEditor/System.Random/DateTime.Now/IO.
   The purity grep in run_all_tests.sh enforces this; never weaken it.
3. Determinism is sacred: same seed = same game. Golden-file tests pin the RNG; if a change
   breaks a golden, STOP and explain instead of regenerating it.
4. Balance numbers live in Assets/Resources/Content/*.json, never in C#. UI colors,
   durations and easings come from ui_theme.json tokens.
5. Before claiming anything works: ./run_all_tests.sh green, then Unity EditMode AND
   PlayMode green via the Temp/ trigger files. Never claim visuals look right — request a
   screenshot and let a human judge.
6. One commit per coherent change, message tagged ([feature]/[fix]/[balance]/…), test
   counts in the message. No Co-Authored-By trailers.
```

### Task templates

**New feature** — *"Act as lead dev. Plan `<feature>` first: which contract section covers
it (cite it), what Core/Game/UI layers it touches, what new tests prove it. If it changes
game rules, write the ruling into DECISIONS.md before code. Implement Core-first with dotnet
tests, then flow, then UI. Full gates + both Unity suites green, then commit."*

**Bug fix** — *"Reproduce `<bug>` as a failing test FIRST (the suite that should have
caught it — if no suite could, say why and add the missing harness). Fix, prove the test
flips green, run everything, commit with the root cause in the message."*

**Balance change** — *"Touch only Content JSON. Re-run Tools/BalanceRunner and report the
medians/death-mix against GDD §4 targets before and after. If targets move, that's a GDD
ruling — flag it."*

**UI / visual pass** — *"All styling through ui_theme.json tokens and the UiComponents
builders — no literals, 9-sliced corners, ≥120 ref-px touch targets (the audit enforces).
After compiling, enter play via Temp/riptide_play.txt and send a screenshot for human
judgment; do not self-certify appearance."*

**Release prep** — *"Run the full matrix: 3 gates, EditMode, PlayMode, AAB build trigger.
Reconcile docs/DECISIONS.md open flags. Anything requiring human hands (device gates,
signing, SDK installs) goes in the report, not under the rug."*

### What an agent should read first

`docs/DECISIONS.md` (the why behind everything) → `docs/ROADMAP.md` (what's next) →
this file's automation table (how to drive the editor). The Unity editor on the dev
machine has Auto Refresh disabled and its update loop parks when unfocused — drop the
trigger file, then focus the editor window, and verify compiles by `Library/ScriptAssemblies`
timestamps rather than trusting silence.
