# Pet Dance teacher — repaired legacy adapter

Implemented 2026-11-08. Open **Wizard101 → Observe & teach → Pet Dance teacher**,
or use **Pet Dance teacher** from Observe. This is a working offline teacher
reviewer, not yet a live pet-game player.

Current 2026-11-09 qualification: all five sequences in the human recording
replay correctly, including its failed round. Live attempt 04 recognizes loading
but not gameplay with the current calibration; it sent no arrows. W101 was
returned to confirmed native Idle. See the [delivery tracker](PET_DANCE_DELIVERY_TRACKER_20261108.md)
for geometry diagnosis, continuous lifecycle recording and the remaining live
acceptance gates. The newer experimental panel reader is not yet the installed
reviewer's complete walk-up/walk-away workflow.

The reviewer now runs as the first [shared scheduled teacher operation](SHARED_OPERATIONS.md).
Use **Run teacher routine → save pack**; Stop cancels queued or active work.
Wizard101's **Routine runs** tab displays receipts and evidence. This scheduling
connection does not enable live game input or qualify current-game recognition.

## What is reused and repaired

The supplied `petdance.zip` is a TAR archive containing the same eight PNG arrow
templates as the reviewed `Wizard101_DanceBot` source. Maelstrom imports only
those SHA-256-pinned images into a local draft pack. Both old resolution sets
remain available. It does not execute or distribute the bundled bot, Python
runtime or unlicensed upstream assets. No production dependency was added.

The old search loop could count a held arrow more than once and started playback
after a fixed delay. The new C# adapter uses these separate components:

| Component | Contract |
| --- | --- |
| `PetDanceAssets` | Bounded ZIP/TAR import; eight reviewed hashes; canonical UP/DOWN/LEFT/RIGHT bindings; no executable capabilities. |
| `PetDanceMatcher` | Existing Emgu/OpenCV; opaque template pixels; score ≥0.96 and inter-direction margin ≥0.025; unknown is explicit. |
| `PetDanceVision` | Window-relative cue region, 20–256 pixels per side; explicit neutral reference; fixed calibrated scale; changed window geometry yields unknown. |
| `PetDanceSequence` | Timestamped Watch/Go/result state machine; stable cues and gaps; repeated identical arrows remain distinct after a confirmed gap; one proposal per Go. |
| `PetDanceStudy` | Completed recording → observations and action proposals → new draft pack and report; original frames/labels unchanged. |

Calibration tries scales 0.50–1.50 in 0.05 increments on a known arrow. Runtime
review reuses the selected scale. Window pixels do not determine game UI scale;
recalibrate after changing either. New UI artwork may require different templates.
The neutral example must be visibly empty: low arrow confidence alone is **not**
treated as a gap.

## Recording workflow

1. Import the supplied Pet Dance archive, or open a previously imported teacher
   `pack.json`. Importing does not enable game controls.
2. Open a completed teaching `recording.json`. Use a video-derived section sampled
   at 20 frames/s when available; 10 frames/s is the minimum practical starting
   point. One-per-second stills cannot resolve these cue boundaries. Current
   recording limits apply: 600 sampled frames per section.
3. Drag around the cue area. Select an empty-gap frame, then a visible arrow frame
   and its direction; select **Calibrate on this arrow**.
4. Use **Review phase labels** to teach the Watch/Go/result text region and export
   a phase pack, then load it in the teacher. Recognized phase names are
   `dance-ready`, `dance-watch`, `dance-go`, `dance-success`, `dance-failure`.
   Alternatively, explicitly label every analyzed frame's phase. Unlabeled frames
   are unknown; the teacher never fills missing phases from elapsed time.
5. Set the first round's cue count, 3–7, and review. A confirmed success after a
   complete proposal increments the expected length; failure keeps it unchanged.
   Clips spanning a restart need the correct starting count or a new section.
6. Inspect the report and replay. Use **Open result in Workshop** for the draft.
   Proposal labels remain distinct from human training labels. Independent sampler
   check recordings cannot be used by this teacher-authoring workflow.

The cue and neutral states must remain visible for at least 70 ms across multiple
samples. Duplicate/backward timestamps, gaps over 150 ms, unclear phase/cue,
missing initial neutral gap, excess/missing cues and incomplete round boundaries
invalidate the round. A Ready/result boundary permits recovery. Returning to
Watch alone does not erase a failed round. A Go frame emits a proposal, never keys.

## Outputs and limits

Every study creates a separate directory under `game-packs/pet-dance-…` with:

- `pack.json`: schema 2 draft, four shared-registry actions, no executable skills.
- `assets/…`: the eight verified local images.
- `pet-dance-calibration.json`: region, geometry, scale and hashed neutral pixels.
- `teacher/session.json` and `events.jsonl`: ordinary replay observations and
  `action_proposal` events. Each proposal contains ordered `action_ids`,
  `label_source: legacy_teacher_proposal`, and `dispatched: false`.
- `teacher-report.json`: frame/proposal/rejection counts, rejection reasons,
  processing mean/p95/max, source pack/recording/calibration/phase-pack identities.

Frame evidence references the original recording by path and SHA-256; it is not
duplicated into the small pack. Preserve that recording and the separate phase
teaching pack when sharing/reproducing a study. There are no input receipts or
reward labels because this adapter does not dispatch. Its examples are not
automatically accepted by the existing human-label-only visual learner.

One teacher operation per application process is allowed, with no queued frame
tasks. OpenCV is capped at one CPU thread (also affecting other OpenCV operations
in that process). Review yields between frames, retains one frame at a time and
supports Stop/close cancellation. There is no local model, GPU inference, network
research or background desktop capture in this workflow.

## Qualification evidence

`.local/pet-dance-integration/check-02/verification.json` records the real compiled
adapter using the supplied images on **synthetic scenes**, not current W101:

- 40/40 recognized scaled arrow cases across both template families at five scales
  from 0.55 to 1.50; no incorrect or uncertain direction in this set.
- Three exact random round sequences (3, 4 and 5 cues), including adjacent repeated
  arrows; 153 replay frames; original labels unchanged.
- Sparse-frame replay produced zero proposals; unknown clutter and changed window
  geometry were rejected.
- About 11.0 ms/frame mean processing, excluding the intentional review pauses;
  13.8 seconds wall time and 6.7 process CPU seconds for the full check. This is
  not a 60 fps capture or live reaction benchmark.
- Sequence unit tests separately exercise 300 random rounds and timing, phase,
  geometry, duplicate, interruption and recovery cases.

Reproduce with a **new** output directory:

```powershell
Maelstrom.exe --verify-pet-dance-teacher F:/Modlists/AutoWizard101/petdance.zip F:/Modlists/AutoWizard101/.local/pet-dance-new-check
Maelstrom.exe --verify-pet-dance-ui F:/Modlists/AutoWizard101/.local/pet-dance-new-ui
```

These commands are bounded and do not activate W101. Verification creates
explicitly synthetic teaching recordings; do not install their calibration as
current-game calibration. The supplied legacy archive remains unchanged.

## Next live gate

Update after the [recorded Commons/Pet Dance demonstration](W101_DEMONSTRATION_20261108.md):
the user reached the Dance sigil, played five rounds and restored native Idle.
The [fade-aware panel replay](PET_DANCE_FADE_VALIDATION_20261108.md) now reconstructs
all five displayed sequences, including repeated arrows, from that recording.
It recognizes four Success results and one Failure; the failed human sequence
remains a failure. References and replay share a recording, so this is development
evidence, not independent accuracy. The separate bounded live teacher uses shared
input ownership and per-arrow checks. Its first started trial remained on the
game's loading screen and sent zero arrows; final native Idle was not confirmed.
The helper and trial stopped. Independent live qualification and product UI
integration remain pending; no new installed release follows from this trial.
See the [delivery tracker](PET_DANCE_DELIVERY_TRACKER_20261108.md) for current gates.

The [Nov 8 game study](W101_GAME_RESEARCH_20261108.md) establishes the navigation
prerequisite and Tiny UI test plan. In that earlier pre-demonstration check, the
location was the Fairgrounds mana-game sigil, not Pet Dance. The Commons map is a location reference; the Pavilion must
be reached through actual travel. The supplied bot's Huge UI instructions and
fixed layouts are historical assumptions, not this client's calibration. A
5 fps passive sample cannot satisfy the native reviewer's 150 ms gap guard.

Current W101 arrow scale, cue cadence, Watch/Go/result recognition, synchronized
capture, action delivery and win/energy outcomes remain unqualified. Connect
qualified proposals through Maelstrom's shared action owner rather than launching
the old full-desktop bot. Stop before snack/purchase flows. Native Idle through
W101's screen menu remains the required between-test state.

During this implementation W101's **YOU ARE IDLE** banner was observed; no game
input or play session was started. Offline tests need no user play and do not
change native Idle.

## Installed release and final validation

Installed `0.2.0-preview.20261108-pet-dance`. Receipts and evidence are under
`.local/pet-dance-integration/`:

- `tests/pet-dance-final.trx`: 326 application tests passed (17 Pet Dance cases,
  including the 300-round randomized test); 29 related Python tests also passed.
  C# formatting verification, build analyzers and Python formatting/lint passed.
- `installed-check/verification.json`: installed executable repeated 40/40 scale
  cases and all three sequence replays, with zero proposals on sparse footage.
  Processing mean 10.68 ms, p95 11.79 ms, maximum 15.51 ms in this synthetic run.
  The phase rubric's serialized fixture is saved and hashed separately.
- Installed replay passed the existing Python pack/session readers: four action
  bindings, 156 events, three proposals, zero input receipts.
- `installer-lifecycle.json`: install/update/uninstall preserved all fixture data
  categories, including packs, recordings, reports, routines and Idle choices.
- `installed.json`: update receipt. No existing routine/Idle-setting files were
  present for that helper's additional hash check; preservation evidence for those
  categories comes from the lifecycle sentinels, not nonexistent real files.
- `imported.json`: eight reviewed legacy images imported into the user's library.
  No synthetic calibration, phase rubric or synthetic recording was installed there.
- `installed-ui/`: offscreen screen render and inactive-default checks. Native
  combo text is absent in the offscreen rendering; the selected value is set in
  the form. No interactive window was activated for this check.

Portable build: `.local/releases/Maelstrom-20261108-192845-e294a9.zip`.
Installer: `.local/releases/Setup-20261108-192918-ebea3d/MaelstromSetup.exe`.
The new teacher guide is bundled in both distributions. All verification processes
exited. A final read-only W101 check again showed **YOU ARE IDLE**, after which the
desktop helper was released. Live gameplay remains unqualified.
