# Reusing Automatus waypoint maps

Maelstrom already contains a native port of Automatus' zone-name adjacency
(`ZoneTopologyService`). This slice adds a bounded importer for Automatus v2's
text waypoint files. It imports route evidence without importing its independent
controller or `wizwalker.goto` timed movement.

## Source format and behavior

The legacy `Map` reader expects the first line to equal the selected location
name. Each later row is an integer `x,y,z` tuple. `follow_map` advances a stored
list and sends each ordinary point to `client.goto(x, y)`. It interprets
`(0,0,z)` entries as key actions; the source's forward/reverse comments disagree
about the key associated with the marker. Neither the timed `goto` calls nor
these marker actions are copied into Maelstrom's live input path.

## Maelstrom port

`AutomatusWaypointMapImporter.Parse` produces an immutable, hashed
`AutomatusWaypointMap` candidate with a strict name header, bounded UTF-8 size,
bounded row count, invariant integer parsing, and coordinate limits. Special
`(0,0,-1..1)` rows are retained as opaque marker codes. The importer never
converts them into keys or declares a route executable.

The candidate can later be reviewed against a matching zone and movement
profile, then translated into Maelstrom's `NavigationAtlas` / `PathFollower`
contracts. Promotion still requires fresh target and zone identity, dynamic
connector checks, qualified directed corridors, obstacle/floor clearance,
loading and arrival evidence, shared scheduler ownership, cancellation and safe
cleanup. Existing historical waypoint lists are not proof those conditions hold.

## Reuse map

| Legacy behavior | Maelstrom destination | Status |
| --- | --- | --- |
| Automatus world/zone names and adjacency | `ZoneTopologyService` | Native source port exists; route reachability only. |
| Automatus waypoint file parsing | `AutomatusWaypointMapImporter` | Imported as candidate evidence; offline-only data conversion. |
| Automatus point iterator and `follow_map` | `NavigationAtlas` / `PathFollower` | Do not copy as a second controller; physical route qualification remains open. |
| wizwalker `goto` and teleport helpers | No direct mapping | Timed point steering is not a qualified path follower; teleport remains excluded from native route execution. |
| Deimos quest/combat loops | Shared quest/combat operation contracts | Preserve decision vocabulary and source provenance; adapters remain independently gated. |

This is one reusable legacy port, not wholesale integration of the archive set.
Combat policy, visual matching, pet/minigame teachers, asset parsers and resource
workflows each need their own bounded source-to-contract ports and qualification.
