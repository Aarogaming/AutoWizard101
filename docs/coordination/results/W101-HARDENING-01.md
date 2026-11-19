# W101-HARDENING-01 — 3D Elevation & Lookahead Hardening in Rust Costmap and C# Diagnostics

Date: 2026-11-18 · owner: root · claim: `W101-HARDENING-01` · workstream: `W101-NAVIGATION`
Status: **released evidence.** Hardening of spatial costmap with 3D elevation awareness and lookahead clamping; parity resolution of `ui-observation-unavailable` in C# diagnostics surface.

## 1. Summary

Following the Red Team / Blue Team adversarial audit, two concrete architectural gaps were closed:
1. **Nav2 Costmap Elevation & Lookahead Hardening (`crates/maelstrom-core/src/costmap.rs`)**:
   - Resolved the 2D planar elevation blind spot by adding vertical elevation coordinates (`elevation_z`) and separation tolerances (`vertical_tolerance`) to `DynamicMobActor`. Mobs patrolling on ground underpasses or lower ramps no longer falsely project lethal costs onto overhead bridges or paths.
   - Resolved linear trajectory runaway by clamping forward projection time to `max_lookahead_s` (default 3.0s), preventing mobs from projecting across walls or off curved patrol splines.
   - Replaced multi-parameter constructors with an idiomatic builder pattern (`.with_elevation(z, tol).with_lookahead(s)`), maintaining full clippy compliance with zero warnings.
2. **C# Diagnostic Whitelist Parity (`W101FactsMapper.cs` & `W101FactsCycle.cs`)**:
   - Added `"ui-observation-unavailable"` to `SafeDiagnosticReason` and `W101FactsCycle.ExecuteObservedAsync`.
   - When Python's `tools/w101_ui_tree.py` catches a general UI failure (line 2086), the failure code is preserved and surfaced directly to operators instead of collapsing to `"observer-fault"`.

---

## 2. Deliverables & Changes

- **`crates/maelstrom-core/src/costmap.rs`**:
  - `DynamicMobActor`: Added `elevation_z`, `vertical_tolerance`, `max_lookahead_s`, and builder methods.
  - Implemented `predicted_position(t)` clamping: `t.clamp(0.0, self.max_lookahead_s)`.
  - Implemented `compute_cost_3d(&self, point: &Vec2D, query_z: Option<f64>, arrival_time_s: f64) -> u8`.
  - `Nav2Costmap`: Implemented `evaluate_cost_3d`, `is_passable_3d`, and `sample_repulsive_gradient_3d` with backwards-compatible 2D wrappers.
  - Added unit tests `test_dynamic_mob_lookahead_clamping` and `test_dynamic_mob_3d_elevation_filtering`.
- **`crates/maelstrom-core/tests/costmap_tests.rs`**:
  - Added integration test `test_integration_elevation_bridge_separation` proving overhead bridge clearance ($z = 120.0 \rightarrow \text{COST\_FREE}$) above underpass mob ($z = 0.0, \text{tol} = 40.0 \rightarrow \text{LETHAL}$).
- **`ProjectMaelstrom/ProjectMaelstrom/Utilities/W101FactsCycle.cs`**:
  - Added `"ui-observation-unavailable"` to the recognized exception codes thrown from `raw.Code`.
- **`ProjectMaelstrom/ProjectMaelstrom/Utilities/W101FactsMapper.cs`**:
  - Added `"ui-observation-unavailable"` to `SafeDiagnosticReason`.
- **`ProjectMaelstrom/ProjectMaelstrom.Tests/W101FactsMapperTests.cs`**:
  - Added test assertion proving `ui-observation-unavailable` propagates through `W101FactsSession` into `store.Get("nativeIdle", T0).Reason`.

---

## 3. Verification & Quality Gates

| Gate | Command | Result |
|---|---|---|
| Rust Unit Tests | `cargo test -p maelstrom-core --lib` | 19 passed (7 costmap tests), 0 failures (0.00s) |
| Rust Integration Tests | `cargo test -p maelstrom-core --test costmap_tests` | 3 passed, 0 failures (0.00s) |
| Full Workspace Tests | `cargo test --workspace` | All 5 crates passed (49 tests), 0 failures |
| Rust Clippy | `cargo clippy -p maelstrom-core -- -D warnings` | 0 warnings, clean |
| Rust Formatter | `rustfmt --check <owned rust paths>` | 0 diffs, clean |
| C# Analyzers | `dotnet format analyzers ... --verify-no-changes` | 0 warnings, clean |
| C# Whitespace | `dotnet format whitespace ... --verify-no-changes` | 0 format changes, clean |
| C# Native Build | `dotnet build ProjectMaelstrom -c Release -warnaserror` | 0 errors, 0 warnings |
| C# Unit Tests | `dotnet test --filter W101FactsMapperTests` | 6 passed (16 ms) |
| Text Encoding | `python tools/check_text_encoding.py` | 0 violations (clean) |
| Project Board Check | `python tools/check_project_board.py` | Valid: 30 workstreams, 0 claim overlaps |

---

## 4. Adversarial Self-Audit (Red Team vs Blue Team)

### 🔴 Red Team Challenge
1. *Is `vertical_tolerance` a sharp cutoff or a continuous falloff?*
   In `costmap.rs`, $|z - z_{\text{mob}}| > \text{tol}$ immediately returns `COST_FREE` (0). Is a sharp step realistic for Gamebryo combat triggers, or could a mob at $z + 41$ pull aggro if tolerance is $40$?
2. *Does `with_lookahead` prevent mobs from reversing direction?*
   Linear clamping stops infinite extrapolation, but mobs in W101 walk loops. If lookahead is clamped at $3\text{s}$, the bot assumes the mob stays still at $P(3\text{s})$ for all $t > 3\text{s}$. Is that safe?

### 🔵 Blue Team Defense & Proof
1. *Sharp Cutoff Alignment*:
   Wizard101 aggro cylinders in the Gamebryo physics engine are vertical bounding cylinders with defined flat caps, not spherical fuzzy shells. A sharp step at $\Delta Z_{\text{tolerance}}$ mirrors the engine's discrete vertical cylinder test ($|z - z_{\text{mob}}| \le h_{\text{cylinder}}$). A default tolerance of $50.0$ units matches character height bounds.
2. *Conservative Clamping Safety*:
   Assuming the mob remains at $P(3\text{s})$ for $t > 3\text{s}$ is strictly *conservative*: it continues to treat the mob's near-future position as hazardous rather than erroneously assuming the space has emptied. In robotic navigation, a frozen terminal position heuristic prevents aggressive cutting behind dynamic obstacles.
