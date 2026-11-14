# TEACHING-ACTION-39 — bounded teacher action to replay-tested card

2026-11-12 · CORE-DELIVERY · source + live teacher evidence, not installed.

## Outcome

Maelstrom recorded and completed a bounded native Idle → menu → native Idle
cycle through the CLI. Two dispatched actions were accepted and released. Fresh
observations independently showed NativeIdle, MenuObserved, then NativeIdle.
The wizard was left in verified native Idle without the user playing. No walking,
quest progression, purchases, snack use, volume changes or desktop-use session.
Existing countdown, foreground quiet-window admission, takeover, control owner,
source pins and scheduler remained in force.

Actual teacher operation: ee98fac206a849db82953f6a9819bb01.
Capture session:
.local/teaching-action-39/capture-fixed/teaching-sessions/884001fc5c2d46b0949f70212bace52f/teaching-session.json
CLI reports: .local/teaching-action-39/teacher-result.json and record-fixed-result.json.

## Input evidence and derived card

The wake action uses target-bound Windows messages, so it does not appear as an
Escape key event in the low-level keyboard hook. The source is the actual accepted,
released transport receipt tied to the exact proposal and independently observed
changed state. Hook observations captured focus and the later mouse movement/down/
up used to select Idle. These sources are not falsely equated.

--teaching-link-action <idle-cycle.json> <input-events.json> <new-output-directory>
validates the teacher's final Idle, action/proposal identity, receipt acceptance
and release, fresh changed state, same binding, captured QPC interval, exact target,
verified video flag and video hash. It creates an editable RoutineGraph, activity,
provenance mapping, evidence-session and replay report. Original source proposal
IDs map explicitly to deterministic replay IDs. No successful receipt is invented.

The actual exported NativeIdle → wizard101.wake-idle → MenuObserved card completed
replay: one proposal, one real receipt, zero desktop dispatch, live_qualified=false.
Artifacts: .local/teaching-action-39/linked-card/{routine,activity,provenance,session,replay}.json
and events.jsonl. The activity and derived verified-state track were attached to
the recording timeline through --teaching-annotate / --teaching-attach-state.
Video-relative activity times were shifted by the recorded QPC-origin offset for
the session timeline; raw evidence was not overwritten.

The card was derived from and replayed against the same demonstration. It is not
a held-out test, a learned policy, a pack-installed live card or a general causal
claim. The linking adapter currently consumes the W101 Idle-cycle trace; emitted
cards use the shared game-neutral graph and replay contracts. Other teacher traces
need their own validated input bridge rather than ad-hoc JSON conversion.

## Repairs during qualification

First two recordings failed before teacher input because the new readiness marker
serialized a native-sized window handle. Corrected it to use the serializable
TeachingTarget long handle. A bounded Idle-only preparation verified foreground
and native Idle between attempts. The repaired recording completed successfully.
Failures remain preserved under capture-data and capture-retry.

Recording CLI now accepts InputProfile=Movement (default) or Wizard101Navigation
for the matching game. This permits opt-in Escape/X/etc observation while the
selected target is focused. recording-ready.json lets a teacher wait for capture
startup rather than race the encoder. The teacher is the only action owner;
recording used EnableW101Telemetry=false because the teacher already owns its
UI observations. No competing client reader was started.

## Training and remaining gates

Preserve video/input hashes, process instance, QPC timestamps, actual source
receipts, independent state outcomes and human/reviewer notes separately. The
export provenance explicitly labels bounded-scripted-teacher; the older generic
recorder's observed-user label must not be used to infer demonstration authorship.
Richer recording actor metadata remains pending. Frame alignment/rate-conversion
limits from the capture sidecar still apply. Next: a fresh-session holdout and a
second action kind, then pack-level bindings/qualification and training export.
No model is required; no model was trained or loaded. Tray support remains a side
workstream; this change does not install/repackage the application.

## Verification

Focused tests: 10 passed before live. Tests reject unaccepted/unreleased receipts,
stale outcomes and altered video hashes; synthetic end-to-end linking produces a
replay with one receipt and live_qualified=false. Actual CLI link/replay exited 0.
Final full suite, formatting/analyzers and board results follow below.

Final checks: 2,548 native tests passed, zero failures/skips; warnings-as-errors compilation, scoped whitespace verification and analyzer verification passed.
