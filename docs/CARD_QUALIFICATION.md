# Custom-card qualification

This component is offline groundwork, not yet connected to the native card editor or deck admission. New cards have no qualification evidence. Catalog adapter availability, a card being dealt, and matching requirements do not establish that a custom card works.

`CardQualification.Bind` constructs an exact binding for card content, the trusted execution graph, routine ID, adapter version, scope and fact definitions. The adapter/compiler must supply the expected execution graph hash. It must not take whichever graph the user selected for a successful replay as proof of the card's implementation. Scope must cover relevant game/build, assets, calibration and environment restrictions. This trusted UI/compiler association remains integration work.

`RecordReplay` accepts a local native replay report and its source graph/session. It verifies the report's native schema, exact hashes, condition checks, accepted replay counts, recorded completion, zero desktop dispatch and lack of live qualification. It freezes the card, fact schema, graph, manifest and events, then runs the existing `RoutineReplay` over those frozen inputs. Only a reproduced successful report with the same hash publishes `evidence.json`. A caller-provided success flag cannot replace missing receipts or outcomes. No capture, game attachment, input owner or model is available here.

Each record uses a new evidence directory and exclusive file creation. Existing evidence is never overwritten. The manifest is published last; failed imports may leave incomplete directories, which remain unqualified. Do not delete existing reports automatically. Artifact hashes detect changes; this is a local evidence system, not a cryptographic authentication boundary against the owner rewriting every artifact.

`Assess` checks the current binding and every frozen artifact. A changed card, graph, adapter version, scope or fact schema yields **Unqualified**, retaining historical evidence. Missing, altered or unsupported artifacts also remain unqualified. It replays copies of the just-verified bytes in a bounded temporary directory and compares the resulting report; editable hash claims and a forged `completed=true` cannot substitute for replay. Temporary files are removed in `finally`; durable evidence is never changed by assessment. This is an explicit review operation, not a per-frame or one-second scheduler check.

Stages are deliberately distinct:

- **Unqualified:** no matching usable evidence, or evidence became stale through a content/context change.
- **ReplayPassed:** the exact configured execution graph reproduced a condition-bound recorded outcome with at least one action and matching receipt. Synthetic fixtures can establish this offline stage; they cannot establish live behavior or visual-recognition quality.
- **LivePassed:** reserved. No recognized live-verifier implementation or promotion entry point exists yet.
- **Repeatable:** reserved. Re-reading or duplicating one replay never reaches this stage. Future promotion needs distinct qualified live runs within an explicit repeatability rubric and scope.

Qualification never grants execution permission. `GrantsExecutionPermission` is always false; runtime availability, current consent, fresh facts, target checks, scheduler ownership, cancellation and cleanup still apply. There is no editable `qualified=true` field, and unknown imported stage assertions are rejected.

Remaining integration: native card badges/history and a guided review flow; compiler-to-expected-graph association; deck readiness rules for custom cards; recognized live adapter receipt/outcome verifiers; explicit invalidation notifications; repeatability rubrics. The current `RecordedCardReview` remains a requirements check. An unrelated selected graph or generic successful operation receipt must not promote a card.

Tests are in `CardQualificationTests.cs`: fresh/unqualified state, immutable reopen, changed bindings, altered/missing artifacts, wrong graph association, forged success without receipts, fact-schema changes and rejected imported live-stage claims. The coordinator owns native build/test execution; no installation or live qualification is implied by this source addition.
