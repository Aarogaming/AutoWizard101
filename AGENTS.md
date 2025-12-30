# Project Maelstrom — Codex Working Agreement (AGENTS.md)

## Mission
Project Maelstrom builds: <one sentence: what it is, who it serves, success criteria>.

## Golden rules
- Make the smallest safe change that solves the task.
- Prefer adding/adjusting tests over “just changing code”.
- Never commit secrets. Don’t print secrets in logs.
- If a requirement is ambiguous, propose 2-3 options and ask the user (me) which to choose.

## Repo workflow
- Branch naming: maelstrom/<short-task-name>
- Commit style: conventional commits (feat:, fix:, refactor:, test:, docs:)
- Before finalizing: run format + lint + tests.

## How to run the project (fill these in)
- Install: <e.g., pnpm i / npm ci / poetry install>
- Lint: <command>
- Format: <command>
- Unit tests: <command>
- Integration/e2e: <command>
- Typecheck/build: <command>

## Coding standards
- Follow existing patterns in this repo (naming, folder structure, error handling).
- No new production dependencies without asking first.
- Keep public interfaces documented (README / docs / inline docstrings as appropriate).

## Definition of Done
- Tests pass locally.
- No lint/typecheck errors.
- Clear changelog/notes in PR description or CHANGELOG if you use one.
- Any new behavior has tests and minimal docs.
