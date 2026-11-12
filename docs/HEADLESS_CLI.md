# Headless Maelstrom commands

The same Windows application accepts `--cli` without opening its graphical UI.
The CLI establishes per-monitor DPI awareness so window coordinates and captured
physical pixels agree; this does not change the game's resolution or Windows settings.
It does not open, focus or move windows, start a model, or run a persistent
service. Observation commands send no game input. The separately enabled
`quest-conversation` command can advance one explicitly selected conversation.
The separate [Card Deck API](JOB_DECK.md) now controls the native deck owner
through `--deck-api` and supports a headless `--deck-host`. These use the same
scheduler; they do not expose every workspace or a generic desktop controller.

Every invocation requires `--output <new-report.json>`. The parent directory
must exist. Results are versioned JSON, existing reports are never overwritten,
and the process exit code indicates success or failure. Because Maelstrom is a
Windows GUI executable, a caller should launch it hidden and wait for exit;
do not assume an interactive shell waits for it or displays its console output.

```text
Maelstrom.exe --cli help --output help.json
Maelstrom.exe --cli windows --output windows.json
Maelstrom.exe --cli status --output status.json
```

`windows` returns the current window handle, process ID and process creation
ticks. It reads window/process metadata; it does not open a wizwalker client.
`status` reads the latest 100 durable operation receipts without acquiring
control or clearing a recovery warning. Reports may include local window titles
and paths; keep them local unless deliberately sharing them.

`module-observe` is a single observation probe for the W101 module integration.
It accepts the same exact window/process identity, workspace, stop-file and
output options as `ui-tree-watch`, and requires both
`--enable-client-observation --enable-ui-tree`. It does not accept action flags
or a repeat duration. Under one scheduler lease it reads and disconnects the UI
observer, then captures the selected game window and runs existing local OCR.
The report preserves candidate prompt text and menu control rectangles. It sends
no inputs, changes no focus, and cannot certify navigation or a successful action.
Stale pixels, changed geometry, user activity and uncertain cleanup stop the probe.
Source-only until a release includes this command; this is not an installed-app
claim. Optional `interactionTitle` / `stableInteractionTitle` in UI-tree results
come from the exact visible NPC prompt title path, not the configured card text.

### Bounded transition and route-teaching commands

Source builds also expose `idle-cycle`, requiring the same exact target and both
observation opt-ins, plus `--enable-module-actions`. W101 must already be foreground.
`--cycle-mode round-trip` (default) wakes from native Idle, observes Options and
returns to Idle. `wake` wakes and closes Options; `idle` enters Idle from an awake
view or Options, or verifies that Idle is already active. Each invocation owns at
most three actions and 60 seconds. Loading pauses actions; user activity stops the
operation. These commands neither advance dialogue nor move the character.

`navigation-probe` adds `--key W|A|D --pulse-ms 1..300` with the same opt-ins.
It records before/after evidence around **one explicitly selected teaching pulse**.
It rejects Idle, menus, dialogue, loading and stale observations. It is not automatic
pathfinding: a changed screenshot or accepted key does not establish progress or
arrival. Reports explicitly retain `arrivalVerified=false` and `routeQualified=false`.

Owned observations use two serial UI samples, then restore/disconnect the hook
before capture. The normal `ui-tree-watch` duration is unchanged. Saved game-only
frames support offline diagnosis. Recognition can combine Windows OCR with the
provenance-marked calibrated Idle-label glyph; it still requires the Graphics
heading and aligned neighboring controls. Synthetic checks, saved-frame matching,
live outcomes and installed-release qualification remain separate.

For the optional W101 observation, copy **all three identity values from the
same current window entry**. No first-window selection or automatic retargeting
is permitted. Example placeholders below must be replaced with actual values:

```text
Maelstrom.exe --cli wizwalker-check --workspace F:\Modlists\AutoWizard101 --window HANDLE --process-id PID --started-utc-ticks TICKS --enable-client-observation --stop-file stop.request --output check.json
```

This is the same one-shot loading observation exposed in the W101 workspace.
It uses `WizwalkerOperation`, `OperationScheduler` and the shared per-user
`GameInputLease`. The reviewed backend lives in the explicitly selected repository
and existing isolated environment; optional dependencies are not bundled into
the base portable package. The check does not activate gameplay hooks, move the
wizard, change native Idle, or perform network operations.

`--data-root <folder>` can select a data library (otherwise installed/portable
defaults apply). It **cannot change the shared control-lock directory**. An
existing GUI scheduler using the same data root causes an error rather than
starting a second scheduler. Another control owner or a recovery warning also
blocks attachment. Close the GUI before the initial standalone CLI workflow.

Create the named stop file while the check is running to request cancellation.
An existing stop file prevents attachment. Ctrl+C also requests cancellation
when the caller supplies a console. The process waits for verified worker exit;
the adapter's existing 8-second observation, supervised shutdown and 25-second
scheduler budgets still apply. No command retries or resumes gameplay. The CLI
cannot clear a recovery warning; use the verified recovery flow.

| Exit code | Meaning |
| --- | --- |
| 0 | Command completed; for a check, its operation receipt is Completed. |
| 1 | Invalid arguments, unavailable setup, competing scheduler or report error. |
| 2 | Operation did not complete successfully; inspect receipt and evidence. |
| 3 | Cancelled; no automatic restart. |

The check report references its ordinary operation evidence. A `false` loading
flag is one observation, not proof that a wizard is signed in or ready to act.
Live game behavior and a clean disconnect must be qualified separately from
the CLI's offline fake-client tests.

## Bounded loading watch

`--cli wizwalker-watch` accepts the same target, workspace, explicit opt-in and
stop-file options as `wizwalker-check`. Default: 15 seconds or 32 readings.
Optional `--duration-seconds 1..60` permits a longer coordinated zone test; the
sample cap scales with duration to at most 128. Reads are serial, and every worker
disconnects before the next starts. A read already in progress may finish after
the observation window; the scheduler allows a further 25 seconds for bounded
worker shutdown. This is not a continuous attachment or a high-frequency reflex
feed. The November 11 card/deck release includes this command.

The result links `loading-observations.jsonl` and `loading-watch.json`. A Completed
watch means the bounded observation series finished, not that loading occurred.
Inspect `loadingSamples` and `observedClearLoadingClear`; zero loading readings
cannot prove a transition was detected. The observer sends no game input. Human
or separately authorized routine actions that cause loading must be identified
separately in the experiment report. Prefer walking between zones over energy-
consuming pet-game entry. Use a notification/question as the coordinated start
signal, and leave enough time for the user to receive it.

## Optional UI-tree watch

`--cli ui-tree-watch` uses the same selected target, repository, data root,
stop-file and output rules. It additionally requires `--enable-ui-tree` together
with `--enable-client-observation`. Default duration is 15 seconds; use
`--duration-seconds 1..60` for a bounded observation.

This optional command uses a reviewed temporary root-window hook, including
client memory writes and verified restoration. It is not the hook-free loading
check. It sends no keyboard/mouse inputs, changes no focus and performs no network
operations. One worker under the existing exclusive control owner requests
loading reads every 250 ms and UI trees every second, staggered by 125 ms. Reads
are serial and missed intervals are dropped. Actual rates may be lower.

The command saves `ui-request.json`, `ui-result.json` and bounded
`ui-observations.jsonl`. States include interaction prompt, spoken dialogue/NPC
services, settings/vendor/pay-to-play menus and native Idle. Separate timestamps and
`stableStates` distinguish fresh samples from repeated agreement. Unknown is not
false, and agreement from one source is not independent validation. Idle absence
is negative only for the qualified current-client layout. No state
authorizes a gameplay action. If restoration/disconnect cannot be confirmed,
the existing recovery guard blocks later gameplay. See the
[UI-tree checkpoint](W101_UI_TREE_20261110.md) for source mappings, privacy limits,
testing and live qualification status. Dismissing quest dialogue may advance a
quest; it must not be automatic cleanup or an unconditional path to Idle. This
observer exposes no dismissal action. The November 11 card/deck release includes
this command; each optional worker must still match the executable's reviewed pin.

`--include-combat-context` is a separate observation-only option. It preserves
up to 64 candidate combat controls and raw layout rectangles from the existing
bounded UI header reads. `qualification` stays `structural-unqualified`,
`geometrySpace` stays `raw-layout-unqualified`, and metadata/castability are
explicitly unqualified. These coordinates are not screen or client-pixel click
targets. The context is cleared during loading or failed observations; it does
not supply verified spell effects, player resources, combatant identity, turn
number, victory or quest progress. No combat inputs are enabled by this flag.
The source build now also loads two pinned, optional read-only decoders:
`fieldCandidates` provides typed card/effect/participant/pip observations, and
`duelCandidates` correlates observed combatants to a bounded duel-manager read.
Both remain `source-layout-only`; checked is not yet qualified as selected, and
a duel's victory phase does not identify the winning side. No additional hook
is installed. A `combat-readiness.json` artifact lists the remaining gates.
The result cap is 256 KiB; the tree, field, duel and trace budgets remain bounded.
Acquisition-start timestamps are preserved in results and traces, including
the disconnect delay. Source candidates do not enable the battle player.
See [the integration evidence](coordination/results/W101-COMBAT-CONNECT-06.md).

## Composed motion and Idle visit (source build)

`navigation-visit` accepts the exact target identity, workspace and observation/action
opt-ins used by `navigation-probe`, plus `--key W|A|D --pulse-ms 1..300`.
It runs wake, one selected motion pulse and native Idle through one scheduler lease
and one control scope. No assistant desktop controller is needed. The game must
already be foreground; the command does not steal focus.

The parent budget is 85 seconds; wake/pulse have 20/12-second limits.
Idle finalization has a 40-second total budget, with each attempt capped at 20 seconds.
Each stage has its own run ID and evidence directory, sharing frozen adapter pins
and input ownership. A clean stage failure may still restore Idle; cancellation,
uncertain cleanup or cancellation prohibits further cleanup input. Desktop activity
blocks input unless quiet recovery was explicitly enabled. Successful Idle
restoration does not turn a failed movement stage into success. Reports explicitly
leave route qualification and arrival false.

Idle restoration is always the closing stage of this composed visit. Optional
CLI settings (only on `navigation-visit`):

- `--idle-retries 0|1|2`: default 2 additional attempts, waiting 250 then 500 ms.
- `--resume-after-quiet true|false`: default false. Opt-in allows only the Idle
  finalizer to resume after desktop activity; it never repeats the movement body.
- `--quiet-seconds 2..10`: default 2. Quiet detection waits at most 12 seconds
  within the shared finalization budget, with a 200 ms poll and no model workload.

Each attempt reads fresh game state; already-Idle completes without another
click. Escape, cancellation, a replaced target, held keys, changed geometry,
missing ownership, stale monitor or uncertain release cannot be acknowledged
away. W101 must be foreground; the app never forces focus. Every attempt and
quiet-resume decision is recorded. Failed gameplay stays failed even if Idle
restoration succeeds.

`W101IdleFailsafe.RunAsync` is the reusable finalizer for other W101 routines:
call it after body/input drain, with the same lease, fresh Idle observer/dispatcher,
readiness guard and optional quiet callback. This source change integrates it
with navigation visits first; older quest/Dance/card entry points have not all
been migrated. Do not infer automatic coverage from this shared API.

For user-observed checks, notify before each run, wait for readiness and allow a
short focus-switch delay. Compare the user's visible outcome with the saved
receipt. Current live evidence covers 150 ms left/right turns and automatic Idle;
it does not establish navigation, controller hardware support or all resolutions.

## Selected quest conversation

`ui-tree-watch --include-quest-context` adds bounded objective/dialogue text and
advance-control evidence to the observer result. It sends no inputs.

`quest-conversation` is a distinct action command. It requires the usual exact
window/process/start identity, workspace, output and data root, plus
`--enable-client-observation --enable-ui-tree --include-quest-context
--enable-quest-actions --selection <json>`. The selection contains `id`,
`objectiveBefore`, `dialogueBefore`, optional `expectedObjectiveAfter` and
`maxPages` (default 12, maximum 16). Unknown fields are rejected. Use a new output
path; selection/output/stop paths must differ.

This can advance/accept the selected quest. It observes under the same control
owner, restores/disconnects before addressed Space pulses, verifies changed
pages, and stops after dialogue closes with a new objective. It has a 60-second
budget, stops on desktop activity, and accepts the existing `--stop-file`.
There is no walking, battle or purchase. Production calls now add the shared Idle finalizer described below. See the
[selected-conversation report](W101_QUEST_STEP_20261110.md) for qualification.

## Optional routine loading condition (source build)

The existing `--pet-dance-visual-trial` and `--pet-dance-sound-trial` launchers
accept `--client-loading-workspace=<absolute-repository-path>` together with
`--target-process=PID:TICKS` and `--return-native-idle`. This explicitly enables
the reviewed loading reader **only for the shared postgame menu/Idle modules**.
It does not enable client-based Dance play or create a new controller. Those
existing trial launchers still use their visible Start/Stop host; they are not
new headless gameplay commands. Normal launches remain visual-only.

The parent admits and pins the optional adapter before gameplay; the continuation
reuses its lease. A reading is taken before a fresh image, at most twice a second
and 32 times per continuation. Loading or missing/invalid/stale readings withhold
actions. Clear readings still require two fresh, consistent visual observations
and the usual context, control and outcome checks. The input dispatcher rechecks
reading freshness. See [qualification](W101_LOADING_TRANSITION_20261110.md).

There is currently no `--update`/`-update` command. Updates use the existing
per-user installer and keep game packs/reports outside versioned application
files. A future CLI update command should invoke that tested installer workflow,
verify the selected build and active-work shutdown, and report the installed
version; it should not implement another file replacement mechanism.

## Routine-level Idle integration

Production `quest-conversation` calls now reserve an Idle finalization phase,
including conversations launched by Quest cards. The selected conversation body
remains bounded and independently reported; the finalizer never advances an
unfinished dialogue just to reach Idle. Its receipt distinguishes body success
from verified Idle. Cancellation/takeover prevents further cleanup inputs.

Complete admitted W101 menu/sigil/Idle modules use the same wrapper. Low-level
wake and movement probes remain explicit diagnostics and do not silently Idle
between nested actions. Unavailable combat/farming cards remain unavailable.

Dance's visual and sound trial options now default to `--return-native-idle`.
`--leave-awake` explicitly disables it, preserving an existing false preference.
Both options together are rejected. Dance reuses its visual postgame controller
under bounded shared fail-safe attempts, without enabling client hooks or quiet
resumption. See the [integration coverage](coordination/results/W101-IDLE-INTEGRATION-11.md).


### Optional position candidate observation

`ui-tree-watch --include-pose-context` explicitly requests one serialized
ActorBody observation after the UI watch. Existing target identity and
`--enable-client-observation --enable-ui-tree` flags remain required.
The UI hook is restored before the reviewed player hook uses the shared scratch
block. The player hook preserves RCX and flags, captures an address only, and
restores its five-byte entry and complete scratch baseline before disconnect.
No coordinate write, movement, focus change or second controller is involved.

The report's `poseCandidate` contains nullable XYZ/yaw, acquisition time and read
counts. It stays `liveQualified=false` and `movementAuthorized=false`; current
build compatibility, local-player identity and heading convention must be
qualified separately. Unknown/absent candidates do not become zero coordinates.
The option is unavailable to other CLI commands and disabled by default.

`navigation-visit --include-pose-context --key W --pulse-ms 100` measures position
before and after the existing owned pulse, then finalizes native Idle. Its pulse
stage budget is 24 seconds (ordinary visits retain 12); total visit remains
85 seconds. `--waypoint-distance 40` additionally freezes a nearby target before
input using the archived heading convention; allowed distance is 20–100 units,
with a fixed 10-unit tolerance. This is a calibration trial, not a route follower.
A miss is Failed. Coordinate agreement is `waypointCandidateReached`, while
`arrivalVerified` and `routeQualified` remain false pending independent validation.

The pose option is therefore accepted only by `ui-tree-watch` and
`navigation-visit`; it does not enable movement in the observer command.

### Quiet foreground admission

`navigation-visit --activate-when-quiet true` permits one normal Windows foreground
request for the exact selected W101 process. Default is false. When the target
is not foreground, Maelstrom observes five quiet seconds under the current input
owner before requesting activation, then verifies actual foreground for up to
one second. Held keys, Escape, changed activity, target/geometry change, stale
heartbeat or owner loss abort. It does not restore minimized windows, attach
input queues, send activation keystrokes, clear takeover, or force focus.
Windows may reject the request; gameplay then remains blocked. Admission evidence
is stored in `foreground-admission.json`. Use fresh HWND/PID/start ticks after
client restart. This option does not authorize launching or signing into W101.

### Explicit control recovery

`--cli recover-input --output <new-report.json>` waits for five quiet seconds and runs the normal stop/check/restore transaction without a window or focus change. The CLI probe requires an active session, the same accessible input desktop with input-read permissions, stable nonzero foreground during each sample, no held controls and fresh repeated observations. It never sends game input or resumes interrupted routines. The 25-second budget, durable recovery evidence and global input-owner checks remain enforced. Exit 0 means restored; exit 2 means blocked. The graphical recovery screen retains its foreground requirement.

The UI adapter selects reviewed register/scratch profiles by exact executable digest. The archived R15 profile for `d410a246...` completed live UI/pose observation with restoration, but native Idle text is unresolved; no movement qualification follows from that observation. Unknown builds remain rejected. Combat fields remain unavailable on this archived layout.

### Bounded forward feedback experiment

`navigation-visit --include-pose-context --feedback-waypoint true --key W --pulse-ms 40 --waypoint-distance 60` adds opt-in feedback to the existing identity/action options. Here pulse-ms is the maximum pulse,20–100ms. Destination is frozen20–100game units ahead of the initial heading; later steps never move the target. At most3pulses and6fresh pose readings, a60second movement-stage budget and125second visit limit. Each pulse uses the established scene/input checks and shared owner. Idle finalization remains subordinate to cancellation/takeover.

The local controller updates speed from measured progress, rejects heading errors above0.15radians, stops on stalls/level changes/jumps/moving away, and requires2new readings within10units. It does not turn, infer collision-free paths, cross zones or certify a route. Reports distinguish coordinate arrival from independent route qualification. Repeated trials begin from actual current positions, not replayed actions.

Future start/destination interface: resolve both to zone-qualified landmarks/coordinates, select a versioned route graph, calibrate turn response, follow segments under the same owner, confirm each gate with loading and destination-zone evidence, and return an explicit arrival result. Missing routes or unqualified edges must be reported instead of inventing movement.


## Character-switch qualification stages (source only)

These commands are bounded integration checks, not a qualified unattended Switch Wizard card:

- `character-inspect`: wake, recognize options/Quit, collect the selection screen; does not press Play.
- `character-roster`: inspect the reviewed seat candidates and confirm the configured Fire wizard banner; does not press Play.
- `character-enter`: selected-wizard entry and post-load character-page evidence with attempted Idle cleanup. Missing identity evidence is not success.
- `character-verify`: post-load character-page evidence and attempted Idle cleanup.
- `character-home`: requires an already open, recognized character page; verifies name/level, closes panels, sends Home once, waits for house evidence, then Idle. The house marker remains unqualified for Luke's actual home; do not treat this as a generally working home routine.
- `character-frame`: game-window-only diagnostic; no focus changes or inputs.

All currently use the existing explicit target identity, workspace, data root, new output path, `--enable-client-observation --enable-ui-tree --enable-module-actions` admission. Inspect/roster/enter/home additionally require `--selection <wizard-profile.json>`. Use `--stop-file` for cancellation. Action stages display the countdown and stop on takeover. Frame-only capture has no countdown because it sends no input. Qualification commands do not enable the deck card or install a build.

See [the evidence and remaining gates](coordination/results/W101-CHARACTER-26.md). Level is mutable profile data; matching name plus school distinguishes the observed same-name wizards. House layout and routes remain per-wizard and explicitly unqualified until taught.


### Guided house segments and Idle-only activation (source)

`idle-cycle --cycle-mode idle --activate-when-quiet true` reuses the navigation
quiet-window foreground admission and countdown. Default activation remains off;
user takeover still stops input. This permits recovery without moving first.

`navigation-visit --key W --pulse-ms 1..3000 --assisted-hold true --include-pose-context`
is an explicitly guided forward segment. Turns and ordinary pulses remain capped
at 300 ms. Key release and takeover checks remain active throughout the hold.
After release, assisted segments sample the reviewed loading flag serially under
the same owner, for at most 24 reads / 20 seconds, requiring two clear samples.
The overall visit is capped at 125 seconds. No movement is replayed while waiting.
A loading transition is saved as `navigation-transition.json`; it does not by
itself verify which destination was reached. Very short loading events that finish
before the first sample can be missed. Endpoint displacement across a transition
must not be interpreted as travel speed or a continuous coordinate segment.

Auto-mapping direction: retain wizard/house-specific landmark nodes and directed
entrance edges backed by before/after area observations, loading evidence, poses,
images and traversal outcomes. Observed edges remain candidates until destination
identity is verified; a return edge needs its own traversal. Repeated runs can
refine travel time, approach radius and obstruction rates without a large model.
Current guided snapshots are evidence collection, not an autonomous map builder.
