# CONJ-PERCEPTION-01 — card-observation fixtures before recognition

| Field | Value |
| --- | --- |
| Parent workstream | `W101-CONJURATION` |
| Task status | Implemented offline; see result below |
| Owner | Coordinator integration |
| Mode | Lightweight offline Python/JSON; no image model or game |

Follow [the collaboration contract](../EXTERNAL_AGENT_GUIDE.md). Claim the exact
paths below before editing.

The fixture work in this original brief is implemented. The coordinator also
connected saved-image recognition to it. See [the result](../results/CONJ-PERCEPTION-01.md)
before starting another task; do not duplicate these modules. Current-client
positive card evidence and live qualification remain open on `W101-CONJURATION`.

## Outcome and inputs

Create a versioned labeled-observation fixture format and validator that can feed
the existing [memory helper](../../../tools/conjuration_memory.py). Read its
[tests](../../../tools/tests/test_conjuration_memory.py),
[offline contract](../../CONJURATION_OFFLINE.md),
[minigame research](../../MINIGAME_RESEARCH.md), and
[old capture limits](../../W101_KEEP_ACTIVE_LIVE_TEST.md).

There is no qualified current-client Conjuration board dataset in this brief.
The old `.local/conjuration-samples` path is a lead to inspect only if supplied
by the coordinator; its existence does not establish readable gameplay. Do not
claim that settings/instruction frames demonstrate a board.

## Proposed ownership

- `tools/conjuration_observation_fixtures.py`
- `tools/tests/test_conjuration_observation_fixtures.py`
- `tools/fixtures/conjuration_observations/schema-v1.json`
- `tools/fixtures/conjuration_observations/synthetic-pairs.json`
- `tools/fixtures/conjuration_observations/synthetic-triples.json`
- `docs/coordination/results/CONJ-PERCEPTION-01.md`

The memory helper, its existing tests, research docs, C# app, central index and
workstream inventory are read-only. If the helper needs a change, report it to
its owner with a minimal regression.

## Scope

Define complete snapshots with local board/slot IDs, explicit qualified group
size, source revision/timebase, phase, card centers, visible identity or unknown,
annotation origin and image/source hash when an actual image exists. Unknown
source fields remain null. Synthetic JSON has `synthetic: true` and no invented
image hash or current-client label.

Reuse `Board`, `Card`, `MemoryMatch` and their validation contract. A fixture
loader may translate records into those types and replay the decisions without
dispatch. Preserve supplied proposals, receipts and outcomes as distinct data.
Bound a fixture to 1 MiB, 64 cards per board and 2,000 observations; reject invalid
files before partial output. Do not fetch image URLs or execute embedded code.

Non-goals: OCR, clustering, training, screenshot collection, live clicking,
automatic board-size inference, bonus-card semantics, rewards or timer constants.

## Acceptance

- Pair and triple synthetic fixtures load and replay through the existing helper.
- Hidden cards cannot supply future identities; unknown observations cannot be
  rewritten as hidden or removed cards.
- Duplicate slots/positions, contradictory identities, stale revisions, changed
  board geometry without reset, bad source hashes and oversize inputs fail clearly.
- An absent image remains absent. No fabricated screenshot or calibration result.
- Tests distinguish same-board replay from new-board evaluation and preserve a
  source digest for each loaded fixture.

Run only the focused standard-library unittest module and existing formatter/lint
tools, one file at a time where required by the environment. Return the exact
fixture count, test results, and missing current-client examples needed next.
