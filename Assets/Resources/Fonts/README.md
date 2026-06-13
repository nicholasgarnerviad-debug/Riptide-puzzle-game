# Drop the game font here

To replace the LiberationSans placeholder with the real typeface, **drop one `.ttf`
or `.otf` file into this folder** (`Assets/Resources/Fonts/`). That's the whole step —
the game picks it up at runtime (`UiText.CustomFont`), generates a dynamic TMP SDF from
it, and reskins **every** text element: TMP screens *and* the legacy-UGUI HUD. No Font
Asset Creator, no wiring, no SDF baking.

## Recommended free fonts (all SIL Open Font License — safe to ship)

| Font | Feel | Get it |
|---|---|---|
| **Outfit** | geometric + rounded, closest to the GDD's "Rungo" | fonts.google.com/specimen/Outfit |
| **Sora** | technical, clean "instrument panel" tone | fonts.google.com/specimen/Sora |
| **Baloo 2** | rounded, friendly casual | fonts.google.com/specimen/Baloo+2 |
| **Nunito** | soft geometric, great at small sizes | fonts.google.com/specimen/Nunito |

Download → unzip → drop the `.ttf` (e.g. `Outfit-Bold.ttf` or the variable font) here.
If you drop several, the first one Unity loads is used — keep just one for a clean look.

## Licensing — is it safe to ship?

Yes, if it's **OFL or CC0**. Fonts are always under copyright (the author owns it), but the
**SIL Open Font License explicitly grants free use, modification, and embedding in
commercial products** — including a paid/closed-track app — with **no royalty**. The one
obligation: keep the font's `OFL.txt` next to the `.ttf` (drop it in this folder too). All
four fonts recommended above are OFL, and so is the LiberationSans placeholder the game
ships with today (`Assets/TextMesh Pro/Fonts/`), which is why it's already safe.

**Do not drop a proprietary font** — Windows system fonts (Segoe UI, Arial, Cascadia, …),
Adobe fonts, or anything without an OFL/CC0 license **cannot** be redistributed in the
build. If in doubt, take it from fonts.google.com (every font there is OFL or Apache 2.0,
both ship-safe).

## Notes

- The dynamic SDF is generated at runtime, so the swap is instant on next Play — no import
  ritual. If you later want a baked SDF for perf, generate one and the same loader path picks
  the Font up either way.
- All sizes/weights/tracking still come from `ui_theme.json` type tokens; the font only
  changes the glyph shapes.
