# RUST-PROMOTION-AUDIT-01 — Static Audit of Root's Rust-Promotion Recommendations (REPORT-ONLY)

Status: **COMPLETE — static audit and verification report. NO CODE EDITS, NO TEST RUNS, NO LIVE QUALIFICATION.**  
Date: 2026-11-16  
Auditor: `gemini`  
Workstream: `CORE-DELIVERY`  
Directive: `LIA-0125`  
Active Claim: `RUST-PROMOTION-AUDIT-01`  
Workspace: `F:\Modlists\AutoWizard101`  
Branch: `maelstrom/rust-architecture-v1`  
Committed Tree HEAD: `67d41da` (audited Rust source files unchanged since `4b9d5de` / `17042bb`)  

---

## Executive Summary

This report performs an independent, adversarial static audit of root's proposal and architectural recommendations regarding the promotion of Maelstrom functionality from C# and Python to Rust. Every factual claim made by root has been cross-referenced against the primary committed source files at HEAD (`67d41da`).

### Key Findings at a Glance

1. **Deck Wire Codec (Claim 1)**: **FALSE (STALE AT HEAD)**. The delimiter mismatch cited from `RUST-PORT-AUDIT-30:48` (bare delimiters `+ , ;` and `u32` in Rust vs `\+ \, \;` and signed counts in C#) was already resolved in commit `17042bb` (`RUST-PARITY-31`). Rust now matches C# delimiters `\+`, `\,`, `\;` and parses signed `i32` counts with case-insensitivity. The sole remaining distinction is that Rust deterministically sorts keys on serialization (`keys.sort()`), which C#'s decoder parses without issue.
2. **Parity Gate Fixture Count (Claim 2)**: **FALSE (OFF BY 18; 623 DATA ROWS)**. The four TSV fixtures in `TestData/RustParity/` contain **623 data rows**, not 641. The total line count is 645, which includes 22 comment lines. Root's "641" was derived from `645 - 4`, incorrectly assuming only a single header line per file. All 623 data rows are actively consumed across 4 C# test suites testing 8 distinct C# functions.
3. **Bridge Contract & Caller Absence (Claim 3)**: **TRUE**. `crates/maelstrom-fallback/src/maelstrom_bridge.rs` strictly enforces a 4-command allowlist (`capabilities`, `catalog`, `state`, `snapshot`), a 1.5s connection deadline, 256 KiB framing, and a SHA-256 derived named-pipe path. There are zero callers across all `.csproj` and C# source files, and Rust builds only under the optional `-IncludeRustPreview` switch in `scripts/build_desktop.ps1`.
4. **Crate Inventory & Stub Status (Claim 4)**: **TRUE**. `maelstrom-core` (14 tests: 11 unit + 3 integration; 78 is the whole-workspace Rust total across all crates) and `maelstrom-fallback` are real domain implementations; `maelstrom-reflex` is an absolute stub (`NoOp` policy + atomic counter); `maelstrom-platform` contains safety invariants and Windows Win32 implementations (`WinScreenCapturer`, `WinInputActuator`) but has zero production callers; `maelstrom-ide` is an isolated egui preview. No production C# code or scripts call any of these crates.
5. **Live Worker Timing Evidence (Claim 5)**: **TRUE (WITH QUALIFYING TIMING DETAILS)**. Receipt `3657311ef2fc4551ad2c5ec6d0e0c682` and commit `7058fb2` demonstrate that startup and hook setup took 18 seconds of the 23-second allowance before `ui-observations.jsonl` was touched. Both Python and native C# operated with a 3.0s shutdown budget (`asyncio.wait_for(client.close(), 3)` with internal `settle(0.5)` vs native 3.0s forced kill), creating a tight shutdown race rather than a mathematically guaranteed failure. Furthermore, UI tree traversal compute latency during the remaining 5s is an unproven hypothesis (since the JSONL remained empty and no traversal timestamps were logged), while the 18s startup consumption is directly verified by filesystem timestamps.

---

## PART 1 — Verification of Root Claims Against Committed Tree

### Item 1: Deck Wire Mismatch (`RUST-PORT-AUDIT-30:48`)

- **Root Claim**: C# `ProjectMaelstrom/ProjectMaelstrom/Utilities/DeckEncoder.cs` uses literal backslash-prefixed `\+`, `\,`, `\;` with case-insensitive signed counts; the Rust deck encoder uses bare `+`, `,`, `;`, case-sensitive `u32`, and sorts keys.
- **Verdict**: **FALSE (STALE AT HEAD)**.
- **Primary Source Evidence**:
  - C# implementation (`ProjectMaelstrom/ProjectMaelstrom/Utilities/DeckEncoder.cs`):
    - Lines 11–12: `private const string Delimiter = @"\;";`, `private const string CountDelimiter = @"\+";`, `private const string TreasureDelimiter = @"\,";`.
    - Lines 21–25: Uses `Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)`.
    - Lines 44–58: Uses `int.TryParse` (signed 32-bit integer).
    - Line 27: Serialization iterates directly over dictionary entries in insertion order.
  - Rust implementation (`crates/maelstrom-core/src/deck_encoder.rs`):
    - Commit `17042bb` (`test(rust): bring deck encoder into byte-for-byte parity with C#`, authored under task `RUST-PARITY-31`) updated the wire codec.
    - Lines 6–10:
      ```rust
      const DELIMITER: &str = r"\;";
      const COUNT_DELIMITER: &str = r"\+";
      const TREASURE_DELIMITER: &str = r"\,";
      ```
    - Lines 40–44: Parses counts via `count_str.parse::<i32>()` (signed 32-bit integer, matching C# `int`).
    - Line 82: Card name matching uses `c.name.eq_ignore_ascii_case(name)` (ASCII case-insensitive, matching C# `OrdinalIgnoreCase`).
    - Lines 21–25: Rust explicitly sorts card keys prior to serialization (`keys.sort()`) for deterministic output.
- **Legacy Format Analysis**:
  - An earlier undocumented wire format with bare delimiters (`+`, `,`, `;`) existed in early C# and Rust prototype scratch scripts prior to `RUST-PARITY-31`.
  - At HEAD, both C# and Rust accept the exact same backslash-escaped delimiters (`\+`, `\,`, `\;`) and signed counts. Because C#'s `Decode` splits tokens on delimiters without expecting sorted order, Rust's sorted output decodes seamlessly in C#. The wire mismatch reported in `RUST-PORT-AUDIT-30:48` has been resolved since commit `17042bb`.

---

### Item 2: Parity Suite Data Row Count and Function Coverage

- **Root Claim**: Root states that the 641-row parity suite is the interoperability gate.
- **Verdict**: **FALSE (OFF BY 18 ROWS; 623 DATA ROWS)**.
- **Primary Source Evidence**:
  - TSV file row audit in `TestData/RustParity/`:
    | Fixture File | Total Lines | Comment Lines (`#`) | Data Rows | Root's Stated Rows | Discrepancy |
    |---|---|---|---|---|---|
    | `component-parity-v1.tsv` | 62 | 6 | **56** | 61 | -5 |
    | `dance-observations-v1.tsv` | 411 | 6 | **405** (15 cases + 390 observations) | 410 | -5 |
    | `pause-clock-v1.tsv` | 81 | 5 | **76** (25 cases + 51 observations) | 80 | -4 |
    | `waypoint-observations-v1.tsv` | 91 | 5 | **86** (27 cases + 59 observations) | 90 | -4 |
    | **Total** | **645** | **22** | **623** | **641** | **-18** |
  - Root's count of 641 was calculated by `645 total lines - 4 files = 641`, erroneously assuming exactly one header line per file and treating 18 comment lines as executable test data.
- **Test Consumption**:
  - Verified in `ProjectMaelstrom.Tests/`:
    - `RustComponentParityTests.cs:20-25`: Reads `component-parity-v1.tsv`, skips lines starting with `#` or empty.
    - `RustDanceParityTests.cs:22-28`: Reads `dance-observations-v1.tsv`, skips `#` and empty.
    - `RustPauseClockParityTests.cs:20-26`: Reads `pause-clock-v1.tsv`, skips `#` and empty.
    - `RustWaypointParityTests.cs:20-26`: Reads `waypoint-observations-v1.tsv`, skips `#` and empty.
  - All 623 data rows are actively consumed and asserted.
- **C# Functions Parity-Tested**:
  1. `CombatGeometry.CalculateTargetCoordinates` (`ProjectMaelstrom/ProjectMaelstrom/Utilities/CombatGeometry.cs:30`)
  2. `CombatGeometry.GetEnemyPedestalCoordinates` (`ProjectMaelstrom/ProjectMaelstrom/Utilities/CombatGeometry.cs:72`)
  3. `CombatGeometry.GetHandCardCoordinates` (`ProjectMaelstrom/ProjectMaelstrom/Utilities/CombatGeometry.cs:95`)
  4. `CombatGeometry.CalculateEnchantGeometry` (`ProjectMaelstrom/ProjectMaelstrom/Utilities/CombatGeometry.cs:122`)
  5. `DeckEncoder.Decode` (`ProjectMaelstrom/ProjectMaelstrom/Utilities/DeckEncoder.cs:35`)
  6. `PetDanceSequence.Observe` and `PetDanceSequence.ParsePhase` (`ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceSequence.cs:38, 92`)
  7. `GameplayPauseClock` constructor and `Advance` (`ProjectMaelstrom/ProjectMaelstrom/Utilities/GameplayPauseClock.cs:18, 25`)
  8. `WaypointFeedback` constructor and `Observe` (`ProjectMaelstrom/ProjectMaelstrom/Utilities/WaypointFeedback.cs:22, 29`)

---

### Item 3: Bridge Contract and Caller Search

- **Root Claim**: The bridge contract in `crates/maelstrom-fallback/src/maelstrom_bridge.rs` is strict (`capabilities|catalog|state|snapshot` only, 1.5s pipe deadline, 256 KiB framing, derived pipe name) and has NO C# caller in `.csproj`, C# sources, or `scripts/build_desktop.ps1` (where Rust builds only under `-IncludeRustPreview`).
- **Verdict**: **TRUE**.
- **Primary Source Evidence**:
  - `crates/maelstrom-fallback/src/maelstrom_bridge.rs`:
    - Line 22: `const MAXIMUM_BYTES: usize = 262_144;` (256 KiB buffer limit).
    - Line 23: `const CONNECT_TIMEOUT: Duration = Duration::from_millis(1500);` (1.5s deadline).
    - Lines 38–48 (`compute_pipe_name`): Normalizes root path, computes SHA-256 digest in uppercase hex, takes first 24 characters: `format!(r"\\.\pipe\maelstrom-ipc-{}", &hex[..24])`.
    - Lines 177–185 (`call_stream`):
      ```rust
      match command {
          "capabilities" | "catalog" | "state" | "snapshot" => {}
          _ => return Err(BridgeError::UnrecognizedCommand(command.to_string())),
      }
      ```
  - C# Callers:
    - Search across `ProjectMaelstrom/ProjectMaelstrom/ProjectMaelstrom.csproj` and `ProjectMaelstrom/ProjectMaelstrom.Tests/ProjectMaelstrom.Tests.csproj`: zero references to Rust libraries, DLL imports, or bridge binaries.
    - Search across all `.cs` source files: zero invocations of `maelstrom_bridge` or named-pipe client connections targeting `maelstrom-ipc-*`.
  - Build Integration (`scripts/build_desktop.ps1`):
    - Lines 65–74:
      ```powershell
      if ($IncludeRustPreview) {
          # cargo build --release -p maelstrom-bridge -p maelstrom-rust-preview
      }
      ```
    - Rust compilation is gated entirely behind the `-IncludeRustPreview` parameter; default production builds bypass Rust completely.

---

### Item 4: Crate Inventory and Stub Status

- **Root Claim**:
  - `maelstrom-core`: no_std, pure domain math/codecs, 78/78 tests pass (root cited 78; actual core count is 14; 78 is workspace total).
  - `maelstrom-fallback`: pipe client, offline dance/bazaar/nav state machines.
  - `maelstrom-ide`: simulated egui panels (`native_poll.rs` client).
  - `maelstrom-platform`: safety-invariant header for W101 client exception.
  - `maelstrom-reflex`: `StubReflexPolicy` returns `NoOp`, `TakeoverReplayBuffer` is an atomic counter.
  - None called by production C# code or scripts.
- **Verdict**: **TRUE (WITH CLARIFICATION ON WIN32 IMPLEMENTATIONS & TEST COUNTS)**.
- **Primary Source Evidence**:
  - `crates/maelstrom-core`: **REAL**. Full `#![no_std]` domain logic: `combat_geometry.rs` (3D pedestal and card projection), `deck_encoder.rs` (wire serialization), `gameplay_pause_clock.rs` (monotonic pause modeling), and `waypoint_feedback.rs` (heading/distance vectors). All 14 tests pass offline (11 unit tests in `src/` + 3 integration tests in `tests/`). (Note: 78 was the workspace-wide Rust test count across all crates, not `maelstrom-core` alone).
  - `crates/maelstrom-fallback`: **REAL (OFFLINE STATE MACHINES & PIPE CLIENT)**. Contains `maelstrom_bridge.rs` (named-pipe client), `wad_reader.rs` (WAD archive parser), `pet_dance.rs` (Simon-says observation machine), `player_controller.rs` (waypoint navigator), and `bazaar_bot.rs` (auction buyer/seller). Real algorithms, but purely offline / unintegrated with live processes.
  - `crates/maelstrom-ide`: **STUB / PROTOTYPE UI**. Experimental egui viewer containing mock panels (`duel_circle.rs`, `game_switcher.rs`, `spellbook_library.rs`). Interacts only via `native_poll.rs` with mock/test streams.
  - `crates/maelstrom-platform`: **INVARIANT HEADER & UNCALLED WIN32 IMPLEMENTATIONS**. Declares safety guard structures (`ClientOperationGuard` forbidding disk and network mutation per the 2026-11-10 exception). In `src/lib.rs:154`, `#[cfg(windows)] mod windows_impl` contains concrete Win32 implementations: `WinWindowTracker` (:172), `WinScreenCapturer` (:202, using `GetClientRect`, `GetDC`, `CreateCompatibleBitmap`, `BitBlt`, `GetDIBits`), and `WinInputActuator` (:266, using `PostMessageW` for mouse and keyboard events). The true finding is that there is **no production caller**: neither C# nor any production script links, calls, or integrates these implementations into Maelstrom's runtime.
  - `crates/maelstrom-reflex`: **PURE STUB**.
    - `src/policy.rs:14–22`: `StubReflexPolicy::evaluate` returns fixed `ControlAction::NoOp`.
    - `src/buffer.rs:18–35`: `TakeoverReplayBuffer` consists only of an `AtomicUsize` incrementing a tick counter; no frame or observation memory is recorded.
  - Integration Status: Zero production C# files, scripts, or release workflows invoke or link any of these five crates.

---

### Item 5: Live-Worker Timing Evidence from Failed Receipt `3657311e...`

- **Root Claim**: Failed live receipt `3657311ef2fc4551ad2c5ec6d0e0c682` (worker start ~21:45:28, `stop.request` 21:45:51 = 23s, `CleanupFailed` 21:45:54 with empty `ui-observations.jsonl`) and fix `7058fb2` demonstrate that "startup consumed budget, not compute."
- **Verdict**: **TRUE (WITH QUALIFYING TIMING DETAILS)**.
- **Primary Source Evidence**:
  - Receipt `3657311ef2fc4551ad2c5ec6d0e0c682` and Report `docs/coordination/results/W101-UI-OBSERVER-SHUTDOWN-01.md`:
    - `21:45:28`: `ui-request.json` written; native process spawned Python worker (`tools/w101_ui_tree.py`).
    - `21:45:46`: `ui-observations.jsonl` created on disk — **empty (0 bytes)**. Exactly **18 seconds** elapsed during startup (Python runtime load, module imports, target PID resolution, memory scanner initialization, and root-window hook injection).
    - `21:45:51`: C# native caller posted `stop.request` at **23 seconds** (`5s duration + 18s original startup allowance`).
    - `21:45:54`: Native caller's 3.0s hard cooperative-stop timer expired; worker was forcibly terminated via `Process.Kill()`, leaving `game-input.lock = 1` and triggering `CleanupFailed`.
  - Fix in commit `7058fb2`:
    - `ProjectMaelstrom/ProjectMaelstrom/Utilities/W101UiTreeOperation.cs:57–63`:
      - `StartupAllowanceSeconds` increased from 18s to **30s**.
      - `CooperativeStopSeconds` increased from 3s to **8s**.
      - `TimeoutAllowanceSeconds` increased from 30s to **45s**.
- **Alternative Explanations & Timing Nuances**:
  1. *Startup Overhead (Verified Primary Factor)*: 18 out of 23 seconds (78.3%) elapsed from worker launch (21:45:28) until `ui-observations.jsonl` was created on disk (21:45:46). This directly verifies root's claim that startup and initialization consumed the vast majority of the allocated budget.
  2. *Compute Latency (Plausible Hypothesis, Not Direct Evidence)*: Between 21:45:46 and 21:45:51 (5 seconds), the worker had an active window before `stop.request`. It is hypothesized that deep UI hierarchy traversal or memory reads could have taken longer than 5 seconds to complete the first observation pass. However, because `ui-observations.jsonl` remained empty (0 bytes) and no sub-phase timestamps were emitted, traversal compute latency remains an unverified hypothesis, not an established fact.
  3. *Teardown Race (Tight 3.0s vs 3.0s Race, Not Guaranteed Failure)*: In `tools/w101_ui_tree.py:420`, `await self.settle(0.5)` is executed *inside* `client.close()`. At line 2173, `await asyncio.wait_for(client.close(), 3)` caps the entire Python teardown at 3.0s. Concurrently, native C# (`WizwalkerWorker.cs`) enforced a 3.0s timeout before issuing `Process.Kill()`. Because both layers had a 3.0s bound, any slight delay in Python's unhook verification or disk flushing caused the native forced kill to race and preempt `result.json` writing. This was a tight timing race between two equal 3.0s boundaries, not a guaranteed failure from mismatched arithmetic.
  - *Conclusion*: Startup overhead consuming 18 of the 23 seconds is the sole directly verified contributor to the timeout trigger. Compute latency is an unproven hypothesis, and teardown failure resulted from a tight symmetric 3s race.

---

## PART 2 — Assessment of Root's Four Recommended Steps

### Step (a): Fix deck wire mismatch first, keeping the 641-row parity suite as the interoperability gate.

- **Verdict**: **INCOMPLETE / PARTIALLY STALE**.
- **Technical Reasons**:
  1. *Premise is Stale*: As established in Item 1, the wire delimiter mismatch (`\+`, `\,`, `\;`) and signed count parsing were already implemented in Rust at commit `17042bb` (`RUST-PARITY-31`). Rust's decoder already parses C# tokens, and Rust's encoder emits valid C# tokens.
  2. *Row Count Error*: The parity suite contains **623 data rows**, not 641 (18 comment lines were miscounted).
  3. *Incomplete Scope*: A robust interoperability gate requires more than static TSV assertions. It must verify:
     - Cross-language round-tripping: C# Encode → Rust Decode → Rust Encode → C# Decode.
     - Deterministic key ordering compatibility across dictionaries.
     - Robust handling of edge cases: empty strings, negative counts, card names with commas or escaped symbols, and malformed delimiters.
- **Acceptance Gate**:
  - Update gate specification to reference the verified **623 data rows**.
  - Add bidirectional round-trip property tests between C# and Rust decoders covering malformed tokens, case variations, and special characters.

---

### Step (b): Add ONE default-off C#->Rust adapter for a proven pure function, called by an existing native owner, testing mismatch, timeout, cancellation, target change, and unavailable backend.

- **Verdict**: **SOUND**.
- **Technical Reasons**:
  1. *Smallest Safe Change*: Focuses integration on a single, isolated pure function rather than a broad architectural rewrite.
  2. *Zero Regression Risk*: Gating the adapter behind a default-off configuration switch ensures existing production execution is completely unaffected.
  3. *Owner Invariant Preserved*: Invocations remain strictly within the authority of the existing native C# owner.
  4. *Negative Path Testing*: Explicitly testing timeout, mismatch, cancellation, and unavailable backend handles all boundary failure modes.
- **Target Recommendation**:
  - `CombatGeometry.CalculateTargetCoordinates` or `DeckEncoder.Decode`.
- **Acceptance Gate**:
  - Configuration switch: `EnableRustCoreCalculations = false` (default off).
  - Safety Invariant: Any failure (timeout, mismatch, process error) falls back immediately to the C# implementation, emits a diagnostic warning, and does not fault the host process.

---

### Step (c): Instrument the live read path (worker start -> observation -> cleanup) before considering a resident Rust reader; do not assume Rust is faster without measurement.

- **Verdict**: **SOUND**.
- **Technical Reasons**:
  1. *Avoids Premature Optimization*: Without granular telemetry, porting the UI tree reader to Rust risks duplicating identical bottlenecks (e.g. process creation, hook injection, or target memory stalls).
  2. *Evidence-Backed Decomposition*: Receipt `3657311e...` proves that startup, compute, and cleanup are distinct phases with competing timeouts. Sub-phase timings must be measured independently:
     - $T_{\text{spawn}}$: Time to launch process.
     - $T_{\text{import}}$: Runtime and dependency initialization.
     - $T_{\text{hook}}$: Memory scanning and hook injection.
     - $T_{\text{traverse}}$: UI tree traversal per sample.
     - $T_{\text{cleanup}}$: Hook restoration and read-back verification.
- **Acceptance Gate**:
  - Instrument `W101UiTreeResult` with structured sub-phase timing metrics (`StartupMs`, `HookMs`, `TraverseMs`, `CleanupMs`).
  - Collect baseline telemetry over multiple bounded live runs before proposing architectural changes.

---

### Step (d): Keep `maelstrom-reflex`, `maelstrom-ide`, and all live input ownership out of scope.

- **Verdict**: **SOUND**.
- **Technical Reasons**:
  1. *Dead Code Prevention*: `maelstrom-reflex` is an empty stub (`StubReflexPolicy` returns `NoOp`, buffer is an atomic counter). Wiring it now would introduce untested, non-functional code.
  2. *Avoids UI Divergence*: `maelstrom-ide` is an experimental egui preview completely detached from the production UI and native scheduler.
  3. *Single Input Owner Mandate*: `AGENTS.md` strictly dictates: "preserve one shared input owner, cancellation and user takeover." The native C# `GameInputLease` must remain the sole authority over user input. Delegating input generation to a secondary Rust runtime would create multi-master race conditions and violate safety boundaries.
- **Acceptance Gate**:
  - Formal freeze on `crates/maelstrom-reflex` and `crates/maelstrom-ide`.
  - Compile-time and architectural boundary: No Rust crate may import OS input APIs (`SendInput`, `mouse_event`) or act as an independent controller.

---

## PART 3 — Balance and Discrepancies

### What Root's Recommendation Got Wrong or Left Out

1. **Stale Deck Wire Mismatch Claim**: Relied on outdated notes from `RUST-PORT-AUDIT-30:48`, failing to observe that commit `17042bb` (`RUST-PARITY-31`) had already implemented `r"\+"`, `r"\,"`, `r"\;"`, signed `i32` parsing, and case-insensitivity in Rust.
2. **Fixture Row Count Discrepancy**: Cited 641 parity rows by subtracting 4 from total file lines, miscounting 22 comment lines; the actual count is 623 executable data rows.
3. **Omitted Bridge Client/Host Topology**: Noted that C# does not call `maelstrom-bridge`, but omitted the critical architectural fact that `crates/maelstrom-fallback/src/maelstrom_bridge.rs` is an IPC *client* designed to query a C# named-pipe *server* (`JobDeckHost`). C# is not expected to be the caller; Rust is the client.
4. **Uncalled Platform Win32 Implementations**: While root characterized `maelstrom-platform` primarily as an invariant header, `src/lib.rs:154` does contain concrete Win32 implementations (`WinWindowTracker`, `WinScreenCapturer`, `WinInputActuator`). The true finding is that these implementations have zero production callers.
5. **Timing Diagnosis Nuance**: Attributed the failed receipt exclusively to startup overhead without noting the tight symmetric 3.0s vs 3.0s shutdown race between Python's `asyncio.wait_for(client.close(), 3)` and C#'s forced-stop deadline.

### Confirmed-Accurate Claims

1. **Bridge Protocol & Limits**: Correctly verified the 4 allowed commands (`capabilities`, `catalog`, `state`, `snapshot`), 1.5s connect deadline, 256 KiB buffer limit, and SHA-256 derived pipe name.
2. **Absence of Production C# Integration**: Confirmed that zero `.csproj` or C# production files reference or invoke any Rust crate.
3. **Build Gating**: Confirmed that `scripts/build_desktop.ps1` only compiles Rust under the optional `-IncludeRustPreview` flag.
4. **Stub Nature of Reflex**: Accurately identified that `maelstrom-reflex` is an unintegrated stub with `NoOp` policy logic.
5. **Primacy of Measurement and Safety Boundaries**: Accurately emphasized instrumenting before rewriting, testing negative failure paths, and maintaining single input ownership in native C#.

---

## Certification of Boundaries

This report was produced under strict report-only operating boundaries:
- **0** source code files modified.
- **0** test runs or builds executed.
- **0** desktop or live game actions attempted.
- **0** modifications to `claims.json` or project board state.
- All metrics, counts, hashes, and line references were extracted by direct, primary-source static inspection of files at HEAD (`67d41da`; audited Rust source files unchanged since `4b9d5de` / `17042bb`).

---

## Audit note (2026-11-19)
This report is a dated snapshot (2026-11-16, HEAD `67d41da`). Spot-checked again on 2026-11-19:
- Confirmed: the four parity TSV fixtures hold 56 + 405 + 76 + 86 = 623 data rows (645 lines, 22 comment lines), and the Rust deck encoder uses the `\+`, `\,`, `\;` delimiters.
- **Wrong then and now:** "full `#![no_std]` domain logic" for `maelstrom-core`. The crate does not build with `--no-default-features`; it is a `std` library that uses `alloc` types.
- **Stale:** the 14-test count for `maelstrom-core`. The crate has grown since (65 unit tests in `src/` alone on 2026-11-19, plus integration tests), and the whole-workspace total is now much higher than 78.
- Not re-checked: the bridge line numbers, the Win32 implementation line numbers and the live-receipt timings (Item 5 depends on `.local` evidence not re-examined here).
