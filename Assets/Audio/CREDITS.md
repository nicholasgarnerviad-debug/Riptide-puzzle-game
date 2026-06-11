# Audio Credits

All 12 sound effects and both music loops in Riptide v1 are **procedurally
synthesized at runtime** by `Assets/Scripts/UI/AudioSynth.cs` (additive sines,
filtered noise, exponential envelopes). No third-party or licensed audio assets
are included in this build.

| Asset | Source |
|---|---|
| Place / Clear / Combo / Drain / Rise / Danger / Rescue / Lost / Drown / Button / Star / Streak | Generated in code (AudioSynth) |
| Calm ambient loop | Generated in code (AudioSynth.CalmLoop) |
| Tense danger loop (water ≥ 7 crossfade) | Generated in code (AudioSynth.TenseLoop) |

When licensed audio replaces these placeholders, list each file, author,
license, and source URL here before shipping (master prompt 8B).
