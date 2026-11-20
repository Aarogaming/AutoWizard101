# Rust native bridge integration repair

Status: offline integration repair complete. Live host qualification and packaging remain separate.

## Reviewed baseline

Gemini handed back commit `1c5127c` and reported 55 tests. Source review found that its fixture used a different JSON shape than the native host. The pipe-name test checked only prefix/length, not hash parity. The IDE polling control changed a boolean without running requests. These reported checks therefore did not establish native integration.

Required repairs: native facts envelope and typed alias array, freshness and unavailable values, preserved subject/target/source, checked numeric conversion, missing schema rejection, exact supported pipe-path normalization, invalidation after transport failure, cancellation and bounded non-overlapping polling.

No game, host, capture, input, installation or model launch is authorized in this slice. Tests use fake streams and native-shaped data. Existing unrelated Rust migration deletions are preserved.

## Coordination

The local liaison received Gemini's first acknowledgements. Its older test grant was revoked when the user requested integration repair. Root paused source edits until GEM-0005 acknowledged the freeze and released the build slot. Bridge repair and IDE integration then resumed on disjoint paths. Channel messages retain the exact sequence.

## Verification

- `cargo test --offline --workspace -j 2 --quiet`: **70 passed**, zero failures. Includes 17 bridge protocol fixtures plus shared snapshot-result validation and IDE lifecycle tests.
- `cargo clippy --offline --workspace --all-targets -j 2 -- -D warnings`: passed. Cargo emitted filesystem incremental-cache hard-link fallback warnings; it copied files instead. No Clippy code findings.
- Scoped `rustfmt --check` on the five changed Rust files: passed.
- `python tools/check_project_board.py` and scoped `git diff --check`: passed.
- First focused run exposed a test-only Rust Drop/struct-update compilation error; repaired before all passing runs above.

## Implemented behavior

Only canonical, correctly typed, fresh aliases matching the snapshot source, target, game and timestamp supply current values. Raw evidence is retained separately without becoming a fallback. Source timestamps older than two seconds, future/malformed timestamps, missing schema versions, duplicate aliases and out-of-range values are rejected. Wizard identity compares target/profile/generation/character identity, excluding level and observation time. Disconnect invalidates every projected value. Both polling paths reject non-object snapshots; dropping asynchronous polling cannot leave the prior snapshot current.

The IDE observer now has a real single-flight read-only worker, explicit existing-host data folder, one-second minimum request interval, bounded cancellation, late-result rejection, and automatic stop when hidden/disconnected. Source-time expiry clears the display. Observation defaults off and cannot start the native host or game. Existing prototype attach/runner controls are visibly simulations, not claims of game control.

## Use and limits

In the Rust IDE telemetry pane, enter the data folder of an existing native deck host and select Observe. This integration has only fake-transport/offline evidence here; no real host was contacted. No installer was built or installed, no desktop control was taken, and W101 was not touched.

Ordinary ASCII Windows drive/UNC/relative paths have native hash fixtures; non-ASCII and device paths are explicitly unsupported pending parity qualification. Native UTC timestamps with zero through seven fractional digits are supported; other timestamp formats remain unknown. The in-tree SHA implementation was retained without a new dependency. A live named-pipe smoke check remains a separate qualification task.
