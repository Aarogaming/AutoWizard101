//! `capability_token`: a draft model of Rust ownership, borrowing, and typestate semantics for runtime authority.
//!
//! Status (audit 2026-11-20): offline library code with unit tests. Nothing outside this module and its tests
//! uses any of these types: no actuator accepts a token or a lease, and nothing acquires one. The one thing the
//! types do guarantee at compile time is the move-only shape: `InputLeaseToken` and `SingleUseTurnIntent`
//! cannot be cloned and are consumed by value (the doctests pin this). They guarantee nothing about the game or
//! the input device: a `Verified<T>` can be built without `verify` (`new_verified`, `pub` fields, `Deserialize`),
//! leases are not exclusive, and a token carries no freshness or combat check once acquired. See
//! `docs/architecture/OWNERSHIP_BORROWING_AND_TYPESTATES.md`.
//!
//! Models:
//! - Typestate transitions (`Unverified<T>` -> `Verified<T>`): a convention for marking sensor data as checked.
//! - Scoped capability tokens (`OutOfCombatToken<'a>`, `ZoneLifetimeToken<'zone>`): borrows of a fact or a zone id.
//! - Move-only actuation leases (`InputLeaseToken`): a consumed-by-value lease that returns a receipt.
//! - Single-use combat turn intents (`SingleUseTurnIntent`): consumed by value on commitment.
//!
//! Follows `Facts ≠ Authority ≠ Actions` as a naming discipline, not as an enforced rule.

use alloc::string::String;
use core::marker::PhantomData;
use serde::{Deserialize, Serialize};

/// Error returned when verification of raw sensory data fails.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct VerificationFailure {
    pub reason: String,
    pub source: String,
    pub timestamp_ms: u64,
}

/// Typestate: Unverified raw observation freshly captured from a sensor, OCR, or memory probe.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct Unverified<T> {
    pub data: T,
    pub source: String,
    pub timestamp_ms: u64,
}

impl<T> Unverified<T> {
    pub fn new(data: T, source: impl Into<String>, timestamp_ms: u64) -> Self {
        Self {
            data,
            source: source.into(),
            timestamp_ms,
        }
    }

    /// Consumes the unverified observation, promoting it to `Verified<T>` if the validator passes.
    ///
    /// The freshness window starts at the observation's own `timestamp_ms` (capture time), not at the time of
    /// this call; the API has no clock. `confidence` is clamped into `0.0..=1.0`, but a `NaN` passes through
    /// the clamp unchanged.
    pub fn verify<F>(
        self,
        validator: F,
        confidence: f64,
        ttl_ms: u64,
    ) -> Result<Verified<T>, VerificationFailure>
    where
        F: FnOnce(&T) -> bool,
    {
        if validator(&self.data) {
            Ok(Verified {
                data: self.data,
                confidence: confidence.clamp(0.0, 1.0),
                verified_at_ms: self.timestamp_ms,
                ttl_ms,
            })
        } else {
            Err(VerificationFailure {
                reason: String::from("Validation predicate rejected candidate fact"),
                source: self.source,
                timestamp_ms: self.timestamp_ms,
            })
        }
    }
}

/// Typestate: Verified epistemic fact grounded by a trust validator, carrying confidence and a freshness TTL.
///
/// The type does not prove a validator ran: `new_verified` is `pub`, `confidence`, `verified_at_ms` and
/// `ttl_ms` are `pub` fields (so a TTL can be edited after the fact), and `Deserialize` is derived.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct Verified<T> {
    data: T,
    pub confidence: f64,
    pub verified_at_ms: u64,
    pub ttl_ms: u64,
}

impl<T> Verified<T> {
    /// Constructs a verified fact without running a validator. Despite the intent, nothing restricts this to
    /// trusted validation routines: it is `pub`. Confidence is clamped like in `verify`.
    pub fn new_verified(data: T, confidence: f64, verified_at_ms: u64, ttl_ms: u64) -> Self {
        Self {
            data,
            confidence: confidence.clamp(0.0, 1.0),
            verified_at_ms,
            ttl_ms,
        }
    }

    /// Evaluates if the verified fact is within its active Time-To-Live window.
    pub fn is_fresh(&self, now_ms: u64) -> bool {
        now_ms >= self.verified_at_ms && (now_ms - self.verified_at_ms) <= self.ttl_ms
    }

    /// Returns a shared borrow of the inner fact if still fresh.
    pub fn read_if_fresh(&self, now_ms: u64) -> Option<&T> {
        if self.is_fresh(now_ms) {
            Some(&self.data)
        } else {
            None
        }
    }

    /// Unconditionally returns a shared borrow of the inner fact.
    pub fn get(&self) -> &T {
        &self.data
    }

    /// Consumes the wrapper, returning the underlying verified data.
    pub fn into_inner(self) -> T {
        self.data
    }
}

/// Fact payload recording the player's combat engagement status.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct CombatPresenceFact {
    pub in_combat: bool,
    pub sigil_index: Option<usize>,
}

/// Scoped capability token proving the character was verified outside combat when it was acquired.
/// Cannot outlive the `Verified<CombatPresenceFact>` borrow `'a`.
///
/// Acquisition is the only check. The token has no methods, does not re-check freshness or combat state, and a
/// newer `Verified<CombatPresenceFact>` saying "in combat" does not invalidate it (the borrowed fact is
/// immutable). Nothing requires one yet.
#[derive(Debug)]
pub struct OutOfCombatToken<'a> {
    _presence: &'a Verified<CombatPresenceFact>,
}

impl<'a> OutOfCombatToken<'a> {
    /// Attempts to acquire an `OutOfCombatToken`.
    /// Returns `None` if the player is in combat or the observation is stale.
    pub fn try_acquire(
        presence: &'a Verified<CombatPresenceFact>,
        now_ms: u64,
    ) -> Option<OutOfCombatToken<'a>> {
        if let Some(fact) = presence.read_if_fresh(now_ms) {
            if !fact.in_combat {
                Some(OutOfCombatToken {
                    _presence: presence,
                })
            } else {
                None
            }
        } else {
            None
        }
    }
}

/// Holds a zone id borrowed for `'zone` plus an epoch. Any `&str` works (a `'static` literal included), so this
/// does not prove the zone is loaded and cannot detect a despawned zone; it only ties the id's borrow to a scope.
#[derive(Debug)]
pub struct ZoneLifetimeToken<'zone> {
    pub zone_id: &'zone str,
    pub instance_epoch_ms: u64,
    _marker: PhantomData<&'zone ()>,
}

impl<'zone> ZoneLifetimeToken<'zone> {
    pub fn new(zone_id: &'zone str, instance_epoch_ms: u64) -> Self {
        Self {
            zone_id,
            instance_epoch_ms,
            _marker: PhantomData,
        }
    }
}

/// Receipt returned after executing a physical action pulse under lease ownership.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct ActionExecutionReceipt {
    pub lease_id: u64,
    pub key: char,
    pub duration_ms: u32,
    pub executed_at_ms: u64,
    pub success: bool,
}

/// Move-only, non-cloneable lease record. It is not an exclusive input owner: `acquire` is `pub` and keeps no
/// registry, so two leases (even with the same `lease_id`) can be valid at once, and it is affine rather than
/// linear (dropping an unused lease is allowed). The real single input owner is the C# `GameInputLease`.
///
/// The `Drop` impl releases nothing; see the comment on it.
///
/// ```compile_fail,E0599
/// use maelstrom_core::capability_token::InputLeaseToken;
/// let lease = InputLeaseToken::acquire(1, "owner", 0, 100);
/// let _copy = lease.clone();
/// ```
#[derive(Debug)]
pub struct InputLeaseToken {
    pub lease_id: u64,
    pub owner: String,
    pub acquired_at_ms: u64,
    pub duration_ms: u64,
    active: bool,
}

impl InputLeaseToken {
    /// Acquires an exclusive input lease.
    pub fn acquire(
        lease_id: u64,
        owner: impl Into<String>,
        acquired_at_ms: u64,
        duration_ms: u64,
    ) -> Self {
        Self {
            lease_id,
            owner: owner.into(),
            acquired_at_ms,
            duration_ms,
            active: true,
        }
    }

    /// Whether the lease is currently valid and unexpired.
    pub fn is_valid(&self, now_ms: u64) -> bool {
        self.active
            && now_ms >= self.acquired_at_ms
            && (now_ms - self.acquired_at_ms) <= self.duration_ms
    }

    /// Consumes the lease and returns a receipt. It dispatches nothing: this module touches no input device, and
    /// `success: true` only means the lease was still valid at `now_ms`.
    ///
    /// The lease is moved, so a second use is a compile error:
    ///
    /// ```
    /// use maelstrom_core::capability_token::InputLeaseToken;
    /// let lease = InputLeaseToken::acquire(1, "owner", 0, 100);
    /// let receipt = lease.consume_action('W', 10, 5).unwrap();
    /// assert!(receipt.success);
    /// ```
    ///
    /// ```compile_fail,E0382
    /// use maelstrom_core::capability_token::InputLeaseToken;
    /// let lease = InputLeaseToken::acquire(1, "owner", 0, 100);
    /// let _first = lease.consume_action('W', 10, 5);
    /// let _second = lease.consume_action('W', 10, 6);
    /// ```
    pub fn consume_action(
        mut self,
        key: char,
        duration_ms: u32,
        now_ms: u64,
    ) -> Result<ActionExecutionReceipt, String> {
        if !self.is_valid(now_ms) {
            self.active = false;
            return Err(String::from("Cannot execute action: lease has expired"));
        }

        self.active = false; // Mark inactive before consumption

        Ok(ActionExecutionReceipt {
            lease_id: self.lease_id,
            key,
            duration_ms,
            executed_at_ms: now_ms,
            success: true,
        })
    }
}

impl Drop for InputLeaseToken {
    fn drop(&mut self) {
        // Intended: release held keys and return to Idle. Today it only clears a private flag that nothing can
        // read after the drop, and this module touches no input device, so nothing is released or restored.
        self.active = false;
    }
}

/// Receipt returned when a combat turn decision is committed.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct TurnCommitReceipt {
    pub round_number: u32,
    pub spell_name: String,
    pub target_index: usize,
    pub committed_at_ms: u64,
}

/// Single-use combat turn intent. Cannot be cloned; consumed upon commitment.
///
/// Moving stops the same intent value being committed twice; it does not stop a second intent being committed
/// for the same round (`round_number` and `now_ms` are caller-supplied and nothing records them).
#[derive(Debug)]
pub struct SingleUseTurnIntent {
    pub spell_name: String,
    pub slot_index: usize,
    pub target_index: usize,
}

impl SingleUseTurnIntent {
    pub fn new(spell_name: impl Into<String>, slot_index: usize, target_index: usize) -> Self {
        Self {
            spell_name: spell_name.into(),
            slot_index,
            target_index,
        }
    }

    /// Consumes the turn intent and returns a receipt. This intent value cannot be committed again:
    ///
    /// ```
    /// use maelstrom_core::capability_token::SingleUseTurnIntent;
    /// let intent = SingleUseTurnIntent::new("Meteor Strike", 1, 0);
    /// let receipt = intent.commit(1, 10);
    /// assert_eq!(receipt.round_number, 1);
    /// ```
    ///
    /// ```compile_fail,E0382
    /// use maelstrom_core::capability_token::SingleUseTurnIntent;
    /// let intent = SingleUseTurnIntent::new("Meteor Strike", 1, 0);
    /// let _first = intent.commit(1, 10);
    /// let _second = intent.commit(1, 11);
    /// ```
    pub fn commit(self, round_number: u32, now_ms: u64) -> TurnCommitReceipt {
        TurnCommitReceipt {
            round_number,
            spell_name: self.spell_name,
            target_index: self.target_index,
            committed_at_ms: now_ms,
        }
    }
}
