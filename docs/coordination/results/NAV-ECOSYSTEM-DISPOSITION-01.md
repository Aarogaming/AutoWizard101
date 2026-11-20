# NAV-ECOSYSTEM-DISPOSITION-01 — navmesh/pathfinding ecosystem vs. our stack

Status: **research disposition — no import, code, build, live or network work.**
Date: 2026-11-17 · owner: root · workstream: `CORE-REFERENCES`

## 1. Source discipline

The owner's message describes the post-patch ecosystem and a proposed five-step pipeline. Those external claims (upstream capability, anti-cheat behaviour, ban history) are **unverified context**, not primary-source facts from this repo. Dispositions below are based only on our tracked tree and prior audits.

## 2. The proposed pipeline vs. what we already have

| Proposed step | Our reality | Disposition |
|---|---|---|
| Extract geometry: `wizwad` + WizWalker `nif.py` | `wizwad-main.zip`, native `WadArchiveReader`, vendored `wizwalker/file_readers/{nif,wad}.py`; `W101-NAV-AUDIT-103:35-40` validates local archive parsing | **Already present as offline reference**; production ingestion still needs bounds/decompression hardening |
| Bake navmesh via Recast/Detour or `three-pathfinding` | No Recast/Detour dependency adopted (`W101-NAV-AUDIT-103:157` explicitly lists it as a candidate); our native `NavigationAtlas` uses exact-pose dynamic connector selection, BCD triangle support, connected floor sheets and conservative proxy bounds | **Not needed yet / not adopted.** Our evidence says `zone.nav` is not known to be Detour-compatible; adding a C++/Python navmesh dependency without qualification would be premature |
| Read quest arrow XYZ + current XYZ/yaw | `W101PoseCandidate`/pose reader bound by owner identity; quest arrow is explicitly advisory (`JOB_DECK.md:105`, `W101-NAV-AUDIT-103:20`) | **Partially present** — current moving-pose binding and zone/epoch qualification remain open gates; arrow is a hint, not authoritative geometry |
| A* over waypoints + yaw/`W` walking | `ZoneTopologyService` master graph (3,517 zones / 10,742 links per `ROADMAP.md:124`), `NavigationAtlas.Plan/PlanDynamic/PlanFromObservations`, `PathFollower` with lookahead/stall/heading guards, `WaypointFeedback`, bounded `W` pulses | **Already implemented offline**, with live pulses demonstrated but no end-to-end qualified route |
| Deimos Auto-Dialogue/Sigil/Combat when arriving | Native module cards, `W101ModuleBindings` (navigation/sigil/menu/Idle) and teacher replay paths exist; Deimos remains a reference, not an imported controller | **Covered by native equivalents**; no second controller imported |

## 3. External names not in our tree

- **Revive101 `spiraldb`**: not tracked. `Aurorium-main.zip` (same org) is a reference for manifest/version provenance only. If spiraldb gives structured zone/quest data, it is a **candidate offline import** requiring its own provenance/licence claim; it cannot be treated as qualified routing evidence.
- **Deimos Sprinty / Flythrough Recorder**: `SprintyClient` and `camera_utils` are present in the vendored Deimos reference; the timed `wizwalker.goto` controller is deliberately **not** imported (`AUTOMATUS_ROUTE_IMPORT.md:6,39`). Reading legacy steering ideas is fine; importing its controller is not.
- **`wiz-packet-map`**: already tracked as `wiz-packet-map-main.zip` but **excluded** — live packet hooking/decryption is outside the owner-approved boundary.
- **`w101-proto-go`, `On-Wiz`**: present as offline schema/documentation references only; no protocol execution.
- **Recast/Detour, three-pathfinding**: not imported. No new production dependency without owner approval; our native geometry path is the current experiment track.

## 4. Doctrine

Our route work is **normal-speed, single-owner, evidence-gated** — exactly the surviving category the owner described — but stricter on provenance: a zone graph, a parsed triangle, a successful key pulse and an arrived screenshot are four different claims (see `W101-NAV-AUDIT-103.md`). Acceptance still requires current identity/zone binding, connector clearance, arrival verification and recovery-safe cleanup. No new repository is needed to continue the current plan.

## 5. Next gate

Finish production binding of the native reader/pose stream, qualify connector roles/collision semantics, and test one bounded Commons→Pavilion route. Any Recast/spiraldb import is a separate, owner-approved task. This report changes nothing outside its own claim.
