# Claim Evidence: W101-COMBAT-DUEL-DOCS-01

## 1. Overview
- **Task ID**: `W101-COMBAT-DUEL-DOCS-01`
- **Workstream**: `W101-COMBAT`
- **Owner**: `coordinator`
- **Scope**: Documentation for end-to-end combat dueling loop integration: Behavior Tree CombatTurn execution, CombatGeometry viewport scaling, card selection, target selection, pass fallback, and DesktopInputActuator kinematic click dispatch.

## 2. Key Deliverables
1. **Combat Dueling Loop Specification** (`docs/COMBAT_DUELING_LOOP.md`):
   - Formalized 3-stage combat turn lifecycle: Card Selection (`PROPOSAL:CombatTurn:PlayCard`), Target Selection (`PROPOSAL:CombatTurn:SelectTarget`), and Pass Fallback (`PROPOSAL:CombatTurn:Pass`).
   - Grounded coordinate transformation matrix mapping canonical 800x600 layout into standard 1280x720 base viewport ($1.6 \times$ horizontal, $1.2 \times$ vertical).
   - Documented bounding boxes and non-centered Gaussian jitter targeting for pass button (`(406.4, 477.6)`), flee button (`(873.6, 477.6)`), hand card slots, and enemy/ally pedestals.
   - Formalized execution invariants under `DesktopInputActuator` and `GameInputLease.Current` (exclusive lease check, mid-pulse focus polling, countdown enforcement, verified release).
2. **Updated Architecture Docs**:
   - `docs/ROUTINE_EXECUTION_MODEL.md`: Added Section 10 specifying end-to-end combat dueling loop integration.
   - `docs/INDEX.md`: Indexed `docs/COMBAT_DUELING_LOOP.md`.

## 3. Red Team / Blue Team Adversarial Audit
- **Robotic Precision & Ban Invariant**:
  - *Red Team Attack*: Clicking the dead-center pixel of cards or target pedestals on every round creates an obvious robotic telemetry signature.
  - *Blue Team Mitigation*: All targets in `COMBAT_DUELING_LOOP.md` use non-centered Gaussian sampling (`sample_target_in_rect` with $\sigma = 0.15 \cdot \text{bounds}$), coupled with Fitts's Law duration models and log-normal dwell time jitter ($40\text{ms} \le t_{\text{dwell}} \le 200\text{ms}$) under `DesktopInputActuator`.
- **Preemption & Mid-Turn Cancellation**:
  - *Red Team Attack*: If combat ends abruptly (e.g. enemy flees, teammate defeats boss, or player is defeated) during a card click glide, can the actuator click an unintended game screen?
  - *Blue Team Mitigation*: Mid-pulse polling every $10\text{ms}$ checks cancellation tokens and lease ownership. If `in_combat` flips to `false`, the Behavior Tree resets the combat branch and cancels pending kinematic clicks prior to actuation.

## 4. Verification Evidence
- `python tools/check_project_board.py`: Valid (30 workstreams, zero broken links).
- `python tools/check_text_encoding.py`: 0 violations.
