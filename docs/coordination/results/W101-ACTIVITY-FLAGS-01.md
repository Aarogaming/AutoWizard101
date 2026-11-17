# W101-ACTIVITY-FLAGS-01 — candidate activity flags from UI visibility

Status: **DELIVERED — offline, trigger-only; no live activation.**
Workstream: `W101-COMBAT` · owner: root · recorded 2026-11-17 during reconciliation.

## Delivered

- `Utilities/W101ActivityFlags.cs` — implements the accepted `UI-SCHEMA-CATALOG-01` predicate: two visible nodes (`DiegoMeter`, `DuelTimerWindow`), ≥3 consecutive samples over ≥600 ms, target binding, image pin, not loading, not native Idle. A failed gate resets the streak; hidden dormant nodes never fire.
- `Utilities/W101ActivityFacts.cs` — publishes flag state to the shared store as **Candidate-only** (`activity.diego.visible`), including an explicit `"false"` with reason `flag-inactive` so observers distinguish "seen inactive" from "never observed".
- Tests: 9 flag-contract + 4 fact-publication cases; full native suite green at the current checkpoint.

## Boundaries

A flag is a **trigger signal** for a future react system — no input dispatch, no gameplay qualification, no fact promotion. It clears on loading/focus loss/target change and is never authoritative for a game outcome. No live Diego session was captured or qualified for this claim; the predicate comes from the accepted design brief.

## Reconciliation note

This result document was missing when the claim was reviewed; the code and tests were already committed. Recorded here so the claim's evidence path exists before release.
