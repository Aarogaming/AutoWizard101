//! Integration tests for Acoustic Fishing Reflex Engine (`fishing_domain`).

use maelstrom_core::audio_cue::{AudioCueEvent, AudioCueType};
use maelstrom_core::fishing_domain::{
    CastPlanner, CaughtFish, FishEntity, FishSchool, FishingOutcome, FishingProposal, FishingSpell,
    FishingState, FishingStateMachine, PondSurface, Vector3D,
};

#[test]
fn test_energy_preconditions_and_exhaustion_guard() {
    let pond = PondSurface::new(Vector3D::new(0.0, 0.0, 0.0), 100.0, -5.0);
    let mut fsm = FishingStateMachine::new(2, 50, pond.clone());

    let lure = FishingSpell::minor_lure(0, FishSchool::Ice); // Costs 3 energy
    assert!(!fsm.can_afford(&lure));

    // Attempting to fish with insufficient energy should halt to EnergyExhausted
    let proposal = fsm.start_fishing(lure.clone()).unwrap();
    match proposal {
        FishingProposal::HaltDueToExhaustion { current_energy } => {
            assert_eq!(current_energy, 2);
        }
        _ => panic!("Expected HaltDueToExhaustion proposal"),
    }
    assert!(matches!(
        fsm.state,
        FishingState::EnergyExhausted {
            current_energy: 2,
            required_energy: 3
        }
    ));

    // Refill energy and verify normal start
    fsm.current_energy = 10;
    fsm.state = FishingState::Idle;
    let prop2 = fsm.start_fishing(lure).unwrap();
    assert!(matches!(
        prop2,
        FishingProposal::SelectLureSpell { slot_index: 0, .. }
    ));
}

#[test]
fn test_lead_cast_kinematics_and_pond_clamping() {
    let pond = PondSurface::new(Vector3D::new(0.0, 0.0, 0.0), 100.0, -5.0);

    // Swimming fish moving along positive X at 15 units/s
    let fish = FishEntity::new(
        1001,
        Vector3D::new(20.0, 10.0, -5.0),
        Vector3D::new(15.0, 0.0, 0.0),
    )
    .with_school(FishSchool::Fire);

    // Ballistic flight time 0.65s, lead distance 25.0
    let target = CastPlanner::compute_lead_cast(&fish, &pond, 0.65, 25.0);

    // Expected X: 20.0 + (15.0 * 0.65) + 25.0 = 20.0 + 9.75 + 25.0 = 54.75
    assert!((target.x - 54.75).abs() < 1e-4);
    assert!((target.y - 10.0).abs() < 1e-4);
    assert_eq!(target.z, -5.0, "Target Z must clamp to water elevation");

    // Test extreme fish heading that would overshoot pond perimeter
    let fast_fish = FishEntity::new(
        1002,
        Vector3D::new(80.0, 0.0, -5.0),
        Vector3D::new(50.0, 0.0, 0.0),
    );
    let clamped_target = CastPlanner::compute_lead_cast(&fast_fish, &pond, 0.65, 25.0);
    // Distance from center (0, 0) should be clamped to 95.0 (95% margin)
    assert!(clamped_target.x <= 95.0 + 1e-4);
    assert!(pond.contains_point_2d(&clamped_target));
}

#[test]
fn test_acoustic_bobber_splash_reflex_trigger_sub_150ms() {
    let pond = PondSurface::new(Vector3D::new(0.0, 0.0, 0.0), 100.0, -5.0);
    let mut fsm = FishingStateMachine::new(20, 50, pond);

    let lure = FishingSpell::regular_lure(1, FishSchool::Storm);
    let _ = fsm.start_fishing(lure).unwrap();
    let _ = fsm.dispatch_cast(None, 10.0).unwrap();
    fsm.confirm_bobber_landed(10.65).unwrap();

    // Cue emitted at 12.000s; processed by state machine at 12.042s (42ms reflex)
    let cue = AudioCueEvent {
        cue_type: AudioCueType::FishingBobberSplash,
        timestamp_s: 12.000,
        confidence: 0.94,
        in_band_power: 0.045,
        total_rms: 0.060,
        snr_ratio: 0.75,
    };

    let proposal = fsm.handle_audio_cue(&cue, 12.042);
    assert!(proposal.is_some());

    match proposal.unwrap() {
        FishingProposal::ReflexStrike {
            key,
            latency_ms,
            is_sub_150ms,
        } => {
            assert_eq!(key, "Spacebar");
            assert!((latency_ms - 42.0).abs() < 1.0);
            assert!(is_sub_150ms, "Must satisfy <= 150ms latency gate");
        }
        _ => panic!("Expected ReflexStrike proposal"),
    }

    assert!(matches!(
        fsm.state,
        FishingState::Striking {
            was_within_latency_gate: true,
            ..
        }
    ));
}

#[test]
fn test_acoustic_bobber_splash_reflex_late_warning() {
    let pond = PondSurface::new(Vector3D::new(0.0, 0.0, 0.0), 100.0, -5.0);
    let mut fsm = FishingStateMachine::new(20, 50, pond);

    let lure = FishingSpell::regular_lure(1, FishSchool::Storm);
    let _ = fsm.start_fishing(lure).unwrap();
    let _ = fsm.dispatch_cast(None, 10.0).unwrap();
    fsm.confirm_bobber_landed(10.65).unwrap();

    // Cue emitted at 12.000s; processed at 12.210s (210ms reflex > 150ms gate)
    let cue = AudioCueEvent {
        cue_type: AudioCueType::FishingBobberSplash,
        timestamp_s: 12.000,
        confidence: 0.88,
        in_band_power: 0.035,
        total_rms: 0.050,
        snr_ratio: 0.70,
    };

    let proposal = fsm.handle_audio_cue(&cue, 12.210).unwrap();
    match proposal {
        FishingProposal::ReflexStrike {
            latency_ms,
            is_sub_150ms,
            ..
        } => {
            assert!((latency_ms - 210.0).abs() < 1.0);
            assert!(!is_sub_150ms, "Latency > 150ms must be flagged");
        }
        _ => panic!("Expected ReflexStrike proposal"),
    }

    assert!(matches!(
        fsm.state,
        FishingState::Striking {
            was_within_latency_gate: false,
            ..
        }
    ));
}

#[test]
fn test_unrelated_audio_cues_are_safely_ignored() {
    let pond = PondSurface::new(Vector3D::new(0.0, 0.0, 0.0), 100.0, -5.0);
    let mut fsm = FishingStateMachine::new(20, 50, pond);

    let lure = FishingSpell::minor_lure(0, FishSchool::Death);
    let _ = fsm.start_fishing(lure).unwrap();
    let _ = fsm.dispatch_cast(None, 1.0).unwrap();
    fsm.confirm_bobber_landed(1.65).unwrap();

    // Fanfare cue arrives while waiting for fish bite
    let fanfare = AudioCueEvent {
        cue_type: AudioCueType::CombatInitiationFanfare,
        timestamp_s: 2.5,
        confidence: 0.95,
        in_band_power: 0.05,
        total_rms: 0.06,
        snr_ratio: 0.83,
    };

    let prop = fsm.handle_audio_cue(&fanfare, 2.51);
    assert!(prop.is_none(), "Unrelated audio cue must yield None");
    assert!(matches!(fsm.state, FishingState::WaitingForBite { .. }));
}

#[test]
fn test_cast_timeout_watchdog() {
    let pond = PondSurface::new(Vector3D::new(0.0, 0.0, 0.0), 100.0, -5.0);
    let mut fsm = FishingStateMachine::new(20, 50, pond).with_cast_timeout_s(25.0);

    let lure = FishingSpell::minor_lure(0, FishSchool::Life);
    let _ = fsm.start_fishing(lure).unwrap();
    let _ = fsm.dispatch_cast(None, 10.0).unwrap();
    fsm.confirm_bobber_landed(10.65).unwrap();

    // At 30.0s elapsed is 19.35s < 25s timeout
    let outcome1 = fsm.update_timer(30.0);
    assert!(outcome1.is_none());

    // At 36.0s elapsed is 25.35s >= 25s timeout
    let outcome2 = fsm.update_timer(36.0);
    assert_eq!(outcome2, Some(FishingOutcome::CastTimedOut));
    assert_eq!(fsm.stats.timeouts, 1);
    assert!(matches!(
        fsm.state,
        FishingState::CatchResolved {
            outcome: FishingOutcome::CastTimedOut
        }
    ));
}

#[test]
fn test_full_fishing_lifecycle_and_telemetry() {
    let pond = PondSurface::new(Vector3D::new(0.0, 0.0, 0.0), 100.0, -5.0);
    let mut fsm = FishingStateMachine::new(30, 50, pond);

    // 1. Start fishing with Minor Fire Lure (cost 3)
    let lure = FishingSpell::minor_lure(0, FishSchool::Fire);
    let prop_select = fsm.start_fishing(lure).unwrap();
    assert!(matches!(
        prop_select,
        FishingProposal::SelectLureSpell { slot_index: 0, .. }
    ));

    // 2. Dispatch cast
    let fish = FishEntity::new(
        2001,
        Vector3D::new(10.0, 10.0, -5.0),
        Vector3D::new(5.0, 0.0, 0.0),
    );
    let prop_cast = fsm.dispatch_cast(Some(&fish), 1.0).unwrap();
    assert!(matches!(prop_cast, FishingProposal::CastLure { .. }));
    assert_eq!(fsm.current_energy, 27, "3 energy should be deducted");

    // 3. Confirm bobber landing
    fsm.confirm_bobber_landed(1.65).unwrap();

    // 4. Record 2 visual nibbles
    assert_eq!(fsm.record_nibble().unwrap(), 1);
    assert_eq!(fsm.record_nibble().unwrap(), 2);

    // 5. Acoustic bite detection splash
    let splash = AudioCueEvent {
        cue_type: AudioCueType::FishingBobberSplash,
        timestamp_s: 4.000,
        confidence: 0.96,
        in_band_power: 0.048,
        total_rms: 0.062,
        snr_ratio: 0.77,
    };
    let prop_strike = fsm.handle_audio_cue(&splash, 4.050).unwrap();
    assert!(matches!(
        prop_strike,
        FishingProposal::ReflexStrike {
            is_sub_150ms: true,
            ..
        }
    ));

    // 6. Confirm strike dispatched
    fsm.confirm_strike_dispatched(4.055).unwrap();
    assert!(matches!(fsm.state, FishingState::ReelingIn { .. }));

    // 7. Resolve catch with CaughtFish
    let caught = CaughtFish::new("Firecuda", FishSchool::Fire, 1, 36.5, 300);
    assert!(caught.is_whopper);
    assert!(!caught.is_small_fry);

    let prop_dismiss = fsm.resolve_catch(FishingOutcome::FishCaught(caught));
    assert!(matches!(prop_dismiss, FishingProposal::DismissRewardPopup));

    // 8. Verify telemetry
    assert_eq!(fsm.stats.casts_attempted, 1);
    assert_eq!(fsm.stats.fish_caught, 1);
    assert_eq!(fsm.stats.chests_caught, 0);
    assert_eq!(fsm.stats.total_energy_spent, 3);
    assert_eq!(fsm.stats.total_xp_gained, 300);
    assert!((fsm.stats.average_reflex_latency_ms() - 50.0).abs() < 1e-4);
    assert!((fsm.stats.catch_success_rate() - 1.0).abs() < 1e-4);

    // 9. Reset to idle
    fsm.reset_to_idle();
    assert_eq!(fsm.state, FishingState::Idle);
}

#[test]
fn test_chest_caught_telemetry() {
    let pond = PondSurface::new(Vector3D::new(0.0, 0.0, 0.0), 100.0, -5.0);
    let mut fsm = FishingStateMachine::new(30, 50, pond);

    let lure = FishingSpell::regular_lure(1, FishSchool::Ice);
    let _ = fsm.start_fishing(lure).unwrap();
    let _ = fsm.dispatch_cast(None, 1.0).unwrap();
    fsm.confirm_bobber_landed(1.65).unwrap();

    let splash = AudioCueEvent {
        cue_type: AudioCueType::FishingBobberSplash,
        timestamp_s: 3.0,
        confidence: 0.92,
        in_band_power: 0.04,
        total_rms: 0.05,
        snr_ratio: 0.8,
    };
    let _ = fsm.handle_audio_cue(&splash, 3.08).unwrap();
    fsm.confirm_strike_dispatched(3.09).unwrap();

    let outcome = FishingOutcome::ChestCaught {
        chest_id: String::from("CHEST_001"),
        loot_summary: String::from("500 Gold, 2 Frost Flowers"),
    };
    let _ = fsm.resolve_catch(outcome);

    assert_eq!(fsm.stats.chests_caught, 1);
    assert_eq!(fsm.stats.fish_caught, 0);
    assert_eq!(fsm.stats.catch_success_rate(), 1.0);
}
