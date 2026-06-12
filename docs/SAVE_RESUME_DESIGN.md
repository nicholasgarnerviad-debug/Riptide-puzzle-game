# Mid-run Save & Resume — Design (ROADMAP deferred #1)

**Status: APPROVED (Nick, "resume aswell", 2026-06-12) — implemented same day; deltas logged in DECISIONS.md.**
Companion to RIPTIDE_GDD.md §8.2/§8.4 and ROADMAP.md ("replay the move list against the
seed; design doc first — interrupt points, version tolerance").

## 1. Goal & non-goals

**Goal:** a run interrupted by process death (OS kill, crash, reboot) resumes exactly
where it stopped — same board, same tray, same water, same score — in all three modes.
Today a backgrounded-but-alive app resumes via the Pause sheet; a killed process loses
the run entirely. For Daily that burns the day's attempt: the worst UX failure the
game currently has.

**Non-goals:** cloud sync (v1.1), resuming an explicitly abandoned run (quit-to-home
keeps its abandon semantics), multiple parked runs (one pending run, ever), replay
sharing/spectating (the format enables it later; not built now).

## 2. Core principle — record the inputs, replay the engine

GDD §8.2: a full game = `(levelConfig identity, seed, List<Move>)`. The engine is
deterministic (rule 5, golden-pinned), so we persist **inputs, never state snapshots**:
no risk of a snapshot disagreeing with the rules, and the 190-test sim suite is the
correctness proof. Resume = rebuild the config, `GameState.NewGame(config, seed)`,
re-apply every recorded move through `SimEngine.ApplyMove`, render the final state
instantly (InstantMode-style — no replayed juice).

## 3. Data model — `RunRecord` (pure Core)

```
{
  "schema": 1,
  "mode": "Voyage" | "Endless" | "Daily",
  "zone": 3, "level": 4,            // Voyage only — config rebuilt from content
  "epochDay": 20618,                 // Daily only — date lock + seed derivation
  "seed": 123456789,                 // flow.CurrentSeed at BeginRun
  "moves": [                         // ordered, complete, includes boosters/continue
    { "t": "place", "slot": 1, "x": 4, "y": 2 },
    { "t": "drain" }, { "t": "pop", "x": 0, "y": 1 }, { "t": "newTide" },
    { "t": "swap", "slot": 2 }, { "t": "continue" }
  ],
  "stateHash": "…"                   // StateHash AFTER the last recorded move
}
```

- Serializer/parser pure Core next to `SaveData` (same hand-rolled JSON discipline,
  loud-fail parse, dotnet-testable). One new file: `Core/Meta/RunRecord.cs`.
- Lives in its OWN file `riptide_run.json` at persistentDataPath (atomic temp-swap,
  same wrapper as the save): a corrupt run file must never threaten the main save,
  and per-move writes must not rewrite meta.
- `stateHash` note: `ContinueUsed` is hash-excluded (documented 2026-06-11), but the
  move list *contains* the ContinueMove, so the replayed state reproduces it exactly —
  the hash check stays a valid divergence guard.

## 4. Lifecycle (interrupt points)

**Record:** a `RunRecorder` (Game layer) subscribes to `GameStore.MoveApplied` at
`BeginRun`. After EVERY applied move it appends to the in-memory record and writes the
file **in the same frame** (atomic swap; ~1–3 KB typical, trivial IO). Writing on
every move (not on OnApplicationPause) is the design: OnApplicationPause never fires
on OS kill-from-recents on some Android versions, and the booster wallet spend happens
at dispatch time — persisting in the same frame closes the spend-then-lose-the-move
window to near zero.

**Clear:** the file is deleted when (a) `FinishRun` processes a terminal state — the
outcome path (coins, bests, streaks) must run exactly once and already has; (b) the
player quits-to-home from Pause (abandon semantics keep meaning abandoned — GDD has
no resume-after-quit); (c) a new run starts (one pending run, ever).

**Detect:** at app boot, after meta load, `GameFlow` checks for a valid pending record.

## 5. Resume flow (UI)

- Boot with a pending run → Home shows a **Resume sheet** (existing Sheet component):
  title `resume.title`, body "Zone 3 · Level 4 — 12 moves in" / "Endless — tide 6" /
  "Daily Riptide #N", ButtonPrimary `resume.continue`, ButtonGhost `resume.abandon`.
- Resume → rebuild config (Voyage: zone/level from content; Endless: ModeFactory;
  Daily: date-derived config), seed from record, replay, enter Playing. The HUD,
  water, meter, chrome all render from the replayed state — no new view code.
- Abandon → delete file; for Voyage/Endless nothing else (no fail recorded — the run
  simply never concluded, same as today's process death); Daily: the attempt stays
  consumed (it was consumed at StartDaily — abandoning a daily mid-run already costs
  the attempt today; resume only ever *improves* on the status quo).
- First-run FTUE (M1) takes priority: a virgin profile can't have a pending run anyway.

## 6. Version tolerance & divergence policy

Replay can diverge if an app update changed content (level JSON, weights, engine fix).
Policy: after replaying all moves, compare `StateHash(replayed)` to the recorded
`stateHash`. On ANY mismatch — or parse failure, unknown move type, schema > known,
illegal move mid-replay, Daily `epochDay` ≠ today — **discard silently-gracefully**:
delete the file, show toast `resume.expired`, land on Home. Never crash, never a
corrupted half-run, never block the player. (Daily + date mismatch: the attempt was
already consumed that day; no restoration — a stale daily record is just gone.)
A discarded record logs `resume_discarded {reason}` (analytics seam) so divergence
in the wild is visible telemetry, mirroring §8.5's corruption event pattern.

## 7. Edge cases (ruled here, logged in DECISIONS on implementation)

1. **Continue offer pending at kill** (drowned, sheet up, neither paid nor declined):
   the recorded state is terminal-drowned with `ContinueUsed == false` → resume
   re-enters Playing at that state and the existing `ScreenManager.Update` settle
   logic re-raises the offer naturally. No special casing.
2. **Terminal state at kill** (died between terminal move and FinishRun): replay ends
   terminal → route straight to `FinishRun` on resume; outcome pays once, file clears.
3. **Rewarded-ad booster mid-flight at kill**: the reward only dispatches a move on
   the SDK callback; no move ⇒ no record entry ⇒ nothing owed either way (the free
   booster per-run flags are flow state and reset with the rebuilt run — player may
   get a second free-booster chance; player-favorable, accepted).
4. **Tutorial run (z1 L1–5)**: resumes like any Voyage level; hints re-anchor from
   the current step's event triggers (TutorialDirector is state-driven).
5. **Settings/Shop visited mid-run** (Pause → settings): run stays live today via the
   stack; unchanged — the record only matters on process death.

## 8. Test plan

**Core (dotnet + EditMode):** RunRecord serializer round-trip for every move type ·
parse failures throw with context · replay-rebuild equals original `StateHash` for
seeded random games across all three mode configs (property test, N=200) · hash
mismatch / bad schema / wrong day all yield `Discard` with the right reason ·
replayed-prefix determinism with boosters and a continue in the list.

**PlayMode:** start voyage run → apply 10 bot moves → destroy app root → `CreateApp`
again → resume sheet present → accept → `StateHash` equals pre-kill hash · decline
→ file gone, Home normal · daily resume keeps the attempt lock and the same seed ·
quit-to-home leaves NO pending record · finished run leaves NO pending record ·
monkey-test extension: random kill/resume injections across 10 runs, zero exceptions.

## 9. Implementation slices (each ends green, committed, ~3 commits)

1. **[resume-a]** Core: `RunRecord` + serializer + `RunReplay.Validate/Rebuild`
   (pure) + full Core test set.
2. **[resume-b]** Game: `RunRecorder` service (subscribe/write/clear), `GameFlow`
   pending-run detection + `ResumeRun()/AbandonPendingRun()` API, wallet-ordering
   note verified in code; PlayMode kill/resume tests.
3. **[resume-c]** UI: Resume sheet + Home wiring + `resume.*` strings (+ registry,
   coverage-tested) + toast; 🎯 gate item: resume feel on device (cold-boot speed
   with a 100-move endless record — replay is pure compute, expected ≪ 1 frame).

## 10. Open question for Nick (non-blocking, default chosen)

Resume PROMPT vs AUTO-resume: this design prompts (a sheet) because silently dropping
a player into a half-drowned board they forgot is hostile; Block Blast auto-resumes
its endless board, but its board carries no mid-run goals. Default = prompt. Say the
word if you want auto-resume for Endless specifically.
