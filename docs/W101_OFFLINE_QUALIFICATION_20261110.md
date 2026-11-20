# Saved Dance selection, Conjuration collection and shared operations

Checkpoint: 2026-11-10; work spans the night of November 9–10 America/New_York. Three
subagents implemented and independently reviewed the parallel tracks; the
coordinator serialized native validation. No game inputs, live capture, new
dependencies, model services, installation or file cleanup were performed.

## What is reusable

The [shared runner](ROUTINE_GRAPHS.md) owns graph transitions, receipts, target/
geometry/visit binding, deadlines, takeover and replay. Capture, recording, action
registration and input ownership remain existing shared services. Games supply
their recognition examples, rules, controls and outcome definitions. The older
specialized paths are not all migrated, and the new operations are not yet
user-editable live routine clips.

[W101 shared operations](W101_SHARED_OPERATIONS.md) now compile landmark-based
navigation, sigil interaction, Cancel/menu navigation and native Idle through that
runner. Spin, Step, Routine and the other existing Auto-Idle choices are preserved.
There is no second controller or dispatcher. A 19-event synthetic rehearsal covers
seven proposed/released actions, loading/unknown waits and observed completion.

Review found that a held `Progress` label with a later timestamp could otherwise
acknowledge another movement pulse. The shared edge contract now supports
`requires_outcome_binding`, and W101 motion opts in. An outcome must identify its
exact pending proposal; old or absent bindings cannot authorize continued motion.
The future visual adapter still has to measure new progress rather than copying
the current proposal ID onto an unchanged scene. Actual navigation remains
unqualified.

## Dance selection result

The saved-layout matcher compares bounded, hashed Wizard City icon references.
It distinguishes appearance from an entry-ready state. Identity, instructions,
pointer-away evidence and enabled Play must agree on the same image. The Play
matcher now accepts a separately pinned hover/press appearance, retaining existing
quality/margin limits.

Actual local OCR initially missed Play on the bright enabled/hover frames. The
existing enlarged bottom-banner region began below that button. A conditional
lower-center grid pass now runs only when Dance and Select Level are recognized
but Play is missing. It uses the existing 1.5-million-pixel raster limit and does
not add continuous OCR or full-screen model inference.

The final image → local OCR → selection/Play matching → entry-classifier command
passed all four declared saved-image cases in **0.625 seconds**, including process
startup and report writing:

| Encoded source time | Measured selection | Entry result |
| --- | --- | --- |
| 11.50 s | Unselected | InstructionsUnselected; Play disabled |
| 11.85 s | Unknown hovered appearance | InstructionsUnselected; Play enabled |
| 18.466667 s | SelectedAppearance | InstructionsReady; Play enabled |
| 18.60 s | Unknown, panel closed | Unknown; no action assumed |

This is **same-recording calibration**, not independent-session accuracy or a
full autonomous visit. Pointer-away and control identity are explicit per-frame
annotations; they are not automatically detected. The positive frame is also a
reference source. The supported image is the recorded 1282×760 outer window;
other arenas, UI scales and geometry remain unqualified. Encoded PTS does not
establish input/capture-clock alignment. Readiness does not approve energy cost,
dispatch a click or prove gameplay start.

Reproduce from repository root with the current build target and a new output:

```text
dotnet ProjectMaelstrom/ProjectMaelstrom/bin/Release/net9.0-windows10.0.22621.0/ProjectMaelstrom.dll --review-pet-dance-entry-images .local/w101-offline-qualification-20261110/dance-entry/manifest-current-ocr.json <new-report.json>
```

`--review-pet-dance-entry-images` accepts 1–16 explicit frames, pinned reference
hashes and source-clock provenance. It retains verified byte snapshots, caps PNG
geometry/file/total bytes, bounds OCR to 30 seconds and writes a new bounded report.
Optional supplied text records `manual-annotation` or `saved-ocr` per frame and
avoids OCR initialization. Cancellation keeps a reader's bitmap alive until that
reader exits while allowing the caller to return. No desktop interface is present.
Exit 0 means declared image expectations matched, not live qualification.

Three early launch attempts accidentally targeted a stale `win-x64` build that
did not contain the new command. Their process deadlines terminated them; they
provide no recognition evidence. Current `TargetPath` was subsequently queried
from MSBuild and the actual assembly hash recorded. Fresh OCR on the correct
pre-repair build completed but failed the selected case; its report is preserved.
See [build retention](BUILD_ARTIFACT_RETENTION.md).

## Conjuration preparation

The [collection plan and importer](CONJURATION_CARD_COLLECTION.md) prepare an
editable draft from explicit existing PNG recordings. Originals, hashes, timestamps,
geometry and metadata are preserved. No recorder starts and no labels or input
receipts are invented.

The actual prepared draft contains **two recordings, 17 frames, one unique PNG
and 16 duplicates**. That PNG is instructions page 1. It cannot supply card faces,
hidden backs, transition evidence or an independent held-out episode. The draft
reports missing phases, cards, rules, client geometry, alpha and timing evidence.

Cross-review repaired two false-completeness cases: `new_board` reusing a previous
identity, and identical images carrying contradictory card identities/states/
positions/group sizes. The checker now checks board continuity and excludes those
conflicts. Even a satisfied curation checklist never enables gameplay or proves
recognition accuracy. Export from curated drafts into qualified replay manifests
remains a later integration step.

## Verification and preserved evidence

| Check | Final result |
| --- | --- |
| Complete native suite, warnings as errors | **1,127 passed; zero skipped** |
| Scoped native formatting and analyzers | Passed on 12 changed files |
| Combined Conjuration suites | **80 passed; zero skipped** |
| Collection-only suite | 20 tests included in the combined total |
| Actual four-image review with fresh local OCR | All four declared expectations matched; zero inputs |
| Project-board checks | 30 workstreams valid; 22 validator tests passed |

Native tests used two logical workers and one build worker; Python vision tests
limited numeric libraries to one thread. Native test duration was about seven
seconds and the combined Python run about 1.8 seconds. These figures are offline
test/review timings, not capture FPS, gameplay reaction latency or general UI
accuracy.

The initial focused run passed 73/74: a transparency test tried to add alpha to
an RGB PNG. Its fixture now explicitly creates an ARGB bitmap and verifies zero
alpha. Production opacity checks and matching thresholds were retained.

Local evidence under `.local/w101-offline-qualification-20261110/`:

- `tests/full-qualification-ocr.trx`: final native test receipt.
- `dance-entry/manifest-current-ocr.json`, `report-current-ocr-rescue.json` and
  `current-ocr-rescue-launch.json`: actual image/OCR results and assembly hash.
- `dance-entry/report-annotated-current.json`: separate manual-text rehearsal.
- `dance-entry/report-current-ocr.json`: preserved pre-rescue failure.
- `conjuration-card-collection/`: original-byte draft, provenance and initial
  completeness report; final checker remains reproducible from annotations.
- `validation.json`: integrated source fingerprints and final check totals.

Next data gates: independent Dance selection/hover examples and qualified current
client geometry; actual Conjuration boards and transitions; motion/landmark/control
verification before live operation binding. Source changes are not packaged or
installed. The board records these limits rather than declaring either game done.
