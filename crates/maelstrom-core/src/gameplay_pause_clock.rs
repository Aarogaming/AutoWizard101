//! Staged pure native GameplayPauseClock port. This accounts scene time; it is
//! not a UI pause/resume controller and never grants input ownership/freshness.
//! Kept unexported until crate-root integration is separately reviewed.

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ClockError {
    InvalidPauseBudget,
    InvalidGameplayBudget,
    InvalidWallTime,
    ContradictoryScene,
}

#[derive(Debug, Clone, PartialEq)]
pub struct ClockState {
    pub started: bool,
    pub is_loading: bool,
    pub active_ms: f64,
    pub paused_ms: f64,
    pub stop_reason: Option<&'static str>,
    pub gameplay_allowed: bool,
}

#[derive(Debug)]
pub struct GameplayPauseClock {
    pause_budget_ms: f64,
    gameplay_budget_ms: f64,
    last_wall_ms: f64,
    advanced: bool,
    gameplay_observed: bool,
    started: bool,
    is_loading: bool,
    active_ms: f64,
    paused_ms: f64,
    stop_reason: Option<&'static str>,
}

impl GameplayPauseClock {
    pub fn new(pause_budget_ms: f64, gameplay_budget_ms: f64) -> Result<Self, ClockError> {
        if !pause_budget_ms.is_finite() || pause_budget_ms <= 0.0 {
            return Err(ClockError::InvalidPauseBudget);
        }
        if !gameplay_budget_ms.is_finite() || gameplay_budget_ms <= 0.0 {
            return Err(ClockError::InvalidGameplayBudget);
        }
        Ok(Self {
            pause_budget_ms,
            gameplay_budget_ms,
            last_wall_ms: 0.0,
            advanced: false,
            gameplay_observed: false,
            started: false,
            is_loading: false,
            active_ms: 0.0,
            paused_ms: 0.0,
            stop_reason: None,
        })
    }

    pub fn state(&self) -> ClockState {
        ClockState {
            started: self.started,
            is_loading: self.is_loading,
            active_ms: self.active_ms,
            paused_ms: self.paused_ms,
            stop_reason: self.stop_reason,
            gameplay_allowed: self.started
                && !self.is_loading
                && self.gameplay_observed
                && self.stop_reason.is_none(),
        }
    }

    /// Wall time is elapsed run time, not capture time. The elapsed interval
    /// belongs to the prior scene; first-call elapsed time is startup waiting.
    /// Invalid observations return an error without changing any state.
    pub fn advance(
        &mut self,
        wall_ms: f64,
        loading: bool,
        gameplay_observed: bool,
    ) -> Result<(), ClockError> {
        if !wall_ms.is_finite() || wall_ms < 0.0 || (self.advanced && wall_ms <= self.last_wall_ms)
        {
            return Err(ClockError::InvalidWallTime);
        }
        if loading && gameplay_observed {
            return Err(ClockError::ContradictoryScene);
        }
        let delta = wall_ms - self.last_wall_ms;
        if !self.started || self.is_loading {
            self.paused_ms += delta;
        } else {
            self.active_ms += delta;
        }

        // Expired time is judged before applying the new scene. A ready frame
        // at a deadline cannot forgive startup/loading already exhausted.
        if self.stop_reason.is_none() {
            if self.paused_ms >= self.pause_budget_ms {
                self.stop_reason = Some(if self.is_loading {
                    "loading-wait-limit"
                } else {
                    "game-start-not-observed"
                });
            } else if self.active_ms >= self.gameplay_budget_ms {
                self.stop_reason = Some("game-duration-limit");
            }
        }
        self.last_wall_ms = wall_ms;
        self.advanced = true;
        self.is_loading = loading;
        self.gameplay_observed = gameplay_observed;
        self.started |= gameplay_observed;
        Ok(())
    }
}

impl Default for GameplayPauseClock {
    fn default() -> Self {
        Self::new(45_000.0, 75_000.0).expect("valid fixed budgets")
    }
}
