# Shared native/Rust Dance observations — 2026-11-12

Task: RUST-DANCE-CONFORMANCE-33. Workstream: CORE-DELIVERY. Owner: root/rust_audit. Status: needs-integration-review; both shared-corpus consumers pass.

## Deliverable

Both language implementations now have test consumers for the same fixed sequence corpus: 15 scenarios and 390 labeled observations. Each observation asserts the complete remembered cue list, proposed registered action IDs and rejection flag. A successful final Go alone cannot hide a prematurely emitted proposal, lost fault or incorrect boundary reset.

Four new owned files:

- `ProjectMaelstrom/ProjectMaelstrom.Tests/TestData/RustParity/dance-observations-v1.tsv`
- `ProjectMaelstrom/ProjectMaelstrom.Tests/RustDanceParityTests.cs`
- `crates/maelstrom-fallback/tests/dance_parity.rs`
- This report.

The native TestData glob supplies the same file Rust embeds with include_str!. No manifest/project/dependency or production-state-machine edits are needed. Consumers reject duplicate/empty scenarios, unknown fixture operations, malformed column counts and unrecognized cue labels. The explicit unrecognized phase fixture intentionally goes through each implementation's existing phase parser.

## Contract coverage and provenance

The synthetic corpus is grounded in native `PetDanceSequence.cs`, the scenarios in `PetDanceTeacherTests.cs` / `PetDanceFadeTests.cs`, and the repaired Rust port from RUST-PARITY-31. Expected outputs were fixed from those contracts, not recorded from either implementation during test execution. All observations specify absolute time, geometry, phase/cue labels, fade source/opacity and expected outputs; no generated input timing or hidden test-helper observations exist at runtime.

Coverage:

- Held and repeated arrows, a full three-cue proposal, duplicate Go suppression and Resolving.
- Success applied once across repeated result labels; next round requires four cues; Failure preserves that length.
- Known low fade plus a single neutral frame at 16 ms rearming repeated arrows.
- Loading clears old cues/proposals and does not manufacture Success or reject a long loading gap.
- Eight faults latch through later observations: unknown cue, unknown phase, unseparated direction change, 151 ms observation gap, duplicate/backwards/non-finite timestamp and changed geometry. A Failure boundary permits a fresh valid round.
- Wrong-direction fade, insufficient final fade, missing sequence beginning and pre-Go gap remain rejected with no proposals.
- The stable-cue boundary is checked at 69 and 70 ms.

SHA256 snapshots:

- Shared corpus: `C13D6173E1AB624EA65709B493022731D0C735F838AD485DFF5A824625A45B1E`.
- Native `Utilities/PetDanceSequence.cs`: `366AEB31B65721C9A1993591C6D8E61D628386CD5F65FD02669C005D27BAE348`.
- Rust `crates/maelstrom-fallback/src/pet_dance.rs`: `7C8FC4B3515F05B424B5B8931120AD679E7AA5960A5B56DDA228DBF1B0F7875D`.

## Verification

Scoped rustfmt and a read-only corpus inventory completed; exactly 15 case rows and 390 observation rows are present. With the exclusive build slot, Rust integration passed (1 test, all 390 observations/15 scenarios) and scoped Clippy exited 0. Clippy reported 3 warnings only in the concurrent unowned fallback/lib.rs: unread fields, field reassignment after Default, and unnecessary mutable reference. Those were preserved; workspace-wide warning-clean status is not claimed.

Owned native whitespace formatting passed with approved normal process access for the build host. The build slot was briefly returned for coordinator live work, then granted again for the remaining checks. Final results:

| Check | Actual result |
| --- | --- |
| Rust Dance integration | 1 passed, 0 failed; 15 scenarios/390 observations, all three outputs checked per observation. |
| Native focused Dance conformance | 1 passed, 0 failed; identical corpus; warnings-as-errors build passed. |
| Scoped rustfmt check | Passed. |
| Scoped native whitespace format and verify | Passed. |
| Scoped native analyzers | Exit 0, no output. |
| Scoped Rust Clippy | Exit 0, unowned runner warnings described above; no warnings in new test. |

No broad suite, runtime substitution or gameplay qualification was performed. Commands executed sequentially:

```powershell
cargo test --offline --locked -j 1 -p maelstrom-fallback --test dance_parity
cargo clippy --offline --locked -j 1 -p maelstrom-fallback --test dance_parity
rustfmt --edition 2021 --check crates/maelstrom-fallback/tests/dance_parity.rs
$env:DOTNET_PROCESSOR_COUNT='2'
dotnet format whitespace ProjectMaelstrom/ProjectMaelstrom.Tests/ProjectMaelstrom.Tests.csproj --no-restore --include ProjectMaelstrom/ProjectMaelstrom.Tests/RustDanceParityTests.cs
dotnet test ProjectMaelstrom/ProjectMaelstrom.Tests/ProjectMaelstrom.Tests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -warnaserror --filter FullyQualifiedName~RustDanceParityTests
dotnet format whitespace ProjectMaelstrom/ProjectMaelstrom.Tests/ProjectMaelstrom.Tests.csproj --no-restore --verify-no-changes --include ProjectMaelstrom/ProjectMaelstrom.Tests/RustDanceParityTests.cs
dotnet format analyzers ProjectMaelstrom/ProjectMaelstrom.Tests/ProjectMaelstrom.Tests.csproj --no-restore --verify-no-changes --include ProjectMaelstrom/ProjectMaelstrom.Tests/RustDanceParityTests.cs
```

## Boundaries

These are synthetic labeled-observation fixtures, not pixels, saved gameplay footage, perception accuracy, live success or a full-visit qualification. Agreement proves the tested observable decision contract only. Explanation text is diagnostic and intentionally not frozen as a wire contract. Default timing thresholds only are covered. There is no input dispatch, game access, bridge, installation or host substitution.

Concurrent dirty production files, manifests and Cargo.lock were read but preserved. No new dependencies or resolver changes are authorized by this task; Rust checks use `--locked`. The earlier core unit-test serde_json mismatch remains outside scope, and a focused fallback integration check will not qualify the complete workspace or library unit-test suite.

Next: retain the native teacher as the active implementation. Extend shared versioned observation/receipt freshness and cancellation contracts before proposing a default-off pure backend substitution. Do not infer a qualified live adapter from these tests.

Final build slot released to coordinator. No worker-started processes remain; desktop/game access: none. Only the four claimed new files were authored; central board/claims and concurrent source edits remain untouched.
