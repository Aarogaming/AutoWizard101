# Quiet W101 foreground admission — 2026-11-12

User explicitly requested automatic foreground admission when not using the
desktop, followed by bounded dorm waypoint testing and native Idle.

Implemented opt-in `navigation-visit --activate-when-quiet true`. Admission uses
the existing shared control scope and activity monitor: five quiet seconds,
original activity revision, no held keys, exact target and geometry, healthy
heartbeat and owner. One SetForegroundWindow request, up to one second to verify.
No synthetic activation keys, thread-input attachment, forced focus, minimized
window restoration or takeover reset. Default remains disabled. Logs attempted,
accepted and final bounded readiness diagnostics. Existing Idle finalization
remains subject to current authority; activation only occurs on initial admission.

Focused68native tests pass. Full suite/scoped checks running. Restarted game client
was not present at the first process-only check; old identity was not reused.
Live activation and dorm waypoint outcome remain pending.

Full suite:2381native tests passed, scoped formatting/analyzers passed. Added separate CLI boolean/scope regression afterward. Two process-only checks found no W101 game client. Waiting for restarted dorm sign-in; no desktop control or input active.

## Live checks after restart

Fresh target: PID99364, HWND3410314, start639301000055010501. No stale identity
reuse. Attempts d8a9ad60c88c40e888ace9a8b10f2be9 and
c89462cbcfac402b9b3413f386e4575d rejected held input before dispatch.
After the user confirmed typing ended,7709ba29b50144efa9f6b8f265a55967 reached
control admission (W101 already foreground), then rejected the changed client
before UI hook installation. Zero movement actions; no waypoint or Idle result.
SetForegroundWindow's success is still not live-qualified: this attempt did not
need an activation call.

New executable SHA256:
d410a2463e7ff0e221d6d11ca10fa53de265abd17cacfba211f9eeca8ec8511a.
Read-only disk review: root signature1match, entry498b8fd8000000(R15) rather
than reviewed R13; reviewed4100-byte scratch signature0matches; archived3900-byte
scratch signature1match; player signature1match. This is a candidate archived
profile, not authorization to replace registers/offsets or enable the adapter.
No installed file was written and no network was used.

The reader returned unavailable/client-build-unreviewed with cleanupConfirmed
and hookRestored true. An outer blanket catch incorrectly latched recovery.
Repaired this using a typed trusted unavailable-after-cleanup exception only
after checking the bounded result's identity, status and cleanup flags. Other
exceptions still latch recovery. Two regressions verify this distinction.
Existing recovery state was NOT silently cleared. Verified recovery remains
required before further control.

Added native build compatibility preflight to UI-based adapter preparation,
before scheduler/control admission. Unknown builds now fail without opening
hooks or changing focus; Python repeats its independent image guard. This is
auto-detection/rejection, not completed automatic adaptation. Future attachment
profiles must include root register/instruction, scratch strategy, field layouts,
source provenance, independent qualification and rollback. Never just whitelist
a new hash.

Final offline checks:2385native tests passed, scoped formatting/analyzers pass.
Live waypoint and native Idle are blocked by changed adapter compatibility and
pending verified recovery. No controller, input or assistant desktop control
remains active. No installation was performed.
