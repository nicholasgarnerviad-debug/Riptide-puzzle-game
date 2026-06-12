# 8-UI editor perf capture (game scene: board+chrome+water+ring+snow)

- captured: 2026-06-11, in-editor play-mode test (seed 3)
- SetPass calls: 6 (material switches; the in-editor batching signal)
- Draw calls: 0 — this recorder may not populate in editor play mode
- Spec §9 budget: ≤80 draw calls ON DEVICE — editor numbers include scene/editor
  overhead and are NOT the device measurement; the device capture is a Gate C item
  (profiler attach on the §10 test phone, flagged in DECISIONS.md).
- Zero-alloc beat routing: covered by JuiceDirector_BeatRouting_DoesNotAllocate.
- Cold boot ≤2.5s: device item (Gate C).
