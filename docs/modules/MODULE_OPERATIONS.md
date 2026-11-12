# Scheduled module operations

`ModuleOperation` connects the reusable module workflow to the existing
`OperationScheduler`. It creates neither another queue nor another input owner.
The scheduler retains bounded queueing, cancellation, deadlines, source pins,
durable receipts and explicit restart behavior.

The implementation is in
[`ModuleOperation.cs`](../../ProjectMaelstrom/ProjectMaelstrom/Utilities/Modules/ModuleOperation.cs).
Its scheduler tests are in
[`ModuleOperationTests.cs`](../../ProjectMaelstrom/ProjectMaelstrom.Tests/ModuleOperationTests.cs).
This is source integration; these files do not establish installation or live
gameplay qualification. The coordinator records executed checks separately.

## Saved-screen review

```csharp
ModuleOperation.SubmitOffline(scheduler, frozenScreenReview, timeoutSeconds: 60);
```

The caller loads a `ModuleScreenReviewSnapshot` through the screen-review loader.
It freezes the bounded manifest, bundle, event stream and original image bytes,
along with SHA-256 source pins. The operation copies those pins into its request.
The scheduler verifies the current source files before execution, then the
reviewer uses the snapshot without reopening sources. A queued edit fails the
operation; an edit during execution cannot replace the frozen pixels.

This entry always requests `WritesGame=false` and `DelegatedInput=false`.
Neither `OperationContext.Input` nor the ambient `GameInputLease.Current` may be
present. The review can therefore run while another routine owns input; it does
not reserve the desktop. It passes only the saved snapshot, a report path and a
cancellation token to `ModuleScreenReview.RunAsync`. There is no dispatcher,
transport selector, game-window handle, script loader or input callback in this
entry. Its local OCR work reads supplied image bytes; it does not capture a game.

The operation writes these artifacts under its existing scheduler run directory:

| File | Meaning |
| --- | --- |
| `receipt.json` | Scheduler state, pinned sources, timestamps and artifact links. |
| `module-operation.json` | Offline mode, session ID, source pins, no acquired input lease, zero desktop dispatch and no live qualification. |
| `module-screen-review.json` | The reviewer's decisions, supplied receipts, recognition provenance and observed module outcomes. |

The review must preserve the distinction between a proposal, an explicitly
supplied rehearsal/recorded receipt, and a later observed outcome. The scheduler
wrapper does not generate receipts or relabel synthetic rehearsals as real
gameplay. A completed offline contract reports `Completed`; a stopped contract
reports `Failed`; an incomplete contract reports `Unknown`. None proves that a
game accepted an action. A malformed or missing report cannot become completion.

User cancellation and the wall-clock deadline are distinct scheduler outcomes.
The reviewer checks cancellation between bounded work and preserves a partial
report. The wrapper includes that report when it exists. Cancelling a queued job
never starts the reviewer. Scheduler restart marks interrupted work; it does not
resume actions or replay a job ID automatically.

## Trusted owned adapter entry

```csharp
ModuleOperation.SubmitOwned(scheduler, bundle, binding, execute, timeoutSeconds: 60);
```

This internal entry is for trusted application code only. No UI, CLI, module
JSON field or offline review selects it. It requests `WritesGame=true` and
`DelegatedInput=false`, allowing the existing scheduler to acquire its normal
`GameInputLease`. Immediately before invoking the callback, admission requires
the held `context.Input` object to be the exact ambient lease. A competing owner
or recovery notice prevents the callback from starting.

The bundle and visit/target/geometry binding are validated before queueing. The
wrapper freezes the admitted bundle and journals it as `admitted-bundle.json`,
with its hash and binding in `module-operation.json`. It does not mutate a plan
when the editor changes the original objects. The journal always says that
admission alone supplies no live qualification.

The callback is trusted code, not serialized bundle content. Its signature does
not receive the frozen bundle, so it must independently freeze and enforce the
same plan/action bindings, or consume the admitted plan. The wrapper cannot
prove that an arbitrary application delegate obeys a bundle. A live adapter must
still check permission, current target identity and geometry, fresh observations,
named controls, manual takeover, cancellation, and confirmed release for every
action. A retained lease is necessary but does not authorize arbitrary keys or
clicks. The existing `InputBridge` batch path is not the module dispatcher.

Cancellation keeps ownership until the callback has exited its cleanup. The
callback's terminal outcome is preserved: in particular, `CleanupFailed` reaches
the existing scheduler recovery mechanism and blocks another owner. The wrapper
does not convert uncertain release, incomplete outcomes or a successful input
submission into game success. A fresh explicit attempt is required after a stop.

The existing `PetDanceNativeIdle.RunAsync` continuation now consumes
`W101ModulePrograms.Postgame()`, `ModuleScreenSession` and
`ModuleOwnedInput.ForPetDance` in source. Its caller passes the original
`OperationContext` only after the Dance specialist permits postgame continuation
and confirms both worker cleanup and input release. The scheduler target now
includes the process start identity. The continuation reuses its current local
OCR result, hashes the exact analyzed frame, and lets the module session own
two-frame stability and receipt-bound transitions. It retains the existing
foreground/geometry, activity, heartbeat, permission, 300 ms freshness, finite
capture and cleanup guards. Its report adds the bundle, binding and module trace.

This replaces the old planner within an existing authorized code path; it adds no
new start button, controller or activation path. `SubmitOwned` itself remains an
internal trusted-code entry, not exposed by UI or CLI. These changes have not
been installed or qualified by a new live run. The owned-entry tests use fake
callbacks and temporary lease directories; they never call a Windows input API.
W101 remains UI-only, with game-specific qualification and current user
authorization required separately.

## Verification scope

The tests exercise the actual scheduler, not a second queue implementation:

- Saved Idle images complete the offline contract without obtaining an input
  lease, including when a temporary competing owner exists.
- Manifest, bundle, events and image edits while queued fail before review.
- Queued cancellation does not create review artifacts.
- Owned callbacks receive the exact shared lease; a competing lease prevents
  invocation, and cancellation retains ownership through callback cleanup.
- Unconfirmed cleanup retains the scheduler recovery barrier. Deadline and user
  cancellation remain distinct, with admission artifacts preserved.
- Invalid visit bindings and budgets fail admission; later editor mutations do
  not change the saved admitted plan.

Use the coordinator's serialized test/build slot. A focused filter is
`FullyQualifiedName~ModuleOperationTests`; it includes local OCR of saved pixels
but no capture, game input, game launch or network work. Passing these checks
qualifies orchestration and its fixtures only, not arbitrary menus, routes,
resolution changes or live completion.

See [reusable modules](../REUSABLE_MODULES.md) for the shared runner contract and
[screen recognition](SCREEN_RECOGNITION.md) for visual evidence admission.
