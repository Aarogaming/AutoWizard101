# GEM-PROTOTYPE-ACCEPTANCE-85 — Card Prototype & Qualification Static Review (Corrected Baseline)

**Task ID**: `GEM-PROTOTYPE-ACCEPTANCE-85`  
**Workstream**: `CORE-DELIVERY`  
**Author**: Gemini  
**Date**: 2026-11-16T12:20:00-05:00 (Revised per coordinator audit `GEM-HONESTY-AUDIT-01` / `LIA-0121`)  
**Repository Revision**: HEAD `6dd15e5` (`maelstrom/rust-architecture-v1`, introduced in commit `236eb00`)  
**Status**: READ-ONLY AUDIT & USER JOURNEY SPECIFICATION (CORRECTED)  
**Owned Output**: `docs/coordination/results/GEM-PROTOTYPE-ACCEPTANCE-85.md`  

---

## 1. Executive Summary & Verification Scope

This static review audits the native card workshop and offline qualification subsystem introduced in `CardPrototypeForm.cs`, `CardPrototype.cs`, and `CardQualification.cs`. The review strictly traces existing symbols, contracts, and line numbers to map the complete user journey:
$$\text{Create} \longrightarrow \text{Configure} \longrightarrow \text{Save} \longrightarrow \text{Load} \longrightarrow \text{Replay/Test} \longrightarrow \text{View History} \longrightarrow \text{Invalidate/Requalify}$$

### 1.1 Architectural Policy & Safety Invariants
- **Configuration Only**: A prototype is purely offline declarative configuration. It is never a runtime callback, dealt card, or live execution grant ([CardPrototype.cs#L12-L14](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardPrototype.cs#L12-L14)).
- **Execution Permission Prohibited**: `CardQualificationStatus.GrantsExecutionPermission` is permanently hardcoded to `false` ([CardQualification.cs#L13-L15](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardQualification.cs#L13-L15)). Qualification evidence never bypasses scheduler admission, target checks, or user takeover boundaries.
- **Exclusive Replay Association**: Offline qualification is strictly bound to `review.routine` with a verified execution graph. Generic routines cannot borrow arbitrary graphs to manufacture qualification evidence ([CardPrototype.cs#L53-L60](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardPrototype.cs#L53-L60)).

---

## 2. End-to-End User Journey Audit Table

| Journey Stage | Primary UI Affordance (`CardPrototypeForm.cs`) | Backend Contract & Method (`CardPrototype.cs` / `CardQualification.cs`) | Exact Validation & Guard Logic | Persistence & Evidence Binding | Known Gaps, Edge Cases & Deficiencies |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **1. Create** | `_actions` dropdown ([L14](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/CardPrototypeForm.cs#L14), [L28-33](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/CardPrototypeForm.cs#L28-L33)), `"New card"` button ([L34-39](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/CardPrototypeForm.cs#L34-L39)). | `CardPrototype.Create(routineId, gameId, title, routines)` ([CardPrototype.cs#L19-L25](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardPrototype.cs#L19-L25)). | `document.SchemaVersion == 1`, `!document.Card.InDeck`, JSON size $\le 65536$ bytes ([CardPrototype.cs#L27-L35](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardPrototype.cs#L27-L35)). | Memory only (`_document`); zero disk writes. History is empty (`[]`). | Throws raw `InvalidOperationException` if catalog has no actions for workspace ([L29](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/CardPrototypeForm.cs#L29)). Default title hardcoded. |
| **2. Configure** | `JobCardView` (`prototypeMode: true`) ([L103-107](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/CardPrototypeForm.cs#L103-L107)), `"Choose routine file…"` ([L42](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/CardPrototypeForm.cs#L42)), `"Choose recording…"` ([L43](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/CardPrototypeForm.cs#L43)). | `SetDocument` ([L97-114](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/CardPrototypeForm.cs#L97-L114)), `ChooseReplayFile` ([L140-148](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/CardPrototypeForm.cs#L140-L148)). | `CanLeaveEditor()` prevents actions while viewing card back ([L115-120](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/CardPrototypeForm.cs#L115-L120)). `ChooseReplayFile` rejects non-replay actions ([L143](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/CardPrototypeForm.cs#L143)). | In-memory `_document` updated; `_dirty = true`. Card ID preserved unless explicitly copied. | Non-replay actions lack specialized setting pickers. Selected files are not existence-checked until test execution. |
| **3. Save** | `"Save new version…"` button ([L40](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/CardPrototypeForm.cs#L40), [L142-157](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/CardPrototypeForm.cs#L142-L157)). | `CardPrototype.Save(newPath, document, routines)` ([CardPrototype.cs#L37-L42](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardPrototype.cs#L37-L42)). | `FileMode.CreateNew` prevents overwrite ([CardPrototype.cs#L40](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardPrototype.cs#L40)); Form sets `OverwritePrompt = false` and cancels via `FileOk` prompt ([CardPrototypeForm.cs#L147-153](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/CardPrototypeForm.cs#L147-L153)). | Writes atomic indented JSON to `<dataRoot>/card-prototypes/<title>.json`. Sets `_dirty = false`. | Preserved files: Form cleanly guards against overwrite via `FileOk` message box without throwing unhandled exceptions. |
| **4. Load** | `"Load prototype…"` button ([L41](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/CardPrototypeForm.cs#L41), [L158-163](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/CardPrototypeForm.cs#L158-L163)). | `CardPrototype.Load(path, routines)` ([CardPrototype.cs#L44-L51](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardPrototype.cs#L44-L51)). | `ConfirmDiscard()` ([L140-141](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/CardPrototypeForm.cs#L140-L141)); `ReadBounded(path, 65536)` ([CardPrototype.cs#L46](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardPrototype.cs#L46)); `document.GameId == _gameId` ([CardPrototypeForm.cs#L99](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/CardPrototypeForm.cs#L99)). | Replaces active `_document`; resets `_dirty = false`. Calls `RefreshHistory()` for loaded `card.Id`. | History lookup strictly keys off `card.Id`. If a loaded JSON was renamed or duplicated externally, prior attempts are unlinked. |
| **5. Replay / Test** | `"Test configured action"` button ([L44-55](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/CardPrototypeForm.cs#L44-L55)). | `CardPrototype.Test(dataRoot, document, routines)` ([CardPrototype.cs#L73-L104](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardPrototype.cs#L73-L104)). | `ReplayBinding` rejects `routineId != "review.routine"` ([CardPrototype.cs#L59](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardPrototype.cs#L59)); form disables button for non-replay actions ([L130-131](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/CardPrototypeForm.cs#L130-L131)). | Writes `<dataRoot>/card-prototype-tests/<cardId>/<attemptId>/` (`attempt.json`, `replay.json`, `evidence/`). | **No Cancellation**: `Test` lacks `CancellationToken`. Replay is synchronous. Form closing is blocked while busy ([L81](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/CardPrototypeForm.cs#L81)). |
| **6. View History** | `_history` ListBox ([L16](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/CardPrototypeForm.cs#L16)), `"Verify selected evidence…"` button ([L69-76](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/CardPrototypeForm.cs#L69-L76)). | `CardPrototype.History(dataRoot, cardId)` ([CardPrototype.cs#L106-L124](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardPrototype.cs#L106-L124)), `ReviewHistory` ([L126-131](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardPrototype.cs#L126-L131)). | Ignores non-GUID folders and reparse points ([CardPrototype.cs#L113-L114](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardPrototype.cs#L113-L114)). `Assess` verifies all artifact hashes and reruns reproduction. | Read-only verification. Historical records are immutable; temporary reproduction directory wiped in `finally`. | ListBox lacks status color/icons. Selecting an item does not auto-assess; requires clicking separate review button. |
| **7. Invalidate** | Card back "Save & flip" or setting edit resets status ([L111-112](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/CardPrototypeForm.cs#L111-L112)). | `CardQualification.Assess` ([CardQualification.cs#L83-L116](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardQualification.cs#L83-L116)). | `evidence.Binding != current` returns `CardQualificationStage.Unqualified` ([CardQualification.cs#L97](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardQualification.cs#L97)). | Historical evidence remains intact on disk; marked `"historical"`. Re-qualifying creates a fresh attempt directory. | No visual diff between historical attempt card and current card. No mechanism to prune obsolete attempts from UI. |

---

## 3. Detailed Journey Architecture & Contract Analysis

### 3.1 Creation & Action Catalog Binding
- **Contract**: `CardPrototype.Create(string routineId, string gameId, string title, IReadOnlyDictionary<string, JobRoutine> routines)` in [CardPrototype.cs#L19-L25](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardPrototype.cs#L19-L25).
- **Execution**: Looks up `routineId` in `routines` catalog. Invokes `JobDeckRoutines.NewCard(routine.Info) with { Title = title }` and wraps it in a `CardPrototypeDocument` with `SchemaVersion = 1`.
- **Validation**:
  - Rejects unknown routine IDs (`throw new InvalidDataException("Choose a catalog action.")`).
  - `Validate(document, routines)` verifies `document.Card.InDeck == false`, valid rules, and serialized size $\le 65536$ bytes.
- **Form Integration**: In [CardPrototypeForm.cs#L28-L33](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/CardPrototypeForm.cs#L28-L33), the form filters routines matching `info.GameId is "*" || info.GameId == gameId`. If `review.routine` exists, it is selected by default; otherwise index 0 is chosen.

### 3.2 Configuration & Path Association
- **Contract**: Card configuration operates through the shared `JobCardView` control in prototype mode ([CardPrototypeForm.cs#L103-L107](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/CardPrototypeForm.cs#L103-L107)).
- **File Choosers**:
  - `ChooseReplayFile(string setting, string title)` ([CardPrototypeForm.cs#L140-L148](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/CardPrototypeForm.cs#L140-L148)) binds `graphFile` or `sessionFile`.
  - Enforces `_document.Card.RoutineId == "review.routine"`. If called on other routines, throws `InvalidOperationException("Choose the Replay action to configure recorded routine files.")`.
- **Flip Guard**:
  - `CanLeaveEditor()` ([CardPrototypeForm.cs#L115-L120](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/CardPrototypeForm.cs#L115-L120)) inspects `_view.ShowingBack`. If the user is editing the card back, they cannot save, test, load, or choose files without first clicking "Save & flip" on the card.

### 3.3 Persistence & Immutable Versioning
- **Contract**: `CardPrototype.Save(string newPath, CardPrototypeDocument document, IReadOnlyDictionary<string, JobRoutine> routines)` in [CardPrototype.cs#L37-L42](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardPrototype.cs#L37-L42).
- **Exclusivity**: Opens the destination file stream with `FileMode.CreateNew, FileAccess.Write, FileShare.None` ([CardPrototype.cs#L40](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardPrototype.cs#L40)).
- **Flush to Disk**: Explicitly calls `output.Flush(flushToDisk: true)` to ensure durable commit before releasing the handle.
- **Form Guard Implementation**: In [CardPrototypeForm.cs#L147-L153](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/CardPrototypeForm.cs#L147-L153), the form sets `OverwritePrompt = false` on `SaveFileDialog` and attaches a `FileOk` handler:
  ```csharp
  file.FileOk += (_, args) =>
  {
      if (!File.Exists(file.FileName)) return;
      args.Cancel = true;
      MessageBox.Show(this, "Existing versions are preserved. Choose a new filename.", "Save a new version");
  };
  ```
  This cleanly prevents overwrite attempts and guides the user without unhandled `IOException` errors or dialog crashes.

### 3.4 Loading & Workspace Isolation
- **Contract**: `CardPrototype.Load(string path, IReadOnlyDictionary<string, JobRoutine> routines)` in [CardPrototype.cs#L44-L51](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardPrototype.cs#L44-L51).
- **Bounded Read**: Uses `RoutineReplay.ReadBounded(path, 65536)` to prevent allocation attacks.
- **Workspace Guard**: [CardPrototypeForm.cs#L99](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/CardPrototypeForm.cs#L99) strictly validates:
  ```csharp
  if (document.GameId != _gameId)
      throw new InvalidDataException("Open this prototype from its own game workspace.");
  ```
  Prevents loading cross-workspace card documents into an active game session.

### 3.5 Replay Test & Evidence Freezing
- **Contract**: `CardPrototype.Test(string dataRoot, CardPrototypeDocument document, IReadOnlyDictionary<string, JobRoutine> routines)` in [CardPrototype.cs#L73-L104](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardPrototype.cs#L73-L104).
- **Binding Generation**:
  - `ReplayBinding(document, routines)` ([CardPrototype.cs#L55-L71](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardPrototype.cs#L55-L71)) constructs a `CardQualificationBinding`:
    - `CardHash`: SHA-256 of canonical card JSON via `RecordedCardReview.Hash(document.Card)`.
    - `ExecutionGraphHash`: SHA-256 of `graphFile`.
    - `RoutineId`: Pinned to `"review.routine"`.
    - `AdapterVersion`: Pinned to `"deck.review.routine-1+prototype-conditions-1"`.
    - `ScopeHash`: SHA-256 of execution scope metadata.
    - `FactSchemaHash`: Hash of combined job and routine facts.
- **Execution & Recording**:
  - Executes `RoutineReplay.Run(graph, session, report, document.Card, facts)`.
  - If successful, calls `CardQualification.RecordReplay` ([CardQualification.cs#L38-L78](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardQualification.cs#L38-L78)), which freezes all input files into `evidence/sources/` and `evidence/session/` and re-runs `RoutineReplay.Run` over the frozen copies to ensure deterministic reproduction before writing `evidence.json`.
- **Status Outcome**: Returns `CardQualificationStage.ReplayPassed` with reason `"This exact configured replay passed offline. Live play remains unqualified."` and the generated evidence GUID.

### 3.6 History Ledger & Invalidation Semantics
- **History Enumeration**: `CardPrototype.History(dataRoot, cardId)` ([CardPrototype.cs#L106-L124](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardPrototype.cs#L106-L124)) scans `<dataRoot>/card-prototype-tests/<cardId>/`.
  - Filters out symlinks/reparse points (`FileAttributes.ReparsePoint`).
  - Limits scan to 256 entries.
  - Verifies `attempt.json` matches schema 1 and has a valid outcome string.
- **Assessment Against Current Binding**: `CardQualification.Assess(directory, currentBinding)` in [CardQualification.cs#L83-L116](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardQualification.cs#L83-L116):
  - Compares frozen `evidence.Binding` with `currentBinding`.
  - If **any** card property, graph, adapter, or fact schema changed, immediately returns:
    $$\text{Stage} = \text{CardQualificationStage.Unqualified}, \quad \text{Reason} = \text{"Card, graph, adapter, facts or scope changed; prior evidence is historical."}$$
  - If binding matches, verifies SHA-256 digests of all frozen files, unpacks into a safe temporary folder (`Path.GetTempPath()/maelstrom-card-qualification-...`), and reruns reproduction before confirming `ReplayPassed`.

---

## 4. Ranked Correctness & Usability Gaps

### Implemented Safeguards Verified
- **Save Overwrite Guard (Verified Implemented)**: `CardPrototypeForm.cs:147–153` sets `OverwritePrompt = false` and cancels via `FileOk` with `MessageBox.Show("Existing versions are preserved. Choose a new filename.")`. There is no unhandled `IOException` thrown to user code.
- **Non-Replay Test Disabling (Verified Implemented)**: In `CardPrototypeForm.cs:130–131`, `_test.Enabled = _document.Card.RoutineId == "review.routine"`, appending `"This action is a draft. Recorded-outcome testing is not connected yet."` when a non-replay routine is selected.

### Remaining Architectural & Usability Gaps

### Priority 1: Lack of Cancellation in Prototype Testing (Robustness Gap)
- **Source**: [CardPrototype.cs#L73](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardPrototype.cs#L73) and [CardPrototypeForm.cs#L79-L83](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/CardPrototypeForm.cs#L79-L83).
- **Issue**: `CardPrototype.Test` does not accept a `CancellationToken`. Replay execution runs synchronously within `Task.Run`. If the user attempts to close the window while a large test is running, `FormClosing` prevents exit (`if (_busy) args.Cancel = true;`), leaving the user stuck until completion.
- **Expected Behavior**: `CardPrototype.Test` should accept a `CancellationToken`, passing it to `RoutineReplay.Run` and allowing immediate cooperative cancellation.

### Priority 2: History Loss Across Card Duplication / Copying (Usability Gap)
- **Source**: [CardPrototypeForm.cs#L122-L123](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/CardPrototypeForm.cs#L122-L123) and [CardPrototype.cs#L106-L124](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardPrototype.cs#L106-L124).
- **Issue**: Clicking "Copy" on the card back generates a fresh `card.Id = Guid.NewGuid().ToString("N")`. Because history is keyed strictly by `cardId` on disk (`card-prototype-tests/<cardId>/`), copying a card instantly wipes visible history in the ListBox with no lineage breadcrumb.
- **Expected Behavior**: The card prototype document should support a `LineageId` or `ParentCardId` to allow browsing prior iteration history.

### Priority 3: Pre-Flight File Existence Validation (Usability Gap)
- **Source**: [CardPrototypeForm.cs#L164-L172](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/CardPrototypeForm.cs#L164-L172).
- **Issue**: Selecting routine graph or recording files via `ChooseReplayFile` stores the path without immediate verification of file presence or schema validity until `Test` is clicked.
- **Expected Behavior**: Validate target file existence and initial JSON schema upon selection, surfacing inline warnings immediately in `_status`.

---

## 5. Smallest Offline Acceptance Checks

The following five focused unit checks validate the boundaries and contracts of the prototype subsystem without live execution:

| Case ID | Verification Area | Target Symbol & Contract | Preconditions & Test Inputs | Trigger / Method Call | Expected Assertion |
| :---: | :--- | :--- | :--- | :--- | :--- |
| **AC-01** | **Save Overwrite Guard** | `CardPrototype.Save` ([CardPrototype.cs#L37](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardPrototype.cs#L37)) | Valid document saved to path $P$. | Call `CardPrototype.Save(P, document, routines)` a second time. | Throws `IOException` due to `FileMode.CreateNew`. Destination file content and timestamp remain untouched. |
| **AC-02** | **Non-Replay Action Guard** | `CardPrototype.ReplayBinding` ([CardPrototype.cs#L55](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardPrototype.cs#L55)) | Document created with `w101.pet.dance` or `commons.travel`. | Call `CardPrototype.ReplayBinding(document, routines)`. | Throws `InvalidOperationException("This action has no prototype replay association yet...")`. Prevents borrowing arbitrary graphs. |
| **AC-03** | **Hash Drift Invalidation** | `CardQualification.Assess` ([CardQualification.cs#L83](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardQualification.cs#L83)) | Valid `ReplayPassed` evidence recorded for card $C_1$. | Mutate card condition ($C_2 = C_1$ with `Runs = 5`), compute new binding $B_2$, call `Assess(evidenceDir, B_2)`. | Returns `Stage = CardQualificationStage.Unqualified`, reason `"Card, graph, adapter, facts or scope changed; prior evidence is historical."`. |
| **AC-04** | **Reparse Point Rejection** | `CardQualification.Assess` ([CardQualification.cs#L90](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardQualification.cs#L90)) | Evidence directory configured as a directory junction/symlink. | Call `CardQualification.Assess(symlinkDir, binding)`. | Returns `Stage = CardQualificationStage.Unqualified`, reason contains `"links"`. Rejects symlink traversal. |
| **AC-05** | **Permanent Permission Denial** | `CardQualificationStatus` ([CardQualification.cs#L11-L15](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/CardQualification.cs#L11-L15)) | Instantiate status with `CardQualificationStage.ReplayPassed` or `Repeatable`. | Inspect `status.GrantsExecutionPermission`. | Asserts `false`. Invariant holds across all qualification stages. |

---

## 6. Traceability, Git State & Coordination Summary

- **Deliverable Path**: `docs/coordination/results/GEM-PROTOTYPE-ACCEPTANCE-85.md`
- **Claim Authority**: `docs/coordination/claims.json` (line 2792, task `GEM-PROTOTYPE-ACCEPTANCE-85`, owner: `gemini`, state: `active`)
- **Git Commit Baseline**: Introduced in commit `236eb00`, audited and corrected at HEAD `6dd15e5` per `LIA-0121` / `GEM-HONESTY-AUDIT-01` (note: prior baseline `595ef2c` predated `CardPrototypeForm.cs`).
- **Coordination Status**: Handed back via `GEM-0033` / `GEM-0108` with `Kind: READY_FOR_REVIEW` in `docs/coordination/gemini/FROM_GEMINI.md`.
- **Policy Compliance**:
  - Exclusively read-only static code and user journey analysis.
  - Zero modifications to production source code or test files.
  - Zero build or test commands executed (`dotnet test` withheld for coordinator build slot).
  - Zero live game access, hook installations, or input dispatches.
