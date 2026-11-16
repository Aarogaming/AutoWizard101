# Oriented shape and headroom diagnostics — 138

2026-11-15. Offline only. No live route/input/countdown or fresh game-state check. Last verified state: Commons native Idle from136.

## Reusable implementation

Added tools/w101_shape_clearance.py: conservative swept vertical body envelope against an explicitly supplied affine local box. Inverts the full scaled matrix rather than assuming perfect orthonormal rotation. Expands local extents by the transformed world-aligned body envelope and checks the center segment. Rejects singular/nonfinite inputs. A miss is conditional on caller-provided shape/body interpretation; it cannot certify game collision roles. Cylinder use is an enclosing box under an explicitly chosen axis, not an exact cylinder solver.

7 pure tests cover crossing, outside paths, rotated thin-box AABB false positives, body-radius expansion, headroom changes, negative scale and rejected invalid inputs.11 existing BCD tests also pass. Black and compileall passed; no dependencies added.

## Actual Commons connector sensitivity

Saved136 start to113, with end height adjusted to saved137 street surface rather than nav vertex Z. Radius25 is an experimental input, not avatar calibration.

Proxy88 Collision Box17(1): all tested heights0/100/150 overlap when full stored dimensions are used as half-extents. Halving all dimensions produces disjoint results at those heights. Thus the remaining issue is materially dimension-convention dependent, not merely world-AABB corner overlap.

Imported Deimos collision.py120-136 labels the fields length/width/depth. Its collision_math.py25ff commented cube helper halves dimensions but also swaps width/length axes and transforms vertices using its own conventions. This is a source-derived interpretation, not proof for the current engine. Do not adopt only its halving while ignoring axis/transform differences.

Proxy122 Collision cylinder101: under local-Z axis, first parameter radius and full second parameter as half-height, enclosing-box checks clear heights0/50/100 but overlap150/200 at radius25. Approximate lower face77.999 versus floor~-27.5 gives only~105.5 vertical units under that convention. Actual character height, cylinder axis/length convention and collision role remain unqualified. Do not use nominal visual size or standing nearby as a global clearance certificate.

Detailed conditional matrix: .local/introspection/shape-clearance-138.json. No connectorVerified flag was promoted. No Commons->Pavilion live route attempted because clearance prerequisites remain unresolved.

## Next concrete work

Cross-check full-size/axis conventions against an independent local format definition or current-client shape construction, then establish a movement-profile body envelope. Alternatively propose an explicitly bounded empirical connector experiment with clear outcome checks; do not mislabel it pre-qualified clearance. Maintain continuous identity/focus/loading guards and Idle cleanup. Gemini125 received exact source questions through LIA-0092. Full persistent route/epoch integration remains separate unfinished work.
