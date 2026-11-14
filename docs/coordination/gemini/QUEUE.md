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
