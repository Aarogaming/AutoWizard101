# Evidence Report: W101-FISHING-DOCS-01

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
