//! Pet Dance Sequence and State Observer ported from ProjectMaelstrom's PetDanceSequence.cs.
//! Tracks pet dance phases and arrow cues using timing constraints rather than blind sleeps.

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum DancePhase {
    Unknown,
    Ready,
    Watch,
    Go,
    Success,
    Failure,
    Resolving,
    Loading,
}

impl DancePhase {
    pub fn parse(label: Option<&str>) -> Self {
        match label {
            Some("dance-ready") => Self::Ready,
            Some("dance-watch") => Self::Watch,
            Some("dance-go") => Self::Go,
            Some("dance-success") => Self::Success,
            Some("dance-failure") => Self::Failure,
            Some("dance-resolving") => Self::Resolving,
            Some("game-loading") => Self::Loading,
            _ => Self::Unknown,
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum DanceCue {
    Unknown,
    Neutral,
    Up,
    Down,
    Left,
    Right,
    Fading,
}

impl DanceCue {
    pub fn as_str(&self) -> &'static str {
        match self {
            Self::Unknown => "unknown",
            Self::Neutral => "neutral",
            Self::Up => "up",
            Self::Down => "down",
            Self::Left => "left",
            Self::Right => "right",
            Self::Fading => "fading",
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct DanceDecision {
    pub cues: Vec<String>,
    pub proposals: Vec<String>,
    pub reason: String,
    pub rejected: bool,
}

pub struct PetDanceSequence {
    pub stable_ms: f64,
    pub maximum_gap_ms: f64,
    cues: Vec<String>,
    expected: usize,
    last_time: f64,
    since: f64,
    candidate: DanceCue,
    phase: DancePhase,
    active: bool,
    armed: bool,
    emitted: bool,
    boundary_seen: bool,
    outcome_applied: bool,
    fade_since: f64,
    low_since: f64,
    last_opacity: f64,
    fault: Option<String>,
    size: Option<(u32, u32)>,
}

impl PetDanceSequence {
    pub fn new(expected: usize) -> Result<Self, String> {
        if !(3..=7).contains(&expected) {
            return Err("Expected sequence length must be between 3 and 7".to_string());
        }

        Ok(Self {
            stable_ms: 70.0,
            maximum_gap_ms: 150.0,
            cues: Vec::new(),
            expected,
            last_time: -1.0,
            since: -1.0,
            candidate: DanceCue::Unknown,
            phase: DancePhase::Unknown,
            active: false,
            armed: false,
            emitted: false,
            boundary_seen: false,
            outcome_applied: false,
            fade_since: -1.0,
            low_since: -1.0,
            last_opacity: 1.0,
            fault: None,
            size: None,
        })
    }

    fn reject(&self, reason: impl Into<String>) -> DanceDecision {
        DanceDecision {
            cues: self.cues.clone(),
            proposals: Vec::new(),
            reason: reason.into(),
            rejected: true,
        }
    }

    fn decision(&self, reason: impl Into<String>) -> DanceDecision {
        DanceDecision {
            cues: self.cues.clone(),
            proposals: if self.phase == DancePhase::Go && self.armed && self.cues.len() == self.expected {
                self.cues.clone()
            } else {
                Vec::new()
            },
            reason: reason.into(),
            rejected: false,
        }
    }

    pub fn observe(
        &mut self,
        time_ms: f64,
        size: (u32, u32),
        phase: DancePhase,
        cue: DanceCue,
        fade_of: DanceCue,
        opacity: f64,
    ) -> DanceDecision {
        let time_valid = time_ms.is_finite() && time_ms >= 0.0 && (self.last_time < 0.0 || time_ms > self.last_time);
        if !time_valid {
            return self.reject("Missing or non-increasing timestamp; round discarded.");
        }

        let gap = self.last_time >= 0.0 && (time_ms - self.last_time > self.maximum_gap_ms);
        self.last_time = time_ms;

        if size.0 < 1 || size.1 < 1 {
            return self.reject("Invalid frame dimensions.");
        }
        if let Some(prev_size) = self.size {
            if prev_size != size {
                return self.reject("Window geometry changed; recalibrate and restart the teacher.");
            }
        }
        self.size = Some(size);

        if phase == DancePhase::Loading {
            self.active = false;
            self.armed = false;
            self.emitted = false;
            self.boundary_seen = false;
            self.outcome_applied = false;
            self.candidate = DanceCue::Unknown;
            self.since = -1.0;
            self.fade_since = -1.0;
            self.low_since = -1.0;
            self.last_opacity = 1.0;
            self.fault = None;
            self.cues.clear();
            self.phase = phase;
            return self.decision("Loading: gameplay paused; waiting for a fresh Watch sequence.");
        }

        if matches!(phase, DancePhase::Ready | DancePhase::Success | DancePhase::Failure) {
            if !self.outcome_applied {
                if phase == DancePhase::Success && self.emitted {
                    self.expected = (self.expected + 1).min(7);
                }
                self.active = false;
                self.emitted = false;
                self.fault = None;
                self.cues.clear();
                self.candidate = DanceCue::Unknown;
                self.since = -1.0;
                self.armed = false;
                self.boundary_seen = true;
                self.outcome_applied = true;
                self.fade_since = -1.0;
                self.low_since = -1.0;
                self.last_opacity = 1.0;
            }
            self.phase = phase;
            return self.decision("Waiting for a fresh Watch sequence.");
        }

        if phase == DancePhase::Resolving {
            self.phase = phase;
            return self.decision("Inputs finished; waiting for independently recognized success or failure.");
        }

        if phase == DancePhase::Unknown {
            self.phase = phase;
            return if self.active {
                self.reject("Unrecognized phase during the round.")
            } else {
                self.decision("Waiting for a recognized Watch phase.")
            };
        }

        if phase == DancePhase::Watch {
            if self.emitted || matches!(self.phase, DancePhase::Go | DancePhase::Resolving) {
                return self.reject("A result or Ready boundary is required before the next round.");
            }
            if !self.active {
                self.active = true;
                self.armed = self.boundary_seen;
                self.candidate = DanceCue::Unknown;
                self.since = -1.0;
                self.boundary_seen = false;
                self.outcome_applied = false;
            } else if gap && self.fault.is_none() {
                self.fault = Some("Observation gap exceeds 150 ms; a cue may have been missed.".to_string());
            }

            self.phase = phase;
            if let Some(fault) = &self.fault {
                return self.decision(fault.clone());
            }

            if cue == DanceCue::Unknown {
                return self.reject("Arrow area is unclear; this round needs review.");
            }

            if cue == DanceCue::Fading {
                if self.cues.is_empty()
                    || !matches!(fade_of, DanceCue::Up | DanceCue::Down | DanceCue::Left | DanceCue::Right)
                    || fade_of.as_str() != self.cues.last().unwrap()
                    || !opacity.is_finite()
                    || !(0.0..0.7).contains(&opacity)
                {
                    return self.reject("Fade is not tied to the last confirmed arrow.");
                }

                if self.fade_since < 0.0 {
                    self.fade_since = time_ms;
                    self.last_opacity = opacity;
                }

                if time_ms - self.fade_since > 350.0 || opacity > self.last_opacity + 0.12 {
                    return self.reject("Fade lasted too long or brightened without a new clear cue.");
                }

                self.last_opacity = self.last_opacity.min(opacity);
                self.candidate = DanceCue::Fading;
                self.since = time_ms;

                if opacity <= 0.25 {
                    if self.low_since < 0.0 {
                        self.low_since = time_ms;
                    }
                } else {
                    self.low_since = -1.0;
                }

                return self.decision("Observing fade transition.");
            }

            // Arrow cue (Up, Down, Left, Right, Neutral)
            if matches!(cue, DanceCue::Up | DanceCue::Down | DanceCue::Left | DanceCue::Right) {
                if self.candidate != cue {
                    self.candidate = cue;
                    self.since = time_ms;
                } else if self.since >= 0.0 && (time_ms - self.since >= self.stable_ms) {
                    let cue_name = cue.as_str().to_string();
                    if self.cues.last() != Some(&cue_name) || self.fade_since >= 0.0 {
                        self.cues.push(cue_name);
                        self.fade_since = -1.0;
                        self.low_since = -1.0;
                        self.last_opacity = 1.0;
                    }
                }
            } else if cue == DanceCue::Neutral {
                self.candidate = DanceCue::Neutral;
                self.since = time_ms;
            }

            return self.decision("Observing watch phase.");
        }

        if phase == DancePhase::Go {
            self.phase = phase;
            if !self.armed {
                return self.reject("Round started without seeing a full Ready boundary.");
            }
            if self.cues.len() != self.expected {
                return self.reject(format!(
                    "Incomplete observation: expected {} cues, saw {}.",
                    self.expected,
                    self.cues.len()
                ));
            }
            self.emitted = true;
            return self.decision("Go phase confirmed; proposals ready.");
        }

        self.phase = phase;
        self.decision("Status updated.")
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_dance_sequence_initialization() {
        let seq = PetDanceSequence::new(3).unwrap();
        assert_eq!(seq.expected, 3);
        assert!(PetDanceSequence::new(2).is_err());
        assert!(PetDanceSequence::new(8).is_err());
    }

    #[test]
    fn test_dance_sequence_lifecycle() {
        let mut seq = PetDanceSequence::new(3).unwrap();
        let size = (800, 600);

        // Ready boundary
        let dec = seq.observe(10.0, size, DancePhase::Ready, DanceCue::Neutral, DanceCue::Unknown, 1.0);
        assert!(!dec.rejected);

        // Watch phase
        let _ = seq.observe(20.0, size, DancePhase::Watch, DanceCue::Neutral, DanceCue::Unknown, 1.0);

        // Cue 1: Up
        let _ = seq.observe(30.0, size, DancePhase::Watch, DanceCue::Up, DanceCue::Unknown, 1.0);
        let dec_up = seq.observe(110.0, size, DancePhase::Watch, DanceCue::Up, DanceCue::Unknown, 1.0);
        assert_eq!(dec_up.cues, vec!["up"]);
    }
}
