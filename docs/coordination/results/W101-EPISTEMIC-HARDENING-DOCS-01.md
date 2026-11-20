# Evidence Report: W101-EPISTEMIC-HARDENING-DOCS-01

Status: **Partially verified** (audited 2026-11-20; the document exists and is indexed, but it specifies far more than the code does and several
figures and keys are unsourced; see "Audit note" at the end).

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

## Audit note (2026-11-20, Claude)

Checked `docs/EPISTEMIC_RUNTIME_HARDENING.md` against `ontology.rs`, `goap.rs`, `maelstrom-fallback`, `WIZARD101_GAME_KNOWLEDGE.md`,
[W101-KNOWLEDGE-AUDIT-01](W101-KNOWLEDGE-AUDIT-01.md) and a throwaway probe crate. The document now carries a status block and **Today** notes.

1. **Not implemented, described as specified:** the half-life curve (decay is a hard TTL cut-off), the Bayesian spawn-prior formula (a
   counter), the Shared Spatial Blackboard (no IPC, SQLite or cross-client state exists), the 3-phase barrier timing (no clock, no 200 ms
   rule), the Tier 1 sources and the Tier 2 halt, lease release and trace snapshot (the tier is a drift counter that nothing reads).
2. **TTL and half-life figures are the author's targets.** The code default is 60 s for every kind; a 500 ms mob TTL exists only through
   `with_ttl_ms(500)`. The 20% recall mana cost and the "100% Health and 50% Mana" potion figures have no source (the repository's notes say
   marking costs mana and that exact costs must be read, not assumed), and `N`, `Ctrl+K` and `Ctrl+M` are unsourced.
3. **The shortcut keys themselves are real**, as are the two quest keys: `Home`, `End`, `Page Down`, `Page Up`, `Ctrl+O`, `Ctrl+T` and
   `Ctrl+Shift+Q` appear in the official defaults recorded in `WIZARD101_GAME_KNOWLEDGE.md`. But `End` is the current world's common area, not
   always Wizard City's Commons, and the code hard-codes `Commons`.
4. **A safety contradiction was fixed.** Section 5 proposed a "canonical camera reset (`Home` key ...)", while section 2 defines `Home` as
   the Dorm/Castle fast-travel key; sending it would travel, not reset the camera.
5. **The replay claim was wrong in two ways.** The binary is `maelstrom-replay` and its batch flag is `--batch-dir <DIR>`, not `--batch`, and
   no code reads a `ProvenanceTag`, so replay cannot distinguish planner regressions from poisoned anchors.
6. **Gate lines held up.** On 2026-11-20 `check_project_board.py` reports 30 workstreams and `check_text_encoding.py` reports 0 violations,
   and `docs/INDEX.md` does list the document (its line now states what is implemented).
