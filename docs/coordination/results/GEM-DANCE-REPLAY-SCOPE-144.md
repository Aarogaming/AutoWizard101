# Offline Replay Scope, Provenance & UI Diagnostics Audit (GEM-DANCE-REPLAY-SCOPE-144)

**Date**: 2026-11-15  
**Workstream**: CORE-DELIVERY / W101-MINIGAME  
**Task ID**: `GEM-DANCE-REPLAY-SCOPE-144`  
**Reference Directives**: `LIA-0103`, `LIA-0104`, `LIA-0121`  
**Baseline Commit**: `6dd15e5` (`maelstrom/rust-architecture-v1`; audited pre-145 initial draft at `7888cf251`)  
**Evidence Input**: `docs/coordination/results/DANCE-REPLAY-PAIRING-143.md`, `.local/dance-visit-replay-heartbeat-20261115.json`, `.local/dance-draft-replay-heartbeat-20261115.json`  
**Scope**: Bounded read-only static analysis and architectural specification. Zero production or test code modified; zero uncoordinated test or build executions; zero live game or desktop input interactions.

---

## 1. Executive Summary & Context of Finding 143

In `DANCE-REPLAY-PAIRING-143.md`, the coordinator executed offline replay tests pairing the Dance session fixture (`session_id = "routine-dance-synthetic"`) against two routine graphs:
1. **Full Visit Graph (`pet-dance-visit.json`)**:
   - Stopped after 1 proposal with reason `"dispatch-unconfirmed"`.
   - Report: `.local/dance-visit-replay-heartbeat-20261115.json`
   - Graph SHA-256: `c81e20c7f9551da51417f493ab2fcd55bdf19b327de507cb439fccedc291e914`
   - Event SHA-256: `a886acd01b6bd64c0e3c912d2629dbe8f21b99dcc2b53cfb6e2d1b46fe86c884`
   - Cause: In event 2, the runner generated proposal `routine-dance-synthetic/pet-dance-visit/1`. In event 3, the fixture provided a receipt for `routine-dance-synthetic/pet-dance-visit-draft/1`. The proposal IDs mismatched, triggering an immediate stop.
2. **Draft Graph (`pet-dance-visit-draft.json`)**:
   - Completed with reason `"observed-completion"` (8 proposals, 8 receipts, 0 desktop dispatches, `live_qualified = false`).
   - Report: `.local/dance-draft-replay-heartbeat-20261115.json`
   - Graph SHA-256: `a560e439f8da1ae80a9e807cf0f4c52f904a9cb763efe4ffb6f335a4ccca48c2`
   - Event SHA-256: `a886acd01b6bd64c0e3c912d2629dbe8f21b99dcc2b53cfb6e2d1b46fe86c884`

### Key Problem Statement
While the underlying engine (`RoutineRunner` and `RoutineReplay`) behaved with complete cryptographic and logical correctness by rejecting the mismatched receipt, **existing user-facing UI and CLI surfaces (`SessionTimelineForm`, `RecordedCardReviewForm`, `CardPrototypeForm`, and `Program.cs`) fail to expose the necessary provenance details**:
1. When replay fails on a proposal mismatch, the UI surfaces merely report a generic binary failure (e.g. `"Replay incomplete or rejected; inspect missing observations/receipts."`), leaving users and developers with no indication that the failure was a pairing mismatch between a draft fixture and a full-visit routine.
2. When replay succeeds against a synthetic draft fixture, the UI surfaces merely report `"Replay contract completed."` or `"Recording test passed"`, creating an acute risk that users conflate **synthetic draft contract validation** with **full-visit live qualification**.
3. Critical cryptographic provenance identifiers—specifically the Routine ID, Session ID, Graph SHA-256, and Event Stream SHA-256—remain buried inside generated JSON report files rather than surfaced in CLI output or UI status banners.

This review conducts a static audit of all existing replay surfaces and defines a lightweight, non-breaking architectural pattern to expose routine and session provenance, display pairing diagnostics, and enforce the operational boundary between synthetic draft replay and full live qualification.

---

## 2. Technical Mechanics of Proposal ID Formulation & Pairing Enforcement

### 2.1 Engine Implementation in `RoutineGraph.cs`
In `ProjectMaelstrom/ProjectMaelstrom/Utilities/RoutineGraph.cs`, proposal formulation and receipt confirmation are tightly coupled to the runner's run ID and the graph ID:

```csharp
// RoutineGraph.cs:189
_proposal = _runId + "/" + _graph.Id + "/" + (++_actions).ToString(System.Globalization.CultureInfo.InvariantCulture);
```

In `RoutineReplay.cs:41–42`, `_runId` is initialized from the manifest's `session_id`:
```csharp
var runner = new RoutineRunner(graph, binding, graph.Nodes.SelectMany(n => n.Edges)
    .Select(e => e.ActionId).ToHashSet(StringComparer.Ordinal), root.GetProperty("session_id").GetString());
```

When an `action_receipt` event is processed:
```csharp
// RoutineGraph.cs:193-203
public void RecordReceipt(RoutineReceipt receipt)
{
    ArgumentNullException.ThrowIfNull(receipt);
    if (_stopReason is not null || _completed) return;
    if (_pending is null || _receiptTime >= 0 || receipt.ProposalId != _proposal || receipt.ActionId != _pending.ActionId ||
        !receipt.Accepted || !receipt.InputReleased || !double.IsFinite(receipt.TimeMs) || receipt.TimeMs < _lastTime ||
        receipt.TimeMs - _firstTime >= _graph.TimeoutMs || receipt.TimeMs - _proposedTime >= _pending.TimeoutMs)
    { Stop("dispatch-unconfirmed"); return; }
    _receiptTime = receipt.TimeMs;
    _lastTime = receipt.TimeMs;
}
```

### 2.2 Trace Walkthrough of Finding 143
In `.local/dance-visit-replay-heartbeat-20261115.json`:
- **Event 2 (`event-2`, $t=100\text{ ms}$)**:
  ```json
  "decision": {
    "node_id": "entry",
    "reason": "action-proposed",
    "proposal_id": "routine-dance-synthetic/pet-dance-visit/1",
    "action_id": "game.interact",
    "completed": false,
    "stopped": false,
    "is_proposal": true
  }
  ```
- **Event 3 (`event-3`, $t=120\text{ ms}$)**:
  ```json
  "receipt": {
    "proposal_id": "routine-dance-synthetic/pet-dance-visit-draft/1",
    "action_id": "game.interact",
    "time_ms": 120,
    "accepted": true,
    "input_released": true
  },
  "decision": {
    "node_id": "entry",
    "reason": "dispatch-unconfirmed",
    "proposal_id": null,
    "action_id": null,
    "completed": false,
    "stopped": true,
    "is_proposal": false
  }
  ```

Because `receipt.ProposalId` (`routine-dance-synthetic/pet-dance-visit-draft/1`) does not equal `_proposal` (`routine-dance-synthetic/pet-dance-visit/1`), `receipt.ProposalId != _proposal` evaluates to `true`, instantly invoking `Stop("dispatch-unconfirmed")`.

### 2.3 Correctness and Immutability of the Guard
This guard is a vital security and verification invariant:
- **No Cross-Routine Receipt Substitution**: An action receipt recorded or synthesized for one routine graph (`pet-dance-visit-draft`) cannot be claimed as proof of execution for a different routine graph (`pet-dance-visit`).
- **No Synthetic ID Relabeling**: Re-writing or mass-replacing proposal IDs in fixture files to force a test to pass without matching observation semantics is an anti-pattern that violates Maelstrom's verification invariants. A full-visit routine requires a session recorded against that exact full-visit specification.

---

## 3. Static Audit of Existing Replay UI & CLI Surfaces

### 3.1 CLI: `Program.cs:113–122`
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
**Deficit**:
- Execution is completely silent on stdout.
- On failure, exit code 2 is returned with zero diagnostic output explaining why the replay stopped (`dispatch-unconfirmed`, `condition-block`, `timeout`, or `action-budget-exceeded`).
- Does not print the Graph SHA, Events SHA, Routine ID, Session ID, or `live_qualified = false` boundary.

### 3.2 UI: `SessionTimelineForm.cs:302–312`

> [!NOTE]
> **Pre-145 Snapshot (Superseded)**: This section describes the pre-145 baseline when this audit was drafted. Root subsequently implemented `RoutineReplay.RunDetailed` in `REPLAY-DIAGNOSTICS-145.md` (commit `6afa0f5` / `LIA-0106`), surfacing routine/session IDs, stop reasons, proposal/receipt counts, graph/event hash prefixes, and `Live qualified: no`.
```csharp
private void Replay()
{
    var graph = LongTeachingSession.Compile(Selected(), _game.Text);
    using var picker = new OpenFileDialog { Title = "Choose observed routine evidence (session.json), not a video annotation", Filter = "Replay evidence|session.json" };
    if (picker.ShowDialog(this) != DialogResult.OK) return;
    var folder = Path.Combine(_root, "reports", "timeline-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
    var graphPath = Path.Combine(folder, "routine.json"); var report = Path.Combine(folder, "replay.json");
    File.WriteAllText(graphPath, JsonSerializer.Serialize(graph, RoutineGraphs.Json));
    var completed = RoutineReplay.Run(graphPath, picker.FileName, report);
    _status.Text = (completed ? "Replay contract completed." : "Replay incomplete or rejected; inspect missing observations/receipts.") + " No game input sent. Report: " + report;
}
```
**Deficit**:
- Status is reduced to a binary string.
- If a pairing mismatch occurs, the user is told `"Replay incomplete or rejected; inspect missing observations/receipts."`, which falsely implies that observations were missing rather than that the session was compiled for a different routine ID.
- Displays no SHA-256 hashes, no session ID, and no scope distinction.

### 3.3 UI: `RecordedCardReviewForm.cs:42–57`

> [!NOTE]
> **Pre-145 Snapshot (Superseded)**: This section describes the pre-145 baseline. Root subsequently implemented `RoutineReplay.RunDetailed` in `REPLAY-DIAGNOSTICS-145.md`, updating `RecordedCardReviewForm` with the detailed diagnostic banner.
```csharp
replay.Click += async (_, _) =>
{
    using var graph = new OpenFileDialog { Title = "Choose routine graph", Filter = "Routine (*.json)|*.json" };
    using var session = new OpenFileDialog { Title = "Choose recorded routine session", Filter = "Session (*.json)|*.json" };
    using var output = new SaveFileDialog { Title = "Save new replay report", Filter = "Report (*.json)|*.json", FileName = "card-action-replay.json" };
    if (graph.ShowDialog(this) != DialogResult.OK || session.ShowDialog(this) != DialogResult.OK || output.ShowDialog(this) != DialogResult.OK) return;
    review.Enabled = replay.Enabled = false;
    try
    {
        if (File.Exists(output.FileName)) throw new IOException("Choose a new report filename.");
        var complete = await Task.Run(() => RoutineReplay.Run(graph.FileName, session.FileName, output.FileName, card, definitions));
        status.Text = $"Recording test {(complete ? "passed" : "did not pass")}. Requirements were checked before each action. See the saved report for details.";
    }
    catch (Exception e) { status.Text = e.Message; }
    finally { review.Enabled = replay.Enabled = true; }
};
```
**Deficit**:
- Status reports `"Recording test passed"` or `"did not pass"`.
- Does not expose why an action test did not pass or which routine graph/session manifest was loaded.

### 3.4 Backend & Qualification: `CardPrototype.cs:73–104` and `CardPrototypeForm.cs:161–173`
```csharp
// CardPrototype.cs:88-95
if (!RoutineReplay.Run(graph, session, report, document.Card, facts))
    status = new(CardQualificationStage.Unqualified, "Recorded requirements and outcomes did not pass together. Inspect the saved report.");
else
{
    var evidence = CardQualification.RecordReplay(Path.Combine(directory, "evidence"), document.Card,
        binding, graph, session, report, facts);
    status = new(CardQualificationStage.ReplayPassed, "This exact configured replay passed offline. Live play remains unqualified.", evidence.Id);
}
```
**Deficit**:
- While `CardQualificationStage.ReplayPassed` explicitly states `"Live play remains unqualified"`, the status label in `CardPrototypeForm.cs:169` displays only:
  `result.Stage + " · " + result.Reason + "\nEvidence never grants game-control permission."`
- The user is not shown whether the replay was against a synthetic fixture or a recorded teaching session, nor the specific routine/session pairing.

---

## 4. Architectural Blueprint for Replay Scope & Diagnostics Exposure

To address these deficits without disrupting existing binary contracts, the replay engine should expose a structured result type, and the UI/CLI consumers should format and display this result.

### 4.1 Typed Replay Result Envelope: `RoutineReplaySummary`

> [!NOTE]
> **Implemented in Task 145**: The proposed typed summary pattern below was implemented by root in `REPLAY-DIAGNOSTICS-145.md` via `RoutineReplay.RunDetailed` returning `RoutineReplaySummary`.
Currently, `RoutineReplay.Run` writes a rich anonymous object to disk (lines 94–103) and returns a bare `bool` (line 106).

A non-breaking enhancement introduces a typed summary record returned by `RoutineReplay.RunDetailed` (with `RoutineReplay.Run` delegating to it and returning `summary.Completed`):

```csharp
public sealed record RoutineReplaySummary(
    bool Completed,
    bool Stopped,
    string Reason,
    string RoutineId,
    string GameId,
    string SessionId,
    string Sha256Graph,
    string Sha256Manifest,
    string Sha256Events,
    int ProposalCount,
    int ReceiptCount,
    int IgnoredEvents,
    bool LiveQualified,
    string ScopeClassification,
    string? ProposalMismatchDiagnostic
);
```

#### Diagnostic Derivation Logic
During the event loop in `RoutineReplay.Run`:
1. When `kind == "action_receipt"` and `runner.Status.Stopped` becomes `true` with `runner.Status.Reason == "dispatch-unconfirmed"`:
   - Check if `receipt.ProposalId != runner.CurrentProposal`:
   - Set `ProposalMismatchDiagnostic`:
     ```csharp
     $"Proposal mismatch at event '{id}' (t={time}ms): routine '{graph.Id}' expected '{runner.CurrentProposal}', but session '{root.GetProperty("session_id").GetString()}' supplied receipt for '{receipt.ProposalId}'."
     ```
2. **Scope Classification**:
   - If `manifest.session_id.Contains("synthetic", StringComparison.OrdinalIgnoreCase)` or manifest `annotation_source == "synthetic"`:
     `ScopeClassification = "OFFLINE_SYNTHETIC_CONTRACT"`
   - Else if manifest `annotation_source == "human"` or manifest `evidence_mode == "labelled-state-replay"`:
     `ScopeClassification = "OFFLINE_RECORDED_TEACHER_SESSION"`
   - Else:
     `ScopeClassification = "OFFLINE_UNCLASSIFIED_EVIDENCE"`
   - `LiveQualified` is unconditionally `false`.

### 4.2 CLI Exposure: `--replay-routine` Diagnostic Output
In `Program.cs:113–122`, when `--replay-routine` is invoked, format and print an explicit summary banner to `Console.Out` before exiting:

```text
================================================================================
MAELSTROM OFFLINE ROUTINE REPLAY SUMMARY
================================================================================
Routine ID:         pet-dance-visit
Session ID:         routine-dance-synthetic
Outcome:            STOPPED (dispatch-unconfirmed)
Scope:              OFFLINE_SYNTHETIC_CONTRACT (live_qualified = false)
Proposals / Receipts: 1 proposed / 0 confirmed (8 in stream)
Graph SHA-256:      c81e20c7f9551da51417f493ab2fcd55bdf19b327de507cb439fccedc291e914
Events SHA-256:     a886acd01b6bd64c0e3c912d2629dbe8f21b99dcc2b53cfb6e2d1b46fe86c884
Diagnostic:         Proposal mismatch at event 'event-3' (t=120ms): routine 'pet-dance-visit' expected 'routine-dance-synthetic/pet-dance-visit/1', but session 'routine-dance-synthetic' supplied receipt for 'routine-dance-synthetic/pet-dance-visit-draft/1'.
Report File:        F:\Modlists\AutoWizard101\.local\dance-visit-replay-heartbeat-20261115.json
================================================================================
```

### 4.3 UI Exposure: `SessionTimelineForm` Status Banner
In `SessionTimelineForm.cs:311`, replace the single-line string with a structured multi-line status report:

```csharp
var summary = RoutineReplay.RunDetailed(graphPath, picker.FileName, report);
var sb = new StringBuilder();
sb.AppendLine(summary.Completed ? "✓ REPLAY CONTRACT COMPLETED" : $"✗ REPLAY REJECTED ({summary.Reason})");
sb.AppendLine($"Scope: [{summary.ScopeClassification}] · Routine: {summary.RoutineId} · Session: {summary.SessionId}");
sb.AppendLine($"Graph SHA: {summary.Sha256Graph[..12]}… · Events SHA: {summary.Sha256Events[..12]}…");
if (summary.ProposalMismatchDiagnostic != null)
{
    sb.AppendLine($"⚠ Pairing Notice: {summary.ProposalMismatchDiagnostic}");
}
sb.AppendLine("Notice: Offline replay validates state contracts only; live gameplay remains unqualified.");
sb.Append($"Report: {report}");
_status.Text = sb.ToString();
```

### 4.4 UI Exposure: `RecordedCardReviewForm` Replay Banner
In `RecordedCardReviewForm.cs:53`:
```csharp
var summary = await Task.Run(() => RoutineReplay.RunDetailed(graph.FileName, session.FileName, output.FileName, card, definitions));
status.Text = (summary.Completed ? "✓ Action Test Passed" : $"✗ Action Test Rejected ({summary.Reason})") +
    $"\nRoutine: {summary.RoutineId} ({summary.Sha256Graph[..10]}…) · Session: {summary.SessionId}" +
    (summary.ProposalMismatchDiagnostic != null ? $"\nDiagnostic: {summary.ProposalMismatchDiagnostic}" : "") +
    $"\nScope: [{summary.ScopeClassification}] · Live play unqualified. Saved: {output.FileName}";
```

---

## 5. Scope Distinction: Synthetic Draft vs Full-Visit Qualification

A central goal of `GEM-DANCE-REPLAY-SCOPE-144` is establishing unambiguous boundaries between synthetic draft contracts and full-visit qualification.

### 5.1 Capability Matrix

| System / Feature Component | `pet-dance-visit-draft` Replay Success | `pet-dance-visit` Full Routine Goal | Live Qualification Requirement |
| :--- | :--- | :--- | :--- |
| **Evidence Mode** | `labelled-state-replay` (synthetic mock) | `labelled-state-replay` / recorded | Live observation & feedback |
| **State Machine Verification** | Verified (8 synthetic node transitions) | Unverified (stopped at node 1) | Verified under real client runtime |
| **Sigil Detection & Entry** | Mocked via synthetic `DanceSigil` scene | Requires sigil prompt OCR/detection | Live sigil interaction |
| **Mini-Game Menu Navigation** | Mocked via synthetic interact receipt | Requires game selection & play click | Live UI interaction |
| **Arrow Prompt Recognition** | Mocked via synthetic arrow scenes | Requires real-time vision / memory | Live dynamic observation |
| **Keyboard / Input Dispatch** | None (`desktop_dispatch_count = 0`) | None (`desktop_dispatch_count = 0`) | Authorized native window input |
| **Snack Feeding & XP Receipt** | Mocked completion scene | Requires post-game rewards handling | Live snack dialog interaction |
| **Live Qualification Badge** | `live_qualified = false` | `live_qualified = false` | `live_qualified = true` |

### 5.2 Key Takeaways for Developers and Users
1. **Passing `pet-dance-visit-draft` is not a Dance Bot**:
   A green status on `pet-dance-visit-draft` proves only that the graph definition is internally consistent with the synthetic event sequence in `routine-dance-synthetic`. It provides **zero evidence** that Maelstrom can successfully enter a pet dance sigil, read arrows, feed a snack, or play the minigame in Wizard101.
2. **`pet-dance-visit` is Not Broken; It is Correctly Guarded**:
   The stoppage of `pet-dance-visit` against `routine-dance-synthetic` is not a defect in `pet-dance-visit.json`. It is the engine's security guard preventing a full-visit routine from falsely claiming validation from a draft synthetic fixture.
3. **No Fixture Relabeling**:
   The proposal IDs in `routine-dance-synthetic/events.jsonl` must **not** be mass-renamed to `.../pet-dance-visit/...`. That would create a counterfeit qualification artifact. Instead, when the full visit routine is ready for offline validation, a complete labelled session representing the full visit flow should be generated and reviewed.

---

## 6. Proposed Offline Acceptance Tests

The following unit tests specify the required diagnostic and scope behaviors. They are marked **`[PROPOSED ONLY - NOT IMPLEMENTED]`** to strictly adhere to the read-only scope of `GEM-DANCE-REPLAY-SCOPE-144`.

```csharp
// [PROPOSED ONLY - NOT IMPLEMENTED]
// File: ProjectMaelstrom.Tests/RoutineReplayDiagnosticsTests.cs

[Fact]
public void RoutineReplay_ProposalMismatch_PopulatesDiagnosticAndStops()
{
    // Arrange: routine with ID "full-visit" and session with receipts for "draft-visit"
    var graph = new RoutineGraph("full-visit", "wizard101", 10, 5000, "entry", [
        new RoutineNode("entry", "Sigil", [new RoutineEdge("Sigil", "game.interact", 1000, "game", ["Game"])], null)
    ]);
    var sessionJson = CreateTestSessionManifest("session-test", "draft-visit");
    
    // Act
    var summary = RoutineReplay.RunDetailed(graph, sessionJson, reportPath);

    // Assert
    Assert.False(summary.Completed);
    Assert.Equal("dispatch-unconfirmed", summary.Reason);
    Assert.NotNull(summary.ProposalMismatchDiagnostic);
    Assert.Contains("expected 'session-test/full-visit/1'", summary.ProposalMismatchDiagnostic);
    Assert.Contains("supplied receipt for 'session-test/draft-visit/1'", summary.ProposalMismatchDiagnostic);
}

[Fact]
public void RoutineReplay_SyntheticSession_ClassifiesScopeAsSyntheticContract()
{
    // Arrange: routine and session matching synthetic naming
    var graph = LoadFixtureGraph("pet-dance-visit-draft.json");
    var session = LoadFixtureSession("routine-dance-synthetic.json");

    // Act
    var summary = RoutineReplay.RunDetailed(graph, session, reportPath);

    // Assert
    Assert.True(summary.Completed);
    Assert.Equal("OFFLINE_SYNTHETIC_CONTRACT", summary.ScopeClassification);
    Assert.False(summary.LiveQualified);
    Assert.Equal(64, summary.Sha256Graph.Length);
    Assert.Equal(64, summary.Sha256Events.Length);
}
```

---

## 7. Invariant Affirmation & Next Steps

1. **Strictly Read-Only Review**: This document completes the static inspection required by `LIA-0104`. Zero source code or test files were modified.
2. **Zero Builds or Live Operations**: No dotnet builds, unit tests, game processes, or desktop input events were dispatched.
3. **Standing Down**: In accordance with `LIA-0104`, reporting `READY_FOR_REVIEW` in `docs/coordination/gemini/FROM_GEMINI.md` and awaiting coordinator review.
