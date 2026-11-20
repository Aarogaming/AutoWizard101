# Installed Pet Dance and native Idle trial — November 9, 2026

## Latest checkpoint

The user-approved 22:07 repair installer is installed in version directory
`08d8c3434d4b41ec8d39a7632435e06f`. Its assembly hash is
`961EAAE4B7CA061B1EA8DEEDEAFA2C7E51E56CFC701D67C7DF274DD346B8AA84`.
`.local/dance-native-idle-live-20261109/repair-installation-verified.json` records
the UI-confirmed installation and all 750 baseline data files still present.
This is a presence check, not a content-hash comparison of every user file.
The older `b5c5cdf4de554492986e529d4af5a1dd` installation and its two trials are
preserved below as historical evidence.

Latest report: `%LOCALAPPDATA%/Maelstrom/reports/pet-dance/20261110-031213-a787bbc5d0244862a8350e68e863e891/`.
The 1280 × 720, Large-UI game recorded five successes and 25 dispatched keys, with
PERFECT independently observed on the reward screen. The gameplay report still
says `game_victory_verified: false`. It processed 1,055 frames in 45.602 seconds,
including 10.577 seconds paused; mean analysis was 4.39 ms per frame. These figures
do not measure total machine load or establish a 60-fps capture rate.

Automatic Idle stopped on `desktop-activity-or-target-change`. Its diagnostics
show **80 reads, zero admitted frames, one missing frame, 79 stale frames and zero
aligned frames**, with `entries: []`, `input_released: true` and
`capture_stopped: true`. The 31 retained stale samples span 495.742–511.382 ms
(mean 499.749 ms), all exceeding the unchanged 300-ms admission limit. No menu
action was proposed or dispatched. Unlike the earlier uninstrumented timeout,
this run identifies stale delivery as the admission failure; it does not prove
every detail of the capture driver's buffering behavior.

The user reported finishing the menus and returning W101 to native Idle. The
saved scheduler receipt is Completed for supervised cleanup; `idle-return.json`
names a supervised check and records `autonomous_idle_recognition: false`.
Neither that receipt nor the user report is a new autonomous or assistant-observed
Idle success. Game controls were enabled for this last trial, and no subsequent
switch-off is verified. No newer live operation has been launched in the current
offline work; do not infer current desktop state from earlier screenshots.
The trial records no feeding, purchases, volume change or automatic focus change.

## Historical live observations — original workspace package

Both visits used Wizard City, a 1280 × 720 client, Large UI, visual sequences and
optional game-only audio. Large UI was restored from the prior Huge setting.
Volume was unchanged. No snacks were fed, no purchases were made, and W101 was
returned to its own visible **YOU ARE IDLE** state between visits and at the end.
Any control-switch restoration for these earlier visits does not establish its
state after the later repair trial described above.
No game memory, client-file changes, network interaction or background game input
was used.

Reports are under `%LOCALAPPDATA%/Maelstrom/reports/pet-dance/`:

| Report directory | Gameplay | Cleanup |
| --- | --- | --- |
| `20261110-025312-caee510b3c78493581d23fc3ce86c259` | Three successful sequences, 12 keys; stopped on `desktop-activity-or-target-change`. The user reported a possible cat bump of the mouse. This is consistent with the guard, not proof of the input's source. | Automatic continuation correctly withheld after interruption. Supervisor returned W101 to Idle; the 120-second manual receipt deadline expired before confirmation, so the report remains Failed. |
| `20261110-025635-c8fab60489844093bad4292c2a772adf` | Five successful sequences, 25 keys; the reward screen visibly read **PERFECT!!!**. | Automatic return timed out with zero admitted OCR frames and zero menu actions. Supervisor completed Next → Finish without feeding → Graphics → Idle and confirmed it in the trial. Scheduler receipt is Completed for supervised cleanup; `autonomous_idle_recognition` is false. |

The second game's responses took 0.872, 1.116, 1.519, 1.766 and 2.108 seconds
from observed Go to all keys released, each inside the five-second budget. It
processed 1,113 frames in 47.804 seconds including 13.210 seconds of paused/loading
time; mean measured analysis was 4.390 ms per frame. These metrics exclude the
rest of the machine and do not prove 60-fps capture or total CPU/GPU cost.

The gameplay report deliberately retains `game_victory_verified: false`; the
PERFECT reward is an independent supervisor UI observation. Five recognized
successes, a reward screen, scheduler completion and automatic Idle are separate
claims. This trial qualifies a complete game at this configuration, **not a
working automatic end-to-end visit** or reliability across all resolutions.

## Historical postgame defect and first repair

`native-idle-operation.json` records `postgame-deadline-exceeded`, `entries: []`,
`input_released: true`, and `capture_stopped: true`. No frame reached menu
classification, so changing button templates or OCR confidence would not address
the observed admission failure.

Code review found that the postgame loop omits the bounded timestamp alignment
already used by gameplay and the sound observer: retain a frame timestamped up
to 30 ms ahead, briefly await that original timestamp, then apply strict freshness.
Rejecting every slightly future frame is a plausible explanation, not a proven
root cause: the installed loop did not record null/timestamp/rejection counters.

The source repair retains the same frame for alignment, preserves its timestamp,
and rechecks readiness before OCR. The 300-ms freshness bound, original input
ownership/activity baseline, cancellation and one dispatch per verified transition
are unchanged. Clock-driven regressions cover future timestamps, cancellation,
activity during alignment, stale/repeated/invalid timestamps and missing frames.
The report now records admission/rejection counts and at most 32 rejected-frame
samples. It does not relabel stale frames or retry menu clicks blindly.

At this point low-rate delivery of buffered stale surfaces was another possible
explanation. The later installed repair trial above supplied the missing evidence:
79 stale rejections and no future-timestamp alignment. Thus the alignment repair
alone did not restore the automatic postgame path.

The parallel measured-client/grid implementation passed 966 offline tests at this
historical stage and was absent from the original workspace package. It is included
as an offline overload in the later installed repair. Its simulated 720p/1080p/1440p
evidence and native-rendering limits remain in [the resolution plan](PET_DANCE_RESOLUTION_PLAN.md).

## Installed repair package — 22:07

The alignment/diagnostic repair and offline grid support were packaged together
after **981/981 native tests** passed
(none skipped), scoped formatting/analyzers passed, the build-script syntax check
passed and both publish steps completed. Builds used two logical processors and
one build worker. The portable archive's assembly hash matches its published
directory and the required preset is present. No new dependency was added.

- Portable: `.local/releases/Maelstrom-20261109-220552-9b4956.zip`
- Portable SHA-256: `18BF8A7FFA330E17C5A9379DA5266676B11C3F510D6BFDF366E443CC417D77A2`
- Assembly SHA-256: `961EAAE4B7CA061B1EA8DEEDEAFA2C7E51E56CFC701D67C7DF274DD346B8AA84`
- Installer: `.local/releases/Setup-20261109-220719-c0e02b/MaelstromSetup.exe`
- Installer SHA-256: `E4DEAEB0FA962330BC8C1AE5EF8F394990E37DB8CA53BFB057C3D6AD1882EBF7`

The user supplied action-time approval for this installer; installation and its
live trial are complete, as recorded at the top of this document. It is not still
waiting for installation permission. The diagnostics worked, but automatic Idle
did not. The 1280 × 720 live gate remains.

## Subsequent source repairs — offline only, not installed

`VisualTransitionCadence` now separates fast capture reacquisition from slow OCR.
Rejected frames allow bounded 25-ms retries (eight per burst, at most 250 ms),
then a 500-ms cooldown. OCR starts remain at least 500 ms apart. The 300-ms
freshness limit, target/context binding, cancellation and inherited activity
baseline are unchanged; stale images are not relabeled as fresh. A fake buffered
capture regression reproduces the old 500-ms starvation and admits fresh images
with the new cadence. The focused cadence/frame-admission run passed 23/23 tests.

Review also found that the final supervised Idle photograph could hang on a GPU
read or capture disposal, even after scheduler cancellation. The new
`BoundedWindowCapture` helper bounds the capture session to 2 seconds and disposal
to a separate 3 seconds. It never retries a stalled read, disposes late images,
propagates cancellation and reports unconfirmed cleanup instead of success.
The confirmation call now uses the operation token. A 32/32 focused offline test
run passed, including nine new cleanup cases and overlapping cadence/read/
confirmation tests. This is not a second complete-suite count.

These cadence and cleanup repairs have not been packaged, installed or live-
qualified. The generic routine graph/replay work is also in progress; its offline
results must remain distinct from actual menu transitions. No new live input,
capture, model workload or volume change was used to write these repairs or this
checkpoint. A fresh release and bounded live qualification are still required.
