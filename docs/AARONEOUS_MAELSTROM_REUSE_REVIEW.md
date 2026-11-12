# Aaroneous and Devtools: game-player reuse review

Reviewed 2026-11-07 for Maelstrom's lightweight, visual, event-driven player.
This is a targeted source review, not a complete audit or a claim that a
cross-repository runtime already works. The local reference checkouts are older:
Aaroneous `a3383f2feeee` and Devtools `513a246e76aa`. Selected source files were
compared with GitHub main at Aaroneous `8fa44f1e47d6` and Devtools `ff0aa7c61439`.
The reviewed Aaroneous player, visual-gate, motor-loop, cartridge and solid-state
files and Devtools model-fit and management files matched the local copies.
Devtools `local_verification.rs` changed; its current main version was separately
inspected for the claims below. Other files may differ. No other repository was
modified, built, or tested in this review.

## Most useful pieces

| Material | What exists | Fit and next proof |
| --- | --- | --- |
| [`.si` format and learner](https://github.com/Aarogaming/aaroneous/blob/8fa44f1e47d65f9ccbbbc5a7d37c2b858f5c2cea/crates/compute/src/si_solid_state.rs) | The source has a reflex configuration, cartridge save/load, a model forward path and success/error adaptation methods. The [format](https://github.com/Aarogaming/aaroneous/blob/8fa44f1e47d65f9ccbbbc5a7d37c2b858f5c2cea/docs/SI_FORMAT.md) describes frozen, adaptation and skill blocks. | Strong candidate for an **optional** specialist backend. First run a small known-input save/load/forward test, specify state and action dimensions, compare outputs against a reference, and measure CPU/RAM and timing. The generic `si_format` execute method is a placeholder; the separate learner contains a real forward path. V2 and v3 constants exist in different paths, so do not infer interoperability without a roundtrip. The loader currently grants all capability bits and requests none; it does not enforce a game's action permissions. |
| [Player and event recorder](https://github.com/Aarogaming/aaroneous/blob/8fa44f1e47d65f9ccbbbc5a7d37c2b858f5c2cea/crates/platform_bridge/src/game_player.rs) | The prototype defines playthrough states and recorded inputs. Its reward is a frame-luminance-change heuristic, its demonstration action cycles linearly, and its default movement is a simple pattern. | Maelstrom can contribute verified outcomes, episode/transition evidence, game-specific rewards, fresh visual decisions and stop reasons. Do not equate a changed screen with success or use the cyclic replay as the survival policy. Its [recorder](https://github.com/Aarogaming/aaroneous/blob/8fa44f1e47d65f9ccbbbc5a7d37c2b858f5c2cea/crates/platform_bridge/src/event_recorder.rs) is a useful event-type reference; declared timestamp units and byte differences alone do not prove capture synchronization or semantic visual similarity. |
| [Visual delta gating](https://github.com/Aarogaming/aaroneous/blob/8fa44f1e47d65f9ccbbbc5a7d37c2b858f5c2cea/crates/platform_bridge/src/delta_vision.rs) | A 128×128 luminance grid is split into sectors; mean-intensity change and hysteresis select active sectors. Its savings percentage is calculated from inactive sectors. | Useful inspiration for skipping optional expensive processing. Equal-mean rearrangements, stationary hazards, color-only changes, occlusion and dropped frames require tests; an inactive sector must not erase the current world state. Measure actual processing time and missed events before using it as a control optimization. |
| [Sensory/motor pipeline and platform traits](https://github.com/Aarogaming/aaroneous/blob/8fa44f1e47d65f9ccbbbc5a7d37c2b858f5c2cea/crates/platform_bridge/src/sensory_motor_loop.rs) | A demonstrator connects gating, projection, a decoder and HID commands through `MockPlatformHost`; [traits](https://github.com/Aarogaming/aaroneous/blob/8fa44f1e47d65f9ccbbbc5a7d37c2b858f5c2cea/crates/platform_bridge/src/traits.rs) define visual and HID structures. | Reuse the separation as an adapter boundary, not as proof of an isolated live desktop. Maelstrom should add target identity, monotonic timestamps/clock origin, observation freshness, permitted actions, dispatch receipts and independently observed outcomes. Define unit and coordinate conversions explicitly. |
| [Devtools model-fit arithmetic](https://github.com/Aarogaming/aaroneous-devtools/blob/ff0aa7c614394665d9f94530e065ff3a7823a910/foundation/crates/steward/src/model_hub.rs) | Pure functions compare declared model weights, cache and overhead with measured host memory, reserved headroom and offload choices. | Good optional model-admission logic for background teachers. Use measured facts and include the game and capture budget; this does not itself monitor actual runtime usage or qualify the fast player. Do not assume LLM KV-cache formulas apply to every `.si` model. |
| [Devtools job attempts and receipts](https://github.com/Aarogaming/aaroneous-devtools/blob/ff0aa7c614394665d9f94530e065ff3a7823a910/foundation/crates/steward/src/local_verification.rs) | Current source records immutable requests, exclusive claims, numbered attempts, receipts and explicit recovery after the old worker is stopped. | Good lifecycle pattern for Maelstrom training jobs: identify exact pack, model, data, environment, budget and attempt; keep incomplete jobs and reject stale results. Its existing request and verdict types are for verification jobs, not ready-made game-training schemas. |
| [Devtools worker/backpressure model](https://github.com/Aarogaming/aaroneous-devtools/blob/ff0aa7c614394665d9f94530e065ff3a7823a910/foundation/crates/devtools_ops/src/management.rs) | Basic host telemetry and a bounded worker admission predicate exist. | Useful policy examples for quiet periods, queue caps and reservations. Hooking up trustworthy live measurements and enforcing aggregate CPU, memory, GPU and disk budgets remains work. |

The `.si` success/error APIs take state and target/error **vectors** plus a
learning rate; the inspected code does not turn a scalar game score or collision
event into those vectors on its own. That translation, temporal credit assignment
and game-policy evaluation are central integration work, not an adapter rename.
The loader also indexes the first four bytes and a declared configuration range
of block 1 after outer geometry checks. A short or inconsistent block may cause
an indexing failure; this is a source-level risk, not a reproduced crash. Add
malformed-block tests and checked ranges before using untrusted cartridges. Its
`self_test` sets `zero_allocation_verified` to true without an allocation check,
so that flag is not evidence of allocation-free inference.

## Shared shape to test

Maelstrom can become the game-facing specialist builder and operator: select a
game workspace, collect experience, define objectives and controls, train and
evaluate a policy, and run a qualified version. Aaroneous `.si` is a candidate
shared specialist format/runtime for that workflow, not an already implemented
dependency. The same training and evidence boundary can serve non-game tasks as
well, while a game pack supplies the game-specific visual and action contract.

1. **Pack and context:** identify game and version; define visual states,
   normalized inputs, allowed actions, survival/progress outcomes and limits.
2. **Trajectory:** retain observation identity and clock, action proposal and
   receipt, resulting observation, reward source, and distinct terminated versus
   interrupted outcomes. A game-over is a failure penalty for Dodge; surviving is
   positive progress. Reward only events that can actually be verified.
3. **Specialist contract:** version the observation vector, action vocabulary,
   model identity, feature extractor, resource budget and stop/unknown behavior.
   A pack may reference an `.si` cartridge, but the existing Dodge linear model is
   not automatically an `.si` model and cannot be relabeled as one.
4. **Offline learning and promotion:** use varied episodes and a held-out set;
   compare frozen policies against baselines on complete games, report the score
   distribution and failures, then preserve or roll back the exact artifacts.
   A reward correlation is not proof that an individual move caused the outcome.
5. **First interoperability slice:** make a bounded test fixture with known
   inputs and expected outputs, exercise the actual Aaroneous writer/reader and
   learner, and then connect it to a read-only Maelstrom replay. Only after that
   succeeds should a private Dodge run compare an `.si` candidate with the same
   game and seed conditions. Keep real-time desktop input outside this experiment.

## Maelstrom's contribution and current limits

Maelstrom already contributes selected-window observation, recordings, draft
packs, a named-action registry, isolated visual Dodge episodes, bounded tests,
offline small-model experiments and score distributions. These are separate
components and the private Dodge player is not a qualified generic Windows
player. Its action-contrast model uses short-horizon observational labels and a
reference-policy guard; it is not yet full-episode reinforcement learning.

This review recommends selective interoperability and shared test fixtures;
it does not establish that the Rust crates can be copied into the C#/Python app
without new dependencies. No changes were made in the other repositories. See
[language-free player learning](PLAYER_LEARNING_COMPARISON.md)
and [Dodge offline learning](DODGE_OFFLINE_LEARNING.md) for the active experiment.
