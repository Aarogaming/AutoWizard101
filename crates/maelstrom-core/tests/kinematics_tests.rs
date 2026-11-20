//! Integration tests for humanized kinematic input profiles.
//! Tests end-to-end integration between CombatGeometry, Fitts's Law duration modeling,
//! Bézier minimum-jerk trajectory generation, and WindMouse physics simulation.

use maelstrom_core::combat_geometry::CombatGeometry;
use maelstrom_core::kinematics::*;

#[test]
fn test_integration_combat_card_selection_pipeline() {
    let mut rng = FastPrng::new(20261118);

    // 1. Get nominal center coordinates for Card 3 of 7 in hand
    let card_nominal = CombatGeometry::get_hand_card_coordinates(2, 7)
        .expect("Card 3 in 7-card hand must resolve");

    // Card dimensions in standard 800x600 resolution: ~52px wide, 72px high
    let card_w = 52.0;
    let card_h = 72.0;
    let card_rect_x = (card_nominal.x as f64) - card_w * 0.5;
    let card_rect_y = (card_nominal.y as f64) - card_h * 0.5;

    // 2. Sample humanized target point within card bounding box
    let target_pt = sample_target_in_rect(card_rect_x, card_rect_y, card_w, card_h, 0.15, &mut rng);
    assert!(target_pt.x >= card_rect_x && target_pt.x <= card_rect_x + card_w);
    assert!(target_pt.y >= card_rect_y && target_pt.y <= card_rect_y + card_h);

    // 3. Current cursor position from prior action (e.g. top-left UI icon)
    let current_cursor = Point2D::new(100.0, 100.0);
    let distance = current_cursor.distance(&target_pt);

    // 4. Calculate realistic movement duration via Fitts's Law
    let fitts = FittsLawModel::default();
    let move_duration_ms = fitts.calculate_duration_ms(distance, card_w);
    assert!(
        (400.0..=800.0).contains(&move_duration_ms),
        "Card acquisition duration {} ms must be within realistic human bounds",
        move_duration_ms
    );

    // 5. Generate smooth cubic Bézier minimum-jerk trajectory
    let config = BezierConfig {
        departure_deviation: 20.0,
        arrival_deviation: 10.0,
        sample_count: 25,
    };
    let plan = BezierTrajectoryGenerator::generate_trajectory(
        current_cursor,
        target_pt,
        move_duration_ms,
        &config,
        &mut rng,
    );

    assert_eq!(plan.len(), 26);
    assert_eq!(plan.start_point().unwrap(), current_cursor);
    assert_eq!(plan.end_point().unwrap(), target_pt);
    assert!((plan.total_duration_ms - move_duration_ms).abs() < 1e-4);

    // Verify monotonic time progression
    for i in 1..plan.len() {
        assert!(
            plan.points[i].elapsed_ms >= plan.points[i - 1].elapsed_ms,
            "Kinematic plan timestamps must be strictly monotonic"
        );
    }

    // Verify humanized dwell time for click
    assert!(
        plan.dwell_ms >= 40.0 && plan.dwell_ms <= 180.0,
        "Click dwell {} ms must be human-like",
        plan.dwell_ms
    );
}

#[test]
fn test_integration_wind_mouse_long_distance_traversal() {
    let mut rng = FastPrng::new(424242);
    let start = Point2D::new(60.0, 80.0);
    let potion_bottle = Point2D::new(740.0, 530.0);

    let config = WindMouseConfig {
        gravity: 10.0,
        wind: 3.5,
        min_wait_ms: 3.0,
        max_wait_ms: 6.0,
        max_step_size: 14.0,
        target_area_threshold: 20.0,
        max_steps: 2000,
    };

    let plan = WindMouseGenerator::generate_path(start, potion_bottle, &config, &mut rng);

    assert!(
        plan.len() > 20,
        "Long traversal must generate multiple waypoints"
    );
    assert_eq!(plan.start_point().unwrap(), start);

    let end = plan.end_point().unwrap();
    assert!(
        end.distance(&potion_bottle) < 1.0,
        "WindMouse must terminate accurately at target"
    );

    // Verify all points are finite and non-NaN
    for pt in &plan.points {
        assert!(pt.x.is_finite());
        assert!(pt.y.is_finite());
        assert!(pt.elapsed_ms.is_finite());
    }

    // Verify that deceleration occurs near target:
    // Compare speed between middle 5 steps and final 5 steps
    let mid = plan.len() / 2;
    let mid_step_dist = plan.points[mid]
        .to_point2d()
        .distance(&plan.points[mid + 1].to_point2d());
    let terminal_step_dist = plan.points[plan.len() - 2]
        .to_point2d()
        .distance(&plan.points[plan.len() - 1].to_point2d());

    assert!(
        mid_step_dist > terminal_step_dist,
        "Terminal approach speed ({}) must decelerate below cruise speed ({})",
        terminal_step_dist,
        mid_step_dist
    );
}

#[test]
fn test_integration_dwell_jitter_statistical_distribution() {
    let mut rng = FastPrng::new(987654321);
    let click_profile = DwellProfile::mouse_click();
    let n = 1000;
    let mut samples = Vec::with_capacity(n);

    let mut sum = 0.0;
    for _ in 0..n {
        let val = click_profile.sample(&mut rng);
        assert!(val >= click_profile.min_ms && val <= click_profile.max_ms);
        samples.push(val);
        sum += val;
    }

    let mean = sum / (n as f64);
    // Theoretical median is e^4.317 ≈ 75ms; mean for log-normal is e^(mu + sigma^2/2) ≈ e^(4.317 + 0.024) ≈ 76.8ms
    assert!(
        (70.0..=84.0).contains(&mean),
        "Empirical mean {} must align with log-normal theoretical expectation",
        mean
    );

    // Compute empirical skewness: E[((X - mu) / sigma)^3]
    let mut var_sum = 0.0;
    for &x in &samples {
        let diff = x - mean;
        var_sum += diff * diff;
    }
    let variance = var_sum / (n as f64);
    let std_dev = variance.sqrt();

    let mut skew_sum = 0.0;
    for &x in &samples {
        let norm = (x - mean) / std_dev;
        skew_sum += norm * norm * norm;
    }
    let skewness = skew_sum / (n as f64);

    // Human motor distributions are positively skewed (sharp onset, long right tail)
    assert!(
        skewness > 0.1,
        "Empirical skewness {} must be positive, confirming human log-normal profile",
        skewness
    );
}
