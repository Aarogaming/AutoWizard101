# Bounded deck observation refresh

Source implementation, 2026-11-11. Offline validation belongs to coordinator.

The existing host has a cancellable one-second admission timer. Only a waiting
deck with an enabled Quest card, exact connected session and existing reader/action
opt-ins can request refresh. Fresh facts, active observer/operation, scheduler work,
recovery, draining and stop prevent submission. It reuses the existing Observe
operation and shared owner; no new controller or live callback is introduced.

Admission is bounded to30attempts per run with5seconds between submissions and at
least5seconds remaining. Each observation is cancelled at the deck deadline and
awaited through cleanup. The cadence deliberately leaves stale intervals; stale
facts cannot authorize work. This is a low-frequency quest context producer, not
a high-rate minigame feed. Other resource producers remain unavailable.

Read-only `capabilities` exposes typed fact descriptors and existing routine
catalog, explicitly separating metadata from permission/live qualification.
Snapshot exposes refresh attempt count and reason. Dispose cancels and awaits
the timer and observer; pause/stop cancel the observer and stop deck eligibility.

Added pure time/budget/eligibility tests and disconnected-host discovery test.
Added actual deck/scheduler integration using a synthetic fact provider: Waiting
becomes eligible after refresh, completes once, and Stop prevents new reads.
This tests composition, not W101 reader accuracy. Cancellation increments an
observation generation; completed results publish only under the host lock while
the generation remains current, the deadline is open and disposal/drain is absent.
Per-routine trusted extension definitions are exposed separately in routineFacts.
No game, desktop, installation or dependency access occurred. Native tests and
formatting must be run by coordinator before this slice is considered verified.
