# Architectural Contract: Language Allocation & Core Engineering Workflows

> **Status (audit 2026-11-20, Claude): draft proposal from a pasted Gemini note, not the adopted language allocation.** The user-accepted decision is the "Language selection" section of
> [ADR_MAELSTROM_INDEPENDENT.md](../ADR_MAELSTROM_INDEPENDENT.md) (2026-11-19: each operation in the language that performs it most efficiently, a direction and not a completed migration, no speculative
> FFI, new production dependencies need approval); where this page and the ADR disagree, the ADR wins. This page assigns the HUD and overlays to C# (the ADR assigns the in-game overlay to Rust `egui` and keeps
> C# for WinForms screens), adds a C/C++ FFI layer the ADR does not have, and states as already true several things that are not; corrections are inline under **Today**.
> Evidence: [CORE-ARCH-WORKFLOWS-01](../coordination/results/CORE-ARCH-WORKFLOWS-01.md).

## 1. Executive Summary & Philosophy

In an epistemic reality-interactor system like Maelstrom, forcing a single programming language across all operational layers creates severe architectural impedance mismatches. A desktop game-player and experiment workbench requires:
- Nanosecond deterministic temporal state estimation without garbage collection pauses.
- First-class Windows desktop and native shell integration for responsive operator interfaces.
- Rapid scientific exploration, data wrangling, and machine learning training pipelines.
- Zero-cost native binary interop for legacy formats and OS hooks.

To maintain maximum reliability, performance, and developer velocity, **each language is allocated strictly to the layer where its memory model, runtime characteristics, and tooling ecosystem excel**.

**Today:** "nanosecond deterministic temporal state estimation" and "high-throughput" are not measured anywhere: the repository has no benchmarks and no timing evidence. No C# code references the Rust crates; the
Rust side is a library, a replay CLI and a read-only pipe client of the C# deck host. The move to Rust is a direction under the ADR, not a completed state.

---

## 2. Language Allocation by Layer

| System Layer | Primary Language | Technical Rationale & Responsibilities | Repository Realization |
| :--- | :---: | :--- | :--- |
| **Core Ontology, Spatial Math, Sensory & Control Loop** | **Rust** | **Memory safety without GC pauses, deterministic execution, and zero-cost abstractions.** Provides high-throughput spatial indexing (`spatial_index.rs`), Nav2 3-layer costmaps (`costmap.rs`), Goertzel/spectral audio DSP (`audio_cue.rs`), pure-pursuit steering (`steering.rs`), GOAP action synthesis (`goap.rs`), short-horizon predictive rollouts (`predictive_rollout.rs`), and turn-by-turn combat evaluation (`combat_turn_evaluator.rs`). The world model, trust verification gates, and lease-bound actuation logic reside here. | `crates/maelstrom-core/`<br>`crates/maelstrom-reflex/`<br>`crates/maelstrom-fallback/` |
| **Desktop HUD, Operator Workbench & Windows UI** | **C# (.NET)** | **First-class Win32/WPF desktop integration and rapid UI iteration.** Owns visual overlays, operator control panels, dashboard telemetry, and the JobDeckHost lifecycle. Establishes a clean separation between visual presentation and the headless Rust engine, communicating via local IPC (`MaelstromAutonomyBridge` named pipes). | `ProjectMaelstrom/`<br>`ProjectMaelstrom.Tests/` |
| **Offline Analysis, Data Tooling & Model Training** | **Python** | **Unmatched data science, batch trace processing, and ML ecosystem.** Used for offline log parsing, dataset curation, exploration notebooks, and model training (e.g. lightweight SSM / patrol prediction) before exporting frozen ONNX/tensor artifacts for Rust runtime inference. Also powers repo integrity linters. | `tools/`<br>`tools/tests/` |
| **Native OS / Asset / Hardware Interop** | **C / C++ (via narrow FFI)** | **Audited native boundaries for legacy engines and hardware hooks.** Restricted strictly to wrapping battle-tested C/C++ libraries (e.g. Gamebryo NIF parsers, direct WASAPI/DirectX hooks) that lack mature native Rust equivalents. Kept behind narrow, audited FFI wrappers to prevent unsafe memory propagation into the core engine. | Native DLL bindings & FFI bridges |

**Today:**
- **Rust row.** "Lease-bound actuation logic reside[s] here" is not true: the actuation lease is the C# `GameInputLease`, the Rust `InputLeaseToken` in `capability_token.rs` is unused and exclusive nothing, and the ADR says
  that until the cutover Rust components act only behind the C# lease. Outside their tests, the ontology types are used only by `predictive_rollout.rs`, which nothing calls.
- **C# row.** The desktop app is WinForms (`UseWindowsForms` in `ProjectMaelstrom.csproj`; there is no WPF or WinUI), and the ADR assigns the in-game overlay to Rust. The named pipe is served by the C# `JobDeckHost`
  and read by a Rust *client* (`MaelstromNativeBridge`): length-prefixed JSON, read-only commands (`capabilities`, `catalog`, `state`, `snapshot`). `MaelstromAutonomyBridge` is an in-process Rust struct used by the
  replay harness, not a pipe.
- **Python row.** Python does train models (`tools/train_dodge_*.py`), but nothing exports ONNX or tensors for Rust: the only ONNX mention in the code is a comment on `StubReflexPolicy` (a policy that always returns
  NoOp). No SSM or patrol-prediction model exists, and the ADR requires approval for an ONNX runtime dependency.
- **C/C++ row.** No C/C++ FFI exists: the only C file is the probe tool `tools/legacy_kiwad_probe.c`; there is no `extern "C"`, `build.rs` or FFI crate, WASAPI loopback capture is C# (NAudio), and no Gamebryo/NIF code
  is present. The ADR says "no speculative FFI".

---

## 3. The Five Core Engineering Workflows

A reality-environment runtime breaks down if engineered as a standard CRUD application. Five disciplined engineering workflows govern all development and logic promotion:

### Workflow 1: Ontology-First, Schema-Driven Design
Before writing adapters or screen parsers, define the world through the **seven domain-agnostic primitives**:
1. `Manifold` (Space): Bounded 3D coordinate frame ($X, Y, Z, \text{yaw}$), gravity/Z-floor rules, and velocity caps.
2. `Entity` (Occupant): Identified occupant carrying typed identity, bounding volume (AABB/cylinder), and kinematics $(\mathbf{p}, \mathbf{v})$.
3. `Affordance` (Interaction Surface): Interactive localized region (dialogue NPC, sigil, reagent, chest) exposing valid state transitions.
4. `Connector` (Topology Edge): Traversable link between manifolds (zone portals, staircases, fast-travel shortcuts).
5. `Hazard` / `Field` (Constraint Region): Spatial cost volumes (mob aggro cylinders, deep water, boss interrupt zones).
6. `Resource` / `Budget` (Reservoir): Dynamic capacity pools (mana, health, energy, potion charges, combat pips).
7. `Predicate` (Epistemic Fact): Grounded relational truth assertion carrying monotonic timestamps and confidence scores.

> [!IMPORTANT]
> **Universal Ingestion Invariant**: Every external source—a Gamebryo 3D zone, a Windows desktop window rect, a WASAPI audio stream, or an OCR bounding box—must map into these primitive schemas before reaching the decision engine.

**Today:** all seven types exist in `ontology.rs` (a `Connector` included), but the invariant is neither enforced nor true: outside tests only `predictive_rollout.rs` uses them, and the behavior tree and the autonomy bridge
work on their own state types (`W101Blackboard`, `NativeHostState`). A `Predicate` is a key, a value, a confidence and a caller-supplied timestamp: it carries no provenance and no TTL (the TTL is an argument of
`is_fresh`), and "monotonic" is not enforced.

### Workflow 2: Trace-Driven Replay & Regression Gating
- Continuous recording of structured session traces (`maelstrom-replay`): inputs, sensory observations, temporal model predictions, residual errors, and emitted intents.
- **Automated Regression Filter**: Before promoting any logic change, run deterministic batch replays against historical trace corpuses.
- **Gating Rule**: If a proposed algorithm modification lowers decision accuracy, increases replanning frequency, or degrades trajectory smoothness (jerk metrics), the change is automatically blocked from promotion.

**Today:** nothing blocks promotion automatically. `maelstrom-replay` exits 2 only when decision accuracy is below 95%; replan counts and jerk are reported and gate nothing; CI runs `cargo test --workspace --locked`
but not the replay CLI; and the committed benchmark matrix is mostly vacuous (see [REPLAY_BATCH_BENCHMARK.md](../REPLAY_BATCH_BENCHMARK.md) and the banner on
[REPLAY-BENCHMARK-MATRIX.md](../coordination/results/REPLAY-BENCHMARK-MATRIX.md)).

### Workflow 3: Analytical Baseline Before Learned Models
- **Deterministic First**: Always establish exact mathematical models, physics equations, and rule-based evaluators first:
  - WindMouse & cubic Bézier minimum-jerk kinematics for locomotion.
  - LIFO stack pipelines for combat damage calculations.
  - Costmap inflation layers and pure-pursuit steering for local obstacle avoidance.
- **Learned Models for High-Variance Gaps Only**: Machine learning is reserved exclusively for messy, stochastic dynamics where analytical physics breaks down (e.g., erratic mob patrol corridors, complex image noise).
- **Advisory Status**: Learned model outputs are always treated as candidate facts gated by trust bounds, never as sovereign executive authority.

**Today:** WindMouse and a Bézier minimum-jerk generator exist in `kinematics.rs`, but WindMouse generates cursor paths, not locomotion; the "LIFO stack pipelines" are not LIFO (the order of the charm and ward lists
has no effect, and the pipeline has no caller; see [CORE-SELF-AUDIT-REPAIRS-01](../coordination/results/CORE-SELF-AUDIT-REPAIRS-01.md)). No learned model exists in the Rust runtime, so the advisory-status rule has
nothing to apply to yet.

### Workflow 4: Strict Separation of Facts, Policy & Actuation
- **The Core Axiom**: `Facts ≠ Authority ≠ Actions`.
  - **Sensors & World Model**: Ground empirical observations into typed facts (`Predicate`, `Entity`).
  - **Arbiter & Evaluators**: Formulate candidate action proposals (GOAP plans, `CombatDecision`, steering vectors) based on current goals.
  - **Input Actuator**: Operates under a single, exclusive execution lease (`GameInputLease`). Keystrokes and mouse events fire only when ownership is verified, countdowns have cleared, and target window focus is proven.
- **Graceful Degradation Ladder**:
  - `Tier 0 (Nominal)`: Full sensor fusion and sub-millimeter precision.
  - `Tier 1 (Degraded)`: Fallback to dead-reckoning and expanded safety margins when visual/memory feeds drop.
  - `Tier 2 (Blind / Panic)`: Controlled emergency stop, key release, and recovery hold.
- **Immediate User Takeover**: Any physical mouse movement or emergency hard-stop key instantly preempts autonomous actuation and releases the input lease.

**Today:** the separation of facts, authority and actions and the single `GameInputLease` are real on the C# side and in `AGENTS.md`. The tier descriptions do not match `ontology.rs`: Tier 1 is a visual/acoustic
fallback entered by a single drift fault (there is no hook-failure input), "sub-millimeter precision" is invented, and Tier 2's "emergency stop, key release and recovery hold" is only intended: the code comment
says nothing does any of that and nothing reads the tier (the rollout engine widens hazard margins by 1.5x and 2.0x per tier, but nothing calls it). Per `AGENTS.md`, mouse movement pauses dispatch and resume needs
a countdown and fresh verification; that "preempts" or "releases the lease" instantly was not checked.

### Workflow 5: Versioned World & Asset Evolution
- **Immutable Provenance**: Extracted navigation meshes (ZoneNavMesh), POI registries, NPC dialogue trees, and reagent spawn nodes are stored as versioned, content-hashed data artifacts.
- **Automated Patch Re-Baking**: Whenever a game update or client patch modifies asset binaries (WADs, NAV meshes, collision geometries), automated regression pipelines detect coordinate drift, re-bake ground priors, and flag desynchronized anchors.

**Today:** not implemented. `ZoneNavMesh` is a binary mesh parser in `costmap.rs`; no code versions or content-hashes extracted meshes, POI registries or dialogue trees, and none detects patch drift or re-bakes ground
priors ([REALITY_RUNTIME_PROPOSAL.md](REALITY_RUNTIME_PROPOSAL.md) lists patch-resilient re-grounding as a gap candidate). This is a goal, not a workflow.

---

## 4. Architectural Cohesion Across Repositories

```
  +------------------------------------------------------------------------+
  |                     C# (.NET) Presentation & HUD                       |
  |   - WPF / WinUI Overlays     - JobDeckHost UI      - Operator Desk     |
  +------------------------------------------------------------------------+
                                      |
                           (Named Pipe IPC JSON-RPC)
                                      |
  +------------------------------------------------------------------------+
  |                   Rust Core Reality Runtime Engine                     |
  |  - Primitive Ontology (`ontology.rs`)    - Costmaps (`costmap.rs`)     |
  |  - Spatial Index (`spatial_index.rs`)    - Steering (`steering.rs`)    |
  |  - Combat Evaluator (`combat_turn_evaluator.rs`)                       |
  |  - Minigame & Pet Domain (`minigame_domain.rs`)                        |
  |  - Predictive Rollouts (`predictive_rollout.rs`)                       |
  +------------------------------------------------------------------------+
              ^                                                |
              | (Frozen Model Artifacts & Weights)             | (Trace Logs)
              |                                                v
  +--------------------------------------+   +-----------------------------+
  |      Python Analysis & ML Tools      |   |  C / C++ Native Interop     |
  |  - Trace Ingestion & Notebooks       |   |  - Audited FFI Wrappers     |
  |  - Model Training (ONNX/SSM)         |   |  - Legacy NIF/Engine Hooks  |
  |  - Project Board & Code Quality      |   +-----------------------------+
  +--------------------------------------+
```

**Today:** the arrow labelled "Named Pipe IPC JSON-RPC" is wrong: the framing is a 4-byte little-endian length prefix and a JSON payload, there is no JSON-RPC in the code, the C# side serves, the Rust side reads, and
nothing flows from Rust to a C# actuator. The "Trace Logs" arrow leaves the Rust box and ends at the C/C++ box, which does not exist (the Python box was probably meant).
