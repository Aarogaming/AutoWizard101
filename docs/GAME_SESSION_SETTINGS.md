# Game session settings transactions

The pure model in
[GameSessionSettings.cs](../ProjectMaelstrom/ProjectMaelstrom/Utilities/GameSessionSettings.cs)
captures verified entry settings, proposes one visual setting change at a time,
and tracks observation-confirmed restoration. It performs **no UI input, file
access, game attachment or settings-file writes**. Automatic settings changes
and restoration are not wired into the application by this component.

## Scope and admission

A pack can state desired values for reviewed visual game settings:
`ui-scale`, `resolution` and `window-mode`. A trusted application caller must
separately allow each key. Imported packs cannot extend the built-in reviewed
key set or authorize themselves. Security, privacy and operating-system settings
are outside this model.

`Capture(sessionId, requirements, trustedAllowedKeys, observation)` records the
actual observed original string for every required key before any proposal.
Missing/blank/unverified originals reject the entire capture; duplicate
requirements, unreviewed keys and invalid desired values are rejected too.
Desired values and originals are not replaced with defaults.

The caller supplies a unique session ID and immutable, verified observations.
A revision is a caller-generated increasing observation sequence, not a
timestamp or proof of freshness by itself. `Verified` must mean the values are
recognized from the supported current screen and the UI transition has settled.
A screenshot taken immediately after clicking is not necessarily verified.

Values are compared exactly. The game-specific recognizer must use consistent
value representations while preserving the entry values needed for restoration.
This model does not know which UI choices a particular game/version supports.

## Lifecycle

| Call | Result |
| --- | --- |
| `Capture(...)` | Immutable baseline session or a rejection reason. No proposal or action. |
| `Advance(session, observation, desktopAvailable)` | Updated session/status and at most one newly issued proposal. Requires a newer verified observation. |
| `RecordDispatch(session, proposalId)` | Records intent/attempt immediately before a caller would dispatch; it does not click. Application/restoration remain unconfirmed. |
| `RequestEnd(session)` | Requests restoration, disables dispatch until fresh admission, and invalidates an undispatched apply proposal. This is also the cancellation path. |

Each proposal contains a stable session/phase/key ID, before value, after value
and Apply/Restore kind. Repeated `Advance` calls do not reissue a pending
proposal. Its ID remains in the session, and only the authoritative latest
session state may be used for explicit dispatch. Dispatching twice against
that updated state is rejected.

An attempt becomes applied only after a newer verified observation shows the
desired value. An old, unverified or unchanged frame leaves it uncertain.
An unchanged value cannot distinguish a failed click from delayed UI work,
even if the scene appears settled. Keep the dispatched intent and never retry
it automatically. A later observed desired value can still confirm the attempt.
If the value is neither original nor desired, retain a conflict without
overwriting it.

On end/cancellation, restore only settings whose application this session
confirmed. Restore them in reverse application order. A setting already at its
original value can be marked restored from observation without a click.
Unchanged settings are never “restored,” so later user changes to those settings
are preserved.

Cancellation does not erase an attempted but unresolved change. In particular,
an apply attempt followed by an unchanged frame must not return `Restored`:
it remains uncertain, and a late observed application creates the normal
restoration obligation. The same rule retains an unresolved restore attempt
until the original value is observed. This pure model has no timeout that
silently converts uncertain attempts into success; a future bounded executor
must report unresolved cleanup and keep its journal for explicit reconciliation.

A restoration proposal is not restoration success. A newer verified observation
must show the captured original. An unobserved restoration remains uncertain or
incomplete without automatic retry. A conflicting value blocks that step and
the remaining restoration sequence until it is resolved from fresh observations.

## User takeover and status

`desktopAvailable = false` means no proposals or dispatch. Observations may
still reconcile an already attempted change, but restoration waits. There is
no focus acquisition or attempt to compete with the user. Calling
`RequestEnd` during takeover records the obligation instead of forcing cleanup.

Statuses distinguish:

- **Ready:** all requested values were observed; this is not session cleanup.
- **AwaitingDispatch:** a single proposal is available but has not been applied.
- **Uncertain / Pending:** observation or outcome still needs reconciliation,
  including an attempted change whose value is still unchanged.
- **Conflict / Incomplete:** a value changed externally or an attempted change
  was not verified; the model does not force or retry it.
- **Paused:** desktop availability blocks the next action.
- **Restored:** all changes attributed to this session have been observed back
  at their originals, or no changes required restoration.
- **Rejected:** no valid baseline transaction was established.

“Restored” does not mean every setting in the game equals a default, or that
the user has not subsequently changed an untouched setting. No status can
replace a current observation in a future session.

## Integration still required

The model is generic calculation and transaction state, not a complete executor.
The following remain separate implementation work:

- A qualified game-specific UI reader/executor, including reacquisition after
  resolution, window-mode or UI-scale changes.
- Shared input ownership, cancellation handling and actual desktop-activity/
  focus checks immediately before any input.
- Binding the transaction to the selected game/window, pack version and
  calibration profile. A caller-supplied session ID is not that binding.
- A durable journal that persists baseline, intent and observations before
  dispatch, survives crashes, and never starts restoration blindly on restart.
- User-visible pending/conflict recovery and an explicit choice if a failed
  attempt should be retried through a newly reviewed transaction.

The controller must preserve and advance one latest state; this in-memory
function cannot stop someone from replaying an old copied state or enforce
cross-process exclusion. It does not persist anything, schedule background
cleanup or guarantee restoration after a crash.

## Tests

[GameSessionSettingsTests.cs](../ProjectMaelstrom/ProjectMaelstrom.Tests/GameSessionSettingsTests.cs)
covers normal apply/restore, already-correct settings, invalid baselines,
allowlist restrictions, stable proposals, unverified dispatch, no automatic
retry, external changes, takeover, cancellation, manual restoration, incomplete
restoration and reverse order. All values and observations are synthetic.
Build and test execution are coordinated by the main project workstream.

