# FOREGROUND-DOCS-SYNC-01 — operator-doc reconciliation

Status: **docs only — no source, build, live or policy change.**
Date: 2026-11-17 · owner: root · workstream: `CORE-DELIVERY`

## Audit result

A repo-wide scan of foreground language found the existing docs are mostly correct: foreground requirements in `HEADLESS_CLI.md` (idle-cycle line 47, navigation-visit line 203, recovery line 323) apply to **input-dispatching actions**, not observation. `PET_DANCE_CONTINUOUS_INTEGRATION_NOTES_20261109.md:52` already states passive observation needs no foreground.

Two places were stale/imprecise and are corrected here:
- `CLI_AGENT_OPERATING_PROCEDURES.md` — replaced the vague "W101 commands commonly require foreground" with the verified distinction: read-only observation verifies identity only (12-tree pass, zero W101-foreground samples, receipt `d8c040db…`), while input actions still need foreground and immediate takeover.
- `GAME_WORKSPACES.md` — recorded the reviewed W101 observation adapter as background-capable for reads, explicitly not for input dispatch; other adapters still need separate checks.

## Evidence

`docs/coordination/results/W101-BG-QUAL-LIVE-01.md`; source `GameWindows.cs:45` (`IsCurrent` = `SameIdentity`, not focus). No new live work was performed for this doc sync.
