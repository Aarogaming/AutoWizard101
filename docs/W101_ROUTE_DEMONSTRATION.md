# W101 route demonstrations

2026-11-08: the user offered to demonstrate Commons → Pet Pavilion → Pet Dance.
The demonstration has now been recorded and reviewed; see the
[dated results and current limitations](W101_DEMONSTRATION_20261108.md).
Use demonstrated actions as a candidate local movement policy, with visual
landmarks to check position and correct drift. This does not qualify a blind
timed replay as navigation, or turn one demonstration into a trained player.

## Passive recording

The development app provides a bounded recording entry point:

```text
ProjectMaelstrom.exe --record-w101-demonstration <current-window-handle> <new-output-directory> <seconds>
```

Duration must be 2–180 seconds. The selected target must still be Wizard101.
This is a supervised developer entry point, not yet a button in the installed
app. It reuses the GPU recorder and opt-in input timeline; no new dependency,
model, focus activation, game input, microphone or audio capture is introduced.

1. Wait for `standby.json`. The recorder waits at most five minutes for
   `start.request`. Input hooks are not installed during standby.
2. Confirm the demonstrator is ready, then create `start.request`.
3. Wait for `recording-ready.json` before announcing that recording has started.
   This file is written on the first capture, after encoder setup.
4. Record the normal Commons port and walking route. Map views at landmarks and
   a Dance instructions/Watch/Go/result cycle are useful optional evidence.
5. Create `stop.request` to end early, or let the duration expire. Wait for
   `finished.json` and process exit before processing results. An external
   supervisor also limits process lifetime in case setup or finalization stalls.

Only selected game keys and mouse events while W101 has foreground focus are
retained. The navigation profile adds X, M, N, R, End, Home, Page Up/Down, Escape
and Num Lock to the existing movement/modifier keys. This is observation of
controls, not an instruction to send all of them. Do not use game chat during a
demonstration: the recorder cannot distinguish those same keys used for chat.
Switching apps excludes their inputs; the game-only video continues. Focus
transitions and injected-event flags remain in the evidence.

## Evidence and limits

Each fresh folder preserves video, input events, SHA-256 video identity, target
identity, captured/submitted/decoded timestamps, client geometry and encoder
scale. Keys use the capture clock; mouse coordinates are client pixels, while
the captured surface includes window borders. A route compiler must account for
that offset and scale rather than assuming the coordinates are interchangeable.

W101's observed 1280×720 client has a 1282×759 captured window. H.264 output is
1282×760, with the full surface scaled by one pixel rather than cropped. Windows
can introduce additional encoded frames in this path. The demonstration entry
point explicitly accepts this only for changed geometry and a decoded duration
within 150 ms of the submitted interval; default recorder checks remain strict.
Raw clocks are retained, but exact input-to-decoded-frame alignment is **not
verified**. These recordings may be reviewed for routes and landmarks; do not
promote them to precisely timed Pet Dance ground truth without alignment checks.

The passive three-second preflight in
`.local/w101-route-study/capture-preflight-02` completed with 105 captured and
submitted frames, no queue rejection, and 185 decoded frames. Measured source
delivery was 33.88 fps, with a 44.45 ms 95th-percentile gap and a 77.78 ms maximum
gap. The 60 fps encoded stream is not evidence of 60 distinct observations.
The saved frame visibly shows native Idle in the Commons. The large tunnel on
the right is labeled Shopping District; it must not be mislabeled as the Pet
Pavilion entrance. The route itself has not yet been demonstrated or qualified.

The separate early-stop check in
`.local/w101-route-study/capture-stop-check` also completed: a 30-second budget
was stopped after roughly two seconds, saved 2.283 seconds of decodable video,
sent no inputs, released its observation hooks and exited. All 350 app tests
passed after these changes; targeted formatting and style checks passed. These
checks establish recorder readiness, not a completed human demonstration. The
development build is ready for that demonstration; no new installer was produced.

`video_verified` means decoded video inspection succeeded, not that navigation,
input alignment, phase recognition or live teaching succeeded. Draft metadata
keeps `route_qualified`, `labels_verified` and
`exact_input_video_alignment_verified` false. No automatic replay follows a
recording. After live tests, restore native Idle through the game menu and verify
its banner; the passive recorder itself never changes game state.
