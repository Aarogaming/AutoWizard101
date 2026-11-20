# W101 destination and quest-intent preflight

Date: 2026-11-11. Workstream: `W101-NAVIGATION`. Task: `W101-DESTINATION-PREFLIGHT-01`.
Owner: `navigation_preflight`. Status: **needs-integration-review**; audit and
pure parser source complete, coordinator validation pending. No destination or
combat operation is implemented or live-qualified by this task.

## Outcome and scope

**Latest steering:** the user changed the tracked objective to **Defeat Any Fire
Elf and Collect Arrows in Firecat Alley. (0 of 3)**. Fireglobe is now a historical
fixture and prospective navigation example, not the current live destination.
Do not execute the old target. The new objective needs combat and collection
capabilities; its parsed text does not make either available or authorize an
implicit battle run. The generic parser described below reflects this update.

Earlier, the user selected **Go To Fireglobe Theatre in Firecat Alley**, with a stop at
verified arrival before dialogue, combat or the next objective. The user reports
the quest arrow shows distance but its direction is vague/broken. The last saved
qualified conversation was Private Quinn in Firecat Alley; it ended with this
exact objective. That is historical context, not a fresh position/zone reading.

The user also clarified that the map is accurate, with zoom changing its scale
and scope. Map topology and road constraints should therefore be primary for
route planning, with localization/heading/zoom calibration. The quest arrow is a
coarse hint, not an immediate steering command; distance is usually useful but
is not a linear or necessarily monotonically decreasing route signal.

The project has useful observation, ownership and bounded routine components,
but no callable, qualified Firecat Alley destination navigator. Do not run the
Python steering dispatcher or a standard macro as a shortcut. The smallest useful
next increment is a typed objective/arrival contract and a narrow navigation
observation projection, followed by bounded walking through the existing owner.
The full trip depends on fresh corridor/threat and arrival evidence, not merely
obtaining the quest target coordinates.

This task read source, tests and saved reports; it did not run tests/builds,
launch a controller/recorder/model, access a game process or installed game files,
use desktop control, browse/download, install anything or send input. Only this
report plus the explicitly reassigned parser/test files are owned/changed.
Central claims and status remain coordinator-owned.

## Actual callable components

| Component | Implemented behavior | Limitation for this pilot |
| --- | --- | --- |
| `Utilities/Modules/NavigationModule.cs` | Compiles 1–16 ordered landmark segments; exact released proposal/outcome binding; Loading/Unknown pause; finite action/time limits. | Caller must recognize landmarks/progress and supply controls. Each segment has one action ID; this is a transition executor, not road perception or a dynamic steering planner. |
| `Utilities/W101SharedOperations.cs` | Binds W/A/S/D primitives to shared navigation; 40–150 ms pulses; reusable sigil/menu/Idle compilation. | `RouteAndMenuVisit` appends X interaction, cancel-menu and Idle. Do not use that entire bundle for an arrival-only quest: build/select navigation alone. World/zone names are required facts, not sensor implementations. |
| `Utilities/W101Routine.cs` | Four named motion primitives: turn-left A, turn-right D, step-forward W, step-back S. | Saved category labels and motion sequences do not implement quest navigation. |
| `DodgeInputPulse.cs`, `HomeMovementRun.cs` | Foreground, target-bound bounded pulse machinery with fresh capture, takeover/cancel checks and key release; home motion has before/after image checks. | Existing home workflow requires confirmed clear home space. Its image-change guard is not a semantic progress/obstacle detector; do not relax that scope to claim road navigation. |
| `W101DialogueInput.cs` | Current selected-process identity, current scheduler input lease, quiet desktop/held-key checks, addressed Space pulse, 200 ms send timeout and mandatory key-up/recovery latch. | Only the exact dialogue action is admitted. It is a strong pattern for a separately reviewed narrow movement adapter, not a generic W/A/S/D API. |
| `WizwalkerOperation.cs`, `tools/wizwalker_adapter.py` | Default-off, source-pinned selected-client loading observation, bounded worker and disconnect validation. | Explicitly accepts only `wizwalker-loading-v1`; no pose, yaw, quest coordinates, enemies or navigation. |
| `W101UiTreeOperation.cs`, `tools/w101_ui_tree.py` | Exact-build reviewed root-window hook, structural UI states, staggered loading reads, cleanup validation; optional bounded tracked-objective/dialogue/advance-control projection. | No navigation distance/heading, current-zone, body pose or combat/threat projection. `menu=false`/`dialogue=false`/`loading=false` does not imply a safe road or no enemies. |
| `W101QuestStep.cs`, `W101QuestConversation.cs` | Exact selected opening text and bounded repeated conversation through MenuModule; requires fresh stable text and released receipts; changed objective plus dialogue closure is the conversation result. | This is the qualified selected conversation, not objective parsing, walking, journal selection or autoquest chaining. The destination pilot must not invoke dialogue progression. |
| `ZoneTopologyService.cs`, `tools/automatus_topology.py` | Static zone-name mapping and breadth-first zone connectivity, including `FIRECAT_ALLEY` / `WizardCity/WC_Streets/WC_Firecat`. | No theatre landmark, local corridor, actor localization, obstacle geometry or proof that the character is currently in that zone. |

Paths in the first table are relative to
`ProjectMaelstrom/ProjectMaelstrom/Utilities/` unless otherwise stated.

## Prototype and legacy findings

`tools/steered_walker.py` has callable angle/pose/waypoint math and a
`SteeredWalkingController.update(pose)` that turns, walks, detects distance stall
and declares radius arrival. Its tests use invented poses and a dry-run dispatcher.
The real `WindowInputDispatcher` calls `PostMessageW` directly, ignores its return
and catches exceptions; it has no native scheduler/lease, target process-instance
check, freshness/zone binding, cancel token, takeover check or bounded hold timer.
Keys stay held between ticks. A lost caller can therefore strand motion. Reuse
pure decision math only after tests; do not activate this dispatcher.

`tools/path_navigator.py` offsets corners geometrically, stops for supplied nearby
enemy points, and implements timed backup/turn/jump/backtrack. It has no actual
road/sidewalk detector, obstacle map, path search, lookahead pure-pursuit algorithm
or threat sensor despite broader header wording. Corner offsetting alone cannot
prove free space. `RoamingEnemy.threat_radius` is unused by `evaluate_threat`,
which uses one global distance. Unknown enemies default to an empty list.
Recovery is evaluated **before** enemy checks, has no finite overall retry budget,
and sends S/D or W/Space without fresh scene checks. For the destination pilot,
stuck means release and stop; do not enable this recovery FSM or automatic combat.

`tools/macro_pipeline.py` supplies useful offline pairing, RDP simplification and
action dictionaries. `MacroExecutor.tick` is not an owned live executor; its
`timeout_s` is not enforced by that method, and nodes advance without shared
dispatch/outcome receipts. Navigation uses ordinary `atan2(dy, dx)` while
`steered_walker.calculate_target_yaw` uses a different W101-style heading basis.
Cardinal-direction and wraparound fixtures must reconcile the convention before
reuse; their existing unit tests do not establish live turn direction.

`Utilities/MacroPlayer.cs` converts `navigate_path` to an `InputCommand` containing
only `Type="navigate_waypoints"` and the node ID. It drops the waypoint/zone
payload. Repository search found no native handler for `navigate_waypoints` or
`combat_gate`; `InputBridge.cs` handles click/key/delay with an unsupported default.
Parsing this macro does not yield a working route. The legacy background
`PlayerController` does share `GameInputLease` and expires held keys after two
seconds, but its hook readiness only checks `IsWindow` and policy; it lacks the
selected process-instance and game-outcome evidence required here and ignores
`PostMessage` results. Prefer the stronger newer operation/input pattern.

`tools/zone_routes.py` contains five hard-coded hub routes, none in Firecat Alley.
Its comments and [macro report](MACRO-PIPELINE-01.md) call them “verified,” but the
route tests only check route count, nonempty labels, serialization and executor
compatibility. There are no source-capture hashes/calibrated coordinate evidence
in those route definitions. They are unqualified data, not live measurements.

Archived wizwalker exposes `client.body.position()`, `body.yaw()`,
`quest_position.position()` and `zone_name()`. The body/quest objects rely on
separate hooks/base addresses and offsets. Only loading and the reviewed current
root-window subset have been integrated/qualified here. Do not enable all hooks
to obtain navigation data. Deimos's `questing.py` itself re-reads quest position
after a delay because it can be wrong. `teleport_math.py` offers nav-data parsing
and geometry, but its teleport/jitter loops are not walking. The old proximity
defect is recorded in the destination-navigation review. Those routines require
separate review; no teleport or file access is implied by this audit.

Archived `wizAPI.py:face_arrow` activates the window, holds A and spins in an
unbounded fixed-pixel color loop, then applies a D correction. It is reference
material only; neither a safe observer nor a qualified arrow follower.

## Objective, arrow and road evidence

The saved exact-client fixture
`tools/fixtures/w101_quest_context_live_20261110.json` contains visible
`Root/WorldView/windowHUD/QuestHelperHud/ElementWindow/ArrowWindow` (node 70).
It is recorded with `childrenSkipped=true`; no angle, distance or geometry is
projected. Current code allowlists only `txtGoalName`, dialogue text and advance
enabled state. The existence of ArrowWindow is a concrete place to investigate,
not proof that a distance/angle property is available or valid. A separately
reviewed extension should collect only known relevant fields; otherwise use a
bounded, calibrated visual crop. Preserve unknown when the arrow is broken,
hidden or inconsistent with observed progress.

Use the exact objective as primary semantic intent. The new deterministic parser
recognizes an allowlisted grammar and returns data corresponding to this earlier
destination example:

```json
{
  "kind": "go-to",
  "target": "Fireglobe Theatre",
  "area": "Firecat Alley",
  "sourceObjective": "Go To Fireglobe Theatre in Firecat Alley",
  "completion": "verified-destination-arrival",
  "followNextObjective": false
}
```

The parser maps `Explore`, `Talk To`, `Defeat`, `Collect` and the compound
`Defeat <enemy> and Collect <item>` to separate typed intents, with targets/area
retained. Only the exact bounded `(current of total)` suffix is read as progress;
other count formats remain unsupported. Do not
treat a parsed verb as an enabled capability or permission: Talk needs a selected
conversation contract; Defeat/Collect need their own qualified routines. Bind
intent to exact objective text/hash, selected client, source time and visit.
Quest title/descriptions can disambiguate after a narrow reader exists; the
current observer does not supply a full journal/title/description feed. Unknown
verbs, missing area, changed objective or ambiguous split produce no action.
The user should not have to author separate low-level movement prompts.

Map geometry/topology should constrain the route. Before using its pixels as
positions, qualify current map scale/zoom, player-marker location, heading and
map-to-view correspondence. Zooming is a separately bounded UI action with a
fresh map check, not a fixed screen-coordinate assumption. A road-following
policy must permit corners, detours and temporarily increasing quest distance.
No blind arrow-heading steering is proposed.

Distance is progress support, not a road direction or arrival witness. A route
around a building can temporarily increase distance. A straight bearing to quest
coordinates can cross walls/enemies. Road/corridor recognition must choose the
next locally safe landmark; map/topology and optional pose can corroborate it.
No implemented W101 road/sidewalk recognizer or saved Fireglobe route fixture was
found in the inspected production helpers/tests. The Commons recording cannot
substitute for that missing scene evidence.

## Prospective destination pilot and completion

This sequence remains design guidance only; the active user objective is now
the defeat-and-collect objective above. A stale Go To card must not execute.

1. **Fresh observation preflight:** under the same current target and opt-in,
   confirm the exact objective, current scene/zone, no dialogue/menu/Idle/loading,
   capture geometry and safe next corridor. Current position remains unknown
   until this succeeds. Stop if in another zone; cross-zone routing is a separate
   stage. Capture a small theatre approach/arrival example if missing.
2. **Offline contract:** parse only this Go To objective into a destination intent;
   bind independently reviewed corridor/landmark observations and actual source
   hashes to it. Add wrong zone, broken arrow, stale/reused sample, objective
   change, obstruction, threat, loading and takeover cases before walking.
3. **One movement qualification:** propose one 40–150 ms W/A/D pulse in confirmed
   free space; send through one scheduler lease and narrow native adapter; release
   before fresh observation. Verify the direction/progress independently. No
   keys remain down while a slow observer attaches/reads/restores/disconnects.
   The existing two-second dialogue observation cadence is not a continuous
   walking feedback loop. Keep initial cycles released and serial.
4. **Short destination attempt:** proposed budget is one attempt, at most
   60 seconds / 32 pulses, 150 ms maximum each, stopping earlier for uncertainty.
   These are proposed first-pilot caps, not claims they suffice for the whole
   distance. Replan the next safe local movement after each outcome; do not replay
   fixed linear actions or perform automatic jump/backtracking recovery.
5. **Arrival:** require a fresh theatre-specific landmark/name/prompt plus current
   Firecat Alley context, no loading, and a second stable post-release observation.
   Optional qualified coordinates/distance corroborate this; none alone proves
   the named destination. Preserve screenshot/observation references and the last
   proposal/receipt. A generic X prompt is not theatre identity. Stop before X,
   Space, doorway/sigil interaction or following the newly tracked objective.

Whether approaching the theatre automatically changes the objective or opens a
scene is not established by saved evidence. Define the endpoint during preflight:
an observed exterior theatre arrival may complete the destination task without
claiming the quest objective was completed. If approach automatically progresses
the objective, record the observed change and stop; never infer permission for
the new step. An unexpected dialogue/loading/combat scene is a stop/unknown
outcome, not successful arrival without destination evidence.

Walking-only policy excludes energy-spending routines, purchases, potion use,
snacks, portals/teleports and battle actions. It cannot promise zero combat merely
because the goal has no energy cost. Fresh route/threat exclusion is required;
unknown threat state never becomes “safe.” With the currently missing sensors,
the first deliverable is observation/fixture readiness, not unattended walking.

Manual takeover immediately cancels and releases. No auto-resume, focus recovery
or fresh downs after takeover. Unconfirmed release/hook restoration/disconnect
marks recovery required and preserves failure evidence. Native Idle is a separate
authorized cleanup outcome: use it only within the coordinator's current control
window when the scene permits harmless menu entry and the banner can be verified.
Never dismiss dialogue to achieve Idle or resume control after user takeover.

## Implemented generic objective parser

The coordinator expanded this claim to two source files:

- `ProjectMaelstrom/ProjectMaelstrom/Utilities/W101QuestIntent.cs`
- `ProjectMaelstrom/ProjectMaelstrom.Tests/W101QuestIntentTests.cs`

`W101QuestIntent.Parse(raw)` is pure and bounded to 256 raw characters. It exposes
`Action`, `Target`, `Area`, `RawObjective`, `IsRecognized`, `UnsupportedReason`,
`ExecutionKey`, `ExecutionNeeds` and `ArrivalOnly`. Defeat/collection additionally
exposes `Enemy`, `Item`, `CurrentCount` and `RequiredCount`. No quest-name-specific
branch, quest ID lookup, input dispatch, process access, provider read, scheduler
or card submission is included.

For the current user-supplied text it yields `DefeatAndCollect`, enemy `Any Fire
Elf`, item `Arrows`, area `Firecat Alley`, progress `0 / 3`, execution key
`combat-collect`, and needs `navigate`, `combat`, `collect`. Those are required
capabilities for a pending card, not evidence that the capabilities are present.
Names remain literal text. No item drop rate, number of battles, map coordinate,
quest chain or game identity is inferred.

Grammar matching ignores case and collapses horizontal whitespace while retaining
proper-name case and bounded raw text. One sentence-ending period is accepted
around the exact progress suffix. Progress uses ASCII nonnegative integers with
`0 <= current <= required <= 10000` and `required >= 1`. Missing/ambiguous area,
unknown verbs, other compound operations, embedded/ambiguous counts, malformed
progress, control characters and oversized text produce explicit unknown results
without execution keys. Raw text exceeding the budget is not retained. This
conservative grammar may reject legitimate names containing “and,” “or,” or
“in,” count words, or punctuation outside the small name vocabulary; it must show
unsupported rather than guess. It is portable across the recognized verb
families, not a claim to understand every quest wording/localization.

Tests cover all six action families, generic targets across areas, the actual
saved Quinn objective, the new user-supplied defeat-and-collect text, unrelated
synthetic compound targets, grammar case/whitespace/punctuation, progress bounds,
malformed/ambiguous counts, missing area, compounds and unknown verbs. The saved
Quinn fixture is a replay source; the new Fire Elf fixture is user-supplied text,
not a fresh game read. Root owns format, analyzers, build and test execution;
no passing result is claimed here before those checks.

## Candidate implementation paths; not newly claimed

| Bounded increment | Exact candidate paths |
| --- | --- |
| Destination acceptance/replay | Reuse the new `W101QuestIntent.cs`; add destination acceptance fixtures under `ProjectMaelstrom/ProjectMaelstrom.Tests/TestData/Navigation/` and a tested observation reducer. Reuse `W101QuestContext`, `RoutineBinding` and the existing module runner. Do not duplicate the intent parser or bind a run to the now-stale Fireglobe objective. |
| Narrow arrow/zone/pose observation review | `tools/w101_ui_tree.py`, `tools/tests/test_w101_ui_tree.py`, `tools/fixtures/w101_ui_tree_review.json`, `ProjectMaelstrom/ProjectMaelstrom/Utilities/W101UiTreeOperation.cs`, `ProjectMaelstrom/ProjectMaelstrom.Tests/W101UiTreeOperationTests.cs`. Add a separate reviewed navigation observer if body/quest hooks are needed, rather than widening the root observer implicitly. Pins must be updated together. |
| Destination orchestration and bounded movement | New `ProjectMaelstrom/ProjectMaelstrom/Utilities/W101DestinationOperation.cs`, `W101NavigationInput.cs` in that same Utilities directory, and corresponding `ProjectMaelstrom/ProjectMaelstrom.Tests/W101DestinationOperationTests.cs`, `W101NavigationInputTests.cs`. Reuse `OperationScheduler`, `GameInputLease`, `RoutineRunner`/`ModuleRunner`, and the `W101DialogueInput` ownership/release pattern; no second dispatcher. |
| Shared navigation data binding | `ProjectMaelstrom/ProjectMaelstrom/Utilities/W101SharedOperations.cs`, `ProjectMaelstrom/ProjectMaelstrom/Utilities/Modules/NavigationModule.cs` and their existing tests only if a minimal arrival-only/dynamic-proposal seam is necessary. Preserve existing route/menu behavior; do not silently remove actions from other callers. |
| Offline geometric repair | `tools/steered_walker.py`, `tools/path_navigator.py`, `tools/tests/test_steered_walker.py`, `tools/tests/test_path_navigator.py`; separate pure proposals from dispatch and test finite values, yaw basis, stale samples, bounded stuck behavior, corridor validity and threat checks during every state. |

The coordinator currently owns CLI/deck integration files; do not edit or expand
their claims from this audit. Keep a destination card unavailable until its real
observation/action/outcome bindings exist. The existing conversation card remains
a separate capability with its own verified outcome.

## Lightweight offline work suitable for a longer development window

- Extend supported objective grammar only from real bounded examples; implement
  the arrival reducer with no input/process APIs; use
  at most 128 synthetic/saved observations per fixture and deterministic clocks.
- Complete the existing proposed `W101-NAV-LANDMARKS-01` evidence rubric, then
  annotate a small number of already saved Commons frames. Keep this explicitly
  same-recording landmark work; it does not qualify Fireglobe or new live runs.
- Compare the two heading conventions on cardinal/intercardinal cases and test
  near-zero distances, angle wrap, nonfinite values and vertical/zone mismatches.
- Replay false progress, threat during recovery, same radius in wrong zone,
  stuck turn, objective change, cancelled read and unconfirmed release. Prefer
  stop behavior to speculative unsticking in this pilot.
- Correct the legacy macro/route readiness labels and make unsupported native
  command kinds fail visibly in a separately owned task. Add provenance fields
  before route definitions can claim observed coordinates.

Each should be an explicitly claimed, bounded source task. Suggested checks are
single-process pure tests under five minutes, small JSON traces and no GPU/model,
capture, live observer, installer or unattended game loop. The coordinator retains
build/test scheduling. Overnight availability is not authorization for live play.

## Evidence and verification of this audit

Reviewed [current workflow](../../W101_WORKFLOW_STATUS_20261110.md),
[selected conversation evidence](../../W101_QUEST_STEP_20261110.md),
[UI-tree review](../../W101_UI_TREE_20261110.md),
[destination design](../../W101_DESTINATION_NAVIGATION.md),
[actual human demonstration](../../W101_DEMONSTRATION_20261108.md), current source
and test bodies. Dated docs sometimes retain older limits/status: the current
owner exception governs client-side scope, and current source already adds some
telemetry freshness/exception/finite checks absent from the older telemetry
review. `W101TelemetryService` still has no concrete live provider/consumer found
for this pilot; it is not a navigation sensor.

The inspected `NavigationModuleTests` explicitly cover released receipt binding,
wrong/stale landmarks, loading, takeover and action budgets. Python steering/path
tests use synthetic coordinates and dry-run dispatch. Standard route tests prove
schema compatibility only. The prior conversation report records 1,688 native
tests and 31 Python observer tests passing; this audit did **not** rerun them or
claim those totals for the current changing checkout.

Selected SHA-256 identities collected by `Get-FileHash`:

| Input | SHA-256 |
| --- | --- |
| `tools/steered_walker.py` | `7C06710D1B6BFE2C699CC76BED6536251C623530E7876BF4696FFB744B1A45EB` |
| `tools/path_navigator.py` | `E3121ADCAD56C8FF0A36B4BDC47433E4F1EB8DAB7A506A14A5C27B4EB861C2D1` |
| `tools/zone_routes.py` | `DB4D9A1832E70E1AEBE35A3200611329ADC9D8D15B569EC258D706073CE5AC5E` |
| `tools/w101_ui_tree.py` | `2D94FA263DB106448AFDD658FFD485D9FC958999D9B9855CDF6C4054008691E1` |
| `tools/fixtures/w101_quest_context_live_20261110.json` | `DE00895A3FE1C5C558BA777158FBD30043476F7E34BCDBEA8BBC222F41D42CE4` |
| `ProjectMaelstrom/ProjectMaelstrom.Tests/TestData/QuestConversation/quinn-offer.json` | `9654DB1E447EDDA7A019925FC759C2E423C8B4434BA57348DEE21F1C7816E0D8` |

Checks: read-only `rg`, `Get-Content`, `Get-FileHash`, `git status --short`; scoped
whitespace/link checks after writing: all three owned files have zero trailing
whitespace lines, all six relative Markdown links resolve, and scoped
`git diff --check` exits 0. These new files are untracked, so the explicit
line-by-line whitespace check supplies coverage that Git's tracked diff does not.
Initial path probes for several
guessed executor/input filenames returned not-found; the actual implementations
were located/read and no conclusion relies on those failed probes. Format,
lint, build and test execution remains with root for the expanded parser task.
Running task processes/desktop use: none. Integration request: accept the audit,
choose exact observation/intent increment owners, and keep live route qualification
separate from the existing selected-conversation result.
