# Rust port audit — 2026-11-12

Task: RUST-PORT-AUDIT-30. Owner: root/rust_audit. Status: needs-integration-review. Parent: CORE-REFERENCES; related CORE-DELIVERY, CORE-ASSETS and W101-DANCE.

Maelstrom has a five-crate Rust scaffold and several translated utilities. It does not yet have a usable Rust game player, Rust desktop application, or a Rust replacement connected to the current C# execution owner. The best immediate reuse is offline contract and utility comparison. Preserve the existing C#/Python application while qualifying individual replacements.

## Scope and evidence

Read-only source review, Git history/status and small text searches; only this report was written. No builds, tests, formatters, linters, downloads, services, models, game access or desktop actions were run, per the assigned resource restriction while another workload is active. Test declarations below are source evidence, not passing executions. No Rust installation or live qualification evidence was found in the reviewed project records.

Read AGENTS.md, PROJECT_ORGANIZATION.md, PRODUCT.md, ROADMAP.md, the external-agent guide, claims and relevant workstreams. CORE-REFERENCES and CORE-ASSETS are backlog; CORE-DELIVERY and W101-DANCE are integration_pending. Their game/runtime gates are not closed by this audit. The board's short next-task wording sometimes predates newer roadmap evidence; retain the explicit source/live distinctions.

Source baseline: commit `b15e5dcb60b957a6dd579d04474854ca1b4ebd1a`, “Phase 1 scaffold - Cargo workspace and crate migration,” following `5e5deeb`, the pre-refactor snapshot. Current fallback manifest/library have uncommitted changes. Legacy `ProjectMaelstrom/rust_port/*` is tracked but deleted in the working tree. Nothing was restored, removed or repaired.

## Inventory and actual usability

Paths in this report are repository-relative.

| Component | Actual implementation | Reuse/readiness |
| --- | --- | --- |
| `Cargo.toml`, `crates/*/Cargo.toml` | Five library members: core, platform, fallback, reflex, ide. | Scaffold exists. No executable entry point or end-to-end Rust application is present in these crates. |
| `maelstrom-core/src/lib.rs` | Serializable card/deck/enchantment/input/observation types; confidence-only Arbiter; PolicyController trait. Three synthetic tests: two confidence branches and JSON card round-trip. | Useful contract prototype. No host bridge, capability registry, owned execution, observation freshness enforcement or verified outcomes. TOML/nested-deck conformance promised in the handoff is not implemented by the JSON test. |
| `maelstrom-core/src/combat_geometry.rs` | Fixed coordinate math, hand/target/enchantment helpers; three small tests. | Actual pure calculation port, suitable for differential fixtures. Fixed geometry is not current-resolution qualification. Claims lineage to WizSDK, Deimos and C# CombatGeometry. |
| `maelstrom-core/src/deck_encoder.rs` | Base64/zlib delimited spell-deck serialization; two round-trip tests. | Actual algorithm, but incompatible with current C# delimiters; see below. This is a spell inventory deck codec, not serialization of the automation Routine Deck. |
| `maelstrom-platform/src/calibration.rs` | Minimum-size/aspect-ratio report; two tests. | Partial C# calibrator translation. Does not attach a window, check actual DPI/focus, or reproduce all C# combat tray/button checks. “Ready” means only the supplied size cleared the minimum. |
| `maelstrom-platform/src/lib.rs` | WindowTracker, ScreenCapturer and InputActuator traits; guard methods reject network manipulation and game-file writes. | Interfaces only: no Win32 attachment/capture/input implementation. A boolean/comment is not an enforced scheduler lease or target/build qualification. |
| `maelstrom-fallback/src/pet_dance.rs` | Observation sequence state machine with two minimal tests, one of which observes only the first cue. | Substantial partial translation, behaviorally divergent from C# and unsafe as a substitute until parity repaired. No pixels, OCR, audio, transport or visit transitions. |
| `maelstrom-fallback/src/wad_reader.rs` | KIWAD header/index parsing, case-insensitive lookup, plain/zlib entry reading. No test declarations. | Real read-only utility candidate, but unbounded allocation/decompression and missing structural/checksum validation require hardening and fixtures. |
| `maelstrom-fallback/src/player_controller.rs`, `bazaar_bot.rs` | Controller prints actions; image matcher returns `(200,150)` when a file exists; Bazaar flow loads names, prints refresh/buy actions and polls filenames. One no-files test. | Demonstration skeleton only. No recognition, purchases or owned input. Do not connect this purchase flow to live input without policies, reserves, identity and outcome verification. |
| `maelstrom-fallback/src/lib.rs` | Payload branches print and return success; loop advances through cards. Current dirty edit begins adding channels/process imports but contains orphan struct fields and unresolved state/event types. | Current source visibly cannot compile as written; this is inspection, not compiler output. Even committed baseline was a logging runner, with no real cancellation, retry, subdeck execution, vision or subprocess implementation. |
| `maelstrom-reflex/src/lib.rs` | Stub policy returns NoOp with configured confidence; “replay buffer” is an atomic counter capped for display. One count test. | No samples stored, no ring storage/export, inference, smoothing, weights, training or DAgger dataset. High configured stub confidence can suppress fallback with NoOp; do not mistake it for learned competence. |
| `maelstrom-ide/src/lib.rs` | Theme/profile string defaults and initialization print; one defaults test. | No GUI, dock panels, card canvas, loading of Wizard101.toml or telemetry UI. Manifest has no eframe/egui dependency. |

There are 16 `#[test]` declarations across the workspace. None establishes recorded-screen recognition, cross-language conformance or live success. No verified current Cargo test/build result was located. A historical Rust build attempt in `docs/W101_LEGACY_VALIDATION_20261108.md` was blocked on cached `roxmltree`; that refers to legacy source validation, not successful validation of these new crates.

## Runtime linkage and provenance

The .NET application remains `ProjectMaelstrom/ProjectMaelstrom/ProjectMaelstrom.csproj` (Windows Forms/net9). Searches of its C# and project files found no Rust crate names, `wizard101_port` integration or Cargo/native Rust build step. Rust manifests define ordinary libraries, not a C ABI `cdylib` bridge. Rust-to-Rust dependencies exist, but no production C# caller was identified.

C# continues using its own implementations: `Utilities/PetDanceLiveTrial.cs`, `PetDancePanelReplay.cs`, `PetDanceSoundObserver.cs` and `PetDanceStudy.cs` instantiate C# PetDanceSequence; `CombatMctsSimulator.cs` calls C# CombatGeometry. `OperationScheduler.cs`, `GameInputLease.cs`, routine/module execution and job-deck contracts remain the execution foundation. Similar names in Rust do not replace these call sites.

Source headers trace geometry to WizSDK/Deimos/C# CombatGeometry, codec to Deimos/C# DeckEncoder, WAD to libkiwad/wizwad/C# WadArchiveReader, calibration to ClientOperationsCalibrator, and Dance to C# PetDanceSequence. These are attribution claims supported by analogous code, not exhaustive license/revision certification. Require exact source revision/hash and fixture lineage for any promoted port.

Two other Rust projects are imported references outside the root workspace: `ProjectMaelstrom/ProjectMaelstrom/Scripts/Library/Aurorium-main/Aurorium-main` and `.../Scripts/Library/Arcane`. They are asset/manifest/parser sources, not Rust implementations of the current game scheduler. Historical `aaroneous` core-contracts and Steward service ideas are reviewed in `CORE-REUSE-AUDIT-01.md` and `GUILD_STEWARD_SCHEDULER_REUSE_20261110.md`; their reuse does not require adding a service or second queue.

## Concrete compatibility defects and migration risks

1. **Spell-deck wire mismatch:** C# `Utilities/DeckEncoder.cs` uses literal backslash-prefixed `\+`, `\,`, `\;`; Rust uses bare `+`, `,`, `;`. C# maps are case-insensitive and signed counts, Rust maps are case-sensitive `u32`; Rust sorts keys. Same-language round-trip tests cannot detect interoperability failures. Define the accepted legacy format before fixing either side, and test both directions, malformed tokens, case collisions and bounds. Both current decoders tolerate malformed entries and unbounded decompression.
2. **Dance is not behavioral parity:** Rust rejection does not latch `_fault` as C# does; subsequent observations can resume. Rust accepts stable direction changes without C#'s confirmed neutral/fade rearming and handles repeated identical cues differently. Go does not reproduce gap/fault/final-neutral checks. It emits bare direction names rather than registered `dance-*` proposal IDs and can repeat proposals on successive Go observations. C# emits once and suppresses duplicates. These are control-contract regressions, not cosmetic differences.
3. **Current runner is incomplete work:** `crates/maelstrom-fallback/src/lib.rs` contains fields outside `DeckRunner` and references undefined RunnerState/RunnerCommand/TelemetryEvent. Preserve the other contributor's work; request an owned completion/review. The Windows/process imports also put OS concerns outside the handoff's promised platform-only FFI boundary. Imports do not prove subprocess cleanup exists.
4. **Core portability is unproven:** no_std/alloc is declared, but `combat_geometry.rs` uses String, format! and ToString without explicit alloc imports in that module. The UUID v4 random source and feature combinations also need a dedicated portability check. Default-build success, if later demonstrated, will not prove no-default-features builds.
5. **Contract gaps:** ObservationFrame has a version/time/sequence but no target/process generation, profile digest, producer qualification or age policy. Deck/card have no enforced schema compatibility, receipt identity, outcome verification or cleanup obligations. Definitions and instances are not actually separated; Deck embeds MacroCards. ExecutionMode variants do not implement one-shot preemption or aura scheduling.
6. **Resource/safety gaps:** WAD trusts counts, offsets, lengths and declared sizes, allocates before bounds validation and does not verify checksum or decompressed length. Existing fixed coordinates and “ready” reports cannot grant live permission. Low policy confidence does not prove fallback safety; unknown states must remain blocked unless fresh preconditions hold.
7. **Duplicate migration state:** root crates coexist in Git history with tracked legacy `rust_port`; working-tree deletions are not finalized migration evidence. Do not restore/delete either tree automatically. Establish the active tree, compare hashes/diffs and preserve source provenance before any cleanup. Imported Aurorium/Arcane are independent references, not duplicates to delete.

## Handoff reconciliation

`Maelstrom Master Architecture & Antigravity Handoff.md` is a target proposal. Its pure-Rust identity, dual-loop trained reflex, zero-copy capture, bounded inference, rock-solid fallback, docking UI and self-training claims exceed implementation. The five crate names and most initial domain shapes now exist. Phases 2–5 remain principally stubs; even phase 1's specified conformance checks are incomplete.

The earlier `docs/coordination/results/W101-IDLE-PROFILE-22.md` correctly described the source at its review time, but its sentence saying the five-crate workspace is absent is now stale. Its architectural corrections remain applicable: one owner, bounded nested work, capability-based legacy bridge, explicit uncertainty, provenance and opt-in learning. This report supersedes that inventory sentence only, not its live-test evidence.

Retain the useful separation of domain, platform, deterministic decisions, optional learning and presentation. Change the implementation order: derive language-neutral contracts from the working runtime; prove offline parity; integrate one measured component under the existing scheduler; replace more only when beneficial. Do not introduce independent aura, one-shot, reflex or fallback actuation. Use registered capabilities rather than arbitrary shell commands in a deck. Preserve Escape takeover and cleanup rules; F12 is only an additional proposed stop binding.

## Ranked bounded next tasks

| Priority | Proposed task and exact acceptance | Why |
| --- | --- | --- |
| 0 | RUST-BASELINE: coordinate with current fallback owner; settle a buildable source checkpoint and tree disposition, pin source hashes; later run serialized offline Cargo format/check/test with cached dependencies, recording missing prerequisites without downloads. | Establishes what actually builds without overwriting ongoing work. No live adapter should be attached now. |
| 1 | RUST-CONTRACT-PARITY: fixture schema for target identity, fresh observations, capability IDs, proposals, receipts, outcomes and cleanup; compare with native routine/deck contracts. Start with geometry and codec bidirectional golden fixtures, including malformed input. | Small, cheap, reviewable reuse; prevents a second incompatible product schema. |
| 2 | RUST-DANCE-PARITY: translate the existing C# sequence regression corpus, repair fail-closed/duplicate/fade semantics; run both against identical synthetic and frozen observations with no input. | Highest control risk in a seemingly complete port; native Dance functionality must not regress. |
| 3 | RUST-ASSET-READER: bounded WAD counts/offsets/decompression, error classification and plain/compressed/corrupt fixture comparison with independent expected bytes. | Fits CORE-ASSETS backlog and can provide value without new input ownership. Choose Rust only if portability/performance or packaging benefit is demonstrated. |
| 4 | RUST-OWNED-BRIDGE: select one proven pure function behind a versioned adapter, called by the existing native owner; test mismatch, timeout, cancellation, target changes and unavailable backend. Keep adapter default-off until qualified. | Makes reuse real without replacing the application or introducing an independent controller. |
| 5 | Measured platform replacement, then UI prototype, then optional reflex research, each separately claimed and budgeted. | No evidence currently justifies capture/host rewrite, additional GPU workload or training. UI and inference are not prerequisites for reusable ports. |

Priorities 0–3 are recommendations, not claimed implementation work. Production dependencies already named in manifests (serde/serde_json/uuid/thiserror/flate2/base64/chrono, and dirty tokio full/windows additions) are source declarations, not proof of approved integration, cached availability or release inclusion. Avoid adding proposed egui/ORT/capture dependencies until a bounded task needs them and approval exists.

## Audit receipts and unknowns

Commands used: targeted `Get-Content`, `rg --files`, `rg -n`, `git status --short -- <Rust paths>`, `git log -5 --oneline -- <Rust paths>`, `git show` of baseline/legacy manifests and runner, and SHA256 reads. Only the report is changed. Validation is source inspection; format/lint/tests intentionally not run under this assignment.

Snapshot hashes (SHA256):

- core/deck_encoder.rs: `1F09FCD3B59CDB4778E3E061762EF9789E8E80367EB066B003A2C94C82E60C35`
- fallback/pet_dance.rs: `7C0C766C7370DFEB0E518A7FFDB9197611F2D31B6A30215899C1C1569B3F80D2`
- fallback/lib.rs (dirty snapshot): `C0CA0FE9379DE0963F54DBF84086FB21AA656D6D824D0D50613CFC4AD06BBB86`
- fallback/wad_reader.rs: `B4204D3B5FA83645DD48BC903AC49629B71E6468BD6E812A4CE3DBBFFDE8B10C`

Unknown: unrecorded Gemini/Antigravity session results outside the reviewed repository, any later concurrent edits, exact dependency-cache/toolchain readiness, current binary provenance, cross-language parity and performance benefit. No private agent/chat history was accessed. The located handoff, Git scaffold commit and prior architecture review are the relevant available continuation evidence.

Integration request: coordinator review this report, reconcile stale inventory wording when appropriate, and assign only the next bounded offline task. No central board/claim edit by this worker. Running processes/desktop use: none.
