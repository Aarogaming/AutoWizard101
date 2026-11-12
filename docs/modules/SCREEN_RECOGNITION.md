# Screen recognition for reusable modules

The recognition bridge supplies the existing module runner with observed scenes,
named controls and their image-local bounds. `ModuleRecognition` is shared data
and validation; `W101MenuRecognition` supplies Wizard101 vocabulary using the
existing local menu OCR, Dance entry recognizer and postgame classifier. It adds
no game controller, capture service, model, movement planner or input dispatcher.

## Evidence contract

`ModuleVisualFrame` contains the encoded image SHA-256, original capture time,
pixel geometry, target/geometry/visit binding and text origin.
`ModuleVisualControl` contains a control ID and its exact image-local rectangle.
`ModuleVisualRecognition` combines that frame with a scene, bounded control/fact
arrays and a reason. The accepted text origins are `local-windows-ocr`,
`saved-ocr`, `manual-annotation` and `injected-test-ocr`; pure classification
preserves the supplied origin.

`ModuleRecognition.AsObservation(recognition, expectedBinding, expectedGeometry,
observedMs, targetReady, contextVerified, inputOwned, userActive, cancelled,
externalFacts)` validates image identity, finite positive geometry, finite bounds,
unique named controls and bounded facts. Capture age must be between zero and
300 ms, with exactly the expected binding and geometry. Stale/future, mismatched
or unusable evidence supplies no actionable controls or verified facts. A changed
binding remains visible to the runner instead of being replaced with the expected
one. User activity, cancellation and ownership/target gates remain external.

The converter retains the original capture timestamp and never fills
`OutcomeForProposalId`. The session uses its trusted monotonic observation clock
for routine deadlines, while retaining the original capture time as separate
evidence. This prevents a bad future capture from advancing the routine's clock
and lets deadlines continue through stale frames. The session also owns stability
checks and receipt-bound outcome association: a fresh image and matching scene
alone are not a receipt or a verified consequence of an action.

## Wizard101 supported observations

| Observed screen | Module scene | Named control |
| --- | --- | --- |
| Native YOU ARE IDLE banner | `NativeIdle` | None; no wake action. |
| Graphics menu with its recognized Idle/Help/OK row | `MenuObserved` | `game-menu.idle` at observed Idle text. |
| Your Reward with Next | `GameResult` | `pet-dance.next` at observed Next text. |
| Feed Your Pet with Finish | `SnackOffer` | `pet-dance.finish-without-snack` at observed Finish text. |
| Dance instructions and level menu | `SigilMenu.dance` | `sigil.dance.cancel` only for unique, localized Cancel text. |
| Generic outside Press/Play prompt | `OutsideCandidate` | None. |
| Unsupported or insufficient evidence, including blank loading OCR | `Unknown` | None. |

Strong supported modal/Idle recognition supplies only the visual fact
`wizard101.ui`. It does not infer world, route, home, clear-area, permission,
energy, selected-arena or movement facts. Native Idle has precedence over
underlying menu/prompt text. Ambiguous Cancel controls leave the recognized Dance
menu without a click binding. No Play, snack-feeding or Crown Shop control is
emitted by this adapter.

Navigation landmarks, motion/progress outcomes and initial sigil identity remain
unsupported. The generic outside prompt is not automatically `OutsideReady`.
The session may adopt it only through the separately verified current transition
and its actual receipt; no such receipt is invented by recognition.

## Encoded-image reader

`W101MenuRecognition.ReadPngAsync(encodedPng, frame, token)` copies the bounded
input bytes, verifies their SHA-256, decodes those same bytes and checks decoded
dimensions before invoking OCR. It reuses the existing bounded PNG decoder and
reader-lifetime helper: at most 8 MiB encoded bytes, 4,194,304 decoded pixels,
8,192 pixels per axis, and a five-second read deadline. A canceled asynchronous
reader retains its borrowed bitmap until it actually finishes; late faults are
observed. The public path serializes one local OCR engine, including cancellation
unwinding. No server model or network request is involved.

The public reader records `local-windows-ocr` only for text it actually reads.
Its internal injected-reader test overload always records `injected-test-ocr`.
Supplied cached OCR and manual words use pure `Classify(frame, text)` with their
own origin. A PNG hash verifies the encoded image bytes; it does not prove that
cached or annotated text was independently recognized from them.

The existing conditional lower-menu rescue remains part of menu OCR. Recognition
does not weaken its matching thresholds or fill missing words from expectations.
Pixel rectangles stay in captured-image coordinates. A live input adapter must
separately verify the current window/client transform and shared ownership before
using a proposed control.

## Offline evidence and tests

The evidence task supplies six original saved PNGs and unchanged saved OCR in
`ProjectMaelstrom/ProjectMaelstrom.Tests/TestData/Modules/W101Menus`. Their
manifest/provenance records source hashes and explicitly missing capture times.
Tests verify those hashes and compare actual OCR-derived recognition against
the review labels. The classifier receives the text, not the expected scene.
Synthetic test clocks are declared rather than presented as original timing.

Additional tests cover freshness boundaries, source/geometry changes, malformed
controls/facts, cancellation, receipt omission, hash mismatch, mutation of input
bytes during reading, unique Cancel recognition, Native Idle priority and delayed
reader disposal. Injected readers test plumbing only; fresh saved-image OCR runs
belong to the coordinator's separate qualification report. No tests here open a
window, acquire input, run a model or dispatch desktop actions.

This source increment awaits the coordinator's serialized build and integrated
offline review. Saved image recognition is not autonomous live gameplay or proof
of complete navigation/menu/Idle operation.
