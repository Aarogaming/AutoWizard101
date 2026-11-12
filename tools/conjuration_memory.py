"""Offline Conjuration memory decisions from supplied observations; never clicks."""

from dataclasses import dataclass


@dataclass(frozen=True)
class Card:
    slot: str
    x: int
    y: int
    state: str  # hidden, face_up, removed, unknown
    identity: str | None = None


@dataclass(frozen=True)
class Board:
    board_id: str
    revision: int
    phase: str  # preview, active, animation, result, unknown
    cards: tuple[Card, ...]


@dataclass(frozen=True)
class FlipProposal:
    proposal_id: str
    board_id: str
    observation_revision: int
    slot: str
    x: int
    y: int
    reason: str


@dataclass(frozen=True)
class DispatchReceipt:
    proposal_id: str
    status: str  # dispatched, rejected, unknown; none establishes a game outcome


@dataclass(frozen=True)
class ObservedOutcome:
    board_id: str
    revision: int
    kind: str
    slots: tuple[str, ...]


class MemoryMatch:
    """One explicitly identified board with a caller-qualified matching group size.

    Supply complete board snapshots, using unknown for unreadable cards. Only
    stable preview/active identities enter memory. A proposal reserves one action;
    its receipt and a later positive reveal are separate steps. No timeout is
    guessed: an uncertain dispatch waits until reconciled or explicitly reset.
    """

    def __init__(self, board_id: str, group_size: int):
        self._serial = 0
        self._board_id: str | None = None
        self.reset(board_id, group_size)

    def reset(self, board_id: str, group_size: int) -> None:
        if not isinstance(board_id, str) or not board_id.strip() or len(board_id) > 128:
            raise ValueError(
                "Use a nonempty local board identifier of at most 128 characters"
            )
        if board_id == self._board_id:
            raise ValueError("A reset requires a new board identifier")
        if type(group_size) is not int or not 2 <= group_size <= 8:
            raise ValueError("Explicitly qualify a group size between 2 and 8")
        self._board_id, self._group_size = board_id, group_size
        self._board: Board | None = None
        self._positions: dict[str, tuple[int, int]] = {}
        self._memory: dict[str, str] = {}
        self._removed: set[str] = set()
        self._proposal: FlipProposal | None = None
        self._pending_slot: str | None = None
        self._dispatch_revision = -1
        self._awaiting: tuple[str, ...] = ()
        self._previous_faces: set[str] = set()
        self._blocked: str | None = None
        self.reason = "waiting-for-board"

    @property
    def group_size(self) -> int:
        return self._group_size

    @property
    def memory(self) -> dict[str, str]:
        return dict(self._memory)

    def observe(self, board: Board) -> ObservedOutcome | None:
        self._validate(board)
        if self._board and board.revision <= self._board.revision:
            raise ValueError("Observation revisions must strictly increase")
        positions = {card.slot: (card.x, card.y) for card in board.cards}
        if self._positions and positions != self._positions:
            raise ValueError(
                "Board positions changed; explicitly reset with a new board ID"
            )
        if self._blocked == "dispatch-rejected-awaiting-new-observation":
            self._blocked = None
        stable = board.phase in {"preview", "active"}
        reveals = {
            card.slot: card.identity
            for card in board.cards
            if stable and card.state == "face_up"
        }
        if self._blocked is None and any(
            slot in self._memory and self._memory[slot] != label
            for slot, label in reveals.items()
        ):
            self._blocked = "conflicting-card-identity"
        if (
            self._blocked is None
            and stable
            and any(
                card.slot in self._removed and card.state in {"hidden", "face_up"}
                for card in board.cards
            )
        ):
            self._blocked = "removed-card-reappeared-without-reset"
        self._board = board
        self._positions = positions
        if self._blocked:
            self.reason = self._blocked
            return None
        self._memory.update(reveals)
        if stable:
            self._removed.update(
                card.slot for card in board.cards if card.state == "removed"
            )
        if board.phase != "active" or any(
            card.state == "unknown" for card in board.cards
        ):
            self.reason = "waiting-for-stable-active-board"
            return None
        cards = {card.slot: card for card in board.cards}
        if self._pending_slot is not None:
            if board.revision <= self._dispatch_revision or cards[
                self._pending_slot
            ].state not in {"face_up", "removed"}:
                self.reason = "waiting-for-dispatched-flip"
                return None
            # A removal without a known complete group is not evidence of success.
            if cards[self._pending_slot].state == "removed":
                self._blocked = self.reason = "flip-removed-without-observed-group"
                return None
            self._pending_slot = None
        outcome = None
        if self._awaiting:
            states = {cards[slot].state for slot in self._awaiting}
            labels = {self._memory[slot] for slot in self._awaiting}
            if states == {"removed"}:
                kind = "match_observed" if len(labels) == 1 else "conflicting_removal"
                if kind == "conflicting_removal":
                    self._blocked = kind
            elif states == {"hidden"}:
                kind = (
                    "mismatch_observed"
                    if len(labels) > 1
                    else "expected_match_not_observed"
                )
                if kind == "expected_match_not_observed":
                    self._blocked = kind
            else:
                self.reason = "waiting-for-group-outcome"
                return None
            outcome = ObservedOutcome(
                self._board_id, board.revision, kind, self._awaiting
            )
            self._awaiting = ()
            self._previous_faces.clear()
        faces = {slot for slot, card in cards.items() if card.state == "face_up"}
        if self._previous_faces - faces and outcome is None:
            self._blocked = "selection-disappeared-without-outcome"
        if len(faces) > self.group_size:
            self._blocked = "too-many-visible-selected-cards"
        elif len(faces) == self.group_size:
            self._awaiting = tuple(sorted(faces))
        self._previous_faces = faces
        self.reason = self._blocked or (
            "waiting-for-group-outcome" if self._awaiting else "ready"
        )
        return outcome

    def propose(self) -> FlipProposal | None:
        board = self._board
        if self._blocked:
            self.reason = self._blocked
            return None
        if self._proposal is not None:
            self.reason = "waiting-for-dispatch-receipt"
            return None
        if self._pending_slot is not None:
            self.reason = "waiting-for-dispatched-flip"
            return None
        if self._awaiting:
            self.reason = "waiting-for-group-outcome"
            return None
        if (
            board is None
            or board.phase != "active"
            or any(card.state == "unknown" for card in board.cards)
        ):
            self.reason = "waiting-for-stable-active-board"
            return None
        hidden = sorted(
            (card for card in board.cards if card.state == "hidden"),
            key=lambda c: (c.y, c.x, c.slot),
        )
        faces = [card for card in board.cards if card.state == "face_up"]
        if not hidden:
            self.reason = (
                "board-cleared-observed"
                if all(c.state == "removed" for c in board.cards)
                else "no-hidden-cards"
            )
            return None
        selected = None
        if faces and len({card.identity for card in faces}) == 1:
            selected = next(
                (
                    card
                    for card in hidden
                    if self._memory.get(card.slot) == faces[0].identity
                ),
                None,
            )
        elif not faces:
            selected = next(
                (
                    card
                    for card in hidden
                    if card.slot in self._memory
                    and sum(
                        self._memory.get(other.slot) == self._memory[card.slot]
                        for other in hidden
                    )
                    >= self.group_size
                ),
                None,
            )
        reason = "complete-known-group"
        if selected is None:
            selected = next(
                (card for card in hidden if card.slot not in self._memory), None
            )
            reason = "learn-unseen-card"
        if selected is None and faces:
            selected, reason = hidden[0], "complete-observed-selection"
        if selected is None:
            self.reason = "no-known-complete-group-rule-review-needed"
            return None
        self._serial += 1
        self._proposal = FlipProposal(
            f"{self._board_id}:{self._serial}",
            self._board_id,
            board.revision,
            selected.slot,
            selected.x,
            selected.y,
            reason,
        )
        self.reason = "proposal-awaiting-dispatch"
        return self._proposal

    def record_dispatch(self, receipt: DispatchReceipt) -> None:
        if self._proposal is None or receipt.proposal_id != self._proposal.proposal_id:
            raise ValueError("Receipt does not identify the outstanding proposal")
        if receipt.status not in {"dispatched", "rejected", "unknown"}:
            raise ValueError(
                "Use dispatched, rejected, or unknown; a receipt is not a game result"
            )
        if (
            receipt.status != "rejected"
            and self._proposal.observation_revision != self._board.revision
        ):
            self._blocked = self._blocked or "dispatch-based-on-outdated-observation"
            self.reason = self._blocked
            self._proposal = None
            return
        if receipt.status == "rejected":
            # Even a known non-dispatch needs a newer observation before a retry.
            # Its receipt cannot clear an independent board/identity conflict.
            self._blocked = (
                self._blocked or "dispatch-rejected-awaiting-new-observation"
            )
        else:
            self._pending_slot = self._proposal.slot
            self._dispatch_revision = self._board.revision
        self._proposal = None
        self.reason = self._blocked or "dispatch-" + receipt.status

    def _validate(self, board: Board) -> None:
        if not isinstance(board.cards, tuple) or any(
            not isinstance(card, Card) for card in board.cards
        ):
            raise ValueError("Use an immutable tuple of Card observations")
        if board.board_id != self._board_id:
            raise ValueError("Different board: explicitly reset first")
        if type(board.revision) is not int or board.revision < 0:
            raise ValueError("Use a nonnegative integer observation revision")
        if board.phase not in {"preview", "active", "animation", "result", "unknown"}:
            raise ValueError("Unknown board phase")
        if not self.group_size <= len(board.cards) <= 64:
            raise ValueError(
                "Provide the complete board, at least one group and at most 64 slots"
            )
        slots, positions = set(), set()
        for card in board.cards:
            if (
                not isinstance(card.slot, str)
                or not card.slot
                or len(card.slot) > 128
                or card.slot in slots
            ):
                raise ValueError("Card slots must be unique nonempty local IDs")
            if (
                any(
                    type(value) is not int or not 0 <= value <= 16384
                    for value in (card.x, card.y)
                )
                or (card.x, card.y) in positions
            ):
                raise ValueError(
                    "Use unique bounded integer card centers relative to the game image"
                )
            if card.state not in {"hidden", "face_up", "removed", "unknown"}:
                raise ValueError("Unknown card state")
            if card.state == "face_up":
                if (
                    not isinstance(card.identity, str)
                    or not card.identity
                    or len(card.identity) > 128
                ):
                    raise ValueError(
                        "A visible identity is required for a face-up card"
                    )
            elif card.identity is not None:
                raise ValueError("Only a visible face-up card may supply an identity")
            slots.add(card.slot)
            positions.add((card.x, card.y))
