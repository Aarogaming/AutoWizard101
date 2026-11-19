//! `ontology`: The Irreducible Grammar of Reality for Maelstrom.
//!
//! Codifies the 7 fundamental reality primitives:
//! 1. `Manifold` (Space): Bounded continuous or topological coordinate frames.
//! 2. `Entity` (Occupant): Identifiable occupants carrying spatial volumes and velocities.
//! 3. `Affordance` (Interaction Surface): Proximity/focus-gated valid state transitions.
//! 4. `Connector` (Topology Jump): Directed non-Euclidean links between manifolds.
//! 5. `Hazard` / `Field` (Cost & Threat): Scalar/vector potential fields penalizing presence.
//! 6. `Resource` / `Budget` (Capacity): Quantified internal reservoirs governing feasibility.
//! 7. `Predicate` (Observable Truth): Grounded assertions with confidence and freshness metrics.
//!
//! Adheres strictly to `Facts ≠ Authority ≠ Actions`.

use alloc::collections::BTreeMap;
use alloc::format;
use alloc::string::String;
use alloc::vec::Vec;
use serde::{Deserialize, Serialize};

use crate::goap::{StateValue, WorldState};
use crate::spatial_index::{EntityKind, SpatialEntity3D, AABB3D};

/// Metric type defining distance calculations within a Manifold.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum MetricType {
    Euclidean3D,
    NavMesh3D,
    ScreenPixel2D,
    GraphTopological,
}

/// 1. `Manifold` (Space): A bounded coordinate frame with defined metric and physics caps.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct Manifold {
    pub id: String,
    pub bounds: AABB3D,
    pub metric: MetricType,
    pub gravity_z: f64,
    pub max_velocity: f64,
    pub epoch: u64,
}

impl Manifold {
    pub fn new(id: impl Into<String>, bounds: AABB3D, metric: MetricType) -> Self {
        Self {
            id: id.into(),
            bounds,
            metric,
            gravity_z: -9.81,
            max_velocity: 30.0,
            epoch: 1,
        }
    }

    pub fn with_velocity_cap(mut self, max_vel: f64) -> Self {
        self.max_velocity = max_vel.max(0.1);
        self
    }

    pub fn increment_epoch(&mut self) {
        self.epoch = self.epoch.wrapping_add(1);
    }
}

/// 2. `Entity` (Occupant): Any identifiable thing occupying a Manifold.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct Entity {
    pub id: u64,
    pub manifold_id: String,
    pub kind: EntityKind,
    pub position: (f64, f64, f64),
    pub velocity: (f64, f64, f64),
    pub radius: f64,
    pub half_height: f64,
}

impl Entity {
    pub fn new(
        id: u64,
        manifold_id: impl Into<String>,
        kind: EntityKind,
        position: (f64, f64, f64),
    ) -> Self {
        Self {
            id,
            manifold_id: manifold_id.into(),
            kind,
            position,
            velocity: (0.0, 0.0, 0.0),
            radius: 5.0,
            half_height: 25.0,
        }
    }

    pub fn with_velocity(mut self, vx: f64, vy: f64, vz: f64) -> Self {
        self.velocity = (vx, vy, vz);
        self
    }

    pub fn with_dimensions(mut self, radius: f64, half_height: f64) -> Self {
        self.radius = radius.max(0.1);
        self.half_height = half_height.max(0.1);
        self
    }

    pub fn predicted_position(&self, dt_s: f64) -> (f64, f64, f64) {
        (
            self.position.0 + self.velocity.0 * dt_s,
            self.position.1 + self.velocity.1 * dt_s,
            self.position.2 + self.velocity.2 * dt_s,
        )
    }

    pub fn to_spatial_entity(&self) -> SpatialEntity3D {
        SpatialEntity3D::new(
            self.id,
            self.kind,
            self.position.0,
            self.position.1,
            self.position.2,
            self.radius,
        )
        .with_velocity(self.velocity.0, self.velocity.1, self.velocity.2)
        .with_cylinder_height(self.half_height)
    }
}

/// Interaction surface taxonomy for Affordances.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub enum AffordanceKind {
    InteractKey(char),
    DialogueNext,
    SigilCountdown,
    SpellSlot(usize),
    ButtonWidget(String),
}

/// 3. `Affordance` (Interaction Surface): Proximity/focus-gated state transition surface.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct Affordance {
    pub id: u64,
    pub parent_entity_id: Option<u64>,
    pub manifold_id: String,
    pub kind: AffordanceKind,
    pub position: (f64, f64, f64),
    pub interaction_radius: f64,
    pub enabled: bool,
}

impl Affordance {
    pub fn new(
        id: u64,
        manifold_id: impl Into<String>,
        kind: AffordanceKind,
        position: (f64, f64, f64),
        radius: f64,
    ) -> Self {
        Self {
            id,
            parent_entity_id: None,
            manifold_id: manifold_id.into(),
            kind,
            position,
            interaction_radius: radius.max(0.1),
            enabled: true,
        }
    }

    pub fn with_parent_entity(mut self, entity_id: u64) -> Self {
        self.parent_entity_id = Some(entity_id);
        self
    }

    pub fn is_accessible(&self, player_pos: (f64, f64, f64)) -> bool {
        if !self.enabled {
            return false;
        }
        let dx = self.position.0 - player_pos.0;
        let dy = self.position.1 - player_pos.1;
        let dz = self.position.2 - player_pos.2;
        let d = (dx * dx + dy * dy + dz * dz).sqrt();
        d <= self.interaction_radius
    }
}

/// Topology jump classification.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum ConnectorKind {
    DoorPortal,
    SpiralWorldGate,
    DungeonSigil,
    RealmHop,
    ViewportSwitch,
}

/// 4. `Connector` (Topology Jump): Directed edge linking two disjoint Manifolds.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct Connector {
    pub id: String,
    pub from_manifold: String,
    pub to_manifold: String,
    pub source_anchor: (f64, f64, f64),
    pub target_anchor: (f64, f64, f64),
    pub kind: ConnectorKind,
    pub traversal_cost: f64,
    pub bidirectional: bool,
}

impl Connector {
    pub fn new(
        id: impl Into<String>,
        from: impl Into<String>,
        to: impl Into<String>,
        source: (f64, f64, f64),
        target: (f64, f64, f64),
        kind: ConnectorKind,
        cost: f64,
    ) -> Self {
        Self {
            id: id.into(),
            from_manifold: from.into(),
            to_manifold: to.into(),
            source_anchor: source,
            target_anchor: target,
            kind,
            traversal_cost: cost.max(0.1),
            bidirectional: true,
        }
    }
}

/// Threat profile category.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum HazardKind {
    DynamicAggroCylinder,
    StaticObstacle,
    LethalPerimeter,
    CostInflationZone,
}

/// 5. `Hazard` / `Field` (Cost & Threat): Scalar/vector potential field penalizing occupancy.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct Hazard {
    pub id: u64,
    pub manifold_id: String,
    pub kind: HazardKind,
    pub position: (f64, f64, f64),
    pub velocity: (f64, f64, f64),
    pub aggro_radius: f64,
    pub repulsion_radius: f64,
    pub vertical_tolerance: f64,
    pub penalty_weight: f64,
}

impl Hazard {
    pub fn dynamic_aggro(
        id: u64,
        manifold_id: impl Into<String>,
        position: (f64, f64, f64),
        velocity: (f64, f64, f64),
        aggro_r: f64,
        repulsion_r: f64,
    ) -> Self {
        Self {
            id,
            manifold_id: manifold_id.into(),
            kind: HazardKind::DynamicAggroCylinder,
            position,
            velocity,
            aggro_radius: aggro_r.max(0.1),
            repulsion_radius: repulsion_r.max(aggro_r),
            vertical_tolerance: 30.0,
            penalty_weight: 1000.0,
        }
    }

    /// Evaluates threat cost at query position at expected arrival time.
    /// Returns 254 (lethal) if within aggro radius and vertical tolerance.
    pub fn evaluate_threat_cost(&self, query_pos: (f64, f64, f64), arrival_time_s: f64) -> u8 {
        if (query_pos.2 - self.position.2).abs() > self.vertical_tolerance {
            return 0; // Separated vertically (e.g. bridge)
        }

        let clamped_t = arrival_time_s.clamp(0.0, 3.0);
        let pred_x = self.position.0 + self.velocity.0 * clamped_t;
        let pred_y = self.position.1 + self.velocity.1 * clamped_t;

        let dx = query_pos.0 - pred_x;
        let dy = query_pos.1 - pred_y;
        let dist = (dx * dx + dy * dy).sqrt();

        if dist <= self.aggro_radius {
            254 // COST_LETHAL_OBSTACLE
        } else if dist >= self.repulsion_radius {
            0
        } else {
            let clearance = dist - self.aggro_radius;
            let field = self.penalty_weight / ((clearance + 1.0) * (clearance + 1.0));
            (field.round() as u8).clamp(1, 253)
        }
    }
}

/// 6. `Resource` / `Budget` (Capacity): Quantified internal reservoir governing feasibility.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct Resource {
    pub id: String,
    pub current: f64,
    pub maximum: f64,
    pub replenish_rate: f64,
}

impl Resource {
    pub fn new(id: impl Into<String>, current: f64, maximum: f64) -> Self {
        let max_val = maximum.max(1.0);
        Self {
            id: id.into(),
            current: current.clamp(0.0, max_val),
            maximum: max_val,
            replenish_rate: 0.0,
        }
    }

    pub fn ratio(&self) -> f64 {
        if self.maximum <= 0.0 {
            0.0
        } else {
            self.current / self.maximum
        }
    }

    pub fn is_depleted(&self) -> bool {
        self.current <= 0.0
    }

    pub fn consume(&mut self, amount: f64) -> bool {
        if amount <= self.current {
            self.current -= amount;
            true
        } else {
            false
        }
    }

    pub fn replenish(&mut self, amount: f64) {
        self.current = (self.current + amount).min(self.maximum);
    }
}

/// 7. `Predicate` (Observable Truth): Grounded semantic assertion with epistemic confidence.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct Predicate {
    pub key: String,
    pub value: StateValue,
    pub confidence: f64,
    pub timestamp_ms: u64,
}

impl Predicate {
    pub fn new(
        key: impl Into<String>,
        value: impl Into<StateValue>,
        confidence: f64,
        timestamp_ms: u64,
    ) -> Self {
        Self {
            key: key.into(),
            value: value.into(),
            confidence: confidence.clamp(0.0, 1.0),
            timestamp_ms,
        }
    }

    pub fn is_fresh(&self, current_time_ms: u64, ttl_ms: u64) -> bool {
        current_time_ms >= self.timestamp_ms && (current_time_ms - self.timestamp_ms) <= ttl_ms
    }
}

/// Unified Reality World Model aggregating the 7 primitives.
#[derive(Debug, Clone, Default, PartialEq, Serialize, Deserialize)]
pub struct RealityWorldModel {
    pub active_manifold_id: Option<String>,
    pub manifolds: BTreeMap<String, Manifold>,
    pub entities: BTreeMap<u64, Entity>,
    pub affordances: BTreeMap<u64, Affordance>,
    pub connectors: Vec<Connector>,
    pub hazards: BTreeMap<u64, Hazard>,
    pub resources: BTreeMap<String, Resource>,
    pub predicates: BTreeMap<String, Predicate>,
}

impl RealityWorldModel {
    pub fn new() -> Self {
        Self::default()
    }

    pub fn set_active_manifold(&mut self, manifold: Manifold) {
        let id = manifold.id.clone();
        self.manifolds.insert(id.clone(), manifold);
        self.active_manifold_id = Some(id);
    }

    pub fn add_entity(&mut self, entity: Entity) {
        self.entities.insert(entity.id, entity);
    }

    pub fn add_affordance(&mut self, affordance: Affordance) {
        self.affordances.insert(affordance.id, affordance);
    }

    pub fn add_connector(&mut self, connector: Connector) {
        self.connectors.push(connector);
    }

    pub fn add_hazard(&mut self, hazard: Hazard) {
        self.hazards.insert(hazard.id, hazard);
    }

    pub fn set_resource(&mut self, resource: Resource) {
        self.resources.insert(resource.id.clone(), resource);
    }

    pub fn assert_predicate(
        &mut self,
        key: impl Into<String>,
        value: impl Into<StateValue>,
        confidence: f64,
        now_ms: u64,
    ) {
        let k = key.into();
        self.predicates
            .insert(k.clone(), Predicate::new(k, value, confidence, now_ms));
    }

    /// Queries all accessible affordances from player coordinates.
    pub fn query_accessible_affordances(&self, player_pos: (f64, f64, f64)) -> Vec<&Affordance> {
        let mut results = Vec::new();
        let cur_manifold = self.active_manifold_id.as_deref().unwrap_or("");
        for aff in self.affordances.values() {
            if aff.manifold_id == cur_manifold && aff.is_accessible(player_pos) {
                results.push(aff);
            }
        }
        results
    }

    /// Evaluates if a point in the active manifold is free from lethal hazard threat.
    pub fn is_point_safe(&self, pos: (f64, f64, f64), arrival_time_s: f64) -> bool {
        let cur_manifold = self.active_manifold_id.as_deref().unwrap_or("");
        for hazard in self.hazards.values() {
            if hazard.manifold_id == cur_manifold
                && hazard.evaluate_threat_cost(pos, arrival_time_s) == 254
            {
                return false;
            }
        }
        true
    }

    /// Converts all current fresh predicates into a GOAP `WorldState`.
    pub fn to_goap_world_state(&self, now_ms: u64, ttl_ms: u64) -> WorldState {
        let mut state = WorldState::new();
        for (k, pred) in &self.predicates {
            if pred.is_fresh(now_ms, ttl_ms) && pred.confidence >= 0.75 {
                state.set(k.clone(), pred.value.clone());
            }
        }
        for (r_name, res) in &self.resources {
            state.set(format!("{}_ratio", r_name), (res.ratio() * 100.0) as i64);
            state.set(format!("{}_depleted", r_name), res.is_depleted());
        }
        state
    }
}
