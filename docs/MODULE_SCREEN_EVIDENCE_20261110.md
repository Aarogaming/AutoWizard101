# Saved W101 screen evidence for reusable modules

Work item: `MODULE-EVIDENCE-05`. This is a bounded offline evidence checkpoint,
not a live-game qualification. Six original saved screenshots and their existing
local OCR are now checked in so recognition, module decisions and scheduler
rehearsal can be tested without desktop use or a running game.

Fixture directory:
[`ProjectMaelstrom/ProjectMaelstrom.Tests/TestData/Modules/W101Menus`](../ProjectMaelstrom/ProjectMaelstrom.Tests/TestData/Modules/W101Menus).
The [recognition cases](../ProjectMaelstrom/ProjectMaelstrom.Tests/TestData/Modules/W101Menus/recognition-cases.json)
contain all original OCR words, their order and numeric bounds. The
[provenance manifest](../ProjectMaelstrom/ProjectMaelstrom.Tests/TestData/Modules/W101Menus/provenance.json)
pins the screenshots, source report and extracted fixture by SHA-256. Property
names were normalized and redundant calculated geometry omitted; words were not
corrected, filtered or added. Screenshot copies are byte-for-byte originals.

## Actual available screens

All six images were visually reviewed and rehashed for this checkpoint. They
total 5,557,420 bytes. Source files are under
`.local/dance-postgame-frames/`; existing OCR comes from
`.local/dance-menu-review-3.json` with SHA-256
`b571ce16fc6014491d92e567724783151c2b39bae307149d6a0dd4ccaea7220d`.

| Fixture | Original | Supported reading | Source/image SHA-256 |
| --- | --- | --- | --- |
| `reward.png` | `frame-01.png` | `YOUR REWARD`, `GOOD JOB!`, and `NEXT`. Expected scene `GameResult`; identified control `pet-dance.next`. | `47d3e9eb90ed759caed626d01a1538b7e9bf93c51f214f15b61c18b59b003e61` |
| `snack.png` | `frame-03.png` | `FEED YOUR PET`, enabled `FINISH`, gray `FEED PET`. Expected scene `SnackOffer`; identified control `pet-dance.finish-without-snack`. No snack selection or consumption is inferred. | `ab687d77b17c20540c95a761bebfebde7f721c6b986fb98a19580fd63e5c2291` |
| `loading.png` | `frame-07.png` | Manually recognizable loading artwork. The stored OCR is insufficient to classify loading automatically, so expected scene remains `Unknown`. | `cb93efd6ca5e373370e3d6195d3e1dda243c14d35993936ae4e5714ddd98dc49` |
| `outside.png` | `frame-08.png` | Bottom `Press … to Play` prompt outside the Dance tent. Expected scene `OutsideCandidate`, with no verified control. | `8ef206ba0b97ea454ae26c1ae5979bc8d14464bcc74696ab0d2150f63a196f93` |
| `graphics-menu.png` | `frame-14.png` | Graphics settings with `IDLE`, `HELP`, `OK` in order. Expected scene `MenuObserved`; identified control `game-menu.idle`. | `bc665c42c816e2204405634de2b1bf590e441f79eba9c92d74f0a8c1f02bb4e4` |
| `native-idle.png` | `frame-19.png` | `YOU ARE IDLE` banner above the still-visible Dance prompt. Expected scene `NativeIdle`, with no action control. | `21cc5af30ff62e37ff8c2326539e692a48858e444603c34505f636f81fab6160` |

The screenshots measure **1282 × 760 outer-window pixels**, including the window
frame. Graphics settings visibly show **1280 × 720**, **Fullscreen Off**, and
**UI Size Tiny**. These dimensions must not be treated as interchangeable client
and capture coordinates. This collection provides no independent Large/Huge,
different-resolution or fresh-session holdout.

Existing OCR places `NEXT` at `(776,574,49,11)`, `FINISH` at
`(465,574,43,12)`, and the menu's `IDLE` at `(572,550,28,12)` in those captured
images. These are measured recognition bounds, not hardcoded live-click targets.
The actual target window, geometry and current recognized bounds still have to
agree before any live dispatcher can use them.

## What a rehearsal can prove

The smallest complete supported composition starts at the recognized Graphics
menu, proposes its identified Idle control, accepts a **synthetic** released-input
receipt, and observes the saved native Idle banner. A longer authored rehearsal
can exercise Reward → Snack → Unknown/loading → Outside → Graphics → Idle.
The reward screen itself does not prove a specialist completed and released
input for the current visit; that eligibility remains separate supplied context.

`OutsideCandidate` can become actionable outside state only after the matching
visit's acknowledged Finish transition. It cannot authorize Escape from an
arbitrary initial screen. The Idle banner takes precedence over the sigil prompt
beneath it. Unknown/loading suppresses proposals and does not manufacture
progress, success or a new input opportunity.

Original capture timestamps and command-bound receipts were not recovered for
these stills. File numbering supplies their review order, not measured elapsed
time. Repeated use of the same PNG at synthetic timestamps is useful for testing
stability and held-screen rejection, but it is **the same image**, not an actual
new capture. A rehearsed outcome must not be described as evidence that an input
was sent to W101 or caused the displayed transition.

Fresh local OCR against these full PNGs is a separate integration check from
regressing the saved OCR word sets. Its output may differ from the historical
report. The coordinator owns those checks and their result report; fixture
preparation did not rerun OCR, build the app or send input.

## Sigil and menu coverage outside this fixture

The existing
[Entry fixture](../ProjectMaelstrom/ProjectMaelstrom.Tests/TestData/PetDance/Entry/provenance.json)
already preserves instructions, hover instructions, loading, a Dance sigil and
Idle OCR from `.local/dance-menu-review-6.json`. Reuse it rather than inventing a
second history. Source `inspect-101.png` shows the Dance title and prompt, but
the title bounds are a **manual annotation**, not an automatic title detector.
Its SHA-256 is
`163d93d418012c55f278a618c1a99c893788955c2d046d5d57410602da52ccaa`.

`PetDanceEntryVision` requires that independent title evidence to identify a
Dance sigil; generic `Press … Play` OCR cannot supply the missing identity.
Instructions require the localized Dance header, Objective, Select Level and
Play. Readable Play alone proves neither enabled state nor selected arena.
[Recorded selection evidence](PET_DANCE_SELECTION_QUALIFICATION.md) retains its
own control, pointer-away, image/time and geometry requirements. This fixture
does not expand that qualification or assert a Cancel control merely because a
dialog is visible.

## Current operation and control boundaries

| Operation | Reuse point | Evidence or implementation still required |
| --- | --- | --- |
| Observe reward, skip snack, open menu, enter native Idle | `PetDancePostGameClassifier`, `PetDancePostGame`, `PetDanceMenuInput` | The existing UI dispatcher allows only Next, Finish without snack, Open menu and Select Idle. It retains current ownership, readiness, geometry and released-input checks; it never activates a window. New module integration must preserve those gates. |
| Interact with a named sigil | `PetDanceEntryVision`, `PetDanceVisit`, generic `SigilModule` | `game.interact` is configured as bounded X plus exact sigil identity. A generic bottom prompt does not establish that identity. Entry dispatch is separate from the postgame allowlist. |
| Select or cancel a menu | Generic `MenuModule`; `W101SharedOperations` bindings | Reuse exact named controls and observed outcomes. The sample `game.cancel-menu` configuration does not itself add a dispatcher or qualify Cancel recognition. |
| Walk toward a destination | Generic `NavigationModule`, `W101Routine` motion primitives | Need recognized route/world context, landmarks, per-pulse progress and arrival evidence. Saved movement or background change cannot stand in for those outcomes. |
| Spin, Step or Routine idle options | Generic `AutoIdleModule`, `HomeMovementOptions` | Separate bounded options, not native Idle. Current binding, clear area and each pulse's outcome still need verification; translating options retain the home requirement. No implicit wake operation is introduced. |

The shared module runner and scheduler should consume recognized scene/control
evidence and proposal-bound outcomes; these screenshots do not create another
controller. See [shared W101 operations](W101_SHARED_OPERATIONS.md) and
[reusable modules](REUSABLE_MODULES.md).

## Navigation evidence is still demonstration material

The existing [Commons demonstration](W101_DEMONSTRATION_20261108.md) documents
walking from the Commons anchor via the bridge and signed Pet Pavilion tunnel,
then through a loading transition to the Dance sigil. It is a human
demonstration, with no verified autonomous arrival. No End key was recorded, so
it does not establish a repeatable port reset. The route must not assume map
teleportation or blindly replay long forward holds.

Read-only metadata at `.local/w101-route-study/human-route-01/demonstration.json`
explicitly has `route_qualified=false`, `labels_verified=false` and
`exact_input_video_alignment_verified=false`; its current SHA-256 is
`8caf980979690c6a127b21288c07bc1d0ad696c083268fe607c56fc7b77c2d81`.
The approximate route review window is 5–45 seconds, with a recorded maximum
source-frame gap of 694.4303 ms. Coarse contact-sheet times are not action or
arrival boundaries. No full video decode was performed for this checkpoint.

Consequently the existing authored W101 route replay remains a **synthetic
contract test**. A real navigation fixture needs a verified starting landmark,
captured before/after evidence for one particular released motion, fresh matching
progress/arrival, and independent held-scene/failed-motion negatives. Until that
exists, menu/Idle integration is the supported saved-screen subset.

## Validation boundary

Preparation verified all six original hashes against the historical OCR report,
the byte-identical checked-in copies, PNG dimensions against OCR geometry, and
preservation of every original word and bounding rectangle. The fixture JSON
and its provenance parse successfully. Source changes, new OCR, native tests,
module/scheduler execution and live qualification are separate coordinator-owned
results; none is inferred from this evidence audit.
