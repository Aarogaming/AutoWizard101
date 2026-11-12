# Game context and identification

Schema 2 draft packs may include an optional version 1 `context` object. It is data, not executable code. The example context-aware Dodge pack at `tools/fixtures/game_packs/dodge-context/pack.json` stores a goal, named states, states where actions are allowed, an identification rubric, numeric constraints, and compact state-signature masks. Pack validation checks structure and size, and Workshop preserves the object when saving a new version. Existing packs without context remain valid.

Workshop's **Dodge context example** button loads the sample and displays its goal, allowed action state, and identification threshold. The full context and masks currently require JSON editing; a general visual context/rubric editor is not implemented yet.

The first consumer is the private WSL Dodge visual test. It uses the pack's viewport, movement speed, player and threat radii, edge margin, prediction horizon, tracking age, observation-age budget, and maximum action duration. It accepts only the supported Dodge context, a consistent action duration within that limit, and advice/action/key mappings that agree on direction; unsupported context fails closed. The older foreground and headless Dodge paths reject context-aware packs until they enforce the same context. They still support the original baseline pack.

## Identification rubric

The current Dodge test requires all three signals before sending a game action:

| Signal | Points | What it means in this test |
| --- | ---: | --- |
| `owned_session` | 2 | The launched private test bridge reported ready. |
| `viewport` | 1 | The captured frame is the expected 480 × 720 size. |
| `visual_state` | 2 | The Dodge specialist recognized a declared state. |

The pack requires 5 of 5 points. These signals are correlated and are not authentication or proof: an owned bridge can be modified, a different game can share a resolution, and visual recognition can be wrong. The report records the actual signals and score. This first rubric is scoped to the explicitly launched local demo; it is not a general-purpose game identification service.

## States and moving hazards

The Dodge specialist compares the frame with the pack's compact menu, ready, and game-over signatures; it reports `playing` only when it finds a unique player sprite, and otherwise reports `unknown`. It sends no action in menu, ready, game-over, or unknown states. The signatures are calibrated to the supplied demo and its private 480 × 720 display. They are not OCR and have not been qualified for arbitrary skins, layouts, or games.

For playing frames, the guest timestamps capture with its monotonic clock. Maelstrom associates nearby threat detections across recent frames, estimates velocities only for plausible, unambiguous matches, and marks other velocities unknown. It evaluates short candidate moves against projected threat positions, boundaries, and pack radii; unknown velocities receive a heuristic uncertainty penalty. Host request-to-decision age is checked before input. These are estimates from pixels and timestamps, not ground truth or a collision-free guarantee; the timestamp does not establish screen-presentation latency.

Initial context-aware checks reached 12 and 14 actions on seeds 247 and 781 before game-over recognition. One development check reached the 20-action cap with 20 movements visually observed on seed 307. After the prediction and uncertainty sampling were tightened, final checks on seeds 307 and 997 ended at game over after eight and six actions, with seven and five movements observed respectively. A seed helps investigation but does not reproduce real-time scheduling exactly. These examples are too few for an accuracy or survival-improvement claim.

For later policy experiments and their censored comparisons, see [Dodge avoidance comparison](DODGE_POLICY_COMPARISON.md). The original `motion-v1` policy remains the default; the candidate policies are not qualified as improvements.
