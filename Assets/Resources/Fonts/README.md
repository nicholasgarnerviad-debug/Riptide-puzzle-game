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

## Licensing

Commit the font's `OFL.txt` alongside it (OFL requires the license travel with the file).
OFL fonts are free for commercial/closed-track use. Don't drop a non-OFL/proprietary font
(e.g. a Windows system font) — it can't be redistributed in the build.

## Notes

- The dynamic SDF is generated at runtime, so the swap is instant on next Play — no import
  ritual. If you later want a baked SDF for perf, generate one and the same loader path picks
  the Font up either way.
- All sizes/weights/tracking still come from `ui_theme.json` type tokens; the font only
  changes the glyph shapes.
