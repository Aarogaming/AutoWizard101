# Reality-environment runtime - proposal and gap map

Status: **PROPOSAL - not adopted, no claim, nothing here authorizes a rewrite.**
Date: 2026-11-19 - source: pasted note from a browser Gemini session (third-party,
unverified). Repository citations below were checked against this checkout on the
date above; the note's own claims about crates and behaviour were not.

Companion to [ONTOLOGY_NORMALIZATION.md](ONTOLOGY_NORMALIZATION.md) (the anchor) and
[REPOSITORY_CENSUS.md](REPOSITORY_CENSUS.md). Where this document and the anchor
disagree, the anchor wins until a coordinator adopts a change.

## What the note proposes

Treat Maelstrom as an epistemic control loop rather than a set of game scripts:

1. **Primitive ontology** - seven domain-agnostic types: `Manifold`, `Entity`,
   `Affordance`, `Connector`, `Hazard`/`Field`, `Resource`/`Budget`, `Predicate`.
2. **Model reality** - fuse static priors (nav graphs, collision, POI registry) with
   live sensors, then run a temporal state estimator.
3. **Trust the model** - kinematic sanity checks, cross-modal corroboration,
   prediction-residual monitoring, strict Facts / Policy / Action separation.
4. **Navigate** - macro (zone graph), meso (3D path + repulsive fields), micro
   (closed-loop steering), semantic (goal planner over affordances and predicates).
5. **Evolve the model** - record unindexed affordances with provenance, replay traces
   as a promotion filter, re-ground priors after game patches.

## Mapping to what already exists

| Note | Existing anchor | Verdict |
|------|-----------------|---------|
| Facts != Authority != Actions (3, last bullet) | Core invariant in `ONTOLOGY_NORMALIZATION.md` | Already adopted. |
| Six vs seven primitives (1) | Anchor has State, Transition, Relationship, Constraint, Trust, Perspective | See "Primitive comparison". |
| Sanity checks, drift monitoring (3) | `crates/maelstrom-core/src/kinematics.rs`; freshness TTL in `W101FactsStore` | Partly present. Whether residual-triggered halt exists is not verified here. |
| Meso pathing with repulsive fields (4) | `costmap.rs` (static, footprint-inflation and dynamic mob layers) | Present, in-house. |
| Spatial registry (5) | `spatial_index.rs` (BVH / R-tree style, no external crates) | Present, in-house. |
| Micro steering (4) | `steering.rs`, `waypoint_feedback.rs` | Present. |
| Goal planner (4) | `goap.rs`, [GOAP_ACTION_PLANNING.md](../GOAP_ACTION_PLANNING.md) | Present. |
| Generic quest interpreter (4) | `quest_interpreter.rs`, `quest_domain.rs`, [QUEST_INTERPRETER_ENGINE.md](../QUEST_INTERPRETER_ENGINE.md) | Present. |
| Replay as promotion filter (5) | `maelstrom-fallback/src/autonomy_replay.rs`; [W101-REPLAY-BATCH-CORE-02](../coordination/results/W101-REPLAY-BATCH-CORE-02.md) (1,094 matrix rows pass: 74 distinct ids, 36 labelled ticks in 8,140 frames; see the [matrix banner](../coordination/results/REPLAY-BENCHMARK-MATRIX.md)) | Partly (audit 2026-11-20). The replay harness and CLI exist, but 1,085 of the 1,094 passing rows had nothing to check, and nothing in CI or the repository's scripts runs it, so it is not yet a promotion filter. |
| Macro zone graph (4) | `ZoneTopologyService` (C#) | C# routes. A Rust `Connector` type exists in `ontology.rs`, but nothing outside its tests uses it (audit 2026-11-20). |
| Persistent empirical registry (5) | none located | **Gap candidate.** |
| Patch-resilient re-grounding (5) | none located | **Gap candidate.** |

Correction to the note: it assumes external crates (`parry3d`, `petgraph`, `rstar`,
`rusqlite`, `pathfinding`, a GOAP crate, and others). `maelstrom-core` is a std library (it does not build with
`--no-default-features`, despite older no_std wording) and currently depends only on `serde`, `uuid` and optional codec crates, and
its modules say they are implemented without external dependencies. Adding any of
those crates is a new production dependency and needs explicit approval
(`AGENTS.md`: no new production dependencies without asking).

## Primitive comparison

The note's seven primitives are mostly a **spatial specialisation** of the anchor's six.
Suggested reading, for discussion only:

| Note | Closest anchor primitive | Note |
|------|--------------------------|------|
| `Manifold` | Perspective (+ State) | The anchor's Perspective already names coordinate frames. |
| `Entity` | State | |
| `Affordance` | Transition (precondition surface) | |
| `Connector` | Relationship | Anchor already cites `ZoneTopologyService` here. |
| `Hazard` / `Field` | Constraint | |
| `Resource` / `Budget` | Constraint | Budgets are already listed under Constraint. |
| `Predicate` | State + Trust | Typed, timestamped, with qualification tier. |

Open question for the coordinator: is a seventh named primitive worth the cost, or is
`Affordance` / `Connector` better treated as typed *kinds* of existing primitives in the
Rust domain model? Splitting the vocabulary between the anchor doc and a new crate
would repeat the "documents disagree" problem the census already corrected once.

## Proposed next steps (each needs its own claim)

1. **Decide the vocabulary** - reconcile six vs seven primitives in the anchor doc.
   Docs only; no code. Owner: whoever holds ontology work.
2. **Empirical registry design note** - what is recorded when an unindexed
   affordance or collision snag is seen, provenance and trust tier fields, storage
   format (no new dependency decided yet; compare plain JSON/JSONL, existing
   `GAME_KNOWLEDGE_DATABASE.md` storage and SQLite). Offline design only.
3. **Re-grounding audit** - list which stored anchors depend on installed game data
   (WAD content, floor heights) and what would flag them stale after a patch. Read-only.
4. **Residual-trust check** - verify whether the "held `W`, no displacement -> trust
   drops -> halt leaf" loop exists today in `PathFollower` / kinematics code, and if not,
   specify it against the existing recovery-hold rules. Verification before design.

## Boundaries

- Facts publish as `Candidate` / `Observed` only; nothing in this proposal lets a fact
  authorize an action.
- The note mentions per-process audio spectra, frame capture and process pose reads.
  Any such sensor remains subject to the runtime boundaries in `AGENTS.md` and the
  W101 client-side exception document; this proposal grants nothing new.
- Proposal text is not evidence of a game outcome. No live test is implied.

## Provenance

Seed text came from the user's pasted Gemini browser note (2026-11-19). Treat its
quantitative or crate-level assertions as unverified. The mapping table was built by
reading module headers and docs in this checkout, not by running the code.
