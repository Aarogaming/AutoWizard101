# Verification Guide (Project Maelstrom)

## Checksums
- Portable: `artifacts/submission/checksums_portable.txt`
- Submission artifacts: `artifacts/submission/checksums_submission.txt`
- Verify on Windows PowerShell:
  ```pwsh
  Get-ChildItem artifacts/portable -Recurse | Get-FileHash -Algorithm SHA256
  Get-ChildItem artifacts/submission -File | Get-FileHash -Algorithm SHA256
  ```
  Compare against the stored checksum files above.

## How to Run (Offline / Simulation-only)
1) Extract `artifacts/portable/`.
2) Ensure `execution_policy.conf` stays at `ALLOW_LIVE_AUTOMATION=false` and Public profile.
3) Run `ProjectMaelstrom.exe` (no network required).
4) DevTools are excluded; samples included only if previously built with `-IncludeSamples`.

## Reports
- Full gate run: `artifacts/submission/final_verify_report.txt`
- UI regression: `artifacts/submission/ui_diff_report.txt`
- Functional tests: `artifacts/submission/functional_test_runner.txt`
- Secret scan: `artifacts/submission/secret_scan.txt`
- Checksums: see above

## Policy Boundary
- Simulation-only by default; live automation is disabled (`ALLOW_LIVE_AUTOMATION=false`).
- Portable build honors the same defaults.
