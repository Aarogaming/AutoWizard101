# Dynamic 3D Spatial Indexing & Robotics Navigation

## 1. Architectural Motivation

Standard game automation often flattens environments into 2D grid costmaps or iterates linearly ($O(n)$) over active entity lists on every tick. In Wizard101, this approach breaks down due to two fundamental constraints:
1. **Vertical Geometry & Overlapping 3D Manifolds**: Wizard101 features bridges (Shopping District, Triton Avenue), spiral staircases (Ravenwood towers, Golem Tower), and multi-deck ramps. A flat 2D distance calculation falsely flags mobs walking beneath a bridge as immediate collision hazards for a player walking on top of it.
2. **Computational Scaling**: As zones populate with dozens of moving mobs, static NPC anchors, reagent spawns, and chests, evaluating all entities on every steering tick at $20\text{ Hz}$ wastes CPU cycles.

To solve this, Maelstrom introduces a cache-coherent **3D Bounding Volume Hierarchy (BVH / R-Tree Spatial Index)** in `crates/maelstrom-core/src/spatial_index.rs`, integrated directly into `costmap.rs`.

```
┌────────────────────────────────────────────────────────────────────────┐
│                   SpatialTree3D (3D Dynamic R-Tree)                    │
│   - Hierarchical AABB3D Nodes [min_x, min_y, min_z]..[max_x..max_z]    │
│   - Leaf Entities: Mobs, Reagents, Chests, Portals, Sigils            │
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
- `distance_squared(x, y, z) -> f64`: Euclidean lower-bound distance to box.

### 2.2 Spatial Entity (`SpatialEntity3D`)
```rust
pub struct SpatialEntity3D {
    pub id: u64,
    pub kind: EntityKind, // Mob, Reagent, SilverChest, Npc, Sigil
    pub position: (f64, f64, f64),
    pub velocity: (f64, f64, f64),
    pub radius: f64,
    pub elevation_z: f64,
    pub vertical_tolerance: f64,
    pub bounds: AABB3D,
}
```

---

## 3. Query Interfaces

### 3.1 $O(\log n)$ Range Query (`query_range`)
Finds all entities whose bounding volumes intersect a target 3D query box. Replaces brute-force scans across the entire zone entity list.

### 3.2 $O(\log n)$ Nearest-Neighbor Query (`query_nearest`)
Finds the closest entity of a specified kind (e.g. `EntityKind::Reagent`) within `max_radius`. Used by the opportunistic world detour filter to evaluate candidate harvesting opportunities against the active detour budget.

### 3.3 $O(\log n)$ Corridor Clearance Query (`query_corridor`)
Given two consecutive waypoints $\mathbf{p}_1$ and $\mathbf{p}_2$, constructs a bounding volume around the transit capsule.
1. **Broad-Phase**: Intersects capsule AABB against `SpatialTree3D`, filtering out $95\%+$ of distant zone entities in $O(\log n)$ time.
2. **Narrow-Phase**: For candidate mobs, evaluates exact cylinder-vs-capsule clearance with 3D elevation filtering ($\vert z - z_{\text{mob}} \vert \le \text{tolerance}$). If any mob's predicted aggro volume breaches the corridor, the route is flagged as hazardous.

---

## 4. Integration with Nav2Costmap

In `crates/maelstrom-core/src/costmap.rs`:
- `Nav2Costmap` contains a `SpatialTree3D` indexing dynamic actors.
- Dynamic mob cost evaluation routes through `query_range` bounding the candidate waypoint $+ \text{repulsion\_radius}$.
- Elevation filtering ensures bridge walkways remain `COST_FREE` even when mobs patrol underneath.
