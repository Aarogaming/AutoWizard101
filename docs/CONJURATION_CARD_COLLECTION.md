# Preparing Conjuration card data

This is an **offline collection plan and annotation preparer**. It does not open
Wizard101, start a recorder, decode a video, click cards or enable a routine.
The [data-only plan](../tools/fixtures/conjuration_collection/plan-v1.json) describes
what a later authorized collection session needs to preserve.

The current two saved recordings contain **17 PNG frames but one unique image**:
Conjuration instructions page 1. They do not contain a playable board, hidden-card
references, matched/inactive cards or a complete transition. The decorative cards
on the instructions page must not become the recognition gallery. Repeated
timestamps or filenames do not turn identical pixels into more state coverage.

## Prepare the existing samples

From the repository root, with the existing Python interpreter:

```powershell
python tools/conjuration_collection.py prepare `
  --recording .local/conjuration-samples/recordings/20261108-175042-fd787863/recording.json `
  --recording .local/conjuration-samples/recordings/20261108-175243-79c9bad5/recording.json `
  --output .local/conjuration-card-collection-01 `
  --evidence-kind recorded --surface-kind outer_window
```

Choose a new output directory each time. The tool refuses an existing destination
and validates all supplied snapshots before publishing the draft. The selected
parent directory must already exist. The evidence/surface arguments are explicit
**caller declarations**, not independent proof. Defaults are `unknown`.

The new workspace contains:

- `annotations.json`: editable curation fields with labels, board observations,
  group sizes and input receipts initially unknown/empty; every episode begins
  with an unassigned split.
- `completeness.json`: the initial coverage/checklist report. Insufficient data
  remains a successful *preparation*, not a complete dataset.
- `images/<sha256>.png`: byte-identical original images, copied once per hash.
- `metadata/`: byte-identical original recording manifests, each with its hash.

The originals remain unchanged. Exact hashes, original filenames/frame indices,
dimensions, timestamps, start date, source workspace and recording completion are
preserved. Recorder `State` strings are not adopted as manual ground truth.

After reviewing annotations, check the current draft:

```powershell
python tools/conjuration_collection.py check .local/conjuration-card-collection-01/annotations.json
```

`check` prints a fresh report and does not overwrite the initial report or images.
Exit code 0 means the input was structurally checked; inspect
`collection_checklist_met`, `gaps` and `excluded_hashes` for actual sufficiency.
No report, including one with a satisfied checklist, enables gameplay.

## What a useful collection must show

Use one stable source geometry and record the actual game resolution/UI scale.
Do not change user settings merely to satisfy a fixture. A later authorized
recorder run should retain the selected game window and source timestamps, along
with opt-in input receipts where available. Keep actions, observed images and
outcomes separate; do not reconstruct missing clicks from the intended sequence.

| Stage | Evidence to retain | Qualification still needed |
| --- | --- | --- |
| Instructions | Every available instructions page and relevant level/rules text | Current group sizes, special cards and inactive display |
| Preview | Full exposed board, boundaries and all slot centers; temporally distinct stable observations | Actual preview phase and card identities |
| Hidden | Same board after turn-over, with unchanged geometry | Positive back-of-card references; unknown is not hidden |
| Flip | Before input, its receipt, animation/uncertainty, stable reveal | Receipt-to-frame timing and visible identity |
| Mismatch | Nonmatching selection through its actual resolved state | No early outcome claim during animation |
| Match | Matching selection through its actual resolved state | Group size for this board; no assumed disappearance |
| Removed/inactive | Positive visual marker, **only if current rules support it** | Empty background never proves a match |
| Result | Actual timeout/result/reward screen | Board cleared and game won remain separate facts |
| New board | Fresh shuffle/level with a new local board ID | Requalify geometry and group size; do not reuse old memory |

The plan follows saved [minigame research](MINIGAME_RESEARCH.md), not invented
current-client rules. Keep bonus behavior, timers and later-board group sizes
unknown until instructions and observations establish them. Do not force an
inactive state into a game that uses another outcome representation.

Assign whole independent episodes to `calibration` or `held_out`. Build references
and choose thresholds from calibration only. Reserve a fresh episode for the
held-out check; adjacent frames of the same playthrough are not independent
episodes. The preparer does not choose the split automatically.

## Annotation and completeness contract

The module [conjuration_collection.py](../tools/conjuration_collection.py) exposes:

```text
build_draft(recording_paths, evidence_kind="unknown", surface_kind="unknown")
    -> (draft, immutable_byte_snapshots)
check_collection(draft, directory, snapshots=None) -> report
prepare_recordings(recording_paths, new_output, evidence_kind=..., surface_kind=...)
    -> initial_report
```

Evidence/surface parameters and `snapshots` are keyword-only. Preparation accepts
the existing `Version: 1` PNG recording contract (`Id`, `Complete`, `Frames`, and
each frame's `File`, `TimeMs`, `Width`, `Height`, `Sha256`). It reads only explicitly
listed files within their recording directory, never scans a whole recording
tree or video. Oversized/video-only recordings require a separately reviewed
bounded still-frame export; this tool does not silently sample or rewrite them.

Review fields are `unreviewed`, `manual`, or `recognizer`. Only manually reviewed,
declared-recorded images can count toward recorded-stage coverage. `stage` is a
curator label, not an automatic phase detector. An optional `board` uses the
existing `Board`/`Card` dictionary contract and needs an explicit `group_size` for
that board. Unknown cards remain unknown; centers must lie in the source image
and, when supplied, the client rectangle. Preliminary match/mismatch labels remain
manual claims; collection validation does not establish game success.

Input receipts are supplied records with `receipt_id`, original `time_ms`, nullable
original `proposal_id`, `status` (`dispatched`, `rejected`, `unknown`), `origin`
(`manual_observation`, `maelstrom_receipt`, `unknown`), and an action description.
Frames link them by `receipt_ids`. Human input may have no proposal ID; do not
invent one. Missing, late or uncertain dispatch evidence leaves a checklist gap.
The preparer never generates a receipt from a card label.

The rules section keeps `removed_state` as `unknown`, `positive_marker` or
`not_used`, with links to manually reviewed instructions. Unknown rules block the
checklist. A supported positive marker needs collected inactive-state examples;
an explicitly supported `not_used` decision removes that conditional stage.

Coverage counts distinct PNG hashes per stage and split. Conflicting stages or
card identities/states/slot geometry/group sizes on identical pixels, shared
calibration/held-out hashes and hashes explicitly known to be synthetic are
excluded. Comparison ignores episode-specific board IDs and revision numbers;
those cannot explain different card labels on the same image. Exact duplicate
copies count once even across
recordings. Hashing does **not** detect visually identical re-encodings or prove
that differently named recordings are independent; manual source review remains
necessary. Synthetic sources and recognizer guesses never supply manual recorded
coverage. Source declarations are not independently verified provenance.

Within an episode, a new-board transition must establish a fresh board identity;
the current or a retired ID cannot masquerade as a new board. Stable repeated
new-board observations may retain that newly established ID. Geometry and group
size stay fixed within one board, and its annotation revisions must increase.

The checklist also reports missing board/card labels, input evidence, UI scale,
client rectangle, timestamp qualification, alpha opacity and incomplete recording
metadata. Unknowns are preserved instead of autofilled. Even a satisfied curation
checklist always reports `recognition_accuracy_verified: false`,
`gameplay_outcome_verified: false`, `runtime_enabled: false` and zero desktop inputs.

## Geometry, alpha and resource bounds

The original image size is measured from its PNG header. `surface_kind` separately
declares client versus outer-window capture; `client_rect` is an explicit integer
rectangle inside every source frame. Capture dimensions are not an invented
in-game resolution setting. `recording-TimeMs-unqualified` is the default clock
basis: existing `TimeMs` numbers do not establish capture-clock alignment by
themselves. The checker will not rewrite those original times.

This tool uses only Python's standard library. It checks PNG chunks/CRCs and
dimensions without decoding compressed pixels. An alpha channel or transparency
chunk is marked `present-unmeasured`; this is not proof of opacity. The existing
Conjuration screenshot's partial alpha therefore remains unresolved here. Any
future crop/normalization must keep original hashes and separately describe the
derived pixels. Preparation performs no image conversion or background removal.

Limits are eight explicit manifests (1 MiB each), 64 listed PNG frames, 8 MiB per
PNG, 64 MiB total source-image reads, and 4,194,304 pixels per image with neither
dimension above 8192. Checks retain bounded immutable byte snapshots and no
inference queue. The input and annotation JSON readers reject duplicate fields.

## Validation and next integration

```powershell
python -B -m unittest discover -s tools/tests -p test_conjuration_collection.py -v
```

The 20 tests passed in 0.430 seconds during development. They include the local
17-frame audit (one unique image, 16 duplicates, insufficient), duplicate and
split contamination, synthetic exclusions, source tampering, traversal, clocks,
alpha metadata, receipts, positive **mocked** curation, resource bounds and
overwrite refusal. The historical-image test skips explicitly if those local
research recordings are unavailable. No game was recorded or played.

After actual board evidence is collected and reviewed, export its labels to
[the observation replay](CONJURATION_OFFLINE.md), then build reviewed reference
images and run [saved-image recognition](CONJURATION_RECOGNITION.md). Both retain
proposal/receipt/outcome separation and remain offline. Automatic conversion from
this editable draft to a qualified replay manifest is a later integration step;
current commands deliberately do not turn manual guesses into executable packs.
