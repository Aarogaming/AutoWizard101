# Live dorm regression frames

Captured by the installed Maelstrom home routine on 2026-11-08 in the user's
Wizard101 dorm, 1280 x 720 client / 1282 x 759 captured window, Tiny UI.
Source report: `home-movement/20261108-172432-29113e305ba3/report.json` in the
local Maelstrom data directory. No credentials or desktop outside the game are shown.

The original W/S routine accepted one W pulse (126.96 ms down-to-release), visibly
moved, and paused before S with `home-hud-changed`. Broad lower-corner regions
included moving floor pixels. Compact health/mana and spellbook anchors accept
this ordinary view change while still rejecting a missing HUD. The regression
does not prove safe navigation, idle prevention, or all UI scales/resolutions.
The current routine offers persistent Spin, Step and custom Routine choices;
default Spin uses A-only pulses. This fixture qualifies the HUD correction,
not every movement option.

SHA-256 originals:

- dorm-before.png: 95be124c2ccecb1da6f52f70e602fc2848b0f9d14eb8ec58dd9b9edc5654cef6
- dorm-after.png: f5df1f6a9afececbf34217571bb42fd90583d130660f58e76f005587a113863a
