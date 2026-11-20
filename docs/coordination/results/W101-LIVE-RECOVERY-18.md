# W101 CLI recovery qualification — 2026-11-12

Status: blocked at verified recovery, before adapter or gameplay.

Authorized scope: unattended recovery, new-profile observation/cleanup, one bounded dorm waypoint, native Idle. No client restart, volume change or unrelated input.

Corrected an earlier inference: restricted process enumeration showed no W101 process, but elevated live enumeration finds PID99364, HWND3410314, start639301000055010501. Thus prior absence was not established. Always use the live execution context for target identity checks.

Recovery attempts .local/w101-cycle-06/recovery-18.json and recovery-18b.json stopped before verification due to desktop activity. Six read-only aggregate samples subsequently showed unchanged activity and zero held controls. recovery-18c.json reached verification but could not confirm the active desktop.

Added one ordinary SetForegroundWindow request after the existing quiet gate and explicit visibility for the authorized verifier surface. No queue attachment, synthetic activation keys, forced focus or verification bypass. recovery-18d.json and recovery-18e.json specifically confirm that Windows rejected foreground admission. Both exited with controls still blocked. Helpers and verification windows closed; no game input was sent.

The global recovery warning remains intact. Live adapter, pose/restoration, waypoint and native Idle were not verified. W101 was not changed by these checks. A successful normal foreground recovery check is required before continuing. No installer was run.

Validation: focused111 tests passed; warnings-as-errors build passed. Final scoped formatting/analyzers and full native suite recorded below.

Final checks: 2386 native tests passed; scoped whitespace verification and analyzers passed; project board valid (30 workstreams).
