# Routine foundation and project organization — validation checkpoint

Date: 2026-11-09 session (some local evidence timestamps are 2026-11-10 UTC).

## Delivered source

- Shared data-only routine graph and native offline replay, with the existing Pet Dance postgame adapter using the graph. Other live input paths are not replaced or enabled.
- Draft full-visit Dance and repeated-movement Dodge graphs. Their synthetic session fixtures reuse the existing game-session v1 envelope and pass its Python reader.
- Bounded fresh-frame reacquisition with menu analysis limited to 2Hz; the 300ms freshness gate remains. Final capture/cleanup confirmation now has separate finite deadlines and respects cancellation.
- Conjuration's offline memory helper, including unknown/delayed outcomes, explicit board resets and conflict preservation. It cannot recognize a screen or click a card.
- Project organizer, current roadmap, machine-readable workstream board and validator, file-ownership claims, updated root AGENTS instructions, contributor guide, template and five external task briefs.

## Validation

The integrated native suite passed **1017/1017**, zero skipped, with warnings treated as errors and `DOTNET_PROCESSOR_COUNT=2`. Scoped native whitespace verification and analyzer checks passed. Full-suite test duration was approximately 6 seconds; this is not a gameplay speed or capture-throughput measurement.

The recorded command was:

```text
dotnet test ProjectMaelstrom/ProjectMaelstrom.Tests/ProjectMaelstrom.Tests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -warnaserror -v:q --logger "trx;LogFileName=routine-foundation.trx" --results-directory .local/routine-foundation-20261109/tests
```

The first restricted-host focused run stalled and was stopped; the working local test host then passed focused and complete checks. No test failure is hidden by that environment retry.

Independent review found and repaired cross-run receipt reuse, post-completion invalidation, a Conjuration conflict cleared by a late receipt, and reappearing removed cards under an unchanged board ID. Regression tests cover all four.

Conjuration's **21 scoped Python tests** pass, including 60 seeded pair/triple board simulations. The organizer's **22 scoped validator tests** pass, and the actual board passes validation. Black formatting (one file at a time) and Python compilation checks pass for both modules and their tests. Pyflakes is not installed in the inspected environments; no Pyflakes pass is claimed and no dependency was installed.

Actual native CLI rehearsals produced:

| Fixture | Proposals | Supplied synthetic receipts | Completed contract | Desktop dispatch |
| --- | --- | --- | --- | --- |
| Full Dance visit | 8 | 8 | Yes | 0 |
| Dodge changing movement choices | 6 | 6 | Yes | 0 |

Evidence is under `.local/routine-foundation-20261109/`: `dance-replay.json`, `dodge-replay.json`, and `tests/routine-foundation.trx`. Replay reports contain graph/session/event hashes, explicit `labelled-state-replay` provenance and `live_qualified: false`. Repeat determinism, missing receipts, rejected receipts, invalid input and post-completion takeover are also covered in native tests.

## Limits and next qualification

These are source changes, not a newly installed release. The previously installed repair remains the installed version. No new W101 control, capture session, model process, volume change, feeding or installation occurred during this work. The user's report that W101 was returned to native Idle is retained as user-reported evidence.

Synthetic replay demonstrates the transition contract, not current-client visual recognition or a complete autonomous game visit. Entry/level/Play adapters, navigation landmarks, native-resolution live qualification, Conjuration perception, editable routine UI and repeated autonomous Idle returns remain separate workstreams. The latest actual installed Idle trial admitted zero frames; the source cadence repair still needs a fresh authorized live test.

Use [Project organization](PROJECT_ORGANIZATION.md), [the workstream board](coordination/workstreams.json) and [the roadmap](ROADMAP.md) for future assignments. These results do not close Dodge's average-50 target or claim other legacy programs are integrated.
