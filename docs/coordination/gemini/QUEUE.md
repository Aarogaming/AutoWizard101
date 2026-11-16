# Gemini sequential work queue

This is Gemini's assigned lane. Codex/coordinator owns implementation integration, shared builds/tests, packaging and live checks. Root claims.json remains the exact ownership authority; this queue does not replace it. Read AGENTS.md and the current TO_GEMINI directive first.

## How to pick work

1. Select the first task below not already marked READY_FOR_REVIEW in your FROM_GEMINI history. Confirm its exact paths are registered to Gemini in ../claims.json. If missing/conflicting, report BLOCKED; do not expand your scope.
2. Append an ACK naming the task and exact paths to FROM_GEMINI.md, then start without another permission request. Only one queue task active at a time.
3. Read existing code/contracts and preserve others' changes. Write only that task's assigned files. If a new file already contains another contributor's work, pause that path and report the conflict.
4. Append READY_FOR_REVIEW with changed paths, evidence, unrun tests, limitations and proposed next step. This hands work to root; it does NOT mean reviewed, accepted, integrated, tests passed or live-qualified.
5. You may move immediately to the next preclaimed task after READY_FOR_REVIEW, with a new ACK. Do not keep modifying handed-back files while root reviews them. When all three are handed back, stop; brief scheduled checks may look for a new assignment, not repeat completed work.

No core source edits, builds/test execution, game/host/desktop access, model processes, dependencies, installs or commits. Root runs checks and reviews. Reading source is allowed; game installation files, if examined, remain read-only. Never include credentials or raw private telemetry. Do not edit the central board/claims, other reports or liaison-owned files.

## 1. GEM-DANCE-AUDIT-71 — Full Dance visit integration map

Owned output: `docs/coordination/results/GEM-DANCE-AUDIT-71.md`.

Read-only source review of the current walk-up → sigil interaction → loading → instructions/level selection → Dance playback/response → result/snack policy → leave → native Idle chain. Trace actual function names and paths, shared scheduler/input ownership, observations, cancellation, transition timeouts and cleanup. Separate implemented callable bindings, tests/replay evidence, and gaps. Identify exact missing edges/bindings; do not infer a complete visit from one perfect game or source presence. Include source revision/worktree caveat and a small prioritized integration table. No new code or live calls.

Acceptance for handback: every stage points to actual source or is explicitly missing; missing bindings name producer/consumer contracts and smallest offline verification. Keep facts current, not copied from historical success claims.

## 2. GEM-REPLAY-MATRIX-72 — Offline Dance visit contract tests

Owned outputs:
- `ProjectMaelstrom/ProjectMaelstrom.Tests/GeminiDanceVisitContractTests.cs`
- `docs/coordination/results/GEM-REPLAY-MATRIX-72.md`

Author test SOURCE only against existing public/internal testable contracts and repository patterns. Cover meaningful interrupted-visit cases such as loading pause/resume, focus pause versus physical-key takeover, timeout/cancellation cleanup, unknown state, snack/Crowns exclusion, and default Idle completion where actual contracts support them. Choose a small coherent subset rather than placeholder tests for every stage. Use deterministic fixtures/fakes; no real game/process/input/capture/model/filesystem dependencies beyond normal isolated test fixtures. Do not invent production APIs, modify core code or duplicate implementation assertions just to raise test counts.

Read nearby test conventions before editing. If a contract cannot be tested through existing seams, report that gap instead of changing production code. Document cases, rationale, fixtures and exact recommended filtered test command; mark ALL execution unrun. No builds, tests, formatters that invoke builds or dependency restore. Root owns execution/format/lint and integration.

Acceptance for handback: focused behavioral tests in the single owned C# file, with real contract references and explicit unexecuted status; report remaining gaps and no fabricated pass count.

## 3. GEM-LEGACY-MAP-73 — Reusable legacy components

Owned output: `docs/coordination/results/GEM-LEGACY-MAP-73.md`.

Map useful existing legacy Dance/navigation materials to actual Maelstrom functions. Inspect already available archive inventories/extracted sources first; no bulk extraction/import or downloads. Record provenance, exact upstream symbol/file, behavior, resolution/scale assumptions, input ownership, client/network dependencies, license status if available, and present Maelstrom equivalent. Classify direct reuse, teacher/reference, missing adapter, duplicate, or unsuitable. Prefer a concise actionable map over a repository-name list.

Acceptance for handback: specific useful components linked to current function/contracts, with smallest bounded next step and test idea. No claims that imported archives are operational integrations, and no installation or running legacy programs.

## Progress authority

FROM_GEMINI.md records your ACK/READY_FOR_REVIEW/BLOCKED messages. Root records acceptance and integration on the central board. Do not modify this queue to grant yourself more work. Latest explicit root assignment supersedes stale freeze messages; unrelated grants are not implied.

## Completed follow-up: GEM-ENTRY-EVIDENCE-78

Owned output: `docs/coordination/results/GEM-ENTRY-EVIDENCE-78.md` only. Root registered this exact path to Gemini and activated the task in LIA-0035. Confirm the claim, ACK, then proceed without another permission request. Tasks 71–73 were handed back in GEM-0027 and must not be repeated or edited during root review. Task72 test source transferred to root repair task79; do not edit it.

Purpose: design the smallest entry/visit evidence adapter using verified existing producers and inventory saved fixtures without assuming a full live runtime already exists.

Read the latest task75 specialist extraction and actual `PetDanceLiveTrial.RunSpecialistAsync`, admission/control/result types, `IPetDanceVisitRuntime`, `PetDanceVisitObservation`, entry/postgame/level vision contracts and shared fact/alias producers. Distinguish the extracted in-process specialist from still-unbound full-visit runtime. Earlier report71's 'subprocess GUI only' description is historical and must not be copied as current.

Required deliverables in the single report:
- Field matrix for every proposed observation: exact consumer field/type; exact existing producer method/field and evidence path; target, geometry, image hash and timestamp binding; freshness and unknown/conflict behavior; verified or missing. No invented producer names or assumed energy-cost authorization.
- Entry stages covering sigil identity, loading, arena identities/selection highlight, enabled Play, configured entry cost versus qualified energy, watch/respond, whole-game result, no-feed controls, menu and native Idle. Include only supported data; record absent producers explicitly.
- Saved fixture inventory from existing indexes/reports/local assets: exact paths, file existence, stage, geometry/hash/timestamp metadata when actually present, and limitation. Do not expose raw private telemetry or turn missing metadata into invented values. No large captures, archive extraction, OCR/model runs or new recordings.
- Smallest proposed adapter interface and ownership boundaries. Keep cancellation/takeover/focus pause distinct; root scheduler/input lease remains sole owner. A specialist receipt is not an entry proof, result proof or full-visit success.
- Prioritized offline acceptance matrix, including mismatched frame/target, expired evidence, ambiguous level, unknown cost/energy, loading transitions, no-feed-only and missing fixtures. Tests are proposed, not executed.

Report provenance/revision and concurrent work caveat. Do not modify prior reports, test source, production code, board/claims or any other path. No builds/tests/live/host/game/model/installer/dependency/commit operations. Return READY_FOR_REVIEW with exact gaps; handback is not acceptance.

## Active next task: GEM-ROUTE-PROVENANCE-83

Owned output only: `docs/coordination/results/GEM-ROUTE-PROVENANCE-83.md`. Root claim verified active; LIA-0037 activates this report-only task. ACK then proceed without another permission request. No source/build/live rights.

Audit existing route artifacts for Wizard City Commons → Pet Pavilion → Dance approach. Inspect current macro/route/knowledge files, saved route demonstration indexes and legacy waypoint source without running them. Produce a concise table for each actual waypoint/transition: exact artifact path + field/symbol, coordinate units/axis/heading convention when documented, zone/world identifiers, start/destination anchor, direction, evidence origin (user recording, live receipt, legacy source, synthetic fixture), timestamp/revision if present, and confidence/reuse limitation. Unknown is an acceptable result; do not invent coordinates or convert between coordinate frames without a verified transform.

Separate verified current-client route evidence from historical legacy coordinates, route proposals, unqualified maps, and tests. Identify missing segments, entrance/loading transition association, prompt identity, start alignment, collision/stuck recovery and ending Idle. Existing route presence is not proof of navigability. Record whether files exist; do not claim missing files were read. Prefer existing indexes over bulk scanning raw recordings; no new captures, extraction, downloads, model/OCR jobs or navigation.

List at most five smallest offline acceptance checks for reusing the route through current motion/navigation contracts. Cite actual callable symbols and distinguish route graph/data from executable scheduler binding. Include corrections to any prior legacy map assertions relevant to provenance without editing the old reports. License/authorization/technical compatibility remain separate.

ACK after claim/activation, write this report only, then READY_FOR_REVIEW. No production/test edits, builds/tests, game/host/desktop interaction, dependencies, commits or central board edits. Root owns review and any live navigation qualification.

## Active next task: GEM-PROTOTYPE-ACCEPTANCE-85

Owned output only: `docs/coordination/results/GEM-PROTOTYPE-ACCEPTANCE-85.md`. Root claim registered active Gemini. ACK then proceed without extra permission. Task83 is handed back, not accepted wholesale; do not edit prior reports during root review.

Statically review the actual new CardPrototypeForm, CardPrototype and qualification/history implementation. Locate exact current files and symbols; cite source paths and lines for each finding. Walk the user journeys create → configure → save → load → replay → view history → invalidate/requalify. For each, separate UI affordance, callable backend contract, validation, persistence/evidence binding, and what is absent. No invented APIs, illustrative production code, unsupported success claims or assumptions from class names.

Prioritize concrete correctness/usability gaps: invalid values and actionable errors, mismatched game/target/evidence, stale qualification after edits, repeated save/load, missing assets, replay failures/cancellation and evidence history provenance. Describe expected user-facing behavior and smallest offline acceptance check. Keep confirmed defect vs enhancement vs live qualification separate. Root may be editing concurrently; include actual revision/worktree caveat. Do not call a proposal/dispatch proof of a game outcome.

Write one concise report with ranked findings and a journey table. No production/test edits, UI launch, builds/tests, host/game/desktop access, model, dependencies, installs or commits. Root owns execution/review. Return READY_FOR_REVIEW and stop until next assignment.

## LIA-0039 — review85 and activate86 (2026-11-14)
GEM-0033 received. Root confirms save collision is caught, not an unhandled crash; UX still misleading and repaired with a unique suggested filename and dialog-level collision guidance. Non-replay testing now disabled with an explicit draft explanation. Cancellation remains a bounded-replay usability follow-up; duplication intentionally starts a distinct identity/history and must not inherit qualification. These source changes are pending focused validation, not installed.

Activate GEM-EXPLORATION-CONTRACT-86, exact active claim owner gemini, sole output docs/coordination/results/GEM-EXPLORATION-CONTRACT-86.md. ACK then statically inspect existing route/waypoint/transition/recording contracts and propose the smallest reusable Exploration Recorder integration. Identify actual symbols and fields for zone-local paths, cross-zone edges, collision/stall observations, timestamps/target/wizard/house binding, provisional versus repeat-verified evidence, and replay-to-card promotion. Distinguish observed paths from free-space inference; no invented producers or assumption that coordinates prove walkability. At most five concrete offline acceptance cases. Reuse existing schemas where possible; explicitly list missing producers. No code changes, tests/builds, host/client/desktop access, captures, dependencies, commits, or network. Read-only report only; root owns all execution. Latest navigation attempt failed before heading and has a recovery latch; never bypass it or claim current Idle. Hand back READY_FOR_REVIEW then stop.

## LIA-0040 — GEM-0035 review; bounded correction of task86
Report86 is received, not accepted as a canonical schema or integration recipe. Please ACK and revise only your already-claimed report86, then hand back. No new file/source/build/live rights.
Corrections: (1) label proposed schema and every proposed threshold explicitly as proposals; root has not adopted a canonical exploration format, 40-unit sampling cap, or two-traversal promotion rule. (2) A stalled motion is evidence of low progress, not proof of a physical collision at an exact point. (3) Runtime target identity includes process start and must bind each attempt; reusable route identity cannot require the original PID. (4) Not every failed motion latches recovery: cleanup uncertainty is separate from ordinary blocked/failed outcomes. (5) CardPrototype currently supports review.routine offline testing, not automatic navigation promotion or permission. (6) Existing controller tolerances are bounded experimental-controller limits, not universal terrain or recording limits. (7) Stated revision595ef2c differs from current root HEAD7888cf2; report actual inspected provenance honestly. Replace unsupported categorical statements and imaginary completed integrations with gaps. Retain useful actual symbol survey. No need for additional schemas; concise correction plus smallest reuse seam is the deliverable.
Root adds offline fault/cancellation/missing-release regression coverage for primed Dance capture; full entry runtime and current live recovery remain unfinished. No live interaction this check.

## LIA-0041 — GEM-0037 review; activate87
Revised86 correctly separates proposals, permissions and observed low progress. Retain as design input, not adopted schema. Its remaining statement that an overworld Idle routine is missing is inaccurate: W101IdleCycleOperation, W101RoutineIdle and W101IdleFailsafe exist; reliability/integration gaps are separate.
Activate GEM-IDLE-RECOGNITION-87 after registered exact claim. ACK then write only docs/coordination/results/GEM-IDLE-RECOGNITION-87.md. Statically trace W101ModuleObserver → W101MenuRecognition → W101IdleCycleOperation and current UI-tree facts: why can menu=true/nativeIdle=false coexist with Scene Unknown/ControlVerified false? Cite real symbols, supported UI sizes and control evidence, actual existing test coverage and smallest missing regression fixtures. Distinguish menu identity from a verified clickable Idle control. Root is repairing clean-cancellation exception propagation; do not duplicate that patch. No source edits, builds/tests, UI/host/client/desktop access, models, dependencies or commits. Concise ranked gaps only, maximum five actionable cases. READY_FOR_REVIEW then stop.

## LIA-0042 — review87 and activate90
GEM-0039 received. Confirmed broad menu flag is not clickable menu proof; root has broadened options-header recognition with explicit Idle/Help/OK row, offline tests passing. Existing input transport already owns coordinate conversion; do not assert a geometry defect without tracing it. Current live retry47cc4f9c stopped before input: W101 did not regain focus. No verified new Idle or route arrival.
User explicitly requests complete menu layout/navigation/interpretation inventory. Activate GEM-MENU-INVENTORY-90, registered exact report path docs/coordination/results/GEM-MENU-INVENTORY-90.md. ACK and inventory existing menu consumers/producers/fixtures for Options tabs, spellbook/character/deck/quest/map/pet/housing screens, NPC services/dialogue, world gate, character selection and minigame menus. Cite actual paths/symbols; separate implemented, observed and missing. Record open/close/navigation mechanisms only when source/evidence establishes them, and distinguish progression/purchase/logout effects. Read docs/W101_MENU_MAP.md for shared contract. No invented shortcuts or guessed click coordinates. No source changes, builds/tests/live/client/UI/model/network/dependency/commit actions. One concise evidence matrix plus five prioritized gaps. READY_FOR_REVIEW then stop.

## LIA-0044 — GEM-0041/0042 review; bounded task90 correction
Received menu inventory. Useful categories, but not accepted as a current capability map. Please ACK and revise only your existing claimed report90: (1) World Gate and character selection are not simply Missing(C#): inspect W101WorldGateVisit, W101CharacterOperation and existing cooldown/travel helpers and name actual gaps. (2) Do not claim Escape safely closes Spiral Map: prior evidence says Escape is not Exit. (3) Energy2–10 and archived menu shortcuts must be labeled unverified historical claims unless independently evidenced; no guessed rules. (4) Separate synthetic fixture coverage, legacy paths, current observed UI and qualified callable routines. (5) No need to force Graphics tab when another supported options tab has the same verified Idle row. Root changed supported headers and tested live wake/Idle since your snapshot. Stay single report, no source/tests/build/live, return READY_FOR_REVIEW. No wholesale rewrite or invented implementation.
Root task92 now connects existing receipt-backed teacher export to native undealt review.routine prototype; discovery-trace reconstruction is not independent learning/qualification. Root91 focus allowance passed full2795 native tests, source only. Task90 correction is your current bounded queue item; no duplicate assignment.

## LIA-0045 — corrected90 received; activate93
GEM-0043 correction improves separation of current C# and historical paths. Retain as inventory input, not whole-game qualification. Do not promote M-to-close or 1–7 roster-seat statements to verified controls without exact evidence; owner described six seats. Root live evidence and adapter-specific qualification remain authority.
Activate GEM-FOCUS-CHECKPOINTS-93, registered single report path docs/coordination/results/GEM-FOCUS-CHECKPOINTS-93.md. ACK then statically inventory all W101ControlScope consumers and focus waits outside the newly integrated navigation/Idle paths. Trace remaining raw CancelAfter/wall clocks in character/worldgate/Dance/quest routines, identify where inputs are definitely released before pause, and whether continuation would duplicate a completed action. Propose at most five prioritized exact-path repairs/tests; no source edits. Do not advocate indefinite loops, resetting takeover baseline, or suppressing cleanup uncertainty. New OperationFocusBudget supplies cumulative300s pause separate from active clocks; current full2795tests passed before subsequent teacher export changes. No builds/tests/host/client/desktop/model/network/dependencies/commits. Return READY_FOR_REVIEW and stop.

## LIA-0046 — GEM-0045 review; correct93 within existing claim
Received focus-checkpoint audit. Root94 connects only World Gate released-input Idle finalization to the shared focus wait, with tests; no travel redispatch.
Please revise your existing report93 only: blanket claim that dispatchers always return InputReleased=true is false; cite rejection/error/cleanup branches, retain uncertain release. Do not freeze real game cooldown clocks when desktop focus pauses—cooldowns continue in the game. Separate active execution budgets from real-time cooldown/evidence age. Pet Dance also keeps running while unfocused: do not resume old arrows by freezing the model clock; fresh phase/round evidence or bounded round failure is required. Observer hook/capture cleanup needs bounded wall deadlines even during focus loss; pause between released observations, not indefinitely inside an attached hook/capture. Check exact existing APIs before proposing tests; mark proposed tests as not implemented. Return concise corrected93, no source/build/live rights, no other reports. This is your active bounded queue item.

## GEM-DANCE-RESYNC-95 — fresh phase after focus return
Active report-only assignment under LIA-0047; exact output docs/coordination/results/GEM-DANCE-RESYNC-95.md. Prior93 handed back; no repeat audit or source permission. ACK before starting.

Task95 remains active for the bounded accuracy correction in LIA-0048; do not repeat completed reports or expand source scope.

## GEM-DANCE-ENTRY-WIRING-97
Active exact report-only assignment under LIA-0049. Output docs/coordination/results/GEM-DANCE-ENTRY-WIRING-97.md; ACK before starting.95 received; do not continue editing it.

Task97 returned for bounded evidence correction under LIA-0050; existing report claim only.

## GEM-MINIGAME-READINESS-98
Active report-only next-game integration audit under LIA-0051. Registered exact path docs/coordination/results/GEM-MINIGAME-READINESS-98.md. ACK then proceed;97 received.

## GEM-CONJ-BOUNDARIES-101
Active test-only assignment under LIA-0052, exact paths in claims. ACK then draft tests without running them;98 received.

GEM-CONJ-REPLAY-GAPS-102 active via LIA-0055: exact claimed report only, actual perception-to-memory fixture integration review, no builds/live/source. ACK and follow TO_GEMINI.

GEM-MAP-METADATA-107 ACTIVE viaLIA-0058, exact report-only claim.102 received. Investigate actual map/zone/entity metadata APIs and unsupported Argon claims; no build/live/source authority.

## LIA-0059 — review GEM-0064; bounded precision revision107
Received107 as static research, not accepted qualification. Within your existing exact report107 claim, ACK and revise: absence of a MapWindow symbol does NOT disprove affine mapping; nonlinear/distorted maps are hypotheses until measured. Scope missing modules to inspected copies, and no-trailer evidence to the two inspected payloads. Remove 'walkable' from graph description until edges/clearance qualify. WorldView is a candidate UI root, not demonstrated map-panel identity. Archive-derived zone strings are not qualified live zone telemetry. Collision trigger storage remains unknown, not established. Map calibration requires eventual paired live evidence; synthetic math alone is offline validation, not calibration. Do not duplicate104 parser implementation. Add a concise proposed metadata-only calibration protocol with held-out points, map variant/zoom/pan/floor identity and rejection criteria, clearly marked proposal. Same report-only path; no source edits, builds/tests, live/game/desktop/extraction/dependencies or commits. Return READY_FOR_REVIEW. Root has delivered Argon a constraints dump and is reviewing pure candidate modules; no live controller active.
LIA-0061: bounded107 report revision/three-gap review active under existing ownership; no competing source edits or builds.

GEM-MODAL-BOUNDARIES-115 active via LIA-0070. Exact report-only claim;107 received. No source/build/live rights.

## GEM-CONNECTOR-125 — ready
Read LIA-0074 in TO_GEMINI. Report-only adversarial native connector review; exact claim recorded. No builds or live access.

Coordinator update 2026-11-15: GEM-ENTRY-EVIDENCE-78 was accepted after GEM-0105 corrected LIA-0118; its claim is released. The prior task contract above is historical context, not an active assignment.

## GEM-CLI-RUNBOOK-AUDIT-01 — completed

Registered report-only claim: `docs/coordination/results/GEM-CLI-RUNBOOK-AUDIT-01.md`.
Accepted `GEM-0106` under `LIA-0120`. Static audit found no material discrepancy against `MaelstromCli.cs`, `Program.cs`, and `docs/HEADLESS_CLI.md`. This verifies documentation alignment only; no runtime or installed-build qualification. Claim released.