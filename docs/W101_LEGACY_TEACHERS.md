# Reusing the legacy Wizard101 collection as teachers

Reviewed locally on 2026-11-08. The recursive archive catalogue contains **88
compressed items**, including earlier Maelstrom builds, test-game downloads and
nested reference repositories. All **25 user-supplied root archives** received a
bounded static source/asset inventory. Archive presence and keyword matches are
not claims of working features or compatibility with today's client.

The reproducible inventory is `.local/legacy-teachers/inventory.json`: actual
container types, SHA-256 hashes, image counts, license-file paths and source line
references. No archive entry point or bundled executable was launched. Generated
builds and nested prior downloads are catalogued separately, not mistaken for
additional W101 teachers. Source review does not establish redistribution rights;
in particular, a dependency's license file is not the enclosing bot's license.

## What we can reuse

| Supplied item | Useful contribution | Teacher status / next adaptation |
| --- | --- | --- |
| `petdance.zip` and the existing `Wizard101_DanceBot` source | Eight arrow images, direction bindings, recognition fragments and sequence playback | First offline teacher prototype implemented. Huge UI and 800×600/1280×800 assumptions need current-window calibration. |
| Both `Deimos-Wizard101` archives | Pet/quest/combat workflows, UI paths, script language, branches, loops and waits | Strong routine-language reference. The reviewed main archive's dance player reads moves through a memory hook; record such labels as privileged teacher information. Do not assume a visual student has those observations. |
| `wizSDK-master.zip`, `wizwalker-master.zip` | Client/window adapters, input and state primitives, combat and pet workflow helpers | Candidate adapters, not standalone trained players. Source/dependency readiness and current-client compatibility remain separate. |
| `Automatus-v2-master.zip` | Modular bot interface, mapping/locomotion and state statistics | Reuse orchestration ideas; its README says the project stopped ahead of a successor. Not a ready general player. |
| `wizAPI-master.zip` | Visual spell templates, battle routines and controls | Useful visual teacher candidate. Documented 800×600, regular UI and desktop-scale assumptions need replacement with window-relative calibration. |
| `Wizard101-Farming-main.zip` | OpenCV card matching and battle action ordering | Candidate battle demonstrations after separating recognition, action proposals and target-bound execution. Deck-specific assumptions require explicit context. |
| `gardening-bot-main.zip` | Image-based spell/menu checks, waits, movement lists and plant-state tracking | Especially useful for the requested home routine framework. Replace global input and fixed waits with bounded, verified actions. |
| `afk-wizard-main.zip` | Small visual farming/keep-active loop and three images | Reference for conditions and actions. Existing unbounded global-input loop is not suitable as the shared input owner. |
| `Wizard101-Bazaar-Tc-Farming-master.zip` | Menu/item images and purchase-flow recognition | Useful visual/UI data; actual purchases are not required for teacher bootstrap. Old fixed-screen assumptions need calibration. |
| `GrubNinja-master.zip` | Grub Guardian maps, UI/state organization and reward metadata | Reviewed reward code submits scores/results through service calls, including a generated score. It does not demonstrate visual tower placement or survival gameplay; those results are not gameplay training evidence. |
| `raid-utility-tools-main.zip` | Raid-specific task organization and utility vocabulary | Specialist context/reference; not yet a qualified visual demonstration source. |
| `Wizard101-Damage-Calculator-master.zip` | Damage/modifier calculations and stat modeling | Potential consequence evaluator for combat decisions; independently test formulas against the current game. |
| `Wizard101-Utilities-master.zip` | Gear/bundle/pack data and images | Context and data-processing helpers, not an autoplayer. |
| `WizWikiAPI-main.zip` | Game-data categories and parsing structure | Research-schema reference. Its bundled README says the API no longer works; do not treat it as a working live data service. |
| `WizGallery-dev.zip` | Photo-gallery and annotation/library organization | Potential observation-library patterns, not control demonstrations. |
| `wizwad-main.zip`, `libkiwad-master.zip` | Client asset container handling | Candidate asset indexing; assets need provenance and matching against actual rendered scenes. No client file extraction is required for this first teacher. |
| `On-Wiz-master.zip`, `wiz-packet-map-main.zip`, `w101-proto-go-master.zip` | Protocol/message references | Developer research. Old protocol descriptions do not establish usable present-day traffic or visual gameplay labels. |
| `Aurorium-main.zip` | Client file/version management | Version/provenance ideas rather than gameplay behavior. |
| `cheat-engine-master.zip` | General diagnostics and inspection framework | Engineering reference, not a W101 gameplay teacher by itself. |
| `ProjectMaelstrom_win-x64_portable.zip` | Older app distribution | Historical UI/runtime reference. Preserve it; do not substitute it for the current build. |

No ready source implementation of Conjuration was found in the bounded review.
That is a search result, not proof that none exists in any binary or outside this
collection. Pet Dance has substantially better reusable material right now.

## First concrete reuse: offline Pet Dance teacher

`petdance.zip` is actually an uncompressed **TAR** with 1,041 entries. The reader
detects its format without renaming, executing or extracting its bundled EXE.
All eight arrow PNGs match the existing reviewed source byte-for-byte; comparison
evidence is `.local/legacy-teachers/dance-bundle-assets.json`.

Reviewed source: `.local/upstream/Wizard101_DanceBot`, Git commit
`84ef7e9fcc67969aa424ad080043c7796e59a4dd`. The harness pins the exact hashes of
`image.py` and `dance_game.py` and loads only their reviewed image-preparation and
decision functions. It replaces capture with synthetic cue images, sleep with a
virtual clock, and key presses with recorded proposals. It does not import the
entry point, activate windows, move the mouse, install listeners or feed snacks.
This controlled invocation is not a general-purpose security sandbox.

Results:

| Test | Exact sequences | Meaning |
| --- | --- | --- |
| Original logic, one frame per cue | 20/20 | Correct on these source-derived fixtures. |
| Original logic, two frames showing the same cue | 0/20 | Repeated observations can be counted as new arrows. |
| Cue filter, same 40 fixtures | 40/40 | One event per presentation; two neutral samples rearm it. |
| Expanded filtered check, both source resolutions and sequence lengths 3–7 | 100/100, 1.63 seconds | Lightweight synthetic regression evidence, not measured live-game accuracy. |

The repeated-frame check explicitly separates an arrow held on screen from two
genuinely repeated arrows separated by a neutral gap. The neutral-gap duration,
animations, occlusion and the Watch/Go phase transition still need live data.
The old source also activates/moves the game window and logs keyboard events;
those full-application behaviors are not adopted by this harness.

Generated local draft:
`.local/legacy-teachers/dance-bootstrap/pack.json` contains the existing schema-v2
action registry, all eight reference images, hashes and rehearsal evidence.
Its skills/fixtures remain empty and `live_qualified` is false. Asset filenames
are recorded as legacy labels, not silently presented as human-reviewed current
game examples. The installed Observe learner currently requires reviewed teaching
examples; this draft is an importable registry/reference bundle, not a playable
or directly learned visual pack. No model weights were trained in this experiment.

## Native teacher integration (2026-11-08)

The repaired Pet Dance adapter is now available in **Wizard101 → Observe & teach**
and **Observe → Pet Dance teacher**. It imports the pinned local arrow images,
calibrates a region/scale, distinguishes neutral from unknown and exports sequence
proposals from saved recordings. See [Pet Dance teacher](PET_DANCE_TEACHER.md) for
the complete workflow, measured evidence and remaining live qualification gate.
The earlier bootstrap pack above remains a historical asset/reference bundle.

## The demonstration-to-player workflow

1. Select a source revision and teacher capability; pin files, settings and bindings.
2. Rehearse its decision component offline and reject known-bad demonstrations.
3. Adapt capture and actions through one Maelstrom-controlled session. Record the
   selected game window, teacher proposals, actual dispatched inputs and outcomes.
4. Retain capture/input timing and whether labels came from pixels, memory or
   scripted fixtures. Mark failures, skipped gameplay and unknown outcomes.
5. Learn a small visual policy or compile stable decisions into routine rules.
6. Test the student on new random sessions and compare with the teacher. Keep
   training and evaluation recordings separate.

Existing `GameplayInputRecorder` already records allowlisted injected keys and
mouse with an `Injected` flag while W101 owns focus. That flag does not identify
which bot emitted an input. Background window messages and direct memory/state
changes may bypass those hooks, so instrumented teacher action logs are necessary
for those adapters. Video alone cannot reliably recover all actions.

Auto-Idle and the teacher must not compete for input. Teacher execution uses the
same session lease, takeover policy, limits and explicit start/stop ownership.
For private bot demonstrations, pause/release on manual activity; for manual
teaching, the user provides the inputs and the recorder only observes.

Before live Pet Dance, replace the old full-desktop coordinates with current
window-relative regions, verify Watch/Go/result states, and stop before snack or
energy-spending postgame flows. The existing GPU video path also needs its odd
outer-window height handling addressed for this 1282×759 capture; the passive
stills workaround does not validate high-rate action/video synchronization.

## Scale sensitivity: user evidence and experiment

The user reports that Pet Dance was highly scale/resolution-sensitive in their
own use. Preserve that as a design requirement. Source inspection supports the
concern: fixed screen regions, resolution-specific images, window repositioning,
hard-coded click positions and a Huge UI prerequisite.

`tools/legacy_dance_scale_check.py` tested 48 synthetic cues at scales 0.72,
0.91, 1.0, 1.13, 1.37 and 1.49 across both supplied template sets. The unchanged
subimage recognizer uniquely identified 18/48. A masked, multiscale matcher
identified 47/48 and returned uncertain for the remaining cue; it accepted no
incorrect direction in this set. All four blank/ambiguous controls were rejected.
Initial evidence: `.local/legacy-teachers/dance-scale-check.json`, 8.39 seconds
total, one OpenCV worker, no game input or model.

This broad search is **offline calibration**, not a measured 60 fps live loop.
The intended runtime caches an accepted template scale and searches a small cue
region; it recalibrates on changed client geometry or UI settings. Window pixel
size, in-game UI scale and Windows DPI are independent context fields. A known
cue region is still an assumption of this experiment, not automatically solved
window calibration. Actual animation, clutter, occlusion and different UI scales
remain untested. One uncertain result is preserved rather than lowering the
threshold to advertise a perfect score.

## Reproduce without desktop use

```powershell
& .local/envs/dance/Scripts/python.exe tools/inventory_w101_legacy.py --paths .local/legacy-archive-paths.txt --output .local/legacy-teachers/inventory.json
& .local/envs/dance/Scripts/python.exe tools/rehearse_w101_dance_teacher.py --source .local/upstream/Wizard101_DanceBot --output .local/legacy-teachers/new-check.json --rounds 25 --deduplicate
& .local/envs/dance/Scripts/python.exe -m unittest tools.tests.test_legacy_teacher_bootstrap
```

The inventory uses the standard library. Rehearsal uses the already installed
isolated DanceBot image packages, one OpenCV worker, no GPU model and no internet.
No new production dependency or upstream executable is bundled into Maelstrom.
