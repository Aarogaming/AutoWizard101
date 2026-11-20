# Dynamic 3D Spatial Indexing & Robotics Navigation

> **Status: draft design note, partly aspirational (audit 2026-11-19, Claude).** In code today
> (`crates/maelstrom-core/src/spatial_index.rs`): the box primitives, a height-balanced bounding-volume tree with
> insert, remove and update, and three queries, with unit tests. Not true today: the per-point cost used by the
> planner (`evaluate_cost_3d`) still scans every mob, stored bounds carry a fixed 3.0 s velocity projection, and
> nothing outside tests calls the index. The corridor safety check (`is_corridor_safe_3d`) no longer uses the index: it examines every mob at its predicted position, because the index's corridor query compares current positions. Details are in the audit notes of
> [W101-SPATIAL-INDEX-CORE-02](coordination/results/W101-SPATIAL-INDEX-CORE-02.md). The 20 Hz tick, the zone examples
> and the cache-coherence wording below are unsourced design assumptions, not measurements.

## 1. Architectural Motivation

Standard game automation often flattens environments into 2D grid costmaps or iterates linearly ($O(n)$) over active entity lists on every tick. In Wizard101, this approach breaks down due to two fundamental constraints:
1. **Vertical Geometry & Overlapping 3D Manifolds**: Wizard101 features bridges (Shopping District, Triton Avenue), spiral staircases (Ravenwood towers, Golem Tower), and multi-deck ramps. A flat 2D distance calculation falsely flags mobs walking beneath a bridge as immediate collision hazards for a player walking on top of it. (The zone examples are unsourced; no zone geometry in the repository was used to check them.)
2. **Computational Scaling**: As zones populate with dozens of moving mobs, static NPC anchors, reagent spawns, and chests, evaluating all entities on every steering tick at an assumed $20\text{ Hz}$ would waste CPU cycles. The 20 Hz rate is a proposed starting rate in W101-NAV-AUDIT-103, not a measured or implemented tick, and no profile in the repository shows the cost today.

To address this, Maelstrom has a simple **bounding-volume tree** (a BVH / R-tree hybrid; "cache-coherent" is not measured, and nodes are heap `Vec`s) in `crates/maelstrom-core/src/spatial_index.rs`, held by `Nav2Costmap` in `costmap.rs`.

```
┌────────────────────────────────────────────────────────────────────────┐
│                   SpatialTree3D (3D Dynamic R-Tree)                    │
│   - Hierarchical AABB3D Nodes [min_x, min_y, min_z]..[max_x..max_z]    │
│   - Leaf Entities: Mobs, Reagents, Chests, NPCs,    Sigils            │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
       ┌────────────────────────────┼────────────────────────────┐
       ▼                            ▼                            ▼
┌──────────────────────┐   ┌──────────────────────┐   ┌──────────────────────┐
│ Range Query          │   │ Nearest-Neighbor     │   │ Corridor Query       │
│ O(log n) Overlap Box │   │ O(log n) Sphere      │   │ O(log n) 3D Capsule  │
└──────────────────────┘   └──────────────────────┘   └──────────────────────┘
       │                            │                            │
       ▼                            ▼                            ▼
  Zone POI Lookup             Reagent Detour              Safe Waypoint Steer
```

---

## 2. Core Primitives

### 2.1 3D Axis-Aligned Bounding Box (`AABB3D`)
Represents an immutable 3D spatial bound:
```rust
pub struct AABB3D {
    pub min_x: f64,
    pub min_y: f64,
    pub min_z: f64,
    pub max_x: f64,
    pub max_y: f64,
    pub max_z: f64,
}
```
Supports:
- `intersects(&other) -> bool`: Axis-overlap separation test.
- `contains_point(x, y, z) -> bool`: Inclusion check.
- `enclosing(&other) -> AABB3D`: Computes minimum enclosing bounding volume.
- `distance_squared_to_point(x, y, z) -> f64`: squared distance to the nearest point of the box (0 inside).
- `surface_area()`, `from_point_radius(..)` and `from_point_cylinder(..)` helpers.

### 2.2 Spatial Entity (`SpatialEntity3D`)
```rust
pub struct SpatialEntity3D {
    pub id: u64,
    pub kind: EntityKind, // Player, DynamicMob, Reagent, SilverChest, CantripChest, NpcAnchor, Sigil
    pub x: f64,
    pub y: f64,
    pub z: f64,
    pub vx: f64,
    pub vy: f64,
    pub vz: f64,
    pub radius: f64,
    pub bounds: AABB3D, // cylinder of half-height 25.0 by default, widened by a fixed 3.0 s velocity projection
}
```
There is no `elevation_z` or `vertical_tolerance` on the entity; those live on `DynamicMobActor` in `costmap.rs`.

---

## 3. Query Interfaces

Insertion splits overflowing nodes and propagates the split upward, so every leaf stays at one depth and the height
is logarithmic in the entity count whatever the insertion order (tested with 20,000 entities inserted along a line,
along a diagonal and at one point). Query cost is expected-case logarithmic for spread-out entities; entities that
overlap heavily, such as many at one position, are all returned. `remove` and `update` are keyed by kind and id and
scan the whole tree. Real entity lists in the repository are tens of entities, where none of this matters yet.

### 3.1 Range Query (`query_range`)
Finds all entities whose bounding volumes intersect a target 3D query box. Mobs carry a fixed 3.0 s forward projection in their
stored box, so this query can return a moving mob that is still far from the box.

### 3.2 Nearest-Neighbor Query (`query_nearest`)
Finds the closest entity of a specified kind (e.g. `EntityKind::Reagent`) within `max_radius`. It is a range query over the
radius box followed by a linear scan of the candidates (ties go to the later entity), not a best-first traversal. It is meant
for the opportunistic detour filter, which has no caller yet.

### 3.3 Corridor Query (`query_corridor`)
Given two consecutive waypoints $\mathbf{p}_1$ and $\mathbf{p}_2$, returns entities near the segment between them.
1. **Broad-Phase**: Intersects the segment's box, grown by `corridor_radius`, with the tree. The share of entities removed is
   not measured.
2. **Narrow-Phase**: Keeps an entity when the 3D point-to-segment distance from its current position (not its predicted one)
   is at most `corridor_radius + entity.radius`. It returns every entity kind, applies no elevation tolerance, and flags no
   route. The mob check with `vertical_tolerance` and the predicted position is in `Nav2Costmap::is_corridor_safe_3d`, which does not call this query: a mob far away now can be inside the corridor at arrival, and this query only sees current positions. Nothing outside tests calls `query_corridor`.

---

## 4. Integration with Nav2Costmap

In `crates/maelstrom-core/src/costmap.rs`:
- `Nav2Costmap` holds a `SpatialTree3D` (`spatial_index`) that `add_mob` and `add_spatial_entity` insert into. A mob is stored
  twice: in `dynamic_mobs` and as an index entity whose radius is its `repulsion_radius`.
- The index serves `find_nearest_reagent`, `find_nearest_chest` and `find_best_detour`. `is_corridor_safe_3d` checks every entry of `dynamic_mobs` at its predicted position and does not use the index. The per-point
  cost `evaluate_cost_3d` (used by the nav-mesh planner) loops over every entry in `dynamic_mobs` and does not use the index.
- Elevation filtering for mob cost is in `DynamicMobActor::compute_cost_3d` and, for corridors, in `is_corridor_safe_3d`; it is
  not part of the spatial index.
- `remove(kind, id)` scans the whole tree and `update` is remove plus insert. `Nav2Costmap::update_mob` and
  `remove_mob` keep `dynamic_mobs` and the index in step; `add_mob` still appends without looking for an existing id,
  and `clear()` empties every kind at once.
