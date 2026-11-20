# W101 navigation stream and typed arrival — tasks111–114

## Delivered source
- Moving-pose decoder now shares bounded window and constant-retention stream logic. Stream hard caps1200samples/60seconds; current CLI qualification surface explicitly limits observation streams to1–3seconds. Callbacks apply backpressure; per-sample context refresh, cancellation, terminal invalidation and outer worker cleanup remain distinct.
- Existing optional UI observer serializes its PlayerHook and checks target lifetime, export, loading and candidate zone around samples. It emits JSONL candidate samples, not qualified gameplay facts. Moving trials use the same scheduler/input owner and existing countdown/Idle visit.
- Canonical heading conversion and signed turn bindings are explicit and tested against saved forward displacement. Stream envelope decoding conservatively maps declared age/delivery/clock uncertainty onto host time, with stale/future rejection. It remains candidate-only; lifetime/epoch/clock-discontinuity qualification is not complete.
- Typed coordinate, named-prompt and zone-transition arrival checks reuse LoadingTransitionGate. Portal requires observed post-dispatch loading, released input with a release timestamp, expected zone/new epoch and two distinct fresh observations. Same-zone transitions require a new epoch. Two matching zones do not prove stationary localization.
- Atlas can represent verified open portals without inventing an X prompt; existing corridor/connector evidence gates remain. Session ends old follower on loading, verifies after release and rechecks ownership after awaited callbacks. Cancelled callbacks get cancellation and bounded cleanup joining; unconfirmed cleanup blocks gameplay.

## Live evidence (before final timestamp/readiness refinements)
Data root: `.local/dance-integration-82-5e383f93418f4831a35b5e16bb1a4e0c/data/operations/`.
- `dcd14789edf94b09b0737888163cf7d3`: observation-only24sample stream, Commons candidate zone, stationary pose, nativeIdle observed; hook restoration/disconnect confirmed.
- `ac981e2cb92f4077b8fb689f58840005`: countdown, nominal100ms forward,24samples covering movement and settling, final nativeIdle verified.120pose reads/1152bytes excludes context fences. Receipt records routeQualified=false, arrivalVerified=false.
- Earlier three-window calibration remains in task110. These are bounded movement probes, not an autonomous Commons-to-Dance route.

## Review changes
Internal review found readiness preceding latest context fences, missing terminal stream notification after final-fence failure, and detached arrival callback risks. Source repairs and synthetic lifecycle tests were added. Sampling timestamps now use perf_counter consistently after coarse Windows monotonic timestamps were observed; old zero intra-read times are not zero latency.
Argon's numerical conversion was recomputed; actual single-pose angular error6.27405e-7rad. Its minimum-transit estimator does not establish an upper age bound. Impulse response is not steady velocity. See the bounded return brief in tasks/ARGON-NAV-REVIEW-112.md.

## Outstanding qualification
Production persistent follower feed with verified target/wizard/transition epochs and clock envelope; held-out heading/movement-profile calibration; current-start connector and corridor qualification; actual portal-zone arrival binding; final Dance prompt/entry. Installed release unchanged. Do not promote candidate graph edges or near-tent coordinates into a qualified route.

Validation and final live recheck recorded below as they finish.

Offline verification checkpoint: complete native suite2941/2941 passed (36s test duration), warnings-as-errors compilation; scoped C# whitespace verification and analyzers passed. Pure moving sampler28tests and fake UiClient stream lifecycle10tests passed. UI observer83tests passed before final timestamp refinement; final rerun tracked next. Heading baseline derived directly from saved receipt with SHA256 in .local/navigation-stream-112-heading-baseline.json.

Final offline checks:83UI tests and10owner-stream lifecycle tests passed on current worker; Black check for all5changed Python files and compileall passed. WorkerSHA cf3499ee408d518ebddfc36516ae658df58caaa412bb85e624c0fd7fbb311cf3. Final high-resolution moving recheck5ee1df3fd3c64d219b50141dc27663b5 currently waiting before input: normal foreground request rejected (target valid/nonminimized, another foreground HWND). Do not claim current-source live qualification until its receipt completes.

Current-start planning check: vertex89 is about1062.14XYunits from latest trial endpoint, versus219.776 for the historical binding pose. No current-start connector qualification inferred.

Final live recheck5ee1df3fd3c64d219b50141dc27663b5 completed: initial foreground rejection paused before input; owner restored foreground, SAME run resumed withcountdown, shortleftturn and24stream samples. Current perf_counter timings show submillisecond sample acquisition rather than coarse zero intervals. Hookrestoration/disconnect and finalnativeIdle verified. This qualifies the bounded stream trial only, not persistent autonomous route following. All root controllers finished; desktop CUA was never acquired.

Heartbeatfollowup: streamended/invalidatedeventsrevokehandshake; malformedcompleteeventsfailclosed.9focusedtests, scopedformat/analyzers andwarnings-as-errorsbuildpassed. Offlineonly; notinstalled.
