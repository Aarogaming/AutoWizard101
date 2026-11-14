# Teaching release 40 — 2026-11-12

Fresh recorded teacher cycle completed two actions and verified native Idle (`.local/teaching-release-40/ready-teacher.json`). Original action-39 card was frozen: SHA256 8c6937d586962b1760efc56e25aed43f6447e71b9e84b26a17c878059bf7e825. Its unchanged timeout passed against fresh-linked/session.json. Earlier observation/focus failures are preserved under the same local report directory, not counted as successful games.

2548 native tests, 14 installer tests and scoped formatting/analyzers passed before packaging. Portable Maelstrom-20261112-205801-d16bec and Setup-20261112-205845-6e2ae7 passed lifecycle and portable replay. Actual install-result.json confirms installation to version 19279bffeee44f5b9ffca7efc315c10b, with separate user data root preserved. Installed-holdout-replay.json also completes with one proposal/receipt, zero desktop dispatch and live_qualified=false.

This qualifies fresh-evidence replay, not generic live card dispatch or repeated autonomous gameplay. No model trained. HEADLESS_CLI documents recording, telemetry, annotation and card export. Active-symbol colors are installed; subsequent circle-only window shape is a separate source change in task 41.

Movement should accept awake or Idle state where safe: movement itself exits native Idle. Explicit wake remains operation-specific, never a universal card prerequisite. End-of-run Idle remains the bounded cleanup policy.
