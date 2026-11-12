# Aarogaming repository reuse review

2026-10-30. This is a local Maelstrom engineering review, not a handoff.
All 17 repositories returned by the configured GitHub connection were screened;
the second inventory page was empty. Full recursive trees were available for 16.
Guild's recursive tree request failed twice, so its root tree and explicitly selected
source/tests were read instead. This is not an exhaustive file audit, branch-history
review or certification of upstream test claims. Repositories were read, not executed.
Current archive flags cannot identify which projects were previously archived.

Follow-up: all 16 other repositories are now available as full local Git clones.
See [local source inventory and initial Maelstrom comparison](LOCAL_REFERENCE_SOURCES.md).
The subsequent [Maelstrom 2.0 reconstruction plan](MAELSTROM_2_RECONSTRUCTION_PLAN.md)
reviews the capture/trainer chain in depth and supersedes the generic priority
ordering below. In particular, Guild contains concrete Diego scheduling logic.
The broader [Desktop-Use Game-Player review and plan](DESKTOP_GAME_PLAYER_PLAN.md)
now sets the product scope and adds overlay, capture, action-contract and curriculum
findings. The Diego review remains a component recovery appendix.

## Inventory and decisions

Full revision, visibility, archive and license metadata are in
`repository-reuse-inventory.json`. References are pinned to the reviewed revision.

| Repository | Revision | Review depth | Decision |
| --- | --- | --- | --- |
| [AutoWizard101](https://github.com/Aarogaming/AutoWizard101/tree/5b5c2400809dbcb9a146254036ffb86fce148094) | `5b5c2400809d` | selected-source-review | Current baseline; preserve local guided tools and observer. |
| [Wizard101_DanceBot](https://github.com/Aarogaming/Wizard101_DanceBot/tree/ca0ec613182a803b66dd9ce0f4a14ca631d9ab27) | `ca0ec613182a` | overview-screen | Already adapted locally; keep isolated backend. No second implementation. |
| [AaroneousAutomationSuite](https://github.com/Aarogaming/AaroneousAutomationSuite/tree/4b52088a021abdbcb8029e3d3ad3a8cc3748a608) | `4b52088a021a` | selected-source-review | Inspect workflow concept only; reject whole kernel import. Missing asyncio import in reviewed workflow. |
| [AndroidApp](https://github.com/Aarogaming/AndroidApp/tree/b68dbf351efa19b0d8508d109760714e8adf84a0) | `b68dbf351efa` | selected-source-review | Defer mobile UI; unique background work/backoff is inspiration, Android WorkManager is not a Windows component. |
| [Merlin](https://github.com/Aarogaming/Merlin/tree/191b9371e20cbc246a140a2b5624aa5ebbefb227) | `191b9371e20c` | selected-source-review | Adapt routing explanations, fallback codes and policy version when a model adapter is added. No model/server import now. |
| [MyFortress](https://github.com/Aarogaming/MyFortress/tree/3a9b922f8ccc86b560cd38d049bdf30962c0b290) | `3a9b922f8ccc` | selected-source-review | Defer gateway; rate limiter needs monotonic time, bounded key retention and concurrency review. |
| [Maelstrom](https://github.com/Aarogaming/Maelstrom/tree/bb6e3608ec0e85cc4d07bb1cda9810a19d505fd0) | `bb6e3608ec0e` | selected-source-review | Compare selectively; existing local MinigameCatalog counterpart makes wholesale copying redundant. |
| [Library](https://github.com/Aarogaming/Library/tree/9ee5442d05441525ff2aac70dd4be88c82f28836) | `9ee5442d0544` | selected-source-review | Adapt artifact/run identity and evidence references. Do not adopt compile/decompile-specific provenance or entire governance. |
| [Workbench](https://github.com/Aarogaming/Workbench/tree/7df1b0ca5ec3275b18e597e4a25127df00a5dfc9) | `7df1b0ca5ec3` | selected-source-review | Apply digest-verification approach to existing fixtures with stricter validation. No verbatim source imported. |
| [Aarogaming.github.io](https://github.com/Aarogaming/Aarogaming.github.io/tree/4317c4c8de4e0d0f71a7fec02af6ba7f298c2565) | `4317c4c8de4e` | overview-screen | README-only site scaffold; no current reuse. |
| [opencode](https://github.com/Aarogaming/opencode/tree/f0a9075fdf4d2c93ca3e1b27b953a871b03846ce) | `f0a9075fdf4d` | overview-screen | Defer full coding-agent stack; outside gameplay/observer scope. |
| [chatterbox](https://github.com/Aarogaming/chatterbox/tree/a29d61f35a0c684d2e0b9d3b36122d0a7fe41e18) | `a29d61f35a0c` | overview-screen | Defer optional spoken coaching until needed; model/runtime dependencies are unnecessary now. |
| [alexa-skills-kit-sdk-for-java](https://github.com/Aarogaming/alexa-skills-kit-sdk-for-java/tree/e7f16b0523b24a34a7971d4df7ecbb48b4539639) | `e7f16b0523b2` | overview-screen | Defer; Alexa/Java service integration does not solve current desktop needs. |
| [Guild](https://github.com/Aarogaming/Guild/tree/b3749df871403488532a950c51476805dabd77ab) | `b3749df87140` | selected-source-review | Adapt separation of observation, decision, execution and health. Do not copy permissive lifecycle transitions. |
| [circuit-simulator](https://github.com/Aarogaming/circuit-simulator/tree/ac24c4f534b914a14481a31b0b16b580d4e02328) | `ac24c4f534b9` | overview-screen | No relevant implementation reuse; domain mismatch and restrictive original license terms. |
| [aaroneous](https://github.com/Aarogaming/aaroneous/tree/a3383f2feeeee32a47bcd11e34698cfc0d138131) | `a3383f2feeee` | selected-source-review | Keep previously reviewed resource-governor/adapter ideas; no Rust rewrite. |
| [aaroneous-devtools](https://github.com/Aarogaming/aaroneous-devtools/tree/513a246e76aac42b9d49fd522bc14e7c67d5ffc7) | `513a246e76aa` | selected-source-review | Highest priority follow-on: attempt receipts, stale-result rejection and explicit recovery. |

## Highest-value candidates

### Workbench: artifact integrity — applied as a local adaptation

Reviewed `scripts/generate_artifact_digest_report.py`,
`scripts/verify_artifact_digests.py` and `tests/test_verify_artifact_digests.py` at
`7df1b0ca5ec3275b18e597e4a25127df00a5dfc9`. The useful behavior is verifying expected
artifacts against hashes, reporting missing files and refusing partial success.
The original verifier overwrites duplicate names in its expected-entry map, accepts
an empty expected set through its function API, and does not verify recorded sizes.
Its report parser assumes an object. These are reasons to adapt, not paste unchanged.

Added `tools/check_fixture_integrity.py`: a stdlib-only checker for our existing
Diego manifest. It verifies every declared file's byte count and SHA-256, rejects
empty/duplicate/unsupported manifests, constrains paths to the fixture directory,
and reports all ordinary per-file failures. It reads no game state and writes no
files. The fixture format and pinned fixture bytes remain unchanged.

```powershell
.local/envs/dance/Scripts/python.exe tools/check_fixture_integrity.py tools/fixtures/diego_observer/v1/manifest.json
```

This is newly written code inspired by the reviewed behavior, not a verbatim source
copy or an implementation of Workbench's CP2 format. It verifies declared files only;
extra files are not certified. The manifest is trusted input for expected hashes:
an attacker changing both data and manifest is outside this integrity check. It is
not a signature check, authenticity proof or concurrent-filesystem sandbox.

### Devtools: attempt identity and recovery — next priority

The preceding review examined `foundation/crates/steward/src/verification.rs` and
`local_verification.rs` at `513a246e76aac42b9d49fd522bc14e7c67d5ffc7`.
Extract the rules that bind a result to an exact request and attempt, reject stale
results, and distinguish failure from inability to run. Implement local run receipts
around ActivityRun; retain Windows job ownership and cooperative stop. Avoid importing
the signing server, GitHub review system or cross-machine routing as prerequisites.

### Guild: useful boundaries, incomplete lifecycle guarantees

Reviewed `core/subsystem_interfaces.py`, `core/manager_health_protocol.py`,
`tests/test_subsystem_interfaces.py` and LICENSE at
`b3749df871403488532a950c51476805dabd77ab`.
GameContext and separate decision/execution interfaces fit a future multi-game adapter
boundary. However, GameState lacks cancelled/expired/review-pending outcomes, unknown
progress becomes zero, and the health mixin's transition method does not validate a
transition graph. It suppresses callback errors. Its tests include basic enum and
object construction checks; README production-readiness claims are not independent
verification. Adapt a smaller local contract after run receipts; do not replace the
working activity runner with this framework.

### Merlin: explainable model routing — defer until inference exists

Reviewed `merlin_routing_contract.py` and its test module at
`191b9371e20cbc246a140a2b5624aa5ebbefb227`.
Selected model, policy version, fallback reason, retryability and trace fingerprints
would explain why an optional planner chose a backend. The module imports global
Merlin settings and includes DMS-specific routing, prompt heuristics and A/B behavior.
Extract a narrow record shape later. Do not use prompt-keyword heuristics as the
real-time minigame controller, confuse stable query fingerprints with unique attempts,
or automatically retry game input because a model request was retryable.

### Library: provenance and searchable evidence — adapt selectively

Reviewed `contracts/discovery/artifact.schema.json` and
`docs/source-of-truth/schemas/chimera_v2_provenance_envelope.schema.json` at
`9ee5442d05441525ff2aac70dd4be88c82f28836`.
Useful concepts: run/artifact identities, source references, hashes, explicit status
and validation errors. The latter schema is specifically for compile/decompile work
and requires a mythic naming structure; it is not a generic game-run receipt.
Maelstrom should record source frame, pack digest, configuration and evidence mode
without claiming a probability from template similarity. Start with files; do not
install a knowledge service to store a handful of reports.

## Overlap and rejection notes

- Older Maelstrom's `src/ProjectMaelstrom/Utilities/MinigameCatalog.cs` has a local
  counterpart already. Blob hashes differ, so this is overlap, not a claim of byte
  identity. Its plugin/provider loading is not proof that a listed game is playable.
- AAS `scripts/core/workflow.py` uses `asyncio.gather` without importing asyncio in
  the inspected module. It clears its queue before processing and has no durable
  attempt receipt. Do not transplant the kernel for our lifecycle requirement.
- MyFortress `gateway/api/rate_limit.py` uses wall time and a process-global map of
  queues. No bounded key eviction or synchronization appears in that file. A future
  local throttle should use explicit monotonic time and bounded storage instead.
- AndroidApp's `StatusCheckScheduler.kt` uses unique periodic WorkManager work with
  network constraints and backoff. Useful UX idea for optional status refresh;
  Android-specific code and its 15-minute cadence do not suit game observation.
- DanceBot is already an isolated adapter. Workbench also contains UI audit/toolkit
  material and trial DanceBot code: compare before duplicating existing local tools.
- OpenCode, Chatterbox and Alexa SDK were screened by tree/README, not deeply audited.
  Their full coding-agent, voice-model and Java service stacks add no immediate value
  to the current offline observer. No model download or service deployment was run.
- Circuit simulator's README reproduces original non-commercial modification terms
  and asks readers to consult the original license. No code is selected for reuse.
  The website repository contains only a minimal scaffold.

## Licensing and independence

GitHub identifies GPL-3.0 for this repository; MIT for Guild, Aaroneous, Devtools,
OpenCode and Chatterbox; Apache-2.0 for Alexa SDK. Guild's root MIT text was also read.
No root license was detected for Merlin, Workbench, Library, older Maelstrom,
MyFortress, AndroidApp, AAS or DanceBot. Workbench's template LICENSE is not a license
for the repository. Ownership/access does not establish licensing for embedded
third-party material. No source from those repositories was copied verbatim here.
Before a future direct copy, trace its origin and applicable notices; retain them.

No repository became a runtime/build/release dependency. No upstream policies were
adopted merely because a file contained instructions. No external repositories,
services, schedules or game processes were changed.

## Verification and bounded next steps

The new checker passes against all 23 files declared by the existing manifest.
The manifest itself is the 24th fixture-pack file and cannot attest to its own hash.
New tests cover mutation, missing files, growing files, path escape, invalid metadata,
duplicate keys, empty maps and unsupported versions. All **46 Python tests passed**.
The two new Python files passed formatting, compilation and whitespace checks.
No C# source changed; the previous 63-test
.NET result remains historical evidence, not a newly run test suite.

Next: add local attempt receipts/outcomes using Devtools' rules and Library's evidence
concepts; then unify input ownership. Merlin routing becomes relevant only after an
optional planner is implemented. No mass imports, framework migration or further
handoff exchange is needed.
