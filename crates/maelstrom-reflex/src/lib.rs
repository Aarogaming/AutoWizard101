//! `maelstrom-reflex`: Lightweight local policy inference and DAgger telemetry ring buffer.

use maelstrom_core::{ControlAction, Intent, ObservationFrame, PolicyController};
use std::sync::atomic::{AtomicUsize, Ordering};

pub struct TakeoverReplayBuffer {
    capacity: usize,
    count: AtomicUsize,
}

impl TakeoverReplayBuffer {
    pub fn new(capacity: usize) -> Self {
        Self {
            capacity,
            count: AtomicUsize::new(0),
        }
    }

    pub fn record_takeover_tick(
        &self,
        _intent: &Intent,
        _obs: &ObservationFrame,
        _action: &ControlAction,
    ) {
        let _ = self.count.fetch_add(1, Ordering::Relaxed);
    }

    pub fn len(&self) -> usize {
        self.count.load(Ordering::Relaxed).min(self.capacity)
    }

    pub fn is_empty(&self) -> bool {
        self.len() == 0
    }
}

/// Baseline stub policy controller (safe fallback when no onnx model is loaded).
pub struct StubReflexPolicy {
    pub fixed_confidence: f32,
}

impl PolicyController for StubReflexPolicy {
    fn propose(&mut self, _intent: &Intent, _obs: &ObservationFrame) -> (ControlAction, f32) {
        (ControlAction::NoOp, self.fixed_confidence)
    }

    fn on_outcome(&mut self, _intent: &Intent, _succeeded: bool) {}
}

#[cfg(test)]
mod tests {
    use super::*;
    use uuid::Uuid;

    #[test]
    fn test_replay_buffer_records_ticks() {
        let buffer = TakeoverReplayBuffer::new(1024);
        assert!(buffer.is_empty());

        let intent = Intent {
            id: Uuid::new_v4(),
            card_id: Uuid::new_v4(),
            name: "Test Intent".into(),
            target_description: "Target".into(),
            required_confidence: 0.85,
        };
        let obs = ObservationFrame::default();
        let action = ControlAction::NoOp;

        buffer.record_takeover_tick(&intent, &obs, &action);
        assert_eq!(buffer.len(), 1);
    }
}
