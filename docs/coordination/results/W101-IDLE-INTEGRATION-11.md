# Idle fail-safe integration across W101 routines

Source integration, November 11. Offline only; no live input or installation.

## Coverage

| Entry point | Integration |
| --- | --- |
| Navigation visit CLI | Existing shared fail-safe, bounded retries and optional quiet recovery. |
| Quest conversation CLI and Quest card | Production preparation wraps the complete conversation with the same-owner finalizer. A 45-second reserve surrounds the existing body budget. Dialogue still open is not forcibly advanced to reach Idle. |
| Admitted menu/sigil/native-Idle modules | Production admission wraps the complete module; its low-level compiler/executor remains composable without an Idle step after every primitive. Availability/qualification gates remain unchanged. |
| Pet Dance visual and sound visits | Existing visual result/no-feed/menu/Idle controller now runs as bounded shared fail-safe attempts. Each attempt has separate evidence and the original gameplay activity baseline. No client-observation dependency added. |
| Dance launcher/default options | Return-to-Idle defaults true. Explicit saved false preferences emit `--leave-awake`; conflicting/duplicate options fail. No saved preference is rewritten. |
| Raw wake/navigation probes and one combat turn | Diagnostic or nested primitives, not complete routines; intentionally do not silently add Idle between actions. |
| Combat battle, farming, navigation route and other unavailable cards | No qualified live host binding is enabled by this change. Complete future routines can use `W101RoutineIdle.Wrap`; do not treat their placeholder capabilities as working routines. |

`W101RoutineIdle.Wrap` establishes its control scope before the routine body,
retains the scheduler lease and adapter pins, waits for the body to return with
cleanup evidence, and calls the shared finalizer. It never replays the body.
Successful Idle cannot convert failed gameplay into success. Unverified Idle
prevents an otherwise successful wrapped routine from reporting full success.
Body/finalizer failures and reports are preserved separately.

Cancellation, takeover, identity/geometry changes and uncertain cleanup block
further inputs. Exceptions without a cleanup receipt require recovery. Quest and
module finalizers currently use the default no-resume policy; the explicit quiet
option remains qualified only in synthetic navigation-visit tests. Dance retains
its existing visual activity monitor and never resets that gameplay baseline.

The user requested offline work. No game observation, desktop control, model,
installer, game restart or live test occurred. Live checks require an attention
notification and the user's Ready reply before running them.

Verification: **2,349 native tests passed**, zero failures/skips; warnings-as-errors
build, scoped whitespace verification and analyzer checks passed. Synthetic tests
cover same owner, one body invocation, separate attempt evidence, retry exhaustion,
body failure preservation, cancellation, takeover, escaping exceptions and option
round-tripping. Project-board validation also passed.

Remaining migration: the older optional HomeMovementRun uses IHomeMovementRuntime
and HomeMovementLease rather than OperationContext. It has not been silently
wrapped with a second owner. It needs an explicit shared-owner bridge before this
finalizer can be used there. Existing movement stop/pause/release behavior remains.
