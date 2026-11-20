# Pose ownership and Olde Town movement — 135

2026-11-15. Registry endpoint ownership verified live; one bounded teaching movement completed; route and live loading-transition qualification remain pending.

## Change

Extended the existing guarded identity lookup with optional expected_owner equality. A valid stats pointer alone cannot satisfy that comparison. The pose reader compares its actor parent directly with the registry-selected local wizard owner before and after its observation window, reusing existing instruction, registry and mutation guards. Results retain opaque character identity, read budgets and endpoint ownership status; no raw pointers are exported. The zone remains candidate-layout evidence, now associated with the verified owner at those endpoints. This is not an uninterrupted identity/epoch guarantee and does not turn the autonomous follower's qualification flag on.

## Offline validation

14 identity tests,10 pose-owner lifecycle tests,83 UI-reader tests passed.70 focused native reader/identity tests and85 native navigation/control/loading lifecycle tests passed before live movement. New test explicitly rejects a different expected owner. Formatting and Python syntax checks passed.

## Live observation

Run d1d444c4bfad47319c093b5b9c3702b3, .local/oldetown-owner-135.json. Owner matched the local registry at both endpoints; same opaque wizard key and stats owner. Candidate zone WizardCity/WC_Streets/WC_OldeTown.58 identity reads,596 identity bytes for the pair. CleanupConfirmed/HookRestored=true, nativeIdle=true, loading=false. No inputs or focus changes in this observation.

## Bounded movement

Run00174137f9094e508246b23f2e3c6087, .local/oldetown-movement-135.json. Existing navigation-visit scheduler/control scope requested a countdown then one W60ms teaching pulse, before/after readings and native Idle cleanup. Receipt Completed: teaching-visit-collected-and-native-idle-observed.

Before XYZ=(1644.1544189453125,253.1980743408203,2.199981689453125).
After XYZ=(1607.2025146484375,257.4874572753906,2.199920654296875).
Planar displacement37.200027380699495. Same wizard owner verified in both measurement windows; no coordinate-space change. This is actual measured displacement, not inferred input success. Do not divide by nominal60ms to claim steady-state speed or calibrated latency. No waypoint/route arrival qualification.

Countdown placement artifact targets HWND945359296; visible=true/soundRequested=true but desktopVisibilityConfirmed=false. No assistant desktop control was acquired; no independent user visual confirmation is claimed. Final routine verified native Idle.

## Remaining work

No actual loading transition occurred. Loading fault tests passed offline, but cross/same-zone live re-localization remains pending. Continuous wizard/zone epoch binding still needs integration into the streaming fence; endpoint checks alone cannot exclude an intervening identity change. Full focus/displacement/replan replay remains pending. Commons-to-Pet-Dance not qualified.

The movement's non-streaming windows showed quantized zero-duration reads. After the live run, explicitly passed perf_counter to the non-streaming reader too.10 owner tests reran successfully; this clock change has not had another live check. Keep its source revision distinct from the movement receipt.

Gemini125 remains report-only; next review LIA-0086 checks continuous epoch/identity propagation and bounded portal-test preparation. No install or commit.
