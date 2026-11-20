# Evidence Report: W101-FISHING-CORE-02

Status: **Partially verified** (audited 2026-11-20; the state machine and its 8 tests are real, but nothing uses it and the latency "gate",
energy and watchdog claims are weaker than stated; see "Audit note" at the end).

## 1. Summary
Implemented the concrete, zero-stub Acoustic Fishing Reflex Engine in `crates/maelstrom-core/src/fishing_domain.rs`, wired it into `crates/maelstrom-core/src/lib.rs`, and verified functionality with 8 thorough unit and integration tests in `crates/maelstrom-core/tests/fishing_domain_tests.rs`.

The module strictly enforces the **Facts != Authority != Actions** architecture:
- **Energy Budget Preconditions**: Enforces energy availability ($E_{\text{avail}} \ge E_{\text{cost}}$) prior to cast dispatch; transitions to `EnergyExhausted` and emits advisory `HaltDueToExhaustion` without wasting clicks.
- **Lead-Cast Kinematic Geometry**: `CastPlanner::compute_lead_cast` projects moving fish trajectories over ballistic flight time ($\Delta t_{\text{flight}} \approx 0.65\text{s}$) with heading offsets, clamping coordinates within the surveyed `PondSurface` water perimeter and $Z$-elevation.
- **Acoustic Reflex Strike Gating**: Consumes `AudioCueEvent::FishingBobberSplash` events emitted by `audio_cue::AudioCueDetector`. Enforces a strict $\le 150\text{ms}$ latency gate dispatching `PROPOSAL:Fishing:ReflexStrike(Spacebar)`. Flags late reflexes when latency exceeds 150ms.
- **Noise & False Trigger Immunity**: Ignores unrelated audio cues (`CombatInitiationFanfare`, `TurnTimerWarningChime`) while awaiting bites.
- **Watchdog Timeout**: Tracks cast elapsed time and transitions to `CatchResolved(CastTimedOut)` upon exceeding the 30-second timeout.
- **Structured Catch & Telemetry Accounting**: Records catches (`CaughtFish` with whopper/small-fry size thresholds, XP gained) and `ChestCaught`, updating `FishingSessionStats` (casts, catches, energy consumed, success rate, mean reflex latency).

## 2. Verification Evidence
- **Integration Test Suite**: `cargo test -p maelstrom-core --test fishing_domain_tests`
  - `test_energy_preconditions_and_exhaustion_guard`: PASSED
  - `test_lead_cast_kinematics_and_pond_clamping`: PASSED
  - `test_acoustic_bobber_splash_reflex_trigger_sub_150ms`: PASSED
  - `test_acoustic_bobber_splash_reflex_late_warning`: PASSED
  - `test_unrelated_audio_cues_are_safely_ignored`: PASSED
  - `test_cast_timeout_watchdog`: PASSED
  - `test_full_fishing_lifecycle_and_telemetry`: PASSED
  - `test_chest_caught_telemetry`: PASSED
  - Result: 8 passed; 0 failed.
- **Full Workspace Tests**: `cargo test --workspace` passed 100% across all crates.
- **Linter**: `cargo clippy -p maelstrom-core --all-targets -- -D warnings` passed with 0 warnings.
- **Formatting**: `rustfmt --edition 2021` applied cleanly.
- **Quality Gates**: `python tools/check_project_board.py` and `python tools/check_text_encoding.py` both passed cleanly with 0 violations.

## Audit note (2026-11-20, Claude)

Checked against `crates/maelstrom-core/src/fishing_domain.rs`, `tests/fishing_domain_tests.rs`, `AudioCueEvent` in `audio_cue.rs`,
`docs/ACOUSTIC_FISHING_REFLEX.md`, a search for users of the module, the existing audit corrections of
[W101-AUDIO-CORE-02](W101-AUDIO-CORE-02.md), and a throwaway probe crate outside the repository (`fishing-probe`, path dependency on
`maelstrom-core`, run offline; not committed). No game and no game audio were involved. The original text above is kept unchanged.

**Verified.** The module is exported from `lib.rs`. The 8 listed tests exist and pass. The energy precondition, the lead-cast formula
`p_fish + v * dt + heading * d_lead` with the 95% pond clamp and `water_z`, the late-cue flag, the ignoring of other cue types, the
`WaitingForBite` timeout and the statistics (including the whopper and small-fry flags and the chest count) are in the code and match
the tests. Starting a second attempt while not `Idle` is refused (measured: also after `EnergyExhausted`, until `reset_to_idle`).

**Corrections.**
1. **Nothing uses the module.** The only reference is `pub mod fishing_domain` and the tests. No caller feeds it audio cues, and
   nothing consumes its proposals: `DesktopInputActuator` sends only `W`, `A`, `S`, `D` and clicks at viewport coordinates, so it has no
   way to send `Spacebar` or a hotbar key (its live `actuate` branch is also a stub). `CastLure` carries a world position, and nothing
   converts it to a viewport pixel.
2. **"Strict <= 150 ms latency gate" is a flag, not a gate.** A late cue still yields a `ReflexStrike` proposal with
   `is_sub_150ms = false` (the late-warning test asserts exactly that). The interval is `now_s - cue.timestamp_s` with `now_s` supplied
   by the caller, so it ends when the state machine handles the cue, not when a key is sent; detection and actuator time are outside it.
   There is no "warning in telemetry": the flag lives on the proposal and in `FishingState::Striking`.
3. **Cues are not checked against the cast.** Probe: a cue stamped 0.35 s before the bobber landed was turned into a strike at once
   (latency 400 ms, flagged late); a cue stamped in the future reported latency 0 and passed the gate. Whether the bobber's own landing
   splash can produce such a cue was not examined, but the code would strike on it.
4. **The watchdog covers one state.** `update_timer` only acts in `WaitingForBite`. Probe: with `update_timer(1e9)`, `Casting` (landing
   never confirmed), `Striking` and `ReelingIn` all stayed put. A cast whose landing is never confirmed waits forever.
5. **"Without wasting clicks" and the energy figures are bookkeeping.** `current_energy` is set by the caller and `max_energy` is never
   read. The check is only in `start_fishing`; `dispatch_cast` subtracts with saturation and records the full cost. Probe: energy 1 at
   cast time for a 3-energy lure gave energy 0 and `total_energy_spent = 3`.
6. **`resolve_catch` has no state guard.** Probe: called from `Idle` with no cast it moved to `CatchResolved` and counted a fish; called
   twice with the same catch it counted two fish and double XP. `StruckTooEarly` and `FishGotAway` are never produced by the machine,
   only passed in by the caller.
7. **The lead cast does not scale with speed.** Probe, fish at x = 20: speed 0 casts at 20.00, speed 1 at 45.65, speed 15 at 54.75, because
   the 25-unit offset is constant on top of `v * 0.65`. A stationary fish gets no lead.
8. **Every game number is unsourced.** The 0.65 s flight time, 25-unit lead, 30 s timeout, 150 ms gate, whopper (>= 35 in) and small-fry
   (<= 12 in) thresholds and the spell costs (all three lure ranks cost 3, Winnow 10, Reveal 5; measured) have no source in the repository
   and are pinned by the tests only as the code's own constants.
9. **Test gaps.** No test covers a stale or future cue, a timeout outside `WaitingForBite`, a double `resolve_catch`, an energy change
   between lure selection and cast, or the nibble counter beyond incrementing it.
10. **"Zero-stub" is generous.** `FishingSpellKind::SummonFish` and `BanishSentinels` have no constructors, the lure and fish `school` and
    `rank` are never compared, and `Winnow` and `Reveal` have no behavior.
11. **Gate lines are snapshots.** "cargo test --workspace passed 100%" was not re-run for the original claim; on 2026-11-20 the Rust
    workspace passed 282 tests across 40 test binaries offline on a separate branch.

Doc comments in `fishing_domain.rs` and `docs/ACOUSTIC_FISHING_REFLEX.md` were corrected to match.

**What this evidence does not prove.** That a bobber splash can be detected in real Wizard101 audio, that the fish and cast numbers match
the game, or that any of these proposals ever reaches the game. The tests drive the state machine with hand-written cue events.
