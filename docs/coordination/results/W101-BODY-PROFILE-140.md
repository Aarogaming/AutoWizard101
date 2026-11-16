# ActorBody height/scale observation — 140

2026-11-15. Read-only fields under the existing optional reviewed observer hook. No movement, focus change, countdown, volume change or installation.

## Reuse finding and implementation

Installed wizwalker memory_objects/actor_body.py103-128 already exposes height at132 and scale at112. Added read_dimensions to the existing pose reader: four reads/64 requested bytes, stable parent and repeated value block, bounded finite values, sanitized unknown outcomes. Hook/registry identity and lifecycle remain owned by the existing reader. Stored under zoneCandidate.bodyDimensionsCandidate; collisionQualified always false. Updated reviewed hashes without adding a dependency. Unknown/changed values never create a usable body profile.

65 focused native tests,8 pose-reader tests,11 owner-stream tests and83 UI-reader tests passed. Scoped formatting/analyzers, Black and compileall passed. New test exercises stable reads and mid-read mutation.

## Live result

Run 67750c63fa8c4b1aac9bdeab570a8453; .local/body-profile-140.json. Height field213.1475067138672; scale field1.0. Candidate zone WizardCity/WC_Hub, registry owner matched at observation endpoints. NativeIdle=true, CleanupConfirmed=true, HookRestored=true. No game inputs were sent; Idle remained active.

## Calibration boundary

This verifies a stable current ActorBody field, not that it is the physics capsule height, its feet/center convention, or interaction with mounts and scale. Do not silently multiply height by scale or equate visual/actor dimensions with collision dimensions. Actual collision height remains uncalibrated. Earlier absence-of-accessor assumption is corrected: an accessor does exist and was missed in the prior review.

Box88 full-size/half-extent and axis convention remains unresolved; no new claim that it is passable. No Commons->Pet Pavilion route test occurred. Resolving a testable empirical connector/body-profile contract remains necessary; a successful field read is not route qualification.

Gemini125 receives actual height/scale result via LIA-0096 and is asked to trace physics semantics or devise a bounded known-clearance measurement, rather than repeat guessed dimensions. Last game state verified this run: Commons native Idle.
