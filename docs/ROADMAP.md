# Project Maelstrom & DevTools Roadmap

## Purpose
Project Maelstrom is a Windows automation suite with safe-by-default runtime guards. DevTools provides a local-first server + tray + toolkit to ingest webhook-triggered jobs, run an OpenAI-backed pipeline, and hand results back to the operator.

## Guiding principles
- **Safe by default:** live automation stays off unless explicitly enabled (policy/guards).
- **Local-first admin plane:** admin APIs/UI should be localhost-only (or protected by strict auth).
- **Webhook ingress is isolated:** webhook endpoints should be exposable via tunnel without exposing admin UI.
- **Auditable job lifecycle:** every job has a durable state machine + logs.
- **Small diffs > big refactors:** fix blockers with minimal changes, then harden.

## Current components
- **Maelstrom (WinForms):** automation suite + OCR/runner tooling.
- **MaelstromBot.Server:** webhook-driven job queue, OpenAI pipeline, admin APIs (/bot/api), dashboard (/bot/ui).
- **HandoffTray:** polls /bot/api, shows notifications, toggles automations, cancel latest awaiting job, configurable base URL/token.
- **MaelstromToolkit:** templating/handoff tooling; includes selftest gate in CI.
- **CI:** build + test + toolkit selftest on main.

## Milestone 0 — Stabilize the developer loop (must-have)
Goal: a new machine can follow README and get a full local demo working reliably.

Checklist:
- [ ] Server runs locally with no manual patching.
- [ ] `--init-admin-key` works reliably (DB schema init + key issuance).
- [ ] `/healthz` returns 200.
- [ ] `/bot/api/status` returns 200 with Bearer token; returns 401 without.
- [ ] Dual-port mode works (webhooks on webhook port only; /bot/* on admin port only).
- [ ] Tray can connect, show counts, notify, toggle automations, and cancel a job.

Definition of done:
- A “fresh clone” runbook exists in docs/TROUBLESHOOTING.md and README links to it.
- A small automated test exists to catch schema-init regressions (see Milestone 1).

## Milestone 1 — Hardening (security + reliability)
Goal: reduce footguns; make webhook/job handling robust and secure.

Security:
- [ ] Document all secrets/env vars and how to rotate them.
- [ ] Verify webhook signatures for GitHub and OpenAI; reject invalid signatures.
- [ ] Add replay protection (timestamp tolerance / idempotency keys).
- [ ] Ensure admin endpoints are never reachable from webhook port in dual-port mode.
- [ ] Ensure logs never print raw tokens/secrets.

Reliability:
- [ ] Schema init runs statement-by-statement or via migrations (no “giant SQL string” execution).
- [ ] Add a schema smoke test (SQLite in-memory or temp file).
- [ ] Job lifecycle is consistent and resilient (queued → awaiting_openai → completed_pending_fetch → completed; failures recorded).
- [ ] Add “stuck job” detection (timeouts / retry policies).

## Milestone 2 — DX & Operations
Goal: make local/demo and small-scale operations smooth.

- [ ] Add scripts:
  - start-server (single port)
  - start-server (dual port)
  - start-tray
  - reset-local-state (delete local DB + artifacts safely)
- [ ] Improve /bot/ui:
  - job detail view
  - copy result_text
  - filter/search by status
  - cancel button per-job
- [ ] Tray UX:
  - connection status indicator
  - last error tooltip
  - “open dashboard” shortcut
- [ ] Toolkit:
  - ship example templates
  - validate template schema in selftest

## Milestone 3 — Packaging & Releases
Goal: installable artifacts and predictable versioning.

- [ ] Add CHANGELOG.md and semantic version tags for DevTools + Toolkit.
- [ ] Build signed release zips (server + tray + toolkit).
- [ ] Optional: tray installer or single-file self-contained publish.
- [ ] Branch protection: CI required on main; protect tags.

## Backlog / future ideas
- Move dashboard HTML/JS out of Program.cs into static assets (wwwroot) to avoid quoting bugs.
- Add audit log + job artifact storage (screenshots, OCR output).
- Add role-based tokens (admin vs read-only).
- Add local metrics endpoint (job counts, latencies).
- Add plugin architecture for job types and automation capabilities.

