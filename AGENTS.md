# Aaroneous Automation Suite — Codex Working Agreement (AGENTS.md)

## Mission
Maelstrom is a lightweight desktop game-player and experiment workbench: users select a game workspace, record and teach reusable game packs, and run bounded routines whose outcomes are verified from observations. A game pack supplies game-specific knowledge; shared capture, action ownership, transitions and replay remain reusable across games.

## Golden rules
- Make the smallest safe change that solves the task.
- Prefer adding/adjusting tests over “just changing code”.
- Never commit secrets. Don’t print secrets in logs.
- If a requirement is ambiguous, propose 2-3 options and ask the user (me) which to choose.

## Response format (every plan, proposal, issue response and report)
Every response that proposes, performs or reports work covers all of: **Who** (owner, implementer, approver, who is affected), **What** (deliverable and what is out of scope), **When** (order, trigger, what it waits on), **Where** (paths, systems, offline vs live, branch/PR), **Why** (problem, risk, evidence of need), **How** (concrete steps, commands, tests), plus **Before** (preconditions, authorizations, baselines, backups), **During** (safeguards, stop conditions, who watches, what is logged) and **After** (verification, evidence, cleanup/rollback, follow-ups, what remains unproven).
- Short items are fine; write `n/a - reason` for an item that does not apply. Silently dropping one is not allowed.
- Written plans and reports under `docs/` that opt in with `<!-- response-format: 5w1h-bda -->` on line 1 use one `## R<n>` section per response; `python tools/check_response_format.py --strict` checks them. Chat replies are not machine-checked, so this rule and review are the control.
- Details and template: `docs/coordination/EXTERNAL_AGENT_GUIDE.md` (Response format). Example: `docs/OPEN_ISSUES_REPORT_AND_PLAN_20261119.md`.

## Repo workflow
- Branch naming: maelstrom/<short-task-name>
- Commit style: conventional commits (feat:, fix:, refactor:, test:, docs:)
- Before finalizing: run format + lint + tests.

## Start here
- New here? Read `WORKER_MANUAL.md` first (about 3 minutes); it is enough to claim a task, stay in scope and hand it back with evidence.
- Read `docs/PROJECT_ORGANIZATION.md`, `docs/PRODUCT.md`, `docs/ROADMAP.md` and the relevant workstream in `docs/coordination/workstreams.json` only when the task touches product direction, sequencing or a workstream's status.
- `docs/INDEX.md` is the documentation map. Dated reports are historical evidence, not the current project status.
- Before edits, claim a bounded task and declare owned paths using `docs/coordination/EXTERNAL_AGENT_GUIDE.md`. All agents share this checkout. Existing modified/untracked files belong to ongoing work: never reset, clean, discard, mass-format or commit them without explicit direction.
- Keep research, implementation, offline verification, installation and live qualification separate. A successful proposal/dispatch is not proof of a game outcome.
- Do not start a second controller, capture job, model, installer or build concurrently. The coordinator owns integration and the build slot. Offline pure tests for disjoint Python tasks may run in parallel.
- Report the workstream status and evidence when completing a task. The coordinator updates the central board and `docs/coordination/claims.json` unless that file is explicitly assigned to another agent. Do not mark a whole game complete because one component passes.

## How to run the project
Run from this repository root in PowerShell. No new production dependencies without approval.

- Native build: `$env:DOTNET_PROCESSOR_COUNT='2'; dotnet build ProjectMaelstrom/ProjectMaelstrom/ProjectMaelstrom.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -warnaserror`
- Native tests: `$env:DOTNET_PROCESSOR_COUNT='2'; dotnet test ProjectMaelstrom/ProjectMaelstrom.Tests/ProjectMaelstrom.Tests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -warnaserror` (use a relevant `--filter` during implementation; the coordinator runs the complete suite).
- Native formatting: `dotnet format whitespace ProjectMaelstrom/ProjectMaelstrom.Tests/ProjectMaelstrom.Tests.csproj --no-restore --include <owned changed C# paths>`; repeat with `--verify-no-changes`.
- Native lint: `dotnet format analyzers ProjectMaelstrom/ProjectMaelstrom.Tests/ProjectMaelstrom.Tests.csproj --no-restore --verify-no-changes --include <owned changed C# paths>` plus the warnings-as-errors build above.
- Python tests: `python -m unittest discover -s tools/tests -p test_<component>.py` (stdlib tests; optional vision tools use existing isolated environments).
- Python formatting: `python -m black --check <owned changed Python paths>`; syntax: `python -m compileall -q <owned changed Python paths>`. Run any component-specific lint/checker too; do not claim a linter ran if it was unavailable.
- Project-board check: `python tools/check_project_board.py`.
- Response-format check: `python tools/check_response_format.py --strict`.
- Offline routine replay: native executable `--replay-routine <routine.json> <session.json> <new-report.json>`; no desktop input is available in this mode. See `docs/ROUTINE_GRAPHS.md`.
- Packaging/installation: follow `PACKAGING.md` only when assigned. An offline task does not authorize live gameplay or installation.

## Runtime boundaries
- Keep resource use bounded, default to local rules and offline replay, and preserve one shared input owner, cancellation and user takeover.
- Default game interaction is UI-only; the owner's 2026-11-10 W101 exception authorizes reviewed client-side reads, hooks and input dispatch under the single scheduler/input owner. Bounded, verified automation — including background visits and held-key pulses such as `W` — is permitted under an explicit task scope with target identity, countdown, cancellation, user takeover, cleanup verification and evidence. Snack feeding and client restart are permitted in an authorized live session with an exact policy and verified post-condition; they remain prohibited in unattended background work. Volume changes and Crown Shop purchases are excluded. Passive, read-only decoding of live client traffic through a reviewed, owner-scoped client-side adapter is permitted as diagnostics when it injects nothing, modifies no traffic and sends no data to any server; saved-capture decoding stays offline research. Coordinate teleporting, crafted/replayed/injected packets, traffic rewriting, auth/session manipulation and other network manipulation are excluded; installed game files remain read-only and are not modified because the launcher patches them. Do not re-ask the settled architectural approval, and do not equate authorization with a working integration or activate tools in unrelated tasks. See `docs/W101_CLIENT_SIDE_EXCEPTION_20261110.md`.
- Live tests require the coordinator's explicit scope and current user authorization. Restore/verify native Idle only within that authorized control window; never evade user takeover to perform cleanup.
- NORMAL (fluid) operation is the default mode. Loading screens, window focus loss and mouse movement pause dispatch; dispatch auto-resumes when those conditions clear (including ≥3 s of sustained quiet after mouse movement), provided (a) no explicit pause or stop latch is set, (b) a visible pre-resume countdown has completed, and (c) target identity, ownership, focus and cleanup are freshly re-verified. SAFE is an explicit per-session caution mode in which resume is never automatic. Explicit pause/stop and the hard-stop keypress are never auto-cleared and take precedence in all modes. No resume may bypass a recovery hold or verified-cleanup requirement.

## Coding standards
- Follow existing patterns in this repo (naming, folder structure, error handling).
- No new production dependencies without asking first.
- Keep public interfaces documented (README / docs / inline docstrings as appropriate).

## Definition of Done
- Tests pass locally.
- No lint/typecheck errors.
- Clear changelog/notes in PR description or CHANGELOG if you use one.
- Any new behavior has tests and minimal docs.
