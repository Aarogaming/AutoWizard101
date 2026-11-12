# CONJ-PERCEPTION-01 — offline observations and image rehearsal

Status: implemented and tested offline; live recognition is not qualified. Coordinator-owned integration, 2026-11-09 session.

The original fixture brief is implemented in `tools/conjuration_observation_fixtures.py`, its tests, and `tools/fixtures/conjuration_observations/`. The separate recognition worker delivered `conjuration_vision.py`. `conjuration_recognition_replay.py` now connects saved image recognition to the same observation loader and `MemoryMatch` helper.

## Contracts and usage

The observation fixture has an explicit schema version, fixture/board identity, clock origin, group size, synthetic flag and ordered events. Observations contain complete card snapshots; dispatch receipts and board resets are separate events. Hidden/removed/unknown cards cannot supply invented face identities. Reset cannot reuse a prior board ID. Optional expectations check proposed slots and observed outcomes without creating receipts.

The loader reuses the strict shared JSON decoder and existing memory helper. It limits JSON to 1 MiB, 2,000 events, 64 cards, and source images to 64 files/64 MiB total with 8 MiB per file. Recorded annotations require contained, hash-matched source files. Synthetic labels may omit image evidence; actual synthetic image files retain their real hashes and synthetic provenance. Label replay hashes image bytes but does not interpret the pixels.

```text
python tools/conjuration_observation_fixtures.py tools/fixtures/conjuration_observations/synthetic-pairs.json <new-report.json>
python tools/conjuration_observation_fixtures.py tools/fixtures/conjuration_observations/synthetic-triples.json <new-report.json>
```

For pixel interpretation, use the existing isolated Dance environment:

```text
.local/envs/dance/Scripts/python.exe tools/conjuration_recognition_replay.py <image-manifest.json> <new-report.json>
```

The image manifest contains `schema_version: 1`, `session_id`, `synthetic`, `board_id`, `group_size`, embedded `profile`, `references`, and `events`. Each reference key maps to an image filename and encoded-file SHA-256. Frame events supply image/hash, revision, timestamp and phase; action receipts and resets use the observation fixture vocabulary. Phase and matching-group-size identification remain separate qualifications, not inferred from the card classifier.

The profile defines normalized board/card regions and a frozen gallery. A recognizer report contains per-card similarity, competing-match margin, local patch error and temporal stability. An uncertain match becomes unknown immediately. Blank background cannot stand in for a matched/removed card.

The image adapter limits work to 256 events/128 frames and 1,048,576 decoded reference pixels in total; the recognizer additionally bounds each source frame to 4,194,304 pixels. It verifies and decodes the **same immutable encoded bytes**, retaining original file hashes separately from normalized descriptor hashes. The derived label replay consumes those same snapshots. A regression swaps an image on disk between verification and decoding: the existing run remains bound to its original snapshot, while a new run rejects the changed file.

Reports are created at new paths, never overwrite earlier reports, declare zero desktop dispatch and `live_qualified: false`. Matched groups or a cleared supplied board do not imply a whole-game win, score or reward.

## Evidence

- Observation fixtures: pair/triple simulations, 11 tests; explicit receipts, monotonicity, geometry/reset, hidden-label rejection, source hashes, path containment, size limits and schema consistency.
- Recognition: 22 tests; shuffled layouts, six scales, ambiguity, occlusion, temporal stability, changed geometry and retired-board identity handling.
- Image-to-memory adapter: 6 tests; completed synthetic episodes, missing receipts, provenance, source snapshots and malformed manifests.
- Existing memory helper: 21 tests. All four suites passed together: **60 tests**, none skipped, in the existing isolated environment with one numeric-library thread.

A self-contained generated example is saved at `.local/parallel-recognition-20261109/conjuration-pixels/manifest.json`. Its report recognizes 16 synthetic frames, proposes four flips, consumes four supplied simulated receipts and observes two matched groups. It sends no inputs. The same episode passes at 400×240 and 800×480 in tests; the wider scale cases belong to recognizer tests, not current-client qualification.

The saved real collection has 17 identical instructions frames and no actual board/card sequence. These supply a negative case only. Their original alpha channel is not accepted as opaque game input; the diagnostic RGB view is explicitly derived. No real positive templates, card-state accuracy rate, phase detector, board layout or group-size rule is qualified for the current client.

Next: preserve a short actual board/preview/flip/match sequence, label independent examples, qualify its regions and back/face/inactive appearance, and evaluate a held-out sequence. Do not enable live clicks from synthetic success. See [the evidence audit](PARALLEL-TRANSITIONS-RECOGNITION-20261109.md) and [recognition contract](../../CONJURATION_RECOGNITION.md).
