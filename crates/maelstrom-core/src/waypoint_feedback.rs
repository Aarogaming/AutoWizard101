//! Staged pure port of native `WaypointFeedback.cs`; no OS, input, or clock access.
//!
//! This module is intentionally not exported by the crate while shared runtime
//! integration is under review. Integration tests import this exact source.
//! A move is a bounded proposal, never proof of dispatch or arrival. The caller
//! must stop on `stopped`; like the native reference, this object does not latch
//! terminal decisions and cannot enforce target identity or input ownership.

#[derive(Debug, Clone, Copy, PartialEq)]
pub struct WaypointPose {
    pub x: f64,
    pub y: f64,
    pub z: f64,
    pub heading: f64,
    pub timestamp: i64,
}

#[derive(Debug, Clone, PartialEq)]
pub struct WaypointDecision {
    pub state: &'static str,
    pub reason: &'static str,
    pub pulse_ms: i32,
    pub distance: f64,
    pub heading_error: f64,
}

pub struct WaypointFeedback {
    destination: WaypointPose,
    maximum_pulse_ms: i32,
    previous: Option<WaypointPose>,
    last_pulse: i32,
    stalls: i32,
    arrivals: i32,
    moves: i32,
    speed: f64,
}

impl WaypointFeedback {
    pub fn new(destination: WaypointPose, maximum_pulse_ms: i32) -> Self {
        Self {
            destination,
            maximum_pulse_ms,
            previous: None,
            last_pulse: 0,
            stalls: 0,
            arrivals: 0,
            moves: 0,
            speed: 0.6,
        }
    }

    /// Consume a pose from the caller's observation clock and propose at most
    /// three forward pulses. Turning requires a separate calibrated model.
    pub fn observe(&mut self, pose: WaypointPose) -> WaypointDecision {
        let dx = self.destination.x - pose.x;
        let dy = self.destination.y - pose.y;
        let distance = (dx * dx + dy * dy).sqrt();
        let angle = (-dx).atan2(-dy) - pose.heading;
        // Reduce first to avoid subtracting two enormous rounded products.
        // Math.IEEERemainder chooses the nearest quotient, with ties to even.
        let remainder = angle % std::f64::consts::TAU;
        let quotient = angle / std::f64::consts::TAU;
        let error = if remainder.abs() > std::f64::consts::PI
            || (remainder.abs() == std::f64::consts::PI
                && quotient.round_ties_even().abs() > quotient.abs())
        {
            remainder - std::f64::consts::TAU.copysign(angle)
        } else {
            remainder
        };
        let decision = |state, reason, pulse_ms| WaypointDecision {
            state,
            reason,
            pulse_ms,
            distance,
            heading_error: error,
        };
        let stop = |reason| decision("stopped", reason, 0);
        if !(20..=100).contains(&self.maximum_pulse_ms)
            || ![
                pose.x,
                pose.y,
                pose.z,
                pose.heading,
                self.destination.x,
                self.destination.y,
                self.destination.z,
                distance,
                error,
            ]
            .iter()
            .all(|value| value.is_finite())
        {
            return stop("invalid-pose-or-budget");
        }
        if (pose.z - self.destination.z).abs() > 10.0 {
            return stop("level-changed");
        }
        if let Some(prior) = self.previous {
            if pose.timestamp <= prior.timestamp {
                return stop("pose-not-newer");
            }
            let moved = ((pose.x - prior.x).powi(2) + (pose.y - prior.y).powi(2)).sqrt();
            if moved > 120.0 {
                return stop("unexpected-displacement");
            }
            if self.last_pulse > 0 {
                let old_distance = ((self.destination.x - prior.x).powi(2)
                    + (self.destination.y - prior.y).powi(2))
                .sqrt();
                if distance > old_distance + 10.0 {
                    return stop("moving-away-from-destination");
                }
                self.stalls = if moved < 2.0 { self.stalls + 1 } else { 0 };
                if self.stalls >= 2 {
                    return stop("movement-stalled");
                }
                if moved >= 2.0 {
                    self.speed = (moved / f64::from(self.last_pulse)).clamp(0.2, 1.2);
                }
            }
        }
        self.previous = Some(pose);
        self.last_pulse = 0;
        if distance <= 10.0 {
            self.arrivals += 1;
            return decision(
                if self.arrivals >= 2 {
                    "arrived"
                } else {
                    "confirm"
                },
                "coordinate-arrival",
                0,
            );
        }
        self.arrivals = 0;
        if error.abs() > 0.15 {
            return stop("heading-correction-needs-calibration");
        }
        if self.moves >= 3 {
            return stop("movement-budget-exhausted");
        }
        let pulse = (distance / self.speed * 0.8).floor();
        self.last_pulse = (pulse as i32).clamp(20, self.maximum_pulse_ms);
        self.moves += 1;
        decision("move", "bounded-forward-correction", self.last_pulse)
    }
}
