# Pet Dance selected-arena evidence

The native selection matcher now distinguishes the recorded Wizard City icon's
unselected appearance from its persistent highlight after the pointer leaves.
This is a bounded offline calibration for one saved layout. It does not enable
live entry, select another arena, authorize energy use, or qualify a complete visit.

## Actual evidence

All samples come from the same supervised recording,
`.local/w101-route-study/lifecycle-01/video/capture.mp4`, SHA-256
`88329347100875165c93a777b0ad7b997d372dc73c8267f8fd4eddcc04b40317`.
The eight previously extracted frames and encoded timestamps are preserved in
`.local/parallel-recognition-20261109/selection-seek/selection-seek.json`.
No additional extraction, desktop operation, capture, model or network was used.

| Sample | Role | What is visible |
| --- | --- | --- |
| 11.500000 s | Unselected calibration | Dim first icon and disabled gray PLAY. |
| 11.850000 s | Same-recording evaluation, excluded from reference construction | Brighter first icon with Wizard City hover tooltip; its appearance differs from both references. |
| 18.466667 s | Selected-away calibration | First icon stays bright after its tooltip disappears; PLAY has the hover/press appearance. |

The first-icon crop is `(452, 485, 59, 55)` in a **1282 × 760 outer-window
frame**, ending above the tooltip. This is not a 1280 × 720 client coordinate.
The independently labeled first icon is Wizard City in this layout. Tooltip text
identifies a control; it does not alone establish that the control is selected.

The tracked fixture manifest records exact source/crop hashes, dimensions,
timestamps, roles and limitations:
`ProjectMaelstrom/ProjectMaelstrom.Tests/TestData/PetDance/Selection/manifest.json`.
The three tracked PNGs are unmodified pixel crops, not synthetic renderings.
Tests place those real crops on an explicitly synthetic blank background to
exercise the bounded matcher; those tests do not validate full-screen recognition.

## Candidate versus qualified state

`PetDanceLevelSelectionVision` compares two 32 × 24 RGB feature grids using the
existing recognizer's similarity threshold 0.94 and winning margin 0.025. It
checks only 3,245 cropped pixels for opacity and makes two feature comparisons;
there is no full-screen search. References must be opaque, equally sized and
distinguishable. The caller verifies pinned reference hashes when loading them.

`Classify(image, imageSha256, capturedMs, binding)` returns:

- `Unselected`: the icon matches the saved unselected reference.
- `SelectedAppearance`: the icon matches the selected-away reference.
- `Unknown`: unfamiliar pixels, ambiguous scores, occlusion, transparency,
  unqualified arena/layout/control geometry, or mismatched frame identity.

`SelectionVerified` is a separate result field. It becomes true only for a
selected appearance with independently supplied, same-frame proof of the Dance
instructions, Wizard City control identity, pointer away from that control, and
enabled PLAY. A missing cursor in a recording is **not** pointer-away evidence.
The 18.466667-second sample supplies a reviewed visual witness: its tooltip has
gone and PLAY has the pointer-hover appearance. Its saved input timings provide
supporting context, but exact input/video alignment was not verified and they
are not proposal-bound action receipts.

No unselected-hover negative was captured. The hovered positive sample is
therefore never used to authorize selection. The initial measured hovered sample
does not clear the existing appearance threshold; no threshold was relaxed to
accept it. This preserves the distinction between highlight, hover and selection.

## Integration contract

The API exposes `QualifiedGeometry` and `QualifiedControlBounds` for the pinned
layout. `PetDanceLevelSelectionBinding` carries image hash, capture time,
geometry, independently identified `PetDanceLevelControl`, and the three
context flags. The caller must associate the supplied bitmap with the verified
encoded source bytes; the matcher does not re-encode or hash a full frame.
Hash/time/geometry mismatch returns Unknown. Snapshot equality does not establish
live freshness: the shared runner still owns age limits and target/input gates.

For an accepted result, the caller can create `PetDanceEntryEvidence` for that
same image/time/geometry with `SelectedArena = result.Arena` and
`SelectionHighlightVerified = result.SelectionVerified`. Keep the independent
level controls and enabled-PLAY evidence. Pass the combined evidence through
`PetDanceEntryVision` and then the receipt-aware `PetDanceVisit`; never dispatch
directly from the matcher. No prior click receipt is required merely to recognize
a currently visible selected level. A proposed action still needs its own actual
dispatch receipt and subsequent observed outcome.

The original enabled-PLAY crop shows a yellow button; the selected-away sample
shows a pink/white hover/press button. The coordinator has added an optional
third reference for this actual saved appearance to `PetDancePlayButtonVision`,
plus the hashed crop and provenance in `TestData/PetDance/Review`. It reuses the
same feature matcher and unchanged confidence, opacity and size guards. The
integration review composes these matchers with existing menu OCR and
`PetDanceEntryVision`; it creates no new dispatcher or capture/input owner.
The coordinator's first focused run passed the new hover-reference and review
cases; the overall result and fixture correction are recorded below. An Unknown match must not be converted into enabled;
the same-recording hover reference does not qualify other button appearances or
fresh games. Pointer-away and control identity remain explicit annotations in
the saved-image review rather than newly implemented automatic detectors.

## Tests and remaining qualification

The coordinator's subsequent four-frame review with the current native build
completed local OCR but exposed a missing-text gap. In
`.local/w101-offline-qualification-20261110/dance-entry/report-current-ocr.json`,
the unselected frame was recognized, while the selected-away frame's OCR found
DANCE GAME, OBJECTIVE and SELECT LEVEL but missed PLAY. The existing enlarged
bottom-banner pass begins at row 608 in this 760-pixel frame; PLAY is around row
574, above that pass.

`PetDanceMenuOcrRescue` now requests one conditional extra pass over the broad
lower-center grid `(25% width, 60% height, 50% width, 25% height)` when localized
Dance and Select Level headers are recognized but menu PLAY is missing. It
preserves the existing three OCR passes, requests 3× enlargement through the
same 1.5-million-pixel/engine-dimension cap, and performs no retry loop. Already
recognized PLAY, incomplete headers, other scenes and native Idle do not trigger
the extra pass. Existing deduplication and source-coordinate mapping still apply.
This adds no inferred word, weakened match threshold, input action or new model.
Pure tests cover the saved missing-PLAY header bounds, skipped cases, broad region,
native sizes and raster limits. The integrated review subsequently recovered PLAY
from the saved selected frame and classified it as InstructionsReady. All four
declared cases passed with fresh local OCR in 0.625 seconds, retaining explicit
pointer/control annotations. See the [final checkpoint](W101_OFFLINE_QUALIFICATION_20261110.md).

New tests cover exact crop provenance, measured reference states, the withheld
same-recording hover sample, independent context gates, stale hash/time/geometry,
other arenas, shifted control bounds, transparent/occluded/ambiguous pixels,
ambiguous references and unsupported sizes. Other UI scales, a one-pixel change
in outer-frame height, translated layouts and other arenas remain Unknown.
Synthetic resize rejection establishes a boundary; it is not evidence that those
sizes are playable.

The coordinator's first focused native run passed **73 of 74 tests**. The single
failure was the reference-transparency test: the saved RGB PNG discarded alpha
when edited in place, so it never became transparent. The test now creates an
explicit 32-bit ARGB copy and asserts that its edited pixel has zero alpha before
checking rejection. Production guards and calibration thresholds are unchanged.
The final serialized suite, including the conditional OCR rescue, passed
**1,127 tests with none skipped**; scoped formatting and analyzers passed. No live
control capability was enabled. Native whole-frame rehearsal details, prior
failures and retained limits are recorded in the coordinator's qualification report.

There is one independent recording, no independent-session holdout, and no
hover-only unselected negative. The selected-away image is a calibration image,
not a test-set victory. Fresh same-layout games, explicit hover-only negatives,
other arenas and separate UI/resolution witnesses remain required before broader
qualification. No selection evidence here changes snack, Crown Shop, user
takeover, loading, input ownership or native-Idle rules.
