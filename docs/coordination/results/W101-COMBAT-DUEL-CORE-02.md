# Claim Evidence: W101-COMBAT-DUEL-CORE-02

Status: **Partially verified** (audited 2026-11-20; the geometry arithmetic, the intent strings, the bridge branch and the click tests are real and
pass, but the claims of calibrated positions, clamping, zero out-of-bounds clicks and end-to-end dueling verification are not supported;
see "Audit note" at the end).

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

## Audit note (2026-11-20, Claude)

Checked against `combat_geometry.rs` and its tests, `behavior_tree.rs`, `maelstrom_bridge.rs`, `kinematics.rs`, `DesktopInputActuator.cs`,
`JobDeckHost.cs`, the C# click tests and a throwaway probe crate that drives `process_state` (no game, not committed). What holds: the
scaling arithmetic and the values quoted ((208.0, 60.0), (406.4, 477.6), (873.6, 477.6)), `combat_turn_action` and the three action
strings, the bridge branch, and the two named C# tests. Corrections:

1. **"Calibrated 720p positions" means scaled, not calibrated.** The 720p values are the 800x600 constants times 1.6 and 1.2. No
   measurement at 1280x720 is recorded, and [RESOLUTION-1440P-01](RESOLUTION-1440P-01.md) says client geometry must be measured, never
   inferred from scale.
2. **"Strictly clamps hand bounds to `1..=7`" is wrong.** `CombatGeometry` returns `Err`; it does not clamp. The bridge then discards the
   error with `unwrap_or` and clicks a default: probe, `PlayCard:9/4` and `PlayCard:0/9` gave the (688, 390) single-card position,
   `SelectTarget:99` and `SelectTarget:abc` gave Enemy 0, and an unknown action such as `Flee` or `Foo`, like the bare intent, gave a card
   click (slot 0 of 4).
3. **"Guaranteeing zero out-of-bounds clicks" holds only because the actuator refuses them.** The check ([0, 1280] x [0, 720]) is real and
   tested. But the ally row is at y = 708 and the jitter box is 50 x 50, so in the probe 7.8% of clicks for allies 4 and 5 (6 and 7 share the
   row) landed below y = 720 and would be rejected.
4. **The trajectory model is partly cosmetic.** The Bezier and Fitts code exists (120 ms and 160 ms constants unsourced), but the path starts
   at the player's world position, not the cursor (the same Pass click planned at 139 ms, 738 ms and 1319 ms from three poses), and the host
   uses only the end point and the dwell. The jitter is a Gaussian centered on the box with a standard deviation of about 0.17 of its
   size, not "non-centered".
5. **"End-to-end dueling click verification" is not end to end.** No Rust code sends `actuate`. The two C# tests (and two more) build a
   `KinematicPlanSummary` by hand with coordinates copied from the Rust constants and use `() => true` for `ready`, so nothing checks that a
   bridge plan reaches the host. The host's live branch sends no input: its runtime returns `false` for every send, so a live click is not
   accepted and trips the 5 s release recovery.
6. **The try/finally release is real but is not state verification.** The actuator retries `SendMouse(..., up: true)` every 100 ms for up to
   5 s and treats its boolean as confirmation; the button state is not read back. `ReleaseKeyAsync` is the key path, not the mouse path.
7. **Nothing chooses a card or target.** `combat_turn_action` is set only by tests and never cleared, so `ExecuteCombatTurn` re-proposes the
   same action every tick.
8. **Gate lines are snapshots.** "51 passed" for `maelstrom-core --lib` (72 today) and "148 passed across 5 crates" (on 2026-11-20 the
   workspace passed 281 tests in 41 test-result lines, offline) were not re-derived. The 8 bridge tests pass. The "16 passed" .NET figure
   matches the 16 `[Fact]`s in the two suites but was not re-run in this audit; `dotnet format` was not re-run.
9. **Test gaps.** No test covers malformed or unknown actions, the start pose, the ally rows against the viewport, a `Flee` action, clearing
   the action, a Rust-to-host contract, or any measured 1280x720 fixture.
10. **Release note.** `claims.json` (coordinator-owned, not edited here) says this task gave "720p/1080p spell card hitboxes" and "5 tests in
    combat_geometry.rs"; `CombatGeometry` has no 1080p path and 10 geometry tests exist.

Doc comments in `maelstrom_bridge.rs` and `combat_geometry.rs` and `docs/COMBAT_DUELING_LOOP.md` were corrected to match. The default-click
fallback and the ally-row overflow are defects, but fixing them changes what a bare `PROPOSAL:CombatTurn` does (an existing test expects a
card plan for it), so they need an owner decision and were not changed.

**What this evidence does not prove.** That the quoted positions match the game at 1280x720, that any plan reaches the game, or that the
jitter or timing has any effect beyond what the tests assert.
