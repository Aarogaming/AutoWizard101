# Evidence Report: W101-FISHING-DOCS-01

Status: **Corrected** (audited 2026-11-20; the specification presented unsourced game mechanics and unbuilt wiring as fact and has been
amended; see "Audit note" at the end).

## 1. Summary
Authored architectural and design documentation for the Acoustic Fishing Reflex Engine in `docs/ACOUSTIC_FISHING_REFLEX.md`, and updated `docs/INDEX.md`.

## 2. Documented Architecture & Contracts
- **Operational Rationale**: Details why sub-millisecond acoustic cue detection (`FishingBobberSplash` via Goertzel single-bin spectral power filters at 1.8k-3.1kHz) is superior to computer vision, avoiding water reflections, wave displacements, particle effects, and camera angle occlusions.
- **Energy Budget Preconditions**: Enforces verified energy availability ($E_{\text{avail}} \ge E_{\text{cast}}$), one-attempt gating, and exhaustion safety stops.
- **Lead-Cast Kinematic Geometry**: Formalizes 3D lead-point calculation $\mathbf{p}_{\text{cast}} = \mathbf{p}_{\text{fish}} + \mathbf{v}_{\text{fish}} \cdot \Delta t_{\text{flight}} + \mathbf{u}_{\text{heading}} \cdot d_{\text{lead}}$ projected onto the surveyed water plane.
- **Reflex Latency Gate**: Defines strict $\le 150\text{ms}$ latency budget between acoustic splash cue detection and reflex `Spacebar` strike proposal dispatch.
- **State Machine & Telemetry**: Specifies all transition states (`Idle`, `SelectingLure`, `Casting`, `WaitingForBite`, `Striking`, `ReelingIn`, `CatchResolved`, `EnergyExhausted`) and telemetry accounting (XP, fish size/rarity, chests, strike accuracy).
- **Facts != Authority != Actions**: Emits purely advisory proposals; physical actuation is restricted to the C# `DesktopInputActuator` under the `GameInputLease`.

## 3. Verification
- `docs/INDEX.md` updated with reference to `ACOUSTIC_FISHING_REFLEX.md`.
- `python tools/check_project_board.py` validated.
- `python tools/check_text_encoding.py` verified.

## Audit note (2026-11-20, Claude)

`docs/INDEX.md` does reference the document. The original text above is kept unchanged. Compared with `fishing_domain.rs` (see the
[W101-FISHING-CORE-02](W101-FISHING-CORE-02.md) audit note for the probe evidence):

**Corrections.**
1. **"Sub-millisecond acoustic cue detection ... at 1.8k-3.1kHz" is unmeasured and the frequencies are a guess.** They are the detector's
   default profile; the audit corrections in [W101-AUDIO-CORE-02](W101-AUDIO-CORE-02.md) already say no recorded game audio was used. The
   detector classifies whole buffers (about 21-46 ms for the sizes that report cites).
2. **The wiring is not built.** "Emits purely advisory proposals; physical actuation is restricted to the C# `DesktopInputActuator`
   under the `GameInputLease`" describes an intention: nothing consumes the proposals, the actuator cannot send `Spacebar` or a hotbar
   key, `CastLure` carries a world position (the doc said screen coordinates), and the live `actuate` branch is a stub.
3. **The latency "gate" flags and does not reject,** the interval ends when the state machine handles the cue (not when a key is sent),
   and stale or future cues are not filtered.
4. **State machine details.** The eight state names exist, but the diagram's `TimedOut` does not (a timeout is
   `CatchResolved { CastTimedOut }`), `CatchResolved -> Idle` needs `reset_to_idle`, and only `WaitingForBite` has a timeout.
5. **Telemetry is thinner than listed.** There is no rarity, weight, strike-accuracy or energy-efficiency figure; the success rate is a
   fraction; `FishEscaped` is `FishGotAway` or `StruckTooEarly`, supplied by the caller.
6. **Energy.** "Verified energy availability" is a caller-set counter, every lure rank costs 3 (the doc said 2-5 by rank), and the energy
   check happens once.
7. **The fishing mechanics in section 1** (the `Shift+F` hotkey, 1-3 nibbles, a 1.0-1.5 s strike window, the energy costs, the loud
   high-frequency bite sound, fish perception cones) cite no source here and none exists in the repository's other documents; they were
   not checked.
8. **Where the document was amended:** a status banner, **Today** notes under section 1 (steps and audio), the diagram, sections 2, 3 and
   4, a rewrite of the late-strike item, sections 5 and 6, and the `INDEX.md` line.
