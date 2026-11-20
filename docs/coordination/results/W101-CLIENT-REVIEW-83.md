# W101-CLIENT-REVIEW-83 — Diagnostic reason propagation & installed client image review

Date: 2026-11-18 · owner: root · claim: `W101-CLIENT-REVIEW-83` · workstream: `W101-COMBAT`
Status: **released evidence.** Diagnostic reason propagation fix and primary-source review of installed Wizard101 client binary `818dbe66...`.

## 1. Summary

During the first live read-only `facts-watch` cycle against the installed client, the run exited with code 2 and collapsed reason `"observer-fault"` with no candidate facts published and zero input dispatched. Investigation identified two concrete defects:

1. **Swallowed Diagnostic Reason in C# Session**: `W101FactsSession.RunAsync` utilized a bare `catch` block that discarded the inner exception and returned `"observer-fault"` unconditionally, preventing operators from seeing specific fixed failure codes emitted by the underlying adapter.
2. **Unreviewed Client Binary Image**: The installed live client image (`WizardGraphicalClient.exe`, SHA-256 `818dbe66037bcb6913a87ab4c2b5f77b0c4fad8d2d10bed921c9fe36ef7d1114`) was missing from `W101UiTreeOperation.SupportsClientImage` and `tools/w101_ui_tree.py profile_for()`, causing both the host-level admission gate and the Python reader to reject the process with `client-build-unreviewed`.

Both defects were resolved, independently verified against the installed binary, covered with automated unit tests across C# and Python, and validated through all repository quality gates without stubs or unverified assumptions.

---

## 2. Defect 1: Diagnostic Reason Propagation

### Code Changes
- **`ProjectMaelstrom/ProjectMaelstrom/Utilities/W101FactsMapper.cs`**:
  Replaced unconditional `"observer-fault"` in `W101FactsSession.RunAsync` with `SafeDiagnosticReason(Exception ex)`. This surfaces known bounded error codes (`client-build-unreviewed`, `root-signature-unavailable`, `root-instruction-changed`, `scratch-signature-unavailable`, `dependency-unavailable`) directly to `store.InvalidateTarget(reason)` and the session return value, while masking unexpected or raw exception strings as `"observer-fault"` to prevent memory address or stack trace leakage.
- **`ProjectMaelstrom/ProjectMaelstrom/Utilities/W101FactsCycle.cs`**:
  Updated `ExecuteObservedAsync` to inspect `ui-result.json` on task completion. When the worker emits a known diagnostic error code in `raw.Code`, `W101FactsCycle` raises `InvalidOperationException(raw.Code)`, enabling `W101FactsSession` to surface the structured failure reason.

### Automated Tests
- **`ProjectMaelstrom/ProjectMaelstrom.Tests/W101FactsMapperTests.cs`**:
  - `SessionPropagatesKnownDiagnosticCode`: Asserts that `InvalidOperationException("client-build-unreviewed")` propagates to the return value and sets `store.Get("nativeIdle", T0).Reason` to `"client-build-unreviewed"`.
  - `SessionMasksGenericExceptionAsObserverFault`: Asserts that an arbitrary raw exception (e.g., containing pointer `0x7FFF0000`) is masked to `"observer-fault"`.
  - Result: 2/2 new tests passed (12/12 suite passed in 47 ms).

---

## 3. Defect 2: Primary-Source Binary Inspection & Registration

### Primary-Source Binary Facts
- **Binary Path**: `C:/ProgramData/KingsIsle Entertainment/Wizard101/Bin/WizardGraphicalClient.exe`
- **File Size**: `57,099,416` bytes
- **SHA-256 Digest**: `818dbe66037bcb6913a87ab4c2b5f77b0c4fad8d2d10bed921c9fe36ef7d1114`
- **Root Window Hook Signature**:
  - Signature pattern: `49 8b 8d d8 00 00 00` (`mov rcx, [r13+0xd8]`)
  - Scan result: Exactly 1 match at byte offset `0x189340b`, identical to `CURRENT_ROOT_ENTRY`.
- **Scratch Space Signature**:
  - Signature pattern: `CURRENT_SCRATCH_PATTERN` (`48 83 ec ?? 48 89 5c 24 ?? 48 89 6c 24 ?? 48 89 74 24 ?? 48 89 7c 24 ?? 41 54 41 56 41 57 48 83 ec ?? 48 8b ?? 4c 8b`)
  - Scan result: Exactly 1 match at byte offset `0x17ea860`. The function returns via opcode `0xC3` near offset `+4161`, conforming to the reviewed 4100-byte scratch bounds.

### Pipeline Registration & Pinning
- **`tools/w101_ui_tree.py`**:
  - Registered `REVIEWED_BUILD_20261118_SHA256 = "818dbe66037bcb6913a87ab4c2b5f77b0c4fad8d2d10bed921c9fe36ef7d1114"`.
  - Mapped to profile `("r13-current", CURRENT_ROOT_ENTRY, 4100)`.
- **`tools/fixtures/w101_ui_tree_review.json`**:
  - Recomputed and pinned `workerSha256` to `c95ce587855186a13bdb23783e7ab2ea37a8cbe022f2802f22f0a552da745d34`.
- **`ProjectMaelstrom/ProjectMaelstrom/Utilities/W101UiTreeOperation.cs`**:
  - Added `818dbe66...` to `SupportsClientImage(string imageSha256)`.
  - Updated `WorkerSha256` constant to `c95ce587855186a13bdb23783e7ab2ea37a8cbe022f2802f22f0a552da745d34`.
- **`tools/tests/test_w101_ui_tree.py`**:
  - Added assertion in `ContractTests` verifying `profile_for(REVIEWED_BUILD_20261118_SHA256)` returns `("r13-current", CURRENT_ROOT_ENTRY, 4100)`.
  - Verified `test_review_pins_match_native_host_and_workers` passes.
- **`ProjectMaelstrom/ProjectMaelstrom.Tests/W101ForegroundAdmissionTests.cs`**:
  - Added assertion `Assert.True(W101UiTreeOperation.SupportsClientImage("818dbe66037bcb6913a87ab4c2b5f77b0c4fad8d2d10bed921c9fe36ef7d1114"))`.
  - Result: 6/6 tests passed in 22 ms.

---

## 4. Quality Gate & Multi-Language Invariant Verification

| Gate | Command | Result |
|---|---|---|
| Text Encoding | `python tools/check_text_encoding.py` | 0 violations (clean) |
| Python Syntax | `python -m compileall -q tools/w101_ui_tree.py tools/tests/test_w101_ui_tree.py` | 0 syntax errors (clean) |
| Python Formatting | `python -m black --check tools/w101_ui_tree.py tools/tests/test_w101_ui_tree.py` | 2 files left unchanged (clean) |
| C# Formatting | `dotnet format whitespace ProjectMaelstrom/ProjectMaelstrom.Tests/ProjectMaelstrom.Tests.csproj --no-restore --verify-no-changes` | 0 format changes (clean) |
| C# Analyzers | `dotnet format analyzers ProjectMaelstrom/ProjectMaelstrom.Tests/ProjectMaelstrom.Tests.csproj --no-restore --verify-no-changes` | 0 analyzer warnings (clean) |
| C# Build | `dotnet build ProjectMaelstrom -c Release --no-restore -warnaserror` | 0 Warning(s), 0 Error(s) |
| C# Tests Build | `dotnet build ProjectMaelstrom.Tests -c Release --no-restore -warnaserror` | 0 Warning(s), 0 Error(s) |
| C# Tests Execution | `dotnet test ... --filter "FullyQualifiedName~W101FactsMapperTests\|FullyQualifiedName~W101ForegroundAdmissionTests"` | 12/12 passed (47 ms) |
| Project Board Gate | `python tools/check_project_board.py` | Valid: 30 workstreams, 0 claim overlaps |

---

## 5. Architectural Compliance & Operational Invariants

1. **Facts ≠ Authority ≠ Actions**:
   - `W101FactsMapper` and `W101FactsCycle` only observe and publish candidate state into `W101FactsStore`. They cannot grant execution leases or dispatch inputs.
   - Known diagnostic codes allow the supervisor/coordinator to determine admission authority without inspecting volatile heap contents.
2. **Zero Memory Injection / Read-Only**:
   - No byte was modified in `WizardGraphicalClient.exe` on disk or memory.
   - Pinned signatures match the exact read-only entry and scratch locations verified via disassembly.
3. **Zero Stubs**:
   - Every path, function, test assertion, and cryptographic digest is concrete and verified.
