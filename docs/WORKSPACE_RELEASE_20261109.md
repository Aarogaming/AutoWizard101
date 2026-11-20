# Workspace and Pet Dance update — November 9, 2026

Current status: the original workspace package documented below was installed,
then superseded by the **user-approved, installed 22:07 repair package** in version
directory `08d8c3434d4b41ec8d39a7632435e06f`. Its assembly SHA-256 is
`961EAAE4B7CA061B1EA8DEEDEAFA2C7E51E56CFC701D67C7DF274DD346B8AA84`; all 750
baseline user-data files remain present (presence check only). Installation proof
is `.local/dance-native-idle-live-20261109/repair-installation-verified.json`.

The latest Large-UI trial recorded five successes/25 dispatched keys and an
independently observed PERFECT. Automatic Idle admitted zero frames: 79 were
stale at roughly 500 ms and one was missing; no menu action ran. It stopped on
desktop activity/target change. The user then reported finishing and returning
W101 to native Idle. The receipt is supervised cleanup, not autonomous proof.
Controls were enabled for that trial; a later switch-off is not verified. See the
[live trial record](PET_DANCE_IDLE_LIVE_20261109.md) for the three reports and limits.
The newer cadence and bounded-cleanup repairs are source-only and offline-tested.

## Original workspace package — delivered features

- Shared dark workspace styling and a dedicated Dodge entry. Existing Observe,
  recording, specialist, isolated tests, experiments, packs and reports are routed
  from one game page, without duplicating execution engines.
- Cross-game switching is blocked while recording even if preview is paused.
  Research is reset on a game switch and Dodge advice is cleared for W101.
- Pet Dance visit setup: arena, optional postgame native Idle, separate snack/goal
  preferences and explicit no-feeding/Crown Shop limits.
- Native Idle operation: fresh, stable menu recognition; inherited input ownership
  and user-activity baseline; no action retries; separate input/capture cleanup.
- Read-only recommendations use verified XP/stat progress, caps, school context,
  per-pet snack relationships, stock protection and budgets. Same school does not
  imply a preference bonus. Energy admission permits one proposed attempt only
  when fresh observed Energy and entry cost preserve the configured reserve.

## Original workspace package — verification

- 936 native tests passed. Scoped formatting and analyzers passed; Release build
  reported zero warnings/errors.
- 32 saved images classified; reward/snack/courtyard/Graphics/Idle stages recognized,
  gameplay/loading negatives withheld. Roughly 55–135 ms per menu image here.
- Ordered 20-frame saved-video replay completes Next → FinishWithoutSnack →
  OpenMenu → SelectIdle → observed Idle with **zero live inputs**. Receipts are
  deliberately simulated, not live dispatch evidence.
- Offline form layout rendering checks structure and clipping; native controls in
  hidden WinForms renders are incomplete, so it does not certify on-desktop visual
  appearance. Separate UI tests verify restored selections and recording locks.
- Installer install/update/uninstall preservation passed in isolated temporary
  folders: `.local/workspace-update-installer-verification-retry.json`. The first
  check failed while cleaning a locked temporary `Start.lnk`; its error is retained
  in `.local/workspace-update-installer-verification.json.error.txt`. The retry
  used the identical installer. This was not an update of the active installation.

## Original workspace package — historical artifacts

- Portable directory: `.local/releases/Maelstrom-20261109-212400-7fecf9/`
- Portable ZIP SHA-256: `AFC944E93F560917B0A068FE76FD5E6BB564BEA5CCCCBABB8966DCA4287B52BF`
- Assembly SHA-256: `BBDF9AD56F70CD0AA209F4BF8823CAACB6E7B5CB14612594466BBBC693ABCA6A`
- Installer: `.local/releases/Setup-20261109-212444-5bf769/MaelstromSetup.exe`
- Installer SHA-256: `730626201713F734371B05629E4180F3B20B8888356201DA45E5F050721B87C3`

The earlier 21:21/21:22 packages were superseded by this final package; do not
use them for release qualification. User packs, recordings and installed files
were not replaced by building these artifacts.

## Required next gates

1. Installation and an on-desktop check of the new home/player setup are complete.
   The four-step explanation clips vertically at this window size; layout polish
   remains separate from the functional player test.
2. Package and qualify the newer capture-cadence/cleanup repairs. The latest
   installed player again completed five sequences with supervised cleanup;
   its diagnostics identify stale postgame delivery. Neither this trial nor the
   earlier PERFECT games qualify automatic cleanup.
3. Test the other arenas independently, especially their different music.
4. Qualify the implemented offline measured-client/grid mapping against actual
   native-resolution rendering, DPI, aspect ratios and UI sizes before lifting
   the 1280×720 live player gate.
5. Connect live pet/inventory/Energy observations to the tested planning layer and
   show inspectable recommendations. Feeding, Bazaar restocking and purchases
   remain separate future work; no Crown Shop action is enabled.
6. Finish integrating the reusable routine graph/replay foundation, then build
   the editable routine timeline on the action registry and scheduler. Current
   cards/navigation and graph sidecars are not a timeline editor or full live visit.

See [product direction](PRODUCT.md), [Dodge workspace](DODGE_GAME_WORKSPACE.md),
[Idle operation](PET_DANCE_POSTGAME_OPERATION.md), [arena/snack research](PET_TRAINING_LEVELS_AND_SNACKS.md)
and [resolution plan](PET_DANCE_RESOLUTION_PLAN.md).

## Subsequent installed repair and current source work

Measured client/grid mapping and bounded native-pixel panel extraction are now
implemented as offline entry points. Menu OCR caps its raster and retains the
inverse transform. At that intermediate source stage the complete native suite
passed **966/966 tests**, with scoped formatting/analyzers passing. Saved game pixels
retain their phase across declared synthetic 720p/1080p/1440p transforms.
Actual native-resolution game rendering and
automatic Idle still need live qualification. The installer above remains frozen
as the exact artifact installed and tested in the live trial record.

The subsequent alignment/diagnostic repair and offline grid package at
`Setup-20261109-220719-c0e02b` passed **981/981 native tests** and was installed
after the user's action-time confirmation. It is the current installed artifact,
not a pending approval. Its portable archive is
`.local/releases/Maelstrom-20261109-220552-9b4956.zip`; full hashes and the latest
live evidence are in the [live/repair record](PET_DANCE_IDLE_LIVE_20261109.md).

The later source cadence fix adds bounded 25-ms capture reacquisition while
retaining at most 2-Hz OCR and the strict 300-ms admission limit. Its 23-test
focused run passed. The final supervised-confirmation capture now respects
cancellation and has a 2-second read/session budget plus a separate 3-second
disposal bound; unconfirmed cleanup remains failure. Its 32-test focused run
passed, with coverage overlapping the cadence tests. These are **not installed
or live-qualified**, and do not replace the pre-package complete-suite result.

The generic routine graph/transition runner and offline replay workflow are
under implementation/review. Parallel Conjuration work reports 18 passing offline
tests but is not yet integrated into the live player/UI. Current work launches no
new live operation and makes no assertion that the game-control switch is off.
