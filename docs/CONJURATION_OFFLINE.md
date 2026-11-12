# Conjuration Concentration: offline memory decisions

Status: **offline candidate only**. This workstream supplies a small deterministic
decision helper, with no desktop access, game inputs, image recognition, network
calls, models or production dependencies. It is not enabled in the installed
W101 player and has not played Conjuration in the current client.

The subsequent [recognition layer](CONJURATION_RECOGNITION.md) and
[image-to-memory replay](coordination/results/CONJ-PERCEPTION-01.md) now feed this
helper from bounded saved images. Positive tests remain synthetic; the available
real samples contain instructions only. Phase and group-size recognition are not
implemented by the card matcher.

The existing [minigame research](MINIGAME_RESEARCH.md) records an initially exposed
board, matching revealed card pictures, a time limit, board transitions, and
historical evidence that later boards may require more than two matching cards.
The bounded repository search found no existing Conjuration solver to extend;
the earlier [legacy teacher review](W101_LEGACY_TEACHERS.md) also records that gap.
This implementation uses that saved research; it makes no new claim about current
client timings, bonus-card rules, group sizes, scores or rewards.

## What exists

- [conjuration_memory.py](../tools/conjuration_memory.py): pure card memory and
  one-action proposals. It prefers known matching groups, then learns unseen
  cards. A partially revealed selection prioritizes a known matching card.
- [test_conjuration_memory.py](../tools/tests/test_conjuration_memory.py): tests
  shuffled layouts, preview memory, delayed reveals, animations, uncertain and
  rejected dispatch, unexpected outcomes, reset, and contradictory observations.

The matching group size is a required argument, bounded to 2–8 for this prototype.
Two and three are tested in synthetic fixtures; this does **not** establish which
live level requires either. Up to 64 explicitly supplied slots are supported.
The policy adapts to each observed board; it contains no prerecorded click path.

## Observation and action contract

`MemoryMatch(board_id, group_size)` owns exactly one local board identity.
`Board(board_id, revision, phase, cards)` supplies a complete immutable tuple of
`Card(slot, x, y, state, identity)` records. Positions are card centers relative to
the game image; they are proposals, not screen coordinates or permission to click.
`board_id` and `slot` are Maelstrom-local identifiers, not official game IDs.

Phases are `preview`, `active`, `animation`, `result`, and `unknown`. Card states
are `hidden`, `face_up`, `removed`, and `unknown`. Only a visible `face_up` card may
carry an identity. Unreadable regions must be marked unknown, not guessed or
silently omitted. `removed` means independently observed as matched/inactive;
the future recognizer must establish how the current client displays that state.

The steps stay distinct:

1. `observe(board)` validates a strictly increasing revision, unchanged positions,
   and consistent identities. Stable previews and reveals teach memory. Preview,
   animation, unknown and result phases never authorize a flip.
2. `propose()` returns one `FlipProposal`, including a unique proposal ID, board,
   observation revision, slot, position and reason. Calling it again cannot emit
   another proposal while that action is outstanding.
3. An external harness supplies `record_dispatch(DispatchReceipt(id, status))`.
   Status is `dispatched`, `rejected`, or `unknown`; none means “matched.” Feed
   this receipt before observations caused by the action. A dispatched/uncertain
   receipt based on an outdated observation blocks the board for reconciliation.
4. A newer stable observation must show the selected card revealed. Hidden or
   unknown frames cannot silently retry a dispatched or uncertain click. An
   explicitly rejected action can be reconsidered after a newer observation.
5. After a complete revealed group, the helper waits for an observed outcome.
   Uniform identities followed by the whole group becoming inactive produce
   `match_observed`. Different identities returning hidden produce
   `mismatch_observed`. Partial or unknown transitions keep waiting.

If a supposedly matching group turns back over, or different identities disappear
together, the helper reports a conflict and withholds further proposals. It does
not invent success or repeatedly retry the same incorrect rule. Conflicting
identities and unexpected disappearing selections also require requalification.
An independent identity conflict survives later rejected or uncertain dispatch
receipts. Once a card is positively observed inactive, it cannot become hidden
or face-up again under the same board ID; that requires an explicit new-board
reset. Temporarily unreadable cards remain unknown and do not establish a reset.
Clearing the supplied board is distinct from winning the game or earning a reward.

`reset(new_board_id, group_size)` explicitly clears memory and invalidates pending
actions for a shuffled or newly recognized board. A fresh ID is required. The
helper does not guess that an unfamiliar screen is a new board. It owns no real
clock or guessed animation timeout; the surrounding workflow must provide finite
observation/attempt budgets and preserve unresolved evidence when stopping.

## Small example

```python
from conjuration_memory import Board, Card, DispatchReceipt, MemoryMatch

player = MemoryMatch("conjuration-board-001", group_size=2)
player.observe(Board("conjuration-board-001", 1, "active", (
    Card("left", 100, 100, "hidden"),
    Card("right", 180, 100, "hidden"),
)))
proposal = player.propose()  # A proposed flip; nothing is clicked.
player.record_dispatch(DispatchReceipt(proposal.proposal_id, "unknown"))
assert player.propose() is None  # Await evidence; never blindly repeat it.
```

This example supplies an uncertain **simulated** receipt. Production adapters must
report what their dispatcher actually did and retain the receipt separately from
the proposal and from subsequent game observations. The helper does not execute
code or actions from card identities.

## Validation and next gates

Run the small standard-library suite from the repository root:

```text
python -m unittest discover -s tools/tests -p test_conjuration_memory.py -v
```

The suite includes 60 seeded hidden-board episodes: 30 shuffled pair boards and
30 shuffled triple boards, each with four identity groups. Their simulator emits
the explicit protocol above; it is not a substitute for current-client evidence.
The seeds vary the positions, not the solver's assumed answers. No input device,
desktop capture or game process participates.

Before a live routine can use this helper, separately establish:

1. Current Conjuration instructions, required group size and special-card behavior.
2. Board/slot identity, image clustering, preview/selection/outcome phase detection,
   scale changes and new-board recognition from saved captures.
3. Replay accuracy against held-out observations, including uncertain/partial
   matches and the actual game's animation boundaries.
4. An adapter to Maelstrom's shared action registry and scheduler with fresh-target,
   geometry, focus, takeover, key/button release and finite-run protections.
5. A bounded supervised live run with verified outcomes and native Idle afterward.

Use the game pack to preserve recognition references, group-size qualification,
proposals, dispatch receipts and observed outcomes. Neither a successful synthetic
episode nor imported source material should automatically enable live controls.
