# W101-COSTMAP-01 — Nav2 3-Layer Costmap & Dynamic Mob Trajectory Repulsion

Date: 2026-11-18 · owner: root · claim: `W101-COSTMAP-01` · workstream: `W101-NAVIGATION`
Status: **released evidence.** Implementation of Nav2-style 3-layer costmap with static geometry, inscribed/inflation footprint clearance, and time-projected dynamic mob trajectory repulsion in pure Rust (`crates/maelstrom-core`).

## 1. Summary

Addressing the kinetic and navigational limitations of classical shortest-path A* (which path directly into streets and patrol mob aggro cylinders), this task implements a zero-stub, pure mathematical 3-layer costmap in `crates/maelstrom-core/src/costmap.rs` adhering to Nav2 conventions and strict zero-stub invariants.

### Layer Architecture
1. **Layer 1: Static Geometry Layer (`PolygonZone`)**:
   - Assigns traversal cost penalties to polygon regions (e.g., street zones receive penalty `50..=127`, whereas sidewalks receive penalty `0`).
   - Implements point-in-polygon containment testing via the ray-casting Jordan curve theorem.
   - When A* plans routes across this layer, the cost heuristic naturally hugs sidewalks and minimizes street exposure.
2. **Layer 2: Footprint & Clearance Inflation Layer (`FootprintInflation`)**:
   - Parameterized by player inscribed radius $R_{\text{inscribed}}$ (e.g. 25.0 unmounted, 45.0 mounted) and outer clearance buffer $R_{\text{inflation}}$ (e.g. 75.0).
   - Solid obstacles (walls, railings, boundaries) represented as line segments (`ObstacleSegment`).
   - For distance $d \le R_{\text{inscribed}}$: returns `COST_INSCRIBED_INFLATED` (253).
   - For $R_{\text{inscribed}} < d < R_{\text{inflation}}$: strictly monotonic linear decay from 252 down to 1.
   - For $d \ge R_{\text{inflation}}$: returns `COST_FREE` (0).
3. **Layer 3: Dynamic Mob Radar & Repulsive Field Layer (`DynamicMobActor`)**:
   - Represents dynamic patrol mobs with coordinate $(x, y)$, velocity vector $(\Delta x / \Delta t, \Delta y / \Delta t)$, aggro radius $R_{\text{aggro}}$, and repulsion radius $R_{\text{repulse}}$.
   - **Forward Trajectory Projection**: Evaluates the mob's predicted position at player arrival time $t_{\text{arrival}}$:
     $$\vec{P}_{\text{pred}}(t) = \vec{P}_0 + \vec{V} \cdot t_{\text{arrival}}$$
   - **Inverse-Square Repulsive Field**:
     - Inside aggro cylinder ($d \le R_{\text{aggro}}$): `COST_LETHAL_OBSTACLE` (254).
     - Inside repulsion buffer ($R_{\text{aggro}} < d \le R_{\text{repulse}}$):
       $$\text{Cost} = \min\left(253, \; \max\left(1, \; \text{round}\left( k \cdot \frac{1}{(d - R_{\text{aggro}} + 1.0)^2} \right)\right)\right)$$
     - Outside buffer ($d > R_{\text{repulse}}$): `COST_FREE` (0).
   - Prevents pathing into nodes that will be occupied by patrolling mobs when the player actually arrives.

---

## 2. Deliverables

- **`crates/maelstrom-core/src/costmap.rs`**:
  - `Vec2D` 2D coordinate and vector primitive with distance, length, dot product, and no_std compatibility.
  - `PolygonZone` point-in-polygon containment.
  - `ObstacleSegment` point-to-segment orthogonal distance.
  - `FootprintInflation` inscribed/clearance cost evaluation.
  - `DynamicMobActor` time-projected position and repulsive field costing.
  - `CostmapComposition` (`Max` and `Additive` modes).
  - `Nav2Costmap` composite evaluator, `is_passable()` query, and numerical repulsive gradient extraction `sample_repulsive_gradient()`.
- **`crates/maelstrom-core/src/lib.rs`**:
  - Exported `pub mod costmap;`.
- **`crates/maelstrom-core/tests/costmap_tests.rs`**:
  - Integration tests verifying multi-layer composition and dynamic mob intercept prediction.

---

## 3. Verification & Quality Gates

| Gate | Command | Result |
|---|---|---|
| Rust Unit Tests | `cargo test -p maelstrom-core --lib` | 17 passed (5 new costmap tests), 0 failures (0.00s) |
| Rust Integration Tests | `cargo test -p maelstrom-core --test costmap_tests` | 2 passed, 0 failures (0.00s) |
| Full Workspace Tests | `cargo test --workspace` | All 5 crates passed (48 tests), 0 failures |
| Rust Linter | `cargo clippy -p maelstrom-core -- -D warnings` | 0 warnings, clean |
| Rust Formatter | `rustfmt --check crates/maelstrom-core/src/costmap.rs crates/maelstrom-core/tests/costmap_tests.rs` | 0 diffs, clean |
| Text Encoding | `python tools/check_text_encoding.py` | 0 violations (clean) |
| Project Board Check | `python tools/check_project_board.py` | Valid: 30 workstreams, 0 claim overlaps |

---

## 4. Test Proof Details

1. **`test_vec2d_distance`**: Verifies exact Euclidean distance $(3, 4) \rightarrow 5.0$.
2. **`test_polygon_zone_containment`**: Verifies inside $(50, 50)$ vs outside $(150, 50)$ for square zone.
3. **`test_obstacle_segment_distance`**: Verifies midpoint projection $(50, 20) \rightarrow 20.0$ and endpoint projection $(130, 40) \rightarrow 50.0$.
4. **`test_footprint_inflation_bounds`**: Verifies distance $\le 25 \rightarrow 253$, $\ge 75 \rightarrow 0$, and strictly monotonic decay in between.
5. **`test_dynamic_mob_radar_trajectory_projection`**: Mob moving from $(100, 0)$ towards origin at $10$ u/s. Query at $(50, 0)$:
   - $t=0$: dist = 50.0 $\rightarrow$ repulsive cost $1 < c < 254$.
   - $t=4$: dist = 10.0 $\le$ aggro radius (20.0) $\rightarrow$ `COST_LETHAL_OBSTACLE` (254).
   - $t=20$: dist = 150.0 > repulsion radius (80.0) $\rightarrow$ `COST_FREE` (0).
6. **`test_integration_additive_and_max_composition`**: Verifies `Max` returns $\max(\text{inflation}, \text{zone})$ and `Additive` returns $\min(252, \text{inflation} + \text{zone})$.
7. **`test_integration_moving_mob_intercept_prediction`**: Demonstrates that a destination passable at $t=0$ is flagged `COST_LETHAL_OBSTACLE` at $t=5$ when a crossing mob intercepts it, while a lateral detour remains `COST_FREE`.

---

## Audit note (2026-11-19)
Scope: wording about `no_std` and dependencies only; the rest of this report was not re-audited in this pass.
- The "no_std compatibility" claim for `Vec2D` is wrong: `maelstrom-core` does not build with `--no-default-features` (22 errors, for example `f64::floor`, `sin`, `cos` and `round` need `std` or `libm`). `costmap.rs` uses `alloc` types plus `serde`, so "no external dependencies" is also imprecise: it depends on `serde`.
- The crate runs as a `std` library; it currently depends on `serde`, `uuid` and optional codec crates. Adding `libm` or similar to support `no_std` would be a new dependency decision.

## Audit note, addendum (2026-11-20)
Scope: the layer descriptions, the `Additive` test claim and the gate table, checked against `costmap.rs` and by a throwaway probe. The 2026-11-19 `no_std` note stands.
- **Test claim 6 is too broad.** `test_integration_additive_and_max_composition` covers a case whose sum is about 157 (inflation about 127 plus zone 30). `Additive` wrapped modulo 256 above 255: 127 plus 227 gave 98.
  A regression test and the saturating fix (commit 3e6b5c8) were merged into this branch on 2026-11-20 as `8c7d301`; `Additive` now saturates at 252.
- **"When A* plans routes across this layer" / "the cost heuristic naturally hugs sidewalks"** describes no code: nothing plans over `Nav2Costmap`, and the only path search is the uncalled Dijkstra `find_safest_path`.
- **"Strictly monotonic linear decay" is non-increasing** (integer rounding; a 1,000-unit span gave 252, 252, 251, 251, 251), the buffer ends at `d < R_repulse`, and zone penalties are not limited to `50..=127`.
- **The gradient gives no direction inside the lethal cylinder** (cost is flat at 254, so it is exactly zero there) or over flat parts of the buffer.
- **The gate table is a 2026-11-18 snapshot.** `costmap_tests.rs` has 21 tests on this branch and `maelstrom-core`'s lib has 72; "48 tests" and "0 claim overlaps" are not current figures.
