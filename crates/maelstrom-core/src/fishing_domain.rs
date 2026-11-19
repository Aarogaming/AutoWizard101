//! `fishing_domain`: Concrete Acoustic Fishing Reflex Engine.
//!
//! Models the complete Wizard101 fishing lifecycle under the strict invariant
//! `Facts ≠ Authority ≠ Actions`:
//! - Energy budget preconditions and one-attempt execution gate.
//! - Lead-cast kinematics over 3D swimming fish entities.
//! - Sub-millisecond acoustic bite detection reflex via `FishingBobberSplash` audio cue.
//! - Reflex latency gating (≤ 150ms) dispatching `Spacebar` strikes.
//! - Structured catch resolution (fish species, chest loot, XP, size classification).
//!
//! Emits purely advisory `FishingProposal` records for the C# `DesktopInputActuator`.

use alloc::string::String;
use serde::{Deserialize, Serialize};

use crate::audio_cue::{AudioCueEvent, AudioCueType};

/// Magic school classification for fish entities and lures.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize)]
pub enum FishSchool {
    Fire,
    Ice,
    Storm,
    Myth,
    Life,
    Death,
    Balance,
}

/// Lure tier rank.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize)]
pub enum LureRank {
    Rank1,
    Rank2,
    Rank3,
}

/// Functional kind of a fishing spell.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub enum FishingSpellKind {
    Lure { school: FishSchool, rank: LureRank },
    RevealSchool,
    WinnowSchool { school: FishSchool },
    SummonFish,
    BanishSentinels,
}

/// A playable fishing spell card from the fishing spellbook/action bar.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct FishingSpell {
    pub slot_index: usize,
    pub name: String,
    pub kind: FishingSpellKind,
    pub energy_cost: u32,
}

impl FishingSpell {
    pub fn new(
        slot_index: usize,
        name: impl Into<String>,
        kind: FishingSpellKind,
        energy_cost: u32,
    ) -> Self {
        Self {
            slot_index,
            name: name.into(),
            kind,
            energy_cost,
        }
    }

    /// Creates a Minor Lure (Rank 1, 3 energy).
    pub fn minor_lure(slot_index: usize, school: FishSchool) -> Self {
        let name = match school {
            FishSchool::Fire => "Minor Fire Lure",
            FishSchool::Ice => "Minor Ice Lure",
            FishSchool::Storm => "Minor Storm Lure",
            FishSchool::Myth => "Minor Myth Lure",
            FishSchool::Life => "Minor Life Lure",
            FishSchool::Death => "Minor Death Lure",
            FishSchool::Balance => "Minor Balance Lure",
        };
        Self::new(
            slot_index,
            name,
            FishingSpellKind::Lure {
                school,
                rank: LureRank::Rank1,
            },
            3,
        )
    }

    /// Creates a Regular Lure (Rank 2, 3 energy).
    pub fn regular_lure(slot_index: usize, school: FishSchool) -> Self {
        let name = match school {
            FishSchool::Fire => "Fire Lure",
            FishSchool::Ice => "Ice Lure",
            FishSchool::Storm => "Storm Lure",
            FishSchool::Myth => "Myth Lure",
            FishSchool::Life => "Life Lure",
            FishSchool::Death => "Death Lure",
            FishSchool::Balance => "Balance Lure",
        };
        Self::new(
            slot_index,
            name,
            FishingSpellKind::Lure {
                school,
                rank: LureRank::Rank2,
            },
            3,
        )
    }

    /// Creates a Mega Lure (Rank 3, 3 energy).
    pub fn mega_lure(slot_index: usize, school: FishSchool) -> Self {
        let name = match school {
            FishSchool::Fire => "Mega Fire Lure",
            FishSchool::Ice => "Mega Ice Lure",
            FishSchool::Storm => "Mega Storm Lure",
            FishSchool::Myth => "Mega Myth Lure",
            FishSchool::Life => "Mega Life Lure",
            FishSchool::Death => "Mega Death Lure",
            FishSchool::Balance => "Mega Balance Lure",
        };
        Self::new(
            slot_index,
            name,
            FishingSpellKind::Lure {
                school,
                rank: LureRank::Rank3,
            },
            3,
        )
    }

    /// Creates a Winnow School spell (10 energy).
    pub fn winnow(slot_index: usize, school: FishSchool) -> Self {
        let name = match school {
            FishSchool::Fire => "Winnow Fire",
            FishSchool::Ice => "Winnow Ice",
            FishSchool::Storm => "Winnow Storm",
            FishSchool::Myth => "Winnow Myth",
            FishSchool::Life => "Winnow Life",
            FishSchool::Death => "Winnow Death",
            FishSchool::Balance => "Winnow Balance",
        };
        Self::new(
            slot_index,
            name,
            FishingSpellKind::WinnowSchool { school },
            10,
        )
    }

    /// Creates a Reveal Fish School spell (5 energy).
    pub fn reveal_school(slot_index: usize) -> Self {
        Self::new(
            slot_index,
            "Reveal Fish School",
            FishingSpellKind::RevealSchool,
            5,
        )
    }
}

/// 3D Spatial Vector for pond surfaces, fish coordinates, and cast targets.
#[derive(Debug, Clone, Copy, PartialEq, Serialize, Deserialize)]
pub struct Vector3D {
    pub x: f64,
    pub y: f64,
    pub z: f64,
}

impl Vector3D {
    pub const fn new(x: f64, y: f64, z: f64) -> Self {
        Self { x, y, z }
    }

    pub fn zero() -> Self {
        Self::new(0.0, 0.0, 0.0)
    }

    pub fn magnitude_squared(&self) -> f64 {
        self.x * self.x + self.y * self.y + self.z * self.z
    }

    pub fn magnitude(&self) -> f64 {
        let m2 = self.magnitude_squared();
        if m2 <= 0.0 {
            0.0
        } else {
            m2.sqrt()
        }
    }

    pub fn distance(&self, other: &Self) -> f64 {
        let dx = self.x - other.x;
        let dy = self.y - other.y;
        let dz = self.z - other.z;
        let d2 = dx * dx + dy * dy + dz * dz;
        if d2 <= 0.0 {
            0.0
        } else {
            d2.sqrt()
        }
    }

    pub fn distance_2d(&self, other: &Self) -> f64 {
        let dx = self.x - other.x;
        let dy = self.y - other.y;
        let d2 = dx * dx + dy * dy;
        if d2 <= 0.0 {
            0.0
        } else {
            d2.sqrt()
        }
    }

    pub fn normalize(&self) -> Self {
        let m = self.magnitude();
        if m > 1e-6 {
            Self::new(self.x / m, self.y / m, self.z / m)
        } else {
            Self::zero()
        }
    }

    pub fn add(&self, other: &Self) -> Self {
        Self::new(self.x + other.x, self.y + other.y, self.z + other.z)
    }

    pub fn sub(&self, other: &Self) -> Self {
        Self::new(self.x - other.x, self.y - other.y, self.z - other.z)
    }

    pub fn scale(&self, factor: f64) -> Self {
        Self::new(self.x * factor, self.y * factor, self.z * factor)
    }
}

/// Observed swimming fish entity in a pond.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct FishEntity {
    pub id: u64,
    pub position: Vector3D,
    pub velocity: Vector3D,
    pub school: Option<FishSchool>,
    pub rank: u8,
}

impl FishEntity {
    pub fn new(id: u64, position: Vector3D, velocity: Vector3D) -> Self {
        Self {
            id,
            position,
            velocity,
            school: None,
            rank: 1,
        }
    }

    pub fn with_school(mut self, school: FishSchool) -> Self {
        self.school = Some(school);
        self
    }

    pub fn with_rank(mut self, rank: u8) -> Self {
        self.rank = rank;
        self
    }

    /// Predicts position after `delta_t` seconds assuming constant velocity.
    pub fn predicted_position(&self, delta_t: f64) -> Vector3D {
        self.position.add(&self.velocity.scale(delta_t))
    }
}

/// Bounded 3D pond surface geometry.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct PondSurface {
    pub center: Vector3D,
    pub radius: f64,
    pub water_z: f64,
}

impl PondSurface {
    pub fn new(center: Vector3D, radius: f64, water_z: f64) -> Self {
        Self {
            center,
            radius: radius.max(1.0),
            water_z,
        }
    }

    /// Returns true if a 2D coordinate falls within the pond perimeter.
    pub fn contains_point_2d(&self, p: &Vector3D) -> bool {
        self.center.distance_2d(p) <= self.radius
    }

    /// Clamps a target point to the pond perimeter and sets elevation to `water_z`.
    pub fn clamp_to_surface(&self, p: &Vector3D) -> Vector3D {
        let dist = self.center.distance_2d(p);
        if dist <= self.radius {
            Vector3D::new(p.x, p.y, self.water_z)
        } else {
            let dir_x = (p.x - self.center.x) / dist;
            let dir_y = (p.y - self.center.y) / dist;
            // 95% radius margin to ensure bobber stays well inside water
            let margin_radius = self.radius * 0.95;
            Vector3D::new(
                self.center.x + dir_x * margin_radius,
                self.center.y + dir_y * margin_radius,
                self.water_z,
            )
        }
    }
}

/// Kinematic lead-cast calculation for moving fish targets.
pub struct CastPlanner;

impl CastPlanner {
    /// Computes the optimal bobber landing point $\mathbf{p}_{\text{cast}}$:
    /// $\mathbf{p}_{\text{cast}} = \mathbf{p}_{\text{fish}} + \mathbf{v}_{\text{fish}} \cdot \Delta t_{\text{flight}} + \mathbf{u}_{\text{heading}} \cdot d_{\text{lead}}$
    pub fn compute_lead_cast(
        fish: &FishEntity,
        pond: &PondSurface,
        flight_time_s: f64,
        lead_distance: f64,
    ) -> Vector3D {
        let predicted = fish.predicted_position(flight_time_s);
        let speed = fish.velocity.magnitude();

        let target_raw = if speed > 1e-4 {
            let heading = fish.velocity.normalize();
            predicted.add(&heading.scale(lead_distance))
        } else {
            // Static fish: cast slightly offset toward pond center or direct position
            predicted
        };

        pond.clamp_to_surface(&target_raw)
    }
}

/// Detailed metadata of a successfully hooked fish.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct CaughtFish {
    pub name: String,
    pub school: FishSchool,
    pub rank: u8,
    pub size_inches: f64,
    pub xp_gained: u32,
    pub is_whopper: bool,
    pub is_small_fry: bool,
}

impl CaughtFish {
    pub fn new(
        name: impl Into<String>,
        school: FishSchool,
        rank: u8,
        size_inches: f64,
        xp_gained: u32,
    ) -> Self {
        Self {
            name: name.into(),
            school,
            rank,
            size_inches,
            xp_gained,
            is_whopper: size_inches >= 35.0,
            is_small_fry: size_inches <= 12.0,
        }
    }
}

/// Outcome of a fishing attempt.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub enum FishingOutcome {
    FishCaught(CaughtFish),
    ChestCaught {
        chest_id: String,
        loot_summary: String,
    },
    FishGotAway {
        reason: String,
    },
    StruckTooEarly,
    CastTimedOut,
}

/// Session telemetry statistics for fishing automation.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct FishingSessionStats {
    pub casts_attempted: u32,
    pub fish_caught: u32,
    pub chests_caught: u32,
    pub escapes: u32,
    pub timeouts: u32,
    pub total_energy_spent: u32,
    pub total_xp_gained: u32,
    pub total_reflex_latency_ms: f64,
    pub reflex_strikes_count: u32,
}

impl FishingSessionStats {
    pub fn new() -> Self {
        Self {
            casts_attempted: 0,
            fish_caught: 0,
            chests_caught: 0,
            escapes: 0,
            timeouts: 0,
            total_energy_spent: 0,
            total_xp_gained: 0,
            total_reflex_latency_ms: 0.0,
            reflex_strikes_count: 0,
        }
    }

    pub fn record_cast(&mut self, energy_cost: u32) {
        self.casts_attempted += 1;
        self.total_energy_spent += energy_cost;
    }

    pub fn record_strike(&mut self, latency_ms: f64) {
        self.reflex_strikes_count += 1;
        self.total_reflex_latency_ms += latency_ms;
    }

    pub fn record_outcome(&mut self, outcome: &FishingOutcome) {
        match outcome {
            FishingOutcome::FishCaught(fish) => {
                self.fish_caught += 1;
                self.total_xp_gained += fish.xp_gained;
            }
            FishingOutcome::ChestCaught { .. } => {
                self.chests_caught += 1;
            }
            FishingOutcome::FishGotAway { .. } | FishingOutcome::StruckTooEarly => {
                self.escapes += 1;
            }
            FishingOutcome::CastTimedOut => {
                self.timeouts += 1;
            }
        }
    }

    pub fn average_reflex_latency_ms(&self) -> f64 {
        if self.reflex_strikes_count == 0 {
            0.0
        } else {
            self.total_reflex_latency_ms / (self.reflex_strikes_count as f64)
        }
    }

    pub fn catch_success_rate(&self) -> f64 {
        if self.casts_attempted == 0 {
            0.0
        } else {
            ((self.fish_caught + self.chests_caught) as f64) / (self.casts_attempted as f64)
        }
    }
}

impl Default for FishingSessionStats {
    fn default() -> Self {
        Self::new()
    }
}

/// Dynamic state in the acoustic fishing state machine.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub enum FishingState {
    Idle,
    SelectingLure {
        spell: FishingSpell,
    },
    Casting {
        spell: FishingSpell,
        target_world_pos: Vector3D,
        cast_timestamp_s: f64,
    },
    WaitingForBite {
        spell: FishingSpell,
        target_world_pos: Vector3D,
        cast_timestamp_s: f64,
        timeout_s: f64,
        nibbles: u32,
    },
    Striking {
        strike_timestamp_s: f64,
        reflex_latency_ms: f64,
        was_within_latency_gate: bool,
    },
    ReelingIn {
        reel_start_s: f64,
    },
    CatchResolved {
        outcome: FishingOutcome,
    },
    EnergyExhausted {
        current_energy: u32,
        required_energy: u32,
    },
}

/// Advisory actuation proposals emitted by the fishing engine.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub enum FishingProposal {
    SelectLureSpell {
        slot_index: usize,
        spell_name: String,
    },
    CastLure {
        target_world_pos: Vector3D,
    },
    ReflexStrike {
        key: String,
        latency_ms: f64,
        is_sub_150ms: bool,
    },
    DismissRewardPopup,
    HaltDueToExhaustion {
        current_energy: u32,
    },
}

/// Concrete Acoustic Fishing Reflex State Machine.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct FishingStateMachine {
    pub state: FishingState,
    pub current_energy: u32,
    pub max_energy: u32,
    pub pond: PondSurface,
    pub stats: FishingSessionStats,
    pub max_reflex_latency_ms: f64,
    pub cast_timeout_s: f64,
    pub ballistic_flight_time_s: f64,
    pub lead_distance: f64,
}

impl FishingStateMachine {
    pub fn new(current_energy: u32, max_energy: u32, pond: PondSurface) -> Self {
        Self {
            state: FishingState::Idle,
            current_energy,
            max_energy,
            pond,
            stats: FishingSessionStats::new(),
            max_reflex_latency_ms: 150.0,
            cast_timeout_s: 30.0,
            ballistic_flight_time_s: 0.65,
            lead_distance: 25.0,
        }
    }

    pub fn with_max_reflex_latency_ms(mut self, max_ms: f64) -> Self {
        self.max_reflex_latency_ms = max_ms.max(10.0);
        self
    }

    pub fn with_cast_timeout_s(mut self, timeout_s: f64) -> Self {
        self.cast_timeout_s = timeout_s.max(5.0);
        self
    }

    /// Checks if player has sufficient energy for the given spell.
    pub fn can_afford(&self, spell: &FishingSpell) -> bool {
        self.current_energy >= spell.energy_cost
    }

    /// Initiates a fishing attempt with the specified lure spell.
    pub fn start_fishing(&mut self, spell: FishingSpell) -> Result<FishingProposal, String> {
        if !matches!(self.state, FishingState::Idle) {
            return Err(String::from(
                "Cannot start fishing: machine is not in Idle state",
            ));
        }

        if !self.can_afford(&spell) {
            self.state = FishingState::EnergyExhausted {
                current_energy: self.current_energy,
                required_energy: spell.energy_cost,
            };
            return Ok(FishingProposal::HaltDueToExhaustion {
                current_energy: self.current_energy,
            });
        }

        let proposal = FishingProposal::SelectLureSpell {
            slot_index: spell.slot_index,
            spell_name: spell.name.clone(),
        };

        self.state = FishingState::SelectingLure { spell };
        Ok(proposal)
    }

    /// Dispatches the cast after selecting lure, computing lead target if fish entity is provided.
    pub fn dispatch_cast(
        &mut self,
        target_fish: Option<&FishEntity>,
        now_s: f64,
    ) -> Result<FishingProposal, String> {
        let spell = match &self.state {
            FishingState::SelectingLure { spell } => spell.clone(),
            _ => return Err(String::from("Cannot cast: must be in SelectingLure state")),
        };

        let target_pos = if let Some(fish) = target_fish {
            CastPlanner::compute_lead_cast(
                fish,
                &self.pond,
                self.ballistic_flight_time_s,
                self.lead_distance,
            )
        } else {
            // Default: cast at pond center water elevation
            Vector3D::new(self.pond.center.x, self.pond.center.y, self.pond.water_z)
        };

        // Deduct energy and record cast
        self.current_energy = self.current_energy.saturating_sub(spell.energy_cost);
        self.stats.record_cast(spell.energy_cost);

        self.state = FishingState::Casting {
            spell,
            target_world_pos: target_pos,
            cast_timestamp_s: now_s,
        };

        Ok(FishingProposal::CastLure {
            target_world_pos: target_pos,
        })
    }

    /// Confirms the bobber has splashed down in the water and is awaiting bites.
    pub fn confirm_bobber_landed(&mut self, now_s: f64) -> Result<(), String> {
        match &self.state {
            FishingState::Casting {
                spell,
                target_world_pos,
                ..
            } => {
                self.state = FishingState::WaitingForBite {
                    spell: spell.clone(),
                    target_world_pos: *target_world_pos,
                    cast_timestamp_s: now_s,
                    timeout_s: self.cast_timeout_s,
                    nibbles: 0,
                };
                Ok(())
            }
            _ => Err(String::from(
                "Cannot confirm bobber landing: not in Casting state",
            )),
        }
    }

    /// Handles discrete acoustic cues emitted by the audio subsystem.
    pub fn handle_audio_cue(&mut self, cue: &AudioCueEvent, now_s: f64) -> Option<FishingProposal> {
        if cue.cue_type != AudioCueType::FishingBobberSplash {
            return None;
        }

        if let FishingState::WaitingForBite { .. } = &self.state {
            let latency_ms = ((now_s - cue.timestamp_s) * 1000.0).max(0.0);
            let within_gate = latency_ms <= self.max_reflex_latency_ms;

            self.stats.record_strike(latency_ms);

            self.state = FishingState::Striking {
                strike_timestamp_s: now_s,
                reflex_latency_ms: latency_ms,
                was_within_latency_gate: within_gate,
            };

            Some(FishingProposal::ReflexStrike {
                key: String::from("Spacebar"),
                latency_ms,
                is_sub_150ms: within_gate,
            })
        } else {
            None
        }
    }

    /// Records a visual non-committal nibble (ripple without splash).
    pub fn record_nibble(&mut self) -> Result<u32, String> {
        match &mut self.state {
            FishingState::WaitingForBite { nibbles, .. } => {
                *nibbles += 1;
                Ok(*nibbles)
            }
            _ => Err(String::from(
                "Cannot record nibble: not in WaitingForBite state",
            )),
        }
    }

    /// Updates watchdog timers and checks for cast timeouts.
    pub fn update_timer(&mut self, now_s: f64) -> Option<FishingOutcome> {
        if let FishingState::WaitingForBite {
            cast_timestamp_s,
            timeout_s,
            ..
        } = &self.state
        {
            if (now_s - *cast_timestamp_s) >= *timeout_s {
                let outcome = FishingOutcome::CastTimedOut;
                self.stats.record_outcome(&outcome);
                self.state = FishingState::CatchResolved {
                    outcome: outcome.clone(),
                };
                return Some(outcome);
            }
        }
        None
    }

    /// Confirms physical strike was dispatched, transitioning to reeling state.
    pub fn confirm_strike_dispatched(&mut self, now_s: f64) -> Result<(), String> {
        match &self.state {
            FishingState::Striking { .. } => {
                self.state = FishingState::ReelingIn {
                    reel_start_s: now_s,
                };
                Ok(())
            }
            _ => Err(String::from("Cannot confirm strike: not in Striking state")),
        }
    }

    /// Resolves the catch outcome, updating telemetry and emitting reward popup dismissal proposal.
    pub fn resolve_catch(&mut self, outcome: FishingOutcome) -> FishingProposal {
        self.stats.record_outcome(&outcome);
        self.state = FishingState::CatchResolved { outcome };
        FishingProposal::DismissRewardPopup
    }

    /// Resets the machine back to Idle state ready for the next cast.
    pub fn reset_to_idle(&mut self) {
        self.state = FishingState::Idle;
    }
}
