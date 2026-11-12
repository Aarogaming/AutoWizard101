# W101 optional telemetry decision and review — 2026-11-10

**Current decision, 2026-11-10:** the owner expanded the W101 exception to
reviewed client-side functionality, including necessary hooks, injection and
memory reads/writes. Network manipulation remains excluded; installed game files
stay read-only. See [the current decision](W101_CLIENT_SIDE_EXCEPTION_20261110.md).
The earlier scope below is historical; its technical findings remain evidence,
not a restriction overriding the owner's expanded approval. No adapter is made
operational merely by this documentation update.

## Earlier user decision

The user explicitly selected **"Approve future opt-in read-only telemetry"**,
then clarified that the pre-existing policy remains the default and W101 is the
exception. Record that as the owner's architectural decision, separate from any
technical assertions in Gemini's pasted memorandum.

- General game interaction remains UI-only by default.
- W101 may add optional read-only process observation as an auxiliary sensor.
  It remains disabled by default. Gameplay actions continue through the UI.
- Visual operation remains independently usable. Missing telemetry must not
  silently become an invented position/resource/combat observation.
- Memory writes, code injection/hooks, live traffic interception and game-client
  file changes remain outside this exception.
- This decision permits development/qualification of the optional capability;
  it does not enable a concrete reader, launch a legacy backend or broaden the
  current Maelstrom-only recovery test.

The user's rationale is their understanding that W101 lacks client-side
anti-cheat of concern. The supplied code/tests do not establish current client
protection details. Record no guarantees of invisibility, zero risk or exact
navigation precision. The existing [memory-observation research](W101_MEMORY_OBSERVATION_RESEARCH.md)
distinguishes OS access rights, publisher statements and unknown client behavior.
This review performed no process-memory access, driver probe or game inspection.

## What the code actually adds

Gemini's six files are present in the shared checkout. They add a default-off
native/Python policy switch, a telemetry record, a provider interface, a null
provider, a static service and mock-provider tests. There is no concrete memory
reader. Repository reference search found no planner consumer of the service.
The files were written at about 17:20 UTC, after the 17:08 UTC recovery package;
they are not part of that installed build and have not been repackaged here.

| Finding | Evidence and consequence | Next implementation requirement |
| --- | --- | --- |
| Stale data is accepted | `TryGetTelemetry` returns any non-null provider data; it does not inspect `TimestampUtc`. The memo's stale-data fallback is not implemented. | Inject a clock; reject default/future/expired timestamps; bind snapshots to a selected process instance, client build and schema. |
| Provider failures escape | `IsAvailable` and `GetTelemetry` run without exception handling. A throwing provider does not yield the promised null fallback. | Bound the provider boundary and return unavailable evidence on expected read/access errors; preserve diagnostic reasons. |
| Calls can block the caller | `GetTelemetry` is synchronous; “non-blocking” is only a comment. | Use a bounded sampler and latest valid snapshot, cancellation and resource limits; do not accumulate background requests. |
| Cached providers bypass later checks | `CurrentProvider` exposes the raw provider while allowed. A retained reference can still be called after the switch is disabled. | Expose gated observations rather than raw providers; tie admission/revocation to an immutable session context. |
| Unknown combat becomes false | `InCombat` is non-nullable with default false. NaN/infinite positions or inconsistent resource ranges are also accepted. | Represent unknown separately; validate finite coordinates, valid resource ranges and field provenance. |
| Native/Python switches are independent | Setting the native boolean does not configure the separate Python worker. A bare global boolean does not record scope, provenance or consent. | Define one versioned opt-in/session contract and adapter parity tests; retain default-off behavior. |
| No consumer integration | There are no service calls from HomeMovementRun, PetSnackPlanner or other planners. The memorandum's consumer wiring is future work. | Integrate one small consumer through a shared observation contract and test visual-only fallback. |
| Concurrent tests share mutable policy | Two test classes mutate the same global switch; no test-collection isolation was found. | Prefer instance-scoped policy/provider state or explicitly isolate tests and restore state. Passing one focused run does not prove race freedom. |

The boolean permits the trusted `wiztype` classification when enabled. That is
admission policy, not an OS restriction guaranteeing that an external program
only reads. Review complete startup/dependency/access paths before connecting a
concrete tool. Arbitrary plugin assemblies and other legacy client backends remain
blocked by their existing classifications.

## Independent verification

- **14 native policy/provider tests passed**, 0 skipped, 33 ms test duration;
  Release warnings-as-errors build, no restore, two processors.
- **8 Python policy tests passed**, 0 skipped, 0.009 s.
- All providers used by these checks are mocks/null providers; no game or desktop
  access occurred. These tests cover the implemented happy/default paths, not the
  missing stale/throwing/blocking/revocation/value-validation behavior above.
- No production files were changed by this review. The owner's working agreement
  and product/boundary documentation were updated to record the explicit exception.
- Evidence: `.local/w101-telemetry-review-20261110/telemetry-review.trx`,
  `native-policy.log` and `python-policy.log`.

Next bounded task: harden and test the provider contract with synthetic data
before implementing any live reader. Keep it separate from installed recovery
verification and avoid publishing the memorandum's proposed guarantees as
verified product behavior.
