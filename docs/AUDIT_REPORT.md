# Riptide v1.0 — Pre-Release Audit Report
**Date:** 2026-06-13 · **Auditor:** lead-dev agent session · **Repo:** `Riptide-puzzle-game@main`
**Scope:** full audit per the v1.0 audit meta-prompt — work / make-sense / feel-finished, zero trust in prior phase claims.

---

## 1. Executive verdict — **FIX-COMPLETE, SHIP AFTER HUMAN GATES**

Engine, determinism, persistence, economy, balance, and content all re-verified green; three real
defects were found and fixed this pass (one exploit, one perf-budget breach, dead content), none
of them blockers in kind. A pure "SHIP" is not available to an agent on this repo by construction:
**eight visual gates have never been signed by human eyes**, and the entire SDK surface is fakes.
The single load-bearing dependency is `docs/HUMAN_GATE_CHECKLIST.md` — one ~30-minute on-device
playthrough — followed by Nick's SDK/signing pass (inventory in §6).

## 2. Baseline → final test counts (Task 0 vs post-fix)

| Pipeline | Baseline | Final |
|---|---|---|
| `run_all_tests.sh` gate 1 (purity grep) | green | green |
| gate 2 dotnet shim (CoreCLR) | 218/218 | **219/219** (+FuzzPinTests) |
| gate 3 ContentCheck | all OK (38 colors, 200 levels, CVD 0.132/0.160) | all OK (strings pruned) |
| Unity EditMode (Mono) | 218/218 | **219/219** |
| Unity PlayMode | 57/57 | **59/59** (+ClockHostility, +pending-offer resume) |
| Boot probe | errors=0, Home, input armed | unchanged |

## 3. Determinism (B1) — the centerpiece, from zero trust

- **Fuzz:** `Tools/DeterminismFuzz` (new) — **10,000 games, 318,835 moves**, random seeds, random
  legal placements **plus all four boosters and drown-continues**, split across Endless and Daily
  configs. Each game **replayed twice with per-move StateHash comparison: 0 divergences,
  0 illegal-move findings** (0.6s wall). Re-run after every logic fix this pass — still 0.
- **Dual-pipeline:** 32 fuzz games pinned to `CrossPipelineFuzzPins.cs` (exact economy.json
  embedded) and replayed by `FuzzPinTests` under **both** runtimes — dotnet CoreCLR and Unity Mono
  agree hash-for-hash. Goldens untouched and green throughout.
- Usual suspects re-checked: purity grep green (no System.Random/DateTime in Core); water math is
  `int` end-to-end; refill-guarantee redraws covered by `RefillGuarantee_IsDeterministic` + the
  10k adversarial board test + every fuzz replay crossing refill boundaries.

## 4. Findings — fixed this pass

| ID | Finding | Fix | Verification | Commit |
|---|---|---|---|---|
| B5 | **Daily attempt lock exploitable by clock rollback** — `CanAttemptDailyToday` used `!=`, so setting the clock back re-armed the attempt (GDD §3.3 one-per-day broken) | strictly-greater comparison in `MetaServices.cs` | failing test first (`Daily_ClockRollback_DoesNotRearmTheAttempt`, verified RED 58/1 against old code, GREEN 59/59 after) | `f08d7ad` |
| B4 | Hostile kill-point untested: process death with a **pending Continue offer** | coverage added — crafted drowned-run record, resume must re-raise the offer (design §7.1) | `KilledWithPendingContinueOffer_Resume_ReRaisesTheOffer` green | `f08d7ad` |
| C5 | **Per-move GC**: place-settle pop allocated 1–9 coroutines per placement (steady-state path; invisible to the alloc test, which runs InstantMode) | pooled 32-slot struct ticker in `AnimationDriver.Update` | suites green; zero allocations by construction (preallocated array, no boxing) | `f750185` |
| C2 | 10 orphaned string keys shipped (incl. DECISIONS-noted stats.line1/2) | pruned from strings.json + registry | StringsCoverage + ContentCheck green | `f750185` |

Rollover rule (B5, one sentence): **a daily attempt binds to the device-local civil date's
epoch-day and only a strictly LATER day re-arms it; the seed derives from that date, the retry
hook lives and dies with the attempt's day, and streaks advance only forward (+1 consecutive,
freeze absorbs a 1-day gap, otherwise reset) — backward clock movement can never mint attempts.**

## 5. Conformance & coherence (A)

- **A1 rules table:** every GDD §2 rule maps to a passing named test (grid 9×12, rows-only clears,
  no gravity/rotation, drain-before-tick, petrify-on-rise, drown at exactly 10, stuck after
  placement AND refill, refill guarantee ≤5 redraws, creature rules) — see
  `Section2_*`/`Section3_2`/`Section5_3`/`ContinueAndSwapTests` suites; tuning values live in
  `economy.json`/`levels/*.json` and are gate-validated by ContentCheck. **Zero unexplained
  mismatches**; all deviations carry DECISIONS entries (notably: endless tuning supersedes §3.2's
  initial numbers per the §4 bot-tune mandate; danger=7; piece count 20).
- **A2 piece set:** catalog = **20 masks**; the GDD's "18 shapes" header contradicts its own
  enumeration (1+2+2+4+8+1+2 = 20). Already **RULED 20** under delegation (DECISIONS 2026-06-11).
  Recommendation: one-word GDD edit ("18"→"20") at the next contract touch.
- **A3 balance, re-measured (N=10,000, fresh CSVs in docs/balance/):**

| GDD §4 target | Measured | Verdict |
|---|---|---|
| Endless GH median survival 45–70 | **54** (p10 50 · p90 60) | PASS |
| Endless stuck ≤15% of losses (drown dominant) | **14.8%** (drown 85.2%) | PASS |
| Daily GH 20-tide survival 40–60% | **44.0%** | PASS |
| Band 1 GC 3-star ≥80% · Band 10 GH 30–50% | 20/20 · 0.38 (generation-time) | PASS¹ |

  ¹ Fresh endless+daily CSVs are **bit-identical** to the committed tuning evidence, proving the
  engine streams are unchanged by the Continue/Swap/milestone work — the generation-time band
  measurements therefore remain valid. `DailyVerifier`: **60/60** dailies from 2026-06-13 completable.
- **A4 economy:** normal day ≈ 3 levels (~93) + daily (75) + ~2 milestones (30) = **~198/day**,
  inside the 150–250 target; rewarded chests (≤150/day) sit on top as designed engagement income.
  Booster habit (100–150/use) > per-level income ✓ designed pressure. **No softlock:** the
  Continue offer self-suppresses when neither ad nor coins are available; every retry/forward path
  is free. Daily's zero-boosters and no-continue are **sim-enforced** (config + move validation,
  tested), not UI hiding.
- **A5/A6:** modes SOUND — three loss causes distinct in state and copy; M1 funnel, abandon
  semantics, and the navigation matrix (incl. the new Pause→Map and hero Map paths) all
  test-pinned. Stars: ceil(par×1.4) ruled+tested; stars gate nothing; milestone coins pay exactly
  once at `FinishRun` (single wallet write-point — resume cannot double-bank by construction,
  and `GameStore.Restore` never re-fires MoveApplied).

## 6. Release-blocker inventory (E) — between `main` and Play submission

| # | Step | Owner |
|---|---|---|
| 1 | Human gate playthrough (`docs/HUMAN_GATE_CHECKLIST.md`) | **Nick** |
| 2 | Rungo TTFs → SDF assets (one-set swap through the type tokens — wiring verified) | Nick |
| 3 | AdMob/Firebase/IAP SDK installs + define flips; FileProvider manifest (share image) | Nick |
| 4 | Gate 4 on device: consent, test ads, rewarded payout, sandbox purchase | Nick |
| 5 | Iconography/wordmark art session (procedural placeholders ship otherwise) | Nick |
| 6 | Device perf session (§9 of the checklist: 60fps, ≤80 draw calls, ≤2.5s boot) | Nick |
| 7 | Bundle id (placeholder `com.riptide.game`), version, Play signing, target-API confirm | Nick |
| 8 | Post-SDK: re-run both suites + fuzz; extend §8.5 analytics only then (pinned) | agent |

**Define-toggle isolation compile: NOT RUN — honestly.** The adapters reference SDK namespaces
that are deliberately not installed; toggling `RIPTIDE_ADMOB` et al. today fails compile *by
design* (DECISIONS, Phase 7). Verifying adapter compilation requires installing the SDKs, which
this audit is forbidden to do. The adapters' API surfaces were last written against the Phase-7
seam contracts; treat step 3 as including a compile-fix budget.

## 7. Open issues (found, not fixed)

| Severity | Issue | Why deferred |
|---|---|---|
| should-fix | M9 remove-ads upsell visibility condition has no unit test (view-layer expression) | needs a session-interstitial fake fixture; behavior is two booleans — low risk, listed for the SDK pass |
| should-fix | Forward clock-stepping can farm streaks day-by-day | indistinguishable from real days without server time — out of v1 scope (local-only by GDD §8.4) |
| cosmetic | HintBubble 4s ghost replay exists for L1 only (meter/rescue hints caption-only) | ruled adequate 2026-06-11; revisit with gate feedback |
| cosmetic | HUD top bar still legacy `Text` (Rungo/TMP re-skin pending fonts) | blocked on asset delivery |

## 8. Observations (out of scope, not implemented)

- BalanceRunner could grow a `--mode level` to re-measure voyage bands directly from shipped JSONs
  (today band evidence rides on stream-identity + generation-time numbers).
- The chaos fuzz would make a cheap nightly: `dotnet run --project Tools/DeterminismFuzz`.
- Daily attempt lock could later bind to last-server-date for full clock immunity (v1.1, with cloud).

## 9. Honesty section — what this audit could NOT verify

- **Anything visual.** Eight gates of accumulated debt; every "looks right" claim in this repo's
  history is unverified until the checklist run. This is the report's only hard dependency.
- **On-device performance** (60fps/draw calls/cold boot) — editor capture exists
  (`docs/perf/8ui_editor_capture.md`) but the budgets are device measurements.
- **Real SDK behavior** (ads, consent, IAP, notifications) — fakes only; threading/cap/latch logic
  is tested against the seams, the real callbacks are not.
- **AAB size (E4), measured:** fresh IL2CPP/ARM64 build during this audit =
  **36.5 MB** (`Builds/riptide-closed-testing.aab`, 2026-06-12 20:40 local) — unremarkable for a
  Unity 6 URP title with zero external assets; debug-signed and pre-SDK, so indicative only.
- Long-horizon retention/difficulty feel — bots approximate humans; the §4 targets are proxies.

---
*Every FIXED claim above names its verification; fuzz count on record: 10,000×2 replays, 0 divergences, both runtimes.*
