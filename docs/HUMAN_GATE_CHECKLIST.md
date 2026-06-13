# Riptide — Consolidated Human Gate Checklist
**One guided playthrough (~30 min) that retires every deferred visual gate.**
Compiled by the 2026-06-13 audit from every OPEN gate in `docs/DECISIONS.md`:
Gate 1 (deferred, Phase 4) · Gate 2 (deferred, Phase 5) · Gate 3 (deferred, Phase 7 entry) ·
Gate 4 (open, needs SDKs) · Gate 5/ship (open) · Gate A (deferred, 4-UI-c) · Gate B (deferred, 5-UI-a) ·
Gate C (open, 8-UI) · fit+genre gate (deferred 2026-06-12).

**Setup:** fresh install state (delete the save via Settings→none? use a clean device/profile, or
delete `riptide_save.json` + PlayerPrefs), device in hand — not the editor — for everything except
the Simulator matrix in §8. Mark every box PASS or FAIL; a FAIL needs one line of what you saw.

---

## 1. First run & tutorial (Gates 2, 5 · M1 funnel)
- [ ] Age gate appears first, neutral styling, no game art; Continue dives straight into Zone 1 · Level 1 (DECISIONS: M1 ruling)
- [ ] L1 hint teaches drag ("drag a piece from the tray"); dismissed by DOING it, never by tapping (Gate 5 / §6.3)
- [ ] L1 idle 4s → ghost-hand drag replay appears, any touch cancels it (delegated ruling §6.3)
- [ ] L2 tide-meter hint reads; L3 rescue hint points at the creature; L4 free Drain Pump demo fires; L5 plays clean (GDD §9 tutorial arc)
- [ ] Hints NEVER appear outside the board (regression check for the huh.png bleed — visit Tidepool mid-tutorial)

## 2. Input feel & drag (Gates 1, A · GDD §7.3 items 1–4)
- [ ] Drag: piece lifts ~90px above finger, visible above thumb, scales 1.08
- [ ] Ghost preview: cyan when legal, red-tinted when illegal; ghost matches the EXACT cells the commit lands on (no off-by-one, test deliberately at board edges)
- [ ] Magnetic snap engages within ~0.6 cell and feels helpful, not grabby
- [ ] Release off-board: piece flies back to tray, no penalty, tray ghost restores
- [ ] Almost-full-row shimmer tell fires when your ghost would leave one gap (max once per 2s)
- [ ] One-thumb playthrough: every primary action reachable in the bottom 60% during play (Gate B / §8)

## 3. The tide meter (Gates 1, A — THE named item)
- [ ] **Grandma test:** someone unfamiliar answers "when will the water rise next?" within 30 seconds, unprompted (§5.2 acceptance)
- [ ] Ring fills clockwise per placement; number inside matches placements-remaining
- [ ] Cyan → amber at 2 remaining → red at 1, with one strong pulse entering danger
- [ ] On rise: ring completes, flashes, empties; on drain the RING does not change (water ≠ ring)

## 4. Water: the villain and the spectacle (Gates 1, A, C · §5.1)
- [ ] Rise (350ms): surge up one row with overshoot-and-settle + heavy haptic — unmistakable
- [ ] Drain (450ms): recede + droplets + sparkle + whoosh-glug — feels like RELIEF, clearly different from rise
- [ ] Multi-row drain: stretched recede + screen-edge cyan pulse
- [ ] Submerged rows read "underwater" instantly (caustics + tint); petrified coral visibly DEAD (pocked, desaturated)
- [ ] Danger at water 7/8/9: gradient crossfades to red, foam pulses, flood line at full alpha + pulse, music tensifies — legible at arm's length
- [ ] Smooth surface: the 44-column wave reads as liquid, not a bar chart (2026-06-12 visual pass)

## 5. Game screen composition (fit+genre gate, 2026-06-12)
- [ ] All 9 columns + depth gauge + tide ring fully visible — nothing clipped at screen edges
- [ ] Booster rail sits BELOW the tray, never over the board; coins label beside it
- [ ] Glossy blocks vs inset wells: pieces pop, the empty grid recedes
- [ ] Place a piece: settle-pop reads as the thunk; praise text on a 2-row clear ("DOUBLE CLEAR"), "RIPTIDE!" at 4
- [ ] Combo chip beside the score appears at chain ×2+; Endless: best-score chip top-left flips gold when beaten
- [ ] Tray pieces readable at a glance at the 0.42 scale

## 6. Full flow loop (Gates 2, B)
- [ ] Home → Voyage map ("Map ›" link AND card tap both work) → play L1–L3 → Results → Next
- [ ] Lose by DROWN: water-tint intro on results — then lose by STUCK: muted flash intro. **The two deaths must feel different** (§4.4)
- [ ] Lose a rescue creature: distinct "friend was lost" copy
- [ ] Endless run → milestone pop at 5 tides → die → NEW BEST banner when earned → Results → Play again
- [ ] Daily: intro ritual (sun, date, streak, Dive) → play → results with tide pips → Share → emoji card with SQUARE water bar (M7) lands in clipboard/share sheet
- [ ] Pause: resume / Level map / sound / quit-to-home all work; Android back = pause in game, back-nav in menus, never traps
- [ ] Kill the app mid-run (swipe away) → relaunch → Resume sheet shows the right mode/progress → Resume lands on the EXACT board → Abandon discards
- [ ] Backgrounding mid-run → return → Pause sheet (never raw board)

## 7. Meta screens (Gates 3, C + 2026-06-12 passes)
- [ ] Home: hero card, daily/endless two-up, icon bar — reads like a finished game at a glance
- [ ] Tidepool: dunes/kelp diorama scrolls with parallax; creatures bob in wells; locked = "???" silhouettes; tap → info card with flavor cycling; Edit mode places/removes decorations at slots; buying a decoration deducts coins
- [ ] Shop: hero (price on the button only), packs with BEST VALUE badge, coin pill; Remove Ads (fake) flips to "Owned" + thanks toast
- [ ] Settings: switches feel like switches; stat tiles correct vs your actual progress; links work
- [ ] Zone map: zone names + star counters; current level glows; locked nodes dead; completed show stars
- [ ] Marine snow/rays present but never distracting; reduced-motion toggle visibly calms everything incl. water (§1.4)

## 8. Fit matrix (fit gate — Device Simulator, then your device)
For each of iPhone 16/13 Pro Max (19.5:9), Pixel-class 20:9, any 21:9, any 16:9/tablet preset:
- [ ] Board + gauge + ring fully on-screen; HUD inside the notch-safe area; tray in thumb zone
- [ ] Menus: nothing under notches/punch-holes; backdrop extends edge-to-edge behind them
- [ ] Simulator actually renders & clicks (SafeArea cache-key regression, dont-fit.png)

## 9. Performance on device (Gates 1, C · §9 — needs your hardware)
- [ ] 60fps sustained during drag (the gesture must never hitch)
- [ ] 60fps through the worst case: 4-row deep-clear + drain + rise queued
- [ ] Cold boot to Home ≤ 2.5s
- [ ] Draw calls ≤ 80 in game scene (Profiler/Frame Debugger session)
- [ ] No GC hitches in steady play (Profiler GC alloc view during ~50 placements)

## 10. Audio & haptics (Gate 5 · §7.2)
- [ ] 12 SFX present and proportionate (place/clear/drain/rise/rescue/lost/drown/buttons/star/streak)
- [ ] Music: calm loop in menus+play; tension layer crossfades in at water ≥ 7; drown muffles before results
- [ ] Haptics: light place / medium clear / heavy rise; haptics toggle kills them all

## 11. Blocked items (cannot be checked until Nick's SDK/release pass — listed so nothing is silently lost)
- Gate 4 (entirely): UMP consent flow, real test ads, rewarded payout on device, sandbox purchase/restore — needs `RIPTIDE_ADMOB`/`RIPTIDE_IAP` + accounts
- Share IMAGE attach (FileProvider manifest); notification pings (`RIPTIDE_NOTIFICATIONS`)
- Rungo typography + real iconography review; final store-listing screenshots
- Play signing, target-API confirm, closed-track upload

---
**Verdict line (fill after the run):** GATES PASSED ☐ / FAILURES LOGGED ☐ — date: ______
