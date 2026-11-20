# Stateful checked stream consumer — 132

2026-11-15. Offline implementation;20 focused native consumer/decoder tests pass,
scoped formatting/analyzers and warnings-as-errors compilation pass. No live
observation, focus change, input or installation in this checkpoint.

`W101PoseStreamConsumer` retains ordered state for the bound decoder: externally
pinned run/stream/target, exact next sequence, increasing acquisition and host
times, and bounded sample age. End/invalidation, malformed events, identity or
sequence failures, owner loss and expiry clear the current pose and permanently
end this consumer. A new stream needs a new instance; late samples cannot revive
an ended stream. Tests cover each major rejection path.

## Integration finding

The current native `W101UiTreeOperation.Runtime` delegates to
`WizwalkerWorker.ExecuteJsonAsync`, which returns a final result. Moving samples
are written to `ui-observations.jsonl`; they are not currently routed into the
new stateful consumer in real time. Wiring that artifact tail under the same
worker lifetime, including partial-line limits and terminal cleanup, remains
required. Do not describe this isolated stateful class as a connected live
navigation consumer. Ended-session replay cannot satisfy live freshness.

Authoritative wizard/zone-owner and same-zone loading epochs remain unresolved;
producer contextQualified remains false. Next work: bounded owner-scoped trace
transport and fake-file lifecycle tests, then candidate-only Olde Town observation
with hook restoration. No motion until the remaining qualification gates pass.

Gemini125 remains active report-only. LIA-0081 requests exact transport attachment
sites and lifecycle counterexamples; no competing build/controller authorized.
