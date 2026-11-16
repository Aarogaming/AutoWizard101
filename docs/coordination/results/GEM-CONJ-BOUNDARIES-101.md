# GEM-CONJ-BOUNDARIES-101 — Conjuration Concentration Memory Boundary Contract Tests

**Task ID**: `GEM-CONJ-BOUNDARIES-101`  
**Workstream**: `W101-CONJURATION`  
**Author**: Gemini  
**Date**: 2026-11-14T22:10:00-05:00  
**Repository Revision**: `6dd15e5` (`maelstrom/rust-architecture-v1`; audited baseline `7888cf251388dd904bac3977c70f73d0a6a31424`)  
**Status**: VERIFIED & FORMATTED OFFLINE TEST IMPLEMENTATION (4/4 PASS IN ROOT SLOT)  
**Owned Outputs**:  
- `tools/tests/test_conjuration_boundary_contract.py`  
- `docs/coordination/results/GEM-CONJ-BOUNDARIES-101.md`  

---

## 1. Executive Summary & Purpose

This report fulfills directive `LIA-0052` and the formatting follow-up in `LIA-0053`. It documents the implementation and verification of four nonredundant, pure offline standard library `unittest` cases in [`tools/tests/test_conjuration_boundary_contract.py`](file:///F:/Modlists/AutoWizard101/tools/tests/test_conjuration_boundary_contract.py).

### 1.1 Invariants & Boundaries Preserved
- **Zero Production Edits**: No changes made to [`tools/conjuration_memory.py`](file:///F:/Modlists/AutoWizard101/tools/conjuration_memory.py) or any other production source file.
- **Strict Interface Fidelity**: All authored tests operate strictly against the existing, published `MemoryMatch`, `Board`, `Card`, and `DispatchReceipt` dataclasses and methods without inventing new properties or mock wrappers.
- **Unsupported Gaps Documented**: Rather than authoring knowingly failing tests for unsupported features, missing runtime features are explicitly documented in Section 4.
- **Zero Live Claims**: This deliverable constitutes a pure offline unit test contract; zero live Conjuration gameplay support is claimed.

### 1.2 Root Validation & Formatter Execution Record
- **Root Build Slot Validation (`LIA-0053`)**: Root coordinator executed the test suite in the shared build slot:
  - Unit Tests: All 4 boundary tests passed (`4/4 passed`).
  - Syntax Check: `compileall` passed cleanly.
- **Authorized Formatter Invocation (`LIA-0053`)**:
  - `python -m black tools/tests/test_conjuration_boundary_contract.py` reformatted the file.
  - Verified clean via `python -m black --check tools/tests/test_conjuration_boundary_contract.py` (exit code 0, 1 file would be left unchanged).
  - Syntax confirmed via `python -m compileall -q tools/tests/test_conjuration_boundary_contract.py` (exit code 0).

---

## 2. Test Suite Specification: `ConjurationBoundaryContractTests`

The four authored tests target critical boundary conditions in `tools/conjuration_memory.py` that were previously uncovered or under-tested in `tools/tests/test_conjuration_memory.py`:

```
+---+---------------------------------------------------------------------------------------+---------------------------------------+
| # | Test Method Name                                                                      | Primary Boundary Contract Evaluated   |
+---+---------------------------------------------------------------------------------------+---------------------------------------+
| 1 | test_pending_flip_with_missing_or_unknown_reveal_withholds_further_action             | Ambiguous / Missing Reveal Evidence   |
| 2 | test_too_many_visible_face_up_cards_blocks_engine                                     | Multi-card Contradiction Latching     |
| 3 | test_board_reset_clears_blocked_error_and_rejects_identical_id                        | Board/Session Reset Invariants        |
| 4 | test_known_pair_with_contradictory_second_card_reveal_blocks_solver                   | Stale / Contradictory Pair Knowledge  |
+---+---------------------------------------------------------------------------------------+---------------------------------------+
```

---

## 3. Detailed Trace & Nonredundancy Mapping

### 3.1 Test 1: `test_pending_flip_with_missing_or_unknown_reveal_withholds_further_action`
- **Location**: `tools/tests/test_conjuration_boundary_contract.py:18-73`.
- **Underlying Source Code Traced**:
  - [conjuration_memory.py:133-137](file:///F:/Modlists/AutoWizard101/tools/conjuration_memory.py#L133-L137):
    ```python
    if board.phase != "active" or any(card.state == "unknown" for card in board.cards):
        self.reason = "waiting-for-stable-active-board"
        return None
    ```
  - [conjuration_memory.py:139-144](file:///F:/Modlists/AutoWizard101/tools/conjuration_memory.py#L139-L144):
    ```python
    if self._pending_slot is not None:
        if board.revision <= self._dispatch_revision or cards[self._pending_slot].state not in {"face_up", "removed"}:
            self.reason = "waiting-for-dispatched-flip"
            return None
    ```
- **Scenario Tested**:
  1. A flip is proposed for slot `"0"` and recorded as `"dispatched"`.
  2. Frame 2 arrives where slot `"0"` remains `"hidden"` (missing reveal evidence, e.g. early frame capture or missed click). Verifies that `solver.reason == "waiting-for-dispatched-flip"` and `propose()` returns `None`.
  3. Frame 3 arrives where slot `"0"` transitions to `"unknown"` (e.g. motion blur or camera occlusion). Verifies that `solver.reason == "waiting-for-stable-active-board"` and `propose()` returns `None`.
  4. Frame 4 arrives with slot `"0"` cleanly `"face_up"` (`identity="Star"`). Verifies that `_pending_slot` is cleared, `solver.reason == "ready"`, memory is updated, and the next flip (slot `"1"`) is admitted.
- **Nonredundancy vs. `test_conjuration_memory.py`**: Prior tests only covered a slot staying hidden across repeated frames; this test explicitly exercises the transition through transient `"unknown"` states during an in-flight pending dispatch, ensuring the pending latch is not discarded or corrupted.

---

### 3.2 Test 2: `test_too_many_visible_face_up_cards_blocks_engine`
- **Location**: `tools/tests/test_conjuration_boundary_contract.py:74-98`.
- **Underlying Source Code Traced**:
  - [conjuration_memory.py:177-178](file:///F:/Modlists/AutoWizard101/tools/conjuration_memory.py#L177-L178):
    ```python
    if len(faces) > self.group_size:
        self._blocked = "too-many-visible-selected-cards"
    ```
  - [conjuration_memory.py:189-191](file:///F:/Modlists/AutoWizard101/tools/conjuration_memory.py#L189-L191):
    ```python
    if self._blocked:
        self.reason = self._blocked
        return None
    ```
- **Scenario Tested**:
  1. For a `group_size = 2` board, an observation is supplied where three cards (`"0"`, `"1"`, `"2"`) are simultaneously `"face_up"`.
  2. Verifies `observe()` returns `None` and latches `self._blocked = "too-many-visible-selected-cards"`.
  3. Verifies `propose()` returns `None`.
  4. Verifies that subsequent frames—even if all cards return to `"hidden"`—cannot clear the blocked state without an explicit `reset()`.
- **Nonredundancy vs. `test_conjuration_memory.py`**: The `too-many-visible-selected-cards` branch in `conjuration_memory.py:177` had zero test coverage across the entire test suite.

---

### 3.3 Test 3: `test_board_reset_clears_blocked_error_and_rejects_identical_id`
- **Location**: `tools/tests/test_conjuration_boundary_contract.py:100-136`.
- **Underlying Source Code Traced**:
  - [conjuration_memory.py:63-68](file:///F:/Modlists/AutoWizard101/tools/conjuration_memory.py#L63-L68):
    ```python
    if not isinstance(board_id, str) or not board_id.strip() or len(board_id) > 128:
        raise ValueError("Use a nonempty local board identifier of at most 128 characters")
    if board_id == self._board_id:
        raise ValueError("A reset requires a new board identifier")
    ```
- **Scenario Tested**:
  1. The solver is forced into an irrecoverable blocked state (`too-many-visible-selected-cards`).
  2. Calling `reset("board-reset-test", 2)` with the **same** board ID raises `ValueError` (asserted via `with self.assertRaises(ValueError)`), proving that callers cannot silently reset internal state without establishing a fresh board context.
  3. Calling `reset` with invalid IDs (`""`, `"   "`, `None`, or $>128$ chars) raises `ValueError` / `TypeError`.
  4. Calling `reset("board-reset-clean", 2)` with a distinct valid ID cleanly clears `_blocked`, resets `reason` to `"waiting-for-board"`, empties `memory`, and allows fresh observations to proceed to `"ready"` and emit proposals.
- **Nonredundancy vs. `test_conjuration_memory.py`**: Existing tests verified that old proposals fail after reset, but did not test error recovery from an active `_blocked` latch, whitespace-only board IDs, or ID length boundaries.

---

### 3.4 Test 4: `test_known_pair_with_contradictory_second_card_reveal_blocks_solver`
- **Location**: `tools/tests/test_conjuration_boundary_contract.py:138-196`.
- **Underlying Source Code Traced**:
  - [conjuration_memory.py:109-113](file:///F:/Modlists/AutoWizard101/tools/conjuration_memory.py#L109-L113):
    ```python
    if self._blocked is None and any(
        slot in self._memory and self._memory[slot] != label
        for slot, label in reveals.items()
    ):
        self._blocked = "conflicting-card-identity"
    ```
  - [conjuration_memory.py:221-229](file:///F:/Modlists/AutoWizard101/tools/conjuration_memory.py#L221-L229):
    Proposing the second card of a group based on matching `self._memory.get(card.slot) == faces[0].identity`.
- **Scenario Tested**:
  1. Preview phase teaches a pair: slot `"0"` = `"A"` and slot `"1"` = `"A"`. Memory stores `{"0": "A", "1": "A"}`.
  2. Board becomes active with all cards hidden.
  3. Solver proposes slot `"0"` (`reason="complete-known-group"`). Dispatched and observed face-up as `"A"`.
  4. Solver proposes slot `"1"` (`reason="complete-known-group"`). Dispatched.
  5. Frame 4 reveals slot `"1"` as `"C"` instead of the remembered `"A"` (contradictory reveal caused by an unexpected board change, reshuffle, or OCR misclassification).
  6. Verifies that `observe()` returns `None`, latches `self._blocked = "conflicting-card-identity"`, withholds subsequent proposals, and preserves the historical memory without silent corruption.
- **Nonredundancy vs. `test_conjuration_memory.py`**: Prior tests tested contradictory identity when an already-remembered card flipped during preview or before dispatch. This test explicitly verifies the in-flight selection failure sequence where the second card of a pair contradicts stored memory.

---

## 4. Unsupported Boundary Gaps in Current API

In accordance with `LIA-0052` ("If a needed behavior is unsupported, document the precise gap rather than writing knowingly failing tests"), the following boundaries are currently unsupported in `tools/conjuration_memory.py`:

1. **Wall-Clock Timeout Enforcement**:
   - `MemoryMatch` operates entirely on discrete revisions (`board.revision`). It possesses no concept of timestamps (`time_ms`), maximum wait duration, or deadman switches. If a dispatched flip never arrives, `MemoryMatch` waits indefinitely with `reason = "waiting-for-dispatched-flip"`.
   - *Requirement*: The outer host / scheduler lease must enforce an active execution deadline.
2. **Dynamic Group Size Inference**:
   - `MemoryMatch` requires the caller to explicitly specify `group_size` at initialization and reset (bounded to integers $2 \le N \le 8$). It cannot infer group size from card counts or game level text.
   - *Requirement*: Live OCR or game rules metadata must supply the group size.
3. **Card Removal Animation Disambiguation**:
   - If two matching cards disappear simultaneously without an intervening frame marked `"removed"`, `MemoryMatch` requires the cards to transition from `face_up` directly to `removed`. If they transition to `hidden`, `MemoryMatch` treats it as `expected_match_not_observed` and latches an error.
   - *Requirement*: The visual recognizer must reliably observe the visual disappearance/removal banner.

---

## 5. Coordination & Verification Record

- **Reviewed Directives**: `LIA-0051` (prior inventory review), `LIA-0052` (task 101 activation), `GEM-0057` (task ACK), `LIA-0053` (root validation & Black formatting directive), `LIA-0054` (shared ownership context).
- **Git HEAD Verified**: `7888cf251388dd904bac3977c70f73d0a6a31424`.
- **Validation Record**: 4/4 tests passed in root build slot; `black --check` passed clean; `compileall -q` passed clean.
- **Zero Modification Invariant**: No production source code modified (`tools/conjuration_memory.py` untouched); zero live game interaction.
- **Deliverables**:
  - Test suite: [`tools/tests/test_conjuration_boundary_contract.py`](file:///F:/Modlists/AutoWizard101/tools/tests/test_conjuration_boundary_contract.py)
  - Report: [`docs/coordination/results/GEM-CONJ-BOUNDARIES-101.md`](file:///F:/Modlists/AutoWizard101/docs/coordination/results/GEM-CONJ-BOUNDARIES-101.md)
