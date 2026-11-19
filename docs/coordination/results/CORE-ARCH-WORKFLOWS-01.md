# Evidence Report: CORE-ARCH-WORKFLOWS-01

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
