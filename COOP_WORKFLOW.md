# Co-op Workflow (Project Maelstrom)

## Final Phase Checklist
- UI polish locked: verify 150% / 175% captures via UiAuditSelfCapture.
- Regression lock: set baseline (`scripts/ui_set_baseline.ps1`) and run diff (`scripts/ui_check_regression.ps1`, threshold 0.5% default).
- Warning cleanup: zero compiler warnings before release (Debug/Release).
- Release readiness:
  - Portable build excludes DevTools and samples by default.
  - No secrets/tokens in repo or configs.
  - Document any known limits in PACKAGING.md / release notes.

## Fast Loop (one command)
- `./scripts/coop_loop.ps1 -Watch` (or without -Watch to prompt)  
  Steps: paste Codex output -> generates handoff (clipboard) -> paste into ChatGPT -> use `./scripts/handoff_to_codex.ps1` footer when prompting Codex.

## Preflight Secret Scan
- Run before push: `./scripts/scan_for_secrets.ps1`
  - Scans common extensions for ghp_/AIza/private keys.
  - Exit 1 if any hits; review/redact (vendor third-party assets if needed).

## Codex ↔ ChatGPT Handoff Bridge
Use a file-based, offline handoff to keep conversations clean and audit-safe.

1) From ChatGPT to Codex
   - Place ChatGPT output in `artifacts/handoff/from_codex/RESULT.md` (one fenced code block required).
   - Run: `./scripts/handoff_from_codex.ps1` (Windows) or `./scripts/handoff_from_codex.sh` (macOS/Linux) which call the cross-platform HandoffBridge tool (`import`).
   - Outputs:
     - `artifacts/handoff/reports/CODEX_REPORT.md` (redacted copy, stamped with version/profile/timestamp and scan PASS/FAIL header)
     - `artifacts/handoff/reports/CODEX_SUMMARY.md` (files/tests/warnings if present)

2) From Codex to ChatGPT
   - Run: `./scripts/handoff_to_codex.ps1` (Windows) or `./scripts/handoff_to_codex.sh` (macOS/Linux) which call the cross-platform HandoffBridge tool (`export`).
   - Generates:
     - `artifacts/handoff/to_codex/HANDOFF_TO_CODEX.md` (single copy-block prompt, stamped with version/profile/timestamp)
     - `CONTEXT_SNAPSHOT.md` (key doc paths)
     - `REPO_STATUS.txt` (branch/commit/status)
   - Secret scan runs; export fails closed if secrets are suspected (see `artifacts/handoff/reports/SECRET_SCAN.txt`, stamped with version/profile/timestamp).
   - Tool version: `dotnet run --project DevTools/HandoffBridge/HandoffBridge.csproj -- --version`
   - Rotation flags:
     - `--no-rotate` disables archiving/pruning of reports/scan logs (latest overwritten).
     - `--max-archives N` (0-200, default 10) sets report archive retention under `artifacts/handoff/reports/`.
   - `INDEX.txt` lives in `artifacts/handoff/reports/` and lists latest files plus archives (newest first).

3) Rules
   - One fenced code block per handoff.
   - No secrets; scripts enforce secret scanning/redaction.
   - Docs/scripts only; no runtime/policy/UI changes via handoff.

Selftest (tooling sanity)
- Run: `dotnet run --project DevTools/HandoffBridge/HandoffBridge.csproj -- selftest --allow-no-scan`
- Validates fence parsing, redaction, fail-closed scan behavior, and rotation/INDEX generation.
- Outputs: `artifacts/handoff/reports/SELFTEST.txt`
