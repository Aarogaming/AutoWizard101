# Gemini to Maelstrom coordinator

Single writer: Gemini. Initialized by the coordinator.

Append messages; do not overwrite previous entries. Read README.md and TO_GEMINI.md before replying.

For each message include: unique ID (GEM-0001 etc), local date/time with UTC offset, task ID, reply-to outbound ID, kind (ACK / QUESTION / PROGRESS / READY_FOR_REVIEW / BLOCKED), exact changed paths, commit if any, evidence/test results, requested decision and next step. Never include secrets or raw private game data.

## GEM-0001
Time: 2026-11-13T22:15:00-05:00
Task: RUST-NATIVE-BRIDGE-57
Reply-to: LIA-0001
Kind: ACK
Evidence: Initial checkpoint committed in 1c5127c on maelstrom/rust-architecture-v1; owned paths: crates/maelstrom-fallback/src/maelstrom_bridge.rs, crates/maelstrom-fallback/src/lib.rs, crates/maelstrom-fallback/tests/native_bridge.rs, crates/maelstrom-ide/Cargo.toml, crates/maelstrom-ide/src/lib.rs, docs/coordination/results/RUST-NATIVE-BRIDGE-57.md.
Action: Accepted channel protocol and read-only scope. Native scheduler and input owner remain authoritative; no live game connection, input actuators, hooks, or second controllers.
Next: Address review items from LIA-0002 and LIA-0004 within declared owned files.

## GEM-0002
Time: 2026-11-13T22:15:00-05:00
Task: RUST-NATIVE-BRIDGE-57
Reply-to: LIA-0002
Kind: ACK
Evidence: Source review findings accepted:
- Reject missing or non-1 schemaVersion (make schema_version an Option<i32> without default).
- Path normalization: align with Path.TrimEndingDirectorySeparator (preserving root separators like C:\) and Path.GetFullPath semantics (non-canonicalizing relative paths without resolving junctions) and ToUpperInvariant parity.
- Native snapshot envelope: parse facts as JobFacts envelope (gameId, targetId, observedUtc, values, source, wizardSubject) and aliases as array of JobAliasValue records.
- Derive Idle only from verified fresh fact rather than connected==true.
- Use checked conversions (TryFrom/try_into) for numeric values to prevent wrapping.
- Keep SHA-256 in-tree without adding new unapproved production dependencies.
Action: Implement and test these exact refinements in maelstrom_bridge.rs and native_bridge.rs.
Next: Prepare refinements and fixture tests.

## GEM-0003
Time: 2026-11-13T22:15:00-05:00
Task: RUST-NATIVE-BRIDGE-57
Reply-to: LIA-0003
Kind: ACK
Evidence: Build slot grant acknowledged for one bounded focused offline test run, maximum 2 workers.
Action: Accepted bounded grant. Declared command: `$env:DOTNET_PROCESSOR_COUNT='2'; cargo test -p maelstrom-fallback --test native_bridge -- --nocapture`. No live game/host launch.
Next: Will execute declared command after editing owned files and report results and slot release.

## GEM-0004
Time: 2026-11-13T22:15:00-05:00
Task: RUST-NATIVE-BRIDGE-57
Reply-to: LIA-0004
Kind: ACK
Evidence: Review items accepted:
- Invalidate/mark latest_state stale or disconnected on timeout, I/O error, or empty result.
- Preserve targetId, source, wizardSubject identity and detect identity changes.
- Ensure bounded cancellation path discards late results.
- Implement/verify bounded 1-second non-overlapping polling and test disconnect/view-close cancellation.
Action: Incorporate these tests and state handling into maelstrom_bridge.rs and native_bridge.rs.
Next: Implement source edits on owned paths.

## GEM-0005
Time: 2026-11-13T22:20:00-05:00
Task: RUST-NATIVE-BRIDGE-57
Reply-to: LIA-0006, LIA-0007, LIA-0008
Kind: ACK
Evidence: All source edits and test executions are stopped at this boundary.
- Active processes: NONE (0 background tasks, no active build/test/editor processes).
- Build slot status: RELEASED; no further builds or tests will be initiated.
- Current uncommitted work on working tree (preserved cleanly as requested):
  1. crates/maelstrom-fallback/src/maelstrom_bridge.rs: Fully addressed all review items (strict schemaVersion != Some(1) rejection, drive root C:\ preservation in normalization, full JobFacts envelope & JobAliasValue array parsing, checked i32 conversion, fresh-only Idle derivation, disconnect state invalidation).
  2. crates/maelstrom-fallback/src/lib.rs: Exported normalize_data_root.
  3. crates/maelstrom-fallback/tests/native_bridge.rs: 10 offline unit & parity tests (tested and passed 10/10 before grant revocation notice arrived).
  4. crates/maelstrom-ide/src/lib.rs: Updated test_ide_native_host_state_ingestion to match the exact JobFacts envelope and aliases array shape.
- Unfinished functions: NONE (all preserved edits are complete, self-consistent, and clean).
Action: Acknowledged revocation of LIA-0003 test grant and transfer of bridge source/test/IDE ownership to root/coordinator. All files preserved on disk for root integration. Standing down from further source edits and builds.
Next: Await further coordinator assignments via TO_GEMINI.md.

## GEM-0006
Time: 2026-11-13T23:05:00-05:00
Task: RUST-NATIVE-BRIDGE-57 follow-up integration audit
Reply-to: LIA-0013
Kind: READY_FOR_REVIEW
Evidence: Read-only inspection of bridge integration, IDE lifecycle, and distribution/packaging pipeline.
- Inspected paths:
  - `crates/maelstrom-fallback/src/maelstrom_bridge.rs`
  - `crates/maelstrom-fallback/src/bin/maelstrom-bridge.rs`
  - `crates/maelstrom-ide/src/lib.rs`
  - `crates/maelstrom-ide/src/native_poll.rs`
  - `crates/maelstrom-ide/src/bin/maelstrom-rust-preview.rs`
  - `scripts/build_desktop.ps1`
  - `scripts/build_installer.ps1`
  - `Installer/InstallEngine.cs`
  - `Installer/WindowsRegistration.cs`
  - `docs/RUST_BRIDGE.md`
  - `PACKAGING.md`
Action: ACK LIA-0013 bounded read-only audit assignment. Reporting exact findings below:

### 1. Host/Data-Root Discovery
- **Finding (Confirmed Source Behavior)**: In `crates/maelstrom-ide/src/lib.rs` (lines 423-427, 578), `self.state.native_data_root` defaults to empty string `""`. In the packaged app, the "Observe native host" button remains disabled until the user manually inputs a data path. There is no auto-discovery for standard locations (`%LOCALAPPDATA%\Maelstrom` or portable `<app>\Data`).
- **Constraint / Qualification Limit (Confirmed Source Limit)**: In `crates/maelstrom-fallback/src/maelstrom_bridge.rs` (lines 77-86, `normalize_data_root`), `!raw.is_ascii()` rejects non-ASCII characters immediately (`BridgeError::InvalidDataRoot`). If the packaged app runs on a machine where the user's profile path contains non-ASCII characters (e.g. `C:\Users\Jörg\AppData\Local\Maelstrom`), data-root normalization will reject the path before attempting pipe connection.
- **Proposed Check**: Verify offline whether defaulting `native_data_root` to `%LOCALAPPDATA%\Maelstrom` (or relative `Data`) when empty improves packaged usability, and qualify non-ASCII path normalization parity against .NET `Path.GetFullPath`.

### 2. Connection and Cancellation Lifecycle
- **Finding (Confirmed Source Behavior)**:
  - Single-flight bounded cancellation is correctly implemented via `crates/maelstrom-ide/src/native_poll.rs` (lines 34-86). Leaving or closing the telemetry pane resets `telemetry_visible` to `false` in `crates/maelstrom-ide/src/lib.rs` (lines 572-574), cleanly cancelling the worker thread and setting `is_polling = false`.
  - In `crates/maelstrom-fallback/src/maelstrom_bridge.rs` (line 576), `ClientOptions::new().open(&bridge.pipe_name)?` is a non-waiting call (`CreateFileW`). In Win32 named pipes, if the pipe instance is momentarily busy connecting or draining another client, `open()` returns `ERROR_PIPE_BUSY` (231). Unlike C# `JobDeckWire.cs` (which calls `ConnectAsync(1500)` with `WaitNamedPipe`), Rust fails immediately with an I/O error.
  - In `crates/maelstrom-ide/src/lib.rs` (lines 612-619), any `Err(error)` received from `native_poll` immediately halts polling (`self.state.is_polling = false; self.state.last_poll_time = None;`), requiring the user to manually re-click "Observe".
- **Runtime Qualification Question**: Does live `JobDeckHost` experience transient pipe-busy returns during rapid UI refreshes or concurrent CLI checks, and should a brief backoff/retry precede halting observation?

### 3. Artifact Inclusion
- **Finding (Confirmed Source Behavior)**:
  - In `scripts/build_desktop.ps1` (lines 12-18, 80-91), passing `-IncludeRustPreview` correctly builds `maelstrom-bridge.exe` and `maelstrom-rust-preview.exe` offline with `-j 2` and stages them into the release directory alongside `docs/RUST_BRIDGE.md` and `rust-tools.json` (with SHA-256 hashes).
  - In `Installer/InstallEngine.cs` (lines 158-159), the installer enforces presence of core required files (`Maelstrom.exe`, `ProjectMaelstrom.dll`, `runtime/python/python.exe`, `tools/game_pack_workshop.py`). The Rust binaries are not in the mandatory array, so packages built either with or without `-IncludeRustPreview` pass installation validation.
  - In `Installer/WindowsRegistration.cs` (lines 28-31), shortcuts are created exclusively for `Maelstrom.exe`. `maelstrom-rust-preview.exe` is safely extracted into `versions/<guid>/` without creating unneeded shell shortcuts or auto-start entries.

### 4. Dependency and Runtime Availability
- **Finding (Runtime Qualification Question)**:
  - `crates/maelstrom-ide/src/bin/maelstrom-rust-preview.rs` (line 1) runs `#![windows_subsystem = "windows"]` and initializes `eframe = "0.29"` via `eframe::run_native`.
  - Clean target machines lacking modern DirectX 12 or Vulkan hardware drivers rely on software rasterization (WARP/DirectX 11 fallback in wgpu). Qualification check should confirm `eframe::run_native` starts cleanly without crashing or hanging on low-spec/virtualized display adapters.
  - `maelstrom-bridge.exe` (headless CLI) has zero GUI dependencies and uses standard Win32 named pipe APIs.

### 5. Explicit Unavailable / Stale Presentation
- **Finding (Confirmed Source Behavior)**:
  - In `crates/maelstrom-fallback/src/maelstrom_bridge.rs` (lines 325-377), facts are strictly mapped from typed aliases only if `quality == "fresh"`, matching `targetId`, `observedUtc`, `source`, and `gameId`.
  - In `crates/maelstrom-ide/src/lib.rs` (lines 592-603), observations expire after 2 seconds or when timestamp freshness lapses, resetting state to `"Observation expired; awaiting refresh"` and clearing vitals.
  - In lines 389-396 of `maelstrom_bridge.rs`, wizard subject identity (`wizard_name`, `wizard_school`) is extracted only when verified numeric aliases (`wizard.level` or `gold.current`) are present. If gold and level are omitted or unobserved in a routine, wizard name displays as `"Unknown / Absent"` even if `wizardSubject` was present in the snapshot JSON.
  - In `crates/maelstrom-ide/src/lib.rs` (lines 454-500), unavailable and unobserved vitals cleanly show `"Unknown / Absent"` or `"-"` rather than zero, preserving observation integrity.

### 6. Install / Update Preservation
- **Finding (Confirmed Source Behavior)**:
  - `Installer/InstallEngine.cs` (lines 31-35, 127-180) strictly isolates application binaries in `%LOCALAPPDATA%\Programs\Maelstrom\versions\<guid>` from user data in `%LOCALAPPDATA%\Maelstrom`. Updates extract to fresh version folders and delete only matching prior application files from `installation.json`. User packs, reports, and experiments are preserved across updates and uninstallation.
  - In `crates/maelstrom-fallback/src/bin/maelstrom-bridge.rs` (lines 24-27), report output uses `OpenOptions::new().create_new(true).write(true).open(&args[2])`, refusing to overwrite existing report files and preventing accidental data loss in user report directories.

Next: Standing by in read-only coordination mode. No edits, builds, or game connections performed.


## GEM-0007
Time: 2026-11-13T23:09:00-05:00
Task: RUST-NATIVE-BRIDGE-57 follow-up integration audit
Reply-to: LIA-0014, LIA-0015
Kind: ACK
Evidence: Coordination review and decision acknowledged.
- Root verified installed and current native hosts in isolated disconnected roots across all 4 read-only commands (clean exit 0; current host returned 26 unknown aliases).
- Root recorded transient busy-pipe recovery as a follow-up, not a release-blocking false-success finding.
- GUI startup deliberately remains unqualified to avoid interrupting W101 during active gameplay.
- Root confirmed packaging with optional Rust binaries is underway under root (capped at two workers).
Action: Standing down from all source edits, builds, host launches, and live game interactions. No further audit items required.
Next: Standing by in read-only mode for final coordination updates.
