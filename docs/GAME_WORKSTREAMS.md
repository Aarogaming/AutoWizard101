# Maelstrom game and legacy workstreams

Inventory snapshot: **2026-11-09, 22:27 America/New_York**. This is a local,
read-only review of existing inventories, source reviews and current bindings.
It adds no downloads, archive extraction, game interaction or new research claims.
It is a work board, not a declaration that the archived programs are integrated.

The [machine-readable project board](coordination/workstreams.json) is the
coordination record for **30 bounded workstreams**. It contains stable IDs,
owners, status, offline/runtime dependencies, evidence, next tasks and acceptance
criteria. This document is its linked human-readable inventory and roadmap;
neither file is an automatic scheduler or a capability grant. LP01–LP11 remain
source lineage. Update the board when work is claimed, evidence arrives or scope
changes; review its queue manually at milestones or a weekly planning session.
No scheduled automation is created.

The owner expanded the W101 exception to
[reviewed client-side functionality](W101_CLIENT_SIDE_EXCEPTION_20261110.md),
including necessary hooks, injection and memory reads/writes. Network manipulation
remains excluded; the general UI-only default and optional visual operation stay.
Legacy functions are eligible for actual integration, not only reference reuse.
Generic legacy guards remain. The first narrow adapter is described below;
no tool is automatically enabled or marked live-qualified by the policy decision.

Latest adapter source: [wizwalker one-shot observation](WIZWALKER_ADAPTER_20261110.md).
The optional W101 entry reads loading state through the existing scheduler and
native control owner, then disconnects. The pinned Python adapter has bounded
work and preserves recovery on uncertain cleanup. Native/Python fake-client tests
include actual process transport; no real client, desktop or installation ran.
Generic client-internal launchers remain blocked; this is a narrow reviewed path.

Latest qualification: [installed recovery workflow](INPUT_RECOVERY_LIVE_20261110.md).
One real Stop -> Check -> Restore pass reached Game controls restored; final
samples were 1,016 ms apart, the guard was clean, settings stayed unchanged and
no routine started. Maelstrom was closed after the brief check. This began from
an empty legacy-clean guard; live dirty/busy/failure and game outcomes are not
claimed. [Packaging and installation](INPUT_RECOVERY_INSTALL_20261110.md) remain
verified, including binary matching and preservation of 972 existing data files.

Latest source checkpoint: [verified recovery flow](INPUT_RECOVERY_FLOW_20261110.md).
Settings and Routine runs expose an explicit Stop → Check → Restore workflow.
All shared live schedulers drain before fresh release verification; original
recovery evidence is preserved before controls are restored. No routine resumes.
The linked source report records offline verification; the real installed quiet
verification path now passes the separately scoped check above.
The final offline suite passes 1,507 native tests and 62 Python/board checks;
scoped format/analyzers pass, and the four recovery task claims are released.

The preceding [input recovery guard](INPUT_RECOVERY_GUARD_20261110.md) checkpoint
records 1,410 native tests and 62 Python/board checks. Failed warning
saves keep native/Python and same-owner gameplay blocked while offline jobs
continue. Tests are isolated/input-free; nothing is installed or live-qualified.
The preceding [scheduler reuse](SCHEDULER_REUSE_INTEGRATION_20261110.md) checkpoint
records 1,379 tests. The current Devtools reference is updated
41 commits; native immutable admission/history and cancellation repairs are
verified offline. The compiled saved-screen workflow passes with zero desktop
input. Its recovery-marker storage-failure follow-on and the verified recovery
interface are implemented in the source checkpoints above.
The preceding [screen/scheduler integration](MODULE_SCREEN_INTEGRATION_20261110.md)
shows saved W101 pixels passing through actual
local OCR, modules and the existing scheduler; the Dance cleanup source now uses
the shared module path and exact input owner. Navigation/sigil evidence, live
qualification and editor work remain pending. [Current Guild/Steward review](GUILD_STEWARD_LATEST_REUSE_20261110.md)
distinguishes implemented contracts from proposals; the earlier cached review
contributed the first job-recovery boundary repair.
The preceding [selection/collection checkpoint](W101_OFFLINE_QUALIFICATION_20261110.md)
records 80 passing Conjuration tests and four saved Dance OCR/image checks;
those unchanged components were not rerun in this module-only increment.
Actual Conjuration cards and new live qualifications remain pending.
Consult the structured board for remaining gates rather than earlier totals below.

## What is actually in the collection

The fresh recursive filename scan finds **101 compressed files**:

| Classification | Count | Meaning |
| --- | ---: | --- |
| Supplied archives at repository root | 25 | 24 source/tool candidates and one historical Maelstrom distribution. |
| Generated Maelstrom releases in `.local/releases/` | 57 | Outputs of this project, not 57 additional legacy projects. |
| Nested downloaded games, runtimes, references and dependencies | 19 | Enumerated below; mostly outside the W101 integration scope. |

The two Deimos archives are separately preserved revisions of one family. Thus
the 24 non-release candidates represent **23 project families**, not 24 different
games. The earlier [88-item inventory](../.local/legacy-teachers/inventory.json)
has the same 25 supplied and 19 nested items, with 44 generated releases. The
13-file increase is generated releases; it does not reveal missing legacy work.
Counts are a snapshot and naturally change when another release is packaged.

Every supplied archive already has a reviewed-source record in the
[25-project work-package manifest](../research/w101-legacy-work-packages.json)
and [qualification matrix](W101_LEGACY_QUALIFICATION.md). Those preserve source
hashes, selected copied files, original languages, defects, binding paths and
acceptance cases. The [validation report](W101_LEGACY_VALIDATION_20261108.md)
records the earlier component tests. **This inventory refresh did not rerun those
tests or rehash/requalify the source copies.** Local `.local` evidence is not
assumed to exist in a clean checkout.

## Reuse decisions for every supplied archive

The evidence column is the existing catalog's component-level status, not a
working-player certification. “Native target” means a proposed C# binding;
“teacher” means an independently identifiable comparison implementation;
“reference” means useful data or design without executing the old application.
Archive names identify the corresponding record and hashed source-copy path in
the manifest above. LP identifiers refer to the existing
[LP01–LP11 implementation plan](W101_LEGACY_WORK_PACKAGES.md), not a second plan.

| Archive / language | Evidence and intended reuse | Next bounded task / shared dependency |
| --- | --- | --- |
| `afk-wizard-main.zip` / Python | Offline components tested; detector teacher → native visual states. Existing Auto-Idle already supplies movement. | Compare positive battle/HUD evidence on saved frames; an obscured book must remain unknown. **Vision, LP05.** |
| `Aurorium-main.zip` / Rust | Reference reviewed; manifest/version concepts. No new Rust service required. | Produce one saved manifest comparison with affected qualification IDs; its old build has missing cached dependencies. **Assets, LP04/10.** |
| `Automatus-v2-master.zip` / Python | Offline components tested; route/statistics teacher → native route records. | Repair missing imports; represent one route with heading, obstruction and observed arrival. **Navigation, LP06/09.** |
| `cheat-engine-master.zip` / Pascal, C/C++, Lua, C# components | Reference reviewed; target identity and diagnostic UX only. | Map freshness/value-origin display patterns into the workspace; no driver, memory editor or nested app. **References/UI, LP10/11.** |
| `Deimos-Wizard101-main.zip` / Python | Repair required; compiler and decision fixtures → declarative native routines. Internal client launcher is blocked by current policy. | Repair signed coordinates and malformed decks in offline fixtures; translate a bounded condition/action example. **Routines/combat, LP06/08/09.** |
| `Deimos-Wizard101-master.zip` / Python | Repair required; separate historical teacher candidate. Not interchangeable with main. | Repair its sign defect independently and compare identical inputs against main, preserving disagreements. **Routines/combat, LP06/08/09.** |
| `gardening-bot-main.zip` / Python | Repair required; plant/wait/movement teacher → native operations. | Fix key release on interruption; one-plant dry run with need, energy and outcome evidence. **Gardening, LP05/06/07.** |
| `GrubNinja-master.zip` / JavaScript, React, Electron | Repair required; Grub Guardian map/state reference, not a demonstrated player. | Import reviewed vocabulary only; generated scores and reward submissions cannot serve as gameplay demonstrations. **Knowledge/reference, LP03/10.** |
| `libkiwad-master.zip` / C | Offline components tested; independent read-only asset parser baseline. | Repair unsafe initialization and unimplemented compressed reads; compare bounded fixtures. **Assets, LP04.** |
| `On-Wiz-master.zip` / documentation | Reference reviewed; dated hypotheses and schema vocabulary. | Preserve source date, scope and uncertainty in a small claim batch. **Knowledge/reference, LP03/10.** |
| `petdance.zip` / bundled Windows program/assets; Python source-equivalent teacher | Offline teacher evidence; a native C# visual/audio player now exists. Broader arena and complete automatic visit qualification remain partial. The archive is TAR despite its name. | Complete event-driven entry → loading → game → reward → no-feed exit → native Idle, with fresh outcome evidence. **Dance, LP02/11.** |
| `ProjectMaelstrom_win-x64_portable.zip` / compiled Windows distribution | Reference reviewed; historical UX/behavior only. | Compare one useful old workflow with the present workspace; do not nest or replace the current app. **Workspace, LP11.** |
| `raid-utility-tools-main.zip` / Python, Qt | Reference reviewed; four role/location/workflow vocabularies. | Record the four roles separately; defer live composition until solo navigation/combat qualifies. **Knowledge, LP03/09.** |
| `w101-proto-go-master.zip` / Go | Reference reviewed; typed historical message/minigame vocabulary. Missing client module prevents the prior offline build. | Map useful type names as reference data without protocol execution or requiring its runtime. **References, LP10.** |
| `wiz-packet-map-main.zip` / C++ | Reference reviewed; historical schema and asset-tool concepts. | Separate saved-file data utilities from old x86 injection; compare only saved data. **Assets/reference, LP04/10.** |
| `wizAPI-master.zip` / Python | Repair required; alternative matcher/proposal teacher → native visual primitives. | Repair nonsquare centers, scale/coordinate assumptions and unbounded waits; compare identical crops with wizSDK. **Vision/combat, LP05/08.** |
| `Wizard101-Bazaar-Tc-Farming-master.zip` / Python | Offline components tested; native bounded item/page/price search target. No purchase routine is qualified. | Repair no-match/color semantics; emit search evidence within page/time caps. **Search, LP05/07.** |
| `Wizard101-Damage-Calculator-master.zip` / C#/.NET library and older UI | Repair required; directly reusable language, but calculation defects prevent trusted use. | Fix repeat-call mutation, flat enchant, zero-pierce resistance and boss-boost behavior against independent arithmetic fixtures. **Pure evaluator, LP08.** |
| `Wizard101-Farming-main.zip` / Python, OpenCV | Offline components tested; card/enchant/attack proposal teacher → native turn policy. | Separate vision from input; normalize coordinates once; replay shuffled hands and observed turn outcomes. **Combat, LP05/08.** |
| `Wizard101-Utilities-master.zip` / Python, JavaScript, JSON | Reference reviewed; gear/pet/research records. | Import one small sourced batch while preserving unknowns and conflicts; do not invent inventory or snack benefits. **Knowledge, LP03.** |
| `WizGallery-dev.zip` / TypeScript/JavaScript, Next.js, Python | Reference reviewed; frame/location indexing patterns → current recording library. | Attach source/frame/zone annotations without adding another website/database stack. **Knowledge/workspace, LP03/11.** |
| `wizSDK-master.zip` / Python | Offline components tested; matching/wait teacher → native helpers. Internal client launcher remains blocked. | Compare saved crops and bounded waits with wizAPI; retain both implementations until evidence favors one. **Vision/routines, LP05/06.** |
| `wizwad-main.zip` / Python | Repair required; read-only asset importer candidate. | Add signature, bounds, expansion and deadline checks; compare malformed and compressed fixtures. Writer remains deferred. **Assets, LP04.** |
| `wizwalker-master.zip` / Python | Offline components tested; WAD journal parsing and observation vocabulary. Process-memory use is not enabled. | Isolate file parsing from client startup; compare the same saved assets with the other readers. **Assets/reference, LP04/10.** |
| `WizWikiAPI-main.zip` / TypeScript, JSON | Reference reviewed; historical world/quest categories. | Import the reviewed 16 world records with dates/conflicts into existing knowledge/cache structures; no archived API service required. **Knowledge/navigation, LP03/09.** |

This is feature-level reuse, not one wrapper around each legacy executable.
Keep the native application/runtime in C# and bounded offline tools in Python.
C, Rust, Go and web stacks remain independent references unless a measured gap
justifies an adapter. Preserve alternatives and their provenance; the same saved
fixtures should decide which behavior is worth moving into the runtime. Review
licenses/redistribution before bundling copied source or assets.

The previously connected **wizard101-companion, moonlight, wiztype and wizdiff**
are separate entries in the [capability map](W101_CAPABILITY_INTEGRATION_MAP.md),
not additional root archives. Their useful roles are guide/OCR exports, saved
capture/schema comparison, historical type comparison and patch differences.
These are their previously reviewed roles. The expanded W101 exception permits
reviewed client-side integrations as described above; network manipulation remains
excluded, installed files stay read-only, and runtime qualification stays explicit.

## Nested archives and peer repositories

These 19 paths account for every compressed item outside the root and generated
release directory. A filename inventory is not a source or execution review.
Paths below are relative to the repository root.

| Paths | Count | Classification and next use |
| --- | ---: | --- |
| `.local/test-games/reflex-679450da.zip` | 1 | Local timing test-game source; use the existing adapter/fixtures. |
| `.local/test-games/godot-dodge-the-creeps-4.7-6ad6167/2d_dodge_the_creeps.zip` | 1 | Dodge source; existing isolated learner/test environment. |
| `.local/test-games/godot-4.7.2/godot.zip`; `.local/wsl-dodge-tools/downloads-current/Godot_v4.7.2-stable_linux.x86_64.zip` | 2 | Windows/Linux test-game runtimes, not additional players. |
| `.local/reference-games/dueling-diego-flashmuseum/dueling-diego.zip` | 1 | Local reference; previous player-loading blockage unresolved. Not a functioning baseline. |
| `.local/coordination/source-snapshot-001/working-tree-files.zip` | 1 | Local source backup; not another candidate. |
| `.local/model-research/env/Lib/site-packages/pkg_resources/tests/data/my-test-package-zip/my-test-package.zip` | 1 | Dependency test fixture; no gameplay task. |
| `.local/reference-repos/opencode/packages/console/app/src/asset/brand/opencode-brand-assets.zip`; `.local/reference-repos/opencode/packages/console/app/public/opencode-brand-assets.zip` | 2 | Branding assets; no gameplay task. |
| `.local/reference-repos/Guild/_tmp/webfirst-test-2242/Circuit-Simulator-Portable/tools/circuitjs-offline-web-release.zip` | 1 | Circuit-simulator reference output; outside current game scope. |
| `.local/reference-repos/Guild/.venv_precommit/lib/python3.12/site-packages/pre_commit/resources/ruby-download.tar.gz`; same directory `ruby-build.tar.gz` and `rbenv.tar.gz` | 3 | Development-tool dependencies; not player projects. |
| `.local/reference-repos/aaroneous-devtools/governance/archive/prototypes/output-lifecycle/v4/source-v4.tar`; same directory `source-v4-canonical.tar` | 2 | Lifecycle/provenance prototype references; consult existing Aaroneous reuse review. |
| `.local/reference-repos/Maelstrom/Client/Meshroom-2023.3.0-win64.zip` | 1 | Historical photogrammetry runtime; no reason to load it for this work. |
| `.local/reference-repos/Guild/workspace/Circuit-Simulator-Portable-WebFirst-StabilityFixes-20260227-1349.zip`; same directory `Circuit-Simulator-Portable-WebFirst-CompatibilityHardening-20260227-1506.zip`; `.local/reference-repos/Guild/workspace/csim_path_stability_eval/portable.zip` | 3 | Circuit-simulator outputs; inventory-only for this scope. |

The earlier [repository reuse review](REPOSITORY_REUSE_REVIEW.md) screened this
project plus 16 peer clones. Their [local source pins](LOCAL_REFERENCE_SOURCES.md)
are a different inventory, not another 17 legacy ZIPs. Aaroneous/Devtools supply
contract/receipt/resource-governance patterns; Guild supplies candidate action
and observer lifecycle patterns; Library supplies evidence identities; WorkBench
supplies fixture-integrity practices; Merlin remains optional offline model
routing. Original Maelstrom informs the current native UI. Reuse those existing
reviews rather than clone or rebuild every peer again.

## The game work board

The planning inventory contains **14 W101 in-client games: eight fairground games
and six pet games**. This is the set already documented in the
[fairground research](MINIGAME_RESEARCH.md) and
[W101 operating research](W101_GAME_RESEARCH_20261108.md), not a claim that every
historical event game in Wizard101 has been enumerated. Mechanics remain dated
research baselines until qualified on the selected current client. No new fixed
timings or reward formulas are introduced here.

“Research” means the rules baseline exists; it does not mean a recognizer,
solver, full game loop or tested pack exists. All rows require entry/loading,
active play, result and cleanup contracts from the shared runtime. Pure solver
and saved-image work can proceed before live collection.

| Game | Existing basis / readiness | Next independent offline deliverable |
| --- | --- | --- |
| **Conjuration Concentration** | [Pure memory solver](CONJURATION_OFFLINE.md) implemented; 21 tests pass, including 60 shuffled pair/triple games and the independently discovered conflict/removed-card regressions. Current-client perception and complete-player qualification remain pending. | Card-grid/preview perception and an observed-event adapter; verify matching/removal/new-board states before live input. |
| **Potion Motion** | Research and sample-pack fixtures; sample data is not a player. | Connected-group board evaluator and row/column-shift simulator; mark wrapping as an unqualified assumption until observed. |
| **Sorcery Stones** | Research; no qualified solver. | Grid/special-stone/settling labels and bounded swap/cascade cases; distinguish animation from an actionable board. |
| **Choo-Choo Zoo** | Research; no qualified solver. | Tile-connectivity and train/animal/castle fixtures; legal rotation proposals and occupied-tile rejection. |
| **Doodle Doug** | Research; no qualified player. | Maze, player, gem, enemy and bomb-state annotations; route/hazard simulations with measured timing left unknown. |
| **Dueling Diego** | Existing observer/trainer/reference footage; local Flash reference remains blocked at loading. | Reuse aligned cue/action/outcome episodes; test randomized enemy order and uncertainty. No fixed action script or prerequisite to repair Flash. |
| **Skull Riders** | Research; gameplay qualification absent. | Annotated altitude/player/enemy/ground states and collision-risk fixtures; then a bounded action-policy comparison. |
| **Hot Shots** | Research; gameplay qualification absent. | Target/projectile/aim/charge state fixtures and a calibration plan; do not invent charge-to-velocity values. |
| **Pet Dance** | Native visual/audio specialist, teacher comparisons and some perfect games; full visit and arena/UI matrix incomplete. | Finish lifecycle replay and new-build qualification. Keep visual phase recognition primary; arena-specific audio remains separately qualified. |
| **Pet Cannon** | Pet-game inventory/research baseline; no qualified pack. | One rules/control card and instructions/active/result fixture manifest; identify measured aim/power state before a policy. |
| **Pet Maze** | Pet-game inventory/research baseline; no qualified pack. | One rules/control card and small maze/player/goal fixture set; reuse navigation representation, not assumed W101 movement behavior. |
| **Pet Drop** | Pet-game inventory/research baseline; no qualified pack. | One rules/control card and board/selection/result labels; confirm the actual current controls before solver choices. |
| **Grumpy Gobblers** | Pet-game inventory/research baseline; no qualified pack. | One rules/control card and aim/projectile/target/outcome labels; share evidence contracts with Cannon/Hot Shots. |
| **Way of the Ninja Pig** | Pet-game inventory/research baseline; no qualified pack. | One rules/control card and player/obstacle/phase labels; share event-driven motion primitives with action games. |

The five Dance arenas are qualification variants of **one game pack**, not five
new implementations. Tiny/Large/Huge are also variants. The
[arena/snack matrix](PET_TRAINING_LEVELS_AND_SNACKS.md) identifies the available
Wizard City evidence and unqualified cells. Do not claim 15 completed
arena/size combinations from a few perfect Wizard City games. Consult current
delivery/run reports for new evidence rather than silently rewriting old results.

**Grub Guardian** is a separate external-game reference, not a fifteenth game in
this in-client list. **Dodge the Creeps** remains the existing isolated continuous
movement/learning test and has a [dedicated workspace](DODGE_GAME_WORKSPACE.md).
The target remains a repeatable average of 50 across complete held-out games;
this inventory does not establish that target has been achieved. **Reflex** is the
existing local timing baseline, not a required browser-based workflow. Chess,
Minecraft and The Isle are future examples from the discussion, outside this
finite delivery scope.

## Knowledge, resources and navigation are separate workstreams

All domains should share the existing [knowledge model](GAME_KNOWLEDGE_DATABASE.md)
and [SQLite schema](../research/game_knowledge_schema.sql): stable game/entity IDs,
typed relationships, source revisions, scoped immutable claims, actual observations
and qualifications, plus disabled runtime-candidate references. Do not create a
different ad hoc database for each legacy program or confuse a public guide with
the selected user's live character/inventory state.

The first [offline pet-snack importer](W101_KNOWLEDGE_IMPORT.md) exists. It imports
eight sources and nine research claims into a new bundle; it creates no live
qualification and no native application database service. Read-only export,
additional explicit mappings, conflict-preserving merge and app integration
remain separate tasks. Source publication/effective/retrieval dates, game version,
region/platform/realm and local artifact hashes remain distinct. Unknowns stay
unknown; a filename or source timestamp cannot establish current applicability.

| Workstream | Smallest useful task | Reuse / completion evidence |
| --- | --- | --- |
| **Navigation** | Commons → Pet Pavilion as a landmark/heading/arrival route; preserve world and transport eligibility. | Automatus/Deimos vocabulary and existing route recording; saved wrong-world/obstruction/loading/arrival cases. The map is reference, not click-to-teleport. User demonstrations remain human evidence. |
| **Quests** | One bounded quest chain: prerequisites, objective variants, NPC/area links, progress observations. | WizWikiAPI/Utilities/Deimos/companion references; source-scoped graph and replayed branch/progress cases. No whole-world autoquest claim. |
| **Spells and combat** | Repair the pure damage evaluator, then one small spell/effect dataset and observed-hand proposal task. | C# calculator plus Farming/wizAPI teacher comparisons; independent arithmetic and repeat-call fixtures before live combat. |
| **Pets and snacks** | Explicit pet identity, age/XP, stats/caps, school, verified preferences, item quantity and reserves; bounded read-only recommendations. | Existing planner, persisted preferences and [context/energy functions](../ProjectMaelstrom/ProjectMaelstrom/Utilities/PetSnackRecommendations.cs). `NeverFeed` default; other modes plan only. Unknown benefits are withheld; school is not proof of liking/loving. |
| **Energy and outcomes** | Bind a verified current resource snapshot to the next attempt and stop/reserve policy. | Existing pure energy gate: unknown → observe only; insufficient → stop; sufficient → one attempt. A stored reserve setting alone is not live enforcement. Verify before/after game and snack outcomes separately. |
| **Gardening/home routines** | One plant's need → proposed action → observed result, with bounded resources and cancellation. | Gardening teacher and motion/wait primitives; no blind whole-garden macro. |
| **Bazaar search** | Bounded item/price/quantity observations only, after shared vision/knowledge basics. | Existing search teacher. Buying is a separate future transaction. Crown Shop purchases remain locked; no current automatic feeding or purchases. |
| **Asset/version references** | One bounded read-only saved-file reader comparison and change-impact report. | wizwad/libkiwad/wizwalker/Aurorium; malformed/compressed/expansion fixtures and unchanged-original checks. No live process observation. |

Navigation details belong in the existing
[destination plan](W101_DESTINATION_NAVIGATION.md) and
[demonstration contract](W101_ROUTE_DEMONSTRATION.md), not another incompatible
route format. Snacks remain independently configurable; successful Dance play
must not grant permission to consume items. The preferences/planner are real
components, but inventory recognition, verified consumption and safe feeding
dispatch are not thereby complete.

## Shared foundation and finite priorities

Reuse the [scheduler/input owner](SHARED_OPERATIONS.md),
[routine primitives](../ProjectMaelstrom/ProjectMaelstrom/Utilities/W101Routine.cs),
[game-pack action registry](../tools/game_packs.py), and
[session event model](../tools/game_session.py). A recognizer proposes an action;
the selected-target dispatcher returns a receipt; a later observation establishes
the outcome. Keep these three records separate. A proposal or successful key
dispatch is not evidence that the game accepted it.

The initial generic transition runtime is integrated and offline validated. The
[validation report](ROUTINE_FOUNDATION_VALIDATION_20261109.md) records 1,017
executed and passing native tests, zero skipped/unexecuted tests, and synthetic
Dance/Dodge replay traces with eight/six proposals and receipts respectively.
Both replays dispatched zero desktop inputs and remain `live_qualified: false`.
Captured-state adapters and full live qualification remain integration gates.

Use this runtime for bounded waits, loading pause, wall-clock deadlines,
cancellation and outcome checks, with input release remaining the dispatcher's
responsibility. Packs describe states/transitions and registered primitives.
No pack, imported guide or legacy script may introduce
arbitrary execution. Offline fake-clock and recorded-observation replays are the
primary development loop; live checks validate a frozen candidate, rather than
requiring an assistant to supervise every development iteration.

| Priority | Deliverable | Dependencies / exit evidence |
| --- | --- | --- |
| **P0: finish one complete workflow** | Shared transition/receipt/outcome contracts; Pet Dance whole visit and native Idle cleanup; honest qualification shown in its workspace. | LP01/02/05/06/11. Replay loading, missed frames, rejected input, stop and changed scale. Then a frozen-build live batch with actual perfect results and cleanup evidence. |
| **P1: parallel independent slices** | Conjuration perception around the completed pure solver; native visual-matcher comparison; pure damage repair; bounded knowledge export/import extension. | LP03/05/08. Each has owned files, saved/synthetic fixtures, clear unknowns and no desktop/model requirement. Start only as agent slots permit. |
| **P2: compose useful operations** | Qualified Commons route; one-plant gardening dry run; Potion Motion board solver; optional snack review with verified context. | LP03/05/06/07/09 and relevant P1 outputs. Independently measured resources, arrival and results; no hidden purchases/feeding. |
| **P3: expand with evidence** | Remaining puzzle/action/pet packs, bounded quest/combat chains, raid references, additional asset readers. | Reuse completed primitives and datasets; each new game begins with the small fixture/rules task in the board above. Do not enable all legacy runtimes. |

These priorities preserve the Dodge experiment track rather than reset it. A
second game using the same transition/action/evidence contracts is a useful
test of genericity; a second bespoke scheduler is not.

## How to use parallel agents without contention

With four available agent slots, use one coordinator and up to three bounded
offline assignments. Keep a short queue rather than launch one heavy process
for every archive. Current initiatives are the generic runtime
(`CORE-TRANSITIONS`), live qualification of integrated cadence/cleanup (`CORE-CLEANUP`),
and current-client perception after the offline Conjuration solver
(`W101-CONJURATION`). The cleanup worker reported 32 focused tests passing and
the integrated native suite now passes 1,017 tests; fresh full-visit evidence
remains pending. No active
owner is invented for queued work.

1. **Coordinator:** shared contracts, integration, combined validation,
   packaging and the only live-desktop lease. Serialize these operations.
2. **Vision/game worker:** saved-image matcher comparison or Conjuration fixtures
   and pure solver; never both workers editing the same recognition contracts.
3. **Knowledge worker:** one explicit ledger mapping or bounded JSON export;
   preserve provenance and schema rules, with its own staged bundle.
4. **Algorithm/reference worker:** pure damage evaluator or one bounded asset
   parser fixture comparison. No game/model process and no competing build.

Each assignment returns: owned paths, source pin/hash references, evidence kind,
tests/fixtures, failures/unknowns, next integration gate and resource needs.
Comparison variants remain independently named. Changes to shared contracts are
coordinated before implementation; separate import bundles use one committing
writer. Existing legacy code is never executed simply because a review starts.

External-agent briefs may use narrower task IDs while referring to parent board
IDs: `W101-CONJURATION`, `W101-POTION-MOTION`, `W101-NAVIGATION`, `W101-SNACKS` and
`CORE-DELIVERY` for routine-editor design. A proposed brief is unclaimed until
the coordinator assigns an owner and reserves exact paths in the
[claim registry](coordination/claims.json). Optional `planned_outputs` name
candidate future files; they are neither authoritative claims nor evidence and
do not imply that current-client fixtures already exist. Not every board row
needs planned outputs before a concrete assignment is chosen.
Changing a status to `complete` requires the named slice's acceptance evidence;
it never automatically qualifies a full game or a live action.

“Done” for a **research** slice is a sourced, bounded dataset with unknowns;
for a **component**, reproducible offline evidence; for a **player**, complete
observed game outcomes and cleanup under its declared conditions; for a
**release**, the installed workflow with packs/reports preserved. These statuses
must not be promoted into one another by a successful compile or a menu entry.

## Validation of this work board

This update checks filename counts and coverage against the existing manifest,
the 25 unique root names, all 19 non-release nested paths, the 14 distinct W101
game rows, existing referenced files, and scoped whitespace. It does not run
legacy applications, builds, models or gameplay. New runtime qualification
belongs in the owning component's report, not this inventory.

Run the [standard-library validator](../tools/check_project_board.py) and its
[offline tests](../tools/tests/test_project_board.py) from the repository root:

```powershell
python -B tools/check_project_board.py
python -B -m unittest tools.tests.test_project_board -v
```

The validator checks schema/status vocabulary, stable IDs, required tasks and
acceptance criteria, dependency references/cycles, existing evidence paths and
archive/game count reconciliation. Evidence paths must resolve inside the
repository; future files belong in `planned_outputs`, not evidence. It checks
root archive filenames against the current directory, while generated-release
totals remain a reconciled snapshot so a new build does not invalidate old
inventory history. It does not independently prove prose claims or require live
evidence for an explicitly offline component. Invalid boards return a nonzero
exit code and a compact error list.

When a claim registry is supplied, the same check validates its schema,
workstream references and exact contained paths, rejecting overlapping active
claims (case-insensitive for Windows). Released claims no longer reserve paths.
This is advisory coordination, not a filesystem lock. All **22 validator tests**
pass, including malformed dependencies/status, path escapes, missing evidence,
archive drift and active-claim overlap; no live evidence is demanded for offline
components.
