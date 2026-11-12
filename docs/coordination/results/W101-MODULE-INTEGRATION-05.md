# W101 concrete observation and input integration

2026-11-11. Coordinator plus parallel input/observer and prompt-evidence work.

This increment closes concrete seams discovered when reviewing the previous
module bindings. It does not establish complete W101 gameplay automation.

- `W101ModuleAdmission` freezes the real reader, helper, interpreter and selected
  game-image pins into the existing module request. No nested scheduler or lease.
- `W101ModuleInput` provides only the registered X and Escape pulses, addressed
  to the selected process instance, with takeover checks and mandatory release.
  Message delivery remains distinct from a game response.
- The UI reader exports bounded current/stable interaction titles from the exact
  visible NPC prompt path. These are source-backed candidates, not permission to
  interact. Saved original traces corroborate that path for Dance Game and NPCs.
- `W101ModuleObserver` connects the actual owned reader, selected-window capture
  and existing local OCR. It preserves capture timestamps and control rectangles;
  missing outside-state evidence is not inferred from absent menus.
- `module-observe` exposes one observation through the CLI. It has no input
  dispatch and no navigation, and preserves evidence for the next qualification.

## Verification status

Initial focused native checks: 59 passed. Final integrated native suite: **2,173 passed**,
zero failed/skipped, warnings-as-errors. Python UI reader: **50 passed**, Black and
compilation passed. Scoped whitespace formatting/verification and analyzers pass.
Final receipt: `.local/w101-module-integration-05/module-final-05.trx`.

The user authorized one observation-only probe. Its real reader observed native
Idle with two UI trees, zero tree failures, hook restoration and clean disconnect.
The combined probe then rejected evidence with `InvalidDataException`; the initial
report did not retain enough diagnostic detail to establish which check failed.
Zero game inputs or focus changes; no volume change, gameplay or installation.
Failed evidence is preserved under operation
`379989fa0a264b60964cab90d1d5310a` in the local evidence root. The repair retains
bounded stage/error/timing diagnostics. Stale readings remain candidates with all
action context disabled and an Unknown result; no freshness limit was relaxed.
The repair passed 150 focused native tests.

The separately approved second probe **completed**, operation
`6f708cb16fe640fda9f61f370ad2dcb8`. Both the current UI tree and current local OCR
identified native Idle. Two UI trees, eight loading samples, zero tree failures;
hook restored and disconnected; capture disposed and worker exited. Zero inputs,
zero focus changes. Observation took 3.658 seconds; image age 220.089 ms and UI
tree age 1,934.695 ms, both within their existing limits. Actual capture geometry
was 1282 × 759 including the window frame. The source is unchanged gameplay-wise:
observed quest still reads Fire Elf arrows (1 of 3); no quest progress claimed.

The UI tree's age was close to the two-second limit. This is one successful probe,
not sustained throughput or reliable action qualification. Avoid widening the
freshness thresholds to hide slow recognition; future integration should reduce
latency and retain independently timed evidence. No installed-release claim.

## Remaining gates

Production cards still need qualified semantic context, independently correlated
post-action outcomes and supported Idle-click transport before being enabled.
Navigation additionally needs current location, a route and observed arrival;
an X/Escape transport is not a navigator. Combat, resource-aware routine switching
and complete quest execution remain distinct unfinished integrations.
