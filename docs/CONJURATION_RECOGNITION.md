# Conjuration recognition from saved images

Status: **offline candidate, synthetic positive fixtures only**. The recognizer
feeds the existing [memory decision helper](CONJURATION_OFFLINE.md); it has no
capture, input, network, model or runtime-enabling code. It is not a live player
and is not included as an enabled installed-game routine.

## Evidence and reuse

The existing [minigame research](MINIGAME_RESEARCH.md) supplies the memory-game
baseline. `tools/diego_vision.py` was reviewed for local gallery, bounded image,
ambiguity and crop patterns. Its detector searches exact-size subject templates,
so reusing that implementation directly would neither provide fixed card slots
nor preserve Conjuration's separate hidden, revealed and inactive states.

The [saved-evidence audit](coordination/results/PARALLEL-TRANSITIONS-RECOGNITION-20261109.md)
found 17 Conjuration PNGs representing one byte-identical instructions screen.
The decorative card strip is not a playable board. There are **no qualified
current-client card identities, slot coordinates, hidden/inactive references or
group-size labels** in those samples.

The checked-in [profile](../tools/fixtures/conjuration_vision/synthetic-profile.json)
therefore describes a procedural four-card test layout. Its shapes, colors and
coordinates are not Wizard101 art or measurements. The
[provenance file](../tools/fixtures/conjuration_vision/provenance.json) pins the
optional real instructions negative and describes the test-only RGB conversion.
Its original alpha ranges from 7 to 255, so the ordinary strict loader rejects it
as partially transparent. A separately hashed, in-memory RGB diagnostic view
produces four unknown slots. No source image is overwritten and this does not
qualify recognition of an actual board.

## API and data boundaries

The implementation is [conjuration_vision.py](../tools/conjuration_vision.py).
Pillow and NumPy are loaded lazily; the existing `.local/envs/dance` environment
already provides them. No dependency is added to the desktop application.
`decode_image(payload)` accepts the exact bounded bytes already checked by an
offline replay adapter, so the verified digest and interpreted pixels can refer
to one snapshot. `load_image(path)` performs a bounded read and delegates to it.

```python
from dataclasses import asdict
from conjuration_vision import ConjurationVision, VisionProfile, load_image

profile = VisionProfile.load("qualified-local-profile.json")
# A caller-supplied mapping from each profile reference key to its frozen image.
recognizer = ConjurationVision(profile, reference_images)
observation = recognizer.observe(
    load_image("saved-frame.png"),
    board_id="local-board-001",
    revision=1,
    time_ms=1250.0,
    phase="preview",
)
report_record = asdict(observation)
memory.observe(observation.board)  # Existing separately configured MemoryMatch.
```

This example is a contract, not a ready current-client profile. The caller must
bind `reference_images` and `memory`; the helper does not discover references or
infer a group size. First positive pixels are deliberately still unknown until
enough repeated evidence arrives.

`VisionObservation` contains the existing `Board` and `Card` records plus source
time/size, profile ID, a `caller-supplied-not-detected` phase marker, reference
descriptor hashes, and per-slot diagnostics. `reference_hashes` means SHA-256 of
the bilinear-normalized 32 × 32 RGB descriptor, encoded as row-major little-endian
float32 values divided by 255. These are not hashes of original reference files;
the caller preserves those separately. Each slot retains the best candidate,
similarity, competing-label margin, worst patch error, stable sample count/span
and reason. Candidate fields are diagnostic guesses: only `Card.state` and
`Card.identity` contain admitted observations. Unknown cards carry no identity.

The caller owns source-file hashes, source-frame identity and timestamp evidence,
phase qualification, group size, board resets, run limits, and any later proposal
or dispatch workflow. Increasing revisions do not prove fresh capture; replay
must not relabel the same captured frame as multiple new source observations.
This module returns observations only. It never acknowledges a proposed click or
manufactures a game outcome.

## Profile and recognition rules

The version-1 profile contains:

- `profile_id`, explicit `qualification` (`synthetic` or `recorded_candidate`),
  reference image size and a normalized board rectangle relative to the full image.
- Two to 64 unique, non-overlapping slot rectangles relative to that board.
  A shared gallery requires consistent slot aspect ratios. The rectangles are
  supplied calibration, not detections or permission to click those coordinates.
- Two to 64 gallery entries with `key`, `state`, and optional visible `identity`.
  Hidden and face-up references are required. A removed/inactive state requires
  its own explicit distinctive reference; an empty or unreadable patch is never
  interpreted as a removed card. Uniform reference backgrounds are rejected.
- Bounded matching settings. Unknown or duplicate JSON fields, overlapping
  geometry, boolean schema versions, invalid numbers and hidden-card identity
  labels are rejected. Profile files use the repository's strict JSON decoder.

Each current slot is normalized to 32 × 32 RGB pixels and compared with the
frozen gallery. Whole-patch mean absolute error supplies similarity; a 4 × 4
patch grid catches localized missing or occluded regions. Labels compete by
state and identity, so duplicate examples of one label do not defeat the margin.
Near-identical references with different labels remain ambiguous.

Defaults require similarity at least 0.96, margin at least 0.03, and worst-patch
error at most 0.14. These are development settings tested on the synthetic shapes,
not thresholds qualified against real Conjuration cards. A positive result needs
at least two observations spanning 50 ms; gaps over 250 ms restart qualification.
Larger inter-observation image changes restart the streak. Any unclear frame
immediately outputs unknown and clears that slot's previous confidence.

`preview` and `active` are supplied stable contexts. `animation`, `unknown` and
`result` withhold every card label even when a template resembles the pixels.
The recognizer does not infer these phases, timer, scoring, rewards, bonus cards
or game completion. Recognition of an inactive-looking card is image evidence;
the memory helper still verifies whether a previously revealed matching group
actually transitioned together.

Uniform source scaling is supported within a 1.5% source-aspect tolerance.
Card centers are emitted in the actual source image's pixels. A size change under
the same board ID raises an error because the memory helper binds geometry;
the caller must explicitly establish a new board identity. Retired identifiers
cannot be reused to restart revision or geometry history. A recognizer accepts
at most 128 distinct board IDs in one bounded session. Distorted aspect ratios
yield unknown, and independently changed UI layout requires a new qualified ROI
profile. This is not arbitrary board detection or scale-free scene understanding.

## Bounds and validation

Saved images are limited to 16 MiB encoded, 4,194,304 pixels and 8192 pixels on
either axis. Reference crops are limited to 262,144 pixels. Native card crops
must be at least 12 × 12 pixels. The profile is limited to 64 KiB. The recognizer
holds only the frozen gallery and one current stability descriptor per slot;
it does not retain a recording or accumulate an inference queue.

Run the scoped suite with the existing environment:

```text
.local/envs/dance/Scripts/python.exe -B -m unittest discover -s tools/tests -p test_conjuration_vision.py -v
```

All 22 scoped tests passed in 0.408 seconds, including the available real negative.
They cover six whole-frame scales (200 × 120 through 1200 × 720),
24 shuffled synthetic boards, stable preview-to-memory integration, ambiguous
identities, explicit inactive evidence, missing/shifted/covered cards, invalid
geometry, stale clocks, corrupt images and uncertainty recovery. No live process
or desktop participates. The real negative test skips explicitly if that local
research file is absent; synthetic positives are always exercised when the
existing optional imaging dependencies are available.

A small single-process development check, using four slots and four references
for 60 observations, measured a mean 0.90 ms at 400 × 240 and 6.82 ms at
1280 × 768. These are in-memory synthetic observations on this machine, excluding
file decoding, capture and dispatch; they do not establish live throughput or a
64-slot/64-reference performance guarantee.

The next gates are a separately authorized set of real board/preview/animation/
outcome images, explicit current rules and phase labels, reviewed per-layout
references, and held-out replay with confusion/unknown counts. Only after those
pass should a native adapter bind admitted observations to the shared scheduler,
target/focus/takeover checks, finite actions and observed outcomes. No production
control capability is enabled by this offline profile.

[Card-data collection preparation](CONJURATION_CARD_COLLECTION.md) now supplies a
bounded plan and importer for selected existing PNG recordings. It preserves
originals, exposes duplicate-only captures, separates whole-episode calibration
and held-out data, and checks the editable annotation draft. It launches no
recording and does not turn a structurally complete checklist into recognition
accuracy. Actual card images remain the next data prerequisite.
