# Pet Dance: first complete native sound-led game

Recorded 2026-11-09. **One supervised live game completed with five recognized Success results and 25 dispatched arrow presses.** The independent UI review recorded `PERFECT!!!` and Games Won advancing from `2/25` to `3/25`. The supervisor supplied no Dance arrows. This is one measured successful game at the tested settings, not general reliability or an unattended navigation qualification.

## Measured result

The [live report](../.local/w101-audio-study/live-teacher-sound-04/live-report.json) preserves the original observations, proposals, audio events, input receipts and timing. Its `game_victory_verified=false` remains correct for the automatic reporter: the separate [independent UI review](../.local/w101-audio-study/live-teacher-sound-04/independent-ui-review.json) supplies the result-screen evidence.

| Measurement | Observed result |
| --- | --- |
| Round results | Five Success, zero Failure; no unfinished response keys at each result |
| Arrow dispatches | 25; every receipt says `dispatched` and records key release |
| Active gameplay | 34.430 seconds |
| Trial elapsed time | 50.904 seconds, including startup/loading and stopping |
| Startup/loading accounting | 16.446 seconds |
| Visual analysis | 1,184 observations; mean 2.798 ms per analyzed frame |
| Old capture frames | Four discarded; pending arrows survived and resumed on fresh frames |
| Supervisor Dance arrows | Zero |
| Game screen | `PERFECT!!!`; Games Won `2/25` before, `3/25` after |
| Other effects | No snacks fed; volume unchanged; native Idle observed afterward |

The five native proposals and actual outcomes were:

| Round | Response sequence | Visual corroboration status | Result |
| --- | --- | --- | --- |
| 1 | Right, Up, Left | Corroborated | Success |
| 2 | Up, Up, Down, Up | Audio-led; some visual uncertainty retained | Success |
| 3 | Left, Up, Right, Up, Right | Corroborated | Success |
| 4 | Right, Right, Left, Right, Left, Right | Corroborated | Success |
| 5 | Down, Down, Left, Up, Down, Up, Right | Audio-led; some visual uncertainty retained | Success |

The observed instructions specify a game ending after five rounds or three failures. The five-result stop is therefore a game boundary in this visit. The mean visual analysis time is not end-to-end reaction latency, capture throughput, CPU utilization or a 60 fps claim.

## What changed in the player

The runtime uses native C# selected-process audio, a bounded spectral detector and a small visual state reader. No hosted language or vision model is required for this loop. [PetDanceAudioDetector](../ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceAudioDetector.cs) scans audio on independent 50 ms anchors with 220 ms sound windows and the frozen 84-bin comparison templates. Source clocks, gaps, confidence, repeat boundaries and availability remain explicit. It does not choose its sound windows using visual arrow timestamps.

[PetDanceSoundSequence](../ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceSoundSequence.cs) requires observed Watch and fresh Go, the expected number of qualified independent sound events, separated occurrences, continuous source timing and processing completed before the decision. The existing similarity/margin gates remain 0.8/0.08. Matching arrow images provide corroboration when available. Unknown or conflicting arrow images remain visible uncertainty; they do not automatically erase a complete qualified sound sequence. Missing/weak audio or unrecognized game state still withholds the response. The former frame-by-frame visual sequence parser remains diagnostic in sound mode.

[PetDanceLiveTrial](../ProjectMaelstrom/ProjectMaelstrom/Utilities/PetDanceLiveTrial.cs) retains explicit Start, shared input ownership, the selected foreground target, desktop-takeover cancellation and private injection tagging. Sound mode arms only at a fresh Watch after 500 ms of quiet with no held keys. Escape remains a stop, including before arming. Menus and return to native Idle remain supervised; this player owns only the Dance arrows.

The freshness repair waits for a new frame instead of ending the game on the first old frame. An armed wait is bounded to five seconds, and dispatch still requires a capture age no greater than 150 ms. Unknown/Resolving observations wait without sending or consuming the next arrow; a new Watch discards unfinished keys from the prior response. Live04 exercised recovery from four old frames, aged approximately 155–175 ms, without sending from those frames or dropping the remaining response.

Current source also distinguishes a transient rejection before key-down from an interrupted input. This one successful game does not exercise every scheduling edge case. A newer cleanup classification distinguishes missing Idle confirmation from uncertain input/capture cleanup; it was implemented but had not yet been exercised by a fresh live trial at the time of this note. These source changes do not retroactively change the live04 evidence.

## Preserve the earlier failures

| Visit | Actual evidence | Interpretation |
| --- | --- | --- |
| [Live01](../.local/w101-audio-study/live-teacher-sound-01/live-report.json) | Zero arrows, three Failure results | One unclear onset frame permanently faulted the old visual sequence in each round. [JSON review](../.local/w101-audio-study/live-teacher-sound-01/audio-visual-event-review.json) found all nine sound cues had 3–5 matching full-arrow observations spanning 83–150 ms, with no conflicting full directions. This motivated sound-led sequencing; it does not turn those failures into wins. |
| [Live02](../.local/w101-audio-study/live-teacher-sound-02/finished.json) | Scheduler rejected startup; no live report | An existing input-recovery notice required review. This was a startup guard, not a gameplay failure or completed trial. |
| [Live03](../.local/w101-audio-study/live-teacher-sound-03/live-report.json) | Correct Right, Right, Up proposal; one Right dispatched, then `stale-capture`; no observed round outcome | The response was interrupted by the old freshness handling. The first sent key did not establish a completed round. |
| [Live04](../.local/w101-audio-study/live-teacher-sound-04/live-report.json) | Five Success results, 25 dispatched arrows; four old frames recovered | First complete native sound-led game, independently checked against the result screen. |
| [Live05, Huge UI](../.local/w101-audio-study/live-teacher-sound-05-huge/live-report.json) | Unchanged profile; zero inputs, no recognized game start; 45.077 seconds elapsed | Changing UI size defeated the existing phase geometry. Same window dimensions did not make the profile portable across UI sizes. This is a failed transfer check, not evidence that Huge UI is unsupported in principle. |

## Cleanup is a separate result

Live04's original [scheduler receipt](../.local/w101-audio-study/live-teacher-sound-04/finished.json) remains **`CleanupFailed`**: its 60-second supervised Idle-confirmation window expired without the required confirmation file. A successful minigame does not repair that receipt.

The later [recovery review](../.local/w101-audio-study/live-teacher-sound-04/recovery-review.json) separately confirms all key-ups, stopped workers and observed native Idle while the input lock was held for review. The original recovery notice was archived with provenance. The independent UI review also records native Idle. These establish later recovery; they do not claim that the original scheduler completed cleanly.

The newly added cleanup policy returns a failed game-state requirement when Idle is unconfirmed but worker cleanup and input release are independently confirmed. Uncertain release/worker cleanup remains `CleanupFailed`. Its 14 focused tests pass, including the scheduler recovery-marker checks. That implementation still needs a new live lifecycle test; neither category should be mislabeled successful cleanup.

## UI size and calibration remain active work

Live04 used the raw 1282 × 759 profile with Top `(512,100,256,40)` and Bottom `(512,647,256,100)` and the tested Tiny UI setup. Its profile mixes raw phase references with earlier arrow/Success references, with pinned hashes. The Success reference supplies the top feedback mask, not old bottom-phase coordinates. The five observed Success results and independent result screen supply concrete evidence for that combination in this visit.

The Huge UI test preserved the same profile and failed to recognize the game. Work is ongoing on bounded layout discovery: search the bottom-middle 3 × 4 region, with a 3 × 2 fallback, and locate the separate top-middle feedback region. These are work areas/search plans, not claims of a qualified invariant layout. See the [layout study artifacts](../.local/w101-audio-study/ui-layout-01/component-normalization.json).

The [Huge calibration sample](../.local/w101-audio-study/huge-ui-calibration-02/recordings/20261109-202710-c150bd6b/passive-sample.json) requested 45 seconds at a maximum of 5 fps but actually stopped at its storage limit: **65 still frames, 18.116 seconds, 135,751,446 bytes (135.75 MB)**. It is not a 45-second/225-frame recording or a complete game demonstration. It recorded and sent no inputs. Any calibration conclusions must use its actual coverage.

After the study, **Large UI at 1280 × 720 was restored, saved with OK and checked by reopening the settings menu**. The [restoration receipt](../.local/w101-audio-study/settings-restored-after-huge-study.json) also confirms native Idle, unchanged volume and release of the desktop helper. Large is the restored user setting; the successful Tiny-profile run does not qualify automatic play at that restored setting.

## Prior evidence and remaining qualification

- [Sound mapping study](PET_DANCE_SOUND_MAPPING_20261109.md): initial preview-note evidence and frozen templates; Up/Down require spectral shape, not a single pitch.
- [Native/Python parity](../.local/w101-audio-study/continuous-01/NATIVE_PARITY.md) and [machine receipt](../.local/w101-audio-study/continuous-01/native-python-parity.json): all 18 emissions and 4,769 common scan-window decisions matched. The five omitted native windows per recording were incomplete tails. Those recordings are development data, not untouched tests.
- [Earlier continuous integration source review](PET_DANCE_CONTINUOUS_INTEGRATION_NOTES_20261109.md): historical proposal and geometry/capture-lifetime concerns. Later implementation and this live result supersede its observer-only implementation status, not its evidence boundaries.

One complete win is a milestone, not repeatability. Remaining checks include additional fresh games, UI-size/resolution calibration and restoration, missed-result recovery, negative audio contexts and altered music/volume conditions, and the full Start → play → confirmed Idle scheduler lifecycle. Navigation, snacks and unattended replay are not qualified by this test. The selected-process scope excludes microphone capture and whole-desktop audio; no broad game or computer-use capability is inferred from these Dance results.

## Region-based direction memory and measured timing follow-up

The latest source adds an opt-in layout search for profiles declaring `UiLayout`.
It searches the bottom-middle twelfth (with an optional bottom-middle sixth
fallback), locates the shared controls, and normalizes their scale before reading
the arrow. Shared button appearance is removed from the shape comparison; a
direction must retain enough independent shape strength. The whole game scene is
not resized. Five configured UI scale hypotheses are covered by synthetic tests;
the saved Tiny and Huge images supply real image evidence. Other resolutions and
fresh live transfers remain unqualified.

`PetDanceVisualSequence` now records arrow **appearances**, not every matching
frame. Two full-direction observations spanning at least 50 ms confirm a cue.
Held arrows count once; a repeated direction requires a confirmed low/neutral
trough. Fades and brief unclear observations do not erase prior directions.
Longer observation gaps and unresolved repeat boundaries remain explicit
uncertainty. The coordinator selects a complete visual sequence first and uses
complete independent sound evidence as a fallback. Both trackers receive the
selected length so subsequent Success/Failure feedback stays synchronized.
Fresh previews recover missed feedback without replaying the prior response.

The visible experimental `--pet-dance-visual-trial` command can test a region
profile without audio. `--pet-dance-sound-trial` uses the visual-first policy for
these profiles with sound fallback; legacy profiles retain their separate
behavior. These research entrypoints and local calibration assets are not a
claim that the installed main app has been updated.

### Timing is not an even five seconds plus five seconds

The [source-clock timing study](../.local/w101-audio-study/timing-structure-01/FINDINGS.md)
and [pinned measurements](../.local/w101-audio-study/timing-structure-01/timing-structure-v2.json)
use existing reports, without game interaction or new capture:

| Observed quantity | Recorded result |
| --- | --- |
| Later preview, 3 cues | 1.500–1.533 seconds |
| Preview, 4 / 5 / 6 / 7 cues | 2.050 / 2.550 / 3.067 / 3.567 seconds |
| No-input response, three 3-cue rounds | 5.999886 / 5.999889 / 5.999887 seconds to first Resolving |
| Timeout uncertainty from adjacent frames | Approximately 5.950–6.050 seconds |
| Actual final release after Go, seven-key win | 2.119 seconds |
| Audio in the five-win recording | 25 preview notes, plus 10 response-phase events |

First-round Watch includes an extra startup lead-in. The preview is consistent
with roughly half a second per cue; successful answers end the response phase
early. These recordings therefore do **not** establish the full unanswered
timeout for four through seven cues. The note detector's 50 ms candidate grid
also means its timestamps are not exact acoustic onsets.

Group sounds by observed Watch/Go boundaries and causal availability, preserving
the full original stream. Do not divide the recording into fixed five-second
chunks or mistake response sounds for the next demonstrated sequence. The
timing study's 100 ms leading tolerance is an analysis convention; the runtime's
existing candidate tolerance remains separately defined and tested.

The player now uses five seconds from its first fresh observed Go as a
**conservative completion target**, not a claimed engine constant. Seven 100 ms
holds and six 220 ms gaps take 2.020 seconds nominally. A repeated Go or Unknown
does not restart the clock. The dispatcher checks both capture freshness and
the absolute release deadline immediately before key-down, retains actual
release timestamps, and discards expired response keys while continuing to
observe the next round. Windows scheduling is not a hard real-time guarantee.

The new source passed all **732 tests**, including a cross-modality regression
that switches visual → sound fallback → visual across increasing round lengths.
The final test build treats warnings as errors; scoped style and analyzer checks
also passed. Static reviews rechecked all 65 saved Huge frames and 24 Tiny snapshots;
these are development evidence, not fresh live trials. A saved-label-only
visual replay accepted the first sequence and exposed incomplete old visual
labels later; that review does not rerun image recognition or replace the
earlier five live sound-led wins.

The attempted fresh Large-UI trial did not start: the supported desktop helper
returned `failed to activate captured window` and the one permitted refreshed
activation retry failed identically. Native Idle was visible before and after
the failed attempt. No teacher was launched; desktop control was released and
volume remained unchanged. See the [preflight receipt](../.local/w101-audio-study/ui-layout-01/live-preflight-blocked.json).
The prior missing-Idle-confirmation marker from Huge05 was reviewed under the
exclusive input lock and archived using its zero-input/stopped-worker evidence
plus the fresh Idle observation; its original failed receipt remains intact.

## Fresh Large-UI qualification: Live06

The next authorized trial recovered normal activation using a freshly selected
game window and the supported `activate_window` call. The normal X interaction
then opened Dance. No forced-focus workaround or activation code change was
needed; the cause of the prior failed activation remains unproven.

[Live06](../.local/w101-audio-study/live-teacher-sound-06-large/live-report.json)
completed a fresh full game at **1280 × 720, Large UI** with the same region
profile and frozen audio templates. The locator selected scale 1.5. The
[independent UI review](../.local/w101-audio-study/live-teacher-sound-06-large/independent-ui-review.json)
recorded **PERFECT!!!**, Games Won `3/25 → 4/25`, agility `30 → 34`, and pet
experience `23/250` after the reward. No supervisor Dance arrows were supplied.

| Round | Selected sequence | Source | Final key release after Go | Result |
| --- | --- | --- | --- | --- |
| 1 | Down, Up, Down | Visual; sound agrees | 0.852 s | Success |
| 2 | Up, Right, Up, Right | Sound fallback | 1.213 s | Success |
| 3 | Down, Right, Up, Down, Right | Visual; sound agrees | 1.514 s | Success |
| 4 | Left, Up, Up, Right, Up, Left | Sound fallback | 1.851 s | Success |
| 5 | Right, Up, Left, Right, Left, Left, Down | Visual; sound agrees | 2.153 s | Success |

All 25 inputs have confirmed releases and all five responses fit the five-second
completion target. Four old frames were discarded without ending the game.
Mean visual analysis was 4.442 ms across 1,303 analyzed frames; this is not a
whole-system CPU or end-to-end latency measurement. The run saved 64 bounded
snapshots, skipping 13 when its budget was exhausted. Audio produced 33 emitted
events; the five accepted preview groups used 25 notes.

Unlike the earlier visit, this trial's [scheduler receipt](../.local/w101-audio-study/live-teacher-sound-06-large/finished.json)
is **Completed**. The supervisor used Next → Finish (without feeding), opened
the game menu, selected native Idle, observed its banner and clicked Confirm
native Idle before the timeout. The [Idle receipt](../.local/w101-audio-study/live-teacher-sound-06-large/idle-return.json)
preserves the final game-window image hash. Input releases, audio/capture cleanup,
process exit and absence of a recovery notice were verified. Desktop control
was released; volume and graphics settings were unchanged.

Visual-only reliability still needs work: round 2 lacked a stable first Up in
the visual ledger; round 4 could not confirm the trough between repeated Ups.
Sound fallback recovered both. Subsequent accuracy work must preserve this
successful original report and distinguish replay improvements from fresh wins.

## Temporal repair and fresh verification: Live07

The two Live06 misses were traced to temporal handling, with the image confidence
gates unchanged:

- The short first Up had two confident full-arrow samples only 33.333 ms apart,
  then a 133.330 ms gap to a clearly recognized Up fade. The reader now allows a
  qualified, same-direction, non-brightening fade to confirm persistence after
  **at least two full-arrow samples**. Fade support is recorded separately; a
  single full sample or fade-only sequence cannot invent a cue.
- The repeated Up had a qualified low fade, one unclear frame, then a qualified
  neutral frame. The reader now preserves the first low observation across that
  bounded unclear interval until the second actual low observation confirms the
  trough. Unknown does not count as positive evidence. High/full observations,
  old trough evidence and longer gaps still prevent this bridge.

The [temporal findings](../.local/w101-audio-study/live-teacher-sound-06-large/TEMPORAL_FINDINGS.md)
preserve the source timings. Added regressions cover these exact cases, the
previously successful repeat, and wrong-direction/rising fades, one-full/fade-only
evidence, long gaps, expired troughs and high observations between low samples.
All **748 tests pass**, with warnings treated as errors and scoped formatting,
style and analyzer checks passing. No production dependency or hosted model was
added. The trial implementation is identified as `region-shape-sequence-v5`.

The saved-direction reviewer now runs the same sound fallback selector when
complete audio evidence exists, keeping feedback progression synchronized. It
does not reclassify images or claim to send inputs. Replaying Live06 and the
earlier Tiny-UI Live04 now recovers all five visual sequences (25 directions)
in each recording, matching their successful original responses. Original
receipts remain unchanged. These recordings informed the repair and are
development evidence, not independent tests.

A **subsequent fresh game**, [Live07](../.local/w101-audio-study/live-teacher-sound-07-large/live-report.json),
then completed with **PERFECT!!!**, five Success results and 25 released arrow
inputs. All five response sequences were selected from vision; independent
phase-grouped audio agreed with every sequence. No sound fallback was needed:

| Round | Visual sequence | Final key release after Go |
| --- | --- | --- |
| 1 | Left, Up, Right | 0.842 s |
| 2 | Down, Left, Left, Up | 1.198 s |
| 3 | Left, Up, Left, Left, Left | 1.535 s |
| 4 | Right, Up, Left, Down, Left, Right | 1.868 s |
| 5 | Right, Right, Down, Right, Left, Up, Right | 2.154 s |

The [independent UI review](../.local/w101-audio-study/live-teacher-sound-07-large/independent-ui-review.json)
records Games Won `4/25 → 5/25`, agility `34 → 38`, pet experience `27/250`,
Large UI and 1280 × 720 unchanged. It also pins the application assembly hash.
The [comparison receipt](../.local/w101-audio-study/live-teacher-sound-07-large/accuracy-comparison.json)
keeps same-recording replay separate from the later random-sequence game.

Mean visual analysis was 4.309 ms across 1,344 analyzed frames, with three old
frames discarded. These timings do not measure whole-system CPU or prove a
particular capture rate. Next → Finish → native Idle was supervised through the
normal UI, without feeding snacks or changing volume. The scheduler returned
**Completed**; capture/audio stopped, all releases were confirmed, the owned
process exited and no recovery notice remained. Desktop control was released.

This qualifies one fresh post-repair game at the tested settings. Longer-run
statistics, other UI sizes and fully unattended menu/navigation remain separate
work. The tested source/build has not been packaged or installed by this check.

## Evidence identity

SHA-256 of the preserved files at documentation time:

| File | SHA-256 |
| --- | --- |
| Live04 `live-report.json` | `ce2522e6d76d12f823d49a8a54dee70690b46c55c3e14f698d20cff680f53412` |
| Live04 `independent-ui-review.json` | `44d571b93beece13f83077c6ab6d77a7ffe7a015923fdd3aad27a22abc6ae48b` |
| Live04 `recovery-review.json` | `bc199a2b83c2709ccf9df19d89b8146e763ff5d8d3ebb8c7348f7361debe561f` |
| `settings-restored-after-huge-study.json` | `530c6fc3dba66110b0e0959d1c1b153403115aac326212db2eb921e648b35274` |

This note was produced by read-only inspection of existing code and receipts. No game actions, captures, builds or source-code changes were performed for the documentation review.
