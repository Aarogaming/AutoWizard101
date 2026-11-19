# Evidence Report: CORE-BORROW-SEMANTICS-01

## 1. Summary
Implemented Rust borrowing, ownership, and typestate semantics for reality runtime authority in `crates/maelstrom-core/src/capability_token.rs` and formalized the architectural doctrine in `docs/architecture/OWNERSHIP_BORROWING_AND_TYPESTATES.md`, indexed in `docs/INDEX.md`.

## 2. Codified Standards & Components

### 1. Typestate Lifecycle (`Unverified<T>` $\rightarrow$ `Verified<T>`)
- Raw sensory inputs (OCR, memory probes, audio cues) start as `Unverified<T>`, preventing planners from using ungrounded data.
- Consumed via `.verify(validator_fn, confidence, ttl_ms)` into `Verified<T>`.
- Verified facts enforce Time-To-Live (TTL) freshness and provide `.read_if_fresh(now_ms)`.

### 2. Scoped Capability Tokens
- **`OutOfCombatToken<'a>`**: Holds a borrow `&'a Verified<CombatPresenceFact>`. Cannot outlive the verification or be acquired if combat has begun, strictly gating potion use, out-of-combat deck switching (`Ctrl+K`), fishing casts, and realm hopping (`Ctrl+M`).
- **`ZoneLifetimeToken<'zone>`**: Parameterized by the active zone manifold lifetime `'zone`, preventing actions from executing within despawned or unloaded zones.

### 3. Linear Non-Cloneable Actuation Lease (`InputLeaseToken`)
- Represents exclusive execution authority over physical hardware dispatch (`!Clone`).
- `.consume_action(key, duration_ms, now_ms)` moves and consumes the lease token by value, emitting an `ActionExecutionReceipt`.
- Implements RAII `Drop` to guarantee held keys are released and safe `Idle` is restored on unexpected early returns or errors.

### 4. Single-Use Combat Intent (`SingleUseTurnIntent`)
- Wraps combat decisions as a linear, move-only type.
- `.commit(round_number, now_ms)` consumes the intent by value, emitting a `TurnCommitReceipt` and preventing duplicate execution in the same round.

## 3. Verification & Compliance
- **Tests (`crates/maelstrom-core/tests/capability_token_tests.rs`)**:
  - `test_unverified_to_verified_typestate_transition`: Passed.
  - `test_verified_fact_freshness_and_ttl`: Passed.
  - `test_out_of_combat_token_acquisition_and_borrow_gating`: Passed.
  - `test_linear_input_lease_move_and_single_execution`: Passed.
  - `test_single_use_turn_intent_consumption`: Passed.
- **Workspace Test Suite**: 100% tests passing across all crates.
- **Clippy**: 0 warnings (`cargo clippy -p maelstrom-core --all-targets -- -D warnings`).
- **Project Board & Encoding**:
  - `python tools/check_project_board.py`: 30/30 workstreams valid.
  - `python tools/check_text_encoding.py`: 0 violations.
