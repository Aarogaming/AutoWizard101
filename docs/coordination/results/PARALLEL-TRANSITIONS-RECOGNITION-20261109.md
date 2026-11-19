# Saved evidence audit: Dance entry and Conjuration recognition

Task: `PARALLEL-TRANSITIONS-RECOGNITION-20261109`; parent workstreams
`W101-DANCE`, `CORE-TRANSITIONS`, `W101-CONJURATION`.
Owner: `dance_levels_snacks_research`. Scope: read-only evidence inspection and
independent code review; this report is the only edited repository document.
The separately authorized bounded follow-up below adds eight derived local PNGs,
their decoder logs and a manifest; source recordings remain unchanged.

## Audit result

**The saved Conjuration collection contains no playable-board evidence.** All
17 PNGs in its two recordings are byte-identical instructions, page 1 of 2.
They supply one useful real negative example, not 17 independent observations
of cards or a current-client calibration set. The decorative card strip above
the title is part of the instructions artwork and must not become a board.

**Dance entry has useful saved positive and negative examples, but selected
arena and enabled controls need evidence beyond OCR text.** In two instruction
frames, OCR reads `PLAY!` at the same position even though the first button is
visibly gray/disabled and the second yellow/enabled. A Wizard City tooltip is
hover evidence, not independently verified selection. The same Dance sigil
prompt remains visible while native Idle is active.

Exactly **ten distinct frames** were visually inspected for this initial audit.
Their bytes were hashed locally. No new screenshots, OCR, game input, model,
network access, video decoding, dependencies or builds were used. Source client
build/realm and broad UI-size compatibility remain unverified. The review does
not promote prior user/supervised actions to autonomous success.

## Reproducible image set

Paths beginning `.local/` are relative to `F:/Modlists/AutoWizard101`. `U` below
is the exact directory
`%LOCALAPPDATA%/Maelstrom/reports/pet-dance/20261110-031213-a787bbc5d0244862a8350e68e863e891`.
These are existing local source files, not new generated fixtures. SHA-256 values
were recomputed during this audit rather than copied without checking.

| ID | Source | SHA-256 | What the pixels support |
| --- | --- | --- | --- |
| C1 | `.local/conjuration-samples/recordings/20261108-175042-fd787863/frame-0000.png` | `c738f117bf34363a32e1652164796ed13397b1e6c658d99e4e54a3813768175d` | Conjuration instructions page 1/2, PLAY button, decorative card strip; no board. |
| D1 | `.local/w101-route-study/lifecycle-01/review/inspect-006.png` | `78c7e1cd3482a0bc0130ee180f559875751d23c4a023353ac74062428b5f0de1` | Dance instructions and five arena icons; PLAY visibly disabled; a tooltip occludes other text. |
| D2 | `.local/w101-route-study/lifecycle-01/review/inspect-013.png` | `7004d579dd1234ea3a0af71b997b851fb58a1e5543ee34127839c307f0776c40` | Same instructions; first icon highlighted, Wizard City hover tooltip, enabled PLAY, red +4 near Agility. Selection is not independently resolved from hover. |
| D3 | `.local/w101-route-study/lifecycle-01/review/inspect-020.png` | `7aceb383428029eacea14c4ecb2d639b4a3700a3f6dc87097f078ee9d2e4240e` | Wizard101 loading artwork; saved OCR has no words. Empty OCR alone is not a runtime loading detector. |
| D4 | `.local/w101-route-study/lifecycle-01/review/inspect-101.png` | `163d93d418012c55f278a618c1a99c893788955c2d046d5d57410602da52ccaa` | Dance sigil prompt says DANCE GAME and Press X or mouse to Play. No Idle banner. |
| D5 | `.local/w101-route-study/lifecycle-01/review/inspect-123.png` | `4ae0f6d6e97f1010d66ee3a6ccbf59eb165dd660176ff1301bfdca77beeab31c` | The same sigil prompt plus YOU ARE IDLE at the top. Idle must take precedence over entry. |
| D6 | `.local/w101-route-study/lifecycle-01/review/inspect-072.png` | `49b338f09d1979a0916c3332b98ccfd379b30f3a856ed4af576ebfee98decf1d` | YOUR REWARD / GOOD JOB / NEXT; a reward screen, not an instruction or active round. |
| D7 | `.local/dance-postgame-frames/frame-03.png` | `ab687d77b17c20540c95a761bebfebde7f721c6b986fb98a19580fd63e5c2291` | FEED YOUR PET with FINISH enabled and FEED PET gray; never an entry/play action. Snack artwork is not Conjuration cards. |
| D8 | `.local/w101-route-study/lifecycle-01/review/inspect-031.png` | `5ef571e21ec19066c05f1bdf5c56d1c9ef583d6371fd51e9e180f627e923449f` | Active Dance stage with OOPS and DONE; not a complete-game result merely because DONE is visible. |
| D9 | `U/first-frame.png` | `7582d605e155ec1305b0eb6f1baf3e39b9a622a67ab09ab8ecc5f0bfcbf86fc9` | Larger-looking Dance instructions, first arena highlight/tooltip and enabled PLAY. This is a separate layout example, not proof of its named UI-size setting. |

C1 also exactly matches `frame-0000.png` through `frame-0015.png` under
`.local/conjuration-samples/recordings/20261108-175243-79c9bad5/`.
The latter `passive-sample.json` reports 16 saved frames over 5,188.9477 ms,
maximum 4 fps, no recorded inputs and zero sent inputs. Its `recording.json`
has empty state labels and null regions. Byte identity does not by itself prove
a broken capture path: the screen may simply have remained on instructions.
It does mean these samples provide no visible action/outcome or temporal change.

The Dance route's `demonstration.json` explicitly records
`exact_input_video_alignment_verified: false`, `route_qualified: false`,
`labels_verified: false` and `automatic_training_eligible: false`.
`review/inspection.json` describes the PNGs as visual interval selections,
not exact key-timing labels, and names source video hash
`88329347100875165c93a777b0ad7b997d372dc73c8267f8fd4eddcc04b40317`.
The video was not decoded or rehashed in the initial audit. The later bounded
follow-up below records its separate video hash check and targeted seeks.

D9's `routine-settings.json` records
`arena_selection: user-selected-in-game-not-automatically-verified` and
`resolution_qualification: 1280x720-client`. It is not automatic selected-arena
proof. No settings witness establishing Large versus Huge was inspected here.

## Existing OCR and manual geometry

The existing OCR ledger is `.local/dance-menu-review-6.json`, SHA-256
`880427e0dbaa01aa875d519d9ab00f2149aaf51300f8a756a5b6478502befa45`.
These are **previously computed OCR results**, not new OCR or perfect ground
truth. The ledger's classifier results are outputs under review, not labels to
train against blindly.

The Conjuration image and newer D9 image are 1282 × 759; the route/OCR Dance
frames are 1282 × 760. C1 and D9 are RGBA with alpha range 7–255 and 4,186
nonopaque pixels. The new Conjuration loader deliberately rejects these raw
captures under its opaque-image contract. Its separately declared in-memory RGB
conversion is a diagnostic negative, not native capture compatibility or a
modification of the source. This limit needs an explicit capture/ROI adapter
decision before real-game use.
These include the window border/title bar. Route metadata names a 1280 × 720
client. Preserve that distinction; an OCR image point cannot be sent unchanged
to a client-pixel dispatcher. No universal offset or scale was calibrated here.

| Evidence | Saved OCR text | Bounds `(x, y, width, height)` in outer-frame pixels | Limitation |
| --- | --- | --- | --- |
| D1 and D2 | DANCE; GAME | `(591,211,50,15)`; `(646,212,45,14)` | Useful instruction heading; different from the lower sigil heading. |
| D1 and D2 | SELECT; LEVEL | `(604,473,36,12)`; `(643,473,32,11)` | Arena selector context, not proof that a choice is selected. |
| D1; D2 | PLAY! | `(778,574,42,11)`; `(778,574,42,12)` | Text is recognized in both disabled and enabled states. |
| D1 | CANCEL | `(463,574,45,12)` | Instruction cancel control; not a general close button. |
| D2 | CIT | `(491,548,11,7)` | Failed reading of the Wizard City tooltip; unrelated quest text also contains Wizard City elsewhere. |
| D4; D5 | Press; Play | `(568,675,36,12/13)`; `(711,675,27,15/16)` | Full-frame OCR misses the visible DANCE GAME sigil title. The generic prompt is shared with other activities. |
| D5 | ARE; IDLE | `(622,55,33,13)`; `(660,55,32,13)` | Positive Idle header coexists with the sigil prompt. |

Conservative manual region annotations supplied to the entry owner, based on
the inspected pixels, are below. They are **approximate human annotations**,
not measured detector accuracy, clickable bounds or permission to act:

- D4/D5 sigil title: `(525,642,75,15)`; complete interaction banner roughly
  `(481,641,297,76)`.
- D2 first arena icon/highlight: `(452,485,59,58)`, center approximately
  `(480,513)`; tooltip is separate, approximately `(450,541,66,24)`.
- D9 first arena icon/highlight: approximately `(358,532,84,81)`.

The first-icon highlight in D2 is brighter/larger than D1, but the same frames
also differ in pointer hover, tooltip, red stat preview and PLAY enabled state.
No same-hover/unselected or selected-without-hover control is available in this
set. Therefore the highlight cannot establish arena selection independently.
Repeated source labels or a user preference saying Wizard City cannot supply
the missing visual evidence.

## Qualification gaps and adapter implications

For Dance, require independently qualified arena/enabled evidence before PLAY;
recognize the specific game heading before interpreting the generic sigil
prompt. Idle, loading, reward/snack and active-stage feedback must not fall
through to entry actions. Frame identity/freshness and target/run identity must
survive the observation → proposal → receipt → next observed scene chain.
Loading can pause a phase clock but cannot reset wall limits or count as arrival.
`DONE!` on the active stage is not proof that the entire game has ended.

For Conjuration, normalized ROIs supplied to a recognizer are configuration,
not evidence that a board exists. A real instructions negative must remain
unknown even if decorative imagery resembles frozen face templates. Hidden,
face-up and removed states need separately sourced references; missing/blank,
occluded or low-quality cards must not be treated as removed. Board identity,
layout changes, phase and matching group size require explicit qualification;
none can be inferred from these instruction screenshots.

No additional image search or full-video decode is justified by this bounded
audit. Missing data is specific: an actual Conjuration preview/hidden/reveal/
match-removal sequence, and controlled Dance selected/unselected/enabled
examples without conflating hover and selection. Such acquisition is a separate
coordinator-owned task; offline synthetic tests can proceed meanwhile.

## Independent implementation review

Reviewed the owners' new `PetDanceEntryVision.cs`, `PetDanceVisit.cs`,
`Resources/Routines/pet-dance-visit.json`, `tools/conjuration_vision.py` and
`tools/conjuration_observation_fixtures.py`, plus their focused test sources.
The shared `RoutineRunner` receipt/clock/binding semantics were inspected to
check how the visit adapter composes with them. This reviewer did not run a C#
build or edit any implementation/test/fixture file.

### Conjuration retired-board defect: reproduced and repaired

The initial recognizer compared revision and geometry only with the immediately
preceding board. A four-observation, in-memory synthetic probe reproduced:

| Observation | Before repair |
| --- | --- |
| `visit-a`, revision 7, time 0 | Accepted, awaiting stability. |
| `visit-b`, revision 0, time 60 | Accepted as a new board. |
| `visit-a`, revision 0, time 120 | Incorrectly accepted despite reusing a retired board ID and rolling back its revision. |
| `visit-a`, revision 1, time 180 | Produced stable hidden/face-up/removed labels. |

This could confuse old and new board context at an adapter boundary. The finding
was sent to the owner, who added a retired-ID rejection and a 128-board bound.
The same independent probe now rejects returning to `visit-a`; a later valid
`visit-b` observation still produces its expected labels, showing the rejected
reset did not corrupt the current board. These probes used the existing
`.local/envs/dance` interpreter and procedural 400 × 240 images in memory,
completed in roughly half a second each, and wrote no image or test files.

The owner reports **20 focused recognition tests passing**, including retired-ID
and history-bound regressions. That is synthetic/offline qualification only.
The separate observation-fixture replay already tracks prior board IDs and
rejects reuse. It hashes referenced local files, preserves supplied receipt
events and does not manufacture gameplay outcomes or dispatch inputs.

The review also flagged ambiguous `reference_hashes` provenance. The owner now
explicitly records those as hashes of bilinear-resized 32 × 32 RGB descriptors,
normalized to little-endian float32; they are not original reference-file hashes.
The original C1 file hash and derived RGB diagnostic hash remain separately
identified in `tools/fixtures/conjuration_vision/provenance.json`.

The reviewed recognizer clears confidence for unknown/animation/result states,
requires increasing source time/revision, rejects same-board geometry changes,
and uses positive references plus similarity/margin/local-error/stability gates.
These mechanisms do not establish real-game thresholds, automatically detect
phase, infer group size, verify board context or supply missing card references.

### Dance entry and visit review

The entry classifier keeps supplemental image hash, capture time and geometry
bound to one OCR observation. It requires an independently identified arena
control, verified selection and enabled PLAY evidence; tooltip/OCR alone is
insufficient. Idle takes precedence, blank OCR remains Unknown without a separate
loading cue, and generic interaction text needs the measured Dance title.
The saved fixtures preserve unknown capture timestamps and synthetic test clocks;
they do not claim source clock reconstruction or selected-level qualification.

The visit adapter uses the existing bounded runner and run-specific proposal
identity. It separately gates target/context/input ownership, fresh observations,
entry cost, specialist start/completion/release, whole-game result and postgame
controls. Starting in Idle or mid-game does not adopt/complete a visit. User
takeover or binding loss cannot initiate cleanup actions. The reviewed graph has
eight maximum actions for the longest entry-to-Idle route and no feeding action.
These are code/test-source findings, not newly observed gameplay or an independent
C# test run.

One diagnostic inconsistency was sent to the owner: a disabled visit's
`Observe()` returned `Stopped=true`/`visit-disabled`, while `Status` queried the
untouched runner and returned incomplete/not stopped. The existing disabled test
only checked that no proposal was emitted. The owner aligned both surfaces and
added the assertion; the repaired source was inspected. No unsafe action path
was found in the
reviewed entry/specialist/result/takeover gates; this is not a proof that all
possible inputs or future dispatch adapters are correct.

The later `PetDancePlayButtonVision.cs` addition was also reviewed. It reuses
the existing small RGB-grid matcher and bounds the observed-text-relative crop
to 65,536 pixels. Opacity is checked on that crop, not unrelated rounded-window
corners. Its two 90 × 25 references retain source/derived hashes and are explicitly
from the same recorded visit, not a held-out evaluation. Button appearance is
separate from selected-arena proof. It cannot turn that missing selection gate
into a verified instruction state by itself.

A public-test/internal-enum parameter accessibility issue was flagged; the owner
had already changed those test parameters to strings before the build, which
passed. The first focused run was owner-reported as **71/73 passing**: the actual
paired saved-button cases and entry/visit/replay cases passed, while two synthetic
2× button transforms remained Unknown. The owner traced that to synthetic GDI
edge alpha and added an opaque backing before drawing the test resize. The rerun
reports **73/73 passing, none skipped**, with production opacity, minimum
similarity and winning-margin guards unchanged. Independent arithmetic also notes
that fractional crop rounding yields `(1,1,179,49)` on the 2× fixture, whereas
literally scaling the original crop yields `(0,0,180,50)`; the corrected synthetic
cases pass with that existing rounding. Neither the failure nor the repair
establishes actual Large/Huge UI behavior. The coordinator owns
the final build/test report; this reviewer ran no C# test suite.

## Return and remaining gates

Evidence limitations were sent directly to both owners before qualification.
The reset reproduction and repaired result were also sent to the coordinator.
This report preserves actual saved pixels, manual observations, synthetic probes,
owner-reported tests and pending live work as different evidence kinds.

Still needed for real-game operation: actual Conjuration board/card references;
qualified phase/group-size/context detection; explicit handling of captured
image alpha and outer-window/client geometry; independently verified Dance arena
selection; and coordinator-owned integration plus fresh complete-visit evidence.
No new live qualification, deployment, feeding, purchases or desktop use occurred.

Final review status: **complete for this bounded saved-evidence/code-review
slice; integration and live qualification remain with the coordinator**. The two
identified state issues were repaired by their owners. Source image hashes and
the retired-ID regression were independently checked here; recognition/C# suite
counts are explicitly owner-reported. Only this report was edited in that slice.

## Bounded selection follow-up

The coordinator authorized one final inspection of at most eight saved-video
frames around the recorded arena-selection and Play events. **Eight additional
unique frames were extracted and inspected, bringing this audit to eighteen
distinct visual frames.** No additional extraction is needed for this checkpoint.

There is a stronger selected-appearance candidate than the original hover still.
At encoded PTS **18.466667 s**, the Wizard City tooltip is absent and Play has a
pink/white hover-or-press appearance, while the first arena icon retains a pale
bright rim and brighter green artwork than the disabled-Play baseline. The first
icon has reduced from its enlarged hover appearance. At **18.600000 s**, the
instructions panel is gone. These pixels support persistent arena highlighting
after the tooltip disappears. The pointer itself is not a qualified visible cue;
its departure is inferred from the changed controls and corroborated by the
separate observed-input log, subject to the alignment limits below.

This is a **candidate reference for selected appearance in this one recorded
visit**, not a general selected-arena detector, qualified target dispatch, or
proof that hover alone could never enable Play. No isolated hover-without-click
state was found in the eight-frame sample. Runtime gates were not changed.

### Source, extraction and timestamps

Source: `.local/w101-route-study/lifecycle-01/video/capture.mp4`.
Its SHA-256 was recomputed by streaming the file:
`88329347100875165c93a777b0ad7b997d372dc73c8267f8fd4eddcc04b40317`.
The existing FFmpeg 8.1 executable at
`.local/releases/Maelstrom-20261109-220552-9b4956/runtime/ffmpeg/ffmpeg.exe`
performed sequential input-side `-ss` seeks with `-copyts`, one output frame,
`showinfo`, no audio/subtitles/data, and decoder/filter/encoder thread limits of
one. Each process had a ten-second timeout and finished in 0.109–0.125 seconds.
This was targeted decoding from keyframes, not a full-video decode.

Outputs are under
`.local/parallel-recognition-20261109/selection-seek/`.
`selection-seek.json` records the source hash, executable version, requested
times, first emitted encoded PTS, image hashes, dimensions and elapsed times.
Each image also has its own `.png.log`. These are local derived evidence, not
new captures or automatically qualified training labels. All eight PNGs are
1282 × 760 RGB. Requested seek time and actual encoded PTS are distinguished:

| PNG | Requested seconds | First encoded PTS seconds | SHA-256 | Manual observation |
| --- | ---: | ---: | --- | --- |
| `frame-11.50.png` | 11.50 | 11.500000 | `81774ef2ea63f10e6dd7b1e5e30d9962068c7db6648c260f09d0097448bf0ce8` | Play gray/disabled; first icon dimmer; Will tooltip visible. |
| `frame-11.62.png` | 11.62 | 11.633333 | `55359b4b5e49db05276b4a6ee3787b57c8928902df00533d323a31a1a216e64a` | Same disabled baseline. |
| `frame-11.68.png` | 11.68 | 11.683333 | `1e7f8ade110b1a4749156bb6617171efa9aaa0331b799d0869764c584d5dc1c0` | Play yellow/enabled, Wizard City tooltip and red +4 near Agility; icon transition. |
| `frame-11.85.png` | 11.85 | 11.850000 | `5fb44be5cbab7bf8bad5f8995ae8e171142cc75d116d55aa54d791b7f975ef81` | First icon enlarged and highlighted; Wizard City tooltip and enabled Play. |
| `frame-18.30.png` | 18.30 | 18.300000 | `a46047f8564cbf38835f3c00e93979bf255b138dc1b49021320a53b7c88b4bf9` | Same enlarged first-icon/tooltip state and enabled Play. |
| `frame-18.40.png` | 18.40 | 18.400000 | `03b3e162036674e8fd1a081340b38554ccdb271e2ef5dc2f36ebde8262b28613` | Same tooltip state immediately before the later visible change. |
| `frame-18.46.png` | 18.46 | 18.466667 | `769de99bd89bdd6c007084d58dba89f8054126b15992b671009881ea06ef738f` | Tooltip gone, first icon still bright with pale rim, Play pink/white. Best persistent-highlight candidate. |
| `frame-18.60.png` | 18.60 | 18.600000 | `978430b16fa10974b4ac56e77a1144af5214d6eba7d6ddfca6dc3bedc92f601c` | Instruction panel closed; outside scene visible. Does not alone establish gameplay start. |

### Input evidence and limits

The saved input ledger has 45 events and records selected foreground-game keys
and mouse only, with client-pixel coordinates and QPC milliseconds relative to
the first captured frame. It is not a log of every possible desktop action.
The relevant entries, all marked `Injected: true`, are:

| Observed event | Ledger milliseconds | Client position |
| --- | ---: | --- |
| Move to first arena | 11624.7325 | (478, 475) |
| Left down / up | 11658.8802 / 11676.6179 | (478, 475) |
| Move to Play | 18405.2190 | (797, 540) |
| Left down / up | 18439.7705 / 18457.5891 | (797, 540) |

Only the first move/down/up appears between six and thirteen seconds; no further
recorded event occurs between that release and thirteen seconds. No focus-loss
event occurs in that interval. This makes the recorded first-arena click a useful
candidate explanation for the visible disabled-to-enabled transition. However,
the `Injected` flag does not identify the actor and none of these entries is a
proposal-bound accepted/released dispatch receipt. The supervisor's action list
names "select Wizard City level" and "Play" without such receipts or per-action
timestamps. It is supervised menu evidence, not autonomous Maelstrom evidence.

`timing.json` explicitly reports 7,056 captured/submitted source frames versus
7,808 encoded frames, `frame_count_matches_submission: false`, and
`source_frame_alignment: unverified-rate-conversion; extra encoded frames are
not new observations`. Source geometry is 1282 × 759, encoded geometry
1282 × 760, and documented client geometry 1280 × 720. Encoded PTS does not
repair the missing exact QPC-to-source-to-video correspondence or qualify a
client-to-outer-frame coordinate transform. The demonstration's
`exact_input_video_alignment_verified` remains **false**. Therefore an exact
click-caused state transition with no external action is still **unverified**.

The small source ledgers were also rehashed:

| Path relative to `.local/w101-route-study/lifecycle-01/` | SHA-256 |
| --- | --- |
| `video/input-events.json` | `c6c48cc15c73cf9ace84e2541a7676e4ca69b57a9189955622bc0da68bd75211` |
| `video/timing.json` | `b789600898c3e3ca8c774439272247bab248359a006f472694e30e97c17cde08` |
| `video/source-timestamps.json` | `e6c85260cae168a5339e8b8e867050991038b0981720ca7e04bfe4d8be585d4d` |
| `supervisor.json` | `bf8ae09a1980a98d6282df1c3d1ff3381ee0ca01509c111518ccf98c022c4916` |
| `demonstration.json` | `3d814ad5d513ea624a1def83b5c1919e48d15b505a40bb0d80cf2eba955e52a8` |
| `review/inspection.json` | `5baac0e538d40837396b0706f84a9bb1f7a8bab34105fc17831c516a9e9b6799` |
| `review/supervision-annotation.json` | `0e24fcf3d2edc72dddf70daa1c8a10a3318937c4f6e375e5322b051dfab498b3` |

Follow-up status: **complete; useful offline candidate recovered, live and
automatic-selection qualification unchanged**. A future verifier should bind
the identified requested control, pre-selection state, accepted/released action,
and newer persistent selection/Play evidence to the same visit and geometry.
Already-enabled Play alone cannot establish which arena was selected. Broad UI
sizes, other arenas and a hover-only negative remain unqualified. No source
recordings, calibrations, runtime code or central project-board files were edited;
no game inputs, volume changes, feeding or desktop control occurred.
