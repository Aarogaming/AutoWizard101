# Verified input recovery — 2026-11-10

This source update adds an explicit recovery flow on top of the
[shared input guard](INPUT_RECOVERY_GUARD_20261110.md). It does not install an
update or qualify a game. Three parallel agents handled the recovery transaction,
interface and scheduler drain; the coordinator integrated the workflow, read-only
Windows adapter, regression coverage and project board.

## User workflow

Open **Settings → Restore game controls** or the same action in **Routine runs**.
Opening the dialog does not stop, inspect or control anything.

1. **Stop routines** pauses new live submissions and requests cancellation of
   queued/running live jobs. It waits for worker, cancellation-callback and input
   ownership cleanup. Offline jobs can continue. If a worker is still cleaning
   up, the dialog explains that and leaves the hold in place.
2. **Check cleanup** asks the user to keep Maelstrom active and leave the controls
   released briefly. Two current samples, at least one second apart, must report
   stopped workers, an accessible active desktop, readable input state, no held
   keys/buttons and unchanged input activity. A failed or unavailable check is
   not treated as evidence that controls are released.
3. **Restore controls** performs a new verification while holding the exclusive
   shared input lock. The preview alone cannot restore anything. Original recovery
   evidence is preserved before clearing the guard. The dialog reports completion
   only after that transaction succeeds.

Interrupted routines stay stopped. Recovery does not enable a disabled automation
setting, authorize a game, resume a queue, release keys by injection, or select a
game window. Closing cancels a pending check and waits for its operation to exit
before disposing its admission hold. Closing alone cannot clear a dirty guard.
Observe, recordings and offline reviews remain usable.

The UI uses fixed explanatory messages. Internal errors, saved warning contents
and local paths are not copied into normal status text. If another worker still
owns input, the user is told to stop/close that worker and retry; recovery never
forces its lock away. A quarantined owner cannot be unlocked by this dialog.

## Shared contracts

`OperationScheduler.PauseLiveForRecovery` creates a scoped admission hold across
all schedulers sharing the same normalized input directory, including directly
constructed schedulers. The hold is not input authority. New live admission is
blocked atomically; queued work from an older recovery generation is permanently
invalidated. Cancelling a wait or reaching its timeout does not release input or
dispose schedulers. The actual drained condition is separate from an operation's
completion receipt and includes ownership cleanup.

`InputRecoveryOperation` owns the explicit Stop → Check → Restore lifecycle.
`WindowsInputRecoveryProbe` is the application-owned read-only verifier, injectable
for offline tests. `GameInputRecovery` creates a fresh challenge under an exclusive
non-dispatching recovery reservation. Neither game packs nor saved JSON can supply
a release verdict or execute a verifier.

Every final sample is challenge-bound, current within 100 ms, monotonic, within a
10-second transaction budget, and separated by at least one second of unchanged
activity. The scheduler hold remains active throughout. The final sample is
checked again after evidence writes, so slow storage cannot silently make old
proof current. No model, polling service, extra queue or production dependency
is introduced. Input state is sampled only during an explicit check.

## Persistence and failure handling

Recovery uses the existing byte-zero OS lock and one-byte guard, not a replacement
lock. It never exposes an input lease or `CanDispatch`. The recovery generation
must match exactly; a new cleanup report invalidates the attempt. Another process
holding the lock, malformed/oversized files, changed warnings, stale samples,
cancellation or failed evidence writes prevent restoration.

Under the input directory, `recovery-history/<attempt-id>/` stores original guard
bytes, the original warning when present, an attempt record and a verification
record. Original warnings are archived without overwrite. Files are published
with create-new semantics and flushed. The verification record says
`verified-for-commit`: it deliberately does not claim a later commit succeeded.
The current guard under the OS lock remains authoritative. Partial attempts are
preserved for diagnosis. No success receipt is fabricated after a storage failure.

The reservation is armed dirty before verification. Aborted recovery restores
dirty state; if even that write fails, the process retains the reservation in
quarantine rather than releasing an unconfirmed guard. The clean transition is
flushed only after verification and evidence preservation. Existing poisoned
lease objects never regain dispatch permission; only a new explicit owner can be
admitted. A Python process's old local recovery latch is not remotely erased:
restart that stopped worker before starting a new job after verified recovery.
Successful OS unlock is the commit boundary. If closing that already-unlocked
handle then fails, the result truthfully reports restored controls with a close
warning and recommends restarting Maelstrom. It never rewrites the guard after
unlock, when a different owner may already hold it.

Older installed binaries do not honor the new shared guard. Update participating
controllers together before live qualification. This source task does not change
the installed application. A process-local quarantine cannot promise durable
storage after its process disappears if the filesystem rejects every write.

## Windows verification boundary

The adapter checks the current WTS session is active, the input desktop matches
its own interactive desktop, Maelstrom owns the foreground window, keyboard-state
reads are available, no asynchronous key/button high bits are set, and input
activity stays unchanged. It checks session/foreground/desktop access again after
reading. It records booleans and an activity revision, not key identities or text.
It does not take focus, install hooks, inject input, capture screens, read game
memory, access traffic or change audio/settings.

This is conservative because [OpenInputDesktop can succeed for a disconnected
session](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-openinputdesktop),
and a zero [GetAsyncKeyState result can also mean unavailable input access](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getasynckeystate).
Session activity uses [WTSQuerySessionInformation](https://learn.microsoft.com/en-us/windows/win32/api/wtsapi32/nf-wtsapi32-wtsquerysessioninformationw).
Keeping Maelstrom active is a user action, not an automatic focus takeover.

This verifies current Windows input state and known scheduler cleanup. It does
not prove W101 reached native Idle, that a game processed a key-up, or that an
arbitrary external bot stopped. Game outcomes require their own observations.
Locked/disconnected sessions and lost foreground remain blocked. Keyboard-only
activation may require releasing the activation key and retrying the check.

## Verification status

The final Release build and full suite pass with warnings treated as errors,
no dependency restore and two processors. All new tests use fake observations,
temporary lock directories and input-free callbacks. The Windows adapter is
compiled and tested at its injected boundary; its real desktop path was not run.

| Check | Result |
| --- | --- |
| Full native suite | **1,507 passed, 0 failed, 0 skipped**; 12-second test duration, final command exit 0. |
| New cases | **97**: 33 transaction, 14 scheduler-drain, 30 workflow/adapter, 13 presentation and 7 UI cases. |
| Python recovery / interoperability / hub / board suites | **62 passed, 0 skipped** (17 / 8 / 15 / 22); helper remains input-free. |
| Format and analyzers | Scoped whitespace verification and analyzers pass for all 14 changed C# files. |
| UI preview | Ready and Restored panels inspected as offscreen composites from actual controls and fixed fake states; no form handle, shown window, focus change or real probe. |
| Board | 30-workstream board validates; four task claims released after integration. |

Regression coverage includes held/unknown/stale/replayed inputs, changed activity,
worker cancellation and timeouts, direct/multiple schedulers, blocked callbacks,
new explicit jobs surviving a delayed old cancellation, evidence-write failure,
changed recovery generations, absent warnings, dirty rollback, late cancellation
after successful commit, and a handle-close error after another owner has begun.
No test is described as proof that a game processed cleanup. Double-failure
quarantine retention is source-reviewed here; these transaction tests do not
intentionally leak a quarantined lock inside the shared test process.

The first full run exposed an omitted `InvalidDataException` handler (20 failures,
all traceable to that path); it was repaired before the passing runs. Preliminary
hidden-form images omitted or misplaced child controls. The test renderer now
lays out a detached content panel, checks non-overlap and button bounds, and
composites it without showing a form. This qualifies the offline preview, not
real displayed-window/DPI behavior. Original test logs are retained.

Evidence is in `.local/input-recovery-flow-20261110/`: final
`recovery-flow-complete.trx`, `native-complete.log`, `python-validation.json`,
`style-validation.json`, `board-validation.log`, `validation.json`, and
`ui/recovery-ready.png` / `ui/recovery-restored.png`. The manifest fingerprints
the 14 source/test files and records the input-free verification boundary.

Remaining qualification is a separately scoped interactive check of the real
Windows verifier and installed UI. No live game, real input, capture, model,
installer or normal user input-lock directory is used in this offline task.
