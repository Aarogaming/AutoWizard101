# GEM-CLI-RUNBOOK-AUDIT-01 — Static Source Audit of CLI Agent Operating Procedures

**Task ID**: `GEM-CLI-RUNBOOK-AUDIT-01`  
**Workstream**: `CORE-DELIVERY`  
**Author**: Gemini  
**Date**: 2026-11-15T22:36:00-05:00  
**Repository Revision**: `7888cf251388dd904bac3977c70f73d0a6a31424` (`maelstrom/rust-architecture-v1` HEAD)  
**Status**: READ-ONLY STATIC CODE AUDIT (COMPLETED / READY FOR REVIEW)  
**Owned Output**: `docs/coordination/results/GEM-CLI-RUNBOOK-AUDIT-01.md`  

---

## 1. Executive Summary & Audit Scope

This report fulfills directive `LIA-0119` under active claim `GEM-CLI-RUNBOOK-AUDIT-01`. 

A comprehensive, line-by-line static source audit was performed comparing the newly introduced agent runbook [docs/CLI_AGENT_OPERATING_PROCEDURES.md](file:///F:/Modlists/AutoWizard101/docs/CLI_AGENT_OPERATING_PROCEDURES.md) against current production implementations in:
1. [ProjectMaelstrom/ProjectMaelstrom/Utilities/MaelstromCli.cs](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Utilities/MaelstromCli.cs)
2. [ProjectMaelstrom/ProjectMaelstrom/Program.cs](file:///F:/Modlists/AutoWizard101/ProjectMaelstrom/ProjectMaelstrom/Program.cs)
3. [docs/HEADLESS_CLI.md](file:///F:/Modlists/AutoWizard101/docs/HEADLESS_CLI.md)

### Audit Verdict: **NO MATERIAL DISCREPANCIES FOUND**
The runbook is technically rigorous, fully aligned with production C# source, and accurately describes invocation semantics, process flags, target identity constraints, cancellation channels, and exit code meanings.

---

## 2. Detailed Technical Verification Matrix

| Runbook Section | Stated Operational Contract | Source Verification Reference | Alignment Status |
| :--- | :--- | :--- | :--- |
| **§2: Entry Point & High DPI** | GUI `WinExe` with headless `--cli` switch; establishes `PerMonitorV2` DPI awareness prior to window creation. | `Program.cs:56–61` (`Application.SetHighDpiMode(HighDpiMode.PerMonitorV2)`) | **VERIFIED ACCURATE** |
| **§2: Target Framework** | Targets `net9.0-windows10.0.22621.0`. | `ProjectMaelstrom.csproj:5` & `ProjectMaelstrom.Tests.csproj:4` | **VERIFIED ACCURATE** |
| **§2: Output Creation Semantics** | Mandatory `--output <path>` with pre-existing parent folder; uses `CreateNew` semantics; never overwrites existing files. | `MaelstromCli.cs:364` (`new FileStream(options.Output, FileMode.CreateNew, ...)`) | **VERIFIED ACCURATE** |
| **§2: PowerShell Invocation** | Invokes hidden GUI process with `Start-Process -FilePath $app -ArgumentList $arg -WindowStyle Hidden -Wait -PassThru` to capture exit codes. | Standard Win32 `WinExe` invocation pattern in PowerShell. | **VERIFIED ACCURATE** |
| **§2: Window Identity Triple** | W101 operations require `handle`, `processId`, and `startedUtcTicks` from the exact same row returned by `--cli windows`. | `MaelstromCli.cs:405–407` & `W101ControlScope.cs` | **VERIFIED ACCURATE** |
| **§3: Command Catalog** | Lists discovery (`help`, `windows`, `status`), selected-client reads, bounded actions, and recovery. | Matches 23 registered commands in `MaelstromCli.cs:37` (`Commands`). | **VERIFIED ACCURATE** |
| **§3: Opt-in Flags** | Requires explicit opt-ins: `--enable-client-observation`, `--enable-ui-tree`, `--enable-module-actions`, `--enable-quest-actions`. | `MaelstromCli.cs:54–120` (rejects unapproved actions/reads without explicit flags). | **VERIFIED ACCURATE** |
| **§3: Cancellation via Stop-File** | `--stop-file <path>` must not exist at launch; creation while running triggers cancellation. Ctrl+C cancels via token. | `MaelstromCli.cs:431, 547, 587` & `Program.cs:63–64` | **VERIFIED ACCURATE** |
| **§3: Input Recovery Flow** | `recover-input` is a dedicated recovery command, not an ordinary routine finalizer; recovery hold must be respected. | `MaelstromCli.cs:372–378` (`CliInputRecovery.RunAsync`) | **VERIFIED ACCURATE** |
| **§4: Process Exit Codes** | `0` = Completed; `1` = Argument/setup/report error; `2` = Not completed / failure receipt; `3` = Cancelled. | `MaelstromCli.cs:435` & `MaelstromCli.cs:479` | **VERIFIED ACCURATE** |
| **§4: Offline Replay Switches** | Separate top-level switches: `--replay-routine <routine> <session> <report>` and `--replay-pet-dance-visit <obs> <report>`. | `Program.cs:103–121` | **VERIFIED ACCURATE** |

---

## 3. Implementation Nuances & Non-Blocking Observations

The audit identified three minor nuances that do not constitute defects, but are valuable operational context for agent callers:

1. **Structured Error Output on Exit Code 1 / 3**:
   - `CLI_AGENT_OPERATING_PROCEDURES.md:48–50` notes that stdout is detached and callers must rely on the JSON report and process exit code.
   - *Source behavior*: In `MaelstromCli.cs:457–470`, even when an unhandled exception or early cancellation occurs (yielding exit code `1` or `3`), `RunAsync` catches the exception and writes a structured JSON error report to the `--output` path:
     `{ "schemaVersion": 1, "status": "command-error" | "cancelled", "message": "..." }`
   - *Callers*: Agents can safely parse the `--output` JSON file even on non-zero exit codes to retrieve the exact error explanation.
2. **Stop-File Pre-Existence Mechanics**:
   - The runbook instructs: *"It must not exist at launch; create that exact file while the operation is running to request cancellation."*
   - *Source behavior*: In `MaelstromCli.cs:431`, if the file at `--stop-file` exists when `CheckAsync` begins, it immediately throws `OperationCanceledException`, resulting in an instant exit code `3` (`cancelled`) rather than an argument error (`1`). The runbook's warning correctly prevents accidental immediate cancellation.
3. **Top-Level Flags vs. `--cli` Subcommands**:
   - The runbook clearly distinguishes `--cli` from top-level offline replay tools (`--replay-routine`, `--replay-pet-dance-visit`).
   - In `Program.cs:50`, the Card Deck API switches (`--deck-api`, `--deck-host`, `--render-job-deck`, `--render-card-workshop`) are also top-level switches rather than `--cli` subcommands. The runbook correctly restricts its operational guidance to the headless `--cli` interface and references `HEADLESS_CLI.md` for broader catalog options.

---

## 4. Conclusion & Recommendation

The runbook `docs/CLI_AGENT_OPERATING_PROCEDURES.md` is **source-complete, technically accurate, and safe for agent handoffs**. It provides operators with clear boundaries, correct PowerShell invocation templates, and explicit warnings against false assumptions of live gameplay qualification.

No revisions to `CLI_AGENT_OPERATING_PROCEDURES.md` are required. Claim `GEM-CLI-RUNBOOK-AUDIT-01` is ready for acceptance and release.
