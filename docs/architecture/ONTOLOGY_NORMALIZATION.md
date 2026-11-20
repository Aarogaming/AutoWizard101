# Ontology normalization - primitives and invariants

Status: **ANCHOR - Phase 1 census lives in [REPOSITORY_CENSUS.md](REPOSITORY_CENSUS.md).**
Date: 2026-11-18 - owner: root - claim: `ONTOLOGY-CENSUS-01`

This document records the shared reality model that AutoWizard101 (legacy teacher)
and Maelstrom (runtime) are being normalized against. It is an interpretation aid
and an audit surface, not a refactor plan: nothing here authorizes a rewrite.

## The core invariant: Facts != Authority != Actions

Three roles, never collapsed:

| Role | Responsibility | Examples in this tree | May it execute change? |
|------|----------------|----------------------|------------------------|
| **Facts** | Observe and store reality | `W101FactsStore`, `W101BackgroundReader`, `W101ActivityFlags`, UI-tree and memory observations | **No.** A fact never authorizes an action. |
| **Authority** | Permit or restrict transitions | `ExecutionPolicy` boundaries, `ManualCaptureHost`/`ManualCaptureController`, `OperationFocusBudget`, recovery holds, SAFE/NORMAL mode | **No.** Authority decides; it does not act. |
| **Actions** | Execute transitions | `PathFollower`, `PetDanceProductionRuntime`, routine-graph transitions, `GameInputLease` dispatch | **Only** while holding the single-owner lease and a fresh authority check. |

Enforcement points already in code (verified):

- Facts publish as `Candidate`/`Observed` only; the facts-watch CLI note tells
  consumers "candidate values are not action authority" (`MaelstromCli.cs`).
- Authority gates live in `ManualCaptureHost`, `OperationFocusBudget` and the
  recovery latch; they return decisions, not input.
- Actions require `GameInputLease` with `CanDispatch`; recovery or focus loss
  fails closed before any dispatch.

An action reachable without passing through authority, or a fact treated as
permission, is a **leak** and belongs in the leak register below.

## The six primitives

Every subsystem should reduce to one or more of these. A subsystem that cannot
be explained through them is audited, not rewritten.

| Primitive | Definition | Anchor example in this tree |
|-----------|------------|-----------------------------|
| **State** | Configuration of the world at time t | `W101FactsStore`, `W101UiTreeResult`, `CircuitSnapshot`, `CapturedWindowFrame` |
| **Transition** | Authorized progression from state A to state B | `RoutineGraph` steps, `PetDanceProductionRuntime` pulses, `PathFollower` motion |
| **Relationship** | Structural, spatial or topological links between entities | `ZoneTopologyService`, WAD member hierarchy, combat `COMBAT_HAND` ancestor trees, waypoint corridors |
| **Constraint** | Invariants, budgets, hardware limits, safety boundaries | `FreshnessSeconds = 2` TTL, `0x2D` scancode mapping, `MAX_BYTES = 65536` read cap, recovery latches |
| **Trust** | Qualification tier and provenance of a fact or capability | `W101FactQualification` (`Observed` / `Candidate` / `Unknown`), SHA-256 target-image allowlists |
| **Perspective** | Coordinate frame or interface through which reality is perceived | normalized layout units (0..1000), client pixels, world XYZ (`ActorBody`) |

Trust note: the tier set is exactly `Observed`, `Candidate`, `Unknown`
(`W101FactsStore.cs:5`). There is no `Qualified` tier; any document claiming one
is wrong.

## Leak register (Phase 4 candidates)

Documented couplings observed in the current tree. Recording a leak is not a
mandate to refactor; each needs its own claim and evidence before any change.

1. **Fact vs. lifecycle** - `W101Fact` carries `ObservedUtcTicks` and the read
   path computes expiry from `FreshnessSeconds` (`W101FactsStore.cs:67`), so
   temporal validity sits beside the assertion rather than in a separate
   constraint record. Note: no `expiresUtcTicks` field exists; earlier audit text
   naming one was inaccurate.
2. **Environment special-case** - `WizwalkerOperation.Prepare` and
   `W101UiTreeOperation` hardcode `WizardGraphicalClient` identity checks and
   reviewed image hashes inside the scheduler-adjacent operation path, coupling
   environment identity to the generic runner. An environment-adapter boundary
   (`Observe` / `GetConstraints` / `ProposeTransitions`) is the target shape,
   not an immediate rewrite.

## Provenance

Derived from a third-party normalization manifesto plus an alignment audit dated
2026-11-18. Citations were re-verified against this checkout before inclusion;
fabricated or inaccurate claims from the source audits were dropped. Corrections
recorded in [ONTOLOGY-CENSUS-01.md](../coordination/results/ONTOLOGY-CENSUS-01.md).


