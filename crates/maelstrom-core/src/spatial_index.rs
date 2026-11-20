//! `spatial_index`: Cache-coherent 3D Bounding Volume Hierarchy (BVH / R-Tree) spatial index.
//!
//! Provides $O(\log n)$ range queries, nearest-neighbor entity lookups, and corridor clearance
//! checks for moving mobs, reagents, silver chests, and NPC anchors.
//! Uses no external crates and no OS or UI calls; it currently builds only with std.

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

    fn is_empty(&self) -> bool {
        match self {
            Self::Leaf { entities, .. } => entities.is_empty(),
            Self::Internal { children, .. } => children.is_empty(),
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

    /// Height of the tree: 0 when empty, 1 when the root is a single leaf. Every leaf sits at the
    /// same depth, so this is also the depth of any entity.
    pub fn height(&self) -> usize {
        let mut height = 0;
        let mut node = self.root.as_ref();
        while let Some(current) = node {
            height += 1;
            node = match current {
                SpatialNode::Leaf { .. } => None,
                SpatialNode::Internal { children, .. } => children.first(),
            };
        }
        height
    }

    /// Inserts an entity into the spatial tree.
    ///
    /// Nodes that exceed the capacity split in two and the split propagates upward, growing the
    /// tree only at the root. All leaves therefore stay at one depth (a B-tree style guarantee), so
    /// the height is logarithmic in the entity count whatever the insertion order, and the recursion
    /// depth equals the height.
    pub fn insert(&mut self, entity: SpatialEntity3D) {
        self.total_entities += 1;
        let Some(mut root) = self.root.take() else {
            self.root = Some(SpatialNode::Leaf {
                bounds: entity.bounds,
                entities: vec![entity],
            });
            return;
        };

        if let Some(sibling) = insert_recursive(&mut root, entity, self.max_leaf_capacity) {
            root = SpatialNode::Internal {
                bounds: root.bounds().enclosing(&sibling.bounds()),
                children: vec![root, sibling],
            };
        }
        self.root = Some(root);
    }

    /// Removes every entity of `kind` with `id` and returns how many were removed.
    ///
    /// This scans the whole tree (O(n)): the tree keeps no id index. Nodes that become empty are
    /// dropped and a root left with one child is collapsed; nodes are never moved, so the equal-depth
    /// property holds, but heavy removal can leave under-full nodes.
    pub fn remove(&mut self, kind: EntityKind, id: u64) -> usize {
        let Some(mut root) = self.root.take() else {
            return 0;
        };
        let removed = remove_recursive(&mut root, kind, id);
        self.total_entities -= removed;
        self.root = collapse_root(root);
        removed
    }

    /// Replaces the entity of the same kind and id (or inserts it if absent). Returns true when an
    /// existing entity was replaced. Use this to move an entity: its stored bounds, including the
    /// velocity projection, are rebuilt from the new value.
    pub fn update(&mut self, entity: SpatialEntity3D) -> bool {
        let replaced = self.remove(entity.kind, entity.id) > 0;
        self.insert(entity);
        replaced
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

/// Inserts `entity` below `node`. Returns a new sibling node when `node` overflowed and split; the
/// caller must add it next to `node` (or grow a new root).
fn insert_recursive(
    node: &mut SpatialNode,
    entity: SpatialEntity3D,
    max_leaf_cap: usize,
) -> Option<SpatialNode> {
    match node {
        SpatialNode::Leaf { bounds, entities } => {
            *bounds = bounds.enclosing(&entity.bounds);
            entities.push(entity);

            if entities.len() <= max_leaf_cap {
                return None;
            }
            // Split the leaf in two along its widest axis; the right half becomes a sibling.
            let (left, right) = split_entities_along_widest_axis(core::mem::take(entities));
            *bounds = compute_bounds(&left);
            *entities = left;
            Some(SpatialNode::Leaf {
                bounds: compute_bounds(&right),
                entities: right,
            })
        }
        SpatialNode::Internal { bounds, children } => {
            *bounds = bounds.enclosing(&entity.bounds);

            // Choose the child whose box grows least; ties go to the smaller box.
            let mut best_idx = 0;
            let mut best_key = (f64::INFINITY, f64::INFINITY);
            for (i, child) in children.iter().enumerate() {
                let current_area = child.bounds().surface_area();
                let expanded_area = child.bounds().enclosing(&entity.bounds).surface_area();
                let key = (expanded_area - current_area, current_area);
                if key < best_key {
                    best_key = key;
                    best_idx = i;
                }
            }

            let sibling = insert_recursive(&mut children[best_idx], entity, max_leaf_cap)?;
            children.insert(best_idx + 1, sibling);
            if children.len() <= max_leaf_cap {
                return None;
            }
            let (left, right) = split_nodes_along_widest_axis(core::mem::take(children));
            *bounds = compute_node_bounds(&left);
            *children = left;
            Some(SpatialNode::Internal {
                bounds: compute_node_bounds(&right),
                children: right,
            })
        }
    }
}

fn remove_recursive(node: &mut SpatialNode, kind: EntityKind, id: u64) -> usize {
    match node {
        SpatialNode::Leaf { bounds, entities } => {
            let before = entities.len();
            entities.retain(|e| !(e.kind == kind && e.id == id));
            let removed = before - entities.len();
            if removed > 0 && !entities.is_empty() {
                *bounds = compute_bounds(entities);
            }
            removed
        }
        SpatialNode::Internal { bounds, children } => {
            let mut removed = 0;
            for child in children.iter_mut() {
                removed += remove_recursive(child, kind, id);
            }
            if removed > 0 {
                children.retain(|child| !child.is_empty());
                if !children.is_empty() {
                    *bounds = compute_node_bounds(children);
                }
            }
            removed
        }
    }
}

/// Drops an empty root and collapses a root that has a single child.
fn collapse_root(mut root: SpatialNode) -> Option<SpatialNode> {
    loop {
        if root.is_empty() {
            return None;
        }
        match root {
            SpatialNode::Internal { mut children, .. } if children.len() == 1 => {
                root = children.remove(0);
            }
            other => return Some(other),
        }
    }
}

fn compute_node_bounds(nodes: &[SpatialNode]) -> AABB3D {
    let mut iter = nodes.iter();
    let Some(first) = iter.next() else {
        return AABB3D::new(0.0, 0.0, 0.0, 0.0, 0.0, 0.0);
    };
    iter.fold(first.bounds(), |acc, node| acc.enclosing(&node.bounds()))
}

/// Splits child nodes at the median of their box centres along the widest axis of their union.
fn split_nodes_along_widest_axis(
    mut nodes: Vec<SpatialNode>,
) -> (Vec<SpatialNode>, Vec<SpatialNode>) {
    if nodes.len() <= 1 {
        return (nodes, Vec::new());
    }
    let union = compute_node_bounds(&nodes);
    let dx = union.max_x - union.min_x;
    let dy = union.max_y - union.min_y;
    let dz = union.max_z - union.min_z;
    let centre = |b: AABB3D| -> (f64, f64, f64) {
        (
            (b.min_x + b.max_x) * 0.5,
            (b.min_y + b.max_y) * 0.5,
            (b.min_z + b.max_z) * 0.5,
        )
    };
    let axis = if dx >= dy && dx >= dz {
        0
    } else if dy >= dz {
        1
    } else {
        2
    };
    nodes.sort_by(|a, b| {
        let (ca, cb) = (centre(a.bounds()), centre(b.bounds()));
        let (ka, kb) = match axis {
            0 => (ca.0, cb.0),
            1 => (ca.1, cb.1),
            _ => (ca.2, cb.2),
        };
        ka.partial_cmp(&kb).unwrap_or(core::cmp::Ordering::Equal)
    });
    let mid = nodes.len() / 2;
    let right = nodes.split_off(mid);
    (nodes, right)
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

    fn lcg_entities(seed: u64, count: usize) -> Vec<SpatialEntity3D> {
        let mut state = seed;
        let mut next = move || {
            state = state
                .wrapping_mul(6364136223846793005)
                .wrapping_add(1442695040888963407);
            ((state >> 33) as f64) / ((1u64 << 31) as f64)
        };
        let kinds = [
            EntityKind::DynamicMob,
            EntityKind::Reagent,
            EntityKind::SilverChest,
            EntityKind::NpcAnchor,
        ];
        (0..count)
            .map(|i| {
                let mut e = SpatialEntity3D::new(
                    i as u64,
                    kinds[i % kinds.len()],
                    next() * 2000.0,
                    next() * 2000.0,
                    next() * 60.0,
                    1.0 + next() * 20.0,
                );
                if i % 5 == 0 {
                    e = e.with_velocity(next() * 20.0 - 10.0, next() * 20.0 - 10.0, 0.0);
                }
                e
            })
            .collect()
    }

    fn brute_force_ids(entities: &[SpatialEntity3D], query: &AABB3D) -> Vec<u64> {
        let mut ids: Vec<u64> = entities
            .iter()
            .filter(|e| e.bounds.intersects(query))
            .map(|e| e.id)
            .collect();
        ids.sort_unstable();
        ids
    }

    fn tree_ids(tree: &SpatialTree3D, query: &AABB3D) -> Vec<u64> {
        let mut ids: Vec<u64> = tree.query_range(query).iter().map(|e| e.id).collect();
        ids.sort_unstable();
        ids
    }

    fn probe_boxes() -> Vec<AABB3D> {
        let mut boxes = Vec::new();
        for i in 0..40 {
            let x = (i % 8) as f64 * 250.0;
            let y = (i / 8) as f64 * 400.0;
            boxes.push(AABB3D::new(x, y, -10.0, x + 300.0, y + 300.0, 80.0));
        }
        boxes.push(AABB3D::new(-5000.0, -5000.0, -500.0, 5000.0, 5000.0, 500.0));
        boxes
    }

    #[test]
    fn test_tree_queries_match_brute_force_after_inserts_and_removals() {
        let mut entities = lcg_entities(7, 1_500);
        let mut tree = SpatialTree3D::with_capacity(6);
        for e in &entities {
            tree.insert(e.clone());
        }
        assert_eq!(tree.len(), entities.len());
        for q in probe_boxes() {
            assert_eq!(tree_ids(&tree, &q), brute_force_ids(&entities, &q));
        }

        // Remove every third entity by (kind, id) and re-check.
        let doomed: Vec<(EntityKind, u64)> = entities
            .iter()
            .filter(|e| e.id % 3 == 0)
            .map(|e| (e.kind, e.id))
            .collect();
        for (kind, id) in &doomed {
            assert_eq!(tree.remove(*kind, *id), 1);
        }
        entities.retain(|e| e.id % 3 != 0);
        assert_eq!(tree.len(), entities.len());
        for q in probe_boxes() {
            assert_eq!(tree_ids(&tree, &q), brute_force_ids(&entities, &q));
        }

        // Insert them again after the removals.
        for (kind, id) in &doomed {
            let e = SpatialEntity3D::new(*id, *kind, 10.0 * (*id as f64 % 97.0), 500.0, 20.0, 4.0);
            entities.push(e.clone());
            tree.insert(e);
        }
        assert_eq!(tree.len(), entities.len());
        for q in probe_boxes() {
            assert_eq!(tree_ids(&tree, &q), brute_force_ids(&entities, &q));
        }
    }

    #[test]
    fn test_tree_height_stays_logarithmic_for_any_insertion_order() {
        let n = 20_000usize;
        for name in ["line", "diagonal", "identical"] {
            let position = |i: usize| match name {
                "line" => (i as f64 * 10.0, 0.0, 15.0),
                "diagonal" => (i as f64 * 10.0, i as f64 * 10.0, 15.0),
                _ => (100.0, 100.0, 15.0),
            };
            let mut tree = SpatialTree3D::new();
            for i in 0..n {
                let (x, y, z) = position(i);
                tree.insert(SpatialEntity3D::new(
                    i as u64,
                    EntityKind::Reagent,
                    x,
                    y,
                    z,
                    5.0,
                ));
            }
            // Capacity 8 gives every node at least 4 entries, so 20,000 entities need at most
            // log4(5,000) + 1 internal levels plus the leaf level.
            assert!(
                tree.height() <= 9,
                "{name}: height {} for {n} entities",
                tree.height()
            );
        }
    }

    #[test]
    fn test_remove_and_update_leave_no_stale_entity() {
        let mut tree = SpatialTree3D::new();
        tree.insert(SpatialEntity3D::new(
            5,
            EntityKind::DynamicMob,
            0.0,
            0.0,
            0.0,
            2.0,
        ));
        tree.insert(SpatialEntity3D::new(
            5,
            EntityKind::Reagent,
            0.0,
            0.0,
            0.0,
            2.0,
        ));
        let near_origin = AABB3D::new(-5.0, -5.0, -5.0, 5.0, 5.0, 5.0);
        let near_far = AABB3D::new(495.0, -5.0, -5.0, 505.0, 5.0, 5.0);

        // Same id, different kind: removing the mob keeps the reagent.
        assert_eq!(tree.remove(EntityKind::DynamicMob, 99), 0);
        assert_eq!(tree.len(), 2);

        assert!(tree.update(SpatialEntity3D::new(
            5,
            EntityKind::DynamicMob,
            500.0,
            0.0,
            0.0,
            2.0
        )));
        assert_eq!(tree.len(), 2, "update replaces, it does not add");
        let at_origin = tree.query_range(&near_origin);
        assert_eq!(at_origin.len(), 1);
        assert_eq!(at_origin[0].kind, EntityKind::Reagent);
        let far = tree.query_range(&near_far);
        assert_eq!(far.len(), 1);
        assert_eq!(far[0].kind, EntityKind::DynamicMob);

        // Updating an id that is not present inserts it.
        assert!(!tree.update(SpatialEntity3D::new(
            6,
            EntityKind::Sigil,
            10.0,
            0.0,
            0.0,
            1.0
        )));
        assert_eq!(tree.len(), 3);

        // Remove everything: the tree is empty again and reusable.
        assert_eq!(tree.remove(EntityKind::DynamicMob, 5), 1);
        assert_eq!(tree.remove(EntityKind::Reagent, 5), 1);
        assert_eq!(tree.remove(EntityKind::Sigil, 6), 1);
        assert!(tree.is_empty());
        assert_eq!(tree.height(), 0);
        tree.insert(SpatialEntity3D::new(
            1,
            EntityKind::Reagent,
            0.0,
            0.0,
            0.0,
            1.0,
        ));
        assert_eq!(tree.query_range(&near_origin).len(), 1);
    }

    #[test]
    fn test_line_sorted_inserts_do_not_overflow_the_stack() {
        // Regression: entities inserted in spatial order built a chain about n/4 deep, and the recursive
        // insert overflowed a 1-2 MB stack at a few thousand entities.
        let mut tree = SpatialTree3D::new();
        for i in 0..20_000 {
            tree.insert(SpatialEntity3D::new(
                i as u64,
                EntityKind::Reagent,
                i as f64 * 10.0,
                0.0,
                15.0,
                5.0,
            ));
        }
        assert_eq!(tree.len(), 20_000);
        let probe = AABB3D::new(99_990.0, -20.0, -20.0, 100_010.0, 20.0, 60.0);
        assert!(!tree.query_range(&probe).is_empty());
    }
}
