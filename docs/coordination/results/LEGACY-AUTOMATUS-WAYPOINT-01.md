# LEGACY-AUTOMATUS-WAYPOINT-01 — route candidate importer

## Scope

Port Automatus v2's text waypoint-map data format into a bounded Maelstrom
candidate import. Do not port its independent controller, timed `goto` execution,
or marker-to-key behavior.

## Findings

- Automatus maps begin with a location-name header and then integer XYZ rows.
- `follow_map` iterates those rows and invokes `wizwalker.goto` for ordinary
  points. Special `(0,0,z)` rows are converted to key presses, but the source
  comments disagree on direction/key semantics.
- Maelstrom already has `ZoneTopologyService` for the Automatus zone adjacency
  source and the separate `NavigationAtlas` / `PathFollower` architecture.

## Changes

- Added `AutomatusWaypointMapImporter`, which applies byte, row and coordinate
  limits; validates the selected map-name header; uses invariant integer parsing;
  hashes source text; and preserves special markers as opaque values.
- Added `docs/AUTOMATUS_ROUTE_IMPORT.md` describing how this legacy source maps
  into the shared navigation model and what remains unqualified.
- The imported map is explicitly `candidate-only` and contains no dispatchable
  actions. No game process, input, installed files, or network were accessed.

## Verification status

Source was reviewed against the local Automatus `map.py` and `locomotion.py`.
Focused importer tests cover valid point/marker preservation, header mismatch,
malformed and out-of-range rows, row-count bounds and minimum route length. The
filtered `dotnet test` command compiled the app and test assemblies, then stalled
after test discovery without reporting cases; it was cancelled after roughly 40
seconds. This is neither a pass nor a failing assertion. No packaging,
installation or live game check was performed.

## Next port candidates

1. Deimos/SprintyCombat policy grammar → a pure, versioned combat policy over
   Maelstrom's normalized observed hand/target facts.
2. wizAPI image matching → shared game-window perception primitives and
   resolution-independent template fixtures.
3. PetDance/DanceBot and Conjuration → teacher evidence and pack data, not a
   parallel controller.
4. wizwalker/WAD, wiztype/wizdiff, and wizwad/libkiwad → bounded asset/version
   adapters behind the existing data registry.
