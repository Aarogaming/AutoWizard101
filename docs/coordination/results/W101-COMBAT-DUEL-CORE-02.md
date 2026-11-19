# Claim Evidence: W101-COMBAT-DUEL-CORE-02

## 1. Overview
- **Task ID**: `W101-COMBAT-DUEL-CORE-02`
- **Workstream**: `W101-COMBAT`
- **Owner**: `coordinator`
- **Scope**: Concrete implementation and verification of the end-to-end combat dueling loop: `CombatGeometry` viewport scaling and card/target/pass resolution, `MaelstromAutonomyBridge` combat turn intent generation, and `DesktopInputActuator` end-to-end dueling click verification.

## 2. Implementation Summary
1. **Viewport Geometry Scaling & Resolvers** (`crates/maelstrom-core/src/combat_geometry.rs`):
   - Added `BASE_WIDTH = 800.0`, `BASE_HEIGHT = 600.0`, `VIEWPORT_1280X720_WIDTH = 1280.0`, `VIEWPORT_1280X720_HEIGHT = 720.0`.
   - Implemented affine coordinate transformers: `scale_point_to_viewport` and `scale_rect_to_viewport`.
   - Added direct 720p base viewport resolvers:
     - `get_hand_card_coordinates_720p(slot, total)`: accurately positions hand cards in 720p.
     - `calculate_target_coordinates_720p(target_index)`: maps enemy 0..3 (`(208.0, 60.0)`, `(486.4, 60.0)`, etc.) and ally 4..7 pedestals.
     - `get_pass_button_coordinates_720p()`: maps canonical pass button to `(406.4, 477.6)`.
     - `get_flee_button_coordinates_720p()`: maps canonical flee button to `(873.6, 477.6)`.
2. **Behavior Tree Combat Delegation** (`crates/maelstrom-core/src/behavior_tree.rs`):
   - Added `combat_turn_action: Option<String>` to `W101Blackboard`.
   - Updated `ExecuteCombatTurn` to dynamically emit parameterized intents (`PROPOSAL:CombatTurn:Pass`, `PROPOSAL:CombatTurn:PlayCard:...`, `PROPOSAL:CombatTurn:SelectTarget:...`).
3. **Autonomy Bridge Trajectory Synthesis** (`crates/maelstrom-fallback/src/maelstrom_bridge.rs`):
   - Coupled `MaelstromAutonomyBridge` to `CombatGeometry`.
   - Formulates parameterized minimum-jerk cubic Bézier curves and Fitts's Law duration models targeted at calibrated 720p positions with non-centered Gaussian jitter.
4. **Host Actuator Dueling Execution** (`ProjectMaelstrom/ProjectMaelstrom.Tests/DesktopInputActuatorTests.cs`):
   - Added `DispatchKinematicClick_CombatPassButton_DispatchesWithinBounds` verifying pass button click down/up release under lease.
   - Added `DispatchKinematicClick_CombatCardAndTarget_DispatchesWithinBounds` verifying sequential card selection $\to$ target clicking.

## 3. Red Team / Blue Team Adversarial Audit
- **Off-Screen Click & Misclick Prevention**:
  - *Red Team Attack*: Hand card slot calculations with 7 cards could drift outside viewport or negative margins.
  - *Blue Team Mitigation*: `CombatGeometry` strictly clamps hand bounds to `1..=7` cards, and `DesktopInputActuator` rejects any target with $X \notin [0, 1280]$ or $Y \notin [0, 720]$, guaranteeing zero out-of-bounds clicks.
- **Unconfirmed Click Latching**:
  - *Red Team Attack*: If mouse button down is dispatched during combat, an interrupted thread could leave left mouse button permanently depressed.
  - *Blue Team Mitigation*: `DesktopInputActuator` wraps click execution in try/finally blocks and asserts `ReleaseKeyAsync` / `SendMouse(..., up: true)` before returning.

## 4. Test & Quality Gate Evidence
- `cargo test -p maelstrom-core --lib`: 51 passed, 0 failed.
- `cargo test -p maelstrom-fallback --test autonomy_bridge_tests`: 8 passed, 0 failed.
- `cargo test --workspace`: 148 passed, 0 failed across all 5 crates.
- `cargo clippy --workspace --all-targets -- -D warnings`: 0 warnings.
- `dotnet test ... (actuator & host suites)`: 16 passed, 0 failed.
- `dotnet format whitespace/analyzers`: clean (`--verify-no-changes`).
- `python tools/check_project_board.py`: Valid (30 workstreams, zero broken links).
- `python tools/check_text_encoding.py`: 0 violations.
