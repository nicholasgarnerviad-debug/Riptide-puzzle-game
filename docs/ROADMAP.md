# Riptide — Post-Gate-C Product Roadmap

Lead-dev plan adopted 2026-06-11 under Nick's delegation ("build a comprehensive plan…
after planning implement"). Rule changes are logged individually in DECISIONS.md.
Everything here is code/data work only — no music, no art assets.

## Tonight's build (this branch, in order)

| # | Item | Why | Risk |
|---|------|-----|------|
| M1 | **First-run → straight into L1** (age gate → tutorial board; Home appears after the first result) | The ≥85% reach-L6 funnel is decided in the first 30 seconds | S |
| M2 | **Continue offer** — drown-only, once/run, Voyage+Endless (never Daily): rewarded ad or coins → water −3 (floored), fresh tray, tide counter reset | The death moment is the highest purchase-intent moment in the genre | M |
| M3 | **Piece Swap booster** — replace ONE tray slot, 40 coins | High-frequency small spend; drains coin balances → pack demand | S |
| M4 | **Endless milestones** — every 5 tides banks bonus coins, paid at run end, announced mid-run | Gives Endless a ladder; longer sessions = more ad slots | S |
| M5 | **Stats block in Settings** — lifetime rescues, voyage stars, endless best, streak best, decorations | Identity/retention from data the save already holds | S |
| M6 | **Home redesign + menu ambience** — voyage hero card with zone progress, daily card with streak, animated water band, marine snow + vignette on menus, staggered entrances, secondary-contrast bump | Single largest perceived-quality jump available without assets | M |
| M7 | **Share-preview water bar drawn as colored squares** (text payload byte-identical) | The Daily share preview currently shows tofu boxes | S |
| M8 | **Notification seam** — fake + real adapter behind `RIPTIDE_NOTIFICATIONS` (streak-risk evening ping, new-daily morning ping) | Daily is the retention engine; nothing calls players back yet. Package install rides Nick's SDK pass | S |
| M9 | **Remove-Ads upsell card** on results after the session's 2nd interstitial | Convert the annoyance moment directly | S |

## Deferred (next sessions, agreed order)

1. **Mid-run save & resume** — replay the move list against the seed; design doc first
   (interrupt points, version tolerance). Biggest remaining UX gap.
2. **Starter pack IAP** — needs real product IDs and the IAP SDK from the release pass;
   the shop card slot and seam are ready.
3. **Rewarded level-skip after 3 voyage failures** — rule change, wants gate feedback on
   actual difficulty feel first.
4. **Last-chance slow-mo drama** — juice pass material, alongside Gate A feedback.
5. **Rotating rare Tidepool decoration** — wants either a server flag or a date-hash
   rotation rule; pairs with the remote-config seam.
6. **Remote-config seam** for caps/pricing/balance A-B — after analytics SDK lands.

## Out of scope until external deliverables land

Rungo fonts + iconography (visual character), AdMob/Firebase/IAP SDK installs
(`SdkAdapters.cs` is prewired), FileProvider manifest (share images), device perf +
visual gates (Nick's hands), Play signing.
