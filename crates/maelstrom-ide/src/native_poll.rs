//! Single-flight read-only polling. UI never blocks on pipe I/O.
use maelstrom_fallback::maelstrom_bridge::{poll_snapshot_blocking, NativeHostState};
use std::path::PathBuf;
use std::sync::{
    atomic::{AtomicBool, Ordering},
    mpsc, Arc,
};
use std::time::{Duration, Instant};

type PollResult = Result<NativeHostState, String>;
type Reader = fn(PathBuf, Arc<AtomicBool>) -> PollResult;

fn read_host(root: PathBuf, cancel: Arc<AtomicBool>) -> PollResult {
    poll_snapshot_blocking(root, cancel).map_err(|e| e.to_string())
}

pub(crate) struct NativePoll {
    pending: Option<(mpsc::Receiver<PollResult>, Arc<AtomicBool>)>,
    last_start: Option<Instant>,
    reader: Reader,
}

impl Default for NativePoll {
    fn default() -> Self {
        Self {
            pending: None,
            last_start: None,
            reader: read_host,
        }
    }
}

impl NativePoll {
    pub fn cancel(&mut self) {
        if let Some((_, cancel)) = &self.pending {
            cancel.store(true, Ordering::Release);
        }
    }

    /// Cancelled workers retain the single-flight slot until they exit. Their results
    /// are discarded even if the user reopens the pane before cancellation completes.
    pub fn tick(&mut self, active: bool, root: PathBuf, now: Instant) -> Option<PollResult> {
        if !active {
            self.cancel();
        }
        if let Some((receiver, cancel)) = &self.pending {
            match receiver.try_recv() {
                Ok(result) => {
                    let cancelled = cancel.load(Ordering::Acquire);
                    self.pending = None;
                    if active && !cancelled {
                        return Some(result);
                    }
                }
                Err(mpsc::TryRecvError::Disconnected) => {
                    let cancelled = cancel.load(Ordering::Acquire);
                    self.pending = None;
                    if active && !cancelled {
                        return Some(Err("Observation worker exited".into()));
                    }
                }
                Err(mpsc::TryRecvError::Empty) => return None,
            }
        }
        if active
            && self
                .last_start
                .is_none_or(|last| now.duration_since(last) >= Duration::from_secs(1))
        {
            let (sender, receiver) = mpsc::sync_channel(1);
            let cancel = Arc::new(AtomicBool::new(false));
            let worker_cancel = cancel.clone();
            let reader = self.reader;
            self.last_start = Some(now);
            match std::thread::Builder::new()
                .name("maelstrom-state-read".into())
                .spawn(move || {
                    let _ = sender.send(reader(root, worker_cancel));
                }) {
                Ok(_) => self.pending = Some((receiver, cancel)),
                Err(error) => return Some(Err(error.to_string())),
            }
        }
        None
    }
}

impl Drop for NativePoll {
    fn drop(&mut self) {
        self.cancel();
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn rate_limit_and_single_flight_do_not_launch_early() {
        let now = Instant::now();
        let mut poll = NativePoll {
            last_start: Some(now),
            pending: None,
            reader: read_host,
        };
        assert!(poll
            .tick(true, PathBuf::new(), now + Duration::from_millis(999))
            .is_none());
        assert!(poll.pending.is_none());
        let (_sender, receiver) = mpsc::channel();
        poll.pending = Some((receiver, Arc::new(AtomicBool::new(false))));
        assert!(poll
            .tick(true, PathBuf::new(), now + Duration::from_secs(2))
            .is_none());
        assert!(poll.pending.is_some());
        assert_eq!(poll.last_start, Some(now));
    }

    #[test]
    fn pane_close_cancels_and_discards_late_success() {
        let (sender, receiver) = mpsc::channel();
        let cancel = Arc::new(AtomicBool::new(false));
        let now = Instant::now();
        let mut poll = NativePoll {
            pending: Some((receiver, cancel.clone())),
            last_start: Some(now),
            ..Default::default()
        };
        assert!(poll.tick(false, PathBuf::new(), now).is_none());
        assert!(cancel.load(Ordering::Acquire));
        sender.send(Ok(NativeHostState::default())).unwrap();
        assert!(poll.tick(true, PathBuf::new(), now).is_none());
        assert!(poll.pending.is_none());
    }

    #[test]
    fn worker_failure_is_delivered_once() {
        let (sender, receiver) = mpsc::channel();
        let now = Instant::now();
        let mut poll = NativePoll {
            pending: Some((receiver, Arc::new(AtomicBool::new(false)))),
            last_start: Some(now),
            ..Default::default()
        };
        sender.send(Err("offline fixture failure".into())).unwrap();
        assert!(poll.tick(true, PathBuf::new(), now).unwrap().is_err());
        assert!(poll.tick(true, PathBuf::new(), now).is_none());
    }
}
