# Commons connector clearance — 137

2026-11-15. Offline saved-evidence assessment; no live input, focus, capture, hook or installation. Last verified game state remains Commons native Idle from136; no claim of a new live state reading.

## Evidence and reuse

Used Commons arrival from136: (707.1978149414062,-231.12669372558594,-27.506072998046875), nearest vertex113 (889.259765625,-79.08629608154297,-30.479736328125), gap237.1979XY. Saved BCD member SHA d674effe1806002da2cdcbdd2af2d1a00824142c903e5122c1bdb07ad29d7231. Reused saved world triangles, primitive classifier, segment intersection helper and production NavigationFloorSheet.Trace; no new planner or game controller.

17 centerline diagnostic samples all intersected street mesh proxy437 Imported collision mesh_HD_WC_Street_A05B_SH.tga. Maximum nearest-height difference from straight endpoint interpolation2.48744units. Sampling alone is not continuous footprint proof.

Production native floor trace over that mesh's26 triangles accepted experimental radii1,10,25,50, maxGrade0.6/maxCrease0.35/heightTolerance5, preserving5 path points. These are explicit experimental parameters, not calibrated avatar dimensions. The geometric sheet result does not qualify the mesh's collision role, headroom or obstacle clearance.

## Concrete repair

The old box broad-phase bound surrounded the entire box with a sphere, greatly widening thin/long boxes along every axis. Now extents use abs(scale)*sum(abs(rotationRow[j])*dimension[j]). This encloses every transformed corner while retaining full input dimensions as conservative half-extents; no unverified half-size convention was introduced. Cylinder/tube conservative bounds remain unchanged. Tests cover rotated-corner containment and a long thin box that must not acquire its length on all axes.

For vertex113 the centerline primitive overlap count fell7->2 (also2 at experimental margin25,3 at50). Remaining: Collision Box17(1), proxy88; Collision cylinder101, proxy122. Other nearest candidates106,112,105,107 still have unresolved primitive overlaps. None was selected for movement.

Proxy122 has source parameters[487.2143859863281,35.398101806640625] and center(917.8297119140625,-353.635009765625,113.39679718017578). Imported parameter names alone do not establish cylinder axis/full-length conventions or avatar headroom. Do not simply exempt it by name or because the current wizard occupies a nearby coordinate. Proxy88's oriented footprint also needs a narrow-phase assessment before its broad-phase hit becomes a real obstruction claim.

## Artifacts / checks

.local/introspection/connector_clearance_137.py
.local/introspection/connector-clearance-137.json
.local/introspection/connector-floor-137.json
.local/introspection/bcd-classification-137.json

11 BCD tests +8 connector tests passed; Black check and compileall passed on owned Python paths. Floor trace result is an offline execution against saved geometry, not live traversal. Central board validated separately.

## Next gate

Resolve the remaining oriented-box/cylinder shape and headroom checks; retain candidate role labels. Then refresh the live pose and wizard identity before any countdown-controlled segment trial. Pet Pavilion/Dance route remains unqualified. Gemini125 is assigned review of these exact remaining proxies through LIA-0090; no competing source/build/live work authorized.
