# UX Maintenance

Purpose
- Preserve cosmetic-only UX changes without affecting gold or behavior.

Tags
- v1.0.0-gold: canonical, immutable baseline (behavior/policy locked).
- v1.1.0-ux: UX V1 cosmetic checkpoint (Screen 1 + Screen 2 + tokens); gold unchanged.

Rules
- Cosmetic-only; no behavior or tab-order changes.
- Do not refresh UI baselines; artifacts under artifacts/ux/** remain ignored.
- Do not modify gold; do not retag gold.

Standard gates for any cosmetic fix
- dotnet build ProjectMaelstrom/ProjectMaelstrom.sln -c Debug (must PASS).
- Spot-check touched screens at 175% DPI and narrow window width (manual, not tracked).
- If non-trivial: run HandoffBridge review loop (handoff export/import, paste CODEX_REPORT to ChatGPT).

Feedback logging
- Record feedback verbatim in FEEDBACK_LOG.md (timestamp + source).
- Classify: doc-only / cosmetic fix / out-of-scope.
- For cosmetic fixes: =2 small commits, then re-freeze; rerun gates above.
