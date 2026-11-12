# Countdown release and installed recovery — 2026-11-12

Installed portable payload Maelstrom-20261112-130237-539646 using Setup-20261112-130527-259153. Receipt active directory: versions/c7dcf91657954b6da585474e70499538 under the per-user Maelstrom program folder.

Original setup UI could be read but Windows rejected activation; no successful GUI installation click. Closed only that task-launched idle setup process. Added explicit --install-cli entry using the existing InstallEngine transaction, preserving shortcut preference, app-closed checks, rollback and independent data directory. No manual application-file copying or registry manipulation bypass.

Validation:14 installer tests passed. Embedded payload lifecycle verification passed install/update/uninstall and all pack/report/recording/audio/experiment/routine/operation-history preservation sentinels. Installer formatting/analyzers passed. Prior application suite2392 tests passed. CLI installation reports installed:true. No app-control window remains active.

Installed recovery evidence: .local/w101-cycle-06/installed-recovery-20.json. Windows rejected the verifier foreground request; its ten-second clickable fallback timed out. Recovery remains blocked. W101 target confirmed PID99364/HWND3410314/start639301000055010501. No game input sent. Countdown rendering, audible sound, adapter qualification, waypoint and native Idle remain unverified. Do not claim a completed live test or assume the existing in-game state.

Next gate: foreground the recovery verifier through a user click / normal Maelstrom recovery screen, complete verified cleanup, then observe the adapter before authorizing the bounded waypoint. No bypass of the global warning. Sound output must be verified audibly or through appropriate scoped audio evidence; dispatch alone is insufficient.
