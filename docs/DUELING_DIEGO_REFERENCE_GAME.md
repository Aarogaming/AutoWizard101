# Dueling Diego standalone reference

The user authorized downloading the standalone game for local project research.
Downloaded 2026-10-30 from [Flash Museum's Dueling Diego page](https://flashmuseum.net/game/dueling-diego-y6y/).
The page identifies KingsIsle as developer, describes its game archive as modified
to load supporting files and notes that game codes cannot be used.

Local, ignored file (not shipped with Maelstrom):
`.local/reference-games/dueling-diego-flashmuseum/dueling-diego.zip`

- SHA-256: `AB7EF955F345895D3461BCFE2EF74C7B7B6701297600C07E2E299FF92C53A903`
- Published archive: `https://games.flashmuseum.net/f612649d-7871-45ca-a0d3-d1fc0a25c4f0.zip`
- ZIP contains a launch HTML file, two Diego SWFs and three supporting SWFs.
  The archived HTML embeds `DuelingDiego_Hack.swf` at 600 × 447; the presence of
  this name is not evidence of any specific in-game modification.
- Only archive metadata and the small text files were inspected. The SWFs were not
  executed, decompiled, or validated as equivalent to the current Wizard101 game.
- No standalone Flash/Ruffle runtime was detected in PATH; the site uses a Ruffle
  web player. Local playback, offline availability and Maelstrom capture remain
  untested. No game code is bundled, redistributed, or automatically executed.

## Intended use

Treat the standalone game as an optional, user-selected controlled test target for
capture, timing, object-detection and replay experiments. Compare layout, controls,
timing and enemy behavior against the actual Wizard101 mini-game before using its
results as evidence for a live pack. Do not assume equivalence or enable gameplay
control based on the archive alone. Real-time recording and input/outcome timelines
remain the critical path in [DELIVERY_COURSE.md](DELIVERY_COURSE.md).

The archive is third-party game content. Keep the reference local while provenance,
runtime compatibility and distribution rights are reviewed before any packaging.
