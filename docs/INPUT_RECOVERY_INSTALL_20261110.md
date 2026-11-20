# Recovery update installation — 2026-11-10

Status: **packaged and installed; interactive recovery verification incomplete**.
The user stopped Computer Use with physical Escape while using the computer.
No further desktop calls followed that stop. The user subsequently requested no
desktop use for about half an hour; the recorded earliest time is
2026-11-10 17:43:08 UTC (12:43:08 America/New_York). This is a restriction, not a
scheduled automatic takeover. No game routine was started.

## Delivered package

- Portable directory: `.local/releases/Maelstrom-20261110-120816-1148bf/`.
- Portable ZIP: `.local/releases/Maelstrom-20261110-120816-1148bf.zip`.
- Installer: `.local/releases/Setup-20261110-120905-0d8781/MaelstromSetup.exe`.
- Installed application directory:
  `%LOCALAPPDATA%/Programs/Maelstrom/versions/d1406a367c444f778395a30a855c9113/`.
- Installer version label remains `0.2.0-preview`; use directory/hash identity to
  distinguish this build from earlier previews.

| Artifact | SHA-256 |
| --- | --- |
| Portable ZIP | `04d66b532311b028a85499618b2a157c07d5585fbd4e04b88ba7aae15ee84307` |
| Installer | `67dbe958fe11fc438bb6b4e888a6c0c2aa226f7bd99ab814c8385ab70f6b7e2b` |
| Application DLL | `797878d25b719b4bbd126c7baf7b193d99ae97ba8862d8d9c3789c0971404608` |

Installed EXE and DLL hashes match the portable build. Setup removed the portable
marker, preserving the ordinary separate installed data location. The 14 source
and test files match the already validated recovery checkpoint; no application
code changed during packaging. Builds used two processors, existing runtimes and
no dependency restore/downloads. Existing release folders were retained.

## Checks and observed outcome

| Check | Result |
| --- | --- |
| Prior source checkpoint | 1,507 native tests and 62 Python/board checks passed; source hashes unchanged. |
| Installer suite | 14 passed, 0 failed, 0 skipped; warnings as errors. |
| Portable archive | ZIP integrity check passed; required application/runtime/Pet Dance preset files present. |
| Embedded-payload lifecycle | Actual isolated install → update → uninstall passed with separate temporary data, shortcuts and registry locations. |
| User installation | Setup reported ready; installed receipt identifies the new directory; EXE/DLL hashes independently match. |
| Existing user data | All 972 pre-existing files retained with unchanged sizes/timestamps; 104 bounded text/config files also retained identical content hashes. |
| Installed startup | Maelstrom's ordinary home screen opened. |
| Real recovery verifier | **Not run.** Recovery dialog was not reached before user takeover. |
| Gameplay | No game selected, no routine started and no game inputs sent. |

The data comparison covered 1,751,431,624 bytes by inventory. Large recordings were
checked by size and modification time rather than rehashing their contents, to
keep disk and CPU use light. No existing file was missing or changed by those
checks. The lifecycle test separately checks preservation of packs, reports,
recordings, video/audio and timing, experiments/decisions/footage, home movement,
routines, idle choices and operation history using temporary sentinels.

On its first UI observation, Setup was already updating; a later observation
reported the application ready. This report does not infer who initiated that
button action. Opening Maelstrom succeeded. A Settings click reported unavailable
input geometry; fresh observations showed only the home window and Settings below
its current viewport. After refreshing the window, the attempted scroll was
stopped by the user's physical Escape. Maelstrom was left open, with no further
UI actions or alternate input path used after that stop.

## Remaining qualification

When desktop use is permitted again, verify **Settings → Restore game controls**
with no gameplay. Observe Stop routines → Check cleanup → Restore controls,
inspect the saved verification record and authoritative guard, and confirm no
routine restarts. Preserve existing recovery evidence. Stop immediately on user
takeover; do not use a backend path to bypass it. Existing offline failure/race
coverage does not substitute for this remaining real-verifier check.

The source flow and its boundaries remain documented in
[Verified input recovery](INPUT_RECOVERY_FLOW_20261110.md). This installation does
not qualify navigation, game cleanup/Idle, or minigame reliability.

## Evidence

Local artifacts: `.local/input-recovery-install-20261110/` contains
`installer-tests.trx`, build logs, `installer-lifecycle.json`,
`package-validation.json`, `installation-before.json`, `installation-after.json`,
`installed-verification.json`, `data-before.json`, `data-preservation.json`,
`desktop-result.json`, and board validation results. Full data inventories remain
local; this report summarizes their comparisons without copying private content.
