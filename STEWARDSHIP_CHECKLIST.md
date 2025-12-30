# Stewardship Checklist

Controlled wait
- Do nothing unless feedback arrives.
- Keep gold immutable (v1.0.0-gold).
- Keep artifacts/ux/** ignored; no baseline refresh.

Feedback handling
- Log feedback verbatim in FEEDBACK_LOG.md (timestamp + source).
- Classify: doc-only / cosmetic fix / out-of-scope.
- Cosmetic fixes: =2 tiny commits, then re-freeze.

Gates for cosmetic fixes
- dotnet build ProjectMaelstrom/ProjectMaelstrom.sln -c Debug (PASS).
- Spot-check at 175% DPI and narrow window for touched screens.
- For non-trivial changes: use HandoffBridge review loop (export --profile ux --template ux; import; paste CODEX_REPORT to ChatGPT).

Tagging
- UX tags: create a new UX tag (e.g., v1.1.x-ux) only when explicitly requested; never touch v1.0.0-gold.
- Tooling tags: for DevTools-only changes (e.g., handoff bridge), tag separately (v1.0.x-tools) after selftest/CI.

Never do
- Do not retag gold.
- Do not change policy/runtime/executors/plugins/packaging/test harness.
- Do not alter tab order or behavior.
- Do not refresh UI baselines.
