# W101-NAV-LANDMARKS-01 — evaluate route landmarks without movement

| Field | Value |
| --- | --- |
| Parent workstream | `W101-NAVIGATION` |
| Task status | Proposed; unclaimed |
| Owner | Unassigned |
| Mode | Text/JSON evidence evaluation; no desktop or video batch processing |

Follow [the collaboration contract](../EXTERNAL_AGENT_GUIDE.md). Claim only the
paths below; do not modify route/runtime code owned by another contributor.

## Outcome and inputs

Create a compact landmark rubric and evaluator for already supplied annotations.
Read [destination navigation](../../W101_DESTINATION_NAVIGATION.md),
[route demonstration requirements](../../W101_ROUTE_DEMONSTRATION.md), and the
[actual human demonstration](../../W101_DEMONSTRATION_20261108.md).

The documented local recording exists under
`.local/w101-route-study/human-route-01/`, but this task does not authorize decoding
the whole video or inventing exact boundaries from coarse contact sheets. Its
observed route includes the Commons bridge, Pet Pavilion entrance/loading, and
Dance sigil. The Shopping District tunnel is a distinct landmark. The ordinary
map is informational; destination-label clicks are not teleportation.

## Proposed ownership

- `tools/w101_landmark_evaluation.py`
- `tools/tests/test_w101_landmark_evaluation.py`
- `research/w101-navigation/landmark-rubric-v1.json`
- `tools/fixtures/w101_navigation/synthetic-observations.json`
- `docs/coordination/results/W101-NAV-LANDMARKS-01.md`

Existing route recordings, study receipts, movement primitives, game packs,
navigation research, generic graph/replay code and central docs remain read-only.

## Scope

Evaluate at most 128 supplied landmark observations per input: local landmark ID,
zone/world context, source time and clock, evidence location/hash, confidence and
camera/mount/UI context. Return whether evidence supports a named transition or
arrival, remains unknown, or contradicts the route context. Distinguish a proposed
route, human demonstration, same-video replay, and independently observed arrival.

Do not infer positions, distances or key durations absent from the evidence.
An unobserved landmark is unknown, not absent. Loading is a pause/transition,
not progress or arrival. If current annotated fixtures are unavailable, complete
the rubric and explicitly synthetic tests; report the exact annotations needed.

Non-goals: pathfinding, movement dispatch, navigation by memorized key durations,
memory-coordinate teleportation, enemy detection, game-installation inspection,
new recordings or live route qualification.

## Acceptance

- Wrong world/zone and Shopping District versus Pavilion cannot validate arrival.
- Unknown/loading frames cannot fabricate a successful transition.
- Changed camera/mount or unqualified capture geometry is disclosed, not ignored.
- Out-of-order times, mismatched source hashes and stale reused observations fail.
- Same-recording evidence is never labeled an independent successful run.
- Outputs retain exact supporting observations and do not contain executable
  movement commands or enable a routine.

Use focused standard-library tests. Return a small evidence table, unresolved
landmarks and the future capture/annotation request; do not request user gameplay
or take desktop control from this task.
