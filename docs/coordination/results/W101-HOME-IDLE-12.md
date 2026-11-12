# Home movement: shared-owner Idle finalization

November 11 source integration. Offline only.

Inspection corrected the earlier roadmap assumption: HomeMovementForm already
submitted to ApplicationOperations and received GameInputLease from the scheduler.
HomeMovementLease was an unused compatibility shim, not an active separate owner.
The missing integration was finalization and exact owner checks in the runtime.

HomeMovementOperation now prepares the complete bounded session. The request
uses the same process-instance target convention as other modules. Runtime
permission requires that exact context lease to remain current and dispatchable.
The form no longer stores or disposes the scheduler-owned lease.

The movement gate exports its original armed activity revision. Only an armed,
cleanly drained session with the same revision, foreground target, no held keys,
no Escape and enabled controls can enter finalization. The visual native-Idle
controller runs under that same lease, monitor and original baseline, using the
shared bounded Idle fail-safe. No extra client hooks, model, focus change, input
owner or capture stream is introduced. Cancellation and takeover skip cleanup.

Reports preserve movement results and Idle results separately. A paused or failed
movement does not become a successful session because Idle succeeds. Missing
cleanup evidence blocks further input. Login retention and actual character
motion are not inferred from a completed session or changed image.

The outer scheduler limit remains 30 minutes. A 45-second finalization reserve
means the maximum configured 30-minute session has at most 29 minutes 15 seconds
for movement. Shorter sessions retain their configured movement duration plus a
small drain margin. The operation report records the exact movement budget.

No automatic quiet resume is enabled in this home flow; human takeover still
requires a deliberate new start. The new shared policy does not silently enable
resume settings from a different routine.

Tests cover shared lease identity, target mismatch, original baseline retention,
normal end, paused movement, takeover, Escape, never-armed sessions, parent
cancellation and uncertain movement/finalizer cleanup. All game callbacks are
synthetic; no live game observation or input is part of this evidence.

Next live gate: attention notification and Ready reply, a short bounded home
session in a verified clear area, then independent confirmation of native Idle.
No installer or live check was run for this task.

Verification: **2,366 native tests passed**, zero failures/skips. Warnings-as-errors
build, scoped whitespace formatting/verification, analyzers and git diff whitespace
checks passed. Tests ran serially with DOTNET_PROCESSOR_COUNT=2. Project-board
validation passed. Live behavior remains unqualified and the installed app is unchanged.
