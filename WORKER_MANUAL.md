# Worker manual (read this first, about 3 minutes)

You are a worker. You receive one bounded task, do it inside its scope, prove it, and hand it back. You do not choose
work, widen scope, or approve your own result. Deeper documents are linked at the bottom; read them only when needed.

## The loop

1. **Get a task.** A brief in `docs/coordination/tasks/` (template: `tasks/TEMPLATE.md`) states the intent and paths.
   A brief is not a claim and not an assignment.
2. **Claim before editing.** Send the coordinator (an agent role; the owner approves anything beyond the brief) a
   claim: workstream_id, task_id, owner, exact `owned_paths`, read-only inputs, planned checks. The coordinator
   registers it in `docs/coordination/claims.json`. Wait for that.
3. **Stay in scope.** Edit only your owned paths. Other modified or untracked files belong to someone else: never
   reset, clean, discard, mass-format or commit them.
4. **Do the smallest safe change, offline, with tests.**
5. **Prove it.** Run the gates below. Copy numbers from output you ran; cite only files and tests that exist.
6. **Hand back and stop.** Return the report block below. Do not mark your own task accepted, update central status,
   release or publish.

## Boundaries

- **Allowed:** read and edit your owned paths; run short, single-process offline tests, formatters on your owned files,
  and the gates.
- **Not allowed without a named owner grant:** live gameplay or game input, launching the game, installs, builds
  outside the shared build slot, network manipulation, new production dependencies, pushing, force-pushing, merging,
  rewriting history, editing other agents' files.

## Evidence required

```text
python tools/check_project_board.py --allow-missing-local --strict-claims
python tools/check_claim_evidence.py --strict
python tools/check_text_encoding.py --quiet
python tools/check_response_format.py --strict      # plan or report documents that carry the marker
```

Plus the tests for the code you changed (commands: `AGENTS.md`, "How to run the project").

## Report block

```text
workstream_id / task_id:
owner / status: completed | blocked | needs-integration-review
changed_paths:
what_changed:
evidence: <sources/fixtures/hashes; synthetic versus recorded>
checks: <exact command, result, failures or unavailable tools>
remaining_limits: <what is not proven; no invented qualification>
integration_request: <minimal next step and proposed file owner>
running_processes_or_desktop_use: none | <cleanup status>
```

## Hard laws

- **No unbacked claims.** A proposal is not a dispatch, a dispatch is not an outcome, a passing offline test is not a
  live result. Avoid absolute words ("100% reliable"); give numerator, denominator and conditions.
- **No scope creep.** No refactors, renames or cleanups outside your owned paths.
- **No self-approval.** You submit evidence; the coordinator, the gates and the owner decide.
- **Blocked or ambiguous?** Stop and report BLOCKED with the exact missing prerequisite.
- **Every plan or report** covers who, what, when, where, why, how, plus before, during and after.

## Read more only when needed

`AGENTS.md` (runtime boundaries, commands) · `docs/coordination/EXTERNAL_AGENT_GUIDE.md` (claims, shared checkout,
evidence contract, response format) · `docs/PROJECT_ORGANIZATION.md`, `docs/PRODUCT.md`, `docs/ROADMAP.md` (direction) ·
`docs/INDEX.md` (documentation map) · `docs/architecture/` (design notes, not required for ordinary tasks).
