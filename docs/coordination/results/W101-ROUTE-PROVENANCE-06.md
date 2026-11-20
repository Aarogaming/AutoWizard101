# Route provenance repair — W101-ROUTE-PROVENANCE-06

2026-11-11. Read-only review followed by a bounded offline repair. This is a
prerequisite for a real route controller, not a completed navigation route.

## Finding and repair

`tools/zone_routes.py` called five route sketches "verified" and exported their
unmeasured coordinates as ordinary `navigate_path` macro nodes. The tests checked
schema and serialization, not actual game travel or arrival. No measured source
for those coordinates was found. `NavigationKnowledge.cs` also contains stale
geographical descriptions; it was reviewed but is outside this edit's scope.

The route library now labels its coordinates as unqualified examples. Both
`to_macro_nodes()` and `to_macro_graph()` fail closed. The old default export
fails before creating its output directory. Explicit `preview_only=True` exports
separate `maelstrom.route-preview.v1` files with `executable: false`, no nodes,
no verified coordinate source, and the original values under `example_waypoints`.
The CLI requires explicit `--preview-only --output`; it no longer defaults to
writing executable-looking macros into application resources. Preview files are
created exclusively and never silently overwrite prior evidence or user edits.

No other source caller of this route library was found beyond its tests and CLI.
`SessionSupervisor.cs` does name two route IDs in pure proposed decisions; those
IDs must not be interpreted as installed, qualified or executable capabilities.
The coordinator expanded the claim to the five previously generated resource
files under `ProjectMaelstrom/ProjectMaelstrom/Resources/Macros`. All five now
contain non-executable previews with no nodes. Their legacy filenames remain
unchanged to avoid deletion or movement. Original XYZ values, draft tolerances
and timeouts are retained as unqualified reference data, alongside a SHA-256 of
each replaced macro. No coordinate provenance was invented. Existing parser
`macro_from_dict` ignores schema, so removing executable nodes is essential;
merely changing a label would not be sufficient.

## Actual route evidence

The real human demonstration is documented in `docs/W101_DEMONSTRATION_20261108.md`.
Its local recording is
`.local/w101-route-study/human-route-01/video/capture.mp4`, SHA-256
`07B52BE94A5C1471E817F84B09C37AFF2C15958460950404119880FC265321EF`.
The approximate 5–45 second route window shows the Commons anchor, bridge,
signed Pet Pavilion tunnel, loading transition, Pavilion and Dance sigil.
These are visual landmarks, not measured XYZ positions. The overview samples
are coarse; exact segment boundaries and input/video alignment remain unqualified.

`input-review.json` preserves the demonstrated W/A/D holds and releases. Use them
as candidate short motions between independently recognized landmarks, not a
blind replay. The demonstration contains no End key, so a fresh Commons-return
anchor needs its own verification. The Shopping District tunnel visible near
the original anchor must not be mistaken for the Pavilion entrance.

The current reviewed UI reader does not expose pose/yaw/zone telemetry. Archived
wizwalker actor/client hooks and their field offsets are source references only,
not current-build qualification. Do not activate all legacy hooks to fill this
gap. A real route must obtain current location/heading or qualified visual
landmarks, check progress after bounded input, pause for loading, and verify
arrival independently before reporting success.

## Checks

- Five focused `test_zone_routes.py` tests pass: both runnable conversion paths
  rejected for all five sketches; default export has no filesystem side effect;
  previews yield no nodes to the existing macro parser/executor; no overwrite;
  all five packaged legacy artifacts retain coordinates but supply no nodes.
- Black checks pass for the two changed Python files; compileall passes.
- No game/client reads, desktop control, models, build or installation performed.

The coordinator owns full integration checks and the central board update.
