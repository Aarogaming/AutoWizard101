# Claim Evidence: W101-COMBAT-DUEL-DOCS-01

Status: **Partially verified** (audited 2026-11-20; the document exists, is indexed and its arithmetic is right, but the lifecycle, bounding boxes,
jitter figure and several actuator invariants it formalizes are not what the code does; see "Audit note" at the end).

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

## Audit note (2026-11-20, Claude)

Checked `docs/COMBAT_DUELING_LOOP.md` against `combat_geometry.rs`, `behavior_tree.rs`, `maelstrom_bridge.rs`, `kinematics.rs`,
`DesktopInputActuator.cs`, `JobDeckHost.cs` and a throwaway probe crate. The document now carries a status block and **Today** notes.

1. **"Formalized 3-stage combat turn lifecycle" is not a lifecycle in code.** There is one blackboard string that tests set and nothing clears;
   there are no stages, preconditions or transitions, and no hand or target evaluation. The intent format is `PlayCard:<slot>/<total>` and
   `SelectTarget:<index>`, not parameters.
2. **The coordinate matrix is arithmetically right and unmeasured.** The values match the code. "Grounded" and "calibrated" mean scaled from
   800x600; no 1280x720 measurement is recorded.
3. **Bounding boxes and jitter were misdescribed.** Pedestal boxes are 50 x 50 (+/- 25 px), not +/- 35; Pass is 36 x 20; Flee has no click path.
   The sampling is a Gaussian centered on the box with a standard deviation of about 0.17 of its size (0.15 is the margin ratio), not
   "non-centered" with sigma = 0.15.
4. **Several actuator invariants were not in the actuator.** Lease identity, the cancellation token and the 10 ms re-check are real, but during
   the dwell only (there is no trajectory to poll). Foreground and focus come from a caller-supplied `ready()`, there is no countdown
   enforcement or "> 2 px" pointer check, and release is a retried send whose boolean is the confirmation, with a recovery hold on failure. It
   does not call `mouse_event`.
5. **The "Ban Invariant" and "robotic telemetry signature" rationale is unsourced**, and the document now describes the click placement only as
   what it does. The preemption claim is also wrong: the tree does not cancel pending clicks (nothing links `in_combat` to the actuator's
   token), and a stale `combat_turn_action` survives the fight.
6. **Acceptance criterion 4 is untested end to end.** No Rust code sends `actuate`; the host's live branch sends no input.
7. **Section 10 of `ROUTINE_EXECUTION_MODEL.md` and the `INDEX.md` entry exist** and now say what is and is not built. `claims.json`
   (coordinator-owned, not edited) cites `docs/W101_COMBAT_DUELING_REPERTOIRE.md`, which does not exist, and "1080p" models the document does
   not contain.
8. **Gate lines held up.** On 2026-11-20 `check_project_board.py` reports 30 workstreams and `check_text_encoding.py` reports 0 violations.
