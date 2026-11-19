# Evidence: W101-ZONE-COSTMAP-02

**Task**: Phase 2 Zone Asset Ingestion Bridge & Dynamic NavMesh Costmap Integration  
**Owner**: `root`  
**Workstream**: `W101-NAVIGATION`  
**Status**: Released  
**Date**: 2026-11-18  
**Owned Paths**:
- `crates/maelstrom-core/src/costmap.rs`
- `crates/maelstrom-core/tests/costmap_tests.rs`
- `docs/coordination/results/W101-ZONE-COSTMAP-02.md`

---

## 1. Architectural Scope & Implementation

The objective of `W101-ZONE-COSTMAP-02` is Phase 2 of the autonomous navigation architecture: bridging native `.nav` zone geometry with the Nav2 3-layer costmap to provide safe, dynamic graph traversal that avoids dynamic enemy mobs and static zone hazards.

### Core Additions in `crates/maelstrom-core/src/costmap.rs`:
1. **`NavVertex`**:
   - 3D spatial vertex `(x: f64, y: f64, z: f64)` matching unpacked `zone.nav` coordinates.
   - 3D Euclidean distance metric (`distance_3d`) and 2D ground projection (`to_vec2d`).
2. **`NavEdge`**:
   - Directed topological connection between graph vertices `(from: usize, to: usize)`.
3. **`ZoneNavMesh`**:
   - **Binary Stream Parser**: Exact wire-format compatibility with the little-endian `zone.nav` structure (`NavigationMap.cs`):
     - 6-byte header: `header_count: i16`, `records: i16`, `header_unknown: i16`.
     - 14 bytes per vertex: `x: f32`, `y: f32`, `z: f32`, `index: i16`.
     - 4-byte edge count: `edge_count: i32`.
     - 4 bytes per edge: `from: i16`, `to: i16`.
   - **Strict Bounds Verification**:
     - Maximum 32,767 vertices, maximum 250,000 edges.
     - Budget-bounded slice lengths, truncated frame detection, and trailing garbage byte rejection.
     - Non-finite float (`NaN`, `±Inf`) detection and rejection.
     - Out-of-bounds vertex and edge index rejection.
     - Legacy index-gap handling equivalent to native C# client parser.
   - **Multi-Point Edge Cost Evaluation (`evaluate_edge_cost`)**:
     - Evaluates edge traversal against `Nav2Costmap` at projected arrival time $t_{\text{arrival}} = t_0 + \text{dist} / v$.
     - Samples along the segment ($n = 2$ for short, $n = 3$ for medium, $n = 5$ for long edges $\ge 60$ units) to eliminate blindspots on vertices and long paths.
     - Rejects edge (`None`) if any point along the segment encounters inscribed or lethal cost ($\ge 253$).
     - Scales traversal cost: $w = L \cdot (1.0 + \text{cost}_{\text{max}} / 50.0)$.
   - **Dynamic Safest-Path Dijkstra (`find_safest_path`)**:
     - Computes the lowest-cost safe path from `start` to `goal` vertex.
     - Evaluates space-time arrival costs dynamically at each graph expansion step.
     - Automatically routes around approaching dynamic mobs onto clear detour branches.
     - Returns `None` when all branches are lethal or blocked.

---

## 2. Test Verification & Multi-Language Quality Gates

### Unit Tests (`crates/maelstrom-core/src/costmap.rs`):
- `test_nav_vertex_3d_distance_and_conversion`: Verified 3D Euclidean distance $(0, 0, 0) \to (2, 3, 6) = 7.0$ and 2D projection.
- `test_zone_nav_mesh_binary_roundtrip`: Built synthetic binary stream matching wire format; verified header counts, coordinates, edge length, and midpoint.
- `test_zone_nav_mesh_corrupt_payload_rejection`: Verified rejection of truncated headers, invalid record counts, `NaN` floats, out-of-bounds vertex/edge indices, and trailing garbage.
- `test_zone_nav_mesh_evaluate_edge_cost_and_pathfinding`: Verified linear graph traversal and failure when a dynamic mob camps on the single route.

### Integration Tests (`crates/maelstrom-core/tests/costmap_tests.rs`):
- `test_integration_dynamic_mob_deflects_nav_mesh_path`:
  - 4-node diamond mesh topology (Start `(0, 0)`, North fork `(50, 30)`, South fork `(50, -30)`, Goal `(100, 0)`).
  - Baseline path without obstacles passes cleanly.
  - Dynamically placed enemy mob on North fork induces lethal cost on North edges.
  - Path planner automatically detours via South fork `[0, 2, 3]`.
  - Adding a second mob on South fork blocks all routes; planner safely returns `None`.
- `test_integration_elevation_bridge_separation`: Overhead bridge vs underpass mob elevation filtering.
- `test_integration_moving_mob_intercept_prediction`: Predictive trajectory intercept in time-space.
- `test_integration_additive_and_max_composition`: Layered costmap composition policies.

### Quality Gates Execution:
- `cargo test -p maelstrom-core`: 23 unit tests + 4 integration tests passed (27 passed, 0 failed).
- `cargo test --workspace`: 87/87 tests passed across all 5 workspace crates (`maelstrom-core`, `maelstrom-fallback`, `maelstrom-ide`, `maelstrom-platform`, `maelstrom-reflex`).
- `cargo clippy -p maelstrom-core -- -D warnings`: 0 warnings, clean dev profile.
- `rustfmt --check`: 0 formatting changes required.
- `python tools/check_text_encoding.py`: 0 violations across repository.
- `python tools/check_project_board.py`: Valid, 30 workstreams checked.

---

## 3. Red Team / Blue Team Adversarial Self-Audit

### Audit Finding 1 (Critical Defect Discovered & Remediated During Red Team Run):
- **Red Team Attack**: In initial drafting, `evaluate_edge_cost` evaluated the costmap only at the edge midpoint. If an enemy mob was stationed at or near a vertex (e.g. Node 1 at `(50, 30)`), the midpoint of edge $0 \to 1$ was at `(25, 15)` (distance $29.15$ from mob). If the mob's aggro radius was $20.0$, the midpoint was outside the aggro radius, causing the edge to be evaluated as passable despite passing directly through the mob at the destination vertex!
- **Blue Team Remediation**: Refactored `evaluate_edge_cost` to perform multi-point segment evaluation ($n \ge 3$ to $5$ depending on edge length). Every point along the line segment from $v_1$ to $v_2$ is sampled; if any sample touches lethal or inscribed cost, the edge is immediately disqualified. Verified by running the integration test which failed under midpoint-only sampling and passed with multi-point segment sampling.

### Audit Finding 2:
- **Red Team Attack**: Speed assumption in Dijkstra ($t_{\text{arrival}} = t_0 + \text{dist} / v$). If the character experiences input latency or turns slowly, arrival time may lag behind projection, possibly causing collision if a mob reaches an intersection later than expected.
- **Blue Team Mitigation**: The path planning frequency is continuous (reactive local replanning on every observation cycle). Dynamic mob lookahead is clamped to $3.0\text{s}$ to prevent projection divergence, and repulsive fields extend beyond the aggro radius, providing safety buffer margins.

### Audit Finding 3:
- **Red Team Attack**: Does `ZoneNavMesh` assume static coordinate frames across loading screens?
- **Blue Team Mitigation**: No. Each `ZoneNavMesh` belongs strictly to a single zone asset. Loading screen detection invalidates active navigation graphs and flushes local costmap state, respecting zone boundary resets and the architectural invariant `Facts ≠ Authority ≠ Actions`.
