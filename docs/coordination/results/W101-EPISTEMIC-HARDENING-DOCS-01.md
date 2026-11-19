# Evidence Report: W101-EPISTEMIC-HARDENING-DOCS-01

## 1. Summary
Authored formal architectural and systems specification for Epistemic Reality Runtime Hardening in `docs/EPISTEMIC_RUNTIME_HARDENING.md`, and updated `docs/INDEX.md`.

## 2. Documented Systems-Level Failure Modes & Solutions
1. **Native Client Affordance Shortcuts**: Formalized built-in fast-travel keys (`Home`, `End`, `Page Down` / `Page Up` marker recall, `Ctrl+O` emergency potion) as $O(1)$ topological Connectors and Affordances, transforming expensive multi-zone navigation into deterministic graph jumps.
2. **Epistemic Decay & Anti-Hallucination TTLs**: Specified half-life decay curves $C(t) = C_0 \cdot e^{-\lambda (t - t_0)}$, dynamic mob vector timeouts ($\le 500\text{ms}$), explicit `EntityEpistemicState::Depleted` cooldown states, and Bayesian negative evidence recording $P(\text{spawn}) \leftarrow \frac{\alpha}{\alpha + \beta + N_{\text{negative}}}$ to prevent planner oscillation between empty anchors.
3. **Multi-Client Synchronization & Hivemind Lockstep**: Designed the Shared Spatial Blackboard IPC/SQLite distribution bus and the 3-phase Sigil Barrier Synchronization protocol (`Preparing` -> `ReadyForSigil` -> `SteppedOnSigil`) ensuring synchronized 10s countdown ring entry.
4. **Dynamic Zone Instancing & Non-Euclidean Interiors**: Formulated `ZoneInstanceKey = (zone_template_id, phase_tag)` decoupling learned geometry from transient process pointers, along with canonical camera pitch/zoom calibration resets.
5. **Deterministic Failure Recovery & Graceful Degradation Tiers**: Codified Tier 0 (Full Ontology), Tier 1 (Degraded Pose / Visual-Acoustic Fallback via compass vector, OCR HUD, and WASAPI audio cues), and Tier 2 (Safe Halt & Replay Trace Snapshot under double residual drift).
6. **Schema Versioning & Provenance**: Defined `ProvenanceTag` tracking `origin`, `build_hash`, `verification_count`, and timestamps to enable post-mortem replay evaluation (`maelstrom-replay --batch`).

## 3. Verification
- `docs/INDEX.md` updated with reference to `EPISTEMIC_RUNTIME_HARDENING.md`.
- `python tools/check_project_board.py` validated (30 workstreams, dependency graph clean).
- `python tools/check_text_encoding.py` verified (0 violations).
