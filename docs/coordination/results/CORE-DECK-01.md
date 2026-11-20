# CORE-DECK-01 — Shared cards, deck, native UI and API

Coordinator checkpoint: 2026-11-11. Source implementation; not packaged or
installed in this task. No game input, live W101 attachment, capture/model job,
volume change or desktop control was used.

## What changed

- Generic data-only card/deck contracts over the existing OperationScheduler.
  Conditions, exact include/exclude scopes, finite copies/attempts/cooldown and
  whole-run deadlines. Deferred cards spend no copies; failed runs pause.
- Collection is distinct from the active Routine Deck. Native front/back editor,
  reversible flip animation, original opposing-corner vector art, template
  duplication, configuration import/export, progress and waiting explanations.
- Registered Offline Replay and the existing W101 selected-conversation adapter.
  Quest auto-fill derives immutable context from fresh observations. Other cards
  expose actual preparation status, not a pretend autonomous player.
- Generic objective parser: destination, exploration, talk, defeat, collect and
  defeat-and-collect with bounded count suffixes. Fireglobe and Fire Elf examples
  are fixtures, never quest-name branches. The newest user-reported objective
  supersedes the earlier destination; no navigation to that stale target ran.
- Current-user-only local pipe, headless host/client, and MCP bridge to the same
  owner. Existing optional HTTP API now proxies authenticated calls to it.
- Durable request deduplication, pinned pending-start revision, bounded summaries,
  tombstone counters, queue-inclusive deadlines, owner exclusivity, stop/drain,
  interrupted-start handling and persistent uncertain-outcome/storage holds.
- Per-operation `job-card.json` preserves admitted configuration, revision and
  observations beside the existing receipt and adapter outcome evidence.

## Gemini review and integration

The supplied first API implementation returned invented starting vitals and
completed action descriptions without dispatch. MCP also returned constants;
native standard-card stubs returned Completed without observing outcomes. These
were replaced by the real owner bridge or explicitly unsupported legacy execution.
Unavailable facts are unknown; an absent native owner returns an error.

Gemini subsequently delivered original vector artwork, plus an additional
`GenericMacroDeck` prototype during testing. The user froze core edits to allow
integration. The artwork is integrated, with bounded clipping/finite geometry,
font resource cleanup, and corrected provenance (the exact game font is unverified).

The alternate prototype and its four tests are preserved. Native compilation
explicitly excludes the prototype; the test project links its original source
and runs the synthetic tests. It has no scheduler/lease/budget/outcome contract
and is not a second production controller. A transient full-suite failure
compiled an earlier reshuffle revision; the frozen-source rerun passes. The
coordinator did not change its algorithm or suppress those tests.

Another full-suite run exposed an existing Dodge permission-revocation timing
race in the **test**: its delayed thread could resume after the pulse ended.
The test now revokes at the fake sink's actual key-down boundary and still checks
interruption and key release. The production Dodge input implementation was not
changed.

A subsequent shipping-check run passed 1,858 tests but failed one forced
fake-worker test during temporary-directory disposal. The worker checks passed
on isolated rerun and no task worker remained running. The fixture now records
its PID and asserts that the actual child has exited before the terminal receipt;
temporary-directory disposal retries only IOException for at most one second.
Production worker behavior is unchanged. The strengthened full-suite rerun passes;
this does not hide an unconfirmed live-client disconnect.

## Validation evidence

- Final native integration: **1,859 passed, 0 failed, 0 skipped** in
  `.local/job-deck-evidence/deck-final-with-worker-exit-check.trx`, including the
  direct worker-exit assertion. Earlier passing and failed reports remain intact.
- Native warnings-as-errors build succeeds. Scoped formatting/analyzers checked;
  final UI-only renderer/picker repair also builds with zero warnings/errors.
- Python: **15 bridge tests + 9 MCP tests** pass; Black single-worker formatting
  and syntax checks pass. Mocked tests do not claim live game access.
- Optional MaelstromBot.Server builds with warnings as errors. Fixed route
  handler return binding and preserved its existing SQLite dependency's legacy
  RID mapping using the SDK's compatibility option; no dependency was added or
  upgraded. Its background GitHub/OpenAI services were not launched.
- Actual built-app headless check:
  `.local/job-deck-evidence/headless-built-app.json`, operation
  `a2157a810a284eccbdd494858ead71fa`: one synthetic Dodge replay, one attempt,
  one verified completion, duplicate request suppressed, initially unknown live
  facts, real MCP → CLI → pipe → native-owner response, orderly stop-file exit.
  Zero game inputs. This is an offline replay, not a live Dodge game.
- Hidden native rendering validates the front and actual timer-driven flip to
  settings. `.local/job-deck-evidence/cards-verified.png` includes readable native
  menu pickers and Gemini artwork. Earlier previews/failures remain preserved.
  The first hidden-render shutdown stalled and only its verified process was
  terminated; corrected rendering exits cleanly without showing a window.

No full correctness or reliability guarantee follows from finite tests. User
data, game installation files and installed Maelstrom were not modified.

## Remaining work

1. Qualify actual combat observations, targeting and outcomes; existing planners
   alone cannot complete the two remaining user-reported Fire Elves. Gemini is
   assigned [W101-COMBAT-OBS-02](../tasks/W101-COMBAT-OBS-02.md).
2. Map-based navigation needs current localization/road/threat observations and
   an owned native executor. Arrow distance may increase on valid detours; old
   Python steering prototypes are not admitted live routes.
3. Connect complete Dance visits, maintenance/Idle and other minigame adapters
   through the same registry, preserving their individual qualification gates.
4. Extend pack-driven game composition and themes; no Minecraft or Combat Arms
   player exists yet. Shared engine contracts remain game-independent.
5. Guided multi-condition/multi-scope editing, deck-specific recovery of ambiguous
   outcomes, persistent multi-deck workspace management and accessibility polish.
6. Package/install this checkpoint and separately qualify the installed workflow
   when assigned. Hand/discard/reshuffle/ward UI metaphors remain design work.

See [JOB_DECK](../../JOB_DECK.md) for usage, API contracts and current limits.
Subagents supplied transport tests, recovery tests, navigation preflight/parser,
and repeated read-only lifecycle reviews. Root retained serial build and
integration ownership. No task-owned controller remains running.
