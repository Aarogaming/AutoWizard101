# W101 profile and verified recovery — 2026-11-12

Status: offline implementation verified; live qualification blocked.

Exact executable d410a2463e7ff0e221d6d11ca10fa53de265abd17cacfba211f9eeca8ec8511a selects the archived R15 seven-byte root entry and 3900-byte scratch profile. Prior 544ab profile retains R13/4100. Unknown images remain rejected. Archived text fields remain candidate-only, and combat decoding is unavailable. No installed game files were modified.

Added explicit CLI recover-input using the existing production stop/check/restore workflow and global shared owner. Five quiet seconds precede its brief verification window, with a 25-second cancellation budget. It cannot clear recovery without fresh Windows checks and does not restart any routine. Blocked recovery exits 2; restored exits 0.

Offline evidence: 2386 native tests and 56 synthetic UI adapter tests passed. Scoped formatting/analyzers and Python formatting/syntax checked. Synthetic profiles and restoration coverage do not prove the new client works live.

Live recovery evidence: .local/w101-cycle-06/recovery-17.json. Stage Blocked: Windows could not confirm the active desktop. Existing recovery warning retained; helper PID51184 exited, verification window closed. No game input was sent. WizardGraphicalClient was absent at preflight; no restart attempted. Consequently new-build live UI/pose/restoration, dorm waypoint arrival and native Idle remain unverified. No installer run.

Next: with W101 signed into the dorm and an available unlocked desktop, rerun explicit recovery, then observation-only profile qualification and verified disconnect. Only after those pass run one bounded waypoint and verify native Idle. No user play or watching is required.
