use maelstrom_core::audio_cue::{AudioCueDetector, AudioCueType};
use maelstrom_core::behavior_tree::{build_w101_canonical_tree, BtNode, BtStatus, W101Blackboard};

#[test]
fn test_integration_fishing_splash_in_ambient_river_noise() {
    let mut detector = AudioCueDetector::new(44100.0);
    let sample_rate = 44100.0;
    let num_samples = 2048; // ~46ms buffer

    // 1. Buffer with only ambient river rumble (low frequency 80 Hz + 180 Hz)
    let mut ambient_river = Vec::with_capacity(num_samples);
    for i in 0..num_samples {
        let t = (i as f64) / sample_rate;
        let r1 = (2.0 * core::f64::consts::PI * 80.0 * t).sin();
        let r2 = (2.0 * core::f64::consts::PI * 180.0 * t).sin();
        ambient_river.push(((r1 + r2) * 0.15) as f32);
    }

    let ambient_events = detector.process_buffer(&ambient_river, 1.0);
    assert!(
        ambient_events.is_empty(),
        "Ambient low-frequency river noise must not trigger fishing splash"
    );

    // 2. Buffer where fish takes bobber: inject splash burst (1800 Hz + 2600 Hz) over ambient
    let mut splash_stream = ambient_river.clone();
    for (i, sample) in splash_stream.iter_mut().enumerate().take(num_samples) {
        let t = (i as f64) / sample_rate;
        let s1 = (2.0 * core::f64::consts::PI * 1800.0 * t).sin();
        let s2 = (2.0 * core::f64::consts::PI * 2600.0 * t).sin();
        *sample += ((s1 + s2) * 0.35) as f32;
    }

    let splash_events = detector.process_buffer(&splash_stream, 2.5);
    assert_eq!(splash_events.len(), 1);
    assert_eq!(
        splash_events[0].cue_type,
        AudioCueType::FishingBobberSplash,
        "Acoustic splash signature must be detected above ambient river noise"
    );
    assert!(splash_events[0].snr_ratio > 0.22);

    // 3. Connect event to Behavior Tree Blackboard
    let mut bb = W101Blackboard::new();
    splash_events[0].update_blackboard(&mut bb);
    assert_eq!(
        bb.proposal_channel.as_deref(),
        Some("PROPOSAL:ReelInFish"),
        "Splash event must emit reel-in proposal intent"
    );
}

#[test]
fn test_integration_combat_fanfare_preempts_behavior_tree() {
    let mut detector = AudioCueDetector::new(44100.0);
    let mut tree = build_w101_canonical_tree();
    let mut bb = W101Blackboard::new();

    // 1. Initial State: Character is questing and navigating
    bb.active_goal = Some(String::from("Quest_GolemCourt"));
    bb.active_waypoints = vec![(50.0, 50.0, 0.0)];

    let st1 = tree.tick(&mut bb);
    assert_eq!(st1, BtStatus::Running);
    assert_eq!(
        bb.proposal_channel.as_deref(),
        Some("PROPOSAL:StepWaypoint")
    );

    // 2. Character encounters mob: audio stream captures battle circle fanfare (520 Hz + 680 Hz brass chord)
    let sample_rate = 44100.0;
    let num_samples = 1500;
    let mut fanfare_buffer = Vec::with_capacity(num_samples);
    for i in 0..num_samples {
        let t = (i as f64) / sample_rate;
        let chord1 = (2.0 * core::f64::consts::PI * 520.0 * t).sin();
        let chord2 = (2.0 * core::f64::consts::PI * 680.0 * t).sin();
        fanfare_buffer.push(((chord1 + chord2) * 0.3) as f32);
    }

    let events = detector.process_buffer(&fanfare_buffer, 14.2);
    assert_eq!(events.len(), 1);
    assert_eq!(events[0].cue_type, AudioCueType::CombatInitiationFanfare);

    // 3. Update blackboard with zero memory inspection / zero OCR!
    events[0].update_blackboard(&mut bb);
    assert!(
        bb.in_combat,
        "Combat fanfare audio cue must flip in_combat to true"
    );

    // 4. Next tick of Behavior Tree: Priority Selector immediately pre-empts navigation!
    let st2 = tree.tick(&mut bb);
    assert_eq!(st2, BtStatus::Running);
    assert_eq!(
        bb.proposal_channel.as_deref(),
        Some("PROPOSAL:CombatTurn"),
        "Combat branch must preempt Navigation branch on the very next tick"
    );
}

#[test]
fn test_integration_turn_timer_warning_chime_detection() {
    let mut detector = AudioCueDetector::new(44100.0);
    let sample_rate = 44100.0;
    let num_samples = 1024;

    // High frequency bell chime (3400 Hz)
    let mut chime = Vec::with_capacity(num_samples);
    for i in 0..num_samples {
        let t = (i as f64) / sample_rate;
        let s = (2.0 * core::f64::consts::PI * 3400.0 * t).sin();
        chime.push((s * 0.3) as f32);
    }

    let events = detector.process_buffer(&chime, 25.0);
    assert_eq!(events.len(), 1);
    assert_eq!(events[0].cue_type, AudioCueType::TurnTimerWarningChime);
    assert!(events[0].snr_ratio > 0.30);

    let mut bb = W101Blackboard::new();
    events[0].update_blackboard(&mut bb);
    assert!(
        bb.status_log
            .iter()
            .any(|msg| msg.contains("TurnTimerWarningChime")),
        "Turn timer chime must be recorded in blackboard status log"
    );
}
