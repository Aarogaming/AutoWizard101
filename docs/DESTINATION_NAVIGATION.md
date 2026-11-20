# Destination navigation: candidate graph to owned execution

Current checkpoint: 2026-11-14. Source implementation and offline evidence only.
No live Commons-to-Dance route is qualified or enabled by this change.

## Reused boundaries

`NavigationMap` parses the inspected nav format and searches directed edges.
`W101NavigationArchive` reads only zone.nav from bounded read-only WAD streams.
`NavigationAtlas` resolves names in a specific zone/instance/map/profile context;
it rejects unknown connectors, missing prompt qualification, conflicting edge
evidence, unknown clearance and mismatched member hashes. Qualification records
are trusted host evidence, not user-authored permission flags.

`PathFollower` is a pure canonical-coordinate state machine. It emits expiring
forward/signed-turn intentions, consumes separate dispatch/release receipts,
stops at corners and invalidates on loading/context changes. Its defaults are
prototype limits, not measured W101 safety bounds. No reverse recovery exists.

`W101PathSession.RunOwnedAsync` connects injected observations and actuation to
the existing OperationScheduler/GameInputLease. It does not create another input
owner or install hooks. The Python moving-reader wire format is consumed by
`DecodeCandidate`, which explicitly converts W101 yaw into canonical heading.
Source monotonic timestamps are not compared with host counters: the request
start is a conservative observation-time lower bound, with a bounded round trip.
This loses freshness rather than inventing clock synchronization.

The scheduler test uses that Python wire shape, the real follower and the real
scheduler with synthetic input/observation delegates. It is not game physics or
live operation. A production moving-reader binding is deliberately absent until
current-zone identity, timing and reader behavior qualify. A candidate window
alone cannot set MovingReaderQualified.

Named arrival requires post-release prompt verification. The parent W101 visit
still owns native Idle finalization, focus-pause recovery, countdown and overlay.
This nested seam does not claim Idle or start a Dance game.

## Actual installed-archive binding results

Read-only production parser analysis wrote reports to
`.local/nav-binding-108/commons-candidate-binding.json` and
`.local/nav-binding-108/pavilion-candidate-binding.json`.

| Graph | Nearest start / goal | XY connector gaps | Directed path length | Largest edge abs(dZ) | Stored reverse-edge fraction |
| --- | --- | --- | --- | --- | --- |
| Commons | 89 / 209 | 219.776 / 269.149 | 7471.859 | 140.176 | 0.999128 |
| Pavilion | 50 / 5 | 262.999 / 197.979 | 3195.979 | 30.479 | 1.0 |

Commons chain: `89,96,105,112,121,127,263,264,165,174,183,192,199,209`.
Pavilion chain: `50,43,34,23,14,5`.
Largest planar turn: Commons 0.909280 radians; Pavilion 0.785398 radians.

These are nearest-XY structural bindings only. In particular the Commons chain
contains a 140-unit vertical edge difference that needs terrain/level inspection;
neither proximity nor connectivity proves a usable player path. Reciprocal
records describe storage, not physically bidirectional passage.

Member SHA-256 values and five candidates per endpoint are in the JSON reports.
The near-tent endpoint remains unverified as a Dance interaction anchor. The
pre-crossing approach heading was about4.376, but the actual subsequent crossing
used about5.507. Neither is a certified portal trigger center or universal entry
heading. No collision.bcd semantics were adopted.

## Remaining live gates

1. Qualify current zone/instance and a persistent bounded moving-reader session;
   retain serialized hook/scratch ownership. Plausibility does not prove atomicity.
2. Measure acquisition/dispatch/release latency and speed-dependent expiry limits.
   Software watchdogs and message acknowledgements are not physical stop guarantees.
3. Validate start/end connectors and actual corridor/terrain evidence. Successful
   centerline traversal does not prove surrounding clearance.
4. Verify the portal destination and fresh Dance prompt after release; handle
   loading as a new route context, including same-zone transitions.
5. Bind the session to the parent visit's focus/countdown/Idle lifecycle and qualify
   repeated complete runs from varied starts. Until then, do not expose this as
   an operational navigation card or silently promote candidate graph edges.

Cross-language parity should require exact discrete states and documented numeric
tolerances, not bit-identical trigonometry or incidental graph visit counts. Rust
replacement must pass the same contracts and separate live qualification.

## Elevation and live observer follow-up

Production `NavigationMap.FindPath` alternatives in `.local/nav-binding-109`:
excluding199->209 gives a22vertex route of12228.161units. Rejecting every edge
with abs(dZ)>40 gives no Commons89->209 path. The40unit threshold is an experiment,
not a qualified terrain rule. The asymmetric stored edge is95->85. Original
route rises137.786units over565.550XY on183->192, stays raised on192->199, then
descends140.176over513.608XY on199->209. This suggests inspecting raised terrain;
it does not establish player walkability. Vertex-number jumps through263/264
do not locate the largest elevation change.

The pose opt-in now includes pinned bounded zone and moving-window candidates.
Zone decoding uses ActorBody parent as a candidate ClientObject and archived
offsets304/88, with pointer/header/string fences. These remain explicit layout
hypotheses. No zone fact is automatically published. The moving window has three
samples and experimental rejection bounds, not a calibrated control feed.

Live observation recovered from an isolated-worker import failure; a regression
test now verifies pinned helper loading without the tools search path. A subsequent
probe read `WizardCity/Interiors/WC_Housing_Dorm_Interior`, position approximately
(93.16,4.14,0), and three stationary samples. Independent game-window observation
d8d4b844ab41461386b9f059e956d639 confirms dorm/native Idle. Hook restoration and
disconnect were verified. This is one-location corroboration, not moving-feed or
cross-zone qualification. No movement occurred in those probes.

Task110 live calibration checkpoint: right100ms operation9d137e96875e4661a5705ce91afed023, left100ms01f67afdbc7840bcb39813988321892c and forward100msd9f249d8c80042dd88761feb3304f0cc all completed with nativeIdle verified. Eight samples per window captured right yaw+0.241275rad, left-0.291540rad, forward deltaXY(48.907349,-36.790131), approximately61.20units. Sampling intervals roughly47-63ms; nominal key duration is NOT a measured latency/velocity profile. Each moving window used40reads/384bytes. Context/atomicity remain explicitly unqualified; this proves changing readings during bounded action, not continuous route control or clearance.75 focused native checks pass. Next: persistent qualified sampling, coordinate/heading convention calibration and typed portal arrival before Commons->Dance route promotion. No controller remains active; no installation performed.

## Nav2 3-layer costmap & dynamic threat repulsion (2026-11-18)

Implemented natively in pure Rust (`crates/maelstrom-core/src/costmap.rs`, claim `W101-COSTMAP-01`) under an assumed operational model (no client-side anti-cheat; server-side kinematic displacement checks $\Delta d / \Delta t \le v_{\text{max}}$ and server-authoritative combat). **Unverified premise:** nothing in the repository tests that model; [POST-PATCH-DOCTRINE-01](coordination/results/POST-PATCH-DOCTRINE-01.md) records that no anti-cheat behaviour was tested. **Today (audit 2026-11-20, Claude):** this is library code and offline tests. `Nav2Costmap` has no caller outside `costmap.rs` and its own tests, so it replaces no pathing. It provides layered spatial costing in the style of Nav2:

### Layer structure
1. **Layer 1: Static Geometry (`PolygonZone`)**:
   - Traversal cost penalties assigned to polygon regions (e.g. street penalty `50..=127`, sidewalk penalty `0`).
   - Point-in-polygon containment testing via the ray-casting Jordan curve theorem.
   - Gives the edge-cost evaluator (`ZoneNavMesh::evaluate_edge_cost`, used by the Dijkstra `find_safest_path`) a cost that prefers sidewalks and margins without asserting false "sidewalk immunity" (mobs still override). No A* planner uses it.
   - **Today:** `PolygonZone::new` accepts any `u8` penalty. `COST_PREFERENTIAL_MAX` (127) is a constant that nothing enforces, and a probe zone with penalty 254 made its cells lethal and impassable. The ranges above are examples.
2. **Layer 2: Footprint & Obstacle Clearance Inflation (`FootprintInflation`)**:
   - Inscribed player/mount radius $R_{\text{inscribed}}$ (e.g. 25 unmounted, 45 mounted) and outer clearance buffer $R_{\text{inflation}}$ (e.g. 75).
   - Orthogonal distance projection to solid obstacle segments (`ObstacleSegment`).
   - For $d \le R_{\text{inscribed}}$: returns `COST_INSCRIBED_INFLATED` (253).
   - For $R_{\text{inscribed}} < d < R_{\text{inflation}}$: linear decay from 252 toward 1, rounded to an integer (`1 + ratio * 251`). **Today:** that makes the cost non-increasing, not strictly monotonic: over a 1,000-unit span a probe gave 252, 252, 251, 251, 251 at distances 11, 11.5, 12, 12.5 and 13. The value 1 is reached only in the last 0.2% of the span (a probe with a 50-unit span gave 2 at 0.1 unit inside the outer radius).
   - For $d \ge R_{\text{inflation}}$: returns `COST_FREE` (0).
3. **Layer 3: Dynamic Mob Radar & Repulsive Field (`DynamicMobActor`)**:
   - Evaluates roaming patrol mobs with position $(x, y)$, velocity vector $(\Delta x/\Delta t, \Delta y/\Delta t)$, aggro radius $R_{\text{aggro}}$, and repulsion radius $R_{\text{repulse}}$.
   - **Forward Trajectory Projection**: Evaluates predicted mob location at player arrival time:
     $$\vec{P}_{\text{pred}}(t_{\text{arrival}}) = \vec{P}_0 + \vec{V} \cdot t_{\text{arrival}}$$
   - **Inverse-Square Repulsive Potential Field**:
     - Inside aggro cylinder ($d \le R_{\text{aggro}}$): `COST_LETHAL_OBSTACLE` (254).
     - Inside repulsion buffer ($R_{\text{aggro}} < d < R_{\text{repulse}}$):
       $$\text{Cost} = \min\left(253, \; \max\left(1, \; \text{round}\left( k \cdot \frac{1}{(d - R_{\text{aggro}} + 1.0)^2} \right)\right)\right)$$
     - Outside buffer ($d \ge R_{\text{repulse}}$): `COST_FREE` (0). A probe with $R_{\text{repulse}} = 100$ returned 0 at exactly 100.
   - **Today:** $k$ is the mob's `repulsion_weight`, so the cost just outside the aggro radius is about $k$ (200 in a probe with weight 200, 50 one unit further out) and the buffer is steep.
4. **Composition & Gradient Extraction (`Nav2Costmap`)**:
   - Consolidates layers via `CostmapComposition::Max` or `CostmapComposition::Additive`. **Today:** `Additive` sums the layer costs and saturates at 252, one below `COST_INSCRIBED_INFLATED`; before commit 3e6b5c8 (merged as `8c7d301` on 2026-11-20) it narrowed the sum with `sum as u8`, which wrapped modulo 256: a zone penalty of 127 plus an inflation cost of 227 gave 98,
     below the 227 of the obstacle layer alone, so a busier cell looked cheaper. Nothing outside tests uses `Additive`.
   - `sample_repulsive_gradient(point, arrival_time_s, delta)` computes $(-\nabla C)$ via central differences, providing the physical repulsive force vector pushing away from approaching mobs and walls.
     **Today:** it differentiates the integer cost, so it is exactly zero wherever the cost is flat: inside the lethal aggro cylinder (254), inside the inscribed zone (253), and over the part of a buffer
     where the rounded cost has already reached 1. In a probe (aggro 30, repulsion 100, weight 200) the gradient was (0, 0) at distances 5, 20 and 29, (116, 0) at 31, (0.5, 0) at 40 and (0, 0) at 60, so it
     gives no escape direction from inside a lethal cylinder. Its unit is cost per world unit, not a physical force.

### Red Team/Blue Team audit findings & roadmap
An adversarial audit on 2026-11-18 identified three hardening requirements and two subsequent architectural stages:
1. **Verticality & Elevation Aware Filtering (Complete - W101-HARDENING-01)**: Added vertical elevation tolerance $|Z_P - Z_{\text{mob}}| \le \Delta Z_{\text{max}}$ on `DynamicMobActor` and `Nav2Costmap` to prevent mobs patrolling beneath bridges from projecting phantom lethal obstacles onto overhead paths. **Today:** the filter runs only when the caller passes `query_z`; the 2D `evaluate_cost` never applies it. The tolerance defaults to 50 units and a mob's elevation to 0 unless `with_elevation` is called.
2. **Lookahead Clamping (Complete - W101-HARDENING-01)**: Clamped trajectory projection time $t \le 3.0\text{s}$ to prevent linear extrapolation runaway on curved patrol splines. **Today:** 3.0 s is the default `max_lookahead_s`, changeable with `with_lookahead` (a non-positive value falls back to 3.0); negative arrival times clamp to 0.
3. **Asset Ingestion Bridge (Library code and offline tests only; not wired to any caller - W101-ZONE-COSTMAP-02)**: `ZoneNavMesh` in `crates/maelstrom-core/src/costmap.rs` parses binary little-endian `zone.nav` data and agrees with the `NavigationMap.cs` test cases (synthetic bytes only; no real file parsed). `evaluate_edge_cost` checks mobs exactly along each edge and samples static layers at least every inscribed radius (at most 256 points); `find_safest_path` is a Dijkstra over those costs with arrival time taken from walked length, tested against stationary and moving mobs on small graphs. See the audit note of [the result](coordination/results/W101-ZONE-COSTMAP-02.md).
4. **Behavior Tree Orchestration (Library code and offline tests - W101-BEHAVIOR-TREE-02, audited 2026-11-20)**: Embeds a deterministic Priority Behavior Tree (`behavior_tree.rs`) where `FollowNavMeshPath` runs as an interruptible leaf action under higher-priority Combat, Vital Recovery, and Stuck Recovery branches. **Today:** `FollowNavMeshPath` runs no path search: it emits `PROPOSAL:StepWaypoint` while `active_waypoints` (set by the caller) is non-empty and `PROPOSAL:Arrival` when it is empty, and `find_safest_path` has no caller. Only `MaelstromAutonomyBridge` ticks the tree.
5. **Local Steering Controller & Odometry Watchdog (Library code and offline tests - W101-STEERING-01)**: Translates planned waypoint paths into smooth kinematic key pulses (`W`, `A`, `D`) using Pure Pursuit / Dynamic Window Approach (DWA) coupled with an odometry stall detector ($\Delta \text{pos} < \epsilon$ over $>1.5\text{s}$) to trigger stuck recovery. **Today:** the Pure Pursuit controller is called by the offline bridge for `StepWaypoint` intents; `OdometryWatchdog` has no caller, so the blackboard's `is_stuck` is set only by tests.
6. **Dynamic 3D Spatial Indexing (W101-SPATIAL-INDEX-DOCS-01 / CORE-02; library code and offline tests only, used only by the uncalled corridor and detour helpers)**: A height-balanced bounding-volume tree in `crates/maelstrom-core/src/spatial_index.rs` with insert, remove, update and range, nearest-neighbor and corridor queries. Expected-case logarithmic; the planner's per-point cost still scans every mob. See [SPATIAL_INDEXING_ROBOTICS.md](SPATIAL_INDEXING_ROBOTICS.md).
7. **Opportunistic Detour Budget Filter (W101-DETOUR-FILTER-DOCS-01 / CORE-02; library code and offline tests only, not wired to any caller)**: Evaluates candidate entity detours using the differential cost equation $(C_{\text{detour}} + C_{\text{resume}}) - C_{\text{direct}} < \text{budget}$ coupled with corridor mob-aggro thresholding to siphon pathing safely to reagents and chests. See [OPPORTUNISTIC_DETOUR_FILTER.md](OPPORTUNISTIC_DETOUR_FILTER.md).
