# Pet Dance delivery tracker — 2026-11-08

The native panel reader now replays all five rounds from the human demonstration,
including fades, repeated arrows and the failed human round. This is development
evidence from the calibration video, **not independent live playing
qualification**. Updated 2026-11-09: loading-aware live attempt 04 recognized
loading and captured changing content, but every gameplay cue remained unknown.
It sent zero keys. Supervised cleanup returned W101 to confirmed native Idle and
released desktop control. A complete supervised visit is now recorded and its
fresh gameplay passes frozen-profile offline evaluation. The next gate is live
geometry and control qualification. The later opt-in audio update is
[installed](GAME_AUDIO_RELEASE_20261109.md). After the user's restart, two more
visits supplied a [first measured preview-sound mapping](PET_DANCE_SOUND_MAPPING_20261109.md)
for all four arrows; nine withheld cues matched their visual labels. These were
unanswered observation games, and live audio-driven play remains unqualified.

The [validation guide](PET_DANCE_FADE_VALIDATION_20261108.md) documents the source,
profile, replay and live-trial contracts. This tracker records actual delivery
status; source edits, passing tests or scheduler receipts alone do not establish
that the game was played correctly.

## Evidence snapshot

- The [human demonstration](W101_DEMONSTRATION_20261108.md) preserves the
  147.950-second Commons route and five-round Dance game, its game-window video,
  input observations and clock/geometry metadata. The user played that game.
- [Replay 04](../.local/w101-route-study/human-route-01/fade-study/replay-04/replay.json)
  analyzed 1,290 panels, produced five sequence proposals and recognized four
  Success outcomes and one Failure. It recorded no sequence rejection, sent zero
  inputs and explicitly reports `live_qualified=false`. Mean analysis time was
  2.1473 ms per analyzed panel; maximum was 13.9221 ms.
- [Comparison 04](../.local/w101-route-study/human-route-01/fade-study/comparison-04.json)
  associates all five proposals with observed human inputs. Four match; the
  failed round differs at its fifth arrow. There are no unscorable/incomplete
  rounds. Exact decoded-video/input alignment remains unverified.
- [Live teacher 02](../.local/w101-route-study/live-teacher-02/live-report.json)
  attempted the Wizard City level. Its 230 observed frames over 12.176 seconds
  produced zero input receipts and zero outcomes, then stopped with
  `unrecognized-game-state`. App capture and independent desktop screenshots
  showed loading at the sampled times. This did not establish a stalled game.
- The [final scheduler receipt](../.local/w101-route-study/live-teacher-02/finished.json)
  is `CleanupFailed`: supervised native Idle confirmation is missing. The
  [teacher-complete record](../.local/w101-route-study/live-teacher-02/teacher-complete.json)
  reports inputs disabled. W101 was still loading, so **native Idle was not
  confirmed within that run**. Later recovery evidence is separate; the original
  failed receipt is unchanged. Key cessation does not imply native Idle.
- Attempt 03 reached gameplay around the old cutoff and later showed its failed
  result. Its separately recorded supervised Idle recovery corrects the earlier
  stalled-game interpretation without rewriting the original receipt.
- [Attempt 04](../.local/w101-route-study/live-teacher-04/live-report.json):
  735 frames/45.007 seconds, 93 Loading and 642 Unknown, 598 content changes,
  zero arrows/results. [Cleanup evidence](../.local/w101-route-study/live-teacher-04/idle-return.json)
  confirms supervised native Idle; Completed describes cleanup, not a won game.
- The prior check after geometry, audio, lifecycle and settings repairs passed
  475/475 C# tests, none skipped, and 41 Python tests. Consolidated checks are recorded in the
  validation guide. Offline tests do not close live qualification gates.
- [Consolidated validation snapshot](../.local/w101-route-study/parallel-validation-20261109.json)
  records the final counts, scoped formatting/lint checks, fresh-video results and
  hashes of the checked C# files. No new package or installation is included.
- [Full-visit study](PET_DANCE_LIFECYCLE_OBSERVATION_20261109.md) preserves the
  130.13-second recording, user attribution and verified Idle return. The same
  original references produced five sequences; three matched the user's arrows
  and Success feedback, while two unanswered rounds showed Failure. This is
  offline perception evidence, not an autonomous game or a 60% accuracy result.
- [Game-audio validation](GAME_AUDIO_VALIDATION_20261109.md) adds opt-in selected-process
  audio, packet timestamps and offline sound/arrow comparison tooling. The latest
  audio changes pass 507 C# tests and 24 focused Python tests. A five-second passive
  W101 check saved measurable audio at the unchanged user-reported 2% volume,
  without drops or game inputs. Its single source image confirms native Idle;
  encoded duplicate frames are not a gameplay-rate or cue-alignment benchmark.
- [Fresh audio visits and mapping](PET_DANCE_SOUND_MAPPING_20261109.md): 18
  visually checked preview cues, six complete unanswered rounds and confirmed
  courtyard Idle after each visit. Nine whole-round-withheld audio predictions
  matched the arrows under unchanged gates. Both recordings contribute
  calibration data and share one client; cross-session reliability remains open.
  Up and Down need spectral shape, not their shared dominant pitch. The current
  analyzer passes 25 focused tests; this followup changed documentation only.

Local evidence lives under `.local` and may be absent from a clean checkout.
Preserve reports and their hashes when exporting a study; do not rewrite earlier
results to make the progression look successful.

## Work and acceptance gates

| ID | Work | Current status / owner | Remaining gate |
| --- | --- | --- | --- |
| PD-01 | Preserve the human baseline | Complete / primary session | Retain original source/video/input hashes and reference provenance. |
| PD-02 | Recognize arrow fades | Implemented; complete replay passed / primary session | Independent sequences and geometry must exercise the same fade/trough rules before live qualification. |
| PD-03 | Separate phases and results | Implemented; four Success and one Failure recognized / primary session | Verify feedback during a fresh game. Bottom DONE means Resolving, never Success by itself. |
| PD-04 | Replay all five recorded rounds | Original development replay and fresh lifecycle-01 evaluation complete / primary session | Original references unchanged: five proposals, three Success/two Failure, three human-input matches and two no-arrow rounds. Further live/control qualification remains separate. |
| PD-05 | Qualify geometry and timing | Implicit stretch removed; explicit profile mapping implemented / primary session | Offline GDI diagnostics reproduce all-unknown under one plausible geometry. Verify actual Dance pixels at live 1282×759 against decoded 1282×760. No mapping is qualified from dimension ratios alone. |
| PD-06 | Bind live teacher to shared ownership | Bounded CLI operation implemented / primary session | Independent accepted keys and game feedback remain unverified; retain takeover, frame freshness, key release and finite-budget guards. |
| PD-07 | Brief independent live trial | Attempt 04 loading recognized; gameplay unknown / primary session | Continuous fresh footage, qualified Watch/Go/cues, accepted/released keys and observed outcomes. No blind retry or takeover while the user uses the desktop. |
| PD-08 | End every test in native Idle | Supervised courtyard Idle confirmed after fresh audio visits 02 and 03 / primary session | Qualify a native Idle detector/executor; retain prior failed receipts and later recovery separately. |
| PD-09 | Product integration and packaging | Pending / primary session | User-facing profile selection/calibration, Start/Stop, pack integration and report access; independent live qualification; regression/build/release checks. No new package/install claimed. |
| SN-01 | Research snack mechanics | Offline research and source ledger complete / snack agent | Current-client verification of costs, preferences, eligibility and screen identity. No feeding enabled. |
| PD-10 | Whole visit | Pure planner tested; supervised full visit recorded through native Idle / integration team | Real menu/result/sigil detectors and one-action dispatch receipts must complete the same visit autonomously. Repetition remains disabled. |
| SS-01 | Temporary session settings | Pure proposal/observation/restoration model / settings agent | Durable journal, game-specific UI reader/executor and visible recovery; no settings changed yet. |
| SN-02 | Separate optional snack operation | Pure planner tests pass / snack agent | Observed inventory, feeding outcome verification and scheduler integration separately from Dance. Default remains disabled. |
| GK-01 | Versioned knowledge storage | Schema and first offline importer complete / review agent | Verified bundle has 8 sources/9 claims, zero runtime qualifications. Native provider/UI and history merging remain pending. |
| AU-01 | Game audio cues | Opt-in capture installed; sound isolation passed; 18 fresh preview cues cover all four arrows; nine withheld cues matched; 25 audio tests / audio team | Continuous cue detection, music rejection, exact alignment, response sounds and fresh-session reliability remain pending. No audio-driven actions enabled. See [mapping study](PET_DANCE_SOUND_MAPPING_20261109.md) and [release receipt](GAME_AUDIO_RELEASE_20261109.md). |

## What the implementation now establishes

[`PetDancePanelVision`](../ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDancePanelVision.cs)
fits arrow opacity against small current-game neutral/direction references.
[`PetDanceSequence`](../ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceSequence.cs)
uses observed Watch/Go/result boundaries, stable clear cues and confirmed troughs.
It does not replay a fixed timetable of directions. Known fading arrows do not
become new directional cues. Unknown observations, missing continuity and changed
geometry remain reasons to stop or reject a round.

The result gate requires sustained top feedback and distinguishes Resolving from
Success/Failure. Success advances the expected cue count only after a complete
proposal; Failure preserves the length. The sequence emits only once at Go and
requires a result/Ready boundary before the next round. Regressions must preserve
these invariants, including repeated identical arrows, low-fade-to-neutral gaps,
gradually brightening fades, stale result candidates and duplicate outcomes.

[`PetDanceLiveTrial`](../ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceLiveTrial.cs)
is a separate `w101.pet-dance-live-trial` operation with shared scheduler ownership,
target checks, activity/takeover monitoring, fresh frames and bounded arrow
pulses. The existing recorded `w101.pet-dance-review` operation remains read-only.
The trial does not navigate menus, choose levels, feed snacks or recognize native
Idle autonomously. Its existence is not a completed product workflow.

## Interpretation and cleanup rules

1. Displayed cues, observed human keys and outcome are different evidence. The
   failed fourth round is useful negative evidence; its mistaken input must not
   be copied into a correct-action training label.
2. Reference selection and replay used the same recording. Freeze the reference
   set and thresholds before assessing a new recording. Keep calibration,
   validation and eventual held-out evaluation separate.
3. Encoded 60 fps includes frame-rate conversion. Replay reviews every other
   packet frame while retaining its decoded timestamp. Its 2.1473 ms analysis
   mean does not prove capture throughput, end-to-end latency or live success.
4. The live trial no longer assumes a one-pixel stretch. A pinned profile must
   name its source dimensions, single transform and calibration evidence.
   Dimension ratios are not measured pixel mappings. Recalibrate after layout,
   UI-scale, border or camera changes; never broaden thresholds to hide a shift.
5. Gameplay outcome, input receipts, key release, scheduler state and native Idle
   are separate results. `Completed` would not itself mean a won game, and a
   stopped teacher does not establish Idle. A supervisor must observe the game
   menu's native Idle state before issuing its confirmation marker.
6. If loading or user takeover prevents cleanup, stop inputs, release ownership
   after the bounded cleanup attempt and record the missing Idle confirmation.
   Never reclaim the desktop or fabricate confirmation to close a checklist.

## Parallel tracks and next order

The [snack research](W101_PET_SNACK_RESEARCH_20261108.md) and
[configuration proposal](W101_PET_SNACK_CONFIGURATION.md) are separate from Dance
cue logic. Training snacks, Happiness and ordinary inventory disposal are distinct
contexts; the latter is excluded from the proposed snack subsystem. The example
settings remain disabled and are not an executable game pack.

The [knowledge database guide](GAME_KNOWLEDGE_DATABASE.md) defines local IDs,
sourced claims, chronology, contradictions and scoped client observations.
The [first bounded importer](W101_KNOWLEDGE_IMPORT.md) now publishes a fresh
pet-snack research database and hash receipt together. It preserves uncertainty,
source dates and unknown client scope; it does not enable routines. Native
provider, knowledge UI and additional-domain importers remain pending.

Next: preserve this replay baseline; resolve capture geometry with full-visit
evidence; perform a bounded independent trial and supervised Idle return;
integrate qualified behavior into pack/Observe controls; then package.
Research/data work can continue without desktop ownership while live testing is
blocked. Follow the [research protocol](RESEARCH_PROTOCOL.md) for every result.
Do not describe startup as a game stall or security reaction: neither was
established. Loading recognition and actual gameplay qualification are separate.
