//! Nav2-style 3-layer costmap for spatial routing, obstacle clearance inflation,
//! and time-projected dynamic mob repulsive potential fields.
//!
//! Designed for zero-stub deterministic path evaluation and collision avoidance.
//! Combines:
//! 1. Static Geometry Layer (preferential routing penalties, e.g. streets vs sidewalks).
//! 2. Footprint Inflation Layer (inscribed player/mount radius + clearance buffer).
//! 3. Dynamic Mob Radar Layer (time-projected velocity vectors + inverse-square repulsive field).

use alloc::string::String;
use alloc::vec::Vec;
use serde::{Deserialize, Serialize};

use crate::spatial_index::{EntityKind, SpatialEntity3D, SpatialTree3D};

/// Standardized costmap values compatible with Nav2 conventions.
pub const COST_FREE: u8 = 0;
pub const COST_PREFERENTIAL_MAX: u8 = 127;
pub const COST_INSCRIBED_INFLATED: u8 = 253;
pub const COST_LETHAL_OBSTACLE: u8 = 254;
pub const COST_NO_INFORMATION: u8 = 255;

/// 2D geometric vector and coordinate primitive.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct Vec2D {
    pub x: f64,
    pub y: f64,
}

impl Vec2D {
    pub const fn new(x: f64, y: f64) -> Self {
        Self { x, y }
    }

    pub fn distance_squared(&self, other: &Self) -> f64 {
        let dx = self.x - other.x;
        let dy = self.y - other.y;
        dx * dx + dy * dy
    }

    pub fn distance(&self, other: &Self) -> f64 {
        #[cfg(feature = "std")]
        {
            self.distance_squared(other).sqrt()
        }
        #[cfg(not(feature = "std"))]
        {
            let d2 = self.distance_squared(other);
            if d2 <= 0.0 {
                0.0
            } else {
                // Newton-Raphson approximation for no_std when core sqrt is unavailable
                let mut x = d2;
                let mut y = 1.0;
                for _ in 0..10 {
                    x = (x + y) * 0.5;
                    y = d2 / x;
                }
                x
            }
        }
    }

    pub fn length(&self) -> f64 {
        self.distance(&Self::new(0.0, 0.0))
    }

    pub fn dot(&self, other: &Self) -> f64 {
        self.x * other.x + self.y * other.y
    }
}

/// Static polygon zone with an associated traversal penalty (e.g. street vs sidewalk).
#[derive(Debug, Clone, PartialEq)]
pub struct PolygonZone {
    pub name: String,
    pub vertices: Vec<Vec2D>,
    pub penalty: u8,
}

impl PolygonZone {
    pub fn new(name: impl Into<String>, vertices: Vec<Vec2D>, penalty: u8) -> Self {
        Self {
            name: name.into(),
            vertices,
            penalty,
        }
    }

    /// Point-in-polygon ray-casting test (Jordan curve theorem).
    pub fn contains_point(&self, point: &Vec2D) -> bool {
        if self.vertices.len() < 3 {
            return false;
        }
        let mut inside = false;
        let n = self.vertices.len();
        let mut j = n - 1;

        for i in 0..n {
            let vi = &self.vertices[i];
            let vj = &self.vertices[j];

            let intersects = ((vi.y > point.y) != (vj.y > point.y))
                && (point.x < (vj.x - vi.x) * (point.y - vi.y) / (vj.y - vi.y) + vi.x);

            if intersects {
                inside = !inside;
            }
            j = i;
        }

        inside
    }
}

/// Solid obstacle line segment (wall, railing, impassable barrier).
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct ObstacleSegment {
    pub start: Vec2D,
    pub end: Vec2D,
}

impl ObstacleSegment {
    pub const fn new(start: Vec2D, end: Vec2D) -> Self {
        Self { start, end }
    }

    /// Orthogonal distance from query point to line segment.
    pub fn distance_to_point(&self, p: &Vec2D) -> f64 {
        let seg_len_sq = self.start.distance_squared(&self.end);
        if seg_len_sq < 1e-9 {
            return p.distance(&self.start);
        }

        let ap = Vec2D::new(p.x - self.start.x, p.y - self.start.y);
        let ab = Vec2D::new(self.end.x - self.start.x, self.end.y - self.start.y);
        let t = (ap.dot(&ab) / seg_len_sq).clamp(0.0, 1.0);

        let projection = Vec2D::new(self.start.x + t * ab.x, self.start.y + t * ab.y);
        p.distance(&projection)
    }
}

/// Character footprint and obstacle clearance inflation configuration.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct FootprintInflation {
    /// Inscribed radius where obstacle collision is guaranteed (e.g. 25.0 unmounted, 45.0 mounted).
    pub inscribed_radius: f64,
    /// Outer clearance buffer radius beyond which cost is 0.
    pub inflation_radius: f64,
}

impl FootprintInflation {
    pub fn new(inscribed_radius: f64, inflation_radius: f64) -> Self {
        assert!(
            inscribed_radius > 0.0,
            "inscribed_radius must be strictly positive"
        );
        assert!(
            inflation_radius >= inscribed_radius,
            "inflation_radius must be >= inscribed_radius"
        );
        Self {
            inscribed_radius,
            inflation_radius,
        }
    }

    /// Evaluates cost given the distance to the closest static obstacle.
    pub fn compute_cost(&self, min_obstacle_dist: f64) -> u8 {
        if min_obstacle_dist <= self.inscribed_radius {
            COST_INSCRIBED_INFLATED
        } else if min_obstacle_dist >= self.inflation_radius {
            COST_FREE
        } else {
            let span = self.inflation_radius - self.inscribed_radius;
            if span < 1e-9 {
                COST_INSCRIBED_INFLATED
            } else {
                let ratio = (self.inflation_radius - min_obstacle_dist) / span;
                // Linear decay from 252 down to 1
                let raw_cost = 1.0 + ratio * 251.0;
                (raw_cost.round() as u8).clamp(1, 252)
            }
        }
    }
}

/// Moving dynamic actor (e.g. street patrol mob).
#[derive(Debug, Clone, PartialEq)]
pub struct DynamicMobActor {
    pub id: u64,
    pub position: Vec2D,
    pub velocity: Vec2D,
    pub aggro_radius: f64,
    pub repulsion_radius: f64,
    pub repulsion_weight: f64,
    /// Vertical elevation coordinate in the game world (z).
    pub elevation_z: f64,
    /// Vertical tolerance for vertical deck / bridge separation.
    pub vertical_tolerance: f64,
    /// Maximum lookahead time in seconds for linear velocity projection.
    pub max_lookahead_s: f64,
}

impl DynamicMobActor {
    pub fn new(
        id: u64,
        position: Vec2D,
        velocity: Vec2D,
        aggro_radius: f64,
        repulsion_radius: f64,
        repulsion_weight: f64,
    ) -> Self {
        Self {
            id,
            position,
            velocity,
            aggro_radius,
            repulsion_radius,
            repulsion_weight,
            elevation_z: 0.0,
            vertical_tolerance: 50.0,
            max_lookahead_s: 3.0,
        }
    }

    /// Sets vertical elevation coordinate and separation tolerance for bridge/ramp clearance.
    pub fn with_elevation(mut self, elevation_z: f64, vertical_tolerance: f64) -> Self {
        self.elevation_z = elevation_z;
        self.vertical_tolerance = if vertical_tolerance <= 0.0 {
            50.0
        } else {
            vertical_tolerance
        };
        self
    }

    /// Sets the maximum lookahead horizon for forward linear velocity projection.
    pub fn with_lookahead(mut self, max_lookahead_s: f64) -> Self {
        self.max_lookahead_s = if max_lookahead_s <= 0.0 {
            3.0
        } else {
            max_lookahead_s
        };
        self
    }

    /// Forward-projects the mob's position along its velocity vector to expected arrival time,
    /// clamped to `max_lookahead_s` to prevent linear extrapolation runaway on curved patrol splines.
    pub fn predicted_position(&self, arrival_time_s: f64) -> Vec2D {
        let clamped_t = arrival_time_s.clamp(0.0, self.max_lookahead_s);
        Vec2D::new(
            self.position.x + self.velocity.x * clamped_t,
            self.position.y + self.velocity.y * clamped_t,
        )
    }

    /// Computes dynamic repulsive cost with optional 3D elevation awareness.
    /// If `query_z` is provided and exceeds `vertical_tolerance` from mob elevation, returns `COST_FREE`.
    pub fn compute_cost_3d(&self, point: &Vec2D, query_z: Option<f64>, arrival_time_s: f64) -> u8 {
        if let Some(z) = query_z {
            if (z - self.elevation_z).abs() > self.vertical_tolerance {
                return COST_FREE;
            }
        }

        let predicted = self.predicted_position(arrival_time_s);
        self.cost_for_distance(point.distance(&predicted))
    }

    /// Cost this mob imposes at planar distance `dist` from its (predicted) position.
    /// Non-increasing in `dist`, so the worst cost along a path is the cost at the closest approach.
    pub fn cost_for_distance(&self, dist: f64) -> u8 {
        if dist <= self.aggro_radius {
            COST_LETHAL_OBSTACLE
        } else if dist >= self.repulsion_radius {
            COST_FREE
        } else {
            let clearance = dist - self.aggro_radius;
            // Inverse-square potential field: Cost = Weight / (clearance + 1)^2
            let field = self.repulsion_weight / ((clearance + 1.0) * (clearance + 1.0));
            (field.round() as u8).clamp(1, 253)
        }
    }

    /// Worst cost this mob imposes on a walker who moves in a straight line from `a` (at height `za`)
    /// to `b` (at height `zb`), starting at `start_time_s` and taking `duration_s` to arrive
    /// (`0.0` evaluates the whole segment at `start_time_s`).
    ///
    /// Unlike point sampling this is exact for the model used by `compute_cost_3d`: the mob moves
    /// linearly and is clamped at `max_lookahead_s`, so the walker-to-mob distance is piecewise
    /// quadratic in the position along the segment and its minimum is found analytically. The
    /// elevation filter is applied to the part of the segment inside the vertical tolerance.
    pub fn compute_segment_cost_3d(
        &self,
        a: Vec2D,
        za: f64,
        b: Vec2D,
        zb: f64,
        start_time_s: f64,
        duration_s: f64,
    ) -> u8 {
        // Part of the segment (parameter s in 0..=1) that is within the vertical tolerance.
        let (mut s_lo, mut s_hi) = (0.0_f64, 1.0_f64);
        let dz = zb - za;
        if dz.abs() < 1e-12 {
            if (za - self.elevation_z).abs() > self.vertical_tolerance {
                return COST_FREE;
            }
        } else {
            let s1 = (self.elevation_z - self.vertical_tolerance - za) / dz;
            let s2 = (self.elevation_z + self.vertical_tolerance - za) / dz;
            s_lo = s1.min(s2).max(0.0);
            s_hi = s1.max(s2).min(1.0);
            if s_lo > s_hi {
                return COST_FREE;
            }
        }

        let lookahead = self.max_lookahead_s.max(0.0);
        let duration = duration_s.max(0.0);

        // Split at the times where the mob's clamped projection changes regime.
        let mut cuts = [s_lo, s_hi, s_lo, s_lo];
        let mut cut_count = 2;
        if duration > 0.0 {
            for boundary_time in [0.0, lookahead] {
                let s = (boundary_time - start_time_s) / duration;
                if s > s_lo && s < s_hi {
                    cuts[cut_count] = s;
                    cut_count += 1;
                }
            }
        }
        let cuts = &mut cuts[..cut_count];
        cuts.sort_by(|x, y| x.partial_cmp(y).unwrap_or(core::cmp::Ordering::Equal));

        let (dx, dy) = (b.x - a.x, b.y - a.y);
        let mut min_dist = f64::INFINITY;
        for piece in cuts.windows(2) {
            let (sa, sb) = (piece[0], piece[1]);
            let mid_time = start_time_s + 0.5 * (sa + sb) * duration;
            // R(s) = r0 + s * w is the walker-to-mob vector while the regime holds.
            let (r0x, r0y, wx, wy) = if mid_time <= 0.0 || mid_time >= lookahead {
                let held = if mid_time <= 0.0 { 0.0 } else { lookahead };
                (
                    a.x - (self.position.x + self.velocity.x * held),
                    a.y - (self.position.y + self.velocity.y * held),
                    dx,
                    dy,
                )
            } else {
                (
                    a.x - (self.position.x + self.velocity.x * start_time_s),
                    a.y - (self.position.y + self.velocity.y * start_time_s),
                    dx - self.velocity.x * duration,
                    dy - self.velocity.y * duration,
                )
            };
            let w_sq = wx * wx + wy * wy;
            let s_best = if w_sq < 1e-12 {
                sa
            } else {
                (-(r0x * wx + r0y * wy) / w_sq).clamp(sa, sb)
            };
            let (rx, ry) = (r0x + s_best * wx, r0y + s_best * wy);
            min_dist = min_dist.min((rx * rx + ry * ry).sqrt());
        }
        self.cost_for_distance(min_dist)
    }

    /// Computes dynamic repulsive cost at a candidate point at expected arrival time (2D projection).
    pub fn compute_cost(&self, point: &Vec2D, arrival_time_s: f64) -> u8 {
        self.compute_cost_3d(point, None, arrival_time_s)
    }
}

/// Layer composition mode for multi-layer cost consolidation.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CostmapComposition {
    /// Takes the highest cost among all layers at the query point (Nav2 standard).
    Max,
    /// Adds penalties together, saturating at COST_LETHAL_OBSTACLE.
    Additive,
}

/// Composite 3-layer costmap providing spatial traversal costing and 3D spatial indexing.
#[derive(Debug, Clone, PartialEq)]
pub struct Nav2Costmap {
    pub footprint: FootprintInflation,
    pub composition: CostmapComposition,
    pub zones: Vec<PolygonZone>,
    pub static_obstacles: Vec<ObstacleSegment>,
    pub dynamic_mobs: Vec<DynamicMobActor>,
    pub spatial_index: SpatialTree3D,
}

/// Candidate detour for opportunistic resource harvesting.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct DetourCandidate {
    pub entity_id: u64,
    pub entity_kind: EntityKind,
    pub position: (f64, f64, f64),
    pub delta_cost: f64,
    pub approved: bool,
}

#[inline]
fn distance_3d(a: (f64, f64, f64), b: (f64, f64, f64)) -> f64 {
    let dx = a.0 - b.0;
    let dy = a.1 - b.1;
    let dz = a.2 - b.2;
    (dx * dx + dy * dy + dz * dz).sqrt()
}

#[inline]
fn closest_point_on_segment_3d(
    p: (f64, f64, f64),
    p1: (f64, f64, f64),
    p2: (f64, f64, f64),
) -> (f64, f64, f64) {
    let seg_x = p2.0 - p1.0;
    let seg_y = p2.1 - p1.1;
    let seg_z = p2.2 - p1.2;
    let seg_len_sq = seg_x * seg_x + seg_y * seg_y + seg_z * seg_z;

    if seg_len_sq < 1e-9 {
        return p1;
    }

    let t = ((p.0 - p1.0) * seg_x + (p.1 - p1.1) * seg_y + (p.2 - p1.2) * seg_z) / seg_len_sq;
    let t_clamped = t.clamp(0.0, 1.0);

    (
        p1.0 + seg_x * t_clamped,
        p1.1 + seg_y * t_clamped,
        p1.2 + seg_z * t_clamped,
    )
}

impl Nav2Costmap {
    pub fn new(footprint: FootprintInflation, composition: CostmapComposition) -> Self {
        Self {
            footprint,
            composition,
            zones: Vec::new(),
            static_obstacles: Vec::new(),
            dynamic_mobs: Vec::new(),
            spatial_index: SpatialTree3D::new(),
        }
    }

    pub fn add_zone(&mut self, zone: PolygonZone) {
        self.zones.push(zone);
    }

    pub fn add_obstacle(&mut self, obstacle: ObstacleSegment) {
        self.static_obstacles.push(obstacle);
    }

    fn mob_index_entity(mob: &DynamicMobActor) -> SpatialEntity3D {
        SpatialEntity3D::new(
            mob.id,
            EntityKind::DynamicMob,
            mob.position.x,
            mob.position.y,
            mob.elevation_z,
            mob.repulsion_radius,
        )
        .with_velocity(mob.velocity.x, mob.velocity.y, 0.0)
        .with_cylinder_height(mob.vertical_tolerance)
    }

    /// Appends a mob. It does not look for an existing mob with the same id: a repeated id leaves the
    /// earlier mob in place. Use [`Self::update_mob`] to move or replace a mob.
    pub fn add_mob(&mut self, mob: DynamicMobActor) {
        self.spatial_index.insert(Self::mob_index_entity(&mob));
        self.dynamic_mobs.push(mob);
    }

    /// Replaces the mob with the same id (and its index entity), or adds it when absent. Returns true
    /// when an existing mob was replaced.
    pub fn update_mob(&mut self, mob: DynamicMobActor) -> bool {
        let replaced = self.remove_mob(mob.id);
        self.add_mob(mob);
        replaced
    }

    /// Removes every mob with `id` and its index entity. Returns true when one was removed.
    pub fn remove_mob(&mut self, id: u64) -> bool {
        let before = self.dynamic_mobs.len();
        self.dynamic_mobs.retain(|m| m.id != id);
        self.spatial_index.remove(EntityKind::DynamicMob, id);
        self.dynamic_mobs.len() != before
    }

    /// Adds an arbitrary spatial entity (reagent, chest, NPC anchor) to the 3D spatial index.
    pub fn add_spatial_entity(&mut self, entity: SpatialEntity3D) {
        self.spatial_index.insert(entity);
    }

    /// Evaluates if a 3D transit corridor between two waypoints is free of dynamic mob aggro intrusions.
    ///
    /// Every mob in `dynamic_mobs` is checked at its predicted position (`predicted_position`,
    /// clamped to the mob's lookahead). The spatial index is deliberately not used as a filter here:
    /// its corridor query compares a mob's *current* position with `corridor_radius + repulsion_radius`,
    /// so a mob that is still far away but will have closed in by `arrival_time_s` was dropped before
    /// the predicted-position test ran and the corridor was wrongly reported safe.
    ///
    /// `arrival_time_s` is one time for the whole corridor; a mob is not tracked along the walk (the
    /// nav-mesh planner uses `evaluate_edge_cost_moving` for the time-correct version). A mob whose
    /// elevation differs from the closest corridor point by more than its `vertical_tolerance` is
    /// ignored.
    pub fn is_corridor_safe_3d(
        &self,
        p1: (f64, f64, f64),
        p2: (f64, f64, f64),
        corridor_radius: f64,
        arrival_time_s: f64,
    ) -> bool {
        for mob in &self.dynamic_mobs {
            let pred_2d = mob.predicted_position(arrival_time_s);
            let pred_3d = (pred_2d.x, pred_2d.y, mob.elevation_z);
            let closest = closest_point_on_segment_3d(pred_3d, p1, p2);
            let closest_2d = Vec2D::new(closest.0, closest.1);
            if (closest.2 - mob.elevation_z).abs() <= mob.vertical_tolerance {
                let dist_2d = closest_2d.distance(&pred_2d);
                if dist_2d <= mob.aggro_radius + corridor_radius {
                    return false;
                }
            }
        }
        true
    }

    /// Queries the nearest reagent to a given location within `max_radius`.
    pub fn find_nearest_reagent(
        &self,
        x: f64,
        y: f64,
        z: f64,
        max_radius: f64,
    ) -> Option<(&SpatialEntity3D, f64)> {
        self.spatial_index
            .query_nearest(x, y, z, max_radius, Some(EntityKind::Reagent))
    }

    /// Queries the nearest chest (silver or cantrip) within `max_radius`.
    pub fn find_nearest_chest(
        &self,
        x: f64,
        y: f64,
        z: f64,
        max_radius: f64,
    ) -> Option<(&SpatialEntity3D, f64)> {
        let silver =
            self.spatial_index
                .query_nearest(x, y, z, max_radius, Some(EntityKind::SilverChest));
        let cantrip =
            self.spatial_index
                .query_nearest(x, y, z, max_radius, Some(EntityKind::CantripChest));
        match (silver, cantrip) {
            (Some(s), Some(c)) => {
                if s.1 <= c.1 {
                    Some(s)
                } else {
                    Some(c)
                }
            }
            (Some(s), None) => Some(s),
            (None, Some(c)) => Some(c),
            (None, None) => None,
        }
    }

    /// Evaluates a candidate entity detour against the detour budget equation:
    /// delta_C = (C(curr -> candidate) + C(candidate -> resume)) - C(curr -> resume) < budget
    /// and checks that both transit corridors are free of dynamic mob aggro intrusions.
    #[allow(clippy::too_many_arguments)]
    pub fn evaluate_detour_candidate(
        &self,
        curr: (f64, f64, f64),
        resume: (f64, f64, f64),
        candidate_id: u64,
        candidate_pos: (f64, f64, f64),
        candidate_kind: EntityKind,
        budget: f64,
        corridor_radius: f64,
        arrival_time_s: f64,
    ) -> DetourCandidate {
        let d_direct = distance_3d(curr, resume);
        let d_leg1 = distance_3d(curr, candidate_pos);
        let d_leg2 = distance_3d(candidate_pos, resume);
        let delta_cost = (d_leg1 + d_leg2) - d_direct;

        if delta_cost >= budget {
            return DetourCandidate {
                entity_id: candidate_id,
                entity_kind: candidate_kind,
                position: candidate_pos,
                delta_cost,
                approved: false,
            };
        }

        let leg1_safe =
            self.is_corridor_safe_3d(curr, candidate_pos, corridor_radius, arrival_time_s);
        let leg2_safe = self.is_corridor_safe_3d(
            candidate_pos,
            resume,
            corridor_radius,
            arrival_time_s + (d_leg1 / 10.0),
        );

        let approved = leg1_safe && leg2_safe;

        DetourCandidate {
            entity_id: candidate_id,
            entity_kind: candidate_kind,
            position: candidate_pos,
            delta_cost,
            approved,
        }
    }

    /// Discovers candidate nearby reagents or chests via SpatialTree3D and returns the best approved detour.
    pub fn find_best_detour(
        &self,
        curr: (f64, f64, f64),
        resume: (f64, f64, f64),
        max_scan_radius: f64,
        budget: f64,
        corridor_radius: f64,
        arrival_time_s: f64,
    ) -> Option<DetourCandidate> {
        let mut candidates = Vec::new();

        let reagents = self.spatial_index.query_within_radius(
            curr.0,
            curr.1,
            curr.2,
            max_scan_radius,
            Some(EntityKind::Reagent),
        );
        for (reagent, _) in reagents {
            let det = self.evaluate_detour_candidate(
                curr,
                resume,
                reagent.id,
                (reagent.x, reagent.y, reagent.z),
                reagent.kind,
                budget,
                corridor_radius,
                arrival_time_s,
            );
            if det.approved {
                candidates.push(det);
            }
        }

        let silver_chests = self.spatial_index.query_within_radius(
            curr.0,
            curr.1,
            curr.2,
            max_scan_radius,
            Some(EntityKind::SilverChest),
        );
        for (chest, _) in silver_chests {
            let det = self.evaluate_detour_candidate(
                curr,
                resume,
                chest.id,
                (chest.x, chest.y, chest.z),
                chest.kind,
                budget,
                corridor_radius,
                arrival_time_s,
            );
            if det.approved {
                candidates.push(det);
            }
        }

        let cantrip_chests = self.spatial_index.query_within_radius(
            curr.0,
            curr.1,
            curr.2,
            max_scan_radius,
            Some(EntityKind::CantripChest),
        );
        for (chest, _) in cantrip_chests {
            let det = self.evaluate_detour_candidate(
                curr,
                resume,
                chest.id,
                (chest.x, chest.y, chest.z),
                chest.kind,
                budget,
                corridor_radius,
                arrival_time_s,
            );
            if det.approved {
                candidates.push(det);
            }
        }

        candidates.into_iter().min_by(|a, b| {
            a.delta_cost
                .partial_cmp(&b.delta_cost)
                .unwrap_or(core::cmp::Ordering::Equal)
        })
    }

    /// Evaluates the total cost at `point` at `arrival_time_s` across all 3 layers with 3D elevation awareness.
    pub fn evaluate_cost_3d(&self, point: Vec2D, query_z: Option<f64>, arrival_time_s: f64) -> u8 {
        // Layer 1: Static preferential zone penalty
        let mut zone_cost = COST_FREE;
        for zone in &self.zones {
            if zone.contains_point(&point) {
                zone_cost = zone_cost.max(zone.penalty);
            }
        }

        // Layer 2: Static obstacle clearance inflation
        let mut min_obstacle_dist = f64::INFINITY;
        for obs in &self.static_obstacles {
            let d = obs.distance_to_point(&point);
            if d < min_obstacle_dist {
                min_obstacle_dist = d;
            }
        }
        let inflation_cost = if self.static_obstacles.is_empty() {
            COST_FREE
        } else {
            self.footprint.compute_cost(min_obstacle_dist)
        };

        // Layer 3: Dynamic mob radar repulsive field (elevation-filtered)
        let mut mob_cost = COST_FREE;
        for mob in &self.dynamic_mobs {
            let c = mob.compute_cost_3d(&point, query_z, arrival_time_s);
            mob_cost = mob_cost.max(c);
        }

        // Consolidate via composition policy
        match self.composition {
            CostmapComposition::Max => zone_cost.max(inflation_cost).max(mob_cost),
            CostmapComposition::Additive => {
                if inflation_cost >= COST_LETHAL_OBSTACLE || mob_cost >= COST_LETHAL_OBSTACLE {
                    COST_LETHAL_OBSTACLE
                } else if inflation_cost == COST_INSCRIBED_INFLATED
                    || mob_cost == COST_INSCRIBED_INFLATED
                {
                    COST_INSCRIBED_INFLATED
                } else {
                    let sum = (zone_cost as u32) + (inflation_cost as u32) + (mob_cost as u32);
                    (sum as u8).min(COST_INSCRIBED_INFLATED - 1)
                }
            }
        }
    }

    /// Evaluates the total cost at `point` at `arrival_time_s` across all 3 layers (2D projection).
    pub fn evaluate_cost(&self, point: Vec2D, arrival_time_s: f64) -> u8 {
        self.evaluate_cost_3d(point, None, arrival_time_s)
    }

    /// Returns true if the query point at elevation `query_z` is strictly passable (cost < INSCRIBED_INFLATED).
    pub fn is_passable_3d(&self, point: Vec2D, query_z: Option<f64>, arrival_time_s: f64) -> bool {
        self.evaluate_cost_3d(point, query_z, arrival_time_s) < COST_INSCRIBED_INFLATED
    }

    /// Returns true if the query point is strictly passable (cost < INSCRIBED_INFLATED).
    pub fn is_passable(&self, point: Vec2D, arrival_time_s: f64) -> bool {
        self.is_passable_3d(point, None, arrival_time_s)
    }

    /// Numerically samples the repulsive gradient (-grad C) via central differences with elevation awareness.
    /// Points in the direction of steepest cost descent (away from obstacles and mobs).
    pub fn sample_repulsive_gradient_3d(
        &self,
        point: Vec2D,
        query_z: Option<f64>,
        arrival_time_s: f64,
        delta: f64,
    ) -> Vec2D {
        let step = if delta <= 0.0 { 1.0 } else { delta };

        let c_right =
            self.evaluate_cost_3d(Vec2D::new(point.x + step, point.y), query_z, arrival_time_s)
                as f64;
        let c_left =
            self.evaluate_cost_3d(Vec2D::new(point.x - step, point.y), query_z, arrival_time_s)
                as f64;
        let c_up =
            self.evaluate_cost_3d(Vec2D::new(point.x, point.y + step), query_z, arrival_time_s)
                as f64;
        let c_down =
            self.evaluate_cost_3d(Vec2D::new(point.x, point.y - step), query_z, arrival_time_s)
                as f64;

        let grad_x = (c_right - c_left) / (2.0 * step);
        let grad_y = (c_up - c_down) / (2.0 * step);

        // Repulsive force vector is the negative gradient
        Vec2D::new(-grad_x, -grad_y)
    }

    /// Numerically samples the repulsive gradient (-grad C) via central differences (2D projection).
    pub fn sample_repulsive_gradient(
        &self,
        point: Vec2D,
        arrival_time_s: f64,
        delta: f64,
    ) -> Vec2D {
        self.sample_repulsive_gradient_3d(point, None, arrival_time_s, delta)
    }
}

/// 3D navigation vertex parsed from zone.nav.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct NavVertex {
    pub x: f64,
    pub y: f64,
    pub z: f64,
}

impl NavVertex {
    pub const fn new(x: f64, y: f64, z: f64) -> Self {
        Self { x, y, z }
    }

    pub fn to_vec2d(&self) -> Vec2D {
        Vec2D::new(self.x, self.y)
    }

    pub fn distance_3d(&self, other: &Self) -> f64 {
        let dx = self.x - other.x;
        let dy = self.y - other.y;
        let dz = self.z - other.z;
        let d2 = dx * dx + dy * dy + dz * dz;
        #[cfg(feature = "std")]
        {
            d2.sqrt()
        }
        #[cfg(not(feature = "std"))]
        {
            if d2 <= 0.0 {
                0.0
            } else {
                let mut x = d2;
                let mut y = 1.0;
                for _ in 0..10 {
                    x = (x + y) * 0.5;
                    y = d2 / x;
                }
                x
            }
        }
    }
}

/// Directed edge between two vertex indices.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct NavEdge {
    pub from: usize,
    pub to: usize,
}

impl NavEdge {
    pub const fn new(from: usize, to: usize) -> Self {
        Self { from, to }
    }
}

/// Structural zone navigation graph parsed from little-endian zone.nav.
#[derive(Debug, Clone, PartialEq)]
pub struct ZoneNavMesh {
    pub header_count: i16,
    pub header_unknown: i16,
    pub vertex_records: usize,
    pub vertices: Vec<NavVertex>,
    pub edges: Vec<NavEdge>,
}

fn read_i16_le(data: &[u8], offset: &mut usize) -> Result<i16, &'static str> {
    if data.len() - *offset < 2 {
        return Err("Truncated navigation integer");
    }
    let bytes = [data[*offset], data[*offset + 1]];
    *offset += 2;
    Ok(i16::from_le_bytes(bytes))
}

fn read_f32_le(data: &[u8], offset: &mut usize) -> Result<f32, &'static str> {
    if data.len() - *offset < 4 {
        return Err("Truncated navigation coordinate");
    }
    let bytes = [
        data[*offset],
        data[*offset + 1],
        data[*offset + 2],
        data[*offset + 3],
    ];
    *offset += 4;
    let val = f32::from_le_bytes(bytes);
    if !val.is_finite() {
        return Err("Nonfinite navigation coordinate");
    }
    Ok(val)
}

fn read_i32_le(data: &[u8], offset: &mut usize) -> Result<i32, &'static str> {
    if data.len() - *offset < 4 {
        return Err("Truncated navigation 32-bit integer");
    }
    let bytes = [
        data[*offset],
        data[*offset + 1],
        data[*offset + 2],
        data[*offset + 3],
    ];
    *offset += 4;
    Ok(i32::from_le_bytes(bytes))
}

impl ZoneNavMesh {
    pub const MAXIMUM_VERTICES: usize = 32767;
    pub const MAXIMUM_EDGES: usize = 250000;
    pub const MAXIMUM_BYTES: usize = 6 + Self::MAXIMUM_VERTICES * 14 + 4 + Self::MAXIMUM_EDGES * 4;

    /// Parses binary little-endian zone.nav stream matching NavigationMap.cs contract.
    pub fn parse(data: &[u8]) -> Result<Self, &'static str> {
        if data.len() < 10 || data.len() > Self::MAXIMUM_BYTES {
            return Err("Navigation byte budget exceeded or header truncated");
        }
        let mut offset = 0;

        let header_count = read_i16_le(data, &mut offset)?;
        let records = read_i16_le(data, &mut offset)?;
        let header_unknown = read_i16_le(data, &mut offset)?;

        if header_count < 0 || records <= 0 || header_count > records {
            return Err("Invalid navigation record count");
        }
        let records_usize = records as usize;
        if data.len() - offset < records_usize * 14 + 4 {
            return Err("Truncated vertex payload");
        }

        let mut vertices = Vec::with_capacity(records_usize);
        for _ in 0..records_usize {
            let x = read_f32_le(data, &mut offset)? as f64;
            let y = read_f32_le(data, &mut offset)? as f64;
            let z = read_f32_le(data, &mut offset)? as f64;
            let index = read_i16_le(data, &mut offset)?;
            if index < 0 || (index as usize) >= records_usize {
                return Err("Invalid navigation vertex index");
            }
            // Equivalent to NavigationMap.cs legacy index gap handling
            if (index as usize) == vertices.len() {
                vertices.push(NavVertex::new(x, y, z));
            }
        }

        if vertices.is_empty() {
            return Err("Navigation contains no indexed vertices");
        }

        let edge_count = read_i32_le(data, &mut offset)?;
        if edge_count < 0 || (edge_count as usize) > Self::MAXIMUM_EDGES {
            return Err("Invalid edge count");
        }
        let edge_count_usize = edge_count as usize;
        if data.len() - offset != edge_count_usize * 4 {
            return Err("Edge payload length mismatch or trailing bytes present");
        }

        let mut edges = Vec::with_capacity(edge_count_usize);
        for _ in 0..edge_count_usize {
            let from = read_i16_le(data, &mut offset)?;
            let to = read_i16_le(data, &mut offset)?;
            if from < 0
                || (from as usize) >= vertices.len()
                || to < 0
                || (to as usize) >= vertices.len()
            {
                return Err("Edge vertex index out of bounds");
            }
            edges.push(NavEdge::new(from as usize, to as usize));
        }

        Ok(Self {
            header_count,
            header_unknown,
            vertex_records: records_usize,
            vertices,
            edges,
        })
    }

    /// Returns midpoint (Vec2D, elevation_z) of a directed edge.
    pub fn edge_midpoint(&self, edge: &NavEdge) -> Option<(Vec2D, f64)> {
        let v1 = self.vertices.get(edge.from)?;
        let v2 = self.vertices.get(edge.to)?;
        Some((
            Vec2D::new((v1.x + v2.x) * 0.5, (v1.y + v2.y) * 0.5),
            (v1.z + v2.z) * 0.5,
        ))
    }

    /// Evaluates edge length in 3D Euclidean units.
    pub fn edge_length(&self, edge: &NavEdge) -> Option<f64> {
        let v1 = self.vertices.get(edge.from)?;
        let v2 = self.vertices.get(edge.to)?;
        Some(v1.distance_3d(v2))
    }

    /// Upper bound on samples taken along one edge for the static layers.
    pub const MAXIMUM_EDGE_SAMPLES: usize = 256;

    /// Evaluates the traversal cost of an edge with every point evaluated at `arrival_time_s`
    /// (the time the walker is at the edge's start). Use [`Self::evaluate_edge_cost_moving`] when the
    /// walker's progress along the edge matters.
    ///
    /// Static layers (zones, obstacle inflation) are sampled at least every
    /// `footprint.inscribed_radius` units, up to [`Self::MAXIMUM_EDGE_SAMPLES`] points, so a feature
    /// narrower than that spacing can still be missed. Dynamic mobs are checked exactly through
    /// [`DynamicMobActor::compute_segment_cost_3d`], so a mob cannot hide between samples.
    /// Returns None if the edge is lethal or inscribed anywhere (blocked by wall or mob).
    pub fn evaluate_edge_cost(
        &self,
        edge: &NavEdge,
        costmap: &Nav2Costmap,
        arrival_time_s: f64,
    ) -> Option<f64> {
        self.edge_cost_over_time(edge, costmap, arrival_time_s, 0.0)
    }

    /// Like [`Self::evaluate_edge_cost`], but time advances along the edge: the walker leaves the
    /// start vertex at `depart_time_s` and moves at `speed_units_per_s` (a non-positive or NaN speed
    /// falls back to 10.0), so mobs are evaluated where they will be when the walker reaches each point.
    pub fn evaluate_edge_cost_moving(
        &self,
        edge: &NavEdge,
        costmap: &Nav2Costmap,
        depart_time_s: f64,
        speed_units_per_s: f64,
    ) -> Option<f64> {
        let len = self.edge_length(edge)?;
        let speed = if speed_units_per_s > 0.0 {
            speed_units_per_s
        } else {
            10.0
        };
        self.edge_cost_over_time(edge, costmap, depart_time_s, len / speed)
    }

    fn edge_cost_over_time(
        &self,
        edge: &NavEdge,
        costmap: &Nav2Costmap,
        start_time_s: f64,
        duration_s: f64,
    ) -> Option<f64> {
        let len = self.edge_length(edge)?;
        let v1 = self.vertices.get(edge.from)?;
        let v2 = self.vertices.get(edge.to)?;

        let spacing = costmap.footprint.inscribed_radius;
        let samples = ((len / spacing).ceil() as usize)
            .saturating_add(1)
            .clamp(2, Self::MAXIMUM_EDGE_SAMPLES);

        let mut max_cost = COST_FREE;
        for i in 0..samples {
            let frac = (i as f64) / ((samples - 1) as f64);
            let pt = Vec2D::new(v1.x + (v2.x - v1.x) * frac, v1.y + (v2.y - v1.y) * frac);
            let z = v1.z + (v2.z - v1.z) * frac;
            let c = costmap.evaluate_cost_3d(pt, Some(z), start_time_s + duration_s * frac);

            if c >= COST_INSCRIBED_INFLATED {
                return None;
            }
            max_cost = max_cost.max(c);
        }

        // Exact closest-approach check per mob, so nothing hides between the samples above.
        for mob in &costmap.dynamic_mobs {
            let c = mob.compute_segment_cost_3d(
                v1.to_vec2d(),
                v1.z,
                v2.to_vec2d(),
                v2.z,
                start_time_s,
                duration_s,
            );
            if c >= COST_INSCRIBED_INFLATED {
                return None;
            }
            max_cost = max_cost.max(c);
        }

        // Traversal cost scales with geometric distance and worst-case costmap penalty
        Some(len * (1.0 + (max_cost as f64) / 50.0))
    }

    /// Finds the safest path (lowest cumulative cost) from start to goal vertex,
    /// dynamically routing around dynamic mobs and static cost areas.
    pub fn find_safest_path(
        &self,
        start: usize,
        goal: usize,
        costmap: &Nav2Costmap,
        start_time_s: f64,
        speed_units_per_s: f64,
    ) -> Option<Vec<usize>> {
        if start >= self.vertices.len() || goal >= self.vertices.len() {
            return None;
        }
        if start == goal {
            return Some(alloc::vec![start]);
        }

        let speed = if speed_units_per_s > 0.0 {
            speed_units_per_s
        } else {
            10.0
        };
        let n = self.vertices.len();
        let mut dist = alloc::vec![f64::INFINITY; n];
        // Walking distance along the chosen route; arrival time comes from this, not from `dist`,
        // which is the weighted cost and grows faster than the walk.
        let mut travelled = alloc::vec![0.0_f64; n];
        let mut prev = alloc::vec![None; n];
        let mut visited = alloc::vec![false; n];

        dist[start] = 0.0;

        let mut adj = alloc::vec![Vec::new(); n];
        for edge in &self.edges {
            adj[edge.from].push(*edge);
        }

        for _ in 0..n {
            let mut u = None;
            let mut min_d = f64::INFINITY;
            for i in 0..n {
                if !visited[i] && dist[i] < min_d {
                    min_d = dist[i];
                    u = Some(i);
                }
            }

            let u = match u {
                Some(v) => v,
                None => break,
            };

            if u == goal {
                break;
            }
            visited[u] = true;

            for edge in &adj[u] {
                let v = edge.to;
                if visited[v] {
                    continue;
                }
                let depart_time = start_time_s + travelled[u] / speed;
                if let Some(edge_weight) =
                    self.evaluate_edge_cost_moving(edge, costmap, depart_time, speed)
                {
                    let alt = dist[u] + edge_weight;
                    if alt < dist[v] {
                        dist[v] = alt;
                        prev[v] = Some(u);
                        travelled[v] = travelled[u] + self.edge_length(edge).unwrap_or(0.0);
                    }
                }
            }
        }

        if dist[goal].is_infinite() {
            return None;
        }

        let mut path = Vec::new();
        let mut curr = Some(goal);
        while let Some(node) = curr {
            path.push(node);
            curr = prev[node];
        }
        path.reverse();
        Some(path)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_vec2d_distance() {
        let a = Vec2D::new(0.0, 0.0);
        let b = Vec2D::new(3.0, 4.0);
        assert!((a.distance(&b) - 5.0).abs() < 1e-6);
        assert!((a.distance_squared(&b) - 25.0).abs() < 1e-6);
    }

    #[test]
    fn test_polygon_zone_containment() {
        // Square street from (0,0) to (100, 100)
        let street = PolygonZone::new(
            "street",
            alloc::vec![
                Vec2D::new(0.0, 0.0),
                Vec2D::new(100.0, 0.0),
                Vec2D::new(100.0, 100.0),
                Vec2D::new(0.0, 100.0),
            ],
            60,
        );

        assert!(street.contains_point(&Vec2D::new(50.0, 50.0)));
        assert!(!street.contains_point(&Vec2D::new(150.0, 50.0)));
        assert!(!street.contains_point(&Vec2D::new(-10.0, 50.0)));
    }

    #[test]
    fn test_obstacle_segment_distance() {
        let wall = ObstacleSegment::new(Vec2D::new(0.0, 0.0), Vec2D::new(100.0, 0.0));

        // Point directly above segment midpoint
        assert!((wall.distance_to_point(&Vec2D::new(50.0, 20.0)) - 20.0).abs() < 1e-6);
        // Point off the end
        assert!((wall.distance_to_point(&Vec2D::new(130.0, 40.0)) - 50.0).abs() < 1e-6);
    }

    #[test]
    fn test_footprint_inflation_bounds() {
        let footprint = FootprintInflation::new(25.0, 75.0);

        // Within inscribed radius: collision guaranteed
        assert_eq!(footprint.compute_cost(10.0), COST_INSCRIBED_INFLATED);
        assert_eq!(footprint.compute_cost(25.0), COST_INSCRIBED_INFLATED);

        // Outside inflation radius: free
        assert_eq!(footprint.compute_cost(75.0), COST_FREE);
        assert_eq!(footprint.compute_cost(100.0), COST_FREE);

        // In between: monotonically decreasing cost
        let c_near = footprint.compute_cost(30.0);
        let c_mid = footprint.compute_cost(50.0);
        let c_far = footprint.compute_cost(70.0);

        assert!(c_near > c_mid);
        assert!(c_mid > c_far);
        assert!(c_far >= 1);
        assert!(c_near <= 252);
    }

    #[test]
    fn test_dynamic_mob_lookahead_clamping() {
        // Mob starts at (100, 0) moving towards origin at 10 units/s with default 3.0s lookahead clamp
        let mob = DynamicMobActor::new(
            1,
            Vec2D::new(100.0, 0.0),
            Vec2D::new(-10.0, 0.0),
            20.0,
            80.0,
            1000.0,
        );

        // At t = 0s -> position = (100, 0)
        assert_eq!(mob.predicted_position(0.0), Vec2D::new(100.0, 0.0));
        // At t = 2s -> position = (80, 0)
        assert_eq!(mob.predicted_position(2.0), Vec2D::new(80.0, 0.0));
        // At t = 3s -> position = (70, 0)
        assert_eq!(mob.predicted_position(3.0), Vec2D::new(70.0, 0.0));
        // At t = 10s -> CLAMPED to 3.0s -> position = (70, 0) (not (0, 0) runaway)
        assert_eq!(mob.predicted_position(10.0), Vec2D::new(70.0, 0.0));
    }

    #[test]
    fn test_dynamic_mob_3d_elevation_filtering() {
        // Mob patrolling at z = 0.0 with vertical tolerance 50.0
        let mob = DynamicMobActor::new(
            1,
            Vec2D::new(50.0, 50.0),
            Vec2D::new(0.0, 0.0),
            20.0, // aggro radius
            80.0, // repulsion radius
            1000.0,
        )
        .with_elevation(0.0, 50.0);

        let query_xy = Vec2D::new(50.0, 50.0);

        // Query at same elevation (z = 0.0): directly on mob -> LETHAL
        let cost_ground = mob.compute_cost_3d(&query_xy, Some(0.0), 0.0);
        assert_eq!(cost_ground, COST_LETHAL_OBSTACLE);

        // Query on ramp slightly elevated (z = 30.0 <= tolerance 50.0): still inside vertical cylinder -> LETHAL
        let cost_ramp = mob.compute_cost_3d(&query_xy, Some(30.0), 0.0);
        assert_eq!(cost_ramp, COST_LETHAL_OBSTACLE);

        // Query on overhead bridge (z = 150.0 > tolerance 50.0): vertically separated -> COST_FREE
        let cost_bridge = mob.compute_cost_3d(&query_xy, Some(150.0), 0.0);
        assert_eq!(cost_bridge, COST_FREE);

        // 2D query (query_z = None): ignores vertical separation for backwards compatibility
        let cost_2d = mob.compute_cost(&query_xy, 0.0);
        assert_eq!(cost_2d, COST_LETHAL_OBSTACLE);
    }

    #[test]
    fn test_dynamic_mob_radar_trajectory_projection() {
        // Mob starts at (100, 0) moving towards origin at 10 units/sec (-10, 0) with 20s lookahead
        let mob = DynamicMobActor::new(
            1,
            Vec2D::new(100.0, 0.0),
            Vec2D::new(-10.0, 0.0),
            20.0, // aggro radius
            80.0, // repulsion radius
            1000.0,
        )
        .with_lookahead(20.0);

        let query = Vec2D::new(50.0, 0.0);

        // At t = 0: mob is at (100, 0), distance = 50.0 -> within repulsion radius
        let c0 = mob.compute_cost(&query, 0.0);
        assert!(c0 > COST_FREE && c0 < COST_LETHAL_OBSTACLE);

        // At t = 4: mob is at (60, 0), distance = 10.0 -> within aggro radius (20.0) -> LETHAL
        let c4 = mob.compute_cost(&query, 4.0);
        assert_eq!(c4, COST_LETHAL_OBSTACLE);

        // At t = 10: mob is at (0, 0), distance = 50.0 -> moved past query
        let c10 = mob.compute_cost(&query, 10.0);
        assert!(c10 > COST_FREE && c10 < COST_LETHAL_OBSTACLE);

        // At t = 20: mob is at (-100, 0), distance = 150.0 -> outside repulsion radius (80.0) -> FREE
        let c20 = mob.compute_cost(&query, 20.0);
        assert_eq!(c20, COST_FREE);
    }

    #[test]
    fn test_nav2_costmap_layered_evaluation() {
        let footprint = FootprintInflation::new(20.0, 60.0);
        let mut costmap = Nav2Costmap::new(footprint, CostmapComposition::Max);

        // Layer 1: Street zone
        costmap.add_zone(PolygonZone::new(
            "street",
            alloc::vec![
                Vec2D::new(0.0, 0.0),
                Vec2D::new(100.0, 0.0),
                Vec2D::new(100.0, 100.0),
                Vec2D::new(0.0, 100.0),
            ],
            50,
        ));

        // Layer 2: Wall at x = 0
        costmap.add_obstacle(ObstacleSegment::new(
            Vec2D::new(0.0, 0.0),
            Vec2D::new(0.0, 100.0),
        ));

        // Layer 3: Mob approaching (50, 50)
        costmap.add_mob(DynamicMobActor::new(
            1,
            Vec2D::new(80.0, 50.0),
            Vec2D::new(-10.0, 0.0),
            15.0,
            50.0,
            2000.0,
        ));

        // Query inside street, far from wall and mob: gets street penalty (50)
        let cost_street_only = costmap.evaluate_cost(Vec2D::new(60.0, 90.0), 0.0);
        assert_eq!(cost_street_only, 50);

        // Query right next to wall (distance 5.0 <= inscribed 20.0): gets INSCRIBED_INFLATED
        let cost_wall = costmap.evaluate_cost(Vec2D::new(5.0, 50.0), 0.0);
        assert_eq!(cost_wall, COST_INSCRIBED_INFLATED);

        // Query at (50, 50) at t = 3.0: mob has reached (50, 50), distance = 0.0 -> LETHAL
        let cost_mob_hit = costmap.evaluate_cost(Vec2D::new(50.0, 50.0), 3.0);
        assert_eq!(cost_mob_hit, COST_LETHAL_OBSTACLE);
        assert!(!costmap.is_passable(Vec2D::new(50.0, 50.0), 3.0));

        // Repulsive gradient points away from approaching mob
        let grad = costmap.sample_repulsive_gradient(Vec2D::new(60.0, 50.0), 0.0, 2.0);
        // Mob is to the right at x=80, so cost increases to the right. -grad_x must be negative (pointing left)
        assert!(grad.x < 0.0, "Repulsive gradient must push away from mob");
    }

    #[test]
    fn test_nav_vertex_3d_distance_and_conversion() {
        let v1 = NavVertex::new(0.0, 0.0, 0.0);
        let v2 = NavVertex::new(2.0, 3.0, 6.0);
        assert!((v1.distance_3d(&v2) - 7.0).abs() < 1e-6);
        assert_eq!(v2.to_vec2d(), Vec2D::new(2.0, 3.0));
    }

    #[test]
    fn test_zone_nav_mesh_binary_roundtrip() {
        let mut bytes = Vec::new();
        // Header: header_count: 2, records: 2, header_unknown: 0
        bytes.extend_from_slice(&2i16.to_le_bytes());
        bytes.extend_from_slice(&2i16.to_le_bytes());
        bytes.extend_from_slice(&0i16.to_le_bytes());

        // Vertex 0: (10.0, 20.0, 30.0), index 0
        bytes.extend_from_slice(&10.0f32.to_le_bytes());
        bytes.extend_from_slice(&20.0f32.to_le_bytes());
        bytes.extend_from_slice(&30.0f32.to_le_bytes());
        bytes.extend_from_slice(&0i16.to_le_bytes());

        // Vertex 1: (40.0, 60.0, 30.0), index 1
        bytes.extend_from_slice(&40.0f32.to_le_bytes());
        bytes.extend_from_slice(&60.0f32.to_le_bytes());
        bytes.extend_from_slice(&30.0f32.to_le_bytes());
        bytes.extend_from_slice(&1i16.to_le_bytes());

        // Edge count: 1
        bytes.extend_from_slice(&1i32.to_le_bytes());
        // Edge 0: 0 -> 1
        bytes.extend_from_slice(&0i16.to_le_bytes());
        bytes.extend_from_slice(&1i16.to_le_bytes());

        let mesh = ZoneNavMesh::parse(&bytes).expect("Valid mesh binary must parse cleanly");
        assert_eq!(mesh.header_count, 2);
        assert_eq!(mesh.header_unknown, 0);
        assert_eq!(mesh.vertex_records, 2);
        assert_eq!(mesh.vertices.len(), 2);
        assert_eq!(mesh.edges.len(), 1);
        assert_eq!(mesh.edges[0], NavEdge::new(0, 1));

        // Edge length: sqrt(30^2 + 40^2 + 0^2) = 50.0
        let len = mesh.edge_length(&mesh.edges[0]).unwrap();
        assert!((len - 50.0).abs() < 1e-6);

        // Edge midpoint: ((10+40)/2, (20+60)/2) = (25.0, 40.0), z = 30.0
        let (mid, z) = mesh.edge_midpoint(&mesh.edges[0]).unwrap();
        assert!((mid.x - 25.0).abs() < 1e-6);
        assert!((mid.y - 40.0).abs() < 1e-6);
        assert!((z - 30.0).abs() < 1e-6);
    }

    #[test]
    fn test_zone_nav_mesh_corrupt_payload_rejection() {
        // Truncated buffer (< 10 bytes)
        assert!(ZoneNavMesh::parse(&[0u8; 8]).is_err());

        // Invalid record count (header_count > records)
        let mut bad_header = Vec::new();
        bad_header.extend_from_slice(&5i16.to_le_bytes()); // header_count
        bad_header.extend_from_slice(&2i16.to_le_bytes()); // records
        bad_header.extend_from_slice(&0i16.to_le_bytes()); // unknown
        bad_header.extend_from_slice(&[0u8; 32]);
        assert_eq!(
            ZoneNavMesh::parse(&bad_header),
            Err("Invalid navigation record count")
        );

        // Nonfinite float coordinate in vertex
        let mut nonfinite = Vec::new();
        nonfinite.extend_from_slice(&1i16.to_le_bytes());
        nonfinite.extend_from_slice(&1i16.to_le_bytes());
        nonfinite.extend_from_slice(&0i16.to_le_bytes());
        nonfinite.extend_from_slice(&f32::NAN.to_le_bytes());
        nonfinite.extend_from_slice(&0.0f32.to_le_bytes());
        nonfinite.extend_from_slice(&0.0f32.to_le_bytes());
        nonfinite.extend_from_slice(&0i16.to_le_bytes());
        nonfinite.extend_from_slice(&0i32.to_le_bytes());
        assert_eq!(
            ZoneNavMesh::parse(&nonfinite),
            Err("Nonfinite navigation coordinate")
        );

        // Edge vertex index out of bounds
        let mut oob_edge = Vec::new();
        oob_edge.extend_from_slice(&1i16.to_le_bytes());
        oob_edge.extend_from_slice(&1i16.to_le_bytes());
        oob_edge.extend_from_slice(&0i16.to_le_bytes());
        oob_edge.extend_from_slice(&0.0f32.to_le_bytes());
        oob_edge.extend_from_slice(&0.0f32.to_le_bytes());
        oob_edge.extend_from_slice(&0.0f32.to_le_bytes());
        oob_edge.extend_from_slice(&0i16.to_le_bytes());
        oob_edge.extend_from_slice(&1i32.to_le_bytes()); // 1 edge
        oob_edge.extend_from_slice(&0i16.to_le_bytes()); // from = 0 (valid)
        oob_edge.extend_from_slice(&5i16.to_le_bytes()); // to = 5 (out of bounds!)
        assert_eq!(
            ZoneNavMesh::parse(&oob_edge),
            Err("Edge vertex index out of bounds")
        );

        // Trailing bytes beyond edge payload
        let mut trailing = Vec::new();
        trailing.extend_from_slice(&1i16.to_le_bytes());
        trailing.extend_from_slice(&1i16.to_le_bytes());
        trailing.extend_from_slice(&0i16.to_le_bytes());
        trailing.extend_from_slice(&0.0f32.to_le_bytes());
        trailing.extend_from_slice(&0.0f32.to_le_bytes());
        trailing.extend_from_slice(&0.0f32.to_le_bytes());
        trailing.extend_from_slice(&0i16.to_le_bytes());
        trailing.extend_from_slice(&0i32.to_le_bytes()); // 0 edges
        trailing.extend_from_slice(&[42u8, 99u8]); // 2 trailing garbage bytes
        assert_eq!(
            ZoneNavMesh::parse(&trailing),
            Err("Edge payload length mismatch or trailing bytes present")
        );
    }

    #[test]
    fn test_zone_nav_mesh_evaluate_edge_cost_and_pathfinding() {
        let mesh = ZoneNavMesh {
            header_count: 3,
            header_unknown: 0,
            vertex_records: 3,
            vertices: alloc::vec![
                NavVertex::new(0.0, 0.0, 0.0),
                NavVertex::new(50.0, 0.0, 0.0),
                NavVertex::new(100.0, 0.0, 0.0),
            ],
            edges: alloc::vec![NavEdge::new(0, 1), NavEdge::new(1, 2)],
        };

        let footprint = FootprintInflation::new(10.0, 30.0);
        let mut costmap = Nav2Costmap::new(footprint, CostmapComposition::Max);

        // Without obstacles or mobs, path from 0 to 2 is directly [0, 1, 2]
        let path = mesh.find_safest_path(0, 2, &costmap, 0.0, 10.0);
        assert_eq!(path, Some(alloc::vec![0, 1, 2]));

        // Now place an aggressive dynamic mob directly on the midpoint of edge 0->1 (at x=25)
        costmap.add_mob(DynamicMobActor::new(
            99,
            Vec2D::new(25.0, 0.0),
            Vec2D::new(0.0, 0.0),
            15.0, // aggro radius covers midpoint
            40.0,
            2000.0,
        ));

        // Edge 0->1 is now lethal -> evaluate_edge_cost returns None
        let edge_cost = mesh.evaluate_edge_cost(&mesh.edges[0], &costmap, 0.0);
        assert_eq!(edge_cost, None);

        // Path is now completely blocked (no alternative branch) -> None
        let blocked_path = mesh.find_safest_path(0, 2, &costmap, 0.0, 10.0);
        assert_eq!(blocked_path, None);
    }

    struct Lcg(u64);

    impl Lcg {
        fn next(&mut self) -> f64 {
            self.0 = self
                .0
                .wrapping_mul(6364136223846793005)
                .wrapping_add(1442695040888963407);
            ((self.0 >> 33) as f64) / ((1u64 << 31) as f64)
        }

        fn range(&mut self, lo: f64, hi: f64) -> f64 {
            lo + (hi - lo) * self.next()
        }
    }

    #[test]
    fn test_segment_cost_matches_dense_sampling() {
        // The analytic closest-approach cost must never be below dense point sampling of the same
        // model (monotone cost in distance) and must not exceed it by more than sampling resolution.
        let mut rng = Lcg(42);
        for case in 0..300 {
            let mob = DynamicMobActor::new(
                1,
                Vec2D::new(rng.range(-100.0, 100.0), rng.range(-100.0, 100.0)),
                Vec2D::new(rng.range(-20.0, 20.0), rng.range(-20.0, 20.0)),
                rng.range(5.0, 20.0),
                rng.range(25.0, 80.0),
                rng.range(50.0, 400.0),
            )
            .with_elevation(rng.range(-30.0, 30.0), rng.range(5.0, 60.0))
            .with_lookahead(rng.range(1.0, 8.0));
            let a = Vec2D::new(rng.range(-150.0, 150.0), rng.range(-150.0, 150.0));
            let b = Vec2D::new(rng.range(-150.0, 150.0), rng.range(-150.0, 150.0));
            let (za, zb) = (rng.range(-40.0, 40.0), rng.range(-40.0, 40.0));
            let t0 = rng.range(-1.0, 6.0);
            let duration = if case % 3 == 0 {
                0.0
            } else {
                rng.range(0.0, 20.0)
            };

            let exact = mob.compute_segment_cost_3d(a, za, b, zb, t0, duration);

            let steps = 20_000;
            let mut sampled = COST_FREE;
            for i in 0..=steps {
                let s = (i as f64) / (steps as f64);
                let p = Vec2D::new(a.x + (b.x - a.x) * s, a.y + (b.y - a.y) * s);
                let z = za + (zb - za) * s;
                sampled = sampled.max(mob.compute_cost_3d(&p, Some(z), t0 + duration * s));
            }
            assert!(
                (exact as i32) + 1 >= sampled as i32,
                "case {case}: exact {exact} below dense sampling {sampled}"
            );
            assert!(
                (exact as i32) - (sampled as i32) <= 8,
                "case {case}: exact {exact} far above dense sampling {sampled}"
            );
        }
    }

    #[test]
    fn test_segment_cost_respects_vertical_tolerance_window() {
        // Mob sits on the segment's path but only at heights 0..=10; the walker climbs from 0 to 100,
        // so only the first tenth of the segment is within tolerance and the mob is crossed there.
        let mob =
            DynamicMobActor::new(1, Vec2D::new(5.0, 0.0), Vec2D::new(0.0, 0.0), 3.0, 3.0, 0.0)
                .with_elevation(5.0, 5.0);
        let cost_through = mob.compute_segment_cost_3d(
            Vec2D::new(0.0, 0.0),
            0.0,
            Vec2D::new(10.0, 0.0),
            10.0,
            0.0,
            0.0,
        );
        assert_eq!(cost_through, COST_LETHAL_OBSTACLE);
        // Same plan but the walker is already far above the mob's window.
        let cost_above = mob.compute_segment_cost_3d(
            Vec2D::new(0.0, 0.0),
            100.0,
            Vec2D::new(10.0, 0.0),
            120.0,
            0.0,
            0.0,
        );
        assert_eq!(cost_above, COST_FREE);
    }
}
