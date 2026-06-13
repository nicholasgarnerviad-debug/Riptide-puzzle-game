# Riptide — Asset Sourcing & Generation

How Riptide gets its art, audio, and type. The project ships with **zero hand-authored
binary assets**: everything visible is generated in code from theme tokens (DECISIONS.md).
This doc records what's procedural, and curated **free / CC0** sources for the few things
that genuinely want real assets before a polished launch.

---

## 1. Generated in code (no download, no license)

| Asset | Generator | Notes |
|---|---|---|
| Blocks, coral, board wells | `SpriteFactory` | glossy beveled tiles, inset wells — grayscale baked, hue from `block.*` tokens |
| **Creatures (8 species)** | `CreatureSprites` | distinct silhouettes — crab/starfish/seahorse/octopus/turtle/pufferfish/jellyfish/axolotl; tinted by `Palette.CreatureColor` |
| Menu glow, gradients, god rays, icons | `MenuSprites` | soft glow, capsule/panel gradients, 12 geometric icons, dunes/kelp |
| Scene atmosphere | `GameSceneDressing` | surface glow, sunbeams, frame shadow |
| App icon | `AutoBuilder.GenerateIconPng` | wave-glyph placeholder |
| 12 SFX + 2 music loops | `AudioSynth` | sine/noise envelopes (`Assets/Audio/CREDITS.md`) |

These are the v1 shipping assets. The sources below are **optional upgrades** Nick can drop
in later — none are required to ship, and pulling binaries is deliberately left to him
(licensing attribution + the SDF/import steps are editor work).

---

## 2. Type — the Rungo gap

The GDD specifies **Rungo** (a paid font, not in the repo); the build runs on
LiberationSans SDF as a placeholder wired through the type tokens. Free, on-theme
alternatives — bold, rounded, geometric, legible small — all **SIL Open Font License**:

| Font | Why it fits | Source |
|---|---|---|
| **Outfit** | geometric, rounded, very close to Rungo's feel | fonts.google.com/specimen/Outfit |
| **Sora** | technical, clean, "instrument panel" tone | fonts.google.com/specimen/Sora |
| **Baloo 2** | rounded + friendly for the casual side | fonts.google.com/specimen/Baloo+2 |
| **Nunito** | soft geometric, excellent at small sizes | fonts.google.com/specimen/Nunito |

**Install path — now a single file drop:** put one `.ttf`/`.otf` in
`Assets/Resources/Fonts/` (see its README). `UiText.CustomFont` picks it up at runtime,
generates a dynamic TMP SDF, and reskins every text element — TMP screens AND the legacy
HUD — with no Font Asset Creator step. Falls back to LiberationSans until a font is
present, so nothing regresses. The type tokens (`ui_theme.json`) still drive size/weight.

---

## 3. Audio — free CC0 upgrades over the synthesized loops

The synthesized SFX are functional placeholders. For richer water/UI sound, all CC0
(public-domain, no attribution required):

- **Kenney Game Assets** — kenney.nl/assets (Interface Sounds, Impact Sounds, Digital Audio) — CC0
- **Freesound** — freesound.org, filter License = "Creative Commons 0" (search: "water drip", "bubble", "underwater ambience", "whoosh")
- **OpenGameArt** — opengameart.org, filter CC0 (calm ambient loops for the menu/play music)

Wire through `AudioDirector`/`JuiceDirector`'s existing event table — swapping a clip is a
data change, no code.

---

## 4. Sprites/icons — only if replacing the procedural set

The procedural creatures and icons are designed to ship as-is. If Nick wants illustrated art:

- **Kenney "Fish Pack" / "Shape Characters"** — kenney.nl — CC0, flat vector, big-eyed (matches the GDD §7.1 brief almost exactly)
- **Game-icons.net** — 4000+ flat SVG icons (fish, anchor, kelp, compass…) — **CC BY 3.0** (requires a credit line)
- **OpenGameArt** sea-life packs — opengameart.org, CC0 filter

Keep the discipline: tint from theme tokens, not baked-in color, so the bioluminescent
palette stays the single source of truth.

---

## 5. Licensing rule

CC0 / public-domain → no attribution needed (preferred). CC BY → a credit line in
`Assets/Audio/CREDITS.md` (extend it to `CREDITS.md` for art). **Never** commit anything
under a non-commercial or share-alike license — it poisons the closed-track build.
