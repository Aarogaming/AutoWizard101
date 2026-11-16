# Producer stream binding — 131

2026-11-15. Offline source change; no new live check or installation. Olde Town
observation and local floor match remain recorded in W101-OLDETOWN-130.md.

## Gap repaired

The producer formerly reused the session token `bounded-pose-window` and emitted
no explicit stream instance or sequence. Each pose invocation now creates a UUID
stream token used in the moving-reader context and emitted sample envelope.
Samples carry increasing sequence numbers plus the existing request run ID and
target identity. The native `DecodeBoundStreamCandidate` validates expected run,
stream, process ID, process start ticks, window handle and exact next sequence
before the existing age/zone/coordinate decoder. Expected identity must come from
the owner, never from the same incoming packet being checked.

Worker/native/review hash pins updated together. Ten Python owner-lifecycle tests
pass. Of83 UI tests,82 passed initially and the worker-pin test caught the old
pin during the update; that exact failed test passed after pins were synchronized.
No live qualification flags were changed.

## Remaining ordered work

1. Connect the bound decoder as the authoritative consumer under the same owner,
   with lifecycle event invalidation and stored sequence state.
2. Establish authoritative zone/wizard identity and loading epoch; a UUID per
   invocation does not detect unobserved loading between samples.
3. Bind collision roles and profile dimensions to asset-scoped evidence. Olde
   Town street279 is locally corroborated, not a global role classifier.
4. Replay recorded Olde Town evidence through the integrated consumer, then run
   another bounded observation before authorizing motion. Grizzleheim secondary.

Gemini's proposed any-reachable-seed relaxation was not adopted: ambiguous
overlapping floor candidates require anchor evidence, not permissive selection.
LIA-0080 requests review through the existing report-only claim.
