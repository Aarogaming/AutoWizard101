# Gameplay scene gates

Loading, menus and pauses belong between perception and a routine's motion
primitives. They are shared session behavior, not a failed round or an instruction
to repeat the last macro. Current implementation: a reusable
[pause clock](../ProjectMaelstrom/ProjectMaelstrom/Utilities/GameplayPauseClock.cs)
and its first consumer, the experimental native Pet Dance trial.

## Implemented loading behavior

- The Dance profile may contain an eleventh named, hashed `Loading.png` reference.
  It uses the same small composite panel as the ten gameplay references. Its
  optional `Source` records separate provenance; the current loading example
  came from a live trial, not the human demonstration video.
- Known Watch/Go/Resolving recognition has priority. Only an otherwise unknown
  panel matching the full loading reference within RMS 8 is classified Loading.
  Unfamiliar images remain Unknown; they do not become indefinite pauses.
- Loading sends no arrows, clears queued/partial cues, and preserves the expected
  round length. Resuming requires a fresh complete Watch sequence. Loading is
  never credited as Success or Failure and old proposals are never resumed.
- Startup/loading time has a cumulative 45-second allowance separate from the
  75-second gameplay allowance. Pausing never resets either budget. The shared
  scheduler allows 200 seconds including supervised cleanup and setup. These
  are experimental run limits, not claims about normal W101 load duration.
- No new capture timestamps for five seconds produces a distinct capture stop.
  Progress reports include capture age, scene, active/paused time, content-change
  count and time since content changed. Static pixels with advancing capture
  timestamps can be normal loading; they do not prove a frozen capture or game.
- Unknown content during an active Dance sequence still invalidates continuity.
  Loading recognition does not weaken target identity, foreground, geometry,
  takeover, key-release or freshness checks.

`GameplayPauseClock` accepts elapsed wall time and the latest scene classification.
It is deliberately independent of W101 and input dispatch. A fresh recognized
frame and the routine's own prerequisites remain necessary to send a key.

## Evidence and correction

Live attempts 02 and 03 used the earlier 12-second unknown-scene cutoff. Both
captured the loading image and sent zero arrows. In attempt 03 the user reported
the game beginning at the cutoff; later visual inspection showed its failed
reward screen. Therefore the earlier description that the game had stalled was
not established. Neither a capture freeze, update mismatch nor security response
was demonstrated. The concrete defect was treating startup as an unknown active
game state and stopping too early.

Attempt 03 was returned through Next → Finish (without feeding) → menu → Idle.
The supervisor observed **YOU ARE IDLE** and stopped the desktop helper. Its
original `CleanupFailed` receipt remains unchanged because confirmation arrived
after the cleanup wait. The later recovery is recorded separately in
`.local/w101-route-study/live-teacher-03/later-recovery.json`.

Offline loading/clock/sequence tests cover a 20-second load with the full gameplay
budget remaining; cumulative pauses; unknown and missing-frame behavior;
loading-image false positives; unchanged gameplay recognition; result-latch
reset; and fresh-sequence requirements. The loading-aware profile also replays
all five original rounds with unchanged proposals/outcomes and no rejected
frames: `.local/w101-route-study/loading-replay-01/replay.json` (1,290 frames,
2.1963 ms mean analysis). This is still the calibration video, not held-out play.

## Remaining integration

Loading-aware attempt 04 observed 735 fresh frames in 45.007 seconds: 93 Loading,
642 Unknown and 598 content changes. Every cue was Unknown, so no arrow was sent.
This verifies loading recognition and continuing capture, not live gameplay
recognition. The supervisor observed the failed game's result, left without
feeding and confirmed native Idle inside the cleanup deadline. Its Completed
receipt therefore establishes cleanup, not successful play. Evidence is under
`.local/w101-route-study/live-teacher-04/`. Only transition panels were saved;
the next diagnostic needs continuous footage of the intervening gameplay.

The loader reference is calibrated to one observed W101 loading artwork and
geometry. Other artworks, games, layouts and resolutions need separate evidence.
The UI does not yet author these profiles or expose this context gate as a
general pack setting. Menus, dialogs, disconnection and modal blockers need
their own detectors and transitions; they must not be relabeled Loading.

A new live round must verify loading-to-Watch transition, actual key acceptance,
observed outcome and native Idle return. The Windows capture surface-copy call
itself is not yet cancellation-bounded; an underlying stalled GPU copy can
outlive the loop deadline. The new no-frame check covers returned missing or
non-increasing frames, not a hung asynchronous copy. This remains a separately
tracked capture-lifecycle gap, not evidence of the observed loading cause.

Live interaction stays through visible screens and ordinary player inputs.
Installed game files are read-only research sources. Process-memory observation
is a separate capability, currently not enabled; it is not equivalent to either
file inventory or screenshot capture.
