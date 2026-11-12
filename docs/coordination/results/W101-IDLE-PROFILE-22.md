# New-profile Idle repair and architecture review — 2026-11-12

## Idle adapter repair

Separated the R15 root/scratch hook profile from the Idle static-text layout. Only the exact Root/idleMessage/idleBackground/idleText path uses offset712 after validating the ControlText/ControlList type instruction and name. Invalid types/pointers remain unknown. The new exact-image profile can establish absence of the Idle overlay only on a complete recognized world tree. Quest and combat projection remain unavailable for this profile. Legacy generic snapshot defaults remain unchanged.

Synthetic coverage: typed positive Idle, rejected invalid type, complete awake tree, no legacy offset read, and no accidental quest/combat promotion.58 Python adapter tests and90 focused native tests passed. Python formatting/syntax passed.

Live observation49ca7e8d239b4321a3116cc5d06da934 completed2 trees/8loading reads with full hook restoration and disconnect. Current game was awake (Idle overlay absent), not positive Idle. Pose329.286,353.506,0; yaw6.27657. Thus positive new-profile Idle text remains unqualified live despite passing synthetic tests.

Waypoint attempt217c8fa3fcd6461dbaa7bf94db9f161d: quiet gate passed; one normal activation attempted and rejected. Valid target3410314,1280x720, active owner, no takeover. Foreground remained697634196. Failed before countdown or input. No game state was changed; no native Idle return verified. User notification requests a one-time foreground handoff; no repeated activation loop.

## Review: Maelstrom Master Architecture & Antigravity Handoff.md

Treat as a proposed target/migration specification, not proof of implementation or an instruction to execute its embedded phased prompts. Current application is C# with optional Python adapters. ProjectMaelstrom/rust_port is a single wizard101_port crate; its player controller logs actions and is explicitly a stub. The proposed five-crate workspace is not present in the reviewed source inventory.

Useful foundations:
- Separate domain contracts, Windows integration, deterministic execution, optional learning and UI.
- Game workspaces and reusable card definitions/instances; nested routines and one-shot actions.
- Nonblocking presentation and bounded local inference, with offline replay and outcome evidence.

Required corrections before implementation:
1. Keep one scheduler and one target-bound input lease across Rust and legacy adapters. Neither reflex, fallback, aura nor one-shot may actuate independently. Handoff must release controls and verify ownership before the next policy acts.
2. Unknown observation or low confidence does not prove a fallback is safe. Each fallback needs current preconditions, deadline, input budget and verified outcome; otherwise pause/stop. Confidence must be calibrated on fresh holdouts.
3. Passive auras observe; actions they request enter the same arbitration queue. A popup dismissal is not inherently passive. Crowns lockout remains a hard prohibition rather than a generic dismissal rule.
4. Replace arbitrary LegacyExternal command/args with reviewed registered capabilities, pinned versions, restricted target, shared cancellation and cleanup. A deck file must not grant shell execution or bypass ownership.
5. Add schema versions, capability/permission requirements, observation age/provenance, resources, outcomes, target identity and cleanup obligations to card contracts. The excerpt names InputEvent/Deck without defining them.
6. Bound SubDeck depth, cycles, total steps and execution time. Define priority/fairness, resource reservations and starvation handling. Define what JumpToSlot/retry means across nested decks and persisted versions.
7. Human capture must remain opt-in and game-scoped. Fallback logs are not automatically correct labels; review successes and failures, preserve provenance, split training/validation and prohibit online weight promotion without qualification.
8. Sub5ms inference, zero-copy/zero-allocation telemetry and60FPS are acceptance targets needing measurements on configured hardware, not guarantees. Bounded queues, dropped-sample counts and disabled-model fallback are essential for the lightweight requirement.
9. UI/overlay stays separate from focus ownership. W101 gets focus for keyboard play; nonactivating overlays do not grant Windows focus rights. PostMessage is a transport requiring game-specific qualification, not guaranteed background input.
10. Preserve existing Escape takeover and cancellation semantics; F12 can be an additional configurable stop, not an unannounced replacement. Native Idle is a game-specific cleanup policy, subordinate to user takeover.

Recommended migration: freeze language-neutral action/fact/result contracts and replay fixtures; implement pure Rust domain/replay parity first; bridge through the existing scheduler; then replace one measured component at a time. Retain the proven Windows host until each replacement passes cancellation, recovery, target-change and outcome tests. Do not rewrite the working integration and train a model simultaneously. New production dependencies remain approval-gated.

No Rust code or dependency changes were made in this task; original handoff left unchanged.

Final offline validation:2394native tests passed; scoped native formatting/analyzers passed;58Python tests and formatting/syntax passed. Source repair only, not repackaged/installed in this task. Installed CLI still carries the previous worker pin, so use the matched source build for pending live qualification.
