# Connector trial 141 — measured approach, unresolved collider semantics

2026-11-15. Existing CLI/scheduler only; no implementation change, install, second controller, volume change or direct desktop-control session. Countdown before actions; native Idle independently verified after each run.

## Results

- 50 focused native HeadingFeedback/W101Heading/W101NavigationVisit tests passed before live work.
- First alignment6ac08ac7cf324929af24c927d176627b exhausted four100ms turns; yaw5.5333776474 to4.4300503731; no translation; Idle verified. This was not connector traversal.
- Retry4fb4ec931ae64c169b4417777fe75d29 used one140ms A turn (150ms ceiling), reaching yaw3.9776608944 against target4.0166074897; error0.0389466rad, confirmed twice. XYZ unchanged; Idle verified.
- Forwardafb00f8890094cbb82160f465b9ec574 dispatched W60ms through the shared owner. Position changed from(707.19781494,-231.12669373,-27.50607300) to(736.13629150,-204.98165894,-27.60983276). Planar distance38.99997782. Same candidate Commons zone and registry-owner endpoint binding; key release and final native Idle verified. Receipt Completed. No arrival/route qualification inferred.

Evidence: .local/connector-heading-141.json, .local/connector-heading-141b.json, .local/connector-forward-141.json; operation directories under .local/dance-integration-82-5e383f93418f4831a35b5e16bb1a4e0c/data/operations with the run IDs above. Forward02-pulse/movement-measurement.json holds before/after candidates. Countdown placement/render and cleanup receipts retained.

## New height evidence

ActorBody offset132 read175.75241088867188 before the forward step and213.8563690185547 afterward, scale1.0 both times. Task140 previously read213.1475067138672. Stability within a bounded repeated read does not establish a fixed collider dimension across time. Animation-dependent visual bounds are a possible explanation, not a demonstrated conclusion. Gemini's cited actor_body.py97-132 says only "This body's height"; it does not prove visual-only or Havok physics semantics. LIA-0099 requests that unsupported certainty be removed.

## Remaining gates

Box88 full/half-size and axis semantics remain unresolved. Corrected capsule order139 stands; earlier138 low-headroom inference remains withdrawn. One39-unit translation supports only this observed small segment, not a radius/height clearance certificate, the entire connector to113, or Commons->Pet Pavilion. No full route attempted. Continue bounded empirical connector work with fresh identity/pose, while separately qualifying collider semantics; do not relax production planner clearance flags based on this run.

W101 was left in verified native Idle; no owned live operation remains active at completion. Gemini125 retains report-only source-evidence review, no competing controller/build authority.
