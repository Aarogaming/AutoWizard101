# Replay diagnostics 145 — expose pairing and qualification scope

2026-11-15. Root implementation task for CORE-DELIVERY. No live game interaction.

## Change

`RoutineReplay.RunDetailed` now returns an immutable summary with routine/game/session IDs, evidence mode, completion/stopped reason, graph/manifest/event SHA-256 values, proposal and recorded-receipt counts, live qualification, and report path. Existing `Run(...) -> bool` remains as a compatibility wrapper.

The Session Timeline and saved-card review surfaces now show the routine/session pairing, stop reason, proposal/receipt counts, shortened graph/event hashes, report path, and an explicit `Live qualified: no`. This distinguishes a successful offline contract replay from game qualification and makes a `dispatch-unconfirmed` mismatch visible in the UI.

CLI `--replay-routine` still returns only an exit code on ordinary replay results; its implementation path is under the active DANCE-CAPTURE-82 claim and was not edited here. The detailed JSON report remains authoritative. No automatic correction or relabeling of session receipts was added.

## Verification

- Focused `RoutineReplayTests`: 15 passed, including a new mismatched graph/session test asserting IDs, reason, hashes, report parity, and `LiveQualified=false`.
- Scoped `dotnet format whitespace` and `dotnet format analyzers --verify-no-changes`: passed for the four owned C# paths.
- Release build with warnings as errors: passed, 0 warnings/errors.
- The test fixture replay previously demonstrated: matching draft graph completes 8/8 proposals and receipts; full graph with draft-specific receipt IDs stops `dispatch-unconfirmed`. That remains synthetic replay, not live qualification.
- `python tools/check_project_board.py`: passed after board/evidence updates.

Files: `RoutineReplay.cs`, `RoutineReplayTests.cs`, `RecordedCardReviewForm.cs`, `SessionTimelineForm.cs`.
