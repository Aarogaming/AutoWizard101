# Game-player research protocol

Updated 2026-11-08. This is the working protocol for Maelstrom experiments,
including the private slow-speed Dodge study. It does not imply that every
historical experiment satisfied all of these practices.

## Before a study

1. State the question and primary outcome. For Dodge the current target is
   repeatable mean game score of at least 50 over multiple complete games,
   alongside median, lower scores and the number at or above 50. The score is
   the game's survival score, not a verified count of enemies defeated.
2. Freeze the candidate, baseline, game and test settings. Reserve distinct
   whole-game seeds for collection, development and final evaluation before
   fitting. Fix the game speed, actions, test count, stop conditions and resource
   budgets; retain the exact artifacts and hashes.
3. Identify what the player observes and what is available solely to the test
   evaluator. The current isolated Dodge policy sees rendered frames; its audit
   can supply score and terminal evidence to offline analysis, not the live
   policy. Do not read an active game's audit to steer that game.
4. Specify the comparison and resource limits. A seed can align openings but
   different actions can produce different later encounters; it is not a
   promise of identical gameplay. Run at most one private game at a time unless
   a separately measured budget permits more.

## During and after a study

- Record observation timing, proposed and sent actions, receipts, observed
  outcomes, model/pack versions, resource limits and exact stop reason.
- Record incomplete, unknown, interrupted and censored episodes as such. Do
  not replace a failed test with another run or average only successful games.
- Tune only on the declared development data. Once a final seed, image or
  outcome is inspected for tuning, reserve a new final set for the revised
  policy. A short screening result is not final evidence.
- Report the full score list, sample size, mean, median, minimum and count at
  or above the target; include uncertainty where useful and state its
  assumptions. A few long games can carry an average. No finite random sample
  establishes certainty.
- Separate statistical association from causation. A recorded later outcome
  reflects subsequent actions and random encounters; an action-match diagnostic
  is not a counterfactual or full-player trial. Keep outcome sources and
  proxy rewards explicit.
- Preserve negative and inconclusive results, code and model hashes, resource
  limits, raw local evidence paths and commands. Do not install or promote a
  research policy merely because one numeric mean crosses a threshold.

## Current experiment index

| Study | Evidence and result | Scope |
| --- | --- | --- |
| Earlier guarded policy | `.local/dodge-benchmarks/` and [learning log](DODGE_OFFLINE_LEARNING.md): ten scores **37, 168, 2, 29, 6, 31, 140, 44, 23, 18**, mean **49.8**. | Private 25% stepped; large variation. |
| Action-contrast, initial final set | `.local/dodge-curricula/outcome-auto-ccdc113f21824c1f98784eb4779cb05b/round-1/final/summary.json`: six complete games then an unrecognized player; no ten-game mean. | The exposed failure frame became a tracker regression; do not reuse that set for revised-policy validation. |
| Stronger tracker, guarded model | `.local/dodge-curricula/outcome-auto-44e5de6cb23a4d00ade014a5ada8e844/round-1/final/summary.json`: **24, 130, 34, 32, 28, 27, 8, 14, 35, 218**, mean **55**, median **30**, two at or above 50. | All ten ended in game over. The empirical mean crossed 50, but eight early losses and a wide distribution do not establish repeatable performance. |
| Fresh experience and episode fit | `.local/dodge-curricula/outcome-auto-a004f089c2d34bd7a87b5df5f6541fc1/round-1/experience/summary.json` and `.local/dodge-episode-models/e8573571ed9c461e8005b7e72a11e3c1/training_report.json`. | Sixteen randomized games; eight train and eight held out. The return-based model's held-out action matches did not show an advantage in this small observational sample. It is research-only. |
| Earlier episode-model comparison | `.local/dodge-curricula/fallback-8ce2fde595724131b502e26837d6cac9/study.json` and `lifecycle-review.json`. | Interrupted after three baseline games (70, 10, 22). The original status was stale; no four-game mean or candidate comparison is available. |
| Native workbench pilot | `.local/experiment-workbench-data/experiments/20261108-055448-5aba7db633e4/`. | Four fresh matched openings: baseline **20, 7, 11, 124**, mean **40.5**; episode candidate **20, 7, 11, 26**, mean **16**. All eight completed. Candidate rejected; both reserved ten-game validation sets remained unused. |
| W101 Dance fade replay | [Validation study](PET_DANCE_FADE_VALIDATION_20261108.md); `human-route-01/fade-study/replay-04` under `.local/w101-route-study/`. | 1,290 reviewed frames, five complete proposals, four Success/one Failure; calibration and replay use the same human recording. No autonomous result claimed. |
| W101 Dance live attempt 02 | `.local/w101-route-study/live-teacher-02/`. | 230 frames over 12.176 seconds, zero arrows/results. Loading observed; the short sample did not establish a stalled game. Original CleanupFailed receipt preserved; later recovery recorded separately. |
| W101 Dance live attempt 03 | `.local/w101-route-study/live-teacher-03/`. | The game started around the old cutoff, then reached its result screen. Later supervised native Idle return recorded separately from the original CleanupFailed receipt. No autonomous play. |
| W101 Dance loading-aware attempt 04 | `.local/w101-route-study/live-teacher-04/`. | 735 frames/45.007 seconds, 93 Loading and 642 Unknown, 598 content changes; all cues unknown and zero arrows/results. Native Idle visually confirmed within cleanup. Completed receipt describes cleanup, not a won game. Continuous intermediate gameplay evidence is missing. |
| W101 Dance full-visit observation | [Full-visit study](PET_DANCE_LIFECYCLE_OBSERVATION_20261109.md); `.local/w101-route-study/lifecycle-01/`. | Verified 130.13-second video, supervised menu controls and user-confirmed demonstration arrows; audio off; native Idle confirmed. Original frozen profile: 1,650 panels, five proposals, three Success/two Failure, no rejection; three input matches and two rounds without arrows. This is fresh-recording evaluation, not autonomous play. |
| W101 Dance preview-sound mapping | [Measured mapping](PET_DANCE_SOUND_MAPPING_20261109.md); `.local/w101-audio-study/dance-02/`, `dance-03/` and `comparison-02-03/`. | 18 visual/audio pairs cover all four directions. Nine whole-round-withheld predictions match, zero Unknown; both recordings partly calibrate the same-client study. Six deliberately unanswered rounds, native Idle after both visits. No audio-only detection or autonomous-play claim. |

Local `.local` evidence is not packaged with the application and may be removed
by the owner; a document entry alone is not a replacement for its raw reports.
Detailed commands, negative results and earlier trials are in the
[Dodge offline learning log](DODGE_OFFLINE_LEARNING.md).

## Remaining research infrastructure

The [human W101 route/Dance study](W101_DEMONSTRATION_20261108.md) preserves a
source-hashed 147.95-second recording, observed input holds, capture timing,
assistant-reviewed visual outcomes and native Tiny UI calibration. Its seven
selected calibration frames are not independent validation. The subsequent
[fade study](PET_DANCE_FADE_VALIDATION_20261108.md) closes the complete recorded
sequence gate; independent live perception and exact input-to-video alignment
remain open. No human-play success is reported as autonomous Maelstrom success.

Parallel research adds [pet snack mechanics](W101_PET_SNACK_RESEARCH_20261108.md),
[installed asset indexes](W101_LOCAL_INSTALLATION_SOURCES.md) and a tested
[knowledge-store schema](GAME_KNOWLEDGE_DATABASE.md). Those are sourced research,
local metadata and database integrity evidence respectively, not qualified game
actions. The schema is not yet a populated application database.

The [W101 operating study](W101_GAME_RESEARCH_20261108.md), dated 2026-11-08,
adds a non-executable [source and observation ledger](../research/w101-context-20261108.json).
It distinguishes publication date, feature availability, region, platform,
Live/Test Realm and local observation. Older omissions are treated as possible
chronological evolution, not automatically as contradictions. Undated pages
remain undated; a recent crawl does not establish a recent mechanic. The study
contains no live Pet Dance accuracy or navigation-success claim.

The existing study code fingerprints game, application, bridge, pack and
training inputs and reserves holdout seeds. The source now records an audit hash
after a private run and both offline outcome/episode loaders verify it when
present; unit tests cover digest calculation and changed-audit rejection. Older
reports with no digest field remain explicitly marked unsealed, while a new
report with an unavailable digest is rejected for training. The native workbench
pilot exercised the new digest path in eight complete private games: report,
audit and terminal image were copied and verified before ranking. Older reports did **not** record audit
hashes when captured, so later hashes identify the bytes read but cannot prove
they are unchanged since collection. The [native workbench](EXPERIMENT_WORKBENCH.md)
now browses its own plans, compares results and preserves decision receipts and
footage. Historical tool studies remain in the research index rather than being
automatically imported. Reliable executable replay across application updates,
automatic candidate training from the app, and system-level resource/latency
measurements remain open.

The pilot reserved distinct openings in its immutable plan before play. Its
post-run registry reconciliation found zero overlap with prior reserved seeds
and added all 24 openings to the shared registry for future exclusion. Subsequent
workbench plans reserve through that same locked registry before publication.
