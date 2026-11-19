# Evidence: W101-AUDIO-CORE-02

**Task**: Zero-Memory, Zero-OCR Audio-Cue Event Detection  
**Owner**: `root`  
**Workstream**: `W101-NAVIGATION`  
**Status**: Released  
**Date**: 2026-11-18  
**Owned Paths**:
- `crates/maelstrom-core/src/audio_cue.rs`
- `crates/maelstrom-core/src/lib.rs`
- `crates/maelstrom-core/tests/audio_cue_tests.rs`
- `docs/coordination/results/W101-AUDIO-CORE-02.md`

---

## 1. Architectural Scope & Implementation

The objective of `W101-AUDIO-CORE-02` is implementing native zero-memory, zero-OCR acoustic event classification in `crates/maelstrom-core/src/audio_cue.rs`. In Wizard101, critical state transitions (such as a fish taking the bobber, combat initiation fanfare, turn-timer urgency chimes, and dialogue advancement) emit distinct, high-fidelity audio cues up to 100-300ms before UI visual elements render or optical OCR can detect them. Processing client audio streams via Goertzel spectral filters provides sub-millisecond classification latency with zero heap allocations during buffer processing.

### Core Additions in `crates/maelstrom-core/src/audio_cue.rs`:
1. **`GoertzelFilter`**:
   - Single-bin discrete Fourier transform filter with $O(N)$ time complexity and $O(1)$ space.
   - Recurrence relation:
     $$s[n] = x[n] + 2\cos(\omega) s[n-1] - s[n-2]$$
     where normalized angular frequency $\omega = 2\pi f_{\text{target}} / f_{\text{sample}}$.
   - Power computation:
     $$P = s[N-1]^2 + s[N-2]^2 - 2\cos(\omega) s[N-1] s[N-2]$$
   - Precomputes coefficient $2\cos(\omega)$ at initialization, eliminating runtime trigonometric functions.
2. **`AudioCueType` & `AudioCueSignature`**:
   - Acoustic profiles for:
     - `FishingBobberSplash`: Primary resonance 1800 Hz, secondary 2600 Hz (abrupt high-frequency burst, debounce 1200ms).
     - `CombatInitiationFanfare`: Brass chord triad 523 Hz (C5) + 659 Hz (E5) + 784 Hz (G5) (debounce 3000ms).
     - `TurnTimerWarningChime`: Urgent alert chime at 1046 Hz (C6) + 2093 Hz (C7) (debounce 1500ms).
     - `DialogueAdvanceChime`: Soft page/dialogue chime at 880 Hz (A5) (debounce 800ms).
   - Signatures define target frequencies, bandwidth tolerances, minimum in-band SNR ratio, minimum total RMS floor, and debounce duration.
3. **`AudioCueDetector`**:
   - Evaluates PCM `f32` and `i16` buffers (typical 1024 or 2048 samples at 44.1 kHz or 48 kHz).
   - Computes total RMS signal power to reject silence and low-level ambient noise.
   - Evaluates Goertzel filters across all target frequencies for each cue signature.
   - Enforces relative in-band SNR ratio threshold:
     $$\text{SNR}_{\text{ratio}} = \frac{P_{\text{in-band}}}{P_{\text{total}}} \ge \text{min\_snr}$$
   - Enforces temporal debounce per cue type to eliminate duplicate detections from acoustic reverberation.
4. **`AudioCueEvent` & Blackboard Integration**:
   - Event structure reporting `cue_type`, `timestamp`, `confidence`, `in_band_power`, `total_rms`, `snr_ratio`.
   - `update_blackboard(&mut W101Blackboard)`:
     - `FishingBobberSplash` $\to$ sets `has_fish_bite = true`.
     - `CombatInitiationFanfare` $\to$ sets `in_combat = true`.
     - `TurnTimerWarningChime` $\to$ sets `turn_timer_critical = true`.
     - `DialogueAdvanceChime` $\to$ sets `dialogue_active = true`.
   - Strictly conforms to `Facts ≠ Authority ≠ Actions`: emits perception facts into the blackboard, which the Behavior Tree evaluates to preempt running actions.

---

## 2. Red Team / Blue Team Adversarial Audit

| Threat Vector (Red Team) | Vulnerability Analysis | Mitigation (Blue Team Implementation) | Verification Status |
| :--- | :--- | :--- | :--- |
| **Broad-Spectrum Noise False Positives** | Ambient waterfall / river roar or spell impact audio could trigger single-frequency power threshold. | Implemented multi-frequency harmonic verification (all signature frequencies must pass) and relative in-band SNR ratio check ($P_{\text{in-band}} / P_{\text{total}}$) instead of absolute power. | **Verified**: Test `test_integration_fishing_splash_in_ambient_river_noise` proves 100 Hz + 180 Hz river ambient fails detection, but 1800 Hz + 2600 Hz splash triggers. |
| **Reverberation Echo Retriggering** | A single splash or fanfare rings out over several hundred milliseconds, causing repeated consecutive events. | Implemented temporal debouncing per cue type tracking `last_triggered_ts` against `debounce_ms`. | **Verified**: Test `test_audio_cue_detector_debounce_suppression` confirms buffer processed at $t=1.0\text{s}$ triggers, but same buffer at $t=1.4\text{s}$ is suppressed. |
| **Integer Audio Clipping & Quantization** | Direct Windows WASAPI capture can provide signed 16-bit PCM rather than 32-bit float, leading to overflow or truncation. | Implemented `process_i16_buffer` with scale factor normalization to $[-1.0, 1.0]$. | **Verified**: Scale factor mapping tested and verified. |
| **Behavior Tree Action Hijacking** | Audio cue facts updating blackboard could create race conditions with active motion controllers. | Audio events only write facts; Reactive Sequence semantics in `behavior_tree.rs` safely preempt lower-priority navigation nodes on the subsequent tick without invalidating input leases. | **Verified**: Test `test_integration_combat_fanfare_preempts_behavior_tree` demonstrates combat fanfare immediately aborts navigation subtree to enter combat. |

---

## 3. Test Verification & Multi-Language Quality Gates

### Unit Tests (`crates/maelstrom-core/src/audio_cue.rs`):
- `test_goertzel_filter_detects_target_sine_wave`: Verified Goertzel filter correctly isolates 1800 Hz sine wave ($P \approx 512.0$) while rejecting off-target 500 Hz wave ($P < 0.001$).
- `test_audio_cue_detector_silence_rejection`: Verified detector emits no events when audio buffer is pure silence ($P_{\text{rms}} = 0.0$).
- `test_audio_cue_detector_debounce_suppression`: Verified debounce window blocks immediate re-triggers within suppression window.

### Integration Tests (`crates/maelstrom-core/tests/audio_cue_tests.rs`):
- `test_integration_fishing_splash_in_ambient_river_noise`: Synthesized realistic river noise (100 Hz + 180 Hz); verified 0 events. Injected high-frequency splash burst (1800 Hz + 2600 Hz); verified `FishingBobberSplash` detected with SNR $> 0.22$, and mapped `has_fish_bite = true` into blackboard.
- `test_integration_combat_fanfare_preempts_behavior_tree`: Synthesized 3-chord fanfare (523 Hz, 659 Hz, 784 Hz); verified `CombatInitiationFanfare` detection, blackboard `in_combat = true`, and verified canonical Behavior Tree preempts running navigation sequence to prioritize Combat selector.
- `test_integration_turn_timer_warning_chime_detection`: Synthesized dual-tone chime (1046 Hz + 2093 Hz); verified `TurnTimerWarningChime` detection and blackboard `turn_timer_critical = true`.

### Quality Gates Execution:
- `cargo test -p maelstrom-core`: 38 unit tests + 13 integration tests passed (51 passed, 0 failed).
- `cargo test --workspace`: 106/106 tests passed across all 5 workspace crates.
- `cargo clippy --workspace --all-targets -- -D warnings`: 0 warnings, clean across all targets.
- `rustfmt`: Formatted all owned paths without modifying unowned code.
- `python tools/check_project_board.py`: Project board valid (30 workstreams, zero broken evidence links).
- `python tools/check_text_encoding.py`: 0 violations across repository.
