//! `steering`: Pure Pursuit local path tracking controller and Odometry Watchdog.
//! Translates high-level NavMesh waypoint paths into smooth, continuous kinematic
//! keyboard pulses (`W`, `A`, `D`, `S`) with deadband stabilization and automatic
//! unstuck recovery maneuvers.
//! Adheres strictly to `Facts ≠ Authority ≠ Actions`.

use crate::costmap::Vec2D;

/// 2D pose with orientation (position and heading angle).
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct Pose2D {
    pub x: f64,
    pub y: f64,
    /// Heading in radians, where 0 is along the positive X-axis (+X),
    /// π/2 is along positive Y (+Y, North), and angles increase counter-clockwise.
    pub yaw_rad: f64,
}

impl Pose2D {
    pub const fn new(x: f64, y: f64, yaw_rad: f64) -> Self {
        Self { x, y, yaw_rad }
    }

    pub fn from_degrees(x: f64, y: f64, yaw_deg: f64) -> Self {
        #[cfg(feature = "std")]
        let rad = yaw_deg.to_radians();
        #[cfg(not(feature = "std"))]
        let rad = yaw_deg * (core::f64::consts::PI / 180.0);
        Self { x, y, yaw_rad: rad }
    }

    pub fn to_vec2d(&self) -> Vec2D {
        Vec2D::new(self.x, self.y)
    }

    pub fn distance_to(&self, target: &Vec2D) -> f64 {
        let dx = target.x - self.x;
        let dy = target.y - self.y;
        let d2 = dx * dx + dy * dy;
        #[cfg(feature = "std")]
        {
            d2.sqrt()
        }
        #[cfg(not(feature = "std"))]
        {
            if d2 <= 0.0 {
                0.0
            } else {
                let mut x = d2;
                let mut y = 1.0;
                for _ in 0..10 {
                    x = (x + y) * 0.5;
                    y = d2 / x;
                }
                x
            }
        }
    }

    /// Computes absolute angle from character pose to target in radians.
    pub fn angle_to(&self, target: &Vec2D) -> f64 {
        let dx = target.x - self.x;
        let dy = target.y - self.y;
        #[cfg(feature = "std")]
        {
            dy.atan2(dx)
        }
        #[cfg(not(feature = "std"))]
        {
            // Simple rational polynomial approximation for atan2 if no_std
            let ax = if dx < 0.0 { -dx } else { dx };
            let ay = if dy < 0.0 { -dy } else { dy };
            let mut angle = if ax > ay {
                (ay / (ax + 1e-12)) * (core::f64::consts::PI / 4.0)
            } else {
                (core::f64::consts::PI / 2.0) - (ax / (ay + 1e-12)) * (core::f64::consts::PI / 4.0)
            };
            if dx < 0.0 {
                angle = core::f64::consts::PI - angle;
            }
            if dy < 0.0 {
                angle = -angle;
            }
            angle
        }
    }

    /// Computes relative angle α to target, normalized to [-π, π].
    /// Positive α indicates target is to the Left; negative α indicates target is to the Right.
    pub fn relative_angle_to(&self, target: &Vec2D) -> f64 {
        let target_angle = self.angle_to(target);
        let mut diff = target_angle - self.yaw_rad;
        let pi = core::f64::consts::PI;
        let two_pi = 2.0 * pi;

        while diff > pi {
            diff -= two_pi;
        }
        while diff < -pi {
            diff += two_pi;
        }
        diff
    }
}

/// Kinematic steering command output produced by the local path controller.
/// Conforms to `Facts ≠ Authority ≠ Actions` as an action proposal intent.
#[derive(Debug, Clone, PartialEq)]
pub struct SteeringCommand {
    /// Duration in milliseconds to pulse forward movement (`W`).
    pub forward_pulse_ms: u32,
    /// Duration in milliseconds to pulse left steering (`A`).
    pub turn_left_pulse_ms: u32,
    /// Duration in milliseconds to pulse right steering (`D`).
    pub turn_right_pulse_ms: u32,
    /// Duration in milliseconds to pulse reverse movement (`S`).
    pub reverse_pulse_ms: u32,
    /// Path curvature κ = 2*sin(α) / Ld.
    pub curvature: f64,
    /// Relative angle error α in radians.
    pub angle_error_rad: f64,
    /// True if the character is within arrival distance of the final waypoint.
    pub is_arrived: bool,
}

impl SteeringCommand {
    pub const fn idle() -> Self {
        Self {
            forward_pulse_ms: 0,
            turn_left_pulse_ms: 0,
            turn_right_pulse_ms: 0,
            reverse_pulse_ms: 0,
            curvature: 0.0,
            angle_error_rad: 0.0,
            is_arrived: false,
        }
    }

    pub const fn arrived() -> Self {
        Self {
            forward_pulse_ms: 0,
            turn_left_pulse_ms: 0,
            turn_right_pulse_ms: 0,
            reverse_pulse_ms: 0,
            curvature: 0.0,
            angle_error_rad: 0.0,
            is_arrived: true,
        }
    }
}

/// Pure Pursuit local steering controller.
/// Translates global path waypoints into continuous, curved keyboard driving pulses.
#[derive(Debug, Clone, PartialEq)]
pub struct PurePursuitController {
    /// Lookahead distance Ld (typically 20.0 to 50.0 units).
    pub lookahead_distance: f64,
    /// Default forward tick pulse duration in ms (typically 200-300ms).
    pub forward_cadence_ms: u32,
    /// Angular turn rate of character in radians per second (typically ~3.0 rad/s ≈ 172°/s).
    pub turn_rate_rad_per_sec: f64,
    /// Deadband tolerance in radians below which turn pulses are omitted to prevent chattering.
    pub heading_tolerance_rad: f64,
    /// Angle threshold above which character turns in place before driving forward.
    pub turn_in_place_threshold_rad: f64,
    /// Arrival distance threshold to declare destination reached (units).
    pub arrival_threshold: f64,
}

impl PurePursuitController {
    pub fn new(
        lookahead_distance: f64,
        turn_rate_rad_per_sec: f64,
        arrival_threshold: f64,
    ) -> Self {
        Self {
            lookahead_distance: if lookahead_distance <= 0.0 {
                30.0
            } else {
                lookahead_distance
            },
            forward_cadence_ms: 250,
            turn_rate_rad_per_sec: if turn_rate_rad_per_sec <= 0.0 {
                3.0
            } else {
                turn_rate_rad_per_sec
            },
            heading_tolerance_rad: 0.08,       // ~4.5 degrees deadband
            turn_in_place_threshold_rad: 0.85, // ~48.7 degrees
            arrival_threshold: if arrival_threshold <= 0.0 {
                10.0
            } else {
                arrival_threshold
            },
        }
    }

    pub fn with_forward_cadence(mut self, cadence_ms: u32) -> Self {
        self.forward_cadence_ms = cadence_ms;
        self
    }

    pub fn with_heading_tolerance(mut self, tol_rad: f64) -> Self {
        self.heading_tolerance_rad = tol_rad;
        self
    }

    pub fn with_turn_in_place_threshold(mut self, thresh_rad: f64) -> Self {
        self.turn_in_place_threshold_rad = thresh_rad;
        self
    }

    /// Selects the target lookahead point along the path from current pose.
    /// Finds the closest waypoint along the path and searches forward to prevent
    /// mistakenly locking onto already-traversed waypoints behind the character.
    pub fn select_lookahead_point(&self, pose: &Pose2D, path: &[Vec2D]) -> Option<Vec2D> {
        if path.is_empty() {
            return None;
        }

        // If at the end of the path and within arrival distance
        let final_wp = path.last()?;
        if pose.distance_to(final_wp) <= self.arrival_threshold {
            return Some(*final_wp);
        }

        // Find index of closest waypoint to current pose
        let mut closest_idx = 0;
        let mut min_d = f64::INFINITY;
        for (i, wp) in path.iter().enumerate() {
            let d = pose.distance_to(wp);
            if d < min_d {
                min_d = d;
                closest_idx = i;
            }
        }

        // Search FORWARD from closest waypoint for first waypoint at least lookahead_distance away
        for wp in &path[closest_idx..] {
            if pose.distance_to(wp) >= self.lookahead_distance {
                return Some(*wp);
            }
        }

        Some(*final_wp)
    }

    /// Computes steering command for current pose against global path waypoints.
    pub fn compute_steering(&self, pose: &Pose2D, path: &[Vec2D]) -> SteeringCommand {
        if path.is_empty() {
            return SteeringCommand::idle();
        }

        let final_wp = match path.last() {
            Some(wp) => wp,
            None => return SteeringCommand::idle(),
        };

        // Arrival check
        if pose.distance_to(final_wp) <= self.arrival_threshold {
            return SteeringCommand::arrived();
        }

        let target = match self.select_lookahead_point(pose, path) {
            Some(t) => t,
            None => return SteeringCommand::idle(),
        };

        let alpha = pose.relative_angle_to(&target);
        let dist = pose.distance_to(&target).max(1.0);

        #[cfg(feature = "std")]
        let sin_alpha = alpha.sin();
        #[cfg(not(feature = "std"))]
        let sin_alpha = {
            // Taylor expansion approximation for sin(alpha) in [-pi, pi]
            let a = alpha;
            let a3 = a * a * a;
            let a5 = a3 * a * a;
            a - (a3 / 6.0) + (a5 / 120.0)
        };

        let curvature = (2.0 * sin_alpha) / dist;

        let abs_alpha = if alpha < 0.0 { -alpha } else { alpha };

        // If orientation error is large, turn in place first
        if abs_alpha > self.turn_in_place_threshold_rad {
            let turn_ms = ((abs_alpha / self.turn_rate_rad_per_sec) * 1000.0) as u32;
            let clamped_turn_ms = turn_ms.min(500);

            if alpha > 0.0 {
                // Target is to the Left
                SteeringCommand {
                    forward_pulse_ms: 0,
                    turn_left_pulse_ms: clamped_turn_ms,
                    turn_right_pulse_ms: 0,
                    reverse_pulse_ms: 0,
                    curvature,
                    angle_error_rad: alpha,
                    is_arrived: false,
                }
            } else {
                // Target is to the Right
                SteeringCommand {
                    forward_pulse_ms: 0,
                    turn_left_pulse_ms: 0,
                    turn_right_pulse_ms: clamped_turn_ms,
                    reverse_pulse_ms: 0,
                    curvature,
                    angle_error_rad: alpha,
                    is_arrived: false,
                }
            }
        } else {
            // Smooth continuous driving: forward pulse + slight turn pulse
            let forward_ms = self.forward_cadence_ms;
            let mut turn_left_ms = 0;
            let mut turn_right_ms = 0;

            if abs_alpha > self.heading_tolerance_rad {
                let turn_ms = ((abs_alpha / self.turn_rate_rad_per_sec) * 1000.0) as u32;
                let clamped_turn_ms = turn_ms.min(forward_ms);

                if alpha > 0.0 {
                    turn_left_ms = clamped_turn_ms;
                } else {
                    turn_right_ms = clamped_turn_ms;
                }
            }

            SteeringCommand {
                forward_pulse_ms: forward_ms,
                turn_left_pulse_ms: turn_left_ms,
                turn_right_pulse_ms: turn_right_ms,
                reverse_pulse_ms: 0,
                curvature,
                angle_error_rad: alpha,
                is_arrived: false,
            }
        }
    }
}

/// Odometry Watchdog for detecting kinematic snags and physical obstacles.
/// Monitors position displacement over time during forward driving.
/// Triggers multi-stage unstuck recovery (Reverse -> Turn -> Replan) when stalled.
#[derive(Debug, Clone, PartialEq)]
pub struct OdometryWatchdog {
    /// Time threshold in seconds to declare a stall (typically 1.5s).
    pub stall_duration_threshold_s: f64,
    /// Minimum expected displacement in units over the stall window (typically 5.0 units).
    pub min_expected_displacement: f64,
    /// Timestamp when forward motion began.
    pub motion_start_time_s: Option<f64>,
    /// Pose when forward motion began.
    pub motion_start_pose: Option<Pose2D>,
    /// Latched stuck flag.
    pub is_stuck: bool,
    /// Step sequence in unstuck recovery: 0 = Idle, 1 = Reverse, 2 = Turn, 3 = Complete.
    pub unstuck_phase: usize,
}

impl OdometryWatchdog {
    pub fn new(stall_duration_threshold_s: f64, min_expected_displacement: f64) -> Self {
        Self {
            stall_duration_threshold_s: if stall_duration_threshold_s <= 0.0 {
                1.5
            } else {
                stall_duration_threshold_s
            },
            min_expected_displacement: if min_expected_displacement <= 0.0 {
                5.0
            } else {
                min_expected_displacement
            },
            motion_start_time_s: None,
            motion_start_pose: None,
            is_stuck: false,
            unstuck_phase: 0,
        }
    }

    /// Updates watchdog state with current observation.
    /// Returns true if character is declared stuck.
    pub fn update(
        &mut self,
        current_time_s: f64,
        current_pose: &Pose2D,
        is_driving_forward: bool,
    ) -> bool {
        if self.is_stuck {
            return true;
        }

        if !is_driving_forward {
            // Not attempting to move forward: reset tracking window
            self.motion_start_time_s = None;
            self.motion_start_pose = None;
            return false;
        }

        match (self.motion_start_time_s, self.motion_start_pose) {
            (None, _) | (_, None) => {
                // Initialize tracking window
                self.motion_start_time_s = Some(current_time_s);
                self.motion_start_pose = Some(*current_pose);
                false
            }
            (Some(start_t), Some(start_p)) => {
                let dt = current_time_s - start_t;
                if dt >= self.stall_duration_threshold_s {
                    let displacement = start_p.distance_to(&current_pose.to_vec2d());
                    if displacement < self.min_expected_displacement {
                        // Forward motion commanded for > threshold, but displacement < min: STUCK!
                        self.is_stuck = true;
                        self.unstuck_phase = 1;
                        true
                    } else {
                        // Moving properly: slide the window forward
                        self.motion_start_time_s = Some(current_time_s);
                        self.motion_start_pose = Some(*current_pose);
                        false
                    }
                } else {
                    false
                }
            }
        }
    }

    /// Progresses multi-stage unstuck recovery maneuver.
    /// Returns SteeringCommand for each step:
    /// - Phase 1: Reverse pulse (`S` for 500ms)
    /// - Phase 2: Lateral turn pulse (`A` for 350ms)
    /// - Phase 3: Clears stuck flag and yields idle to signal path replan.
    pub fn step_unstuck_maneuver(&mut self) -> Option<SteeringCommand> {
        match self.unstuck_phase {
            1 => {
                self.unstuck_phase = 2;
                Some(SteeringCommand {
                    forward_pulse_ms: 0,
                    turn_left_pulse_ms: 0,
                    turn_right_pulse_ms: 0,
                    reverse_pulse_ms: 500, // Back away from obstacle
                    curvature: 0.0,
                    angle_error_rad: 0.0,
                    is_arrived: false,
                })
            }
            2 => {
                self.unstuck_phase = 3;
                Some(SteeringCommand {
                    forward_pulse_ms: 0,
                    turn_left_pulse_ms: 350, // Rotate away from contact normal
                    turn_right_pulse_ms: 0,
                    reverse_pulse_ms: 0,
                    curvature: 0.0,
                    angle_error_rad: 0.0,
                    is_arrived: false,
                })
            }
            _ => {
                // Completed unstuck sequence: clear flags
                self.clear();
                None
            }
        }
    }

    /// Clears stuck latch and resets watchdog state.
    pub fn clear(&mut self) {
        self.is_stuck = false;
        self.unstuck_phase = 0;
        self.motion_start_time_s = None;
        self.motion_start_pose = None;
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_pose2d_relative_angle_calculations() {
        // Facing +X (yaw = 0.0)
        let pose_east = Pose2D::new(0.0, 0.0, 0.0);

        // Target directly ahead at (50, 0): relative angle is 0.0
        assert!(pose_east.relative_angle_to(&Vec2D::new(50.0, 0.0)).abs() < 1e-6);

        // Target to the Left (+Y, North) at (0, 50): relative angle is +π/2
        let rel_left = pose_east.relative_angle_to(&Vec2D::new(0.0, 50.0));
        assert!((rel_left - core::f64::consts::PI / 2.0).abs() < 1e-4);

        // Target to the Right (-Y, South) at (0, -50): relative angle is -π/2
        let rel_right = pose_east.relative_angle_to(&Vec2D::new(0.0, -50.0));
        assert!((rel_right - (-core::f64::consts::PI / 2.0)).abs() < 1e-4);
    }

    #[test]
    fn test_pure_pursuit_arrival_and_idle() {
        let controller = PurePursuitController::new(30.0, 3.0, 10.0);
        let pose = Pose2D::new(95.0, 0.0, 0.0);
        let path = alloc::vec![Vec2D::new(0.0, 0.0), Vec2D::new(100.0, 0.0)];

        // Character is at x=95, final waypoint is at x=100 (dist=5.0 <= arrival_threshold 10.0)
        let cmd = controller.compute_steering(&pose, &path);
        assert!(cmd.is_arrived);
        assert_eq!(cmd.forward_pulse_ms, 0);
    }

    #[test]
    fn test_pure_pursuit_continuous_curved_steering() {
        let controller = PurePursuitController::new(30.0, 3.0, 10.0);
        // Facing East (yaw = 0.0)
        let pose = Pose2D::new(0.0, 0.0, 0.0);

        // Waypoint slightly to the left at (50, 15) -> relative angle ~0.29 rad (< turn_in_place_threshold 0.85)
        let path = alloc::vec![Vec2D::new(50.0, 15.0)];
        let cmd = controller.compute_steering(&pose, &path);

        assert!(!cmd.is_arrived);
        // Continuous driving: commands forward motion while steering left
        assert_eq!(cmd.forward_pulse_ms, 250);
        assert!(cmd.turn_left_pulse_ms > 0, "Must pulse Left steering");
        assert_eq!(cmd.turn_right_pulse_ms, 0);
    }

    #[test]
    fn test_pure_pursuit_turn_in_place_for_sharp_angles() {
        let controller = PurePursuitController::new(30.0, 3.0, 10.0);
        // Facing East (yaw = 0.0)
        let pose = Pose2D::new(0.0, 0.0, 0.0);

        // Waypoint sharply behind at (-50, 50) -> relative angle > turn_in_place_threshold
        let path = alloc::vec![Vec2D::new(-50.0, 50.0)];
        let cmd = controller.compute_steering(&pose, &path);

        assert!(!cmd.is_arrived);
        // Turns in place: NO forward movement until oriented
        assert_eq!(cmd.forward_pulse_ms, 0);
        assert!(cmd.turn_left_pulse_ms > 0);
    }

    #[test]
    fn test_odometry_watchdog_stall_detection_and_recovery() {
        let mut watchdog = OdometryWatchdog::new(1.5, 5.0);
        let pose_start = Pose2D::new(10.0, 10.0, 0.0);

        // t = 0s: start forward driving
        assert!(!watchdog.update(0.0, &pose_start, true));

        // t = 1.0s: character hasn't moved (snagged on geometry), but dt < 1.5s -> not yet stuck
        assert!(!watchdog.update(1.0, &pose_start, true));

        // t = 1.6s: dt = 1.6s >= 1.5s, displacement = 0.0 < 5.0 -> STUCK DECLARED!
        assert!(watchdog.update(1.6, &pose_start, true));
        assert!(watchdog.is_stuck);

        // Step 1 of unstuck: Reverse pulse
        let step1 = watchdog.step_unstuck_maneuver().expect("Step 1 must exist");
        assert_eq!(step1.reverse_pulse_ms, 500);

        // Step 2 of unstuck: Turn pulse
        let step2 = watchdog.step_unstuck_maneuver().expect("Step 2 must exist");
        assert_eq!(step2.turn_left_pulse_ms, 350);

        // Step 3: Complete unstuck maneuver -> returns None, watchdog cleared
        let step3 = watchdog.step_unstuck_maneuver();
        assert_eq!(step3, None);
        assert!(!watchdog.is_stuck);
    }
}
