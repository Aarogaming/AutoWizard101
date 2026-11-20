# Shared operations release validation — 2026-11-08

Installed version: `0.2.0-preview.20261108-shared-operations`.
Implementation and current boundaries: [Shared operations](SHARED_OPERATIONS.md).

## Verified

- 343 application tests passed, including the scheduler and compatibility-input
  ownership tests. Coverage includes competing owners, cancellation during
  cleanup, queue cancellation, callback lock ordering, timeout distinction,
  changed inputs, duplicate/restart handling, finite capacity, failed-release
  recovery, focus loss and the two-second held-key limit.
- 36 Python checks passed, including native-to-Python and Python-to-native
  exclusion, owner-process termination and recovery-notice enforcement.
- 12 installer tests passed. The finished installer also completed an isolated
  install/update/uninstall cycle preserving packs, reports, recordings, video,
  experiments, routines, Idle preferences and operation receipts.
- Formatting, Python lint and build analyzers passed. Builds used one worker;
  no model was loaded and no game-playing input was sent by the test suite.
- The compiled teacher ran as `w101.pet-dance-review` through the common queue:
  153 synthetic frames, three exact varied sequences, 40/40 scale cases and zero
  sparse-recording proposals. Original labels were preserved. Its first check
  took 16.38 seconds wall time and 8.19 process CPU seconds. This is synthetic
  recorded replay, not live-game accuracy or reaction latency.
- Offscreen UI inspection showed the real saved teacher receipt in **Routine
  runs**, a disabled Stop for the completed job, and inactive live defaults.
- The update preserved the hashes of all 15 existing user JSON/JSONL/config
  documents checked by the update helper. Synthetic recordings and calibration
  stayed in the development evidence folder, outside the user's pack library.

## Brief live entry check

The selected W101 window initially displayed **YOU ARE IDLE**. Four bounded
desktop actions opened the minigame selector, opened settings, selected native
Idle, and closed the remaining selector. The final view again displayed **YOU
ARE IDLE**, with the selector closed. The desktop helper was then released.

The selector contained the eight mana minigames, not Pet Dance. Settings showed
1280 × 720, fullscreen Off and **Tiny UI**. No settings were changed and no game
round was played. This verifies the manual native Idle return in this check; it
does not qualify an automatic cleanup routine. Current Pet Dance scale, phase
recognition, actual action delivery and round outcomes remain open gates.

## Evidence and packages

Under `.local/scheduler-integration/`:

- `tests/final.trx`, `tests/installer.trx`
- `teacher-check-01/verification.json`, its `operations/` receipts and teacher pack
- `installed-check/verification.json`: the installed executable's repeat check
- `ui-02/routine-runs.png`, `ui-02/home-movement-ui.json`
- `installer-lifecycle.json`, `installed.json`, `w101-entry-check.json`

Portable: `.local/releases/Maelstrom-20261108-214444-f76b08.zip`.
Installer: `.local/releases/Setup-20261108-214620-814682/MaelstromSetup.exe`.
User data remains under `%LOCALAPPDATA%/Maelstrom`.

The next live gate is a current-game Pet Dance capture/calibration and verified
menu-based Idle cleanup. Periodic scheduling, routine dependency editing and
automatic recovery from unconfirmed key release are not delivered in this slice.
