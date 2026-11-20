# W101 destination navigation audit and candidate methods

Date: 2026-11-14 (America/New_York). Task: W101-NAV-AUDIT-103.
Status: source audit plus bounded read-only installed-file inspection; proposed architecture, not completed autonomous navigation.

## Main finding

The current live CLI workflow is supervised route teaching: the coordinator selects headings and segments; Maelstrom executes bounded actions, measures outcomes, handles some focus/loading transitions, and restores Idle. It is not yet destination-driven navigation. The last operation419068a005b749d6b3d06ca013eff603 completed with native Idle verified near the Dance tent. Live work stopped for this audit. The restarted client successfully entered Pet Pavilion in ab32e93449e34a7b91cf22fdd6663118. No full Dance visit or energy-cost qualification was achieved.

The next implementation should connect the existing foundation into one destination resolver, planner and feedback follower. Do not add another independent controller or relabel timed movement as pathfinding.

## 1. Material actually available

| Material | Current evidence | Appropriate use and missing qualification |
|---|---|---|
| Current XYZ/heading | Native bounded pose samples; repeated live turns/translation | Localization candidates; current decoder demands a stationary double-read and is not a moving telemetry stream |
| Current zone | Imported wizwalker `Client.zone_name` and `ClientZone` fields | High priority qualification; shared `area` fact is currently unavailable; quest.area is NOT current location |
| Loading flag | Existing optional adapter, successful live crossings | Interrupt/release and await transition; loading clearing does not establish which zone arrived |
| Exact interaction title | Shared UI-tree observer and walk-until-title module | Endpoint confirmation for named sigils/NPCs; a nearby label alone is not successful arrival/interaction |
| Current objective/dialogue | Existing qualified and candidate registry paths | Resolve semantic destination/task; quest arrow is advisory, not obstacle-free immediate bearing |
| Named loaded entities | Imported get_base_entity_list, template-name and display-name searches | Candidate local landmark/NPC/mob catalogue; current layouts, types, positions, identities and freshness need separate qualification |
| Speed multiplier | Imported client-object field | Candidate mobility hint; corroborate measured speed, mount state and actual progress |
| Zone topology | Native ZoneTopologyService from Automatus, graph search | Reusable coarse graph, not current-client verified portal connections or executable routes |
| Local archives | Native WadArchiveReader, imported wizwalker/libkiwad/wizwad lineage | Read-only metadata/map extraction; harden bounds/decompression before production ingestion |
| Navigation graph | Deimos/navwiz parse_nav_data for zone.nav | Vertex/edge graph candidate, not automatically a polygon navmesh or proof of collision-free player movement |
| Static collision | Deimos collision.py and collision_math.py; collision.bcd | Geometry/clearance candidates; transforms, flags, terrain, elevation and actor footprint must be interpreted |
| Recorded routes | Human footage and native operation receipts/poses/screens | Seed measured corridors and portal evidence; video alone does not supply exact world XYZ |
| In-game map | Owner reports useful green player arrow, labels, zoom/scope | Important complementary localization/name source; projection and overlay geometry need measured calibration per map |
| Rust | Staged waypoint parity and archive/bridge components | Reusable pure modules; no connected autonomous navigator emerges from language conversion alone |

### Installed archive facts, newly checked read-only

Source directory: C:/ProgramData/KingsIsle Entertainment/Wizard101/Data/GameData.

| Archive | Total bytes | zone.nav decoded bytes | Parsed vertices / edge records | collision.bcd decoded bytes |
|---|---:|---:|---:|---:|
| WizardCity-WC_Hub.wad | 6,328,380 | 8,658 | 290 / 1,147 | 218,391 |
| WizardCity-WC_Streets-Interiors-WC_PET_Park.wad | 4,024,879 | 3,408 | 101 / 496 | 32,210 |

Archive journal bounds and navigation edge indices were in range; all nav bytes were consumed under the imported format interpretation. Header fields differ by one (289/290 and100/101); preserve their actual semantics rather than treating the first field as a proven vertex count. ClassicMode archives also exist: map variant is part of identity. This was a structural audit, not full collision decoding, graph connectivity analysis or live path qualification. Installation files were never modified. Evidence: `.local/navigation-audit-103/archive-inventory.json` and `nav-structure-summary.json`.

## 2. Candidate navigation methods

| Method | Inputs needed | Value | Limitation / disposition |
|---|---|---|---|
| Native zone.nav graph search | Validated vertices, edges, current zone/pose, destination anchor | Best immediate local path candidate; small offline A*/Dijkstra problem | Edge direction, clearance, player accessibility and connector projection need validation |
| Collision-derived walkable map | collision.bcd surfaces, transforms, collision semantics, footprint | Clearance, bridge rails, keep-out zones; possibly derive a mesh/grid | Collision geometry is not automatically a walkable mesh; stairs/levels matter |
| Recorded breadcrumb corridors | Timestamped pose/zone plus outcomes and optional screenshots | Fast way to qualify known routes and personalize houses | Sparse samples cannot prove the area between them is clear; broaden only with evidence |
| In-game map calibration | Map image/layout, player arrow, labels, zoom/pan/scope, paired XYZ | Named destinations, localization cross-check, coarse routing and visual-only fallback | Drawn maps may distort scale, omit obstacles, stack levels or use separate panels |
| Named entity targeting | Current loaded entity names/IDs/positions and validated types | Find local NPCs, sigils, reagents and mobs by name | Only loaded objects; duplicate/localized names and hostile entities require disambiguation |
| Quest-guided destinations | Objective text/IDs, quest target candidate, zone and catalogue | “Go to active objective” semantic input | Collect/defeat tasks have regions or multiple entities; arrow distance is not path distance |
| Portal/world graph | Directed zone edges, entrance regions, exit anchors, requirements | Cross-zone route planning; normal Home/Commons/world-gate actions | Housing, quest gates, costs, cooldowns and one-way edges cannot be assumed symmetric |
| Image landmark matching | Recorded references, current frames, scale/camera evidence | Recovery and fallback; independent arrival checks | ORB/SIFT match is image alignment, not world heading or navigable clearance |
| Visual road following | Ground/road segmentation plus heading/camera model | Potential low-cost fallback on obvious roads | No current qualified road follower; screen appearance alone does not reveal collision geometry |
| Dead reckoning / timed macros | Calibrated speed/turn rate and elapsed input | Short gap bridging and fallback estimates | Drift, mounts, blocked motion and latency invalidate open-loop arrival claims |
| Bounded exploration / SLAM-like mapping | Trusted poses, observed transitions, traversal outcomes | Discover missing portal connections and house routes | Repeated visits do not establish all unseen terrain; must bound time/resources and retain unknowns |
| Imported goto/navmap teleport | Archived primitives and hook layouts | Math/parser references; separate explicitly configured transport research | goto is timed straight motion, navmap_tp is teleport fallback; neither substitutes for walking navigation |

Recommended combination: zone graph + validated local nav graph/corridors + continuous pose-based follower + UI/map/entity evidence for semantic endpoints. Vision remains an independent fallback/cross-check. No language model is required in the movement loop.

## 3. Why a complete map need not be hand-entered

Use three connected layers:

1. **World/zone graph:** Commons connects to Pavilion through a portal. Edges have conditions, direction, allowed method, expected destination and outcome evidence. Imported topology starts as candidate data.
2. **Local traversability:** zone.nav or measured corridor graph provides route geometry within that zone. Collision evidence constrains clearance; a graph edge is not accepted merely because its endpoints exist.
3. **Named places and entities:** aliases such as “Pet Dance”, “Dance Game”, and “Pet Pavilion / Dance” resolve to a stable place ID. The entry contains zone, anchor/interaction region, expected title, allowed arrival behavior and evidence/version.

Unknown destinations remain unresolved or exploration candidates. Do not manufacture coordinates. Use contextual disambiguation for repeated NPC/place names, classic/current map variants and multiple interiors. House profiles require wizard plus house/instance identity; decorations can invalidate old clearance.

A destination does not always mean a single point: use an interaction region, portal crossing corridor, vendor approach area, or quest search region. This avoids an unnecessarily precise coordinate for every possible interaction.

## 4. Learn W101's map system explicitly

This should be a first-class calibration/research lane, not an afterthought.

- Inventory actual map screens and UI-tree controls, labels, player arrow, quest markers, zoom/pan and parent/child maps. Treat opening/closing controls as observed actions; do not assume map labels teleport.
- Inspect local archive members for map textures/layout/name metadata and correlate them to actual displayed maps. Asset presence alone is not proof of current use.
- Collect paired world poses and player-arrow map coordinates at several separated known locations. Fit an initial transform, then test held-out locations; use per-region transforms if a single affine map fails.
- Track map ID, bounds, scale, pan, UI size, camera/arrow orientation and timestamp. Determine experimentally whether the arrow rotation follows avatar yaw, camera yaw or another convention.
- Match labelled entrances to candidate nav graph locations and observed portal transitions. Map labels help name places; actual zone/pose/loading/title verifies travel.
- Do not infer precise bridge width, obstacle clearance, floor connectivity or interaction radius from a decorative map.
- Keep optional visual-only operation: map-arrow localization can support coarse travel where memory telemetry is unavailable, with lower confidence and separately tested limits.

## 5. Existing implementation audit

- `Utilities/Modules/NavigationModule.cs`: explicit landmark/progress/correction state graph; consumes supplied scenes/actions. Not coordinate path search.
- `Utilities/HeadingFeedback.cs`: useful receipt-bound rate estimation; up to four turns, tolerance0.06 radians. At2500 units that angle permits roughly150 units lateral deviation. Narrow bridge clearance needs cross-track control, not one global tolerance.
- `Utilities/WaypointFeedback.cs`: local20–100ms forward corrections, at most3; no turn composition; rejects >10Z change. Useful calibration, not long-route controller.
- `Utilities/W101FeedbackVisit.cs`: destination is generated immediately ahead, not resolved from a supplied named goal.
- `Utilities/W101NavigationVisit.cs`: separate teaching modes and repeated wake/observe/act/Idle wrapper. Useful bench, unsuitable as the final per-segment execution architecture.
- `tools/w101_pose_reader.py:61–72`: exact repeated24-byte equality rejects legitimate moving samples. New moving-sample contract must retain stable actor identity/coherence; merely removing this check is not a fix.
- `W101UiTreeOperation.ReadOwnedAsync`, `WizwalkerWorker`, `tools/w101_ui_tree.py:669–694`: fresh worker/two-tree reads and repeated hook lifecycle, including250ms settle. Main latency is orchestration, not trigonometry or C# language speed.
- `JobDeckRoutines.cs:37–47`: generic navigation remains offline rehearsal. `W101ModuleBindings` live bindings are sigil/menu/Idle, not destination navigation.
- `ZoneTopologyService`: reusable BFS over authored zone connections; no portal coordinates/requirements/current-zone observer. `SessionSupervisor` returns decisions, not an implemented multi-zone executor.
- `tools/zone_routes.py`: explicitly quarantined example coordinates; executable export rejects them. Do not promote these sketches.
- `tools/route_landmarks.py`: image-space ORB/SIFT candidates, not world bearing or walkability.

### Disconnected prototypes and misleading claims

`tools/steered_walker.py` and `tools/path_navigator.py` exist, but are not safe drop-in live adapters:

- Positive yaw error sends A, while measured native convention requires D. Root dry-run at(0,0),yaw0,target(-100,0) returned TURN_LEFT with+90degree error. Existing6steering+5path tests still pass; tests do not establish real steering direction.
- Dispatcher calls PostMessage, ignores outcome/exceptions, updates held-key bookkeeping before confirmed delivery, and lacks shared lease, process-start, focus, freshness and cleanup contracts.
- Pose has no timestamp/zone/target identity.
- Advertised “pure pursuit” is discrete turn-then-forward waypoint control, without projected path position/lookahead/cross-track regulation.
- Blind corner offsets have no collision proof. Enemy yield has no verified safe sidewalk and uses a generic threshold rather than a calibrated per-type risk model.
- Progress timeout can classify legitimate turning as obstruction. Recovery backs/turns/jumps and decrements an index; it does not plan an alternate collision-free path.

Keep these as offline candidates, extract corrected pure decisions and test cases, and use the existing native owner/actuator. Historical W101-PATH-NAVIGATOR-01 claims of solving these problems exceed actual evidence.

## 6. Legacy reuse decisions

- Deimos `teleport_math.py:69`: navwiz-derived graph parser, embedded Boost notice. Harden lengths/counts/indices/floats and use integer adjacency; get_neighbors has a not-found-index defect.
- Deimos `collision.py:321–409`: object/mesh reader and collision.bcd acquisition. Harden before production.
- wizwalker `client.py:133,149–211`: zone and loaded named entity candidates. Qualify current version before registry publication.
- wizwalker `client.py:418–435`: goto computes duration, writes yaw and holdsW; no obstacle-aware feedback.
- wizSDK `client.py:627–642`: straight-line walk wrapper, explicitly no obstacle handling.
- Deimos `navmap_tp`: teleport-first; its “moved away from start” criterion is not destination arrival.
- Existing C# WadArchiveReader opens read-only but also needs bounds/decompression hardening. Prefer extending reviewed existing infrastructure over a new dependency.
- Imported Deimos/wizwalker/wizSDK have GPLv3 notices; parser has its own embedded Boost notice. Preserve provenance and applicable license notices; ownership of the host repository does not relicense upstream materials.

## 7. Proposed connected navigation service

One shared capability: `Navigate(destinationIdOrName, constraints)` through CLI/API/MCP/cards.

Pipeline: resolve destination -> qualify current zone/pose -> choose allowed portal/local path -> follow corridor with feedback -> handle transition -> verify named arrival -> top-level Idle.

Proposed contracts (not existing public APIs):
- NavigationSnapshot: target lifetime, zone/instance/map variant, monotonic sample time, XYZ/yaw, loading, motion lock, confidence/source.
- Destination: stableID, aliases, zone, anchor/region, interaction signature, access requirements, provenance.
- Route: version/hash, directed edges, corridors/clearance, transitions, estimated cost, blocked/unknown status.
- MotionIntent: desired turn/forward state, short expiry, expected progress, original owner; no independent input dispatch.
- NavigationOutcome: actual arrival evidence, interruptions/recoveries, resources, route/version, final Idle or explicit reason unavailable.

Use one bounded observation/control session for the whole route. Proposed starting rates for measurement—not promises—are pose/control10–20Hz, loading5–10Hz and semantic UI1–2Hz/on events. Tune from measured latency; the existing guard's10ms polling is not proof of100Hz telemetry. Separate input-release watchdog from model, UI and telemetry backlog. Preserve the existing serialized hook/scratch restriction; simultaneous hooks require independent reviewed storage or serialized lanes, never accidental overlap.

Follower computes along-path progress, cross-track error, lookahead target, heading error and measured speed. Slows near corners/portals, may turn in place for large errors, and can combine forward+turn only through a qualified shared actuator. No Idle/menu between internal waypoints; default Idle is the finalizer for the route, failure recovery and explicit between-test boundary.

Recovery first distinguishes focus pause, loading, stale localization, blocked progress, wrong-zone transition and user stop. Release inputs, reobserve, then bounded re-center/replan inside known clearance. Do not blindly jump, reverse into unknown space or reset to Commons on every transient. Portal success requires destination identity, not just disappearance of a loading screen.

## 8. Practical delivery order and acceptance

1. Qualify current zone/instance and moving pose observation; latency/freshness/cleanup tests, restart invalidation, loading invalidation.
2. Harden offline archive/nav parser; export a versioned candidate graph and debug map for Commons/Pavilion; map-overlay calibration alongside it.
3. Pure route planner and destination registry; reject disconnected/unknown/locked edges; connect entry/exit regions with evidence.
4. Integrate one corridor follower into the native scheduler. Reuse owner, countdown, overlay, focus pause and Idle. Keep independent inputs disabled in prototypes.
5. Test recorded trace/simulated plant cases with noise, delay, speed changes, bridges, blocked paths and wrong-zone events. Replay checks decisions; it cannot prove a different unexecuted action would succeed.
6. Qualify one named route, “Pet Dance”, from varied Commons starts/headings. Suggested first acceptance target:10fresh complete runs, all key releases confirmed, no assistant-chosen intermediate headings, correct portal/interaction evidence and finalIdle. This is a proposed gate, not achieved reliability or statistical proof.
7. Add cross-zone alternatives and discovery: observations populate candidate edges; repeated validated outcomes promote exact versions. User card exposes destination, allowed transport, avoid-combat, recovery/time budgets and arrival behavior.

No broad neural training or full-world manual survey is prerequisite for the first route. Learn speed, turn response, edge reliability and landmark associations from evidence. Keep facts, hypotheses and qualified edges separate. A world graph can be incomplete and still route within its known connected component.

## 9. External reference architectures

- Nav2 regulated pure pursuit supplies a reference for lookahead path tracking and curvature/collision-based regulation. Borrow algorithm/architecture ideas; do not bundle ROS2 merely to walk W101. https://docs.nav2.org/jazzy/configuration_and_development/configuration_guide/controller_plugins/configuring_regulated_pp/
- Recast/Detour separates geometry-to-navmesh generation, path queries and crowd handling. Candidate if validated W101 geometry needs a polygon mesh; zone.nav is not known to be Detour-compatible. No dependency adopted. https://recastnav.com/

## Verification scope

Read-only source audit by root plus two subagents; root ran6existing steered-walker tests and5path-navigator tests (all pass), plus dry-run directional counterexample. Installed two-archive structural inventory performed without game writes. No new production code, dependency, live hook or input was added for the audit. Previous2837native-suite result predates this documentation-only audit. Board validation required after updates. Full gameplay/navigation remains integration_pending.
