# Connected-floor tracing — 129

2026-11-15. Offline only.35 focused native tests pass; scoped whitespace,
analyzers and warnings-as-errors build pass. No game observation/input occurred.

`NavigationFloorSheet.Trace` builds exact shared-edge adjacency for a bounded
reviewed support set (maximum2048 triangles). It rejects nonmanifold edges and
same-side folded adjacency, limits slope and crease, anchors the component to
the supplied start height, checks global XY clearance from component boundaries,
and traces continuous XY intervals into piecewise floor heights. Native
`NavigationAtlas.PlanDynamic` can preserve these heights in its connector path,
then uses existing qualified downstream routes. Obstacle checks run on every
piecewise segment. Available path clearance must cover the explicit footprint.

New synthetic tests cover flat-to-ramp creases, missing triangles, excessive
crease, nonmanifold duplication, wrong endpoint elevation and Argon's shallow
neighbor. The latter is rejected by both the existing convex-pair method and
the new sheet tracer. Argon's premise about ignoring a partner's outer edges
does not describe the current implementation.

The tracer is conservative: exact vertices only, no welding, all sheet boundary
edges considered in XY, no implicit supporting-role inference, and no attempt
to repair arbitrary overlapping meshes. Height tolerances and dimensions are
fixture inputs, not measured W101 values. Flat/ramp validation here is synthetic;
the actual Olde Town BCD export remains candidate evidence with unverified roles.

Remaining before live attachment/execution: versioned collision-role receipts,
qualified world-transform/body profile, checked geometry loading, and trusted
producer-owned target/stream/zone epoch binding. The pure settled-position entry
point is not a live stream connection. Olde Town first; Grizzleheim second.
Gemini125 remains report-only and received LIA-0078 to review the current changes.
