# W101 loading transitions and shared routine conditions

Task `W101-LOADING-16`, 2026-11-10 Eastern (live timestamps below are UTC on
2026-11-11). This checkpoint changes source; the installed build remains
`11d29e86169b472ab46b3a4f2f64de02` from the preceding CLI release.

## Result and scope

The reviewed hook-free wizwalker loading read matched an independently observed
Dance loading screen, then cleared after the game loaded. A subsequent one-minute
user-demonstrated Pet Pavilion/Commons crossing test produced **six complete
clear → loading → clear periods**, 105 observations, 27 loading readings, and
105 confirmed disconnects. The user confirmed repeated crossings. The observer
sent no inputs, activated no windows, captured no video, and ran no models.

The six zone periods were detected from client readings, not individually matched
to saved loading-screen video. This establishes the loading flag's usefulness on
this selected client and route; it does not qualify navigation, every client build,
exact transition latency, arbitrary state recognition or perfect reliability.

Shared `ModuleRunner` / `ModuleScreenSession` now accept an optional loading
condition. `WizwalkerModuleLoading` supplies it using the parent's scheduler lease,
target and source pins. Dance's existing postgame modules can explicitly opt in
through their trial launchers. That combined live postgame path has **not** been
run; its composition, guarded dispatch and cleanup paths are verified offline.

## Live evidence

Local artifacts: `.local/w101-loading-transition-20261110/`.
Selected process: PID 27396, started UTC ticks 639299525550685854, HWND 1914306730.
Client executable SHA-256:
`544ab726ef7b920a5ccb67aa3d06b245db7e0012317d2fe7856d58c17c31cf7e`.
Reviewed Python worker/source hashes are unchanged from the installed adapter.

### Initial Dance transition

`live-watch-01.json`, operation `f73a549d754d4ff0a04e442ec2faf99a`:
28 readings, 3 loading, 15,077 ms. The watch ended while loading, so its own
`observedClearLoadingClear` is correctly false. A separate verified read
(`after-play.json`, operation `91eeb9a869ba4d79b7103898fc9f8fff`) returned false
after Dance loaded, with cleanup confirmed and 281 ms backend duration.

- 02:23:04.644: observed instructions, Wizard City selected, cost 4, energy 55/55.
- 02:24:01.455: independently observed actual W101 loading screen after Play.
- 02:24:17.976: Dance visible; no arrow-playing routine was running.
- 02:24:53: separate client read clear after loading.
- 02:28:07.286: independently observed YOU ARE IDLE at the Dance sigil;
  the user also confirmed Idle.

The coordinator used brief UI interaction to trigger/exit the game. Some input
calls reported intervening input and required fresh observations, so they do not
establish autonomous sigil or cleanup qualification. The game was allowed to end
without playing; the displayed result was TRY HARDER/+0. Entry consumed 4 energy.
No snack was fed, no volume was changed, and no purchase was made. Prefer zone
crossings for subsequent loading qualification to avoid pet-energy consumption.

### Zone crossings

The first 15-second attempt (`zone-watch-01.json`) captured 28 clear readings.
The user had not crossed yet; it is a coordination miss, not a detector failure.
Question notifications were agreed as the start signal. The next watch was
started before notifying the user and bounded to 60 seconds.

`zone-watch-02.json`, operation `43d4cd60b2964e1c92ad0b97bd27eb14`:
105 readings in 60,538 ms (a final in-progress reading may finish after 60 s).
All six detected periods returned to clear within that watch:

| Period | First loading reading UTC | First subsequent clear UTC |
| --- | --- | --- |
| 1 | 02:37:46.624 | 02:37:49.388 |
| 2 | 02:37:52.822 | 02:37:55.096 |
| 3 | 02:37:59.633 | 02:38:02.444 |
| 4 | 02:38:09.759 | 02:38:11.753 |
| 5 | 02:38:16.949 | 02:38:20.012 |
| 6 | 02:38:24.553 | 02:38:27.382 |

These timestamps bracket sampled states; they are not exact screen onset/end
times. The user walked the route. No coordinator desktop input occurred during
the watch. At 02:39:57.145 one game-window snapshot independently confirmed
YOU ARE IDLE near the Commons passage. No input was sent for that snapshot.
`zone-final-cleanup.json` records the observer process exited, no adapter Python
workers remained, and the shared control guard was read under its OS byte lock:
one ASCII `0` byte (48), clean. No guard reset or recovery bypass was performed.
`zone-qualification.json` and copied trace/summary preserve the derived result.

## Implementation and boundaries

- CLI `wizwalker-watch`: 15-second/32-sample default, optional
  `--duration-seconds 1..60`, at most 128 serial observations. The existing
  one-shot worker disconnects after each reading. Cancellation/stop-file remains
  available. The watch retains one control lease and never sends game inputs.
  Completed means the observation series ended, not that a transition occurred.
- `WizwalkerOperation.ReadOwnedAsync` reuses an already admitted owner. The
  module bridge verifies explicit enablement, exact target/visit, admitted pins
  and clean result provenance. It launches no second controller.
- A loading sample includes target/geometry/visit binding, source, monotonic
  sampling time and nullable flag. Loading, unavailable, absent, wrong identity,
  future and stale observations withhold actions and outcomes. A false flag
  supplies no scene, context, proximity, control or resource evidence.
- Screen stability and aliases reset while blocked. Two fresh visual observations
  are required after clear. Pending receipts are retained; loading never counts
  as an action's successful result. Finite deadlines keep advancing.
- Source timestamps are checked; startup and disconnect consume the conservative
  one-second freshness budget. The dispatcher rechecks freshness and `CanDispatch`
  immediately before input. Recovery uncertainty blocks the same owner too.
- Optional Dance postgame loading reads are capped at 32, no faster than two
  per second, before fresh capture. This is a slow transition aid, not a 60 fps
  reflex feed. Ordinary Dance play and default launches remain visual-only.
- No production dependencies, new network behavior, memory writes or hooks added.
  Installed game files remained read-only. This checkpoint is not packaged or
  installed. `--update` is not implemented; keep future updating on the existing
  installer lifecycle, which preserves packs and reports.

## Verification

- 1,601 native tests passed; zero failed or skipped, including actual existing
  fake-child adapter supervision, new shared loading-condition tests, saved-OCR
  menu outcomes, inherited-owner integration, stale/unknown/identity rejection,
  dispatch expiry, resource caps, cleanup failure and command-duration validation.
  Full run: `loading-full-suite.trx`; focused runs also retained separately.
- Scoped whitespace format/verify and analyzers passed on owned C# paths;
  Release test build used warnings as errors, two CPU workers and no shared compiler.
- 16 Python adapter tests passed using fake clients only. The restricted runner
  stalled without a result and was stopped; the normal runner completed in 1.172 s.
- Project-board validation passed: 30 workstreams, evidence paths and dependency
  graph checked. No gameplay, model or capture workloads ran alongside the suites.

## Next work

Qualify the opt-in postgame composition in one bounded live visit, then expose
qualified operations consistently through workspace/CLI controls. X is the common
`game.interact` action for nearby sigils, NPCs, reagents and quest objects; vary the
observed precondition and expected result, not the input implementation. Current
sigil graph reuse is implemented, but standalone live dispatch/recognition still
needs qualification. Native Idle is already a reusable menu/Auto-Idle operation.

For other client-derived states, review and validate each field against a named
game state before treating it as authoritative. Not-loading does not imply signed
in, safe to move, within interaction range, dialogue open or native Idle. Retain
visual confirmation for those unresolved states. Resource-aware task selection
and multi-game schedules should compose finite verified operations; around-the-
clock farming/questing is not delivered by this loading checkpoint.
