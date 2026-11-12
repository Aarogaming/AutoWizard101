# Guild and Steward scheduler reuse — 2026-11-10

Work item: `SCHEDULER-REUSE-05`. Maelstrom should reuse Guild/Steward's job and
evidence contracts while retaining its existing C# scheduler and single desktop
input owner. A Rust service, second queue consumer or automatic retry of game
actions is unnecessary for this increment.

This review read local source only. No fetch, network request, reference-repo
execution, build, game capture or desktop input occurred. The supplied reference
copies are historical snapshots, not evidence of the user's latest work in
another checkout or on GitHub.

## Source provenance

`git rev-parse HEAD`, `git status --short`, and the last commit date were read in
each checkout. All three reported a clean working tree at review time.

| Local checkout under `.local/reference-repos/` | Exact reviewed HEAD | Last commit time |
| --- | --- | --- |
| `aaroneous-devtools` | `513a246e76aac42b9d49fd522bc14e7c67d5ffc7` | 2026-10-30 13:19:21 −04:00 |
| `Guild` | `b3749df871403488532a950c51476805dabd77ab` | 2026-04-09 15:35:28 −04:00 |
| `aaroneous` | `a3383f2feeeee32a47bcd11e34698cfc0d138131` | 2026-10-29 16:25:02 −04:00 |

The standalone Guild snapshot is substantially older than the devtools snapshot.
The presence of both does not establish that they implement the same current
architecture. Exact paths below are relative to these pinned checkouts.

## Keep the layers distinct

| Layer | Source inspected | Useful contract | Maelstrom boundary |
| --- | --- | --- | --- |
| Advisory work allocation | Devtools `foundation/crates/foundation_cli/src/scheduler.rs`; `foundation/crates/steward/src/cards.rs` | Count claimed/review lanes, choose eligible work within capacity, preserve a holder, return standby when nothing is available. | Repository workstreams and worker allocation; not game-input ownership. |
| Durable verification jobs | Devtools `foundation/crates/steward/src/local_verification.rs`, `verification.rs`, `job_service.rs` | Immutable request identity, exclusive claim, numbered attempts, exact result matching, explicit recovery, bounded artifact import. | Inspiration for offline experiment/verification jobs and result acceptance. |
| Legacy task orchestration | Guild `core/task_manager.py`; `core/guild/task_manager.py` | Priority/dependency filtering, task claims, completion and event reporting. The `core/guild` path is a re-export of the larger task manager. | Adapt small contracts; do not import its database, model, peer-review and IPC orchestration stack. |
| Legacy IPC queue | Guild `core/ipc_command_queue.py` | Bounded queue, queue age, processing metrics, explicit retry counter and expiry. | Useful queue visibility ideas. Its multiple-worker/automatic-retry policy is unsuitable for desktop actions. |
| Product priority queue | Aaroneous `crates/orchestrator/src/priority_scheduler.rs` | Bounded priority capacity and backpressure. | Reference for later offline resource lanes, not a reason to change current live FIFO behavior. |
| Maelstrom execution | `ProjectMaelstrom/ProjectMaelstrom/Utilities/OperationScheduler.cs`, `GameInputLease.cs`, `Modules/ModuleOperation.cs` | One sequential worker, durable receipts, pinned inputs, finite run budget, exclusive input lease, cancellation waits for cleanup, no restart replay. | Remains the sole execution backbone. Modules provide decisions; the scheduler admits trusted workers. |

The devtools [foundation boundary](../.local/reference-repos/aaroneous-devtools/FOUNDATION_BOUNDARY.md)
explicitly keeps Guild/Steward policy in devtools while it is stabilized and
permits narrow, independently verified graduation. Its source does not prescribe
adding devtools as another product's runtime dependency.

## Contracts worth adopting

**Request identity and attempt identity are separate.** Steward's
`verification.rs::Request::id` hashes the repository, exact candidate/base,
platform, suite and suite digest. `local_verification.rs::Store::claim` creates an
exclusive claim and a monotonically numbered attempt record. The receiving
service checks the admitted request, worker and exact latest attempt before
accepting a result. A byte-identical completed result is idempotent; a different
replacement is refused. Maelstrom already gives every explicit run a distinct
ID and every action a proposal ID. Preserve that behavior: two intended games
with the same pack are separate episodes, not duplicate requests to suppress.
Future comparison jobs can add stable study/request identity plus distinct run
attempts without changing the input loop. Sources:
[verification](../.local/reference-repos/aaroneous-devtools/foundation/crates/steward/src/verification.rs),
[local store](../.local/reference-repos/aaroneous-devtools/foundation/crates/steward/src/local_verification.rs),
[service result admission](../.local/reference-repos/aaroneous-devtools/foundation/crates/steward/src/job_service.rs).

**Expiry is not proof that an old worker stopped.** Steward's advisory cards use
tick-based allocation leases, but its execution store explicitly does not
auto-expire a running claim. Recovery names the exact node/attempt and records
abandonment; timeouts retain the execution claim when descendants may remain.
The pure verification module also states that routing is not execution fencing.
Maelstrom's existing cancellation and input-recovery latch follow the appropriate
execution rule: do not give input to a successor until cleanup is confirmed.
Never transfer advisory card expiry directly into `GameInputLease`.

**A successful transport is separate from verified results.** Steward validates
the exact suite, logs, node and attempt, and treats contradictory verdicts as
Conflict rather than Pass. Maelstrom's equivalent distinctions are command
proposal, accepted/released input receipt, observed game outcome, and qualified
experiment result. An accepted click, a `Completed` scheduler receipt or a
synthetic replay is not by itself evidence of live gameplay success.

**Dependencies need evidence, not only a task label.** Guild's ordinary
next-task selection filters completed prerequisites and checks verification
failure events in its database path. Its explicit-task claim branch does not
perform that same dependency loop, and completion orchestration mixes status
updates with starting verification. These are inspected source differences, not
test results. Reuse the dependency concept with a single eligibility predicate
for all admission paths; do not copy the entire implementation. Across Maelstrom
jobs, prerequisites should name completed receipts and pinned artifacts. Within
a game session, the existing module/routine graph already expresses conditional
steps and should remain responsible for them. Source:
[Guild task manager](../.local/reference-repos/Guild/core/task_manager.py).

**Backpressure should stay small and visible.** Steward bounds the service spool
and artifact body/file counts and refuses more work when archival is required.
Guild exposes queue size, oldest age, processing time and retries. Maelstrom
already caps active-plus-queued jobs at 32 and runs one worker. Keep that default;
use explicit queue-state reporting before adding throughput. If independent
offline lanes are added later, each needs a separate small resource budget and
must never increase the number of live input owners. Sources:
[Steward service](../.local/reference-repos/aaroneous-devtools/foundation/crates/steward/src/job_service.rs),
[Guild IPC queue](../.local/reference-repos/Guild/core/ipc_command_queue.py).

## Concrete adoption order

| Priority | Change | Reuse method and acceptance |
| --- | --- | --- |
| Now | Constrain scheduler recovery to its own job receipts. | Implement the narrow spool boundary in C#; nested report files must stay untouched, and receipt identity must match its job folder. See the repair below. |
| Next small increment | Preserve immutable admission/terminal evidence alongside the current status view. | Adapt Steward's immutable-card/receipt distinction. Keep each transition or terminal record attributable to one run; a restart must not erase why a previous run stopped. No automatic rerun. |
| Next user-workflow increment | Show queued/running/blocked work, queue age and why a prerequisite is unmet. | Adapt Guild's task visibility. A dependency completes only from verified matching evidence, not elapsed time or an imported status string. |
| Later, when multiple offline workers are needed | Add explicit study/request → attempt → artifact lineage and bounded CPU/GPU lanes. | Adapt Steward attempt matching and result conflict handling. Preserve a separate unique episode ID for intended repeat games. |
| Later, only for cross-machine workers | Transport/versioned receipts, worker identity and result signatures. | Steward's service protocol is relevant then; network service, signing infrastructure and Rust process remain unnecessary for local gameplay. |
| Do not adopt into the live path | TTL takeover, automatic input retries, arbitrary command manifests, multiple desktop consumers. | Preserve the existing one-owner lock and human takeover behavior. Recovery never silently resumes an interrupted game operation. |

## Small repair implemented in this work item

The inspected scheduler constructor searched recursively for every
`operations/**/receipt.json`. A nested report with that name could be mistaken
for an owning job, block startup, reserve an unrelated ID, or be rewritten as
Interrupted. Its History view already selected immediate GUID directories, so
startup and history disagreed about the storage boundary.

The minimal repair shares one nonrecursive enumerator between startup and
History. It considers only an immediate GUID-named job directory's own
`receipt.json`, rejects links at that boundary, validates schema/request/state,
and requires the receipt's request ID to match the folder before recovery. GUID
comparison is case-insensitive to match Windows path identity. History retains
its latest-100 limit. Valid terminal receipts remain unchanged; valid unfinished
jobs still become Interrupted without executing or resuming anything.

Changed native source:
[`OperationScheduler.cs`](../ProjectMaelstrom/ProjectMaelstrom/Utilities/OperationScheduler.cs).
New regression cases:
[`OperationSchedulerRecoveryTests.cs`](../ProjectMaelstrom/ProjectMaelstrom.Tests/OperationSchedulerRecoveryTests.cs).
The 18 cases cover nested malformed/valid artifacts, folder mismatch, invalid
saved contracts, queued/running recovery, preservation of all terminal states,
case-only duplicate IDs and the History limit. The coordinator owns serialized
native builds/tests; this worker did not execute them. A later integration report
must supply the actual result before treating the repair as validated.

No queue service, new dependency, retry mechanism, external worker, input owner
or game behavior was added. This keeps the useful Guild/Steward principle—small
trusted jobs with attributable evidence—inside Maelstrom's established runtime.
