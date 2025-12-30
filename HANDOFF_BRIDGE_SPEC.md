# Handoff Bridge Spec (Project Maelstrom)

Scope: Cross-platform Codex <-> ChatGPT handoff, docs/scripts/tools only. Writes under artifacts/handoff/**.

Folder layout:
- artifacts/handoff/to_codex/ (HANDOFF_TO_CODEX.md, CONTEXT_SNAPSHOT.md, REPO_STATUS.txt)
- artifacts/handoff/from_codex/ (RESULT.md required; FILES_TOUCHED.txt, TESTS_RUN.txt, NOTES.md optional)
- artifacts/handoff/reports/ (CODEX_REPORT.md, CODEX_SUMMARY.md, SECRET_SCAN.txt)

Rules:
- RESULT.md must contain exactly one fenced code block; import fails otherwise.
- Export/Import exit nonzero on any validation/scan failure; no partial outputs if validation fails.
- Naming is stable; update docs if names change.
- Fail closed on suspected secrets (scanner script or fallback).
- Profiles: docs (default, no UI visual changes) or ux (cosmetic UI allowed; runtime/policy/packaging/tests forbidden).
- Branch-agnostic prompts (uses git branch name).
- Stamps include tool version/profile/timestamp on key outputs (HANDOFF_TO_CODEX.md, CODEX_REPORT.md, SECRET_SCAN.txt).
- DO NOT TOUCH runtime/policy/packaging/tests via this bridge.

Commands (HandoffBridge):
- export: generates to_codex/ files; runs secret scan; validates single fenced block in HANDOFF_TO_CODEX.md.
- import: validates RESULT.md, redacts, writes reports; runs secret scan; marks report header PASS/FAIL.
- --version: prints tool version (also shown in stamps).

Wrappers:
- scripts/handoff_to_codex.{ps1,sh}
- scripts/handoff_from_codex.{ps1,sh}

Usage (minimal):
1) Export: run wrapper -> open HANDOFF_TO_CODEX.md -> paste into ChatGPT.
2) Import: save ChatGPT reply to from_codex/RESULT.md (one fenced block) -> run wrapper -> use CODEX_REPORT.md.

Phase 38: version stamping + profile-aware prompts + stamped reports complete.
