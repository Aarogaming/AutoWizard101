"""Potion Motion pure board evaluator and candidate move planner.

Models rectangular boards, row/column displacements, 4-neighbor connected groups
(including bent L/T shapes), explicit wrapping semantics, and candidate scoring
without live desktop input or UI dependencies.
"""

from __future__ import annotations

from collections import deque
from dataclasses import dataclass
from typing import Any, Dict, List, Optional, Set, Tuple


@dataclass(frozen=True)
class MoveProposal:
    move_type: str  # "row" or "column"
    index: int
    displacement: int
    score: int
    matched_cell_count: int
    groups: List[List[Tuple[int, int]]]
    allow_wrapping: bool

    def to_dict(self) -> Dict[str, Any]:
        return {
            "move_type": self.move_type,
            "index": self.index,
            "displacement": self.displacement,
            "score": self.score,
            "matched_cell_count": self.matched_cell_count,
            "group_count": len(self.groups),
            "groups": self.groups,
            "allow_wrapping": self.allow_wrapping,
        }


class PotionMotionBoard:
    """Immutable representation of a Potion Motion grid."""

    UNKNOWN_TOKENS = frozenset({"unknown", "empty", "none", "", "?"})

    def __init__(self, grid: List[List[str]]):
        if not grid or not isinstance(grid, list):
            raise ValueError("Board grid must be a non-empty 2D list.")
        self._rows = len(grid)
        self._cols = len(grid[0])
        if self._rows == 0 or self._cols == 0:
            raise ValueError("Board dimensions must be positive.")
        for r_idx, row in enumerate(grid):
            if not isinstance(row, list) or len(row) != self._cols:
                raise ValueError(
                    f"Inconsistent column count at row {r_idx}; expected {self._cols}."
                )
        # Store as immutable tuple of tuples of normalized strings
        self._grid: Tuple[Tuple[str, ...], ...] = tuple(
            tuple((c.lower().strip() if isinstance(c, str) else "unknown") for c in row)
            for row in grid
        )

    @property
    def rows(self) -> int:
        return self._rows

    @property
    def cols(self) -> int:
        return self._cols

    def get_cell(self, r: int, c: int) -> str:
        if 0 <= r < self._rows and 0 <= c < self._cols:
            return self._grid[r][c]
        raise IndexError(f"Cell ({r}, {c}) out of bounds ({self._rows}x{self._cols}).")

    def to_list(self) -> List[List[str]]:
        return [list(row) for row in self._grid]

    def shift_row(
        self, row_idx: int, delta: int, allow_wrapping: bool = False
    ) -> PotionMotionBoard:
        """Shift a single row horizontally by delta (positive = right, negative = left)."""
        if not (0 <= row_idx < self._rows):
            raise IndexError(f"Row {row_idx} out of range [0, {self._rows - 1}].")
        if delta == 0:
            return self

        new_grid = [list(r) for r in self._grid]
        row = list(self._grid[row_idx])

        if allow_wrapping:
            effective_delta = delta % self._cols
            shifted_row = row[-effective_delta:] + row[:-effective_delta]
        else:
            shifted_row = ["unknown"] * self._cols
            for c in range(self._cols):
                target_c = c + delta
                if 0 <= target_c < self._cols:
                    shifted_row[target_c] = row[c]

        new_grid[row_idx] = shifted_row
        return PotionMotionBoard(new_grid)

    def shift_col(
        self, col_idx: int, delta: int, allow_wrapping: bool = False
    ) -> PotionMotionBoard:
        """Shift a single column vertically by delta (positive = down, negative = up)."""
        if not (0 <= col_idx < self._cols):
            raise IndexError(f"Column {col_idx} out of range [0, {self._cols - 1}].")
        if delta == 0:
            return self

        new_grid = [list(r) for r in self._grid]
        col = [self._grid[r][col_idx] for r in range(self._rows)]

        if allow_wrapping:
            effective_delta = delta % self._rows
            shifted_col = col[-effective_delta:] + col[:-effective_delta]
        else:
            shifted_col = ["unknown"] * self._rows
            for r in range(self._rows):
                target_r = r + delta
                if 0 <= target_r < self._rows:
                    shifted_col[target_r] = col[r]

        for r in range(self._rows):
            new_grid[r][col_idx] = shifted_col[r]

        return PotionMotionBoard(new_grid)

    def find_connected_groups(self, min_size: int = 3) -> List[List[Tuple[int, int]]]:
        """Find 4-connected components of identical non-unknown colors (min_size >= 3).

        Diagonal contact does NOT count. Bent L/T/cross shapes count.
        """
        visited: Set[Tuple[int, int]] = set()
        groups: List[List[Tuple[int, int]]] = []

        for r in range(self._rows):
            for c in range(self._cols):
                if (r, c) in visited:
                    continue

                color = self._grid[r][c]
                if color in self.UNKNOWN_TOKENS:
                    visited.add((r, c))
                    continue

                # BFS for 4-connected component
                component: List[Tuple[int, int]] = []
                queue: deque[Tuple[int, int]] = deque([(r, c)])
                visited.add((r, c))

                while queue:
                    cr, cc = queue.popleft()
                    component.append((cr, cc))

                    for dr, dc in [(-1, 0), (1, 0), (0, -1), (0, 1)]:
                        nr, nc = cr + dr, cc + dc
                        if 0 <= nr < self._rows and 0 <= nc < self._cols:
                            if (nr, nc) not in visited and self._grid[nr][nc] == color:
                                visited.add((nr, nc))
                                queue.append((nr, nc))

                if len(component) >= min_size:
                    # Sort component coordinates for deterministic output
                    component.sort()
                    groups.append(component)

        # Sort groups deterministically by starting cell
        groups.sort(key=lambda g: g[0])
        return groups

    def evaluate_board(
        self, min_size: int = 3
    ) -> Tuple[int, List[List[Tuple[int, int]]]]:
        """Calculates total match score and groups."""
        groups = self.find_connected_groups(min_size=min_size)
        total_cells = sum(len(g) for g in groups)
        # Score heuristic: larger groups receive non-linear bonus
        score = sum(len(g) * 10 + (len(g) - 3) * 5 for g in groups)
        return score, groups

    def find_best_moves(
        self,
        allow_wrapping: bool = False,
        limit: int = 10,
        max_displacement: Optional[int] = None,
    ) -> List[MoveProposal]:
        """Exhaustively simulates all valid single row and column shifts, returning top proposals."""
        proposals: List[MoveProposal] = []
        max_row_disp = max_displacement or (self._cols - 1)
        max_col_disp = max_displacement or (self._rows - 1)

        # 1. Row moves
        for r in range(self._rows):
            for delta in range(-max_row_disp, max_row_disp + 1):
                if delta == 0:
                    continue
                shifted = self.shift_row(r, delta, allow_wrapping=allow_wrapping)
                score, groups = shifted.evaluate_board()
                if score > 0:
                    proposals.append(
                        MoveProposal(
                            move_type="row",
                            index=r,
                            displacement=delta,
                            score=score,
                            matched_cell_count=sum(len(g) for g in groups),
                            groups=groups,
                            allow_wrapping=allow_wrapping,
                        )
                    )

        # 2. Column moves
        for c in range(self._cols):
            for delta in range(-max_col_disp, max_col_disp + 1):
                if delta == 0:
                    continue
                shifted = self.shift_col(c, delta, allow_wrapping=allow_wrapping)
                score, groups = shifted.evaluate_board()
                if score > 0:
                    proposals.append(
                        MoveProposal(
                            move_type="column",
                            index=c,
                            displacement=delta,
                            score=score,
                            matched_cell_count=sum(len(g) for g in groups),
                            groups=groups,
                            allow_wrapping=allow_wrapping,
                        )
                    )

        # Deterministic sorting: higher score, then higher matched cells, then tie-break on type/index/displacement
        proposals.sort(
            key=lambda p: (
                -p.score,
                -p.matched_cell_count,
                p.move_type,
                p.index,
                p.displacement,
            )
        )

        return proposals[:limit]
