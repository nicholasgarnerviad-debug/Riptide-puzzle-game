# Game font

The shipping typeface is **Fredoka** (SIL Open Font License 1.1 — `OFL.txt` in this folder,
which must ship with the fonts). It is bundled as three static weights cut from the
google/fonts variable font:

| File | §1.2 weight token |
|---|---|
| `Fredoka-Medium.ttf` | Medium (caption) |
| `Fredoka-SemiBold.ttf` | SemiBold (body) |
| `Fredoka-Bold.ttf` | Bold (display, title, heading, micro, score) |

The game picks fonts up at runtime (`UiText.FontFor`): every `.ttf`/`.otf` in this folder
becomes a dynamic TMP SDF, and a file named `Family-Weight.ttf` serves that weight token
(missing weights fall back to the nearest available one; glyphs the font lacks fall back to
the bundled LiberationSans). No Font Asset Creator, no SDF baking, no wiring.

## Swapping the typeface

Replace the three files with another family's `-Medium` / `-SemiBold` / `-Bold` files (or
drop a single file — it then serves every weight). Keep that family's `OFL.txt` here.

## Licensing — is it safe to ship?

Yes, if it's **OFL or CC0**. Fonts are always under copyright (the author owns it), but the
**SIL Open Font License explicitly grants free use, modification, and embedding in
commercial products** — including a paid/closed-track app — with **no royalty**. The one
obligation: keep the font's `OFL.txt` next to the `.ttf`. Fredoka is OFL, and so is the
LiberationSans fallback (`Assets/TextMesh Pro/Fonts/`).

**Do not drop a proprietary font** — Windows system fonts (Segoe UI, Arial, Cascadia, …),
Adobe fonts, or anything without an OFL/CC0 license **cannot** be redistributed in the
build. If in doubt, take it from fonts.google.com (every font there is OFL or Apache 2.0,
both ship-safe).
