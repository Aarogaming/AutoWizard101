# Scheduler reuse integration — 2026-11-10

Status: source implementation and offline verification complete. This checkpoint
records the requested Devtools refresh and selective reuse; it does not qualify
new gameplay, install an update or replace Maelstrom's scheduler. Three agents
reviewed upstream contracts, implemented disjoint components/tests and performed
independent review; the coordinator serialized formatting, build and validation.

## Reference update and reuse boundary

The existing clean `aaroneous-devtools` reference checkout was fast-forwarded
from `513a246e76aac42b9d49fd522bc14e7c67d5ffc7` to
`7e7a3d3f45c3d24b8ff8de32d1e1af288840f3ed`: 41 new commits. The new HEAD is dated
2026-11-09 00:16:21 America/New_York. Source identity, retrieval method and reviewed
file hashes are recorded in `.local/scheduler-reuse-20261110/reference.json`.
The reference checkout remains source material; its programs were not executed.

[Current Guild/Steward review](GUILD_STEWARD_LATEST_REUSE_20261110.md) separates
implemented upstream behavior from proposed contracts and identifies exact source
locations. The earlier [cached-source review](GUILD_STEWARD_SCHEDULER_REUSE_20261110.md)
is retained as historical evidence. The relevant refresh changes Git environment
handling; it did not newly introduce Steward's durable request/attempt contracts.

## Selected native changes

- Freeze the complete admitted request's input map behind a read-only private
  copy. A caller must not be able to change queued input expectations through
  the ticket or worker context. Validate the actual copy being admitted.
- Retain bounded immutable admission and lifecycle evidence while preserving
  `receipt.json` as the existing application's latest-status view. Completed
  evidence must survive an interruption between journal publication and view
  replacement. Recovery never resumes game actions.
- Reject malformed worker outcomes before saving them. An undefined state must
  not produce a receipt that prevents the next application startup.
- Contain exceptions from explicit-stop and deadline callbacks. An asynchronous
  completion barrier keeps input ownership until callbacks and worker cleanup
  finish, including when cancellation resumes the worker inline.

These are C# adaptations using existing runtime libraries. There is no imported
Rust service, model, background polling loop, second input owner or new production
dependency. One GUID remains one explicitly submitted run. Logical multi-attempt
jobs, dependency scheduling and signed distributed authority remain separate work.

The existing scheduler still serializes at most 32 admitted jobs, preserves
cancellation and input-cleanup ownership, verifies queued file hashes before
execution and requires fresh explicit admission for another run. A completed
offline review means the review finished; game success still needs observed
outcome evidence from the game-specific adapter.

## Verification and remaining work

All checks ran offline with `DOTNET_PROCESSOR_COUNT=2`, no package restore and no
shared compiler. No game, local model, installer or desktop controller was started.

| Check | Observed result |
| --- | --- |
| Full native suite, Release, warnings as errors | **1,379 passed, 0 failed, 0 skipped**, 11-second test duration. |
| New regression cases | **63**: 16 request-snapshot, 25 journal, 19 scheduler integration, 3 cancellation cases. |
| Scoped formatting and analyzers | Whitespace applied to seven changed C# files; verify-no-changes and analyzers both exit 0. |
| Coordination checks | Project board valid: 30 workstreams; all 22 board tests passed. |
| Compiled app saved-screen workflow | Exit 0 in **1.097 seconds**; 2 completed modules, 4 proposals, 4 supplied rehearsal receipts, 11 image observations, zero desktop inputs and no input lease. |
| Generated journal | Admission plus Queued → Running → Completed, **9,464 bytes** total; every predecessor hash and admitted request matched; latest snapshot equals the compatibility receipt. |

The command used the freshly built framework-dependent TargetPath rather than
the stale runtime-specific sibling. It reran the existing real PNG/local OCR
fixture with synthetic timing and supplied input receipts. This validates the
application integration, not a live visit, new recording or performance benchmark.

Evidence is under `.local/scheduler-reuse-20261110/`: `reference.json`,
`scheduler-reuse-full.trx`, `cli-validation.json` and `validation.json`. The actual
run is `scheduled-review-160445/operations/cebd8a1df9d04276a4c1d2ec92b66429/`.
Its application SHA-256 is
`29302fd79e6ee1f5b5e668d1481a69bd69542ca90003fd7fc65c9adb70c5b376`.
These local artifacts are supporting evidence, not assumed present in a clean clone.

The new journal caps admitted request JSON at 512 KiB, reserves terminal/recovery
headroom, permits at most four 1-MiB lifecycle records, and bounds diagnostics,
artifact references and directory entries. Original artifact order is preserved.
Unknown schema/state, mismatched identity/hash, sequence gaps and contradictory
terminal entries are rejected. Whole-spool validation precedes recovery writes.
Incomplete staging is not committed evidence; legacy receipt-only jobs keep
their prior behavior. Snapshot sequence orders events even if the wall clock
moves backwards. Filesystem/hardware power-loss durability and hostile rewriting
of the whole store are not established by these tests.

Remaining product gates are unchanged: navigation/sigil recognition evidence,
workspace module controls, current-client runtime qualification and packaging of
an approved checkpoint. The project board remains the coordination record rather
than a runtime grant or an automatic distributed scheduler.

One pre-existing failure case remains explicitly open: if writing the shared
input-recovery marker itself fails, the existing lease layer lacks an independent
in-memory live-admission hold. This change preserves the CleanupFailed result;
it does not claim that storage failure proves safe cleanup or blocks all future
controllers. Address that shared-ownership failure path before broader live
qualification. Queue snapshots, dependency scheduling and logical multi-attempt
jobs are also deferred; the refreshed upstream proposals do not supply those
features automatically.
