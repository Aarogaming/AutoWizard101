# W101-COMBAT-INPUT-07 — bounded addressed combat clicks

Date: 2026-11-11. Workstream: `W101-COMBAT`. Owner: `combat_input`.

## Status and scope

Implemented for coordinator review and serialized offline validation. **No live
W101 mouse-input qualification and no combat outcome evidence were produced by
this task.** No game, desktop, capture, model or process-observation job ran.
The coordinator owns integration, native checks and any separately authorized
live check. No production dependency was added.

Owned changes:

- `ProjectMaelstrom/ProjectMaelstrom/Utilities/W101CombatInput.cs`
- `ProjectMaelstrom/ProjectMaelstrom.Tests/W101CombatInputTests.cs`
- This result report.

## Contract and integration

`W101CombatInput.ForAddressedWindow(target, geometryId, clientSize, clock)` creates
an input adapter bound to one exact W101 process/window, opaque observation
geometry ID and admitted client raster size. Its `DispatchAsync(proposal,
context, ready, token)` matches the combat-turn dispatch contract. The caller
supplies the existing turn executor's freshness/activity/loading gate and
qualified **normalized client bounds**, not raw W101 UI-layout units.

The adapter accepts only enabled card, pass, or living enemy/self controls with
their corresponding registered combat action. It rejects different geometry,
changed client size, incorrect scheduler target, read-only/delegated ownership,
missing current lease, replayed proposals and concurrent use. The instance has
a bounded 128-proposal budget; create it for the bounded battle/operation, not
as a permanent global service.

Normalized control centers map into `[0,width) × [0,height)`; messages use signed
16-bit client coordinates and therefore reject sizes exceeding 32768 on either
axis. The production path sends only `WM_LBUTTONDOWN`/`WM_LBUTTONUP`, with a
40 ms hold and 200 ms per-message timeout using
`SMTO_BLOCK | SMTO_ABORTIFHUNG | SMTO_ERRORONEXIT`. It never moves the desktop
cursor, changes foreground focus, installs a hook or sends global mouse input.

Readiness is rechecked immediately before the down attempt. Every attempted
down, including timeout/exception, gets a release attempt to the original
current target. Release ignores cancellation, changed readiness and changed
size; it never goes to a replaced/destroyed target. Missing or ambiguous release
latches the existing input recovery guard, even if saving its warning fails.
The adapter returns rejection when the press was not admitted, readiness was
lost, or release is uncertain. A successful message receipt still proves only
transport delivery, not a game action; the existing turn outcome observer must
verify the resulting game state.

## Offline cases and checks

Synthetic runtime seams cover:

- Client-coordinate mapping, raster boundaries, nonfinite/out-of-range bounds
  and coordinate packing limits.
- Incorrect ownership, target, geometry, action and control role; disabled or
  dead/unknown target controls; cancellation before press.
- Readiness recheck, duplicate suppression and concurrent rejection.
- Down timeout/exception; cancellation in send/hold; user takeover; resize during
  hold; hold failure. Release remains required in all attempted-down cases.
- Release timeout/exception, target replacement, and failed warning persistence;
  further gameplay remains blocked.
- Card, pass, enemy and self actions retain matching proposal/action receipts.

Source-only `git diff --check` completed without output (the new source files
are untracked, so this is **not** a formatter or compiler verification).
Native formatting, analyzer checks, compilation and execution were deliberately
left to the coordinator's single build slot. Relevant filter:
`FullyQualifiedName~W101CombatInputTests`.

## Remaining limits

The reviewed legacy mouse handler also uses a synthetic cursor hook. This
adapter deliberately implements only addressed coordinate messages; W101 may
ignore them or use the current cursor position instead. A bounded live test
must establish the actual game response and qualify normalized geometry before
this becomes an enabled gameplay capability. Do not infer that the working
Space dialogue adapter proves mouse clicks work.

Process identity/size checks and message submission are not an atomic OS
transaction. The existing outer observer must detect geometry/state changes
and reject the next action. This adapter does not acquire a second controller,
resolve game controls, infer input acceptance, enter battles, restore native
Idle, or implement a foreground fallback. Those responsibilities remain with
the coordinator and shared runtime components.
