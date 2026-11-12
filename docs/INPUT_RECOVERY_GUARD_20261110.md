# Input recovery guard — 2026-11-10

Status: source integration and offline verification complete. This closes the
warning-persistence gap identified in the [scheduler reuse review](SCHEDULER_REUSE_INTEGRATION_20261110.md).
The scope is shared ownership and failure handling, not game interaction or an
installer. Three agents own native ownership, Python interoperability and
independent integration regressions; the coordinator owns scheduler/module
integration, the board and the serialized validation slot.

## Contract

The explanatory `input-recovery-required.txt` file is no longer the only recovery
barrier. Updated native and Python owners share a minimal state in the existing
`game-input.lock`, without deleting, replacing or adding another input lock.
Every state read/write occurs while holding the existing byte-zero OS lock.

| Lock-file contents | Admission behavior |
| --- | --- |
| Empty legacy file | May be upgraded under the OS lock before granting input. |
| Exactly one ASCII `0` byte | Clean; must successfully write and flush `1` before granting input. |
| Exactly one ASCII `1` byte | Cleanup is unconfirmed; deny new gameplay ownership. |
| Any other contents | Unknown/corrupt state; deny ownership instead of guessing. |

Normal release writes and flushes `0` only when the owner has not requested
recovery. `RequireRecovery` first latches the lease and process-local directory
hold, then attempts the text warning. Its dirty guard remains in place if that
write fails or the warning is missing. Disposal of a poisoned owner does not
clear it. A process dying while it owns input releases the OS mutex but leaves
cleanup unconfirmed for updated runtimes. The ordinary lock release is not proof
that the input state was cleaned up.

`IsHeld` describes physical ownership. `CanDispatch` describes eligibility to
send new input; a poisoned lease is no longer exposed by the ambient `Current`
scope. Legacy cached-owner checks also reject new downs/clicks, while key-up
cleanup remains available. Offline scheduler work does not acquire this lease
and stays usable while live operations are blocked.

The scheduler retains the lease's recovery verdict even after disposal or an
exception: worker success and cancellation cannot downgrade it from CleanupFailed.
The module adapter preserves an unreleased receipt if writing the diagnostic
warning fails after the recovery latch was established.

## Boundaries

These checks rely on existing adapters reporting uncertain cleanup; they do not
independently prove that a game reacted or that an arbitrary legacy program
released every key. No new automatic retry or recovery reset is introduced.
Removing the warning alone does not establish clean state or clear the guard.
A user-facing, verified recovery procedure remains separate work.

Recovery must be reported within the ownership lifetime. The scheduler already
joins worker cleanup and cancellation callbacks before disposing its lease.
Native reports arriving during clean persistence poison first and cause a
dirty-state restoration before release. Python serializes reporting and context
close. Completely late reports after ownership has ended are rejected; native
also attempts a diagnostic warning. Neither runtime takes over a later owner's
lock or claims to retroactively revoke control already granted to another process.

The one-byte protocol is shared by the updated source. Older installed binaries
ignore it and must not remain participating controllers when a future update is
qualified. This offline task does not update them. No legacy backend or capability
is enabled by these changes.

The state is flushed before admission. Tests cover process termination and
injected storage failures, not filesystem/hardware power-loss durability or
hostile modification of the state file. If clean-state persistence and restoration
both fail, a process-local quarantine can retain the OS lock; it cannot promise
storage persistence after that process disappears when the storage layer rejects
all writes. The warning-write failure itself does not attempt a clean transition.

## Validation

All checks use isolated temporary lock directories, fake dispatch and input-free
native/Python child processes. No game, desktop input, audio, model or installer
was used, and no test touched the user's normal shared lock directory.

| Check | Result |
| --- | --- |
| Release native suite, warnings as errors, no restore, two processors | **1,410 passed, 0 failed, 0 skipped**, 10-second test duration; final command exit 0. |
| New native cases | **31**: 21 lease/guard cases, 5 scheduler/module failure cases, 5 cached legacy-owner cases. |
| Python recovery tests | **17 passed**, including persistence failure/quarantine and report/close handling. |
| Native/Python interoperability | **8 passed, 0 skipped** against the rebuilt input-free helper. |
| Existing hub regressions | **15 passed**. Previewed commands were not executed. |
| Board regressions | **22 passed**; the 30-workstream board validates. |
| Native format/analyzers | Scoped whitespace verification and analyzers passed for eight changed C# files. |
| Python style/syntax | Black checks passed individually for the three changed Python files; compileall passed. |

The cross-process failure check injects a native warning writer that throws,
confirms there is **no warning path**, then exits. The shared guard remains `1`.
A fresh native process and Python owner both refuse admission. Other cases cover
native process termination, Python worker exceptions/nonzero exit, clean releases
in both directions and malformed guard contents. These are actual OS file-lock
tests with no input transport, not simulated ownership alone.

The integration regressions also show zero live callback executions after a
failed warning save across two schedulers while offline callbacks continue.
An adapter cannot dispatch a second fresh proposal through the poisoned lease;
its uncertain first receipt is retained. A mistaken Completed verdict or subsequent
cancellation remains CleanupFailed. Release-only key-up cleanup is preserved.

Evidence: `.local/input-recovery-20261110/input-recovery-final.trx`,
`python-validation.json`, `python-style.json`, individual test logs and
`validation.json`. The first native attempt passed the tests but failed the strict
build on a platform annotation and test nullability warning; both were fixed
before the clean final run. Its original TRX is retained. A multi-file Black
check stalled in the restricted runner and was interrupted; bounded single-file
checks then passed with no matching formatter parent process remaining.

This is a tested source update. Installation and live qualification remain
separate; older installed runtimes do not gain the new shared guard automatically.
