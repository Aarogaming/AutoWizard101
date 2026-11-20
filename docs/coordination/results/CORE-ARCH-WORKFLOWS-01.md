# Evidence Report: CORE-ARCH-WORKFLOWS-01

> **Status:** Draft (the report says "adopted"; no adoption is recorded, and the user-accepted decision is the ADR's "Language selection"; audited 2026-11-20, see "Audit note").

## 1. Summary
Formulated and adopted the formal architectural contract defining layer-by-layer language allocation and the five core engineering workflows for epistemic reality-interactor systems in `docs/architecture/LANGUAGE_ALLOCATION_AND_WORKFLOWS.md` and indexed in `docs/INDEX.md`.

## 2. Codified Standards

### Layer-by-Layer Language Allocation
1. **Rust (Core Ontology, Spatial Math, Sensory & Control Loop)**:
   - Eliminates garbage collection pauses and nondeterministic latency.
   - Houses the primitive ontology (`ontology.rs`), spatial indexing (`spatial_index.rs`), Nav2 costmap engine (`costmap.rs`), audio DSP Goertzel classifiers (`audio_cue.rs`), pure-pursuit steering (`steering.rs`), GOAP macro-planner (`goap.rs`), short-horizon predictive rollouts (`predictive_rollout.rs`), and turn-by-turn combat evaluation (`combat_turn_evaluator.rs`).
2. **C# (.NET) (Desktop HUD, Operator Workbench & Windows UI)**:
   - First-class Win32 desktop integration, fast WPF/WinUI UI iteration, and clean presentation decoupling.
   - Operates overlays, HUD telemetry, and the JobDeckHost lifecycle, interacting with the headless Rust engine over IPC (`MaelstromAutonomyBridge` named pipes).
3. **Python (Offline Analysis, Data Tooling & Model Training)**:
   - Offline batch trace exploration, dataset curation, experiment notebooks, and model training.
   - Pre-trains and freezes lightweight predictors (ONNX/tensors) for runtime execution by Rust, and enforces repository board and text-encoding integrity.
4. **C / C++ via Narrow FFI (Native OS / Asset / Hardware Interop)**:
   - Confined strictly to narrow, audited FFI wrappers around legacy binary formats (e.g. Gamebryo NIF parsers, direct WASAPI/DirectX hooks) lacking native Rust alternatives, preventing memory corruption propagation.

### The Five Core Engineering Workflows
1. **Ontology-First, Schema-Driven Design**:
   - Universal grounding into the seven primitives (`Manifold`, `Entity`, `Affordance`, `Connector`, `Hazard`, `Resource`, `Predicate`) carrying provenance tags and freshness TTLs before reaching decision engines.
2. **Trace-Driven Replay & Regression Gating**:
   - Continuous recording of session traces and deterministic batch evaluation (`maelstrom-replay`). Logic regressions that reduce accuracy, increase replanning churn, or increase trajectory jerk are blocked from promotion.
3. **Analytical Baseline Before Learned Models**:
   - Exact kinematics (WindMouse, cubic Bézier minimum-jerk), LIFO combat stacks, and pure pursuit steering establish the baseline. Learned models are deployed only where trace residuals demonstrate deterministic failure, and remain advisory.
4. **Strict Separation of Facts, Policy & Actuation**:
   - Enforces `Facts ≠ Authority ≠ Actions`. Sensory estimation publishes facts; evaluators propose candidate intents; only the C# scheduler dispatches actuation under verified `GameInputLease` with immediate user preemption.
5. **Versioned World & Asset Evolution**:
   - Extracted world meshes, POI registries, and dialogue trees are versioned and content-hashed. Client game patches trigger automated diff/re-bake validation to detect and correct spatial drift.

## 3. Verification & Compliance
- `python tools/check_project_board.py`: 30 workstreams valid.
- `python tools/check_text_encoding.py`: 0 violations.
- `docs/INDEX.md` updated with cross-references.

---

## Audit note (audit 2026-11-20, Claude)
Checked against the repository (greps and file reads, nothing run live), `docs/ADR_MAELSTROM_INDEPENDENT.md`, `ontology.rs`, `maelstrom_bridge.rs`, `ProjectMaelstrom.csproj` and the CI workflow.
- **Verified:** the modules named under Rust exist (`ontology.rs`, `spatial_index.rs`, `costmap.rs`, `audio_cue.rs`, `steering.rs`, `goap.rs`, `predictive_rollout.rs`, `combat_turn_evaluator.rs`); `JobDeckHost`,
  `GameInputLease` and `MaelstromAutonomyBridge` exist; all seven ontology primitives exist in Rust; WindMouse and a Bézier minimum-jerk generator exist in `kinematics.rs`; the board and encoding checks pass today;
  the `INDEX.md` entry exists.
- **"Adopted" is not supported.** The accepted decision is the ADR's "Language selection" (2026-11-19). The page disagrees with it (HUD and overlay in C# instead of Rust `egui`; an extra C/C++ FFI layer the ADR
  rules out as speculative) and the ADR wins; the page is marked a draft proposal.
- **The summary's runtime picture is wrong in five places.** WPF/WinUI (the app is WinForms); C# talking to Rust over `MaelstromAutonomyBridge` named pipes (the pipe is served by C# and read by a Rust client,
  read-only, and `MaelstromAutonomyBridge` is not a pipe); Python freezing ONNX predictors for Rust (nothing exports one; no ONNX runtime dependency); C/C++ FFI wrappers (none exist); "lease-bound actuation" in Rust (the
  lease is C#, the Rust token is unused).
- **Workflows 2 and 5 are goals, not mechanisms.** Replay exits 2 only on accuracy below 95% and CI does not run it; no versioning, hashing or re-bake pipeline for meshes, POIs or dialogue exists. "LIFO combat
  stacks" are order-independent in code; "sub-millimeter" and "nanosecond" figures in the page are unmeasured (no benchmarks).
- **Not edited:** `claims.json` (coordinator-owned). The page carries a status banner and **Today** notes; the `INDEX.md` line carries a caveat.
