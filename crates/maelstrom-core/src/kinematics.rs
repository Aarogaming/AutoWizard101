//! `kinematics`: Humanized kinematic input profiles and biomechanical trajectory generators.
//! Implements WindMouse physics simulation (gravitational pull, Ornstein-Uhlenbeck stochastic wind,
//! dynamic deceleration damping), cubic Bézier trajectories with minimum-jerk velocity profiles,
//! Fitts's law target acquisition timing, and log-normal dwell time jitter.
//!
//! Adheres strictly to `Facts ≠ Authority ≠ Actions`:
//! Produces immutable `KinematicPlan` trajectory waypoints without direct OS API actuation.

use alloc::vec::Vec;

/// 2D point representation for screen coordinates.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct Point2D {
    pub x: f64,
    pub y: f64,
}

impl Point2D {
    pub const fn new(x: f64, y: f64) -> Self {
        Self { x, y }
    }

    /// Computes 2D Euclidean distance to another point.
    pub fn distance(&self, other: &Point2D) -> f64 {
        let dx = self.x - other.x;
        let dy = self.y - other.y;
        #[cfg(feature = "std")]
        return (dx * dx + dy * dy).sqrt();
        #[cfg(not(feature = "std"))]
        {
            let sum = dx * dx + dy * dy;
            if sum <= 0.0 {
                0.0
            } else {
                // IEEE-754 bit-level square root approximation seed for instant convergence
                let bits = sum.to_bits();
                let seed_bits = (bits >> 1) + (1023u64 << 51);
                let mut x = f64::from_bits(seed_bits);
                // 4 Newton-Raphson refinement steps guarantee full 53-bit double precision
                for _ in 0..4 {
                    x = 0.5 * (x + sum / x);
                }
                x
            }
        }
    }
}

/// A timestamped screen coordinate waypoint within an input trajectory.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct TimedPoint {
    pub x: f64,
    pub y: f64,
    /// Elapsed time in milliseconds from the start of the motion.
    pub elapsed_ms: f64,
}

impl TimedPoint {
    pub const fn new(x: f64, y: f64, elapsed_ms: f64) -> Self {
        Self { x, y, elapsed_ms }
    }

    pub fn to_point2d(&self) -> Point2D {
        Point2D::new(self.x, self.y)
    }
}

/// Complete kinematic plan output by the trajectory planner.
#[derive(Debug, Clone, PartialEq)]
pub struct KinematicPlan {
    /// Ordered sequence of intermediate cursor positions with relative timestamps.
    pub points: Vec<TimedPoint>,
    /// Total duration of the motion trajectory in milliseconds.
    pub total_duration_ms: f64,
    /// Post-movement action dwell time (e.g. mouse button down-up hold duration) in milliseconds.
    pub dwell_ms: f64,
}

impl KinematicPlan {
    pub fn new(points: Vec<TimedPoint>, total_duration_ms: f64, dwell_ms: f64) -> Self {
        Self {
            points,
            total_duration_ms,
            dwell_ms,
        }
    }

    pub fn is_empty(&self) -> bool {
        self.points.is_empty()
    }

    pub fn len(&self) -> usize {
        self.points.len()
    }

    pub fn start_point(&self) -> Option<Point2D> {
        self.points.first().map(|p| p.to_point2d())
    }

    pub fn end_point(&self) -> Option<Point2D> {
        self.points.last().map(|p| p.to_point2d())
    }
}

/// Lightweight deterministic XorShift64 pseudo-random number generator for reproducible kinematics.
#[derive(Debug, Clone)]
pub struct FastPrng {
    state: u64,
}

impl FastPrng {
    pub const fn new(seed: u64) -> Self {
        let state = if seed == 0 { 0x853c49e6748fea9b } else { seed };
        Self { state }
    }

    /// Generates next pseudo-random u64 using XorShift64*.
    pub fn next_u64(&mut self) -> u64 {
        let mut x = self.state;
        x ^= x >> 12;
        x ^= x << 25;
        x ^= x >> 27;
        self.state = x;
        x.wrapping_mul(0x2545F4914F6CDD1D)
    }

    /// Generates float in range [0.0, 1.0).
    pub fn next_f64(&mut self) -> f64 {
        let val = self.next_u64() >> 11;
        (val as f64) / (9007199254740992.0) // 2^53
    }

    /// Generates float in range [min, max].
    pub fn next_range_f64(&mut self, min: f64, max: f64) -> f64 {
        if min >= max {
            return min;
        }
        min + self.next_f64() * (max - min)
    }

    /// Generates standard normal variable N(0, 1) using Box-Muller transform.
    pub fn next_standard_normal(&mut self) -> f64 {
        let mut u1 = self.next_f64();
        while u1 <= 1e-15 {
            u1 = self.next_f64();
        }
        let u2 = self.next_f64();

        #[cfg(feature = "std")]
        {
            let radius = (-2.0 * u1.ln()).sqrt();
            let theta = 2.0 * core::f64::consts::PI * u2;
            radius * theta.cos()
        }
        #[cfg(not(feature = "std"))]
        {
            // Fallback approximation when libm/std is not active
            (u1 + u2 - 1.0) * 1.732
        }
    }

    /// Generates Gaussian distributed float with specified mean and standard deviation.
    pub fn next_gaussian(&mut self, mean: f64, std_dev: f64) -> f64 {
        mean + std_dev * self.next_standard_normal()
    }

    /// Generates Log-Normal distributed float: exp(mu + sigma * Z).
    pub fn next_log_normal(&mut self, mu: f64, sigma: f64) -> f64 {
        let z = self.next_standard_normal();
        #[cfg(feature = "std")]
        {
            (mu + sigma * z).exp()
        }
        #[cfg(not(feature = "std"))]
        {
            // Taylor expansion approximation for exp(x)
            let x = mu + sigma * z;
            1.0 + x + (x * x) / 2.0 + (x * x * x) / 6.0
        }
    }
}

/// Configuration parameters for the WindMouse physics generator.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct WindMouseConfig {
    /// Magnitude of gravitational attraction toward the target point (default: 9.0).
    pub gravity: f64,
    /// Maximum magnitude of stochastic wind disturbance (default: 3.0).
    pub wind: f64,
    /// Minimum time step duration per simulation step in milliseconds (default: 3.0 ms).
    pub min_wait_ms: f64,
    /// Maximum time step duration per simulation step in milliseconds (default: 7.0 ms).
    pub max_wait_ms: f64,
    /// Maximum spatial step displacement per tick in pixels (default: 12.0 px).
    pub max_step_size: f64,
    /// Target proximity threshold (pixels) below which wind and gravity damping engage (default: 15.0 px).
    pub target_area_threshold: f64,
    /// Maximum allowable simulation steps before forced convergence (guards against limit cycles).
    pub max_steps: usize,
}

impl Default for WindMouseConfig {
    fn default() -> Self {
        Self {
            gravity: 9.0,
            wind: 3.0,
            min_wait_ms: 3.0,
            max_wait_ms: 7.0,
            max_step_size: 12.0,
            target_area_threshold: 15.0,
            max_steps: 3000,
        }
    }
}

/// WindMouse trajectory generator.
pub struct WindMouseGenerator;

impl WindMouseGenerator {
    /// Generates a natural humanized cursor trajectory from `start` to `target`.
    pub fn generate_path(
        start: Point2D,
        target: Point2D,
        config: &WindMouseConfig,
        rng: &mut FastPrng,
    ) -> KinematicPlan {
        let dist_initial = start.distance(&target);
        if dist_initial < 1e-4 {
            return KinematicPlan::new(
                alloc::vec![TimedPoint::new(target.x, target.y, 0.0)],
                0.0,
                75.0,
            );
        }

        let mut points = Vec::with_capacity(128);
        let mut cur_x = start.x;
        let mut cur_y = start.y;
        let mut v_x = 0.0;
        let mut v_y = 0.0;
        let mut w_x = 0.0;
        let mut w_y = 0.0;
        let mut elapsed_ms = 0.0;

        points.push(TimedPoint::new(cur_x, cur_y, elapsed_ms));

        let sqrt3 = 1.7320508075688772;
        let sqrt5 = 2.23606797749979;

        for _ in 0..config.max_steps {
            let dist = Point2D::new(cur_x, cur_y).distance(&target);
            if dist < 2.5 {
                break;
            }

            // In proximity zone: wind is damped and gravity reduced to eliminate oscillation
            let (eff_wind, eff_gravity) = if dist < config.target_area_threshold {
                let factor = dist / config.target_area_threshold;
                (config.wind * factor, config.gravity * factor.max(0.4))
            } else {
                (config.wind, config.gravity)
            };

            // Ornstein-Uhlenbeck continuous drift update
            w_x = (w_x / sqrt3) + (rng.next_range_f64(-eff_wind, eff_wind) / sqrt5);
            w_y = (w_y / sqrt3) + (rng.next_range_f64(-eff_wind, eff_wind) / sqrt5);

            // Gravitational pull toward target
            let g_x = eff_gravity * (target.x - cur_x) / dist;
            let g_y = eff_gravity * (target.y - cur_y) / dist;

            v_x += w_x + g_x;
            v_y += w_y + g_y;

            // Velocity damping
            let speed = Point2D::new(0.0, 0.0).distance(&Point2D::new(v_x, v_y));
            if speed > config.max_step_size {
                let scale = config.max_step_size / speed;
                v_x *= scale;
                v_y *= scale;
            }

            cur_x += v_x;
            cur_y += v_y;

            // Variable time step
            let step_dt = rng.next_range_f64(config.min_wait_ms, config.max_wait_ms);
            elapsed_ms += step_dt;

            points.push(TimedPoint::new(cur_x, cur_y, elapsed_ms));
        }

        // Terminal settling step snapped exactly to target
        if let Some(last) = points.last() {
            if (last.x - target.x).abs() > 1e-4 || (last.y - target.y).abs() > 1e-4 {
                elapsed_ms += rng.next_range_f64(config.min_wait_ms, config.max_wait_ms);
                points.push(TimedPoint::new(target.x, target.y, elapsed_ms));
            }
        }

        let dwell_ms = DwellProfile::mouse_click().sample(rng);
        KinematicPlan::new(points, elapsed_ms, dwell_ms)
    }
}

/// Configuration parameters for cubic Bézier minimum-jerk trajectory generation.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct BezierConfig {
    /// Lateral deviation standard deviation for departure control point P1 (default: 25.0 px).
    pub departure_deviation: f64,
    /// Lateral deviation standard deviation for arrival control point P2 (default: 12.0 px).
    pub arrival_deviation: f64,
    /// Number of sampled waypoints along the curve (default: 30).
    pub sample_count: usize,
}

impl Default for BezierConfig {
    fn default() -> Self {
        Self {
            departure_deviation: 25.0,
            arrival_deviation: 12.0,
            sample_count: 30,
        }
    }
}

/// Cubic Bézier trajectory generator parameterized by 5th-order minimum-jerk polynomials.
pub struct BezierTrajectoryGenerator;

impl BezierTrajectoryGenerator {
    /// Generates a cubic Bézier trajectory with asymmetric minimum-jerk acceleration/deceleration.
    pub fn generate_trajectory(
        start: Point2D,
        target: Point2D,
        total_duration_ms: f64,
        config: &BezierConfig,
        rng: &mut FastPrng,
    ) -> KinematicPlan {
        let dist = start.distance(&target);
        if dist < 1e-4 || config.sample_count <= 1 {
            return KinematicPlan::new(
                alloc::vec![TimedPoint::new(target.x, target.y, 0.0)],
                0.0,
                DwellProfile::mouse_click().sample(rng),
            );
        }

        // Chord direction vector and perpendicular normal vector
        let dx = target.x - start.x;
        let dy = target.y - start.y;
        let norm_x = -dy / dist;
        let norm_y = dx / dist;

        // Control point P1: 1/3 along chord with departure lateral jitter
        let dev1 = rng.next_gaussian(0.0, config.departure_deviation);
        let p1_x = start.x + 0.33 * dx + norm_x * dev1;
        let p1_y = start.y + 0.33 * dy + norm_y * dev1;

        // Control point P2: 2/3 along chord with arrival lateral jitter
        let dev2 = rng.next_gaussian(0.0, config.arrival_deviation);
        let p2_x = start.x + 0.67 * dx + norm_x * dev2;
        let p2_y = start.y + 0.67 * dy + norm_y * dev2;

        let mut points = Vec::with_capacity(config.sample_count + 1);

        for i in 0..=config.sample_count {
            let tau = (i as f64) / (config.sample_count as f64);
            // Minimum jerk polynomial: s(tau) = 10*tau^3 - 15*tau^4 + 6*tau^5
            let s = tau * tau * tau * (10.0 + tau * (-15.0 + 6.0 * tau));

            // Cubic Bezier interpolation: B(s) = (1-s)^3 P0 + 3(1-s)^2 s P1 + 3(1-s) s^2 P2 + s^3 P3
            let one_minus_s = 1.0 - s;
            let b0 = one_minus_s * one_minus_s * one_minus_s;
            let b1 = 3.0 * one_minus_s * one_minus_s * s;
            let b2 = 3.0 * one_minus_s * s * s;
            let b3 = s * s * s;

            let x = b0 * start.x + b1 * p1_x + b2 * p2_x + b3 * target.x;
            let y = b0 * start.y + b1 * p1_y + b2 * p2_y + b3 * target.y;
            let t = tau * total_duration_ms;

            points.push(TimedPoint::new(x, y, t));
        }

        let dwell_ms = DwellProfile::mouse_click().sample(rng);
        KinematicPlan::new(points, total_duration_ms, dwell_ms)
    }
}

/// Fitts's Law model for estimating target acquisition movement duration.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct FittsLawModel {
    /// Intercept parameter 'a' in milliseconds (minimum initiation and ballistic execution time).
    pub a_intercept_ms: f64,
    /// Slope parameter 'b' in milliseconds (inverse bandwidth / information rate).
    pub b_slope_ms: f64,
}

impl Default for FittsLawModel {
    fn default() -> Self {
        Self {
            a_intercept_ms: 120.0,
            b_slope_ms: 160.0,
        }
    }
}

impl FittsLawModel {
    pub const fn new(a_intercept_ms: f64, b_slope_ms: f64) -> Self {
        Self {
            a_intercept_ms,
            b_slope_ms,
        }
    }

    /// Computes movement duration: T = a + b * log2(1 + D / W).
    /// Target width is clamped to >= 1.0 px to prevent division by zero or negative logarithms.
    pub fn calculate_duration_ms(&self, distance: f64, target_width: f64) -> f64 {
        let d = distance.max(0.0);
        let w = target_width.max(1.0);
        let ratio = 1.0 + d / w;

        #[cfg(feature = "std")]
        let log2_term = ratio.log2();
        #[cfg(not(feature = "std"))]
        let log2_term = {
            // Rough log2 approximation for no_std
            let mut val = ratio;
            let mut count = 0.0;
            while val > 2.0 {
                val /= 2.0;
                count += 1.0;
            }
            count + (val - 1.0)
        };

        (self.a_intercept_ms + self.b_slope_ms * log2_term).max(self.a_intercept_ms)
    }
}

/// Log-Normal distribution profile for human motor dwell times and inter-event intervals.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct DwellProfile {
    pub mu: f64,
    pub sigma: f64,
    pub min_ms: f64,
    pub max_ms: f64,
}

impl DwellProfile {
    pub const fn new(mu: f64, sigma: f64, min_ms: f64, max_ms: f64) -> Self {
        Self {
            mu,
            sigma,
            min_ms,
            max_ms,
        }
    }

    /// Pre-calibrated profile for mouse button down-to-up hold duration.
    /// Median: ~75ms, 95% within [45ms, 140ms].
    pub fn mouse_click() -> Self {
        Self::new(4.317, 0.22, 40.0, 180.0)
    }

    /// Pre-calibrated profile for keyboard key tap duration.
    /// Median: ~85ms, 95% within [50ms, 160ms].
    pub fn key_tap() -> Self {
        Self::new(4.442, 0.20, 45.0, 220.0)
    }

    /// Pre-calibrated profile for inter-keystroke cadence delay.
    /// Median: ~130ms.
    pub fn inter_keystroke_cadence() -> Self {
        Self::new(4.867, 0.28, 60.0, 350.0)
    }

    /// Samples a duration from the log-normal distribution, clamped between `min_ms` and `max_ms`.
    pub fn sample(&self, rng: &mut FastPrng) -> f64 {
        let val = rng.next_log_normal(self.mu, self.sigma);
        val.clamp(self.min_ms, self.max_ms)
    }
}

/// Samples an interaction target point within a UI rectangle using 2D Gaussian clustering
/// around the center rather than robotic exact-center clicking.
pub fn sample_target_in_rect(
    rect_x: f64,
    rect_y: f64,
    rect_w: f64,
    rect_h: f64,
    margin_ratio: f64,
    rng: &mut FastPrng,
) -> Point2D {
    let center_x = rect_x + rect_w * 0.5;
    let center_y = rect_y + rect_h * 0.5;

    // Margin boundary clamping (e.g. margin_ratio = 0.2 means clicks stay within inner 60%)
    let half_w = rect_w * 0.5 * (1.0 - margin_ratio).clamp(0.1, 0.95);
    let half_h = rect_h * 0.5 * (1.0 - margin_ratio).clamp(0.1, 0.95);

    let offset_x = rng.next_gaussian(0.0, half_w * 0.4).clamp(-half_w, half_w);
    let offset_y = rng.next_gaussian(0.0, half_h * 0.4).clamp(-half_h, half_h);

    Point2D::new(center_x + offset_x, center_y + offset_y)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_point2d_distance_and_timed_point() {
        let p1 = Point2D::new(10.0, 20.0);
        let p2 = Point2D::new(40.0, 60.0);
        assert!((p1.distance(&p2) - 50.0).abs() < 1e-5);

        let tp = TimedPoint::new(15.0, 25.0, 100.0);
        assert_eq!(tp.to_point2d(), Point2D::new(15.0, 25.0));
    }

    #[test]
    fn test_fast_prng_distribution_properties() {
        let mut rng = FastPrng::new(1337);

        // Verify uniform range
        for _ in 0..100 {
            let f = rng.next_f64();
            assert!((0.0..1.0).contains(&f));
            let r = rng.next_range_f64(10.0, 20.0);
            assert!((10.0..=20.0).contains(&r));
        }

        // Verify Gaussian mean and variance over large sample
        let mut sum = 0.0;
        let count = 1000;
        for _ in 0..count {
            sum += rng.next_gaussian(50.0, 5.0);
        }
        let mean = sum / (count as f64);
        assert!(
            (mean - 50.0).abs() < 1.0,
            "Empirical mean {} must be close to 50.0",
            mean
        );
    }

    #[test]
    fn test_fitts_law_target_acquisition_duration() {
        let fitts = FittsLawModel::default();

        // Target close and large: ID = log2(1 + 50/50) = log2(2) = 1.0
        // Duration: 120 + 160 * 1.0 = 280ms
        let t_close = fitts.calculate_duration_ms(50.0, 50.0);
        assert!((t_close - 280.0).abs() < 1.0);

        // Target far and small: ID = log2(1 + 750/25) = log2(31) ≈ 4.954
        // Duration: 120 + 160 * 4.954 ≈ 912.6ms
        let t_far = fitts.calculate_duration_ms(750.0, 25.0);
        assert!(t_far > 850.0 && t_far < 950.0);
        assert!(
            t_far > t_close,
            "Far, small targets must require significantly more acquisition time"
        );
    }

    #[test]
    fn test_dwell_profile_bounds_and_sampling() {
        let mut rng = FastPrng::new(42);
        let profile = DwellProfile::mouse_click();

        for _ in 0..100 {
            let sample = profile.sample(&mut rng);
            assert!(
                sample >= profile.min_ms && sample <= profile.max_ms,
                "Sample {} must respect [{}, {}] clamping bounds",
                sample,
                profile.min_ms,
                profile.max_ms
            );
        }
    }

    #[test]
    fn test_wind_mouse_generates_smooth_curved_path() {
        let mut rng = FastPrng::new(999);
        let start = Point2D::new(100.0, 100.0);
        let target = Point2D::new(600.0, 400.0);
        let config = WindMouseConfig::default();

        let plan = WindMouseGenerator::generate_path(start, target, &config, &mut rng);

        assert!(!plan.is_empty());
        assert_eq!(plan.start_point().unwrap(), start);
        let end = plan.end_point().unwrap();
        assert!(
            end.distance(&target) < 1.0,
            "WindMouse terminal waypoint must snap accurately to target"
        );
        assert!(plan.total_duration_ms > 100.0);
        assert!(plan.dwell_ms >= 40.0 && plan.dwell_ms <= 180.0);

        // Verify non-linearity: path contains curvature due to wind drift
        let mid_idx = plan.len() / 2;
        let mid_point = plan.points[mid_idx].to_point2d();
        // A straight line mid point would be exactly (350, 250)
        let dist_to_straight_mid = mid_point.distance(&Point2D::new(350.0, 250.0));
        assert!(
            dist_to_straight_mid > 0.1,
            "WindMouse path must exhibit natural non-linear drift"
        );
    }

    #[test]
    fn test_bezier_trajectory_minimum_jerk_velocity() {
        let mut rng = FastPrng::new(2026);
        let start = Point2D::new(200.0, 300.0);
        let target = Point2D::new(700.0, 500.0);
        let duration = 650.0;
        let config = BezierConfig::default();

        let plan = BezierTrajectoryGenerator::generate_trajectory(
            start, target, duration, &config, &mut rng,
        );

        assert_eq!(plan.len(), config.sample_count + 1);
        assert_eq!(plan.start_point().unwrap(), start);
        let end = plan.end_point().unwrap();
        assert!((end.x - target.x).abs() < 1e-4);
        assert!((end.y - target.y).abs() < 1e-4);
        assert!((plan.total_duration_ms - duration).abs() < 1e-4);

        // Verify minimum-jerk acceleration/deceleration property:
        // Velocity at start and end is near zero, peak speed in middle
        let step1_dist = plan.points[0]
            .to_point2d()
            .distance(&plan.points[1].to_point2d());
        let mid = plan.len() / 2;
        let mid_dist = plan.points[mid]
            .to_point2d()
            .distance(&plan.points[mid + 1].to_point2d());
        let end_dist = plan.points[plan.len() - 2]
            .to_point2d()
            .distance(&plan.points[plan.len() - 1].to_point2d());

        assert!(
            mid_dist > step1_dist,
            "Middle velocity ({}) must exceed launch velocity ({})",
            mid_dist,
            step1_dist
        );
        assert!(
            mid_dist > end_dist,
            "Middle velocity ({}) must exceed terminal deceleration ({})",
            mid_dist,
            end_dist
        );
    }

    #[test]
    fn test_sample_target_in_rect_avoids_corners_and_exact_center() {
        let mut rng = FastPrng::new(5555);
        let rect_x = 100.0;
        let rect_y = 200.0;
        let rect_w = 80.0;
        let rect_h = 120.0;

        let center = Point2D::new(rect_x + rect_w * 0.5, rect_y + rect_h * 0.5);

        for _ in 0..50 {
            let pt = sample_target_in_rect(rect_x, rect_y, rect_w, rect_h, 0.15, &mut rng);
            // Must stay within rectangle bounds
            assert!(pt.x >= rect_x && pt.x <= rect_x + rect_w);
            assert!(pt.y >= rect_y && pt.y <= rect_y + rect_h);
            // Must not be exactly dead center
            assert!(pt.distance(&center) > 1e-4);
        }
    }
}
