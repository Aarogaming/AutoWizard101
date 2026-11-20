//! Integration tests for Capability Tokens, Typestates & Borrowing Semantics (`capability_token.rs`).
//! Validates Unverified -> Verified lifecycle, OutOfCombatToken acquisition, linear InputLeaseToken consumption,
//! and single-use combat turn commitment.

use maelstrom_core::capability_token::*;

#[test]
fn test_unverified_to_verified_typestate_transition() {
    let now_ms = 10_000;
    let raw_presence = Unverified::new(
        CombatPresenceFact {
            in_combat: false,
            sigil_index: None,
        },
        "OCR_Combat_HUD",
        now_ms,
    );

    // Validation passes: promotes to Verified<CombatPresenceFact>
    let verified = raw_presence
        .verify(|p| !p.in_combat, 0.95, 2000)
        .expect("Verification must pass for valid out-of-combat data");

    assert_eq!(verified.confidence, 0.95);
    assert_eq!(verified.verified_at_ms, now_ms);
    assert!(verified.is_fresh(now_ms + 500));
    assert!(!verified.get().in_combat);

    // Validation fails: rejects invalid observation
    let bad_presence = Unverified::new(
        CombatPresenceFact {
            in_combat: true,
            sigil_index: Some(2),
        },
        "OCR_Combat_HUD",
        now_ms,
    );

    let result = bad_presence.verify(|p| !p.in_combat, 0.95, 2000);
    assert!(result.is_err());
    let err = result.unwrap_err();
    assert_eq!(err.source, "OCR_Combat_HUD");
}

#[test]
fn test_verified_fact_freshness_and_ttl() {
    let verified = Verified::new_verified("TargetNPC:Private_O_Ryan", 0.90, 5000, 1000);

    // Fresh at 5500 ms (within 1000 ms TTL)
    assert!(verified.is_fresh(5500));
    assert_eq!(
        verified.read_if_fresh(5500),
        Some(&"TargetNPC:Private_O_Ryan")
    );

    // Stale at 6100 ms (exceeded 1000 ms TTL)
    assert!(!verified.is_fresh(6100));
    assert_eq!(verified.read_if_fresh(6100), None);
}

#[test]
fn test_out_of_combat_token_acquisition_and_borrow_gating() {
    let now_ms = 10_000;

    // Case 1: Verified out of combat -> Token acquisition succeeds
    let presence_out = Verified::new_verified(
        CombatPresenceFact {
            in_combat: false,
            sigil_index: None,
        },
        0.98,
        now_ms,
        3000,
    );

    let token_out = OutOfCombatToken::try_acquire(&presence_out, now_ms + 1000);
    assert!(
        token_out.is_some(),
        "Must acquire OutOfCombatToken when verified out of combat"
    );

    // Case 2: Verified in combat -> Token acquisition rejected
    let presence_in = Verified::new_verified(
        CombatPresenceFact {
            in_combat: true,
            sigil_index: Some(0),
        },
        0.98,
        now_ms,
        3000,
    );

    let token_in = OutOfCombatToken::try_acquire(&presence_in, now_ms + 1000);
    assert!(
        token_in.is_none(),
        "Must reject OutOfCombatToken when verified in combat"
    );

    // Case 3: Stale fact -> Token acquisition rejected
    let token_stale = OutOfCombatToken::try_acquire(&presence_out, now_ms + 4000);
    assert!(
        token_stale.is_none(),
        "Must reject OutOfCombatToken when observation is stale"
    );
}

#[test]
fn test_linear_input_lease_move_and_single_execution() {
    let now_ms = 20_000;
    let lease = InputLeaseToken::acquire(42, "PurePursuitSteering", now_ms, 500);

    assert!(lease.is_valid(now_ms + 200));

    // Consumes lease by value (move semantics)
    let receipt = lease
        .consume_action('W', 250, now_ms + 200)
        .expect("Must successfully dispatch action within lease duration");

    assert_eq!(receipt.lease_id, 42);
    assert_eq!(receipt.key, 'W');
    assert_eq!(receipt.duration_ms, 250);
    assert!(receipt.success);

    // An expired lease is rejected
    let expired_lease = InputLeaseToken::acquire(43, "Steering", now_ms, 100);
    let expired_result = expired_lease.consume_action('W', 250, now_ms + 200);
    assert!(expired_result.is_err());
    assert!(expired_result.unwrap_err().contains("expired"));
}

#[test]
fn test_single_use_turn_intent_consumption() {
    let intent = SingleUseTurnIntent::new("Meteor Strike", 1, 0);

    // Consumes intent by value; compiler prevents using `intent` afterwards
    let receipt = intent.commit(3, 30_000);

    assert_eq!(receipt.round_number, 3);
    assert_eq!(receipt.spell_name, "Meteor Strike");
    assert_eq!(receipt.target_index, 0);
    assert_eq!(receipt.committed_at_ms, 30_000);
}

#[test]
fn test_verified_freshness_window_is_inclusive_and_never_reaches_into_the_past() {
    let fact = Verified::new_verified(0u8, 1.0, 1000, 500);

    assert!(
        !fact.is_fresh(999),
        "an observation from the future is not fresh"
    );
    assert!(fact.is_fresh(1000));
    assert!(
        fact.is_fresh(1500),
        "the last millisecond of the TTL is still fresh"
    );
    assert!(!fact.is_fresh(1501));
    assert_eq!(fact.read_if_fresh(1500), Some(&0u8));
    assert_eq!(fact.read_if_fresh(1501), None);
    assert_eq!(*fact.get(), 0u8, "get() ignores freshness by design");
}

#[test]
fn test_verify_starts_the_ttl_at_capture_time_and_clamps_confidence() {
    // Captured at 1000. The API has no clock, so the window cannot start later than the capture.
    let fact = Unverified::new(7u8, "probe", 1000)
        .verify(|v| *v == 7, 1.7, 500)
        .unwrap();
    assert_eq!(fact.verified_at_ms, 1000);
    assert!(fact.is_fresh(1500));
    assert!(!fact.is_fresh(1501));
    assert_eq!(fact.confidence, 1.0);

    let low = Unverified::new(7u8, "probe", 0)
        .verify(|_| true, -0.2, 10)
        .unwrap();
    assert_eq!(low.confidence, 0.0);

    assert_eq!(Verified::new_verified(0u8, 3.0, 0, 1).confidence, 1.0);
    assert_eq!(Verified::new_verified(0u8, -3.0, 0, 1).confidence, 0.0);
}

#[test]
fn test_out_of_combat_token_freshness_boundary() {
    let presence = Verified::new_verified(
        CombatPresenceFact {
            in_combat: false,
            sigil_index: None,
        },
        1.0,
        100,
        50,
    );

    assert!(OutOfCombatToken::try_acquire(&presence, 99).is_none());
    assert!(OutOfCombatToken::try_acquire(&presence, 100).is_some());
    assert!(OutOfCombatToken::try_acquire(&presence, 150).is_some());
    assert!(OutOfCombatToken::try_acquire(&presence, 151).is_none());
}

#[test]
fn test_input_lease_window_boundaries() {
    let lease = InputLeaseToken::acquire(2, "owner", 100, 50);
    assert!(!lease.is_valid(99));
    assert!(lease.is_valid(100));
    assert!(lease.is_valid(150));
    assert!(!lease.is_valid(151));

    let last_instant = InputLeaseToken::acquire(3, "owner", 100, 50)
        .consume_action('W', 1, 150)
        .expect("a lease is still valid at the last millisecond of its window");
    assert_eq!(last_instant.executed_at_ms, 150);

    let too_early = InputLeaseToken::acquire(4, "owner", 100, 50).consume_action('W', 1, 99);
    assert!(
        too_early.is_err(),
        "a lease cannot be used before it was acquired"
    );
    let too_late = InputLeaseToken::acquire(5, "owner", 100, 50).consume_action('W', 1, 151);
    assert!(too_late.is_err());
}
