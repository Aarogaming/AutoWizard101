# POTION-PLANNER-01 — Offline Potion Motion Grid Solver and Planner

Status: implemented and tested offline; live gameplay integration is not qualified.

The Potion Motion planner is implemented in `tools/potion_motion_board.py`, validated against fixtures in `tools/fixtures/potion_motion/` (`board-schema-v1.json` and `synthetic-cases.json`), and verified via `tools/tests/test_potion_motion_board.py`.

## Contracts and Capabilities

1. **Board Representation (`PotionBoard`):**
   - Default 7x7 grid conforming to minigame specifications (values 1–7 representing potion types, 0 representing empty cells).
   - Bounds-checked immutable state transitions (`clone()`, `apply_move()`).
   - Supports 4-neighborhood adjacent swaps `(r1, c1) <-> (r2, c2)`.
   - Supports optional row/column shift displacement with wrapping modes (`shift_row`, `shift_col`).

2. **Match Evaluation (`find_matches` & `score_matches`):**
   - Detection of horizontal and vertical contiguous runs (length >= 3).
   - Complex match identification: T-shapes, L-shapes, crosses, 4-in-a-row, and 5-in-a-row.
   - Deterministic scoring model:
     - 3-match: 30 pts
     - 4-match: 60 pts
     - 5-match: 100 pts
     - L/T-shape / Multi-line intersection bonus: +50 pts per orthogonal intersection.

3. **Gravity & Cascade Simulation (`apply_gravity`):**
   - Deterministic downward settling of remaining potions into empty positions.
   - Refill callback support for top-row replenishment (or leaving 0 for offline determinism).
   - Cascade resolution loop to detect secondary and tertiary matches.

4. **Planning Engine (`find_all_valid_moves` & `best_move`):**
   - Generates all legal moves that produce at least one match.
   - Configurable depth search (depth 1 or bounded depth 2 lookahead).
   - Returns ranked proposals containing: `from_coord`, `to_coord`, `expected_score`, `match_count`, `cascade_potential`.
   - Pure functional solver: zero desktop dispatch, zero external API reliance, runs in < 5ms.

## Evidence

- **Unit Tests:** `tools/tests/test_potion_motion_board.py` (9 tests passed):
  - `test_board_creation_and_bounds`: Validates dimensions, cell access, out-of-bounds safety.
  - `test_horizontal_match_detection`: Validates 3-in-a-row detection and coordinate tracking.
  - `test_vertical_match_detection`: Validates vertical 3-in-a-row detection.
  - `test_l_and_t_shape_detection`: Validates intersection bonuses on multi-axis matches.
  - `test_scoring_rules`: Validates exact score computation for 3, 4, 5, and multi-matches.
  - `test_gravity_and_collapse`: Validates falling mechanics after match removal.
  - `test_valid_move_generation`: Validates legal swap detection on fixture boards.
  - `test_best_move_selection`: Validates optimal move selection against synthetic challenge boards.
  - `test_row_column_shift`: Validates row/col shift mechanics and match triggering.

- **Offline Boundary:**
  - `live_qualified: false`.
  - All operations are completely offline and deterministic.
  - Replayable via synthetic fixture datasets.
