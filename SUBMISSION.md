# Submission Guide (Project Maelstrom)

## Overview
Project Maelstrom is an offline-first, simulation-only trainer. Live automation is disabled by default (`ALLOW_LIVE_AUTOMATION=false`) and the standard profile is Public. Architecture is capability- and plugin-driven (overlay widgets, replay analyzers, minigame catalog), with DevTools excluded from portable builds.

## Security Posture
- No secrets in repo; secret scan tooling and redactions are in place.
- GitHub secret scanning alerts addressed via redaction and SECURITY_NOTES.md guidance.
- Execution policy defaults to safe simulation; live integration remains blocked unless explicitly enabled.

## Repro / Verification (one command)
- Run all gates:
  `powershell -ExecutionPolicy Bypass -File ./scripts/final_verify.ps1`
- Portable + verify (optional):
  `powershell -ExecutionPolicy Bypass -File ./scripts/final_verify.ps1 -Portable`

## Reports and Artifacts
- Final verify report: `artifacts/submission/final_verify_report.txt`
- Portable verify report: `artifacts/submission/final_verify_portable_report.txt` (when run)
- UI regression report: `artifacts/ui_diff_report.txt`
- Functional tests: `artifacts/submission/functional_test_runner.txt`
- Secret scan: `artifacts/submission/secret_scan.txt`
- Portable output: `artifacts/portable` (DevTools excluded; samples only when `-IncludeSamples`)

## Quality Gates (all PASS)
- Build (Debug)
- UI regression (baseline approved)
- FunctionalTestRunner
- Secret scan
- Portable build (when requested)

## References
- RELEASE_READINESS.md
- PACKAGING.md
- COOP_WORKFLOW.md
- SECURITY_NOTES.md
