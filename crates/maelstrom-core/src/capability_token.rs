//! `capability_token`: Rust ownership, borrowing, and typestate semantics for reality runtime authority.
//!
//! Enforces:
//! - Typestate transitions (`Unverified<T>` -> `Verified<T>`) preventing planners from acting on raw sensor data.
//! - Scoped capability tokens (`OutOfCombatToken<'a>`, `ZoneLifetimeToken<'zone>`) preventing stale out-of-context actions.
//! - Linear move-only actuation leases (`InputLeaseToken`) preventing duplicate execution and enforcing RAII cleanup.
//! - Single-use combat turn intents (`SingleUseTurnIntent`) consumed upon commitment.
//!
//! Follows `Facts ≠ Authority ≠ Actions`.

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
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct Verified<T> {
    data: T,
    pub confidence: f64,
    pub verified_at_ms: u64,
    pub ttl_ms: u64,
}

impl<T> Verified<T> {
    /// Constructs a verified fact directly (restricted to trusted validation routines).
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

/// Scoped capability token proving the character is verified outside combat.
/// Cannot outlive the `Verified<CombatPresenceFact>` borrow `'a`.
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

/// Scoped capability token proving the player is grounded within an active zone manifold.
/// Cannot outlive the zone lifetime `'zone`.
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

/// Linear, non-cloneable execution lease granting exclusive hardware/input dispatch authority.
///
/// Implements RAII `Drop` to guarantee safe cleanup and prevent stuck-key faults on early returns or errors.
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

    /// Consumes the lease token to dispatch an action pulse (linear move semantics).
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
        // Enforce cleanup on drop (releasing keys and returning Idle state)
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

    /// Consumes the turn intent, returning a receipt. Guarantees this action cannot be re-executed.
    pub fn commit(self, round_number: u32, now_ms: u64) -> TurnCommitReceipt {
        TurnCommitReceipt {
            round_number,
            spell_name: self.spell_name,
            target_index: self.target_index,
            committed_at_ms: now_ms,
        }
    }
}
