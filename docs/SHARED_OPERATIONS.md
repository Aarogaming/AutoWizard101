# Shared operations and the first scheduled teacher

Implemented 2026-11-08. This is the first working slice of the
[W101 integration map](W101_CAPABILITY_INTEGRATION_MAP.md), not completion of all
its gameplay or cleanup qualification gates.

The [2026-11-10 scheduler checkpoint](SCHEDULER_REUSE_INTEGRATION_20261110.md)
records the current source update, refreshed Guild/Steward references and its
verification status. The installation and early teacher measurements below
remain historical evidence.
The later [input recovery guard](INPUT_RECOVERY_GUARD_20261110.md) closes the
warning-save gap across native and Python owners; 1,410 native tests and the
input-free cross-process checks pass. Neither checkpoint installs an update.
The [verified recovery flow](INPUT_RECOVERY_FLOW_20261110.md) adds the current
Settings/Routine runs interface, shared live-worker drain and evidence-preserving
restoration. Its validation record separates offline checks from the still-pending
real-desktop and installation qualification.

## Use it

Open **Wizard101 → Observe & teach → Pet Dance teacher**. Load a calibrated pack
and completed recording, then choose **Run teacher routine → save pack**. The
teacher enters the common queue and saves proposals plus a run receipt. Stop
cancels either the queued job or the running review. Original recording labels
remain unchanged. The [teacher guide](PET_DANCE_TEACHER.md) describes calibration.

**Wizard101 → Routine runs** shows the latest 100 run results, explicit Stop and
Open run evidence. Refresh is manual; the view does not poll the game or start
background work. Loading history never resumes an interrupted routine.

This first native Pet Dance operation reviews recorded rounds. It does not press
game keys, play live rounds, feed snacks or infer victories from proposals. Live
Pet Dance still requires current-game cue/phase calibration and independently
verified action and round outcomes. The existing guided legacy player remains a
separate, explicitly unqualified method; scheduling does not qualify it.

## Shared execution path

| Entry point | New connection |
| --- | --- |
| Pet Dance recorded teacher | `w101.pet-dance-review`, immutable input hashes, frozen phase predictor, 600-second budget, report and draft pack artifacts |
| Auto-Idle | `w101.auto-idle`, same queue and ownership, existing desktop-activity/freshness/target checks, maximum 1800-second scheduler budget |
| Old SmartPlay navigation/scripts/capture | Common operation worker; finite repeats, serialized pump, current policy/target checks; unverified gameplay pauses the remaining legacy task chain |
| Manage Scripts / bridge | Common scheduled script run, entry-point hash, preflight, owned child-process group and bounded duration |
| Guided Deimos / legacy Dance | Common scheduled worker with explicit delegated input ownership: the Python hub holds the same OS lock while its supervised child runs |
| Legacy input batches | Common bounded operation when called outside an existing owner; selected target, 256-command limit and bounded delays |
| Native compatibility inputs | Shared ownership, exact current foreground game checks, no focus stealing; held keys expire within two seconds and release on policy/focus loss |
| Dodge desktop probe | Same shared input owner for its existing short bounded run |

The old standalone Halfang and Bazaar purchasing forms retain their source but
cannot start outside a shared owned routine. They are not silently promoted to
qualified operations. This closes historical timer-driven bypasses while their
future repairs remain tracked in the integration map.

The coordinator runs one job at a time per application data root, admits at most
32 jobs, and has no idle polling worker. Its timer is not a gameplay reaction
clock. Pet Dance retains one sequential OpenCV worker and no model inference.
The old SmartPlay intake has a separately bounded staging queue; its requests
are migrated into the common durable queue as admitted. Periodic schedules and
editable dependency graphs are not implemented by this change.

## Contracts and persistence

`OperationRequest` records a unique job ID, operation, backend version, finite
timeout, whether it writes to the game, target description, input hashes and
whether an external adapter owns input. Delegates are trusted application code,
not executable content loaded from a game pack. Direct low-level methods remain
implementation details behind these entry points.

The admitted request has a private, read-only copy of its input hashes. Editing
the original collection or casting the returned ticket's collection cannot
change the queued checks. The scheduler validates the copied values and checks
the actual files again immediately before execution.

New jobs retain `operations/<job-id>/journal/admission.json` and numbered,
hash-linked lifecycle snapshots. Complete staged bytes are published without
replacement before updating `receipt.json`, the existing latest-status view.
The journal is bounded to four snapshots of at most 1 MiB each; normal execution
uses Queued, Running and one terminal snapshot. A committed terminal snapshot
remains authoritative if interruption leaves a stale or missing status view.
The log orders events by sequence, not by assuming the wall clock never moves
backwards. Hash links detect inconsistent evidence; they do not authenticate
external edits or establish power-loss durability.

Terminal states distinguish Completed, Failed, Cancelled, TimedOut, Interrupted,
Unknown and CleanupFailed. Hash mismatch prevents execution. Duplicate IDs
cannot replay a job. Startup validates authoritative job evidence before
recovery writes, preserves new-job history and marks unfinished jobs Interrupted.
It never reconstructs or executes their delegates. A new run requires explicit
submission and fresh admission. Receipt-only jobs from older versions retain
their existing recovery behavior; history that was never saved is not invented.
The current run-history screen remains read-only and does not start a controller.

Teacher jobs also retain `teacher-request.json` and, when used, the exact frozen
`phase-rubric.json`. Source frame hashes are checked by the existing recording
reader; asset hashes are checked by the teacher importer. This is not a snapshot
of every transitive legacy dependency: those adapters still need independent
version qualification.

Cancellation callbacks run outside the queue lock. A cancelled queued job finishes
immediately without executing. A running job retains ownership until its worker
and cleanup exit; there is no timeout race that abandons a still-active writer.
Managed external jobs are killed as owned process groups on cancellation. Native
delegates must cooperate with cancellation; arbitrary blocking plugin code is not
made safe merely by giving it a timeout.

Legacy JSON inboxes retain received requests and parse errors instead of deleting
invalid input. These archived requests are evidence, not an automatic retry queue.
Process exit and an empty command list are no longer labeled verified gameplay
success. Current legacy results can be Unknown even after a zero exit code.

## Cross-language ownership

Native and Python adapters lock byte zero of the same persistent file:

`%LOCALAPPDATA%/Maelstrom/session-locks/game-input.lock`

This location is independent of the source workspace and portable data folder.
The file is never deleted/replaced to force acquisition. Windows releases the OS
lock when its owning process dies. Updated runtimes also store a one-byte guard
under that lock: they flush `1` before granting input and clear it to `0` only on
ordinary clean release. Empty legacy files are upgraded; dirty/unknown contents
deny admission. Thus process death releases the mutex but leaves cleanup
unconfirmed. This is not proof that the game reacted or that an arbitrary external
bot released every key.

If a native input release cannot be confirmed, an
`input-recovery-required.txt` notice explains the recovery hold. The hold is
latched before that write and the guard stays dirty even if the notice cannot be
saved. Further actions through the same owner and subsequent owners are blocked;
release-only cleanup and offline evidence review remain available. Warning
removal does not clear the guard. **Settings → Restore game controls** (also in
**Routine runs**) now provides Stop routines → Check cleanup → Restore controls.
It verifies current released input under exclusive ownership before clearing
the guard and preserves original evidence; interrupted runs never resume.
Keep Maelstrom active during both checks. See the
[full recovery contract](INPUT_RECOVERY_FLOW_20261110.md) for failure handling,
worker-restart limits and validation status. Older builds do not honor the new guard; update participating
native/Python workers together before qualifying live routines.

## Tests and remaining live boundary

The offline suite covers serialized execution, queue cancellation, cancellation
lock ordering, duration limits, concurrent owners, immutable queued inputs,
restart/duplicate behavior, queue capacity and failed-release recovery. The
cross-language test starts only the small input-free test helper: Python blocks
native acquisition, native blocks Python, and terminating the owner frees the
lock. Its directory override is confined to isolated tests.

The compiled Pet Dance verification now runs the actual teacher operation through
the scheduler on synthetic recorded scenes using the supplied archive images.
The first check reconstructed all three varied sequences from 153 frames,
recognized all 40 scale cases, and rejected sparse-frame proposals. It took
16.38 seconds wall time and 8.19 seconds process CPU, with one OpenCV thread and
no model or game input. This measures a recorded-fixture workflow, not live
reaction speed or current-game accuracy.

Evidence: `.local/scheduler-integration/teacher-check-01/verification.json` and
its `operations/` receipts; tests are saved under
`.local/scheduler-integration/tests/`. Release validation is recorded separately.

W101 was visually checked at the minigame sigil and showed **YOU ARE IDLE**. These
offline tests did not leave that state and required no user play. Automatic
screen-menu Idle restoration and current-game Pet Dance execution remain
separate live qualification gates. In particular, the Auto-Idle scheduler receipt
does not claim that it proved login retention or restored native Idle.

Next: qualify Watch/Go/result recognition on a real Pet Dance recording, implement
and verify menu-based native Idle cleanup, then enable bounded live teacher
dispatch through this shared owner. Do not use synthetic calibration as live
game calibration or promote an unverified legacy script to a trusted teacher.

The [release validation](SHARED_OPERATIONS_VALIDATION_20261108.md) records the
installed update, preservation checks and the later brief live entry check. That
check confirmed the current sigil serves mana minigames, observed Tiny UI, and
returned W101 to native Idle through the screen menu. It was a manual desktop
check, separate from the scheduler-driven recorded teacher verification.
