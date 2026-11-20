# Pet Dance postgame operation

The native player now owns an optional bounded return-to-Idle step after an
observed normal game ending. This replaces the supervisor's menu clicks when
the screen can be recognized. Gameplay success and cleanup remain separate.

`Next → Finish without feeding → observe the outside interaction prompt →
Escape → recognize Graphics menu → Idle → verify YOU ARE IDLE`.

Every action needs two stable, fresh observations, the original game process and
geometry, the existing scheduler's input lease, unchanged desktop-activity
baseline and an enabled control setting. There is at most one dispatch per
transition. Loading/unknown images wait within a 45-second deadline. User input,
focus loss, Stop and Escape end menu control without reclaiming the desktop.
No shopping, snack feeding, arbitrary navigation or focus activation is exposed
by this operation. Cancellation still releases a pressed key/button.

The current source adapter consumes the shared [routine graph](ROUTINE_GRAPHS.md)
for sequencing. Each receipt has a run-specific identity and actual release time;
only a newer observed outcome advances it. The trusted action allowlist, OCR,
control bounds and scheduler checks remain in the W101 adapter.

Local Windows OCR reads the selected window only, at most twice per second.
Broad relative regions supplement whole-frame recognition for decorative text;
recognized button bounds are mapped through measured window/client rectangles.
The local OCR engine uses the Windows English language recognizer; its absence
falls back to supervised confirmation, without installing a model or contacting
a service. [Microsoft OCR API](https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr.ocrengine).

Reports contain each observation, proposal, dispatch receipt, result, and capture
cleanup separately. A recognized Idle image does not prove capture stopped or
the game was won. Cleanup uncertainty prevents a Completed receipt. Manual
confirmation remains available after an incomplete automatic attempt, with all
automated input stopped. The scheduler's 310-second bound covers game/loading,
automatic cleanup, and the existing 120-second manual confirmation window.

## Offline evidence and remaining live check

Saved lifecycle footage at Tiny UI supplied reward, snack, courtyard, Graphics
and Idle examples. In `.local/dance-menu-return-replay.json`, 20 ordered frames
produce exactly four simulated actions (Next, FinishWithoutSnack, OpenMenu,
SelectIdle), then recognize Idle. **Inputs sent: 0.** Dispatch receipts in this
offline replay are simulated, not proof Windows accepted a live input.

The separate 32-image classification review also retains menu/in-game/loading
negatives. Gameplay/loading images propose no menu actions. Local OCR took
roughly 55–135 ms per image on this machine. This is a development dataset, not
an independent resolution/arena reliability benchmark.

The installed operation was tried after complete Large-UI PERFECT games. The
22:07 repair package is installed; its latest trial logged 79 stale frames around
500ms old, zero admitted frames and zero menu actions. The user completed the
return to Idle. New source uses bounded short reacquisition after stale frames,
while keeping OCR at most 2Hz and strict freshness. This source repair and the
shared runner pass offline checks but are not installed or live-qualified.
See [the trial record](PET_DANCE_IDLE_LIVE_20261109.md).
The previous three consecutive perfect Huge/Tiny/Huge games used the preceding
installed player and supervised cleanup. None qualify automatic cleanup.

## Pet planning

`pet-training-settings.json` stores arena, Idle choice, snack policy, selected XP
or attribute objective, feed allowance, protected/allowed names and reserves.
NeverFeed is default; corrupt settings retain their original file and load safe
defaults. Names are preferences, not proof of inventory identity.

The read-only recommendation layer ranks verified useful benefit, overshoot and
verified pet preference. Pet/snack school is explanatory context; matching
schools alone does not manufacture a liked/loved bonus. Stat caps and stable
progress must be observed. Energy admission authorizes only one proposed attempt
from fresh Energy/cost evidence above the reserve; unknown values remain
Observe-only. These pure planners are tested but live inventory/Energy reading
and the suggestion UI are not connected. No automated feeding or refill purchase
is enabled. Bazaar restocking needs a separate gold budget and verified inventory
transaction workflow. See [arena/snack research](PET_TRAINING_LEVELS_AND_SNACKS.md).
