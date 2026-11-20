# Rust codec and Dance parity increment — 2026-11-12

Task: RUST-PARITY-31. Workstream: CORE-DELIVERY. Owner: root/rust_audit. Status: needs-integration-review. This is source and offline verification, not host substitution, installation or live qualification.

## Changes

Owned implementation paths:

- `crates/maelstrom-core/src/deck_encoder.rs`
- `crates/maelstrom-fallback/src/pet_dance.rs`
- This report.

The spell-deck codec now uses the native literal backslash-prefixed delimiters `\+`, `\,`, `\;`. Counts are signed i32, matching C# int; this changes the Rust public DeckDefinition map value type from u32. No production Rust consumer was identified in the audit. Decoder preserves name whitespace, ignores malformed/out-of-range entries as the reference does, retains first ASCII spelling with the last case-insensitive value, accepts embedded ASCII Base64 whitespace, and uses replacement decoding for invalid UTF-8 as native Encoding.UTF8 does. Deterministic sorted encoding remains.

An independently generated .NET ZLibStream fixture now checks decoding native wire data. A separate test decompresses Rust output and compares exact native delimiter text, including a negative count. These supplement existing same-language round trips and a case/whitespace/malformed-entry test; they do not claim complete interoperability for every Unicode/culture case.

Dance now follows the existing C# observation-only state machine: faults latch until a recognized reset boundary; held cues count once; neutral gaps or qualified fade troughs rearm repeated directions; ambiguous direction changes reject; final Go checks gaps/faults/completeness/final separation; registered `dance-*` proposals emit once; Loading discards stale cues without fabricating outcomes; repeated Success applies once and Failure preserves expected length. No input transport was added.

Six new Dance regression tests adapt the native teacher/fade test scenarios. One exhausts all 64 three-direction sequences and also checks repeated-cue lengths 4–7. Others cover eight evidence faults and recovery, repeated-arrow fade/short-neutral troughs, invalid fades, Success/Failure/Loading, missing start, premature Go, partial final fade, pre-Go gap and Resolving. Two original tests remain.

## Reference and fixture provenance

Native sources:

- `ProjectMaelstrom/ProjectMaelstrom/Utilities/DeckEncoder.cs`, SHA256 `C5BCCD312694B1EC6645E4DCD5296029954181AACD6696D4FC659E87DDDC5C9A`.
- `ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceSequence.cs`, SHA256 `366AEB31B65721C9A1993591C6D8E61D628386CD5F65FD02669C005D27BAE348`.
- `ProjectMaelstrom/ProjectMaelstrom.Tests/PetDanceTeacherTests.cs` and `PetDanceFadeTests.cs`: 50 ms sample cadence, held/repeated cues, evidence fault matrix, boundaries and fade-trough scenarios.

The fixed compressed fixture is `eJwLSc0tSC0uidE2idEJLskvyk3KSUxJjdE2irF2zklNzCtOVXDOSCzKjdE2jLH2AwCCWQ/m`. It was generated in PowerShell using `[Text.Encoding]::UTF8.GetBytes`, `[IO.Compression.ZLibStream]` with Optimal compression, and `[Convert]::ToBase64String`, from literal text `Tempest\+4\,Stormblade\+2\;Cleanse Charm\+1\;N`. This invokes the platform codec independently of the Rust implementation; it is not a call to the application DeckEncoder method. Rust output is asserted against exact native wire text rather than exact compressed bytes, because compression implementations need not produce identical streams.

All Dance inputs are synthetic labeled observations. No recorded pixels, perception model, screen capture or game outcome is claimed. The tests are grounded in native regression scenarios, not a side-by-side execution of every native test or an exhaustive state-machine proof.

## Actual checks

The coordinator granted an exclusive bounded offline Rust build slot after implementation. Commands ran sequentially with cached dependencies, without downloads or installation:

| Command | Result |
| --- | --- |
| `rustfmt --edition 2021 crates/maelstrom-core/src/deck_encoder.rs crates/maelstrom-fallback/src/pet_dance.rs` | Passed; only owned Rust files formatted. |
| `rustfmt --edition 2021 --check crates/maelstrom-core/src/deck_encoder.rs crates/maelstrom-fallback/src/pet_dance.rs` | Passed. |
| `cargo test --offline -j 1 -p maelstrom-core` | 11 passed, 0 failed; 0 doctests. |
| `cargo test --offline -j 1 -p maelstrom-fallback pet_dance::tests` | 8 passed, 0 failed, 2 unrelated tests filtered out. |
| `cargo clippy --offline -j 1 -p maelstrom-core -p maelstrom-fallback --lib --tests` | Exit 0; existing/concurrent unowned warnings remain. No warnings reported in owned files. |

Lint is not warning-clean across these crates: core/lib.rs has unused imports; fallback/lib.rs has unused imports/fields/functions, redundant unsafe and an ignored CloseHandle result. Core test output also noted an unused serde_json dependency in its manifest. Those paths were not owned and were preserved. No `-D warnings` claim is made.

The earlier audit's dirty-runner syntax blocker was a historical snapshot. Another contributor changed the shared runner during this task; the runner compiled for the above targeted tests. Its observed current SHA256 was `7F9A6E9A1DDBCA94CD9C059F7CD5353C62E464F64A190EB3A712FDAF5C9811CC`. This task neither repaired nor qualified that runner's execution behavior. A successful filtered test build does not establish the whole workspace or fallback controller is production ready.

`Cargo.lock` was observed dirty after checks (235 additions, 1 deletion relative to Git). Shared manifests/runner were changing concurrently; authorship of this delta is not assigned. It was preserved and reported to the coordinator. No manifests or dependencies were intentionally edited by this worker. Future checks should add `--locked` to prevent resolver changes.

## Remaining compatibility and integration limits

- Rust maps remain ordinary case-sensitive HashMaps. Decoding coalesces ASCII-equivalent names as native OrdinalIgnoreCase does, but callers must use retained spelling for lookup. Non-ASCII OrdinalIgnoreCase equivalence and locale-specific .NET integer signs remain unqualified; raw Unicode names are retained. This is a bounded compatibility repair, not universal .NET dictionary/culture emulation.
- Native malformed-entry skipping, extra-section tolerance and unlimited decompression are retained. Separate bounded codec hardening needs an explicit compatible policy; this task does not silently redefine legacy valid data.
- Sorted map encoding may differ from C# insertion order and compressor bytes while decoding to equivalent data. Old prototype tokens with bare delimiters are not automatically accepted; no saved-token migration was found or authorized. Do not silently infer which incompatible format was intended.
- Rust typed enums cannot represent arbitrary numeric invalid enum values as C# can. Width/height remain u32. Public Rust timing fields remain configurable; native parity tests cover default 70/150 ms thresholds only.
- No production bridge, scheduler/input owner, fallback actuation, OCR, installer or client integration changed. Optional substitution must remain gated on contract conformance, resource bounds, target identity, cancellation and independently verified outcomes.

## Next validation/integration request

Review and accept these two pure component changes before extending the port. For reproducibility, rerun only if changes warrant it:

```powershell
cargo test --offline --locked -j 1 -p maelstrom-core
cargo test --offline --locked -j 1 -p maelstrom-fallback pet_dance::tests
cargo clippy --offline --locked -j 1 -p maelstrom-core -p maelstrom-fallback --lib --tests
rustfmt --edition 2021 --check crates/maelstrom-core/src/deck_encoder.rs crates/maelstrom-fallback/src/pet_dance.rs
```

A later bounded task should introduce native/Rust shared fixture exchange and decide Unicode/culture and malformed-token compatibility explicitly, before wiring a versioned optional pure adapter through the current host. Keep the existing C# Dance teacher active until actual replacement acceptance is met.

Build slot released to coordinator after checks. Running processes/desktop use: none. Central claims/board and concurrent source edits were not modified.
