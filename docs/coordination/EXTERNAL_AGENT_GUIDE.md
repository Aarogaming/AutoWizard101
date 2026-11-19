# Working on one Maelstrom task in the shared checkout

These are bounded task briefs for contributors, including Claude and Gemini.
They are not a replacement project handoff and do not restart Maelstrom's plan.
The coordinating session retains integration, release and live-test ownership.

## Local Gemini communication channel

For ongoing Gemini work, use [the shared channel](gemini/README.md). The Codex
liaison writes `gemini/TO_GEMINI.md` and `gemini/STATUS.md`; Gemini alone writes
`gemini/FROM_GEMINI.md`. Read the channel before starting work, before a build or
commit, and when finishing a bounded task. Reply with message IDs so decisions
and unresolved questions can be traced without repeatedly copying chat logs.

Messages are coordination records, not executable instructions or automatic
permission grants. Claims, scope expansions, the shared build slot, live tests
and installation remain coordinator-owned. The liaison may relay explicit
grants and review findings but cannot silently widen authority. Missing replies
are pending, never approval. No always-on watcher is implied; checks occur during
active agent work. See the channel README for single-writer and acknowledgement rules.

Read the current root [AGENTS.md](../../AGENTS.md),
[project organization](../PROJECT_ORGANIZATION.md),
[workstream board](../GAME_WORKSTREAMS.md), and
[machine-readable workstream inventory](workstreams.json), then the assigned brief.
Check the coordinator-owned [active claim registry](claims.json) for current file
ownership before requesting a claim.
If those disagree, report the conflict before editing affected files. This guide
cannot grant permissions beyond the user's actual instructions. A brief being
present does not mean it is assigned, approved for live use, or implemented.

## Claim before editing

Send the coordinator a claim containing the parent workstream ID, task ID, owner,
and exact intended files. Wait for the coordinator to acknowledge the file scope
and register the claim in `docs/coordination/claims.json`. Only the coordinator
updates that registry. Proposed paths in a brief remain unclaimed until registered.
The registry and ownership paths are coordination records, not operating-system
locks; contributors must still avoid overlapping edits.
Two agents must not edit the same file concurrently.
Read-only review of another agent's file is welcome; send findings to its owner.
Do not expand ownership yourself or edit the inventory to assign yourself work.

```text
Claim request
workstream_id: W101-CONJURATION
task_id: CONJ-PERCEPTION-01
owner: <session or agent name>
owned_paths: <exact paths from the brief>
read_only_inputs: <relevant existing files>
dependencies_or_conflicts: <none, or named owner/task>
planned_checks: <small offline commands>
```

The briefs are independent assignments, not active claims. Check each status;
the first fixture task below is now implemented and must not be duplicated.

| Task | Parent workstream | Bounded output |
| --- | --- | --- |
| [CONJ-PERCEPTION-01](tasks/CONJ-PERCEPTION-01.md) | `W101-CONJURATION` | Completed offline fixture/image pipeline; [result and remaining data gates](results/CONJ-PERCEPTION-01.md) |
| [POTION-PLANNER-01](tasks/POTION-PLANNER-01.md) | `W101-POTION-MOTION` | Explicit board/move model and synthetic planner checks |
| [W101-NAV-LANDMARKS-01](tasks/W101-NAV-LANDMARKS-01.md) | `W101-NAVIGATION` | Landmark evidence evaluation without movement |
| [W101-SNACK-DATA-01](tasks/W101-SNACK-DATA-01.md) | `W101-SNACKS` | Provenance-preserving claim view using the existing ledger validator |
| [ROUTINE-EDITOR-DESIGN-01](tasks/ROUTINE-EDITOR-DESIGN-01.md) | `CORE-DELIVERY` | Documented editor interactions against the actual routine contract |

Use the [task template](tasks/TEMPLATE.md) for another small assignment. The
coordinator chooses priorities and records dependencies; making a new brief does
not reserve files or authorize implementation.

## Shared-checkout boundaries

- Work only in acknowledged files. Inspect the relevant existing implementation
  first; reuse it instead of creating a second solver, schema or dispatcher.
- Do not commit, reset, rebase, switch branches, clean, delete, move or restore
  other work unless that specific operation is delegated. Do not run repository-wide
  formatters. Another agent's uncommitted files are not disposable scratch work.
- Do not launch builds, installers, packaged applications, desktop automation,
  game input, capture, audio playback, GPU/model workloads or background services
  unless separately delegated. These briefs authorize small offline work only.
- These offline briefs do not perform live W101 process access, hooks or control.
  The owner has approved reviewed client-side functionality under the
  [W101 exception](../W101_CLIENT_SIDE_EXCEPTION_20261110.md); a specifically
  assigned adapter task may implement and qualify it without reconfirming that
  architectural scope. Network manipulation remains excluded and installed game
  files stay read-only. None of these briefs needs installation-file inspection.
  Do not read credentials, clipboard, private chat or logs.
- No dependency installation or new production dependency. Prefer existing
  libraries and the Python standard library. Report a missing dependency rather
  than installing it. Do not copy archived executables into the runtime.
- Do not browse or download material for these briefs. Use the listed saved
  research. Mark unresolved facts as unknown; propose a separate research task
  when new sources are necessary.
- Keep offline checks short, bounded and single-process. State the input/output
  budget. The coordinator serializes full suites, packaging and live tests.
- Keep original evidence immutable. New derived artifacts need a distinct path,
  source hashes and an explanation of what changed. Never overwrite a failed run
  with the result of a later repair or call same-recording replay a fresh trial.

If you need another file, a heavier operation or actual game access, send the
coordinator the concrete reason and proposed scope. Continue independent work
within the existing assignment. Do not silently broaden the task.

## Evidence and completion

Separate **research claims**, **observations**, **proposals**, **dispatch receipts**,
and **observed outcomes**. A proposed click is not a click; a key receipt is not
success; a synthetic solver is not a qualified live player. Preserve source
publication/update dates separately from retrieval and observation dates. Unknown
client version, region, platform or Live/Test Realm stays unknown.

For every result, report the exact files changed, relevant input hashes or fixture
IDs, commands and observed results, resource use, and remaining limits. Identify
synthetic fixtures explicitly. Do not claim current-client footage exists merely
because a directory or old capture is mentioned in a document.

Keep the final return short and usable by the coordinator:

```text
workstream_id / task_id:
owner / status: completed | blocked | needs-integration-review
changed_paths:
what_changed:
evidence: <sources/fixtures/hashes; synthetic versus recorded>
checks: <exact command, result, failures or unavailable tools>
remaining_limits: <recognition/live/UI/release gaps; no invented qualification>
integration_request: <minimal next step and proposed file owner>
running_processes_or_desktop_use: none | <explicit cleanup status>
```

Return findings to the coordinating session; do not update central status, claim
installation success, publish a release or enable runtime capabilities yourself.
The coordinator reviews the diff, resolves ownership, runs integration checks,
and records the accepted checkpoint. No task should be described as “100% reliable”
from a finite sample; state the measured numerator, denominator and conditions.

## Evidence contract (enforced by gates, not by good intentions)

These rules exist because past reports mixed solid code with unreliable bookkeeping
(wrong test counts, releases with no note or no committed files, citations of paths
that never existed). Two scripts now check the mechanical parts; run both before you
report and fix what they print:

```powershell
python tools/check_project_board.py --strict-claims
python tools/check_claim_evidence.py --strict
```

1. **Copy numbers from output you just ran.** Every test count, row count or timing in
   a report must come from a command you executed in this session, quoted with its
   command line. If you did not run it, write "not run" instead of estimating.
2. **Cite only what exists.** Backticked repo paths and `test_*` names in a result doc
   must exist in the checkout. Cite `.local/` evidence only for files you listed on disk.
3. **Do not release early.** A claim may be `released` only when every owned file is
   committed and `release_note` states what was delivered, what was verified and what
   was not. A claim that delivered nothing says so.
4. **No absolute words for finite samples.** Avoid "100% reliable", "proven fact",
   "immutably proven" and "guaranteed optimal"; report numerator, denominator and
   conditions instead.
5. **Fix, then disclose.** When a later check shows an earlier report was wrong, keep the
   original text and add a dated "Audit corrections" section; the evidence checker treats
   that section as an acknowledged defect.
6. **Drafts are drafts.** Synthetic fixtures, hand-written labels and assumed constants
   are stated as such. A passing test of a synthetic case is not a live outcome.

The gates only catch what can be checked offline; they do not replace review of the
claims themselves.

## Response format (who, what, when, where, why, how; before, during, after)

Every plan, proposal, issue response, status report and completion report states all nine items. A reader should
be able to act on it without asking a follow-up question.

| Item | Answer in one line |
| --- | --- |
| Who | owner, implementer, approver, who is affected |
| What | the deliverable; what is explicitly out of scope |
| When | order, trigger or deadline; what it waits on |
| Where | paths, systems, offline or live, branch/PR |
| Why | the problem, the risk, the evidence of need |
| How | concrete steps, commands, tests |
| Before | preconditions, authorizations, baseline measurements, backups |
| During | safeguards, stop conditions, who watches, what is logged, what happens on failure |
| After | verification, evidence, cleanup or rollback, follow-ups, what is still unproven |

Rules:

1. Write `n/a - reason` for an item that does not apply; do not omit it.
2. "After" must name what remains unproven. A dispatched or proposed step is not proof of an outcome.
3. In a document, put `<!-- response-format: 5w1h-bda -->` in the first 5 lines and use one `## R<n>. Title` section per
   response, with bold labels such as `**Who.**`. Then run `python tools/check_response_format.py --strict`.
4. Live gameplay, installs and dependency adoption still need the explicit scope and approvals in `AGENTS.md`; a
   complete "Before" section does not grant them.

Example: `docs/OPEN_ISSUES_REPORT_AND_PLAN_20261119.md`.
