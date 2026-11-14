# Shared native/Rust component conformance — 2026-11-12

Task: RUST-CONFORMANCE-32. Workstream: CORE-DELIVERY. Owner: root/rust_audit. Status: needs-integration-review; focused shared conformance checks passed.

## Deliverable

Added one language-neutral TSV corpus with independent native C# and Rust consumers. This extends RUST-PARITY-31 from separate language-local tests to both implementations checking the same inputs and expected results. No production behavior, manifests, dependencies, scheduler, input adapter or installation changed.

Exact owned paths:

- `ProjectMaelstrom/ProjectMaelstrom.Tests/TestData/RustParity/component-parity-v1.tsv`
- `ProjectMaelstrom/ProjectMaelstrom.Tests/RustComponentParityTests.cs`
- `crates/maelstrom-core/tests/native_parity.rs`
- This report.

The existing native TestData copy glob includes the corpus without a project-file change. Rust embeds the same repository file via include_str!, so no copied duplicate fixture or parser dependency is introduced. Both consumers reject unknown operations, duplicate IDs and wrong column counts and require exactly 56 cases.

The corpus covers:

- All 8 legacy target slots and all 4 enemy pedestals.
- All 28 hand coordinates for hand sizes 1–7.
- Enchantment before/after/equal X positions and odd-width integer rounding; original click points, result name, Y and width remain checked.
- 9 invalid signed-index/count cases.
- 3 native zlib/Base64 spell-deck tokens: ordinary names, empty sections, and mixed-case duplicates with signed counts, malformed entries and overflowing count rejection.

These are synthetic legacy-coordinate and wire-format fixtures. Agreement is compatibility evidence, not independent proof that any coordinate matches the current client or that native legacy calculations are correct gameplay behavior. No desktop/game access occurs.

## Provenance

Geometry expected values were enumerated from the reviewed native contract and existing NativeSuppliedIntegrationsTests, including full slot coverage. Both consumers compare against fixed values rather than generating their own expected values with the production formula.

Codec tokens were generated with .NET ZLibStream/Convert.ToBase64String from these exact literal wire strings (backslashes are bytes in the format):

```text
Tempest\+4\,Stormblade\+2\;Cleanse Charm\+1\;N
N\;N\;N
Tempest\+4\,TEMPEST\+-2\,bad\,Overflow\+2147483648\;Cleanse Charm\+1\;Wand Hit\+0
```

Reference SHA256:

- Corpus: `476FAC06BBA93BF828EEF7AEAD8822B61C2C9C9C10542FEEF17D0B140AF94A75`.
- Native `Utilities/CombatGeometry.cs`: `DAB920A69F1830025760172D0DE8C350854E6829252295106E15AB599B362129`.
- Native `Utilities/DeckEncoder.cs`: `C5BCCD312694B1EC6645E4DCD5296029954181AACD6696D4FC659E87DDDC5C9A`.

## Verification

The coordinator granted the exclusive build slot after live checks ended. All checks ran sequentially; Rust used cached dependencies and `--locked`. Actual results:

| Check | Result |
| --- | --- |
| Rust `native_parity` integration test | 1 passed, 0 failed; all 56 corpus cases exercised. |
| Scoped Rust Clippy | Exit 0, no warnings emitted. |
| Scoped rustfmt check | Passed. |
| Native `RustComponentParityTests` | 1 passed, 0 failed; same 56 cases exercised. Warnings-as-errors native build succeeded. |
| Owned native whitespace formatting and verify | Passed. |
| Owned native analyzers | Passed, exit 0, no output. |

Initial native formatter attempt could not connect to its build host's local pipe under restricted process access. The approved elevated retry succeeded; subsequent native checks used that working mode. No automatic approval rejection occurred. No full native suite, full Rust workspace test or live verification was run for this tests-only increment.

Commands executed:

```powershell
cargo test --offline --locked -j 1 -p maelstrom-core --test native_parity
cargo clippy --offline --locked -j 1 -p maelstrom-core --test native_parity
rustfmt --edition 2021 --check crates/maelstrom-core/tests/native_parity.rs
$env:DOTNET_PROCESSOR_COUNT='2'
dotnet test ProjectMaelstrom/ProjectMaelstrom.Tests/ProjectMaelstrom.Tests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -warnaserror --filter FullyQualifiedName~RustComponentParityTests
dotnet format whitespace ProjectMaelstrom/ProjectMaelstrom.Tests/ProjectMaelstrom.Tests.csproj --no-restore --include ProjectMaelstrom/ProjectMaelstrom.Tests/RustComponentParityTests.cs
dotnet format whitespace ProjectMaelstrom/ProjectMaelstrom.Tests/ProjectMaelstrom.Tests.csproj --no-restore --verify-no-changes --include ProjectMaelstrom/ProjectMaelstrom.Tests/RustComponentParityTests.cs
dotnet format analyzers ProjectMaelstrom/ProjectMaelstrom.Tests/ProjectMaelstrom.Tests.csproj --no-restore --verify-no-changes --include ProjectMaelstrom/ProjectMaelstrom.Tests/RustComponentParityTests.cs
```

## Shared-checkout observations and limits

At task start external dirty paths included core manifest/lib.rs, fallback manifest/lib.rs and Cargo.lock; prior codec/Dance task edits also remain dirty. None were edited or reverted. A potential external regression was reported: core manifest removed serde_json while a core library unit test still references it. The proposed integration-test command targets only the new test binary; it does not establish that all core unit tests compile. Resolve unrelated failures with their owner, not by changing those paths here.

Rust uses unsigned indices, native uses signed ints. The Rust fixture adapter explicitly rejects negative signed input during conversion before calling the production API; this is representation-boundary conformance, not a claim the usize API accepts signed values. Codec corpus is ASCII-focused; Unicode comparer/culture, resource bounds and old bare-delimiter token migration remain the limits documented in RUST-PARITY-31.

No bridge or adapter is enabled. Next increment should extend shared observed-input fixtures to Dance and versioned receipt/freshness contracts, with explicit ownership and acceptance before optional host substitution.

At completion Cargo.lock remained externally dirty (235 additions, 33 deletions against Git); no ownership is attributed and it was not edited/reverted by this worker. Build slot released after verification. Running test/build processes and desktop use: none.
