# Replay Diagnostics & Provenance Messaging Audit (GEM-REPLAY-DIAGNOSTICS-146)

**Date**: 2026-11-15  
**Workstream**: CORE-DELIVERY  
**Task ID**: `GEM-REPLAY-DIAGNOSTICS-146`  
**Reference Directives**: `LIA-0105`, `LIA-0106`, `LIA-0107`  
**Input Reference**: `docs/coordination/results/REPLAY-DIAGNOSTICS-145.md`, `W101-REATTACH-147`  
**Inspected Files**:
- `ProjectMaelstrom/ProjectMaelstrom/Utilities/RoutineReplay.cs` (lines 12–22, 101–123)
- `ProjectMaelstrom/ProjectMaelstrom/SessionTimelineForm.cs` (lines 310–313)
- `ProjectMaelstrom/ProjectMaelstrom/RecordedCardReviewForm.cs` (lines 52–55)
- `ProjectMaelstrom/ProjectMaelstrom.Tests/RoutineReplayTests.cs` (lines 70–94)
- `ProjectMaelstrom/ProjectMaelstrom/Program.cs` (lines 113–122)
**Scope**: Bounded read-only static review of the `REPLAY-DIAGNOSTICS-145` implementation and user-facing qualification messaging. Zero production or test code modified; zero uncoordinated test/build executions; zero live game or desktop operations.

---

## 1. Executive Summary

In task `REPLAY-DIAGNOSTICS-145`, root implemented `RoutineReplay.RunDetailed` and updated `SessionTimelineForm` and `RecordedCardReviewForm` to surface structured replay diagnostics, addressing the UI deficits identified in `GEM-DANCE-REPLAY-SCOPE-144`. 

Directive `LIA-0106` assigned `GEM-REPLAY-DIAGNOSTICS-146` to evaluate three specific architectural questions:
1. **UI Wording**: Does the updated UI wording accurately separate synthetic contract replay from recorded evidence?
2. **Actionability**: Are the surfaced SHA-256 hashes and stop details actionable for diagnosing mismatches?
3. **CLI Visibility**: What diagnostic information does CLI `--replay-routine` still hide, and what prevents its update?

This audit validates that the core pairing diagnostics (`RoutineId`, `SessionId`, `Reason`, `ProposalCount`, `ReceiptCount`, shortened hashes, and explicit `Live qualified: no`) represent a major leap in observability over prior binary status messages. It also inventories residual gaps in evidence provenance classification and CLI stdout visibility, framing bounded recommendations for follow-up work.

---

## 2. Technical Evaluation of Implemented Architecture (`REPLAY-DIAGNOSTICS-145`)

### 2.1 The `RoutineReplay.Summary` Record and `RunDetailed`
In `ProjectMaelstrom/ProjectMaelstrom/Utilities/RoutineReplay.cs:12–14`, root introduced an immutable record:
```csharp
internal sealed record Summary(string RoutineId, string GameId, string SessionId, string EvidenceMode,
    bool Completed, bool Stopped, string? Reason, string GraphSha256, string ManifestSha256,
    string EventsSha256, int ProposalCount, int ReceiptCount, bool LiveQualified, string ReportPath);
```
- **Signature Compatibility**: `RoutineReplay.Run(...) -> bool` is preserved as a one-line delegate to `RunDetailed(...).Completed` (`RoutineReplay.cs:16–18`), preventing breaking changes across existing test suites and headless callers.
- **Fail-Safe Live Qualification**: `LiveQualified` is unconditionally populated as `false` (`RoutineReplay.cs:122`), ensuring that offline replay cannot inadvertently grant or imply live execution authority.
- **Trace Parity**: All report telemetry written to disk (`RoutineReplay.cs:110–120`) is mirrored directly into the returned in-memory `Summary` object.

### 2.2 Test Verification in `RoutineReplayTests.cs`
In `ProjectMaelstrom.Tests/RoutineReplayTests.cs:70–94`, the newly added test `DetailedReplayExplainsRoutineSessionPairingAndKeepsOfflineScope` specifically verifies the exact pairing mismatch from Finding 143:
- Replays graph `pet-dance-visit` against fixture session `routine-dance-synthetic`.
- Asserts that `result.RoutineId == "pet-dance-visit"` and `result.SessionId == "routine-dance-synthetic"`.
- Asserts `result.Completed == false`, `result.Stopped == true`, and `result.Reason == "dispatch-unconfirmed"`.
- Asserts proposal count is 1 and receipt count is 8.
- Asserts `result.LiveQualified == false`.
- Asserts 64-character SHA-256 digests for graph and event streams.

This unit test provides rigorous regression coverage ensuring that mismatched proposal IDs halt execution cleanly and populate the summary fields correctly.

---

## 3. Detailed Audit of Core Questions (`LIA-0106`)

### 3.1 Question 1: Does UI Wording Accurately Separate Synthetic Contract Replay from Recorded Evidence?

#### Current UI Implementation
In `SessionTimelineForm.cs:311–313`:
```csharp
_status.Text = $"Offline replay {(result.Completed ? "completed" : "stopped")}: {result.RoutineId} / {result.SessionId}. " +
    $"Reason: {result.Reason ?? "observed completion"}. {result.ProposalCount} proposals, {result.ReceiptCount} receipts. " +
    $"Graph {result.GraphSha256[..12]}, events {result.EventsSha256[..12]}. Live qualified: no. No game input sent. Report: {result.ReportPath}";
```
In `RecordedCardReviewForm.cs:53–55`:
```csharp
status.Text = $"Offline replay {(result.Completed ? "completed" : "stopped")}: {result.RoutineId} / {result.SessionId}. " +
    $"Reason: {result.Reason ?? "observed completion"}. {result.ProposalCount} proposals, {result.ReceiptCount} receipts. " +
    $"Graph {result.GraphSha256[..12]}, events {result.EventsSha256[..12]}. Live qualified: no. Report: {result.ReportPath}";
```

#### Assessment
1. **Strengths**:
   - The wording explicitly states **`Offline replay`** and **`Live qualified: no`**, as well as **`No game input sent`** in `SessionTimelineForm`.
   - This eliminates the previous ambiguity where a successful replay produced an unqualified `"Replay contract completed."` or `"Recording test passed"`.
2. **Residual Ambiguity (Synthetic vs Recorded)**:
   - The UI distinguishes sessions by displaying `{result.RoutineId} / {result.SessionId}`.
   - When a synthetic fixture is replayed, the user sees `routine-dance-synthetic`, which communicates synthetic origin *only* if the session ID happens to contain the substring `"synthetic"`.
   - In `RoutineReplay.cs:112, 121`, `evidence_mode` / `result.EvidenceMode` is hardcoded to `"labelled-state-replay"` for both synthetic test fixtures and recorded human sessions.
   - In `SessionTimelineForm.cs:299`, human-exported routines explicitly record `annotation_source: "human"`. However, `RoutineReplay.RunDetailed` does not inspect `manifest.annotation_source` and does not distinguish whether the underlying session was derived from human play vs a mock synthetic state machine.
   - **Conclusion**: The UI accurately and cleanly separates *offline replay* from *live qualification*. However, it does **not** yet explicitly classify whether the offline evidence was *synthetic contract data* versus *recorded live gameplay*.

---

### 3.2 Question 2: Are Hashes and Stop Details Actionable?

#### Current Status String Breakdown
```text
Offline replay stopped: pet-dance-visit / routine-dance-synthetic. Reason: dispatch-unconfirmed. 1 proposals, 8 receipts. Graph c81e20c7f955, events a886acd01b6b. Live qualified: no. Report: F:\...\replay.json
```

#### Assessment
1. **Cryptographic Hashes (`Graph c81e20c7f955, events a886acd01b6b`)**:
   - **Actionability: High**.
   - Truncating SHA-256 to the first 12 characters ($48\text{ bits}$ of entropy) provides sufficient uniqueness while keeping UI status labels compact.
   - Developers and reviewers can instantly verify whether the replayed graph or event stream has drifted from reviewed git artifacts without needing to open the report file.
2. **Stop Detail (`Reason: dispatch-unconfirmed. 1 proposals, 8 receipts`)**:
   - **Actionability: Moderate-High**.
   - Seeing `1 proposals, 8 receipts` immediately signals that replay did not fail due to a missing observation at the end of the session, but rather aborted immediately on the first action receipt.
   - Pairing `RoutineId: pet-dance-visit` with `SessionId: routine-dance-synthetic` makes the mismatch obvious to an operator familiar with fixture naming.
3. **Residual Gap**:
   - The UI does not format the exact proposal ID mismatch string inline (e.g. `Mismatched proposal ID: expected '.../pet-dance-visit/1', observed '.../pet-dance-visit-draft/1'`).
   - However, because the exact absolute `Report: {result.ReportPath}` is printed in the status string, the operator has direct access to the full JSON trace on disk where event-3 contains the exact receipt object.

---

### 3.3 Question 3: What Does CLI Still Hide (`Program.cs`) & Path Ownership?

#### Current CLI Implementation (`Program.cs:113–122`)
```csharp
if (args.Length > 0 && args[0] == "--replay-routine")
{
    try
    {
        if (args.Length != 4) throw new ArgumentException("Expected routine JSON, session manifest and a new report path.");
        if (!RoutineReplay.Run(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), Path.GetFullPath(args[3]))) Environment.ExitCode = 2;
    }
    catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
    return;
}
```

#### Assessment
1. **Path Ownership Boundary**:
   - As confirmed in `LIA-0106` and `claims.json`, `Program.cs` is currently owned under claim `DANCE-CAPTURE-82`.
   - In accordance with the Codex Working Agreement (`AGENTS.md`) and external agent guidelines, root appropriately respected owned path boundaries and did not modify `Program.cs` during task 145.
2. **Information Hidden by CLI**:
   - On successful contract completion (`ExitCode = 0`), the CLI emits **zero stdout lines**.
   - On contract failure or stop (`ExitCode = 2`), the CLI emits **zero stderr and zero stdout lines**.
   - The CLI completely hides:
     - Routine ID, Session ID, and Game ID.
     - Graph SHA-256 and Events SHA-256.
     - Failure reason (`dispatch-unconfirmed`, `condition-block`, `timeout`).
     - Proposal and receipt counts.
     - Explicit confirmation that `live_qualified = false`.
   - Any script, build pipeline, or command-line developer must write external JSON-parsing logic against `args[3]` (the report path) to determine why exit code 2 occurred.
3. **Recommended Future Seam**:
   - When `Program.cs` ownership is next open for edits, `--replay-routine` should invoke `RoutineReplay.RunDetailed` and print a 2-line summary to `Console.Out`:
     ```csharp
     var result = RoutineReplay.RunDetailed(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), Path.GetFullPath(args[3]));
     Console.WriteLine($"[MAELSTROM REPLAY] {(result.Completed ? "COMPLETED" : "STOPPED")}: {result.RoutineId} / {result.SessionId} (Reason: {result.Reason ?? "observed completion"}, Proposals: {result.ProposalCount}, Receipts: {result.ReceiptCount})");
     Console.WriteLine($"[MAELSTROM REPLAY] Graph: {result.GraphSha256[..12]}, Events: {result.EventsSha256[..12]}, LiveQualified: no, Report: {result.ReportPath}");
     if (!result.Completed) Environment.ExitCode = 2;
     ```

---

## 4. Operational Context: Reattach Boundaries (`LIA-0107`)

Directive `LIA-0107` recorded fresh live evidence under `W101-REATTACH-147`:
- Following client restart, `windows` reported a new `PID`, `start-ticks`, and `HWND`.
- Root explicitly passed this refreshed triple into the observers and navigation visit.
- Live observations confirmed Commons and native Idle; the feedback visit measured local coordinate arrival while leaving broad route/arrival flags unverified.
- **Architectural Boundary**: This reattach protocol demonstrates that target binding is strictly an **explicit, operator-driven handshake**, never an automatic heuristic or guess.

### Relevance to Replay Diagnostics
- In offline replay (`RoutineReplay.cs`), the target context (`game_id`, `target_id`, `context_id`) is strictly bound to the historical manifest (`session.json`).
- Offline replay contracts must never simulate or guess live target freshness.
- The presence of `Live qualified: no` in the UI status strip directly prevents operators from mistaking successful offline replay of a recorded session for proof that the runner is attached to a live re-started client.

---

## 5. Prioritized Recommendations for Subsequent Tasks

| Priority | Component | Recommendation | Dependency / Ownership |
| :--- | :--- | :--- | :--- |
| **P1** | CLI (`Program.cs`) | Add 2-line summary output to `Console.Out` on `--replay-routine` reflecting `RunDetailed` outcome, IDs, reason, and `LiveQualified=false`. | Blocked on release of `DANCE-CAPTURE-82`. |
| **P2** | Evidence Classification | Add optional `manifest.annotation_source` check in `RoutineReplay.RunDetailed` to expose `ScopeClassification` (`"OFFLINE_SYNTHETIC"` vs `"OFFLINE_RECORDED"`). | Non-breaking extension to `RoutineReplay.Summary`. |
| **P3** | UI Mismatch Hint | In `SessionTimelineForm` and `RecordedCardReviewForm`, if `result.Reason == "dispatch-unconfirmed"` and `result.ProposalCount == 1` while `result.ReceiptCount > 0`, append hint: `"(Check fixture pairing)"`. | Low-risk UI tweak in form status formatters. |

---

## 6. Invariant Affirmation & Deliverable Handback

1. **Exclusively Read-Only Review**: This document completes the static review assigned under `LIA-0106`. Zero source code or test files were modified.
2. **Zero Builds or Live Operations**: No dotnet builds, unit tests, game processes, or desktop input events were dispatched.
3. **Standing Down**: Reporting `READY_FOR_REVIEW` in `docs/coordination/gemini/FROM_GEMINI.md` and awaiting coordinator review.
