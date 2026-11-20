# Argon request: independent navigation contract review

Maelstrom uses C# for scheduler/input ownership, Python for optional W101 telemetry and Rust for separately qualified components. Do not propose a rewrite or a second controller.

Observed live evidence: 100ms nominal requests yielded right yaw +0.241274834rad and left -0.291540146rad. Forward at client yaw5.3573236465 produced deltaXY(48.907348633,-36.790130615), approximately61.20 units. Samples arrived roughly47-63ms apart. Nominal request duration is not measured actuation latency. Each operation restored nativeIdle. A single orientation does not establish a complete movement profile.

Current conversion is canonicalHeading=atan2(-cos(clientYaw),-sin(clientYaw)). Canonical heading is counterclockwise from+X; D increases clientYaw and therefore decreases canonical heading. Input remains the existing scheduler's responsibility.

Please provide an offline adversarial review, no implementation or live access:
1. Check conversion and turn sign against the numbers above; propose the smallest held-out calibration set that can distinguish axis/sign errors, deflection, latency and mount effects.
2. Specify how a bounded continuously sampled feed can expose freshness across Python monotonic time and C# monotonic time without comparing unrelated clock origins. Include uncertainty, delayed delivery, clock jumps and stream restarts.
3. Review arrival evidence: coordinate arrival is not named-prompt arrival; a portal needs post-dispatch loading, released movement, then two fresh observations of the exact destination zone in a new transition epoch. Same-zone transitions need a new epoch too. List counterexamples and minimum tests.
4. Suggest the smallest useful next experiment for Commons->Pet Pavilion using candidate archive graphs. Unknown corridor clearance and 200-270unit endpoint connectors remain unqualified. Repeated centerline traversal is not swept-clearance proof. Do not invent dimensions, thresholds, portal tables or coordinates.

Return Observed / Inferred / Proposed separately, at most five prioritized findings. We retain working code and one input owner. This is a review request, not permission to operate the client, extract files or add dependencies.

## New measured checkpoint and corrections to your two follow-ups

- Continuous observation run dcd14789edf94b09b0737888163cf7d3: 24 emitted samples, Commons zone candidate, no input, hook restoration/disconnect confirmed, nativeIdle observed.
- Moving stream run ac981e2cb92f4077b8fb689f58840005: same-owner100ms nominal W pulse with countdown;24 samples spanning about1.44s captured movement and settling, then nativeIdle verified. Moving-pose reads120/1152bytes, excluding zone/loading/export fences. This is not an entire route or sustained60Hz feed.
- Existing Windows monotonic readings were visibly quantized; zero measured intra-sample duration is NOT zero latency. Root is switching sampling timestamps to perf_counter consistently and will requalify; do not infer sub-millisecond accuracy from old receipts.
- Current code supports pure typed coordinate/prompt/zone arrival checks, with post-dispatch loading, confirmed release, exact destination zone, new epoch and two fresh confirmations. Production continuous path-following and corridor qualification remain incomplete.
- Our read-only review found and root is repairing readiness emitted before fresh context fences, missing terminal stream invalidation, and timeouts that could stop awaiting without joining observer cleanup. Treat the patch as pending final tests, not assumed repaired.

Your broader boundary design is useful. Please refine it around the existing scheduler rather than introducing another lease owner/goal runtime. Controlled child handoff should preserve one authoritative owner; interrupted followers must be rebuilt. UI-modal handling is the next shared priority. Arbitrary popup dismissal, purchases, snacks or combat are never default recovery actions.

Corrections to the minigame note: the owner's W101 exception permits reviewed optional client-side functionality including necessary memory writes; network manipulation remains excluded. We are not trying to alter scores. The proposed explanation of historical leaderboard values, repository coverage, exact game controls and combat/UI timings is unverified in this review; cite primary evidence before treating it as fact. A test-count claim does not qualify a production Rust controller. Navigation calibration can inform shared dispatch timing but cannot qualify a different minigame detector, hitbox, animation timing or movement mode. Avoid 'only plug in a detector', universal20-60Hz requirements and80-percent code-reuse predictions without measurements.

Requested return: at most five actionable findings, each with an observable failure case, smallest reusable repair, and offline/live acceptance evidence. Separate facts, source interpretations and proposals. Keep Commons->Pet Dance as the active integration target; no new architecture rewrite.

## Review of adversarial return (November15)

Accepted: distinct arrival types, post-dispatch loading, new epoch for same-zone transitions, post-release observation timestamps, impulse displacement versus steady speed, shorter isolated route experiments.
Corrections: recomputation gives sin=-0.79913904384, cos=0.60114622898, canonical=-0.64493466616rad and displacementAngle=-0.64493403876rad; error6.27405e-7rad, displacement61.2000201units. Algebra/turnsign are sound but the supplied numerical values are not. Single-direction evidence remains limited. Two differing turn outcomes do not by themselves identify OS jitter as their cause.
Minimum observed cross-clock offset includes unknown minimum transit delay. Subtracting it measures excess delay, not total delay; the proposed Age_upper is not a proven upper bound without an independently justified upper bound on that baseline/drift. A consistently buffered stream is a counterexample. Prefer measured request/reply offset intervals with explicit assumptions, or paired common UTC/host-monotonic envelopes with discontinuity rejection. Do not promote the estimator to an input guard yet.
Two pulse durations isolate a constant overhead only if a stable linear response model is already justified; otherwise the difference is another empirical response estimate. Repeated trials and within-motion measurements remain necessary.
The older219.776-unit connector belongs to the old recorded pose, not the current(-482.52,397.35) pose after the latest probe. Rebind current start. Passive human demonstration is optional; owner requests app-driven tests without manual driving. Portals prove destination-zone arrival, not stationary localization/clearance; keep a separate settling/relocalization check before starting the next follower.

Current-start correction: graph vertex89=(-536.39404296875,-663.42041015625,0.0003662109375). Latest moving trial ended around(-482.52,397.35). Its XY gap to89 is approximately1062.14units, not219.776. The old connector measurement cannot be reused for the current position; an automated89->96 trial needs a newly bound approach or a different initial edge. No manual placement is assumed.
