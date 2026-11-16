# Production pose-stream connection — 134

2026-11-15. Production observation connected; navigation identity remains unqualified.

## Implemented

The native UI observer now supplies the existing worker's bounded trace callback. The scheduler generates a stream UUID before process launch; Python validates and echoes it rather than selecting the native consumer's identity. The consumer checks request/run/target/sequence and receipt freshness while the worker is alive. Loading or unknown loading, stream invalidation, malformed samples, owner loss and end terminate this consumer. Finalization invalidates it and writes pose-stream-consumption.json. No cached pose is exposed after end. The observation API cannot send input and does not grant movement authority.

The zone label in the first valid sample remains a candidate. Wizard identity and zone-owner qualification flags are deliberately false. A same-zone loading event that is observed terminates the stream; unobserved transitions between reads are not claimed detectable. Focus pause/resume and replan are not implemented by this observation-only boundary.

## Offline evidence

75 focused native tests passed (stream consumer, incremental trace transport, production observer, UI reader, worker). Python: 10 owner-lifecycle and 83 UI-reader tests passed. New fault cases cover excessive delivery delay, sequence loss, foreign stream, same/cross-zone loading followed by attempted revival, EOF and owner loss. These are synthetic fault tests using the earlier Olde Town pose values, not a complete recorded-session replay or proof of key release. Existing worker cleanup remains authoritative; no new movement/release implementation was introduced.

## Live evidence

Run e51c8860e846427ebbf0dcb7761e62c4; report .local/oldetown-observe-134.json. Existing reviewed optional hooks, no keys/clicks/focus changes. Target PID21044, process start639303092887288354, HWND945359296. Native consumer accepted24 records in real time and ended on moving-pose-ended. Candidate zone WizardCity/WC_Streets/WC_OldeTown. Current recorded XYZ=(1644.1544189453125,253.1980743408203,2.199981689453125), yaw1.6863594055175781. These supersede previous coordinates for this observation only.

Result observed; CleanupConfirmed=true, HookRestored=true, nativeIdle=true, Loading=false. SawLoading=false: this check does NOT qualify an actual loading transition. Observer exited and no desktop control was acquired.

## Remaining gates and next work

1. Existing tools/w101_identity.py establishes identity against a stats object; pose() instead follows actor+72 and labels its owner actor-parent-unqualified. Prove the relationship to the active registry owner before declaring wizard/zone identity verified. Reuse the guarded registry reader; do not substitute a process ID for a wizard ID.
2. Carry independently established subject/zone ownership and loading epoch through the native fence, with mutation fault tests.
3. Add recorded-session replay of focus pause/displacement/resume and observer timeout/key-release cleanup. Current observation tests do not satisfy these movement lifecycle cases.
4. Only then run bounded Olde Town movement with countdown and verified Idle cleanup. Movement was withheld in this pass because identity prerequisites remain unqualified. No route or collision role was promoted.

Gemini remains assigned report-only review via LIA-0083/0084. No installation or commit.
