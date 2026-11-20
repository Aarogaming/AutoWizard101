# W101-NAV2-DOCS-01 — Navigation & Research Documentation Sync for Nav2 Costmap

Date: 2026-11-18 · owner: root · claim: `W101-NAV2-DOCS-01` · workstream: `W101-NAVIGATION`
Status: **released evidence, corrected 2026-11-20** (see "Audit note"). Documentation updates reflecting native Rust implementation of the Nav2 3-layer costmap engine, adversarial audit findings, and automation readiness milestones.

## 1. Summary

Following the implementation of `W101-COSTMAP-01` (`crates/maelstrom-core/src/costmap.rs`) and the subsequent Red Team / Blue Team adversarial audit, all related navigation and research documentation surfaces have been updated to reflect the current architectural state and forward roadmap without stubs or unverified claims.

---

## 2. Documentation Deliverables

1. **`docs/DESTINATION_NAVIGATION.md`**:
   - Added section `## Nav2 3-layer costmap & dynamic threat repulsion (2026-11-18)`.
   - Detailed the 3-layer spatial architecture:
     - Layer 1: Static Geometry (`PolygonZone`) ray-casting Jordan curve theorem with street penalties ($50..=127$) vs sidewalk ($0$).
     - Layer 2: Footprint & Obstacle Clearance Inflation (`FootprintInflation`) with inscribed radius jump ($253$) and monotonic clearance decay ($252 \rightarrow 1$).
     - Layer 3: Dynamic Mob Radar & Repulsive Field (`DynamicMobActor`) with velocity-based forward trajectory projection $\vec{P}_{\text{pred}} = \vec{P}_0 + \vec{V} \cdot t$, lethal aggro cylinder ($254$), and inverse-square repulsive field $Cost \propto \frac{1}{(d - R_{\text{aggro}} + 1)^2}$.
     - Numerical gradient extraction $\vec{F}_{\text{repulsive}} = -\nabla C$ via central differences.
   - Formally recorded the three hardening items from the adversarial audit: $Z$-elevation awareness ($|Z_P - Z_{\text{mob}}| \le \Delta Z_{\text{max}}$), lookahead clamping ($t \le 3.0\text{s}$), and real zone asset ingestion.
2. **`docs/OPEN_SOURCE_PLAYER_RESEARCH.md`**:
   - Updated the `Nav2 (navigation2)` entry in the pathfinding research table from conceptual proposal to **Implemented natively in pure Rust** (`crates/maelstrom-core/src/costmap.rs`, claim `W101-COSTMAP-01`).
   - Confirmed zero C++/ROS FFI or external runtime dependencies.
3. **`docs/W101_AUTOMATION_READINESS.md`**:
   - Updated row `Avoid enemies while travelling` to reflect implementation of the pure Rust Nav2 3-layer costmap (`crates/maelstrom-core/src/costmap.rs`, `W101-COSTMAP-01`).
   - Recorded remaining gates: live threat tracking, zone asset ingestion, reachable paths, and battle interruption.
4. **`docs/INDEX.md`**:
   - Updated the navigation entry to link `DESTINATION_NAVIGATION.md` and explicitly reference the Nav2 3-layer costmap engine (`W101-COSTMAP-01`).

---

## 3. Verification & Quality Gates

| Gate | Command | Result |
|---|---|---|
| Text Encoding | `python tools/check_text_encoding.py` | 0 violations (clean) |
| Project Board Check | `python tools/check_project_board.py` | Valid: 30 workstreams, 0 claim overlaps |

---

## 4. Invariant Compliance

- **No Overstatement**: Stated clearly that the costmap provides pure mathematical evaluation; zone asset ingestion and live threat tracking remain distinct un-merged gates.
- **Authority**: Edits were strictly bounded to the 5 owned documentation paths in `claims.json`. Zero code or build assets were modified in this claim.

---

## Audit note (audit 2026-11-20, Claude)
Checked against `crates/maelstrom-core/src/costmap.rs`, `tests/costmap_tests.rs`, `maelstrom-core/Cargo.toml`, the repository's other result files and a throwaway probe (not committed).
- **Verified:** the constants (253 inscribed, 252 to 1 decay, 254 lethal), the inverse-square buffer formula (`repulsion_weight / (clearance + 1)^2`, clamped to 1..=253), the forward projection, the 3.0 s
  default lookahead, the elevation tolerance, `Max` and `Additive`, the central-difference gradient, the tests that cover them, and that the four named doc paths and the INDEX entry exist. `maelstrom-core`
  adds no ROS, C++ or FFI dependency. The readiness row already says "not a qualified W101 routine".
- **"Without stubs or unverified claims" does not hold.** The section opens with "the unburdened operational reality (no client-side anti-cheat; server-side kinematic displacement checks ...)"; nothing in the
  repository tests that, and `POST-PATCH-DOCTRINE-01` says no anti-cheat behaviour was tested. It is now labelled an unverified premise.
- **"Replaces bare Euclidean distance pathing" and "gives the A* planner a cost heuristic" are wrong today.** `Nav2Costmap` has no caller outside its own file and tests, and there is no A* planner over it;
  the only planner is the Dijkstra `ZoneNavMesh::find_safest_path`, which also has no caller.
- **Smaller inaccuracies:** the decay is rounded, so non-increasing rather than "strictly monotonic"; the buffer is `d < R_repulse` (0 at equality), not `<=`; `PolygonZone` penalties are not limited to `50..=127`;
  the gradient is zero inside the lethal cylinder and over flat parts of the buffer, so it is not a general "repulsive force pushing away from mobs"; the elevation filter applies only when `query_z` is passed.
- **Defect found, not a claim of this report:** `Additive` composition wrapped modulo 256 in `sum as u8` (127 + 227 gave 98, below the 227 of the obstacle layer alone). Fixed with a regression test in commit 3e6b5c8, merged as `8c7d301` on 2026-11-20.
- **Gate table:** "0 claim overlaps" is not a message the project-board checker prints today (it reports `30 workstreams; evidence paths and dependency graph checked`); both gates pass on 2026-11-20.
  The claim that edits were bounded to the five owned paths was not re-checked (`claims.json` is coordinator-owned).
