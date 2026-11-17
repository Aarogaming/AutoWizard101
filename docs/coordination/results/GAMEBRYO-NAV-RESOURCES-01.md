# GAMEBRYO-NAV-RESOURCES-01 — Gamebryo navigation reference study

Status: **study-only — four verified reference clones in git-ignored `.local/upstream`; no tracked source, build, install, live or dependency.**
Date: 2026-11-17 · owner: root · workstream: `CORE-REFERENCES`

## 1. Verified upstreams (git ls-remote)

`Deimos-Wizard101/bots` @ `c935100` (GPL-3), `niftools/niflib` @ `e291661`, `ikpil/DotRecast` @ `6fce4ac`, `recastnavigation/recastnavigation` @ `9f4ce64` — shallow-cloned to `.local/upstream/{bots,niflib,DotRecast,recastnavigation}`. `niftools/nifskope` @ `3a85ac5`, `OpenMW/openmw` @ `77eec04`, `bryantmh/tes4skyrim` @ `dd5216e` were URL-verified but **not cloned** (no need yet; avoid bulk). `kinif` was already cloned in batch 03.

## 2. Disposition of the owner's claims

| Claim | Finding |
|---|---|
| `wizsprinter` is a dedicated movement module needing extraction | **Partially true.** Vendored Deimos source imports `wizwalker.extensions.wizsprinter.sprinty_combat` and `SprintyClient` in `Deimos.py`; no separate repo/extraction was verified and no pathing logic is called by our native code. Teleport/timed `goto` remain excluded (`AUTOMATUS_ROUTE_IMPORT.md:39`) |
| `kinif` extracts raw NIF vertex geometry | ✅ Confirmed from its README: `NiTriShapeData`/`NiTriStripsData` and `NiMesh`/`NiDataStream` for collision detection. GPL-3, study-only; no code copied |
| Deimos `bots` registry holds practical zone scripts | URL verified and cloned (2,873 files, 1.7 M); content not yet reviewed — study lead, not imported |
| NifTools/NifSkope is the definitive Gamebryo reference | URL verified; format knowledge may inform our readers; no dependency |
| OpenMW `components/navigation` generates navmeshes in a background thread | Unverified content claim; repo URL verified only, no code reviewed |
| tes4skyrim solves cell seams/door reaches | Unverified content claim; URL verified only |
| Recast/Detour and DotRecast are relevant pathfinding engines | URL/licence verified; **no production dependency adopted** — `W101-NAV-AUDIT-103.md:157` says zone.nav is not known to be Detour-compatible and lists Recast as a candidate |
| "part of your 14-crate Rust workspace" | ❌ **Correction:** root `Cargo.toml` has **5** workspace members (`maelstrom-core`, `-platform`, `-fallback`, `-reflex`, `-ide`) |
| Recast path should replace live navigation reads | Premature: our gates require current identity/zone binding, connector clearance and verified arrival; an offline mesh cannot supply those by itself |

## 3. What our architecture already does

`ZoneTopologyService` holds a coarse/master zone graph (55-zone WizardCity port; 3,517 zones / 10,742 links decoded offline per `ROADMAP.md:124`). `NavigationAtlas.Plan/PlanDynamic/PlanFromObservations`, `PathFollower`, `WaypointFeedback` and `NavigationSettledPose` implement local planning and bounded `W` pulses with stall/heading/geometry guards. `NavigationConnectorGeometry`/`NavigationFloorSheet` consume BCD triangle evidence with conservative clearance. This is already a hybrid graph: global zone edges + local movement, with verification gates — the same family of architecture the owner describes.

## 4. Boundaries / next gates

No Recast/DotRecast/niflib code was copied into tracked paths; no build or live test. Any dependency adoption needs owner approval plus a source/build claim. The next qualification gates remain: production reader binding, connector role/coverage, verified arrival and Idle. The four clones stay on the study shelf; the `bots` repo content is the most direct follow-up if the owner wants a deeper read.
