# Collision parameter-order correction — 139

2026-11-15. Offline research and tested converter correction. No game input, focus, live calibration, route test or countdown. Last verified state remains Commons native Idle from136; no new state verification claimed.

## Independent source finding

Reviewed https://raw.githubusercontent.com/vbe0201/katsuba/main/src/katsuba-bcd/src/lib.rs on2026-11-15. GeomParams parsing114-120 reads length before radius; docs85-90 distinguish local-Z capsule (Cylinder) from cylinder (Tube). The imported Deimos fields assign the opposite meanings. For proxy122, raw487.214/35.398 therefore means long/narrow under Katsuba's interpretation. This supersedes138's broad/shallow headroom inference, which must not be used for calibration.

## Implementation and tests

Conservative transformed capsule bounds use a local axial segment plus spherical radius. Retain the full length as half-length until length convention is separately qualified; this over-encloses rather than silently halving it. Tube currently uses the same conservative capsule enclosure. Rotation and scale included; no role/coverage promotion. Box handling unchanged.

12 BCD tests pass, including distinguishing axial extent from radial extent and rotating the long axis. Regenerated .local/introspection/bcd-classification-139.json from saved raw evidence. No production dependency added.

Commons arrival->113 diagnostics at experimental radius25: corrected proxy122 bounds do not overlap for tested body heights0/100/200/500. Proxy88 box remains a possible overlap at all those heights. At500, proxy399 also overlaps. These are conditional geometric checks, not measured wizard body dimensions.

## Wizard calibration finding

Imported Deimos collision_math.py436-437 hardcodes capsule_radius20 and capsule_length10. These are example algorithm inputs, not current-wizard measurements. Gemini's earlier radius20 suggestion must not become calibration by repetition. No validated client physics-body accessor or measured known-height clearance trial has been established. Wizard collision height remains unknown.

## Remaining limits

Proxy88 full-size/half-extent and axis conventions still require independent proof or bounded empirical qualification. Route follower/transition integration is not solved merely by correcting static shape metadata. No Commons->Pavilion test was run; no route claim or body-profile qualification changed. Next work should establish a controlled empirical connector/body-profile experiment with before/after evidence rather than accumulating hypothetical geometry assumptions. Gemini125 assigned the explicit remaining box/body questions through LIA-0094.

## Follow-up source audit: box dimension convention (2026-11-15)

Reviewed the primary upstream sources directly:

- [`katsuba-bcd`](https://raw.githubusercontent.com/vbe0201/katsuba/main/src/katsuba-bcd/src/lib.rs) defines BCD boxes as `length`, `width`, `depth` and parses those three floats in that order (lines 73-75, 98-105). It does not state whether they are full dimensions or half-extents. Its proxy format includes rotation, location, and scale (lines 195-207).
- [Deimos `collision_math.py`](https://raw.githubusercontent.com/Deimos-Wizard101/Deimos-Wizard101/main/src/collision_math.py) constructs box vertices by dividing each stored dimension by two (lines 52-67), then applies a transposed rotation matrix and translation (lines 69-84). This is a concrete consumer convention and supports interpreting the BCD values as full dimensions. The same source explicitly describes player clearance as calibrated behavior and says the player capsule is not readable from its collision data (lines 120-147); it does not establish Wizard101's native player collider dimensions.

Offline recomputation for the saved Commons `Collision Box 17 (1)` (proxy88) uses its saved raw dimensions `[1242.891, 645.75, 417.512]`, rotation, location `(1016.848, 1079.276, 205.484)`, and scale1. Under the Deimos full-dimensions convention, the rotated candidate half-extents are approximately `(647.088, 683.365, 208.756)`, yielding world Y bounds `[395.911, 1762.641]`. The saved approach segment's Y range is `[-231.127, -79.086]`, so its bounding box is disjoint from proxy88 under that convention. This explains why the conservative full-as-half treatment falsely reports an overlap for this segment.

This materially improves the dimension hypothesis, but it does **not** prove proxy88 is a blocking object, certify the approach as walkable, qualify the actor-body height field as a collision capsule, or validate the Commons-to-Pavilion route. The candidate needs format/runtime corroboration and the navigation corridor still needs outcome evidence. Runtime collision remains disabled; no live action or game-file write was performed.
