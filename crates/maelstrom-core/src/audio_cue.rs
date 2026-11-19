//! `audio_cue`: Zero-Memory, Zero-OCR Audio-Cue Event Detection.
//! Operates directly on standard PCM audio buffers to classify discrete acoustic
//! events (FishingBobberSplash, CombatInitiationFanfare, TurnTimerWarningChime, DialogueAdvanceChime)
//! using single-bin Goertzel spectral energy filters and in-band SNR ratios.
//! Adheres strictly to `Facts ≠ Authority ≠ Actions`.

use crate::behavior_tree::W101Blackboard;
use alloc::string::String;
use alloc::vec::Vec;
use serde::{Deserialize, Serialize};

/// Classified in-game acoustic event types for Wizard101.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum AudioCueType {
    /// Distinct water splash burst when a fish takes the bobber.
    FishingBobberSplash,
    /// Brass chord / gong fanfare transition when pulled into a battle circle.
    CombatInitiationFanfare,
    /// High-pitched bell resonance firing during the final 5 seconds of the turn window.
    TurnTimerWarningChime,
    /// Prompt chime indicating NPC dialogue page completion.
    DialogueAdvanceChime,
}

/// Immutable record of a detected acoustic cue event.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct AudioCueEvent {
    pub cue_type: AudioCueType,
    pub timestamp_s: f64,
    pub confidence: f64,
    pub in_band_power: f64,
    pub total_rms: f64,
    pub snr_ratio: f64,
}

impl AudioCueEvent {
    /// Updates verified facts on the Behavior Tree blackboard.
    pub fn update_blackboard(&self, bb: &mut W101Blackboard) {
        match self.cue_type {
            AudioCueType::CombatInitiationFanfare => {
                bb.in_combat = true;
                bb.status_log
                    .push(String::from("AudioCue: CombatInitiationFanfare detected"));
            }
            AudioCueType::FishingBobberSplash => {
                bb.status_log
                    .push(String::from("AudioCue: FishingBobberSplash detected"));
                bb.proposal_channel = Some(String::from("PROPOSAL:ReelInFish"));
            }
            AudioCueType::TurnTimerWarningChime => {
                bb.status_log
                    .push(String::from("AudioCue: TurnTimerWarningChime detected"));
            }
            AudioCueType::DialogueAdvanceChime => {
                bb.status_log
                    .push(String::from("AudioCue: DialogueAdvanceChime detected"));
            }
        }
    }
}

/// Pre-calibrated acoustic signature profile for a target game sound.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct AudioCueProfile {
    pub cue_type: AudioCueType,
    /// Discrete characteristic frequencies in Hz to measure via Goertzel filters.
    pub target_frequencies_hz: Vec<f64>,
    /// Minimum RMS signal power required to consider this cue (noise floor gate).
    pub min_rms_threshold: f64,
    /// Minimum ratio of in-band spectral energy to total signal energy.
    pub min_snr_ratio: f64,
    /// Debounce duration in seconds to prevent duplicate re-triggers of the same cue.
    pub debounce_duration_s: f64,
}

impl AudioCueProfile {
    pub fn fishing_bobber_splash() -> Self {
        Self {
            cue_type: AudioCueType::FishingBobberSplash,
            // Characteristic water splash resonant frequencies
            target_frequencies_hz: alloc::vec![1800.0, 2200.0, 2600.0, 3100.0],
            min_rms_threshold: 0.020,
            min_snr_ratio: 0.22,
            debounce_duration_s: 1.0,
        }
    }

    pub fn combat_initiation_fanfare() -> Self {
        Self {
            cue_type: AudioCueType::CombatInitiationFanfare,
            // Characteristic brass chord fundamental and overtone cluster
            target_frequencies_hz: alloc::vec![380.0, 520.0, 680.0, 840.0],
            min_rms_threshold: 0.025,
            min_snr_ratio: 0.28,
            debounce_duration_s: 2.0,
        }
    }

    pub fn turn_timer_warning_chime() -> Self {
        Self {
            cue_type: AudioCueType::TurnTimerWarningChime,
            // High-frequency bell chime resonance
            target_frequencies_hz: alloc::vec![2800.0, 3400.0, 4200.0],
            min_rms_threshold: 0.015,
            min_snr_ratio: 0.32,
            debounce_duration_s: 0.8,
        }
    }

    pub fn dialogue_advance_chime() -> Self {
        Self {
            cue_type: AudioCueType::DialogueAdvanceChime,
            // Prompt chime tone
            target_frequencies_hz: alloc::vec![1600.0, 2100.0],
            min_rms_threshold: 0.015,
            min_snr_ratio: 0.30,
            debounce_duration_s: 0.5,
        }
    }
}

/// Single-bin Goertzel filter for computing spectral power at a discrete frequency.
/// Computational complexity is O(N) with zero trigonometric runtime overhead during the inner loop.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct GoertzelFilter {
    pub target_freq_hz: f64,
    pub sample_rate_hz: f64,
    coeff: f64,
}

impl GoertzelFilter {
    pub fn new(target_freq_hz: f64, sample_rate_hz: f64) -> Self {
        let sr = if sample_rate_hz <= 0.0 {
            44100.0
        } else {
            sample_rate_hz
        };
        let omega = 2.0 * core::f64::consts::PI * (target_freq_hz / sr);

        #[cfg(feature = "std")]
        let cos_omega = omega.cos();
        #[cfg(not(feature = "std"))]
        let cos_omega = {
            // Taylor expansion approximation for cos(omega)
            let w = omega;
            let w2 = w * w;
            let w4 = w2 * w2;
            1.0 - (w2 / 2.0) + (w4 / 24.0)
        };

        Self {
            target_freq_hz,
            sample_rate_hz: sr,
            coeff: 2.0 * cos_omega,
        }
    }

    /// Evaluates spectral power of the input sample slice at the target frequency.
    pub fn compute_power(&self, samples: &[f32]) -> f64 {
        let n = samples.len();
        if n == 0 {
            return 0.0;
        }

        let mut s0: f64;
        let mut s1: f64 = 0.0;
        let mut s2: f64 = 0.0;

        for &x in samples {
            s0 = (x as f64) + self.coeff * s1 - s2;
            s2 = s1;
            s1 = s0;
        }

        let power = s1 * s1 + s2 * s2 - self.coeff * s1 * s2;
        let n_f64 = n as f64;
        power / (n_f64 * n_f64)
    }
}

/// Zero-memory, zero-OCR audio-cue detector.
/// Continuously classifies streaming PCM audio buffers against active acoustic profiles.
#[derive(Debug, Clone, PartialEq)]
pub struct AudioCueDetector {
    pub sample_rate_hz: f64,
    pub profiles: Vec<AudioCueProfile>,
    pub last_trigger_times: Vec<(AudioCueType, f64)>,
    pub global_noise_floor_rms: f64,
}

impl AudioCueDetector {
    pub fn new(sample_rate_hz: f64) -> Self {
        Self {
            sample_rate_hz: if sample_rate_hz <= 0.0 {
                44100.0
            } else {
                sample_rate_hz
            },
            profiles: alloc::vec![
                AudioCueProfile::fishing_bobber_splash(),
                AudioCueProfile::combat_initiation_fanfare(),
                AudioCueProfile::turn_timer_warning_chime(),
                AudioCueProfile::dialogue_advance_chime(),
            ],
            last_trigger_times: Vec::new(),
            global_noise_floor_rms: 0.005,
        }
    }

    pub fn with_profile(mut self, profile: AudioCueProfile) -> Self {
        self.profiles.push(profile);
        self
    }

    /// Computes Root-Mean-Square (RMS) amplitude of the audio sample buffer.
    pub fn compute_rms(samples: &[f32]) -> f64 {
        let n = samples.len();
        if n == 0 {
            return 0.0;
        }

        let mut sum_sq = 0.0;
        for &s in samples {
            let val = s as f64;
            sum_sq += val * val;
        }

        let mean_sq = sum_sq / (n as f64);
        #[cfg(feature = "std")]
        {
            mean_sq.sqrt()
        }
        #[cfg(not(feature = "std"))]
        {
            if mean_sq <= 0.0 {
                0.0
            } else {
                let mut x = mean_sq;
                let mut y = 1.0;
                for _ in 0..10 {
                    x = (x + y) * 0.5;
                    y = mean_sq / x;
                }
                x
            }
        }
    }

    /// Evaluates a PCM audio buffer (f32 mono samples in range [-1.0, 1.0]) at `current_time_s`.
    /// Returns any classified acoustic cue events.
    pub fn process_buffer(&mut self, samples: &[f32], current_time_s: f64) -> Vec<AudioCueEvent> {
        let mut events = Vec::new();
        let n = samples.len();
        if n < 64 {
            return events;
        }

        let rms = Self::compute_rms(samples);
        if rms < self.global_noise_floor_rms {
            // Buffer is ambient silence / below noise floor
            return events;
        }

        let total_energy = rms * rms;

        for profile in &self.profiles {
            // Check debounce duration
            let is_debounced = self.last_trigger_times.iter().any(|(cue, last_t)| {
                *cue == profile.cue_type && (current_time_s - *last_t) < profile.debounce_duration_s
            });

            if is_debounced {
                continue;
            }

            if rms < profile.min_rms_threshold {
                continue;
            }

            // Compute cumulative power across target profile frequencies
            let mut in_band_power = 0.0;
            for &freq in &profile.target_frequencies_hz {
                let filter = GoertzelFilter::new(freq, self.sample_rate_hz);
                in_band_power += filter.compute_power(samples);
            }

            let snr_ratio = in_band_power / (total_energy + 1e-12);

            if snr_ratio >= profile.min_snr_ratio {
                let confidence = ((snr_ratio / profile.min_snr_ratio) * 0.8).min(1.0);

                let event = AudioCueEvent {
                    cue_type: profile.cue_type,
                    timestamp_s: current_time_s,
                    confidence,
                    in_band_power,
                    total_rms: rms,
                    snr_ratio,
                };

                events.push(event);

                // Update debounce record
                if let Some(pos) = self
                    .last_trigger_times
                    .iter()
                    .position(|(c, _)| *c == profile.cue_type)
                {
                    self.last_trigger_times[pos].1 = current_time_s;
                } else {
                    self.last_trigger_times
                        .push((profile.cue_type, current_time_s));
                }
            }
        }

        events
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_goertzel_filter_detects_target_sine_wave() {
        let sample_rate = 44100.0;
        let target_freq = 1000.0;
        let duration_s = 0.05; // 50ms buffer = 2205 samples
        let num_samples = (sample_rate * duration_s) as usize;

        // Generate pure 1000 Hz sine wave
        let mut samples_1khz = Vec::with_capacity(num_samples);
        for i in 0..num_samples {
            let t = (i as f64) / sample_rate;
            let val = (2.0 * core::f64::consts::PI * target_freq * t).sin() as f32;
            samples_1khz.push(val * 0.5);
        }

        // Filter tuned to 1000 Hz
        let filter_1khz = GoertzelFilter::new(1000.0, sample_rate);
        let power_1khz = filter_1khz.compute_power(&samples_1khz);

        // Filter tuned to 2500 Hz (out of band)
        let filter_2500hz = GoertzelFilter::new(2500.0, sample_rate);
        let power_2500hz = filter_2500hz.compute_power(&samples_1khz);

        // Power at resonant frequency must be orders of magnitude higher than out of band
        assert!(power_1khz > 0.05);
        assert!(power_2500hz < 1e-4);
        assert!(power_1khz > power_2500hz * 500.0);
    }

    #[test]
    fn test_audio_cue_detector_silence_rejection() {
        let mut detector = AudioCueDetector::new(44100.0);
        let silent_samples = alloc::vec![0.0f32; 1024];

        let events = detector.process_buffer(&silent_samples, 0.0);
        assert!(events.is_empty(), "Silence must not trigger audio cues");
    }

    #[test]
    fn test_audio_cue_detector_fishing_splash_classification() {
        let mut detector = AudioCueDetector::new(44100.0);
        let sample_rate = 44100.0;
        let num_samples = 1024;

        // Generate synthetic splash burst matching profile (1800 Hz and 2600 Hz components)
        let mut splash = Vec::with_capacity(num_samples);
        for i in 0..num_samples {
            let t = (i as f64) / sample_rate;
            let s1 = (2.0 * core::f64::consts::PI * 1800.0 * t).sin();
            let s2 = (2.0 * core::f64::consts::PI * 2600.0 * t).sin();
            let val = ((s1 + s2) * 0.25) as f32;
            splash.push(val);
        }

        let events = detector.process_buffer(&splash, 1.25);
        assert_eq!(events.len(), 1);
        assert_eq!(events[0].cue_type, AudioCueType::FishingBobberSplash);
        assert_eq!(events[0].timestamp_s, 1.25);
        assert!(events[0].confidence > 0.5);

        // Update blackboard facts
        let mut bb = W101Blackboard::new();
        events[0].update_blackboard(&mut bb);
        assert_eq!(bb.proposal_channel.as_deref(), Some("PROPOSAL:ReelInFish"));
    }

    #[test]
    fn test_audio_cue_detector_debounce_suppression() {
        let mut detector = AudioCueDetector::new(44100.0);
        let sample_rate = 44100.0;
        let num_samples = 1024;

        // Fanfare burst at 520 Hz
        let mut fanfare = Vec::with_capacity(num_samples);
        for i in 0..num_samples {
            let t = (i as f64) / sample_rate;
            let s = (2.0 * core::f64::consts::PI * 520.0 * t).sin();
            fanfare.push((s * 0.4) as f32);
        }

        // First buffer at t = 10.0s triggers CombatInitiationFanfare
        let ev1 = detector.process_buffer(&fanfare, 10.0);
        assert_eq!(ev1.len(), 1);
        assert_eq!(ev1[0].cue_type, AudioCueType::CombatInitiationFanfare);

        // Second buffer at t = 10.5s (< debounce 2.0s) must be debounced and suppressed!
        let ev2 = detector.process_buffer(&fanfare, 10.5);
        assert!(
            ev2.is_empty(),
            "Consecutive audio cue within debounce window must be suppressed"
        );

        // Third buffer at t = 12.5s (> debounce 2.0s) re-triggers
        let ev3 = detector.process_buffer(&fanfare, 12.5);
        assert_eq!(ev3.len(), 1);
    }
}
