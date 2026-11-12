# Pet Dance entry and full-visit transitions

Status: offline implementation and saved-evidence review. This adds no live
dispatcher, capture, launch, navigation or initial-Idle wake operation. The
installed player and its 1280×720 gate are unchanged.

## Reused foundation

`PetDanceVisit` adapts the shared `RoutineRunner` and the reviewable
`Resources/Routines/pet-dance-visit.json` sidecar. It uses the existing
`PetDanceScene`/`PetDanceLifecycleAction` vocabulary, menu OCR records and postgame
bounds validation. It does not introduce another scheduler or input owner.

The initial entry node accepts a qualified Dance sigil or instructions. A screen
with a positively verified requested arena can proceed to Play; otherwise a
ChooseLevel proposal requires an independently identified control for that arena.
After Interact, instructions may already have the correct arena selected. These
are scene branches, not an assumed fixed sequence of clicks.

The normal visit is:

1. Qualified sigil → Interact receipt → observed instructions.
2. If needed, measured requested-level control → ChooseLevel receipt → observed
   selected arena and enabled Play.
3. Explicit entry-cost approval → Play receipt → verified beginning of Watch.
4. Qualified teacher → matching completed/released specialist receipt → a newer,
   independently verified whole-game reward screen.
5. Next receipt → snack screen → FinishWithoutSnack receipt → outside prompt →
   OpenMenu receipt → Graphics menu → SelectIdle receipt → observed Idle banner.

Initial Idle is neither completion nor permission to wake the game. A game already
in Watch, Go, round feedback or a reward screen cannot be adopted as this visit.
The in-game DONE control and OOPS feedback do not establish a whole-game result.
Snacks and Crown Shop actions are absent from the graph and action allowlist.

## Recognition and evidence contract

`PetDanceEntryVision.Classify(text, imageSha256, capturedMs, evidence)` accepts
existing `PetDanceMenuText` and optional `PetDanceEntryEvidence`. OCR is bounded
to 512 words, each at most 128 characters, with finite image-contained bounds.
The classifier never performs OCR itself or accesses a window.

The instructions require a localized DANCE GAME header, OBJECTIVE, SELECT LEVEL
and a unique PLAY control below the selection header. OCR text establishes the
instruction/control location only. Disabled Play still has readable text.

Supplemental facts must match the exact image SHA-256, capture timestamp and
image geometry. A Ready result additionally needs all of:

- An explicitly identified, measured level control for the selected arena.
- Independent selection-highlight verification, not merely a hovered tooltip.
- Independent enabled-Play verification.

Missing proof yields InstructionsUnselected, meaning **selection/readiness is
unverified**, not a positive claim that no arena is selected. Conflicting or
malformed control geometry yields Unknown. Controls cannot be duplicated,
overlapping, outside the image, or outside the band between SELECT LEVEL and Play.
There is no default coordinate or inferred first-icon selection.

The generic bottom Press/Play prompt requires a separately verified Dance title
before it becomes DanceSigil. YOU ARE IDLE takes precedence over the sigil beneath
it. Empty OCR remains Unknown; a separately verified same-frame loading cue can
produce Loading. Neither loading nor unknown advances a transition or resets its
finite deadline.

`PetDancePlayButtonVision` supplies a small enabled/disabled appearance comparison.
It expands the observed PLAY text bounds into a bounded analysis crop, normalizes
using `VisualStateRecognizer.Features`, and compares two reference RGB grids.
Existing similarity and winning-margin thresholds apply; low similarity, ambiguity,
transparency and invalid geometry return Unknown. At most 65,536 source pixels and
two fixed 32×24 RGB reference comparisons are used. This detector does not prove
the selected arena. Its paired reference crops are test data, not automatically
installed runtime calibration.

## Visit API

`PetDanceVisitOptions` binds target, geometry, context/visit, requested arena,
enabled state and overall time budget. The default is disabled. It never derives
authorization from saved preferences alone.

`Observe(PetDanceVisitObservation)` requires fresh image identity, matching binding,
target readiness, verified context and input ownership. Entry evidence is rechecked
against that exact observation. Additional gates are `EntryCostApproved`,
`WatchStartVerified`, `SpecialistQualified` and `WholeGameResultVerified`, all false
by default. These are supplied verified observations/authorization, not detectors
or enforcement claims implemented by this adapter.

`RecordDispatch(proposalId, action, releasedMs, accepted, inputReleased)` records
the exact proposal and actual release time. The teacher action cannot use that
method to claim completion. `RecordSpecialist(PetDanceSpecialistReceipt)` requires
the same proposal, target, geometry, visit and arena, positive started/completed
facts and released input. A newer reward observation must follow; dispatch and
specialist completion alone are not a verified game outcome.

The graph bounds actions and transition/visit time. Repeated scenes cannot retry
an unresolved action. Rejected/unreleased receipts, changed binding, cancellation
and user takeover stop the visit; no cleanup inputs are invented after takeover.
Completion remains a historical result and does not authorize another game.

## Saved fixture provenance and limits

`ProjectMaelstrom.Tests/TestData/PetDance/Entry/entry-ocr.json` contains five
unmodified OCR word/bounds sets extracted from `.local/dance-menu-review-6.json`.
Each source PNG hash was checked. `provenance.json` records the OCR report hash,
fixture hash, source paths, manual annotations and reference-crop hashes.

| Saved frame | Supported evidence | Unsupported claim |
| --- | --- | --- |
| `inspect-006.png` | Instructions, SELECT LEVEL and readable disabled Play | Text implies enabled Play |
| `inspect-013.png` | Instructions, enabled Play pixels, hovered Wizard City tooltip | Hover highlight alone proves selected arena |
| `inspect-020.png` | Visually reviewed loading artwork; OCR contains no words | Empty OCR alone detects loading |
| `inspect-101.png` | Interaction prompt; separately annotated Dance title | Generic Press/Play uniquely identifies Dance |
| `inspect-123.png` | Native Idle banner over the retained sigil | Starting Idle means a visit completed |

These are 1282×760 outer-window images, not a native-resolution qualification.
Their capture times are unavailable; test timestamps are explicitly synthetic.
The paired `play-disabled.png` and `play-enabled.png` are 90×25 crops from frames
006 and 013, respectively, at source rectangle `(754,568,90,25)`. Their manual
labels describe actual saved appearance. Both are from the same recorded visit;
self-reference classification is calibration, not held-out accuracy.

The newer report's first-frame image is 1282×759 and is deliberately not treated
as the same geometry. No selected-without-hover or hover-without-selection
negative exists in this fixture set, so selected-arena recognition remains
unqualified. Explicit synthetic proof tests exercise the readiness gates without
presenting those proof flags as measured current-client success.

The subsequent [selection qualification](PET_DANCE_SELECTION_QUALIFICATION.md)
adds exact selected-after-pointer-away crops from the saved recording. The new
`PetDanceEntryImageReview` composes that bounded matcher, the enabled/disabled/
hover Play matcher, and this entry classifier. It supports explicitly labeled
saved text as well as optional local OCR; each frame preserves its text origin.
It does not automatically detect pointer location or qualify another arena/UI
size. See the [integrated checkpoint](W101_OFFLINE_QUALIFICATION_20261110.md) for
actual outcomes and the bounded OCR timeout rather than interpreting the older
fixture limitations above as the final evidence inventory.

## Validation and remaining gates

Focused tests cover saved OCR evidence, disabled/enabled button pixels, independent
readiness gates, different entry branches, wrong arena, stale bounds, missing
receipts, specialist identity/release, unknown/loading delays, user takeover,
terminal Idle versus initial Idle, and default-disabled behavior. Declared scale
transforms are synthetic regressions, not live UI-size qualifications.

The focused warnings-as-errors build/test run passed **73/73**, none skipped,
covering `PetDanceEntryVisionTests`, `PetDanceVisitTests`,
`PetDancePlayButtonVisionTests`, `PetDanceVisitReplayTests`, `RoutineGraphTests`
and `PetDancePostGameTests`. It used two logical processors and one build worker,
with no capture, game input, OCR rerun or model. The first run passed 71/73; two
synthetic enlarged-button images contained resize-generated transparent edges.
Their fixture construction now uses an opaque backing like the original game
pixels. Production opacity, similarity and margin guards were not relaxed.
This is a focused result, not the final full-suite, packaging or live result.

Before enabling a full live visit, supply independent selected-arena and Play
reference examples across declared UI sizes, current entry-cost observations, a
qualified Watch-start detector and verified specialist/result integration. Then
qualify one bounded fresh visit under the existing shared input owner. Navigation,
initial Idle exit, automatic repeated games and snack feeding remain separate work.
