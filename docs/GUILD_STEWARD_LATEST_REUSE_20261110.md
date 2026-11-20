# Refreshed DevTools contracts and Maelstrom reuse — 2026-11-10

Work item: `SCHEDULER-AUDIT-06`. This is an independent source review, followed by
review of the coordinator's scoped native implementation. It does not qualify a
live game, install an update, or add a DevTools runtime dependency.

## Exact source and what changed

The coordinator refreshed `.local/reference-repos/aaroneous-devtools`. This audit
read that local checkout and its Git history; it did not fetch, execute its
services, build Rust, use a model, or interact with the desktop.

| Reference | Commit |
| --- | --- |
| Previous DevTools review | `513a246e76aac42b9d49fd522bc14e7c67d5ffc7` |
| Current DevTools review | `7e7a3d3f45c3d24b8ff8de32d1e1af288840f3ed` |
| Reachable commits in `513a246..7e7a3d3` | 41, including merge commits |

The relevant implementation change is C187: all three Steward local Git
adapters now remove inherited `GIT_*` environment variables case-insensitively
on Windows through `git_env::scrub_inherited`. Previously a lower-case
`git_dir` could evade the prefix check. The range also adds C190 review-carry,
which reports whether a candidate's files are still the files a reviewer saw,
plus tests, governance material and dependency updates. It does **not** add a
new scheduler or revise `contracts/`, `foundation_cli/src/scheduler.rs`, or
`foundation_cli/src/local_worker.rs` in this range. The small diff in
`local_verification.rs` replaces its old environment cleanup loop; its claim and
recovery protocol predates this refresh.

Source: [Git environment boundary](../.local/reference-repos/aaroneous-devtools/foundation/crates/steward/src/git_env.rs),
[review-carry](../.local/reference-repos/aaroneous-devtools/foundation/crates/steward/src/review_carry.rs),
[local verification](../.local/reference-repos/aaroneous-devtools/foundation/crates/steward/src/local_verification.rs).
These local links resolve to the refreshed checkout; the hashes above pin the
review even if that checkout advances again.

The [earlier review](GUILD_STEWARD_SCHEDULER_REUSE_20261110.md) remains historical
evidence. Its standalone Guild checkout is not the source of current DevTools
behavior. This review does not infer current functionality from that older
Python snapshot.

## Implemented behavior versus proposals

| Source at the pinned current commit | Status and useful boundary |
| --- | --- |
| `steward/src/verification.rs`, lines 54–184 and its evidence reducer | Implemented pure request/admission/outcome contracts. Request identity binds repository, head/base, platform, suite and suite digest. Results count only for the admitted attempt and node with matching evidence. Conflicts do not become success. |
| `steward/src/local_verification.rs`, lines 272–339 | Implemented exclusive per-request claims and numbered attempts. Recovery requires the exact node and attempt, writes abandonment evidence, and does not automatically expire a running claim. |
| `steward/src/job_service.rs`, result admission near lines 394–454 | Implemented bounded receipt import with exact current-attempt checks. Transport acknowledgement and successful work remain separate. |
| `foundation_cli/src/scheduler.rs` | Implemented advisory schedule review: reports capacity, claimed/review lanes and recommended claims. It is not a game executor or automatic queue consumer. |
| `foundation_cli/src/guild.rs`, lines 214–342 and 569–570 | Implemented log/checkpoint verification and publication ordering. A mismatch holds for recovery; append and checkpoint are explicitly separate writes, and publication ordering is not claimed to provide power-loss durability. Missing checkpoints are supported for legacy logs. |
| `foundation_cli/src/local_worker.rs`, lines 247–315 | Implemented bounded pending-output policy and visible `backpressure` status. Its separate reservation-lock TTL deletion is unsuitable for desktop input ownership. |
| `contracts/guild-operation-v1.md` | Explicitly **PROPOSED, NOT IMPLEMENTED, NOT ADOPTED**. It supplies design constraints and unresolved qualification gates, not an available production operation service. |
| `contracts/maintenance-recovery-v1.md` | Recovery contract to evaluate against implementation. Its statement that rename establishes power-loss durability is explicitly rejected as a premise by the Guild operation proposal. |

Sources: [verification](../.local/reference-repos/aaroneous-devtools/foundation/crates/steward/src/verification.rs),
[job service](../.local/reference-repos/aaroneous-devtools/foundation/crates/steward/src/job_service.rs),
[advisory scheduler](../.local/reference-repos/aaroneous-devtools/foundation/crates/foundation_cli/src/scheduler.rs),
[current Guild log](../.local/reference-repos/aaroneous-devtools/foundation/crates/foundation_cli/src/guild.rs),
[local worker](../.local/reference-repos/aaroneous-devtools/foundation/crates/foundation_cli/src/local_worker.rs),
[Guild operation proposal](../.local/reference-repos/aaroneous-devtools/contracts/guild-operation-v1.md),
[maintenance recovery](../.local/reference-repos/aaroneous-devtools/contracts/maintenance-recovery-v1.md).

DevTools' [foundation boundary](../.local/reference-repos/aaroneous-devtools/FOUNDATION_BOUNDARY.md)
calls for narrow, independently verified graduation of useful capabilities. It
does not establish a dependency from Maelstrom to its Rust services or authorize
another controller.

## Concrete Maelstrom findings at review start

These are inspected source paths, not failures observed in a game. The
coordinator assigned separate fixes and tests; implementation status belongs in
the integration checkpoint rather than being inferred from this findings list.

| Finding | Trigger and implication | Small repair and regression |
| --- | --- | --- |
| Admitted pins remain mutable | `Submit` copies pins into a `Dictionary`, then exposes that same object through `OperationTicket.Request` and the executor context as `IReadOnlyDictionary`. A caller can cast it back and change a queued digest. | Freeze and bound the admitted request once. Queue behind a blocked first job; attempt mutation through both the original request and returned ticket; changing the pinned file must still prevent execution. |
| Invalid terminal data can poison restart | The worker rejects only Queued/Running outcomes. An undefined enum value can be written and then rejected by restart's stricter reader. Message/artifact/request string sizes are not consistently bounded at the write boundary. | Validate and snapshot terminal metadata before journal publication; normalize an invalid executor result to a bounded failure. Test undefined state, null/oversized fields, and producer mutation of retained artifact arrays. |
| Current status overwrites admission and stop history | A single `receipt.json` is updated Queued → Running → terminal, and restart replaces an unfinished status with Interrupted. It does not retain why that run was originally admitted or each state transition. | Add a small immutable admission/lifecycle journal. Keep `receipt.json` as a compatibility view, with no automatic retry or new executor. |
| Recovery can mutate before discovering later corruption | The old constructor validates and rewrites one folder at a time. A later malformed job can stop startup after an earlier job was already changed. | Validate all relevant job snapshots first, then apply recovery projections. Test a valid unfinished job followed by a corrupt one and preserve both original byte sequences on rejection. |
| Cancellation callbacks can throw | Active `CancellationTokenSource.Cancel()` catches only `ObjectDisposedException`. A throwing registered callback can escape `Cancel` and interrupt disposal before the scheduler lease is released. | Isolate callback exceptions without releasing input early or interrupting cancellation of other jobs. Test a throwing callback together with an asynchronously completing cleanup. |
| Queue visibility is only a count | Capacity 32 is enforced, but consumers cannot inspect one immutable snapshot of active/queued/cancelling work and pressure. | Expose bounded descriptive snapshots from the existing queue. Do not create a polling controller or a second consumer. |

Inspected native boundaries:
[scheduler](../ProjectMaelstrom/ProjectMaelstrom/Utilities/OperationScheduler.cs),
[input lease](../ProjectMaelstrom/ProjectMaelstrom/Utilities/GameInputLease.cs),
[module operation](../ProjectMaelstrom/ProjectMaelstrom/Utilities/Modules/ModuleOperation.cs),
[scheduler tests](../ProjectMaelstrom/ProjectMaelstrom.Tests/OperationSchedulerTests.cs),
[recovery tests](../ProjectMaelstrom/ProjectMaelstrom.Tests/OperationSchedulerRecoveryTests.cs),
[module operation tests](../ProjectMaelstrom/ProjectMaelstrom.Tests/ModuleOperationTests.cs).

`ModuleOperation.SubmitOwned` already freezes a bundle for its admitted artifact.
The trusted executor callback remains responsible for using the corresponding
plan and target. The inspected call sites are tests; this audit does not claim a
live adapter executes unpinned plans. A future production adapter should receive
the admitted plan directly instead of recapturing mutable caller state.

## Small journal acceptance constraints

1. Validate and freeze admitted semantics before publication. Commit admission
   before exposing the ticket or making the worker runnable. An existing record
   with different bytes is a conflict, not permission to overwrite it.
2. Bound JSON bytes, collection counts and string lengths on both writes and
   reads. Validate known schemas, states, request identity, sequence and
   predecessor hashes. These checks detect inconsistency; hashes do not grant
   authority or prove external effects occurred.
3. Append each authoritative state record before updating `receipt.json`.
   A crash after a terminal record but before projection replacement must
   reconstruct that terminal view without running the callback again.
4. Admission-only or Queued/Running journal state becomes Interrupted after
   restart, preserving the prior evidence. Do not synthesize dispatch, gameplay
   completion, cancellation acknowledgement or successful cleanup.
5. Legacy receipt-only folders remain readable and retain their existing
   recovery behavior. Do not invent missing historical lifecycle entries or
   silently reinterpret a partially present new journal as legacy data.
6. Reject malformed, conflicting, linked or oversized authoritative records
   without modifying their evidence. Validate a startup batch before recovery
   writes. A missing compatibility view can be reconstructed; conflicting
   immutable records cannot be repaired by guessing.
7. Keep one scheduler writer and the existing `GameInputLease`. Cancellation
   requests do not prove execution stopped. Retain the cleanup/recovery latch;
   never inherit advisory TTL takeover or automatic game-input retries.
8. Do not describe atomic staging/rename or hash chaining as qualified
   power-loss durability. Document the exact process-crash cases tested and
   leave filesystem/hardware durability outside that claim.

## Reuse now and defer

The useful near-term transfer is native C# behavior: immutable bounded admission,
exact evidence association, explicit recovery, visible queue pressure and
request-scoped process configuration. Keep the current FIFO capacity and one
input owner. A completed scheduler job is evidence that its worker completed;
only separately observed game outcomes qualify gameplay.

Defer distributed claims, network job transport, signatures, arbitrary command
recipes, multi-worker resource lanes, dependency scheduling and recursive
operations until a concrete product workflow needs them. Do not add a Rust
service, model host or standalone Guild database merely to reuse these rules.
Repeated games remain explicit new runs; identical pack contents do not make
distinct intended episodes duplicates.

## Verification status

This worker performed read-only source/history inspection, wrote this document,
and subsequently added three assigned offline cancellation regressions. No
build, test execution, game interaction, installation or external request was
performed by this worker. Coordinator-owned test execution establishes the
implementation checkpoint separately.

The first independent implementation review found four boundary issues in the
new journal: wall-clock rollback was incorrectly treated as sequence corruption;
Queued could transition directly to Completed; a regular file at the journal
directory path could fall back to legacy handling; and a near-limit receipt
needed room for its enclosing journal record. The implementation owner repaired
those source paths, using sequence/hash ordering, stricter state transitions,
an explicit directory check, bounded serialization and envelope headroom.
Those are source-review observations, with regression execution pending the
coordinator's serialized checks. The separately reviewed request snapshot uses
one bounded enumeration and a private read-only dictionary; no additional
concrete defect was found in that helper.

The scheduler integration review also identified request-size headroom needed
for later failure/recovery messages, and a cancellation reentrancy case: a
callback can synchronously resume the awaiting worker on the cancelling thread.
A reentrant monitor alone cannot prove that all other callbacks finished before
input release. The revised source reserves 512 KiB for request metadata and
awaits a separate asynchronous cancellation-completion barrier before releasing
input. Both repairs were re-read independently. The added
[cancellation tests](../ProjectMaelstrom/ProjectMaelstrom.Tests/OperationSchedulerCancellationTests.cs)
cover an explicit callback exception with retained cleanup ownership and journal
reopening, a deadline callback exception, and inline worker resumption before a
later cleanup callback. They use temporary lock files, bounded waits and no
desktop transport. This document does not predeclare those tests passed.

One preexisting storage-failure limitation remains explicitly outside this
increment: if writing the input-recovery marker itself fails after a worker
reports CleanupFailed, retaining that terminal disposition does not ensure a
subsequent input owner is blocked. Complete fail-closed containment of that
filesystem failure requires a separately tested ownership/recovery change.
Neither ordinary recovery tests nor successful journal publication prove this
failure case safe.
