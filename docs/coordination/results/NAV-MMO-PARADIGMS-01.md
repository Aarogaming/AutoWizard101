# NAV-MMO-PARADIGMS-01 — WoW/FFXIV/OSRS navigation paradigm audit

Status: **study-only — 2 verified clones in git-ignored `.local/upstream`; no tracked source, build, install, live or dependency.**
Date: 2026-11-17 · owner: root · workstream: `CORE-REFERENCES`

## 1. Verified upstreams and clones

- `namreeb/namigator` @ `54eae69` (MIT, 46 M, 150 files) — cloned to `.local/upstream/namigator`. README line 35 confirms `recastnavigation` is its underlying computational-geometry library. The owner's "BVH + Recast + raycast Z" description is directionally plausible but only the Recast dependency was verified here; no code was run.
- `itsdax/Runescape-Web-Walker-Engine` @ `58bd4fa` (Apache, 1.5 M, 156 files) — cloned to `.local/upstream/web-walker`. Source confirms `Teleport` objects, teleport blacklists and move-cost weighting (`DaxWalker.java:186-226`). The "Dijkstra global + A* local" algorithm split was not independently verified from source in this pass; treat as context.
- URL-verified but **not cloned**: `TrinityCore/TrinityCore` @ `15f2f25`, `azerothcore/azerothcore-wotlk` @ `f22f096`, `goatcorp/DalamudPluginsD17` @ `ec59b5b`. FFXIV `vnavmesh` was not located as a distinct verified repo in this pass; its architecture claims remain unverified.

## 2. What we already have (correction to the proposal)

Our native stack **already implements the hybrid structure** the owner recommends:
- **Global zone graph**: `ZoneTopologyService.FindShortestZonePath()` — unit-weight **BFS** (`Queue<List<string>>`), equivalent to Dijkstra when all links cost 1; master topology 3,517 zones / 10,742 links decoded offline (`ROADMAP.md:124`). Coarse graph evidence, not current-client-verified portals.
- **Local graph**: `NavigationMap.FindPath()` — true **Dijkstra** with `PriorityQueue<int,(double,int)>`, bounded at 10,000 visits (`NavigationMap.cs:116-135`).
- **Dynamic connectors / arrival / movement**: `NavigationAtlas.Plan/PlanDynamic/PlanFromObservations`, `PathFollower`, `WaypointFeedback`, `NavigationSettledPose` — bounded `W` pulses with heading/stall/geometry guards.
- **Geometry evidence**: `NavigationConnectorGeometry`/`NavigationFloorSheet` consume BCD triangle data conservatively; role flags remain untrusted (`BCD-FLAG-EVIDENCE-AUDIT-01.md`).

So the recommendation "use Dijkstra for zone jumps and A*/Recast for local walking" maps onto an existing design — **the gap is qualification, not architecture**. `W101-NAV-AUDIT-103.md:157` says zone.nav is not known to be Detour-compatible; no Recast/DotRecast dependency was adopted.

## 3. Directly useful ideas vs. unverified claims

| Idea | Disposition |
|---|---|
| Offline mesh with exact Z raycast to avoid "falling" server checks | **Useful concept** — our `NavigationSettledPose`/`NavigationConnectorGeometry` already gate on exact geometry; no Recast needed yet |
| vmap/mmap extractor pipeline (archive → collision triangles → movement map) | URL verified; **content not reviewed**. Concept aligns with our WAD/BCD offline path; no clone |
| FFXIV in-RAM dynamic navmesh built from live memory | Unverified architecture claim; our background facts service is the analogous track, still awaiting reader binding |
| OSRS wayport hybrid (teleports as jumps, local A*) | Concept matches our zone-graph + local path split; verified source shows teleport weighting, not full algorithm proof |

## 4. Boundaries / next gates

Nothing was imported into tracked source; no dependency added; no build, live or desktop work. Any Recast/DotRecast adoption needs owner approval plus a source/build claim. Current acceptance gates remain: production reader/pose binding, connector roles/coverage, verified arrival and Idle cleanup. The two clones stay on the study shelf for future comparison.
