//! `ontology`: The Irreducible Grammar of Reality for Maelstrom.
//!
//! Codifies the 7 fundamental reality primitives:
//! 1. `Manifold` (Space): Bounded continuous or topological coordinate frames.
//! 2. `Entity` (Occupant): Identifiable occupants carrying spatial volumes, velocities, and epistemic states.
//! 3. `Affordance` (Interaction Surface): Proximity/focus-gated valid state transitions and client shortcuts.
//! 4. `Connector` (Topology Jump): Directed non-Euclidean links, fast-travel shortcuts, and recall markers.
//! 5. `Hazard` / `Field` (Cost & Threat): Scalar/vector potential fields penalizing presence.
//! 6. `Resource` / `Budget` (Capacity): Quantified internal reservoirs governing feasibility.
//! 7. `Predicate` (Observable Truth): Grounded assertions with confidence and freshness metrics.
//!
//! Includes systems-level hardening:
//! - Epistemic decay & anti-hallucination TTLs.
//! - Bayesian negative evidence recording for depleted resource anchors.
//! - Multi-client hivemind spatial sharing and 3-phase sigil barrier lockstep.
//! - Phased zone instance keying (`zone_template_id`, `phase_tag`).
//! - Tiered graceful degradation (Tier 0 Full -> Tier 1 Visual/Acoustic -> Tier 2 Safe Halt).
//! - Formal provenance tagging (`ProvenanceTag`).
//!
//! Adheres strictly to `Facts ≠ Authority ≠ Actions`.

use alloc::collections::BTreeMap;
use alloc::format;
use alloc::string::String;
use alloc::vec::Vec;
use serde::{Deserialize, Serialize};

use crate::goap::{StateValue, WorldState};
use crate::spatial_index::{EntityKind, SpatialEntity3D, AABB3D};

/// Provenance origin classifying where a spatial or semantic record was derived.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize)]
pub enum ProvenanceOrigin {
    StaticWadExtract,
    EmpiricalLiveObservation,
    ManualSeed,
    SyntheticTestFixture,
}

/// Formal audit provenance tag tracking data origin, verification count, and freshness.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct ProvenanceTag {
    pub origin: ProvenanceOrigin,
    pub build_hash: String,
    pub verification_count: u32,
    pub first_observed_epoch_ms: u64,
    pub last_verified_epoch_ms: u64,
}

impl ProvenanceTag {
    pub fn new(origin: ProvenanceOrigin, build_hash: impl Into<String>, now_ms: u64) -> Self {
        Self {
            origin,
            build_hash: build_hash.into(),
            verification_count: 1,
            first_observed_epoch_ms: now_ms,
            last_verified_epoch_ms: now_ms,
        }
    }

    pub fn record_verification(&mut self, now_ms: u64) {
        self.verification_count += 1;
        self.last_verified_epoch_ms = now_ms;
    }
}

/// Decoupled zone template and phase key for instanced / phased manifolds.
#[derive(Debug, Clone, PartialEq, Eq, PartialOrd, Ord, Hash, Serialize, Deserialize)]
pub struct ZoneInstanceKey {
    pub zone_template_id: String,
    pub phase_tag: String,
}

impl ZoneInstanceKey {
    pub fn new(template_id: impl Into<String>, phase_tag: impl Into<String>) -> Self {
        Self {
            zone_template_id: template_id.into(),
            phase_tag: phase_tag.into(),
        }
    }
}

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
    pub zone_key: Option<ZoneInstanceKey>,
    pub provenance: Option<ProvenanceTag>,
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
            zone_key: None,
            provenance: None,
        }
    }

    pub fn with_velocity_cap(mut self, max_vel: f64) -> Self {
        self.max_velocity = max_vel.max(0.1);
        self
    }

    pub fn with_zone_key(
        mut self,
        template_id: impl Into<String>,
        phase_tag: impl Into<String>,
    ) -> Self {
        self.zone_key = Some(ZoneInstanceKey::new(template_id, phase_tag));
        self
    }

    pub fn with_provenance(mut self, prov: ProvenanceTag) -> Self {
        self.provenance = Some(prov);
        self
    }

    pub fn increment_epoch(&mut self) {
        self.epoch = self.epoch.wrapping_add(1);
    }
}

/// Epistemic belief state of an Entity preventing hallucinated actions on stale/depleted objects.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub enum EntityEpistemicState {
    Active,
    Depleted { cooldown_until_ms: u64 },
    Stale,
    Unknown,
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
    pub epistemic_state: EntityEpistemicState,
    pub last_observed_ms: u64,
    pub ttl_ms: u64,
    pub negative_evidence_count: u32,
    pub provenance: Option<ProvenanceTag>,
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
            epistemic_state: EntityEpistemicState::Active,
            last_observed_ms: 0,
            ttl_ms: 60_000,
            negative_evidence_count: 0,
            provenance: None,
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

    pub fn with_ttl_ms(mut self, ttl_ms: u64) -> Self {
        self.ttl_ms = ttl_ms.max(50);
        self
    }

    pub fn with_provenance(mut self, prov: ProvenanceTag) -> Self {
        self.provenance = Some(prov);
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

    /// Records fresh positive observation, resetting negative evidence and setting state to Active.
    pub fn record_observation(&mut self, now_ms: u64) {
        self.last_observed_ms = now_ms;
        self.negative_evidence_count = 0;
        self.epistemic_state = EntityEpistemicState::Active;
        if let Some(prov) = &mut self.provenance {
            prov.record_verification(now_ms);
        }
    }

    /// Records that the agent checked this entity's coordinate and confirmed nothing was present.
    pub fn record_negative_evidence(&mut self, now_ms: u64) {
        self.negative_evidence_count += 1;
        self.last_observed_ms = now_ms;
        if self.negative_evidence_count >= 2 {
            self.epistemic_state = EntityEpistemicState::Unknown;
        }
    }

    /// Marks a harvestable entity as depleted with an explicit cooldown timer.
    pub fn mark_depleted(&mut self, cooldown_until_ms: u64) {
        self.epistemic_state = EntityEpistemicState::Depleted { cooldown_until_ms };
    }

    /// Evaluates temporal staleness based on elapsed time and TTL.
    pub fn update_decay(&mut self, now_ms: u64) {
        match self.epistemic_state {
            EntityEpistemicState::Active => {
                if now_ms >= self.last_observed_ms && (now_ms - self.last_observed_ms) > self.ttl_ms
                {
                    // Dynamic roaming mobs decay to Unknown; static objects decay to Stale
                    if self.kind == EntityKind::DynamicMob {
                        self.epistemic_state = EntityEpistemicState::Unknown;
                    } else {
                        self.epistemic_state = EntityEpistemicState::Stale;
                    }
                }
            }
            EntityEpistemicState::Depleted { cooldown_until_ms } => {
                if now_ms >= cooldown_until_ms {
                    // Cooldown has elapsed; node is potentially respawned but unconfirmed
                    self.epistemic_state = EntityEpistemicState::Unknown;
                }
            }
            EntityEpistemicState::Stale | EntityEpistemicState::Unknown => {}
        }
    }

    /// Returns true if this entity is actively trusted for path planning and targeting.
    pub fn is_valid_for_planning(&self) -> bool {
        matches!(self.epistemic_state, EntityEpistemicState::Active)
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
    HotbarShortcut(String),
    SetMarker,
    RecallMarker,
    EmergencyPotion,
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

/// Topology jump classification including native client fast-travel shortcuts.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub enum ConnectorKind {
    DoorPortal,
    SpiralWorldGate,
    DungeonSigil,
    RealmHop,
    ViewportSwitch,
    FastTravelHome,
    FastTravelCommons,
    MarkerRecall { marker_id: String },
    DungeonSigilCountdown { duration_seconds: u32 },
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
            traversal_cost: cost.max(0.01),
            bidirectional: true,
        }
    }

    /// Fast-Travel to Dorm/Castle shortcut connector (`Home` key).
    pub fn fast_travel_home(
        current_manifold: impl Into<String>,
        home_manifold: impl Into<String>,
        home_anchor: (f64, f64, f64),
    ) -> Self {
        Self {
            id: String::from("FastTravel:Home"),
            from_manifold: current_manifold.into(),
            to_manifold: home_manifold.into(),
            source_anchor: (0.0, 0.0, 0.0),
            target_anchor: home_anchor,
            kind: ConnectorKind::FastTravelHome,
            traversal_cost: 0.1,
            bidirectional: false,
        }
    }

    /// Fast-Travel to Commons Hub shortcut connector (`End` key).
    pub fn fast_travel_commons(
        current_manifold: impl Into<String>,
        commons_anchor: (f64, f64, f64),
    ) -> Self {
        Self {
            id: String::from("FastTravel:Commons"),
            from_manifold: current_manifold.into(),
            to_manifold: String::from("Commons"),
            source_anchor: (0.0, 0.0, 0.0),
            target_anchor: commons_anchor,
            kind: ConnectorKind::FastTravelCommons,
            traversal_cost: 0.1,
            bidirectional: false,
        }
    }

    /// Marker Recall shortcut connector (`Page Up` key).
    pub fn marker_recall(
        current_manifold: impl Into<String>,
        target_manifold: impl Into<String>,
        target_anchor: (f64, f64, f64),
    ) -> Self {
        Self {
            id: String::from("FastTravel:MarkerRecall"),
            from_manifold: current_manifold.into(),
            to_manifold: target_manifold.into(),
            source_anchor: (0.0, 0.0, 0.0),
            target_anchor,
            kind: ConnectorKind::MarkerRecall {
                marker_id: String::from("PRIMARY_MARKER"),
            },
            traversal_cost: 0.1,
            bidirectional: false,
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

/// Status of an individual client in a multi-account sigil synchronization group.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub enum SigilClientStatus {
    Preparing {
        health_ratio: f64,
        mana_ratio: f64,
        deck_verified: bool,
    },
    ReadyForSigil,
    SteppedOnSigil {
        countdown_remaining_s: f64,
    },
    InCombat,
}

/// Multi-client sigil lockstep synchronization barrier.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct MultiClientSigilBarrier {
    pub sigil_id: String,
    pub required_clients: Vec<String>,
    pub client_states: BTreeMap<String, SigilClientStatus>,
}

impl MultiClientSigilBarrier {
    pub fn new(sigil_id: impl Into<String>, clients: Vec<String>) -> Self {
        let mut client_states = BTreeMap::new();
        for c in &clients {
            client_states.insert(
                c.clone(),
                SigilClientStatus::Preparing {
                    health_ratio: 0.0,
                    mana_ratio: 0.0,
                    deck_verified: false,
                },
            );
        }
        Self {
            sigil_id: sigil_id.into(),
            required_clients: clients,
            client_states,
        }
    }

    pub fn update_client_status(&mut self, client_id: &str, status: SigilClientStatus) {
        self.client_states.insert(String::from(client_id), status);
    }

    /// Barrier passes only when all required clients are ReadyForSigil, SteppedOnSigil, or InCombat.
    pub fn can_step_onto_sigil(&self) -> bool {
        if self.required_clients.is_empty() {
            return false;
        }
        self.required_clients.iter().all(|c| {
            matches!(
                self.client_states.get(c),
                Some(SigilClientStatus::ReadyForSigil)
                    | Some(SigilClientStatus::SteppedOnSigil { .. })
                    | Some(SigilClientStatus::InCombat)
            )
        })
    }
}

/// Graceful degradation operating tiers when external reality assumptions drift or fail.
#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Default, Serialize, Deserialize)]
pub enum DegradationTier {
    /// Tier 0: Primary full ontology (Process memory pose + 3D BCD mesh + visual validation).
    #[default]
    Tier0FullOntology,
    /// Tier 1: Memory offset shift / hook failure (Compass bearing vector + OCR HUD + audio cues).
    Tier1VisualAcoustic,
    /// Tier 2: Double residual drift threshold fault (Release GameInputLease, dump replay trace, safe park).
    Tier2SafeHalt,
}

/// Unified Reality World Model aggregating the 7 primitives with systems-level hardening.
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
    pub degradation_tier: DegradationTier,
    pub consecutive_drift_faults: u32,
    pub sigil_barriers: BTreeMap<String, MultiClientSigilBarrier>,
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

    /// Evaluates drift between expected pose and observed pose against tolerance.
    /// Manages the tiered degradation ladder (Tier 0 -> Tier 1 -> Tier 2).
    pub fn record_drift_observation(
        &mut self,
        drift_magnitude: f64,
        tolerance: f64,
    ) -> DegradationTier {
        if drift_magnitude > tolerance {
            self.consecutive_drift_faults += 1;
            if self.consecutive_drift_faults >= 2 {
                self.degradation_tier = DegradationTier::Tier2SafeHalt;
            } else {
                self.degradation_tier = DegradationTier::Tier1VisualAcoustic;
            }
        } else {
            self.consecutive_drift_faults = 0;
            if self.degradation_tier == DegradationTier::Tier1VisualAcoustic {
                self.degradation_tier = DegradationTier::Tier0FullOntology;
            }
        }
        self.degradation_tier
    }

    /// Updates epistemic decay for all registered entities.
    pub fn decay_stale_entities(&mut self, now_ms: u64) {
        for entity in self.entities.values_mut() {
            entity.update_decay(now_ms);
        }
    }

    /// Records negative evidence when inspecting an expected entity location.
    pub fn record_entity_negative_evidence(&mut self, entity_id: u64, now_ms: u64) {
        if let Some(entity) = self.entities.get_mut(&entity_id) {
            entity.record_negative_evidence(now_ms);
        }
    }

    /// Registers a multi-client sigil barrier.
    pub fn register_sigil_barrier(&mut self, barrier: MultiClientSigilBarrier) {
        self.sigil_barriers
            .insert(barrier.sigil_id.clone(), barrier);
    }

    /// Updates client status in a registered sigil barrier.
    pub fn update_sigil_client(
        &mut self,
        sigil_id: &str,
        client_id: &str,
        status: SigilClientStatus,
    ) {
        if let Some(barrier) = self.sigil_barriers.get_mut(sigil_id) {
            barrier.update_client_status(client_id, status);
        }
    }

    /// Checks if a sigil barrier is cleared for all clients to step onto the ring.
    pub fn can_step_onto_sigil(&self, sigil_id: &str) -> bool {
        self.sigil_barriers
            .get(sigil_id)
            .map(|b| b.can_step_onto_sigil())
            .unwrap_or(false)
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
