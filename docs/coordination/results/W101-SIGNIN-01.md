# W101-SIGNIN-01 — optional local sign-in storage

Date: 2026-11-11. Workstream: `CORE-DELIVERY`. Owner: `combat_input`.

## Delivery

Implemented source for an optional Wizard101 credential settings dialog and
per-Windows-user Credential Manager store, ready for the coordinator's settings
link and serialized validation. **Automatic game login is not implemented or
qualified.** This task did not open the dialog, access the real Windows vault,
read credentials, run native tests/builds, launch a process or use the desktop.
No dependencies were added.

Owned paths:

- `ProjectMaelstrom/ProjectMaelstrom/Utilities/GameCredentialStore.cs`
- `ProjectMaelstrom/ProjectMaelstrom/GameSignInForm.cs`
- `ProjectMaelstrom/ProjectMaelstrom.Tests/GameCredentialStoreTests.cs`
- `docs/W101_SIGN_IN.md`
- This result report.

## Behavior

`new GameSignInForm()` opens Wizard101 setup; the coordinator owns adding that
entry point to `ResearchSettingsForm`. Username/password fields start empty,
the password is masked, Save/Remove are explicit, and fields clear afterward or
on close. Fixed error messages never echo usernames, passwords or provider
exception details. The dialog explains that saving does not enable login.

`GameCredentialStore.Windows()` supplies the native runtime.
`GetStatus(GameCredentialProfile.Wizard101)` returns Missing, Stored or
Unavailable; `Save(profile, username, ReadOnlySpan<char>)` and `Remove(profile)`
operate only on the app/game-specific credential target. There is no retrieval,
export, environment-file or command-line secret interface.

New secrets are written as UTF-16 directly into temporary unmanaged buffers;
secret and username buffers are zeroed before release on success and failure.
Existence checks use exact-target enumeration and do not dereference username
or copy password bytes into managed memory. Bounded native blob copies are
cleared before returning their allocation to Windows. The store uses Windows
generic credentials with local-machine persistence for the current user.
The game profile schema is reusable; only the W101 dialog is supplied now.

## Verification status

Synthetic native-seam tests were written for local-user persistence and exact
target, UTF-16 content, cleanup after write/native/cleanup errors, sanitized
exceptions, invalid fields and limits, profile namespace restrictions, missing
vs unavailable status, no username reads during existence checks, cleared
enumeration copies, and exact/idempotent removal.

The coordinator retains the sole native build slot. Formatter, analyzer, build
and test results are **pending**, not claimed by this report. Relevant test
filter: `FullyQualifiedName~GameCredentialStoreTests`. Unit tests supply inert
buffers and injected delegates; none calls real Credential Manager APIs.

Remaining: coordinator settings integration, offline checks, UI qualification,
packaging/installation if assigned, and a separately reviewed/qualified bounded
auto-submit adapter. WinForms entry strings cannot be guaranteed to disappear
immediately from managed memory; no stronger erasure claim is made. See
`docs/W101_SIGN_IN.md` for the user flow and future adapter gates.
