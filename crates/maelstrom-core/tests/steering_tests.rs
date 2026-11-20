use maelstrom_core::costmap::Vec2D;
use maelstrom_core::steering::{OdometryWatchdog, Pose2D, PurePursuitController};

#[test]
fn test_integration_smooth_pure_pursuit_tracks_waypoint_path() {
    let controller = PurePursuitController::new(30.0, 3.0, 10.0)
        .with_forward_cadence(250)
        .with_heading_tolerance(0.05);

    let path = vec![
        Vec2D::new(0.0, 0.0),
        Vec2D::new(50.0, 0.0),
        Vec2D::new(100.0, 30.0),
        Vec2D::new(150.0, 30.0),
    ];

    // 1. Initial pose at origin facing East (+X, yaw = 0.0)
    let pose1 = Pose2D::new(0.0, 0.0, 0.0);
    let cmd1 = controller.compute_steering(&pose1, &path);
    assert!(!cmd1.is_arrived);
    assert_eq!(cmd1.forward_pulse_ms, 250);
    // Heading directly along waypoint: turn pulse within deadband
    assert_eq!(cmd1.turn_left_pulse_ms, 0);
    assert_eq!(cmd1.turn_right_pulse_ms, 0);

    // 2. Character advances to (50.0, 0.0) facing East.
    // Next target waypoint is at (100.0, 30.0) (bearing left / North-East).
    let pose2 = Pose2D::new(50.0, 0.0, 0.0);
    let cmd2 = controller.compute_steering(&pose2, &path);
    assert!(!cmd2.is_arrived);
    assert_eq!(cmd2.forward_pulse_ms, 250);
    // Controller commands smooth curved steering: hold W while pulsing A (Left)!
    assert!(
        cmd2.turn_left_pulse_ms > 0 && cmd2.turn_left_pulse_ms <= 250,
        "Turn pulse must be bounded by forward cadence for continuous curvature"
    );
    assert_eq!(cmd2.turn_right_pulse_ms, 0);
    assert!(
        cmd2.curvature > 0.0,
        "Curvature must be positive for left turn"
    );

    // 3. Character has turned and is approaching final destination at (145.0, 30.0)
    let pose3 = Pose2D::new(145.0, 30.0, 0.0);
    let cmd3 = controller.compute_steering(&pose3, &path);
    // Distance to final waypoint (150, 30) is 5.0 <= arrival_threshold (10.0)
    assert!(cmd3.is_arrived, "Must signal arrival at final waypoint");
    assert_eq!(
        cmd3.forward_pulse_ms, 0,
        "Forward motion must halt upon arrival"
    );
}

#[test]
fn test_integration_odometry_watchdog_detects_obstacle_snag_and_executes_recovery() {
    let mut watchdog = OdometryWatchdog::new(1.5, 5.0);

    // Initial pose at (20, 20)
    let mut pose = Pose2D::new(20.0, 20.0, 0.0);
    let mut current_time = 0.0;

    // Phase 1: Healthy movement for 2 seconds
    // Character moves 10 units every 0.5s
    for _ in 0..4 {
        assert!(!watchdog.update(current_time, &pose, true));
        current_time += 0.5;
        pose.x += 10.0;
    }
    assert!(
        !watchdog.is_stuck,
        "Healthy moving character must not trigger watchdog"
    );

    // Phase 2: Character hits obstacle at (60, 20) and snags
    // Position stops updating while forward motion is commanded
    assert!(!watchdog.update(current_time, &pose, true)); // t = 2.0s
    current_time += 1.0;
    assert!(!watchdog.update(current_time, &pose, true)); // t = 3.0s (healthy window slide to t=3.0)

    // After snagging at (60, 20), 1.6s of forward drive elapses with zero displacement
    current_time += 1.6; // t = 4.6s (1.6s elapsed with displacement 0.0 < 5.0)
    let triggered = watchdog.update(current_time, &pose, true);
    assert!(
        triggered,
        "Watchdog must declare stuck after 1.5s stall window"
    );
    assert!(watchdog.is_stuck);

    // Phase 3: Execute multi-stage unstuck recovery maneuver
    // Step 1: Reverse pulse (S)
    let step1 = watchdog.step_unstuck_maneuver().expect("Must have step 1");
    assert_eq!(step1.reverse_pulse_ms, 500);
    assert_eq!(step1.forward_pulse_ms, 0);

    // Step 2: Lateral yaw rotation (A)
    let step2 = watchdog.step_unstuck_maneuver().expect("Must have step 2");
    assert_eq!(step2.turn_left_pulse_ms, 350);
    assert_eq!(step2.reverse_pulse_ms, 0);

    // Step 3: Complete maneuver -> resets watchdog for path replan
    let step3 = watchdog.step_unstuck_maneuver();
    assert_eq!(step3, None);
    assert!(!watchdog.is_stuck);
}

#[test]
fn test_integration_stationary_player_never_triggers_watchdog() {
    let mut watchdog = OdometryWatchdog::new(1.5, 5.0);
    let pose = Pose2D::new(100.0, 100.0, 0.0);

    // Idle for 10 seconds: is_driving_forward is false
    for t in 0..20 {
        let current_time = (t as f64) * 0.5;
        let is_stuck = watchdog.update(current_time, &pose, false);
        assert!(
            !is_stuck,
            "Stationary idle player must never trigger watchdog"
        );
    }
    assert!(!watchdog.is_stuck);
}
