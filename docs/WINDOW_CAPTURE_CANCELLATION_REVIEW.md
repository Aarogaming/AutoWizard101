# Window capture cancellation and lifetime review

Reviewed **2026-11-09**. This is a read-only design review. No capture code was
changed, built or run, and no desktop/game access occurred.

**The smallest safe fix separates cancellation of the caller's wait from
ownership of an already-started native copy.** Adding cancellation to gate
admission is useful but does not bound an in-flight GPU copy. A deadline wrapper
alone does not solve result disposal or observer lifetime.

## Current paths and limits

| Path | Observed behavior | Consequence |
| --- | --- | --- |
| [WindowObserver.ReadPixelsAsync](../ProjectMaelstrom/ProjectMaelstrom/Utilities/WindowObserver.cs), lines 161–192 | Uncancelable `_gate.WaitAsync()`; holds the gate and capture frame across `SoftwareBitmap.CreateCopyFromSurfaceAsync`, buffer copy and byte extraction | Correctly prevents observer disposal during this copy, but the caller cannot interrupt its wait. |
| Same file, `ReadTimestampedAsync` / `ReadAsync` | Adds bitmap creation after the pixel read, with no token | A late result may be a disposable bitmap, not just a native copy task. Cancellation ownership must cover the complete requested result. |
| Same file, `DisposeAsync`, lines 247–263 | Waits on the same gate; then takes `_surfaceSync` and disposes session/pool/device | A stalled copy also stalls cleanup. A blocked surface callback can delay cleanup separately. |
| Same file, `ReadSurfaceAsync` / `SetSurfaceReceiver` | Transfers frame ownership to consumers; the gate does not cover that consumer's later GPU use | CPU-copy cancellation changes do not automatically qualify surface/encoder teardown. |
| [PetDanceLiveTrial.PlayAsync](../ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceLiveTrial.cs) | Checks cancellation before an unbounded frame read; then awaits observer disposal in finally | Gameplay/no-frame clocks cannot run while the read is stuck. The final report is written after cleanup, so it may also be delayed. |
| Pet Dance supervised Idle check | Calls `WindowsHomeMovementRuntime.CaptureAsync(..., CancellationToken.None)` | Cleanup capture has no caller cancellation and inherits the same copy/disposal limitation. |
| [HomeMovementRun.CaptureAsync](../ProjectMaelstrom/ProjectMaelstrom/Utilities/HomeMovementRun.cs) and [DodgeLiveRun.NextFrameAsync](../ProjectMaelstrom/ProjectMaelstrom/Utilities/DodgeLiveRun.cs) | Two-second/requested timeout loops check time between reads | These are cooperative loop limits, not upper bounds on a single read or final disposal. |
| [PassiveTeachingSampler](../ProjectMaelstrom/ProjectMaelstrom/Utilities/PassiveTeachingSampler.cs) | Duration/token checks surround an unbounded read; uses `await using` | A sampling duration is not currently a hard capture shutdown deadline. |
| [GameWorkspaceForm](../ProjectMaelstrom/ProjectMaelstrom/GameWorkspaceForm.cs), `TickAsync` / `ReleaseAsync` | Busy flag prevents overlapping preview reads; release awaits observer disposal | UI remains asynchronous, but stop/close completion can still wait indefinitely. Generation checks already help reject late preview publication. |
| [NativeWindowVideoProbe](../ProjectMaelstrom/ProjectMaelstrom/Utilities/NativeWindowVideoProbe.cs) and [WindowVideoProbe](../ProjectMaelstrom/ProjectMaelstrom/Utilities/WindowVideoProbe.cs) | Producers await unbounded pixel reads; finally awaits producer and observer | Encoder timeouts do not by themselves bound capture-producer shutdown. |

`WaitForFrameAsync(token)` already has a cancellable 250 ms notification wait.
It does not bound the separate frame-copy operation. The existing
[OperationScheduler](../ProjectMaelstrom/ProjectMaelstrom/Utilities/OperationScheduler.cs)
retains input ownership until its worker exits; its deadline cancels a token,
rather than forcibly interrupting these native operations.

## Why a timeout wrapper alone is insufficient

Microsoft describes `Task.WaitAsync` as a task representing the wait, which
can finish on cancellation/timeout independently of the original task. It does
not document stopping that underlying work.
[Task.WaitAsync](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.waitasync?view=net-9.0)

The surface-copy API is asynchronous and returns a new bitmap on successful
completion. Its reference does not provide a fixed completion bound or permission
to dispose the source while copying.
[SoftwareBitmap surface-copy contract](https://learn.microsoft.com/en-us/uwp/api/windows.graphics.imaging.softwarebitmap.createcopyfromsurfaceasync?view=winrt-26100)

Therefore, avoid these apparent fixes:

- Returning after `WhenAny`/`WaitAsync` and forgetting the original task:
  late exceptions and disposable results still need an owner.
- Canceling an await inside the current `using frame` and treating that as
  proof that native work no longer uses its surface.
- Disposing the observer/session/pool/device simply because the caller timed out.
- Returning from a bounded read and immediately awaiting the same unbounded
  `DisposeAsync` in finally; the caller deadline still has no useful bound.
- Moving the whole gameplay routine into an abandoned task; it could retain
  input capability after its scheduler owner returns.

Native cancellation may be investigated separately. The reviewed API references
do not establish a teardown deadline; no such guarantee should be inferred.

## Proposed smallest implementation sequence

### 1. Cancel admission without weakening current ownership

Add optional caller tokens through `ReadAsync`, `ReadTimestampedAsync`,
`ReadPixelsAsync` and `ReadSurfaceAsync`. Use the token while waiting to acquire
the gate and check it before starting new work. Release a gate only after
successful acquisition.

Keep the existing copy/frame/device lifetime intact. Do not claim that this step
interrupts an already-started native copy. It is a small independently testable
improvement, not the full caller-deadline fix.

### 2. Give each CPU read a lifetime owner

Introduce one small owner around the **complete CPU read task**, returning
`CapturedWindowFrame` or `CapturedWindowPixels`. Suggested interface concepts
are a read completion, caller wait, and tracked cleanup completion; names are
proposals, not implemented APIs.

The owner must enforce these outcomes:

| Outcome | Required owner and action |
| --- | --- |
| Read completes and caller accepts result | Transfer the returned disposable exactly once to the caller. |
| Caller cancels before native work starts | Remove/cancel admission; no frame, copy or returned buffer needs abandonment. |
| Caller cancels during copy | Return cancellation to the caller; keep the original task, frame and observer alive in a tracked drain. |
| Abandoned read later succeeds | Drain disposes the late bitmap/pixel result exactly once, then completes observer cleanup when eligible. |
| Abandoned read later faults | Drain observes and records the exception; no unobserved fault and no automatic retry. |
| Completion races cancellation | Exactly one result owner wins: caller or drain. Never both, and never neither. |

Do not rely on the GC or a discarded continuation for ownership. A completion
task must account for the whole late result, including bitmap conversion after
native copy. Keep at most one CPU read in flight per observer and reject new
reads once stop begins.

### 3. Separate requested stop from finished disposal

Mark the owner closing before waiting on native locks, so new reads and callback
admission stop. Do not synchronously wait for `_surfaceSync` from the caller's
cancellation path. Existing callbacks/resources may still require draining.

An explicit bounded stop wait should return either **drained**, **cleanup pending**
or **cleanup failed**, with a retained completion/receipt identity. Keep
`DisposeAsync` truthful: when its task completes successfully, disposal really
has completed. Do not quietly redefine it as “disposal scheduled.”

The application's capture supervisor must own any pending drain after the
consumer returns. It must outlive a preview form or live-trial host, observe
every outcome, and refuse automatic replacement of a still-retiring observer.
For the first slice, a single pending-drain slot is sufficient; if occupied,
new capture is unavailable rather than creating another detached worker.

This bounds the caller wait and the number of retained operations. It cannot
promise native completion or immediate resource reclamation in the same process.
A permanently stuck native operation may retain bounded resources indefinitely;
report that state instead of claiming successful cleanup.

### 4. Integrate the live caller narrowly

For Pet Dance first:

- Pass the run token and a per-read deadline into the owned CPU-read path.
- Recheck cancellation, activity/target permission and frame freshness after
  receiving a result and before deriving or dispatching an input.
- On cancellation, clear pending actions and finish/release owned input through
  the existing input mechanism. The detached drain must contain capture work
  only and have no input capability.
- Request capture stop and wait only the declared cleanup budget. Save a report
  even when cleanup remains pending. Preserve a pending cleanup outcome instead
  of labeling the operation fully completed.
- Give the supervised Idle evidence capture a separate finite cleanup token;
  using an already-canceled gameplay token would also be wrong.
- Do not release a scheduler/input owner while an input-capable worker survives.
  A tracked, capture-only drain is a different ownership boundary.

Then migrate home movement, Dodge frame acquisition, passive sampling and
Observe preview. Preview generation checks must continue to prevent a canceled
read's bitmap from appearing after disconnect.

## Surface/encoder path requires its own lifetime check

[GpuSurfaceVideoProbe](../ProjectMaelstrom/ProjectMaelstrom/Utilities/GpuSurfaceVideoProbe.cs)
retains frames with
[SharedLease](../ProjectMaelstrom/ProjectMaelstrom/Utilities/SharedLease.cs)
until the encoder's Processed callback. Its callback/encoder path also has an
uncancelable sample gate. Finally drains queue entries and releases remaining
in-flight entries after encoder failure/cancellation.

This is a separate qualification question: completion of a canceled await alone
does not prove that every encoder callback has stopped using its surface. Do not
claim the current forced-finally release is qualified by the CPU-copy review.

A returned surface lease should eventually retain the observer lifetime as well
as the frame until the consumer has finished using it. Receiver callbacks must
remain bounded and nonblocking. The proposed CPU deadline owner should not be
applied to raw surface results without accounting for those outstanding leases.

## Test seams before native testing

Extract the ownership coordinator from Windows capture behind a deferred read
delegate and disposable fake result/resource. Use controllable completion
sources, not a live GPU, for these tests:

1. Cancellation before gate acquisition creates no copy and does not over-release
   the gate; a subsequent admitted read still works.
2. Cancellation during copy returns promptly while source frame and observer
   remain undisposed until the deferred copy is completed.
3. A late successful result is disposed once; a delivered result stays caller-owned.
4. A late fault is observed in cleanup evidence and does not schedule another read.
5. Completion/cancellation races transfer exactly one ownership claim.
6. Stop during copy closes admission, shares one cleanup task across repeated
   stop calls, and cannot grow the supervisor's pending-drain count.
7. Cleanup-budget expiry reports pending; later drain changes it to drained or
   failed. It never rewrites the earlier pending event as though cleanup was instant.
8. A blocked surface callback/borrowed lease prevents actual disposal while the
   caller still receives a pending status.
9. Caller integration writes a cancellation/cleanup report, emits no later game
   inputs and never publishes a late preview.
10. CPU-copy tests do not assert that encoder surface ownership is solved.

Use injected cancellation/completion signals for deterministic tests. A later
authorized native test can measure cancellation latency and resource release,
but cannot prove a universal GPU-driver deadline from one successful run.

If hard process-level fault containment is required, evaluate an owned capture
helper process with bounded stop/exit supervision. That is a larger change with
transport/throughput costs, not a one-line token fix; it must never terminate the
game. Even then, report any unconfirmed exit/resource cleanup accurately.

**Review status:** concrete path and test plan only. No cancellation or lifetime
repair is implemented by this document.

