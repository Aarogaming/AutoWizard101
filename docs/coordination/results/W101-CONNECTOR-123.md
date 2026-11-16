# Dynamic connector inspection — 123

Offline only, 2026-11-15. `tools/w101_connector.py` adds bounded reverse
Dijkstra distances and pose-dependent connector ranking to
`tools/w101_zone_route.py:plan(connector_request=...)`. Macro routes stay
runtime-disabled. This is not yet wired to the native live follower.

Inputs include explicit settled/fresh flags, canonical heading, reach/elevation/
grade limits, clearance margin and transformed obstacle boxes. Broad-phase
segment/expanded-box intersection conservatively rejects hits. Missing geometry
coverage cannot qualify a connector. Caller-provided qualified vertices must be
scoped to this exact pose, profile, assets and epoch; the current offline API
does not independently validate that scope. Native integration must replace this
assertion with the existing atlas evidence checks and qualified downstream edges.

Eight connector tests and six route tests pass. Black formatting and compileall
pass. No game input or process observation performed.

Installed Commons zone.nav was decoded read-only using the existing bounded
member loader. Both saved poses were compared toward vertex209. Illustrative
inspection settings: 600 maximum XY,40 elevation,.25 grade,0 turn penalty;
these are not a qualified movement profile. No obstacle coverage or fresh live
pose was asserted: saved poses were treated as synthetic settled inputs only.

| Saved pose | Best total-distance candidate | XY gap | Nearest alternative |
|---|---|---|---|
| (-371.1885,-518.4771,-9.92746) | 96 |346.56|89,219.78|
| (-482.5171,397.3479,-27.5987) |98|437.88|91,114.27|

Both return `arrived-in-zone-connector-unqualified`. Joint cost can prefer a
farther connector; it must not outrank a missing clearance check. Existing BCD
mesh inspection does not yet provide complete primitive obstacles, swept player
footprint or floor coverage. Actual BCD obstacle binding, native selector
integration and live route qualification remain pending. Spawn bounds unknown.
