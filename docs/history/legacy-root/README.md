# Legacy root files (moved 2026-11-19)

These files sat at the repository root from the earlier Aaroneous Automation Suite and "Wizard101 Smart Trainer" eras
(content from late December 2025). They describe a different product and process, so they were moved here with
`git mv` (history preserved) to keep the root and the agent entry path small.

**They are historical, not current policy.** The current rules are in `AGENTS.md` and `WORKER_MANUAL.md`. Known
conflicts: `README_PROJECT.md` says not to read game memory, which the owner's later W101 client-side exception
supersedes (`docs/W101_CLIENT_SIDE_EXCEPTION_20261110.md`); `STEWARDSHIP.md`, `GOLD_FREEZE.md` and
`RELEASE_READINESS.md` describe the old gold-build process, not the current claims and evidence gates.

Moved: AUDIT_TRAIL, CHECKSUMS, CONTRIBUTING, COOP_WORKFLOW, FEEDBACK_LOG, GOLD_FREEZE, MAINTENANCE, MODULARITY_PLAN,
POLICY_BOUNDARY, README_PROJECT, RELEASE_READINESS, RESPONSE_TEMPLATES, STEWARDSHIP, SUBMISSION, TODO, UI_POLISH_TODO and
VERIFY (all `.md`). References between these files are bare filenames, not links: resolve them in this folder, or at the
root for `SECURITY.md`, `SECURITY_NOTES.md` and `PACKAGING.md`, which stayed there. Older documents in `docs/history/`
that mention root filenames are left as written.
