# W101-CHARACTER-27 — house route teaching and transition recovery

Workstream: W101-NAVIGATION. Owner: root. Status: integration in progress.

The current slice repairs bounded HUD traversal, adds explicitly assisted forward
holds, and connects Idle-only recovery to quiet foreground admission. The earlier
Idle failure was a genuine desktop takeover, not a false Idle classifier result;
no guard was removed. A fresh CLI recovery independently observed native Idle.

Source adds a bounded straight walk-until-loading option under the same owner,
countdown and target checks. Loading observations are serial, lightweight reviewed
one-shot reads; no model, second controller or video capture job is used. Forward
input releases on loading/unknown, takeover or stale readings. An entrance event
is distinct from destination identity and from a replay-qualified route.

## Live evidence (local, not portable artifacts)

- `farm-27-forward-3.json`: 2483.15-unit movement; Idle stopped on desktop activity.
- `indoor-27-idle-3.json`: stopped before input because target was not foreground.
- `indoor-27-idle-4.json`: quiet activation and independently observed native Idle.
- `indoor-27-forward-1.json`: 823.55-unit movement and verified native Idle; saved
  pixels show bridge approach, not indoor arrival.
- `indoor-27-forward-2.json`: desktop takeover before action; no movement.
- `indoor-27-loading-walk-1.json`: extra startup reading invalidated initial screen
  freshness; no successful movement; native Idle restored. Removed redundant
  startup read in the trusted runtime; dispatch still validates fresh clear UI.

All receipts are below `.local/w101-cycle-06/`. The study manifest is
`house-route-study-27/study.json`. It retains the owner-estimated 100 units/step
as an estimate, not measured gait. Observed mount displacement is approximately
782–828 units per commanded second, not a universal speed calibration.

## Limits and follow-up

The farm turn was passed; farm, fairgrounds and storage destinations remain
unqualified. The complete unattended character-switch card remains unfinished.
No installer was built or run. Source is not the installed release.

Auto-mapping should persist directed entrance edges between wizard/house-specific
area nodes, with before/after observations, loading evidence, poses and outcomes.
Reverse travel must be observed independently; changed house layouts invalidate
old approach assumptions. Repetition can refine timings and failure rates without
large-model inference. The current study is evidence collection, not a working
autonomous mapper or winding-road follower.


## Loading-walk incident and repair

`indoor-27-loading-walk-2.json` / operation
`26a6d58c414541cdbc1802f76380adf1` observed loading on its eleventh sample,
but synchronous key-up delivery was not acknowledged. The owner independently
reported continued walking after loading, then stopped it manually. The operation
correctly latched CleanupFailed and did not attempt Idle. This is **not** a
successful autonomous entrance/Idle sequence. No arrival coordinate is certified.

The source repair queues only the same key-up when synchronous delivery times out
and retries acknowledgement for at most five seconds, without another key-down.
Unconfirmed release still blocks gameplay. This transport repair requires live
qualification; queue acceptance alone is not treated as release verification.


## Successful reverse crossing

`indoor-27-loading-walk-4.json` / operation
`a8afb625865f4cf2854ec7a6691e1e18` completed: nine loading-watch samples,
loading observed, key release acknowledged, two clear readings, new pose, and
native Idle verified. Owner independently reported success. Saved final pixels
show the exterior courtyard and YOU ARE IDLE. The owner aligned the character
before this trial; autonomous steering and repeatability are not established.

Outside arrival: X -4.07338, Y 9015.84668, Z 14.71118, yaw 0. This is an observed
landing landmark, not the exact trigger boundary. Interior approach and exterior
landing use separate coordinate spaces; cross-loading distance is null.
House-linked evidence is saved locally in
`live-data/wizard-profiles/9d82d54801774708b7294a837216cfbc.house-landmarks.json`.
It is an evidence sidecar, not an enabled runtime routing database.

The successful live report had a misleading summary reason no-loading-observed
for its post-release clear-only phase while SawLoading was true from the walking
phase. Source now preserves the combined loading-cleared reason; original evidence
is unchanged. Full native suite passed 2437 at the release-retry checkpoint.

## Named interaction follow-up

Owner requested a reusable exact-title prompt interrupt and home-spawn/backward
World Gate calibration. Source now includes a single owned streaming UI observer,
exact target-title match (unrelated prompts ignored), stable confirmation after
stopping, bounded W/S movement, and no X input. Updated the reviewed archived UI
profile's exact NPC-title text field and title classifier; 61 Python reader tests
pass. This is not a claim that World Gate title recognition is live-qualified.
168 focused native tests pass before the Home calibration. Final formatting,
analyzers and full-suite checks will follow live qualification.


## Home semantics and prompt calibration

`indoor-27-home-1.json`: Home was accepted/released; the owner observed arrival
in the dorm. Two clear readings before the delayed teleport had been mistaken for
transition completion. Source now requires an actual loading observation for Home
before accepting the clear phase. A legitimate loading-only UI result with no tree
timestamp now remains paused evidence rather than a timestamp exception. No Home
retry was sent. The owner manually used Home again after cooldown and returned to
the equipped house; World Gate was already visible, so reverse steps are unnecessary.

`world-gate-27-observe-1.json` recorded two exact World Gate titles and the original
home-spawn pose. Final admission rejected the combined pose/UI reading as older
than the two-second action window. Report validation now accepts up to five seconds,
while dispatch checks retain the two-second bound. This does not make stale facts
eligible for movement.

The owner reported cat mouse activity behind repeated takeovers and requested
keyboard-only takeover. Added a persisted W101 settings checkbox (default false),
configured true only in this test data root. Keyboard/Escape and foreground guards
remain enabled; no raw key identities or mouse positions are logged. Generic game
monitors retain keyboard-and-mouse behavior. Full checkpoint resume after focus loss
is still a follow-up; current opt-in quiet recovery resumes Idle cleanup only.


## World Gate check passed

`world-gate-27-check-2.json` / operation
`eb15c994e733405fa149b4a5b6d3f55b` completed with keyboard-only takeover enabled.
Exact Title and StableTitle were World Gate. `inputDispatched=false`,
`interactionDispatched=false`, before/after pose identical (planar distance zero),
and native Idle verified. Home-spawn landmark saved alongside front-door exterior
landing in Luke's house evidence sidecar. Moving into prompt range remains to be
live-qualified; this test establishes the already-present target branch only.

Current game state at completion of live checks: verified native Idle at equipped
house World Gate spawn. No CUA/desktop-control session, capture job or model was
started. CLI scopes and their observers disconnected after each bounded visit.


Owner-requested next composition: reusable inter-world gate travel. Reach gate,
verify exact prompt, invoke registered X interaction, recognize destination menu,
select an explicitly configured world, wait through loading, verify actual world,
then native Idle. Quest navigation calls this routine when world context differs.
Destination selection and actual-world verification remain unqualified; no world
switch was requested or sent in this slice. Unknown/locked destinations must not
fall back to the first menu item. Keep each step's receipt separate from arrival.


## Final checks for this source increment

- Native warnings-as-errors build: zero warnings/errors.
- Full native test suite: 2448 passed.
- Python UI-reader suite: 61 passed.
- Scoped whitespace format + verify and analyzers: passed for edited C# files.
- Python Black check and compileall: passed.
- Project board validation: passed (30 workstreams).
- Owned live CLI process IDs no longer running; no desktop-control session held.

No installation performed. Source increment is verified as above, but complete
unattended character switching, moving-to-prompt qualification, focus-resumable
job bodies, road steering, automated map construction and world selection remain
open. W101 was last independently observed in native Idle at World Gate spawn.


## World Gate interaction follow-up (November 12)

Owner authorized testing gate interaction. Added bounded CLI world-gate-inspect
and world-gate-close using the existing scheduler owner, countdown, X/Escape
transport, optional UI tree, game-only capture/OCR and native Idle cycle.

`world-gate-28-inspect-1.json` opened the actual Spiral Map after exact prompt
verification and one X. Saved pixels show page 3/3: Zafaria, Avalon, Azteca,
Khrysalis; no destination selected. Initial text qualification incorrectly assumed
early-world names; subsequent OCR omitted O in WORLDS. Replaced that assumption
with page-independent labels plus two UI-tree samples of the map's named controls.

`world-gate-28-close-3.json` / operation dfa23ee1dbd74593bc4d34a337583cdb
verified the menu, sent Escape, and independently verified native Idle.
No world travel, Home command, cooldown bypass, purchase or desktop tool used.
Full-cycle repeat and final checks follow below.


The repeat `world-gate-28-inspect-2.json` found the exact range prompt absent
after wake, so withheld X. A clean-up-only CLI call `world-gate-28-idle-1.json`
verified native Idle again. Source now also attempts the normal observed Idle
cycle on the missing-prompt branch; no blind reinteraction or movement was added.
Opening the gate and closing/restoring Idle are live verified separately, not a
repeatable complete visit. Prompt re-arming after leaving the menu remains open.
The menu already had a game-selected world; reports' earlier worldSelected=false
meant no selection was dispatched by Maelstrom. New source uses the explicit field
worldSelectionDispatched=false to avoid confusing UI selection with agent action.


World Gate increment final validation: full native suite 2459 passed, zero failed/skipped; warnings-as-errors build and scoped whitespace/analyzer checks passed. Project board valid (30 workstreams). No installation performed. Live qualification remains partial: menu opened and close/Idle verified separately; repeat prompt recovery and destination travel remain pending.


## World Gate recovery audit — November 12 (task 29 follow-up)

Correction to the previous follow-up: Escape did NOT close Spiral Map. Saved
wake/Idle pixels from c94fb1b1f8cd47ddb244c60a32824064 show the map underneath
native Idle. The earlier menu classifier omitted Spiral Map and conflated an
Idle overlay with an exited map. Previous close success is revoked; Idle itself
was observed. New reader recognizes the paired named Spiral Map controls, and
new close logic requires menu disappearance independently of the Idle outcome.

A rejected movement dispatcher and a delivered/released 100 ms reverse pulse
were recorded; neither recovered the prompt. A later 100 ms forward pulse also
did not clear the overlay. These movement workarounds were removed from source.
An exact-size calibrated Exit click was delivered but map disappearance was not
observed (04736a3b512f4392a759c05cdfce6361); correctly reported Failed. Recovery
now tries one exact-title World Gate reinteraction for the overlapping prompt.
No world travel or Home command has been sent during this follow-up yet.


Recovery succeeded in 44b14ac801ef482b85705c1ae2c2c5f9: exact World Gate
reinteraction removed the overlapping prompt; fresh OCR found Exit; click,
menu disappearance and Idle were verified. Saved final pixels show the actual
house World Gate, not Spiral Map. Removed ineffective movement and fixed-size
coordinate fallback from final source.

Two full repeats passed: 49df9a316f954498b768e14d79eb0a66 and
82bf88946b054248a7b659210706252b. Both require exact prompt before X, actual
Exit click, independent menu absence and native Idle. Reader suite:62 passed;
85 focused native CLI/menu/transition tests passed before travel qualification.


### Live round-trip result

Current-selection travel 694e436c33674118b13d56b3f5df14c7 completed: seven
loading samples included true and clear; destination gameplay/HUD observed and
native Idle verified. Saved arrival image shows Silent Market/Moon Cliffs/etc.,
corroborating the menu's Khrysalis selection. Native report correctly leaves
exact destination identity unverified; the landmark interpretation is review.

Home return df12bac57f0c478ba7293280502dd943 completed with one normal Home
command, loading observed and cleared (11 samples), after-position exactly
matching the previously saved equipped-house spawn within 0.001 game units:
(-17.00933074951172,-20.727779388427734,-329.2207946777344). Final saved
pixels show the equipped house, World Gate prompt and YOU ARE IDLE. Home was
visually available before dispatch; the arrival image displays its normal
cooldown. No bypass or retry spam; active-cooldown waiting was not exercised.

Derived summary: .local/w101-cycle-06/world-gate-29-round-trip-summary.json.
Two complete menu repeats and one travel/Home round trip succeeded through
separate bounded CLI calls. This does not establish a single-call travel card,
automatic world selection, machine-verified zone identity or automatic cooldown
waiting. No installation. No desktop-control tool session was used.

Final source checks:2460 native tests passed (zero failed/skipped);62 Python reader tests passed. Scoped C# whitespace format/verify and analyzers passed; warnings-as-errors compilation passed. Python Black/compileall passed. Board validator passed30 workstreams. All owned live CLI process IDs were checked absent; W101 was left at equipped-house spawn in verified native Idle.
