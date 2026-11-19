//! `spatial_index`: Cache-coherent 3D Bounding Volume Hierarchy (BVH / R-Tree) spatial index.
//!
//! Provides $O(\log n)$ range queries, nearest-neighbor entity lookups, and corridor clearance
//! checks for moving mobs, reagents, silver chests, and NPC anchors.
//! Designed for `no_std` + `alloc` with zero external dependencies.

use alloc::vec;
use alloc::vec::Vec;
use serde::{Deserialize, Serialize};

/// Type classification for indexed spatial entities.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum EntityKind {
    Player,
    DynamicMob,
    Reagent,
    SilverChest,
    CantripChest,
    NpcAnchor,
    Sigil,
}

/// 3D Axis-Aligned Bounding Box.
#[derive(Debug, Clone, Copy, PartialEq, Serialize, Deserialize)]
pub struct AABB3D {
    pub min_x: f64,
    pub min_y: f64,
    pub min_z: f64,
    pub max_x: f64,
    pub max_y: f64,
    pub max_z: f64,
}

impl AABB3D {
    pub const fn new(
        min_x: f64,
        min_y: f64,
        min_z: f64,
        max_x: f64,
        max_y: f64,
        max_z: f64,
    ) -> Self {
        Self {
            min_x,
            min_y,
            min_z,
            max_x,
            max_y,
            max_z,
        }
    }

    /// Constructs an AABB enclosing a 3D sphere centered at (x, y, z) with radius `r`.
    pub fn from_point_radius(x: f64, y: f64, z: f64, r: f64) -> Self {
        let r = r.abs();
        Self {
            min_x: x - r,
            min_y: y - r,
            min_z: z - r,
            max_x: x + r,
            max_y: y + r,
            max_z: z + r,
        }
    }

    /// Constructs an AABB enclosing a vertical 3D cylinder centered at (x, y, z) with planar radius `r`
    /// and half-height `half_h`.
    pub fn from_point_cylinder(x: f64, y: f64, z: f64, r: f64, half_h: f64) -> Self {
        let r = r.abs();
        let half_h = half_h.abs();
        Self {
            min_x: x - r,
            min_y: y - r,
            min_z: z - half_h,
            max_x: x + r,
            max_y: y + r,
            max_z: z + half_h,
        }
    }

    /// Checks if this AABB intersects another AABB.
    pub fn intersects(&self, other: &Self) -> bool {
        self.min_x <= other.max_x
            && self.max_x >= other.min_x
            && self.min_y <= other.max_y
            && self.max_y >= other.min_y
            && self.min_z <= other.max_z
            && self.max_z >= other.min_z
    }

    /// Checks if a 3D point is contained within this AABB.
    pub fn contains_point(&self, x: f64, y: f64, z: f64) -> bool {
        x >= self.min_x
            && x <= self.max_x
            && y >= self.min_y
            && y <= self.max_y
            && z >= self.min_z
            && z <= self.max_z
    }

    /// Computes the minimal enclosing AABB containing both `self` and `other`.
    pub fn enclosing(&self, other: &Self) -> Self {
        Self {
            min_x: self.min_x.min(other.min_x),
            min_y: self.min_y.min(other.min_y),
            min_z: self.min_z.min(other.min_z),
            max_x: self.max_x.max(other.max_x),
            max_y: self.max_y.max(other.max_y),
            max_z: self.max_z.max(other.max_z),
        }
    }

    /// Squared Euclidean distance from a point to the nearest point on this AABB.
    /// Returns 0.0 if the point is inside the box.
    pub fn distance_squared_to_point(&self, x: f64, y: f64, z: f64) -> f64 {
        let dx = if x < self.min_x {
            self.min_x - x
        } else if x > self.max_x {
            x - self.max_x
        } else {
            0.0
        };
        let dy = if y < self.min_y {
            self.min_y - y
        } else if y > self.max_y {
            y - self.max_y
        } else {
            0.0
        };
        let dz = if z < self.min_z {
            self.min_z - z
        } else if z > self.max_z {
            z - self.max_z
        } else {
            0.0
        };
        dx * dx + dy * dy + dz * dz
    }

    /// Surface area of the AABB (used for heuristic cost evaluations).
    pub fn surface_area(&self) -> f64 {
        let dx = (self.max_x - self.min_x).max(0.0);
        let dy = (self.max_y - self.min_y).max(0.0);
        let dz = (self.max_z - self.min_z).max(0.0);
        2.0 * (dx * dy + dy * dz + dz * dx)
    }
}

/// An indexed entity occupying 3D manifold space.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct SpatialEntity3D {
    pub id: u64,
    pub kind: EntityKind,
    pub x: f64,
    pub y: f64,
    pub z: f64,
    pub vx: f64,
    pub vy: f64,
    pub vz: f64,
    pub radius: f64,
    pub bounds: AABB3D,
}

impl SpatialEntity3D {
    pub fn new(id: u64, kind: EntityKind, x: f64, y: f64, z: f64, radius: f64) -> Self {
        let bounds = AABB3D::from_point_cylinder(x, y, z, radius, 25.0);
        Self {
            id,
            kind,
            x,
            y,
            z,
            vx: 0.0,
            vy: 0.0,
            vz: 0.0,
            radius,
            bounds,
        }
    }

    pub fn with_velocity(mut self, vx: f64, vy: f64, vz: f64) -> Self {
        self.vx = vx;
        self.vy = vy;
        self.vz = vz;
        if vx != 0.0 || vy != 0.0 || vz != 0.0 {
            let projected = self.predicted_bounds(3.0);
            self.bounds = self.bounds.enclosing(&projected);
        }
        self
    }

    pub fn with_cylinder_height(mut self, half_height: f64) -> Self {
        self.bounds = AABB3D::from_point_cylinder(self.x, self.y, self.z, self.radius, half_height);
        if self.vx != 0.0 || self.vy != 0.0 || self.vz != 0.0 {
            let projected = self.predicted_bounds(3.0);
            self.bounds = self.bounds.enclosing(&projected);
        }
        self
    }

    /// Forward-projects the entity's position along its velocity vector.
    pub fn predicted_position(&self, dt_s: f64) -> (f64, f64, f64) {
        (
            self.x + self.vx * dt_s,
            self.y + self.vy * dt_s,
            self.z + self.vz * dt_s,
        )
    }

    /// Forward-projects the entity's bounding box along its velocity vector.
    pub fn predicted_bounds(&self, dt_s: f64) -> AABB3D {
        let (px, py, pz) = self.predicted_position(dt_s);
        let half_h = (self.bounds.max_z - self.bounds.min_z) * 0.5;
        AABB3D::from_point_cylinder(px, py, pz, self.radius, half_h)
    }
}

/// Node representation within the 3D spatial index.
#[derive(Debug, Clone, PartialEq)]
enum SpatialNode {
    Leaf {
        bounds: AABB3D,
        entities: Vec<SpatialEntity3D>,
    },
    Internal {
        bounds: AABB3D,
        children: Vec<SpatialNode>,
    },
}

impl SpatialNode {
    fn bounds(&self) -> AABB3D {
        match self {
            Self::Leaf { bounds, .. } => *bounds,
            Self::Internal { bounds, .. } => *bounds,
        }
    }
}

/// Dynamic 3D Spatial Index tree providing $O(\log n)$ queries across manifold entities.
#[derive(Debug, Clone, PartialEq)]
pub struct SpatialTree3D {
    root: Option<SpatialNode>,
    total_entities: usize,
    max_leaf_capacity: usize,
}

impl Default for SpatialTree3D {
    fn default() -> Self {
        Self::new()
    }
}

impl SpatialTree3D {
    pub fn new() -> Self {
        Self {
            root: None,
            total_entities: 0,
            max_leaf_capacity: 8,
        }
    }

    pub fn with_capacity(max_leaf_capacity: usize) -> Self {
        Self {
            root: None,
            total_entities: 0,
            max_leaf_capacity: max_leaf_capacity.max(2),
        }
    }

    pub fn len(&self) -> usize {
        self.total_entities
    }

    pub fn is_empty(&self) -> bool {
        self.total_entities == 0
    }

    pub fn clear(&mut self) {
        self.root = None;
        self.total_entities = 0;
    }

    /// Inserts an entity into the spatial tree.
    pub fn insert(&mut self, entity: SpatialEntity3D) {
        self.total_entities += 1;
        if self.root.is_none() {
            self.root = Some(SpatialNode::Leaf {
                bounds: entity.bounds,
                entities: vec![entity],
            });
            return;
        }

        let mut root = self.root.take().unwrap();
        insert_recursive(&mut root, entity, self.max_leaf_capacity);
        self.root = Some(root);
    }

    /// Queries all entities whose bounding volumes intersect the given AABB.
    pub fn query_range(&self, query_box: &AABB3D) -> Vec<&SpatialEntity3D> {
        let mut results = Vec::new();
        if let Some(ref root) = self.root {
            query_range_recursive(root, query_box, &mut results);
        }
        results
    }

    /// Finds the nearest entity of an optional kind within `max_radius`.
    pub fn query_nearest(
        &self,
        x: f64,
        y: f64,
        z: f64,
        max_radius: f64,
        kind_filter: Option<EntityKind>,
    ) -> Option<(&SpatialEntity3D, f64)> {
        let max_r2 = max_radius * max_radius;
        let query_box = AABB3D::from_point_radius(x, y, z, max_radius);
        let candidates = self.query_range(&query_box);

        let mut closest: Option<(&SpatialEntity3D, f64)> = None;
        let mut min_d2 = max_r2;

        for entity in candidates {
            if let Some(kind) = kind_filter {
                if entity.kind != kind {
                    continue;
                }
            }
            let dx = entity.x - x;
            let dy = entity.y - y;
            let dz = entity.z - z;
            let d2 = dx * dx + dy * dy + dz * dz;

            if d2 <= min_d2 {
                min_d2 = d2;
                closest = Some((entity, d2.sqrt()));
            }
        }

        closest
    }

    /// Queries all entities matching an optional kind within Euclidean distance `max_radius`.
    pub fn query_within_radius(
        &self,
        x: f64,
        y: f64,
        z: f64,
        max_radius: f64,
        kind_filter: Option<EntityKind>,
    ) -> Vec<(&SpatialEntity3D, f64)> {
        let max_r2 = max_radius * max_radius;
        let query_box = AABB3D::from_point_radius(x, y, z, max_radius);
        let candidates = self.query_range(&query_box);
        let mut results = Vec::new();

        for entity in candidates {
            if let Some(kind) = kind_filter {
                if entity.kind != kind {
                    continue;
                }
            }
            let dx = entity.x - x;
            let dy = entity.y - y;
            let dz = entity.z - z;
            let d2 = dx * dx + dy * dy + dz * dz;

            if d2 <= max_r2 {
                results.push((entity, d2.sqrt()));
            }
        }

        results.sort_by(|a, b| a.1.partial_cmp(&b.1).unwrap_or(core::cmp::Ordering::Equal));
        results
    }

    /// Queries all entities that intersect a 3D transit corridor between two waypoints.
    ///
    /// Performs broad-phase AABB culling followed by narrow-phase capsule-to-sphere/cylinder
    /// distance calculation.
    pub fn query_corridor(
        &self,
        p1: (f64, f64, f64),
        p2: (f64, f64, f64),
        corridor_radius: f64,
    ) -> Vec<&SpatialEntity3D> {
        let min_x = p1.0.min(p2.0) - corridor_radius;
        let min_y = p1.1.min(p2.1) - corridor_radius;
        let min_z = p1.2.min(p2.2) - corridor_radius;
        let max_x = p1.0.max(p2.0) + corridor_radius;
        let max_y = p1.1.max(p2.1) + corridor_radius;
        let max_z = p1.2.max(p2.2) + corridor_radius;

        let corridor_box = AABB3D::new(min_x, min_y, min_z, max_x, max_y, max_z);
        let candidates = self.query_range(&corridor_box);

        let mut hits = Vec::new();
        let r_corr = corridor_radius;

        for entity in candidates {
            // Narrow-phase: point-to-line-segment distance in 3D
            let dist_to_segment =
                distance_point_to_segment_3d((entity.x, entity.y, entity.z), p1, p2);
            if dist_to_segment <= r_corr + entity.radius {
                hits.push(entity);
            }
        }

        hits
    }
}

fn insert_recursive(node: &mut SpatialNode, entity: SpatialEntity3D, max_leaf_cap: usize) {
    match node {
        SpatialNode::Leaf {
            ref mut bounds,
            ref mut entities,
        } => {
            *bounds = bounds.enclosing(&entity.bounds);
            entities.push(entity);

            if entities.len() > max_leaf_cap {
                // Split leaf into two leaves along widest axis
                let all_entities = core::mem::take(entities);
                let (left, right) = split_entities_along_widest_axis(all_entities);

                let left_node = SpatialNode::Leaf {
                    bounds: compute_bounds(&left),
                    entities: left,
                };
                let right_node = SpatialNode::Leaf {
                    bounds: compute_bounds(&right),
                    entities: right,
                };

                *node = SpatialNode::Internal {
                    bounds: left_node.bounds().enclosing(&right_node.bounds()),
                    children: vec![left_node, right_node],
                };
            }
        }
        SpatialNode::Internal {
            ref mut bounds,
            ref mut children,
        } => {
            *bounds = bounds.enclosing(&entity.bounds);

            // Choose child with minimum area expansion
            let mut best_idx = 0;
            let mut min_expansion = f64::INFINITY;

            for (i, child) in children.iter().enumerate() {
                let current_area = child.bounds().surface_area();
                let expanded_area = child.bounds().enclosing(&entity.bounds).surface_area();
                let expansion = expanded_area - current_area;

                if expansion < min_expansion {
                    min_expansion = expansion;
                    best_idx = i;
                }
            }

            insert_recursive(&mut children[best_idx], entity, max_leaf_cap);
        }
    }
}

fn split_entities_along_widest_axis(
    mut entities: Vec<SpatialEntity3D>,
) -> (Vec<SpatialEntity3D>, Vec<SpatialEntity3D>) {
    if entities.len() <= 1 {
        return (entities, Vec::new());
    }

    let bounds = compute_bounds(&entities);
    let dx = bounds.max_x - bounds.min_x;
    let dy = bounds.max_y - bounds.min_y;
    let dz = bounds.max_z - bounds.min_z;

    if dx >= dy && dx >= dz {
        entities.sort_by(|a, b| a.x.partial_cmp(&b.x).unwrap_or(core::cmp::Ordering::Equal));
    } else if dy >= dx && dy >= dz {
        entities.sort_by(|a, b| a.y.partial_cmp(&b.y).unwrap_or(core::cmp::Ordering::Equal));
    } else {
        entities.sort_by(|a, b| a.z.partial_cmp(&b.z).unwrap_or(core::cmp::Ordering::Equal));
    }

    let mid = entities.len() / 2;
    let right = entities.split_off(mid);
    (entities, right)
}

fn compute_bounds(entities: &[SpatialEntity3D]) -> AABB3D {
    if entities.is_empty() {
        return AABB3D::new(0.0, 0.0, 0.0, 0.0, 0.0, 0.0);
    }
    let mut b = entities[0].bounds;
    for e in &entities[1..] {
        b = b.enclosing(&e.bounds);
    }
    b
}

fn query_range_recursive<'a>(
    node: &'a SpatialNode,
    query_box: &AABB3D,
    results: &mut Vec<&'a SpatialEntity3D>,
) {
    if !node.bounds().intersects(query_box) {
        return;
    }

    match node {
        SpatialNode::Leaf { entities, .. } => {
            for entity in entities {
                if entity.bounds.intersects(query_box) {
                    results.push(entity);
                }
            }
        }
        SpatialNode::Internal { children, .. } => {
            for child in children {
                query_range_recursive(child, query_box, results);
            }
        }
    }
}

/// Evaluates distance from a 3D point `p` to a line segment `(p1, p2)`.
fn distance_point_to_segment_3d(
    p: (f64, f64, f64),
    p1: (f64, f64, f64),
    p2: (f64, f64, f64),
) -> f64 {
    let seg_x = p2.0 - p1.0;
    let seg_y = p2.1 - p1.1;
    let seg_z = p2.2 - p1.2;
    let seg_len_sq = seg_x * seg_x + seg_y * seg_y + seg_z * seg_z;

    if seg_len_sq < 1e-9 {
        let dx = p.0 - p1.0;
        let dy = p.1 - p1.1;
        let dz = p.2 - p1.2;
        return (dx * dx + dy * dy + dz * dz).sqrt();
    }

    let t = ((p.0 - p1.0) * seg_x + (p.1 - p1.1) * seg_y + (p.2 - p1.2) * seg_z) / seg_len_sq;
    let t_clamped = t.clamp(0.0, 1.0);

    let proj_x = p1.0 + seg_x * t_clamped;
    let proj_y = p1.1 + seg_y * t_clamped;
    let proj_z = p1.2 + seg_z * t_clamped;

    let dx = p.0 - proj_x;
    let dy = p.1 - proj_y;
    let dz = p.2 - proj_z;

    (dx * dx + dy * dy + dz * dz).sqrt()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_aabb3d_overlap_and_distance() {
        let b1 = AABB3D::new(0.0, 0.0, 0.0, 10.0, 10.0, 10.0);
        let b2 = AABB3D::new(5.0, 5.0, 5.0, 15.0, 15.0, 15.0);
        let b3 = AABB3D::new(20.0, 20.0, 20.0, 30.0, 30.0, 30.0);

        assert!(b1.intersects(&b2));
        assert!(b2.intersects(&b1));
        assert!(!b1.intersects(&b3));

        assert!(b1.contains_point(5.0, 5.0, 5.0));
        assert!(!b1.contains_point(15.0, 5.0, 5.0));

        assert_eq!(b1.distance_squared_to_point(5.0, 5.0, 5.0), 0.0);
        // Distance to (15, 0, 0) is 5.0 -> d^2 = 25.0
        assert_eq!(b1.distance_squared_to_point(15.0, 0.0, 0.0), 25.0);
    }

    #[test]
    fn test_spatial_tree_insert_and_range_query() {
        let mut tree = SpatialTree3D::with_capacity(4);

        // Insert 20 entities across a 200x200x50 area
        for i in 0..20 {
            let x = (i as f64) * 10.0;
            let y = (i as f64) * 10.0;
            let z = 15.0;
            let kind = if i % 2 == 0 {
                EntityKind::DynamicMob
            } else {
                EntityKind::Reagent
            };
            tree.insert(SpatialEntity3D::new(i as u64, kind, x, y, z, 5.0));
        }

        assert_eq!(tree.len(), 20);

        // Query sub-region [0..50, 0..50, 0..30]
        let query_box = AABB3D::new(0.0, 0.0, 0.0, 55.0, 55.0, 30.0);
        let results = tree.query_range(&query_box);
        assert!(!results.is_empty());
        assert!(results.len() < 20);

        for e in results {
            assert!(e.x <= 60.0);
        }
    }

    #[test]
    fn test_spatial_tree_nearest_neighbor() {
        let mut tree = SpatialTree3D::new();

        tree.insert(SpatialEntity3D::new(
            1,
            EntityKind::DynamicMob,
            100.0,
            100.0,
            10.0,
            5.0,
        ));
        tree.insert(SpatialEntity3D::new(
            2,
            EntityKind::Reagent,
            105.0,
            100.0,
            10.0,
            2.0,
        ));
        tree.insert(SpatialEntity3D::new(
            3,
            EntityKind::Reagent,
            150.0,
            100.0,
            10.0,
            2.0,
        ));

        // Find nearest reagent to (101, 100, 10)
        let nearest = tree
            .query_nearest(101.0, 100.0, 10.0, 20.0, Some(EntityKind::Reagent))
            .expect("Should find reagent 2");

        assert_eq!(nearest.0.id, 2);
        assert!((nearest.1 - 4.0).abs() < 1e-4);

        // Search with small radius that misses
        let miss = tree.query_nearest(101.0, 100.0, 10.0, 2.0, Some(EntityKind::Reagent));
        assert!(miss.is_none());
    }

    #[test]
    fn test_spatial_tree_corridor_clearance() {
        let mut tree = SpatialTree3D::new();

        // Mob right next to corridor from (0,0,0) to (100,0,0) at (50, 5, 0)
        tree.insert(SpatialEntity3D::new(
            1,
            EntityKind::DynamicMob,
            50.0,
            5.0,
            0.0,
            4.0,
        ));
        // Distant mob at (50, 50, 0)
        tree.insert(SpatialEntity3D::new(
            2,
            EntityKind::DynamicMob,
            50.0,
            50.0,
            0.0,
            4.0,
        ));

        // Corridor of radius 3.0 along X axis: mob 1 distance is 5.0 <= 3.0 + 4.0 = 7.0 -> hit
        let hits = tree.query_corridor((0.0, 0.0, 0.0), (100.0, 0.0, 0.0), 3.0);
        assert_eq!(hits.len(), 1);
        assert_eq!(hits[0].id, 1);

        // Narrow corridor radius 0.5: distance 5.0 > 0.5 + 4.0 = 4.5 -> miss
        let narrow_hits = tree.query_corridor((0.0, 0.0, 0.0), (100.0, 0.0, 0.0), 0.5);
        assert!(narrow_hits.is_empty());
    }
}
