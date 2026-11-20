# Installed recovery workflow check — 2026-11-10

The installed Maelstrom recovery workflow **passed one real desktop check**:
Settings → Restore game controls → Stop routines → Check cleanup → Restore
controls. The dialog reported **Game controls restored**. Maelstrom was closed
afterward; no routine or game was started.

## Scope and timing

The user authorized this check after a half-hour desktop pause. No desktop tools
were used before the agreed 17:43:08 UTC gate. The check began after 17:43:21 UTC
and all Maelstrom windows were confirmed closed by 17:45:03 UTC, an upper bound
of 102 seconds including window discovery and closing. This slightly exceeded
the initial 90-second target. Future checks should reserve closing time within
the budget rather than count it as extra work.

Only the installed Maelstrom UI was operated. No game window, game inputs,
capture, audio, model, W101 telemetry reader, installer or build was used.
The user's W101 read-only observation exception is a separate, default-off
architectural decision; it does not change this test's scope.

The installed `ProjectMaelstrom.dll` SHA-256 matched the tested release:
`797878d25b719b4bbd126c7baf7b193d99ae97ba8862d8d9c3789c0971404608`.
Its directory is `versions/d1406a367c444f778395a30a855c9113` under the per-user
Maelstrom installation. No repackaging was needed.

## Observed behavior

| Stage | Observed result |
| --- | --- |
| Open recovery | Check cleanup and Restore controls disabled. Opening alone did not start recovery. |
| Stop routines | Routines stopped; Check cleanup enabled; Restore controls still disabled. |
| Check cleanup | Checking cleanup, then Ready to restore controls; Restore enabled. |
| Restore controls | New cleanup verification, then Game controls restored. All three recovery actions disabled on completion. |
| Close | Recovery and Settings closed without saving settings; main Maelstrom window closed. No Maelstrom windows remained. |

Screenshots and accessibility observations in the session support these UI
results. Immediate post-click accessibility snapshots sometimes retained the
previous heading while the screenshot had already changed; final states were
reobserved before choosing the next action.

## Independent saved evidence

The transaction created one attempt:
`d6ba245a1fb349a788197a9cbb85e51a`, beginning at 17:44:41.811 UTC.
Its two release samples were **1,016 ms apart**, had the same challenge and
activity revision, and each reported stopped workers, an available desktop,
known input state and released inputs. The verification record correctly says
`verified-for-commit`; it alone is not a completion receipt.

Completion is supported separately by the observed Restored UI and a subsequent
read of the authoritative guard while holding its existing byte-zero OS lock.
The guard contained ASCII `0` (clean). No manual guard edit or fabricated release
sample was used. The preserved original guard matched the preflight SHA-256.

The preflight guard was an empty legacy-clean file and no warning existed.
This therefore qualifies the real UI, verifier, evidence transaction and clean
commit path. It **does not qualify live recovery from a dirty guard, held key,
failed storage, busy external controller or running game job**. Those branches
retain the [offline coverage and limits](INPUT_RECOVERY_FLOW_20261110.md).

The operations directory remained absent before and after the check: no
scheduled routine was created or resumed. General settings and pet-training
settings retained identical file metadata and SHA-256 hashes. No warning was
created. No game outcome or native-Idle state is inferred from this check.

## Artifacts and next gate

Local evidence lives in `.local/input-recovery-live-20261110/`: `scope.json`,
`snapshot.ps1`, `before.json`, `after.json`, `desktop-result.json` and
`verification-summary.json`. The actual application-owned attempt files remain
under the user's Maelstrom `session-locks/recovery-history/` directory. They were
read without modification. The summary records 15 passing consistency checks.

This documentation-only increment requires no new native build. The board and
its 22 regression tests pass; scoped whitespace checks and PowerShell parsing
pass. The prior installed release's native and installer tests are recorded in
[the installation report](INPUT_RECOVERY_INSTALL_20261110.md), not rerun here.

The installed recovery check is complete. Shared modules still need their
separately scoped game qualification, particularly the full Dance postgame-to-Idle
path and navigation/sigil evidence. No game workstream is marked complete by
this recovery result.
