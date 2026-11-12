# Live W101 keep-active checks — 2026-11-08

This is a bounded test record, not a general claim of idle prevention or navigation.
The user authorized live testing, pausing on desktop activity, and then offered an
hour of manual demonstrations. No clipboard contents or credentials were read.

## Initial installed W/S build

- Installed build: `0.2.0-preview.20261108-home-movement`.
- W101 target: PID 67500, HWND 3342490, 1280 × 720 client, Tiny UI.
- The game was already authenticated. Entered the selected wizard, initially at
  the fairground. A Home shortcut was sent before the user's suggestion to stay
  by the minigame sigil arrived; it completed to the dorm. No route was followed.
- Session report: `home-movement/20261108-172432-29113e305ba3/report.json` in local
  Maelstrom data. One W pulse was accepted and released, with 126.96 ms measured
  between dispatch and release. Saved before/after images visibly show a changed
  viewpoint consistent with forward movement.
- The routine paused before S with `home-hud-changed`, completed pairs = 0. The
  lower-corner comparisons included moving floor pixels. This is a failed complete
  routine trial, not a successful keep-connected result.
- Correction: compact health/mana and spellbook anchors; actual frames added as a
  regression fixture. A missing-HUD variant must still be rejected. Measurements
  and a small reference descriptor are now retained for diagnosis.

## Native Idle check

- At 17:26:13 UTC, the client visibly offered **Idle** on its Escape menu. Selecting
  it displayed **YOU ARE IDLE**. Maelstrom's movement routine was already paused.
- At 17:34:19 UTC, the banner was absent and the view had rotated. The user later
  confirmed that they had demonstrated A/D manually. The exact interruption time
  was not recorded.
- Therefore this interval is **interrupted**, not an uninterrupted eight-minute
  test and not evidence of native Idle failure. No claim about maximum idle duration
  is established. Native Idle is useful because it does not require repeated inputs;
  its availability and long-term connection behavior are separate claims.

## Corrected A/D build and next live check

- Installed build: `0.2.0-preview.20261108-home-turns`.
- Default input changed to opposing 60 ms A/D turns, initially misunderstanding the user's correction;
  no forward/back translation is scheduled. Existing takeover/stop/focus gates remain.
- All 296 app tests passed, including real-frame regression and bounded-turn
  sequencing. Formatting and analyzers passed. These are offline checks.
- Desktop control was released before requesting the user's brief live check:
  capture the dorm reference, Start, wait quietly for the first pair, then move
  the mouse and check for a latched pause. Results will be reconciled from the
  session report. A longer keep-connected trial remains pending.

The user wants to use the available hour to move toward minigame play. Conjuration
Concentration is the first candidate from the existing research: visually remember
groups from the preview, propose clicks, and verify each reveal/removal. This is
not a claim that a Conjuration adapter already exists. The root ZIP source scan
found no ready Conjuration implementation in the bounded text-file search.

## Persistent choices and unified workspace

The user clarified that spinning means one direction and requested persistent
options rather than repeated changes. Source now offers Spin (A-only, D-only,
optional opposing turns), Step, saved Routine, and Native Idle. Wizard101 has
one workspace with routine categories and the existing teaching workflow.

At 17:50 UTC, passive Conjuration recording kept only one frame because the
low-rate consumer read stale queued captures. A newest-frame option was added
without changing ordered video capture. The 17:52:43 UTC recheck saved 16 fresh
1282 × 759 frames in 5.19 seconds, with one rejected sample and maximum observed
frame age 300.13 ms (sampler limit 500 ms). No input or foreground change was
requested. It shows instructions, not a card-matching demonstration. Evidence:
`.local/conjuration-samples/recordings/20261108-175243-79c9bad5/`.

The passive sampling limit does not relax live movement's 200 ms requirement.
Four requested stills/sec is not a claim of real-time video or reaction speed.

## Installed workspace update

`0.2.0-preview.20261108-w101-routines` is installed. Receipt:
`.local/w101-routines-installed.json`. The installed offscreen check found one
Wizard101 window and confirmed Auto-Idle unchecked, Start/Stop disabled and no
active operation. 307 app tests, 12 installer tests, and 15 pack/replay/Workshop
tests passed. Pack export was validated by the existing Python registry. The
packaged install/update/uninstall lifecycle preserved all sentinel user data,
including saved routines and Auto-Idle settings. These checks sent no game input.

The user deferred the manual Conjuration demonstration. No recording or live
movement session remains running from this work. Subsequent teacher research is
offline and recorded in [Legacy W101 teachers](W101_LEGACY_TEACHERS.md).
