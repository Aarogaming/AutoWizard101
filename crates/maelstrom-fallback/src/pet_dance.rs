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

    fn reject(&mut self, reason: impl Into<String>) -> DanceDecision {
        let fault = self.fault.get_or_insert_with(|| reason.into()).clone();
        self.decision(fault)
    }

    fn decision(&self, reason: impl Into<String>) -> DanceDecision {
        DanceDecision {
            cues: self.cues.clone(),
            proposals: Vec::new(),
            reason: reason.into(),
            rejected: self.fault.is_some(),
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
        let time_valid = time_ms.is_finite()
            && time_ms >= 0.0
            && (self.last_time < 0.0 || time_ms > self.last_time);
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
                return self
                    .reject("Window geometry changed; recalibrate and restart the teacher.");
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

        if matches!(
            phase,
            DancePhase::Ready | DancePhase::Success | DancePhase::Failure
        ) {
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
            return self.decision(
                "Inputs finished; waiting for independently recognized success or failure.",
            );
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
                return self
                    .reject("A result or Ready boundary is required before the next round.");
            }
            if !self.active {
                self.active = true;
                self.armed = self.boundary_seen;
                self.candidate = DanceCue::Unknown;
                self.since = -1.0;
                self.boundary_seen = false;
                self.outcome_applied = false;
            } else if gap && self.fault.is_none() {
                self.fault =
                    Some("Observation gap exceeds 150 ms; a cue may have been missed.".to_string());
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
                    || !matches!(
                        fade_of,
                        DanceCue::Up | DanceCue::Down | DanceCue::Left | DanceCue::Right
                    )
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
                    return self
                        .reject("Fade lasted too long or brightened without a new clear cue.");
                }

                self.last_opacity = self.last_opacity.min(opacity);
                self.candidate = DanceCue::Fading;
                self.since = time_ms;

                if opacity <= 0.25 {
                    if self.low_since < 0.0 {
                        self.low_since = time_ms;
                    }
                    if time_ms - self.low_since >= 16.0 {
                        self.armed = true;
                    }
                } else {
                    self.low_since = -1.0;
                    self.armed = false;
                }

                return self.decision(if self.armed {
                    "Known arrow faded to a confirmed trough."
                } else {
                    "Following the confirmed arrow fade; no new cue counted."
                });
            }

            // A low fade followed by a neutral frame is one continuous trough.
            if cue == DanceCue::Neutral && self.fade_since >= 0.0 {
                if self.low_since < 0.0 {
                    self.low_since = time_ms;
                }
                if time_ms - self.low_since >= 16.0 {
                    self.armed = true;
                }
            }
            if cue != DanceCue::Neutral {
                self.fade_since = -1.0;
                self.low_since = -1.0;
                self.last_opacity = 1.0;
            }
            if cue != self.candidate {
                self.candidate = cue;
                self.since = time_ms;
            }
            if time_ms - self.since < self.stable_ms {
                return self.decision("Waiting for a stable cue or neutral gap.");
            }
            if cue == DanceCue::Neutral {
                self.armed = true;
                return self.decision("Neutral gap confirmed.");
            }
            let direction = cue.as_str().to_string();
            if !self.armed {
                if self.cues.is_empty() {
                    return self.reject("Recording began inside a cue; its beginning is missing.");
                }
                if self.cues.last() != Some(&direction) {
                    return self.reject("Cue changed without a confirmed neutral gap.");
                }
                return self.decision("Held cue already counted.");
            }
            self.armed = false;
            self.cues.push(direction);
            return if self.cues.len() > self.expected {
                self.reject("Too many cues for the selected round.")
            } else {
                self.decision(format!(
                    "Remembered {}/{} cues.",
                    self.cues.len(),
                    self.expected
                ))
            };
        }

        self.phase = phase;
        if self.emitted {
            return self.decision("Go already handled; no duplicate proposal.");
        }
        if !self.active {
            return self.decision("Go without a complete observed Watch sequence.");
        }
        if gap && self.fault.is_none() {
            self.fault = Some("Observation gap before Go; round discarded.".to_string());
        }
        if let Some(fault) = &self.fault {
            return self.decision(fault.clone());
        }
        if self.cues.len() != self.expected
            || !self.armed
            || !matches!(self.candidate, DanceCue::Neutral | DanceCue::Fading)
        {
            return self.reject("Go arrived without a complete, separated cue sequence.");
        }
        self.emitted = true;
        DanceDecision {
            cues: self.cues.clone(),
            proposals: self.cues.iter().map(|cue| format!("dance-{cue}")).collect(),
            reason: "Verified sequence proposal at Go; no input sent.".to_string(),
            rejected: false,
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    // Same 50 ms observation cadence and cue/Go helpers as the native
    // PetDanceTeacherTests and PetDanceFadeTests. These are synthetic observations.
    struct Round {
        sequence: PetDanceSequence,
        time: f64,
    }
    impl Round {
        fn new(expected: usize) -> Self {
            Self {
                sequence: PetDanceSequence::new(expected).unwrap(),
                time: 0.0,
            }
        }
        fn sample(&mut self, phase: DancePhase, cue: DanceCue, count: usize) -> DanceDecision {
            self.fade(phase, cue, count, DanceCue::Unknown, f64::NAN)
        }
        fn fade(
            &mut self,
            phase: DancePhase,
            cue: DanceCue,
            count: usize,
            fade_of: DanceCue,
            opacity: f64,
        ) -> DanceDecision {
            let mut result = None;
            for _ in 0..count {
                self.time += 50.0;
                result = Some(self.sequence.observe(
                    self.time,
                    (1280, 720),
                    phase,
                    cue,
                    fade_of,
                    opacity,
                ));
            }
            result.unwrap()
        }
        fn cue(&mut self, cue: DanceCue) {
            self.sample(DancePhase::Watch, DanceCue::Neutral, 3);
            self.sample(DancePhase::Watch, cue, 5);
        }
        fn arrow(&mut self, cue: DanceCue) {
            self.sample(DancePhase::Watch, cue, 3);
            self.fade(DancePhase::Watch, DanceCue::Fading, 1, cue, 0.5);
            self.fade(DancePhase::Watch, DanceCue::Fading, 2, cue, 0.2);
        }
        fn go(&mut self) -> DanceDecision {
            self.sample(DancePhase::Watch, DanceCue::Neutral, 3);
            self.sample(DancePhase::Go, DanceCue::Unknown, 1)
        }
    }

    #[test]
    fn native_varied_held_and_repeated_cues_emit_registered_actions_once() {
        let directions = [
            DanceCue::Up,
            DanceCue::Down,
            DanceCue::Left,
            DanceCue::Right,
        ];
        // Exhaust all three-cue words; cover longer rounds with all repeated cues.
        for word in 0..64 {
            let cues = [
                directions[word % 4],
                directions[word / 4 % 4],
                directions[word / 16],
            ];
            let mut round = Round::new(3);
            for cue in cues {
                round.cue(cue);
            }
            let result = round.go();
            assert!(!result.rejected, "{}", result.reason);
            assert_eq!(
                result.proposals,
                cues.iter()
                    .map(|c| format!("dance-{}", c.as_str()))
                    .collect::<Vec<_>>()
            );
            assert!(round
                .sample(DancePhase::Go, DanceCue::Unknown, 3)
                .proposals
                .is_empty());
        }
        for length in 4..=7 {
            let mut round = Round::new(length);
            for _ in 0..length {
                round.cue(DanceCue::Left);
            }
            assert_eq!(round.go().proposals, vec!["dance-left"; length]);
        }
    }

    #[test]
    fn native_broken_evidence_latches_until_boundary() {
        for fault in [
            "gap",
            "backwards",
            "duplicate",
            "cue",
            "phase",
            "geometry",
            "neutral",
            "extra",
        ] {
            let mut round = Round::new(3);
            round.cue(DanceCue::Up);
            match fault {
                "gap" => {
                    round.time += 200.0;
                    round.sample(DancePhase::Watch, DanceCue::Neutral, 3);
                }
                "backwards" | "duplicate" => {
                    let time = round.time - if fault == "backwards" { 1.0 } else { 0.0 };
                    round.sequence.observe(
                        time,
                        (1280, 720),
                        DancePhase::Watch,
                        DanceCue::Neutral,
                        DanceCue::Unknown,
                        f64::NAN,
                    );
                }
                "cue" => {
                    round.sample(DancePhase::Watch, DanceCue::Unknown, 3);
                }
                "phase" => {
                    round.sample(DancePhase::Unknown, DanceCue::Neutral, 3);
                }
                "geometry" => {
                    round.sequence.observe(
                        round.time + 1.0,
                        (1024, 768),
                        DancePhase::Watch,
                        DanceCue::Neutral,
                        DanceCue::Unknown,
                        f64::NAN,
                    );
                }
                "neutral" => {
                    round.sample(DancePhase::Watch, DanceCue::Left, 3);
                }
                _ => round.cue(DanceCue::Left),
            }
            round.cue(DanceCue::Down);
            round.cue(DanceCue::Right);
            let result = round.go();
            assert!(result.rejected, "{fault}");
            assert!(result.proposals.is_empty(), "{fault}");
            round.sample(DancePhase::Failure, DanceCue::Unknown, 3);
            for _ in 0..3 {
                round.cue(DanceCue::Up);
            }
            assert_eq!(round.go().proposals.len(), 3, "{fault}");
        }
    }

    #[test]
    fn native_fade_and_short_neutral_troughs_rearm_repeated_arrows() {
        for trough in ["fade", "low-neutral", "two-neutral"] {
            let mut round = Round::new(3);
            round.sample(DancePhase::Watch, DanceCue::Neutral, 3);
            for cue in [DanceCue::Right, DanceCue::Right, DanceCue::Up] {
                if trough == "fade" {
                    round.arrow(cue);
                } else {
                    round.sample(DancePhase::Watch, cue, 3);
                    let (opacity, count) = if trough == "low-neutral" {
                        (0.2, 1)
                    } else {
                        (0.31, 2)
                    };
                    round.fade(DancePhase::Watch, DanceCue::Fading, 1, cue, opacity);
                    round.sample(DancePhase::Watch, DanceCue::Neutral, count);
                }
            }
            let result = round.sample(DancePhase::Go, DanceCue::Neutral, 1);
            assert_eq!(
                result.proposals,
                ["dance-right", "dance-right", "dance-up"],
                "{trough}: {}",
                result.reason
            );
        }
    }

    #[test]
    fn native_invalid_fades_never_emit() {
        for problem in ["direction", "brightening", "overlong", "unknown", "nan"] {
            let mut round = Round::new(3);
            round.cue(DanceCue::Up);
            round.fade(DancePhase::Watch, DanceCue::Fading, 1, DanceCue::Up, 0.4);
            let result = match problem {
                "direction" => {
                    round.fade(DancePhase::Watch, DanceCue::Fading, 1, DanceCue::Left, 0.2)
                }
                "brightening" => {
                    round.fade(DancePhase::Watch, DanceCue::Fading, 1, DanceCue::Up, 0.6)
                }
                "overlong" => round.fade(DancePhase::Watch, DanceCue::Fading, 8, DanceCue::Up, 0.4),
                "nan" => round.fade(
                    DancePhase::Watch,
                    DanceCue::Fading,
                    1,
                    DanceCue::Up,
                    f64::NAN,
                ),
                _ => round.sample(DancePhase::Watch, DanceCue::Unknown, 3),
            };
            assert!(result.rejected, "{problem}");
            assert!(round.go().proposals.is_empty());
        }
    }

    #[test]
    fn native_success_once_failure_and_loading_preserve_expected_length() {
        let mut round = Round::new(3);
        for _ in 0..3 {
            round.cue(DanceCue::Up);
        }
        assert_eq!(round.go().proposals.len(), 3);
        round.sample(DancePhase::Success, DanceCue::Neutral, 3);
        round.sample(DancePhase::Resolving, DanceCue::Neutral, 3);
        round.sample(DancePhase::Success, DanceCue::Neutral, 3);
        for _ in 0..4 {
            round.arrow(DanceCue::Down);
        }
        assert_eq!(
            round
                .sample(DancePhase::Go, DanceCue::Neutral, 1)
                .proposals
                .len(),
            4
        );
        round.sample(DancePhase::Failure, DanceCue::Unknown, 3);
        let loading = round.sample(DancePhase::Loading, DanceCue::Unknown, 400);
        assert!(!loading.rejected);
        assert!(loading.cues.is_empty());
        assert!(round
            .sample(DancePhase::Go, DanceCue::Unknown, 1)
            .proposals
            .is_empty());
        round.sample(DancePhase::Loading, DanceCue::Unknown, 1);
        for _ in 0..4 {
            round.cue(DanceCue::Right);
        }
        assert_eq!(round.go().proposals.len(), 4);
    }

    #[test]
    fn native_missing_start_premature_go_partial_fade_and_go_gap_fail_closed() {
        let mut missing = Round::new(3);
        missing.sample(DancePhase::Watch, DanceCue::Up, 3);
        missing.cue(DanceCue::Down);
        missing.cue(DanceCue::Right);
        assert!(missing.go().proposals.is_empty());
        let mut short = Round::new(3);
        short.cue(DanceCue::Up);
        assert!(short.go().proposals.is_empty());
        assert!(Round::new(3)
            .sample(DancePhase::Go, DanceCue::Unknown, 1)
            .proposals
            .is_empty());
        for gap in [false, true] {
            let mut round = Round::new(3);
            round.sample(DancePhase::Watch, DanceCue::Neutral, 3);
            round.arrow(DanceCue::Down);
            round.arrow(DanceCue::Left);
            round.sample(DancePhase::Watch, DanceCue::Up, 3);
            round.fade(
                DancePhase::Watch,
                DanceCue::Fading,
                2,
                DanceCue::Up,
                if gap { 0.2 } else { 0.5 },
            );
            if gap {
                round.time += 200.0;
            }
            let result = round.sample(DancePhase::Go, DanceCue::Fading, 1);
            assert!(result.rejected);
            assert!(result.proposals.is_empty());
            round.sample(DancePhase::Resolving, DanceCue::Neutral, 3);
            assert!(
                round
                    .sample(DancePhase::Watch, DanceCue::Neutral, 3)
                    .rejected
            );
        }
    }

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
        let dec = seq.observe(
            10.0,
            size,
            DancePhase::Ready,
            DanceCue::Neutral,
            DanceCue::Unknown,
            1.0,
        );
        assert!(!dec.rejected);

        // Watch phase
        let _ = seq.observe(
            20.0,
            size,
            DancePhase::Watch,
            DanceCue::Neutral,
            DanceCue::Unknown,
            1.0,
        );

        // Cue 1: Up
        let _ = seq.observe(
            30.0,
            size,
            DancePhase::Watch,
            DanceCue::Up,
            DanceCue::Unknown,
            1.0,
        );
        let dec_up = seq.observe(
            110.0,
            size,
            DancePhase::Watch,
            DanceCue::Up,
            DanceCue::Unknown,
            1.0,
        );
        assert_eq!(dec_up.cues, vec!["up"]);
    }
}
