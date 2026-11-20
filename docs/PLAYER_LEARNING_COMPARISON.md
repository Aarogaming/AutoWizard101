# Language-free game-player learning

Research note, 2026-11-07. The user describes Maelstrom as reaction training
against a rubric, supplied with context, relevance and intention, producing a
small player that does not need language during play. That is a useful description
of a **conditioned policy**: it maps observations, recent history, task context
and allowed actions to a next action. It can be a rule graph, a compact learned
model, or a combination. The environment and the learned model can still behave
stochastically; a frozen program is not a promise that every run follows the same
sequence or has the same outcome.

## How related systems do it

| Approach | Training or authoring | Live player | Useful lesson and limit |
| --- | --- | --- | --- |
| [MaaFramework pipelines](https://github.com/MaaXYZ/MaaFramework/blob/main/docs/en_us/3.1-PipelineProtocol.md) | Authors define recognition, actions, branching and recovery nodes; recognizers can include templates, OCR and models. | A recognition-driven pipeline chooses the next matching node and executes its bounded action. | Good inspiration for inspectable game packs and event-driven macros. A configured pipeline does not itself prove that a game policy was learned or qualified. |
| [Atari DQN](https://arxiv.org/abs/1312.5602) | A visual neural policy is trained with reinforcement learning from game observations and reward in a controlled environment. | A trained model evaluates actions from visual input without a language model narrating every move. | Demonstrates language-free learned play; success depends on the environment, reward, action space, data and evaluation. It is not a drop-in policy for arbitrary desktop games. |
| [Minecraft VPT](https://openai.com/index/vpt/) | Human demonstrations with recorded controls train an inverse-dynamics model; that model proposes action labels for a much larger set of videos, and a player is trained from those labels. Further imitation and reinforcement learning specialize it. | The trained player uses game observations to produce keyboard and mouse actions. | Online video is useful but generally does not contain the exact inputs. Predicted action labels need their own validation; the published project used a very large dataset and is not a lightweight default for Maelstrom. |
| [Game2Player](https://github.com/game2player/Game2Player) | A scout, environment builder, players and feedback loop adapt games, define verifiable tasks and collect trajectories for training. | Its code player uses exposed game state; its visual player uses screenshots. | Closest organizational analogy for a player-building harness. State exposed by an instrumented test is not automatically available in a normal desktop game and must not be presented as screen-only performance. |
| [ScreenQuest](https://github.com/DanielTea/screenquest#architecture) | A local experiment combines visual checks, a fast decision path, a slower planner and independent post-run review. | The fast path works from the latest available state and does not wait for the slower planner. | Useful architecture reference for freshness, arbitration and review; it is a Mac-specific prototype, not proof that the same setup works for Maelstrom or every game. |

[Gymnasium's environment contract](https://gymnasium.farama.org/api/env/)
also illustrates the training boundary: reset an episode, apply an action,
record the next observation and reward, and distinguish a true terminal outcome
from an externally truncated run. Maelstrom can adopt those ideas without making
Gymnasium a production dependency or assuming every commercial game exposes a
reset or reward API.

## A practical Maelstrom design

### A visual survival reflex

For Dodge, an embodied player need not have feelings or use language. It needs a
continuously updated working representation of **itself**: the player location,
recent movement, arena edges, available actions, nearby visible threats and the
uncertainty or age of those observations. The policy can react to cues from
recent frames rather than relying on a fixed order of encounters. Estimating
motion or risk is useful, but it does not require claiming that random enemy
spawns are predictable.

The training rubric can provide positive reward for verified survival over time
and relevant game progress, and a terminal penalty when a collision is verified.
The current Dodge score is a survival timer, not a verified count of enemies
defeated, so the trainer must not invent a per-enemy reward. Any extra reward for
clearance, movement or other proxy should be tested against complete-game score:
a convenient proxy can be optimized without producing the intended behavior.
Rewards are numerical training signals, not evidence of subjective pain or joy.
Whole episodes and actual subsequent transitions are needed for temporal credit
assignment; a later outcome following a chosen action does not alone prove that
the action caused it.

Two related examples are [Swift drone racing](https://www.nature.com/articles/s41586-023-06419-4),
which combines compact perception, onboard control and simulation-trained RL,
and [robot soccer](https://deepmind.google/research/publications/31284/), which
uses varied simulation and perturbations to train movement and recovery. Their
hardware and training setups differ materially from desktop games; they provide
architecture lessons, not pretrained drop-in Maelstrom players. Reward design
should also be checked for [specification gaming](https://deepmind.google/blog/specification-gaming-the-flip-side-of-ai-ingenuity/).
The [UNREAL agent](https://deepmind.google/blog/reinforcement-learning-with-unsupervised-auxiliary-tasks/)
also used auxiliary tasks concerning how actions affect pixels and rewards; this
is relevant to learning the relationship between controls and the player, but
does not establish that its computational requirements fit Maelstrom's default.

```text
Game pack: identity + controls + context + allowed actions + goals/limits
                               │
Selected game → frames → perception + recent history → fast policy → input owner
       ↑                                                        │
       └────────────────── game responds / next frame ───────────┘
                               │
                 recordings + action receipts + outcomes
                               ↓
          offline collection → training → frozen candidate → fresh evaluation
                               ↓
                    versioned pack / optional player bundle
```

The **runtime context** says which game, task, controls, constraints and current
state apply. A goal can be a numeric or symbolic identifier; it does not have to
be a sentence. Recent frames or tracked motion supply temporal context: a still
image alone may not reveal whether an enemy is approaching. The **evaluation
rubric** separately defines success, failure, score and budgets. An instrumented
test game can supply a score or terminal event to the evaluator, while the actual
player remains restricted to the intended screen-derived observations. For an
ordinary game with no test API, outcome checks must come from observable UI or
reviewed evidence.

### Relationship to Aaroneous `.si`

The user's intended architecture is compatible in concept with the Aaroneous
[`.si` specification](https://github.com/Aarogaming/aaroneous/blob/main/docs/SI_FORMAT.md):
a game pack can describe a particular game's identity, visual cues, controls,
survival objective, limits, and evidence, while an optional versioned specialist
cartridge carries a compact policy or reflex implementation. The pack should
record its exact model, feature and action contract, hashes, and evaluation
results, and should remain understandable without mounting a model. A small
standalone rule/weight specialist remains a valid implementation.

This is an integration direction, not an existing `.si` bridge or a claim that
every documented Aaroneous feature is operational. In the source inspected on
2026-11-07, the canonical [format header](https://github.com/Aarogaming/aaroneous/blob/main/crates/si_format/src/header.rs)
declares v3 while a [solid-state implementation](https://github.com/Aarogaming/aaroneous/blob/main/crates/compute/src/si_solid_state.rs)
also declares v2. The generic [cartridge `execute_tick`](https://github.com/Aarogaming/aaroneous/blob/main/crates/si_format/src/cartridge.rs)
is explicitly a placeholder. The separate `SiOnlineLearner` in `si_solid_state.rs`
does contain a model forward path and success/error update methods, but those
methods are not yet validated as a game policy or a reward-learning algorithm
for Maelstrom. A real interoperability experiment must identify the actual
intended reader and version, exercise pack/load/infer with known
inputs and outputs, establish permissions and resource limits, and test behavior
on fresh episodes before claiming support. Do not silently rely on online weight
updates during play: train and evaluate a new version offline, then freeze it for
the real-time specialist unless a separately tested mode explicitly permits it.

The fast controller chooses among the pack's permitted actions with an explicit
freshness and target check. A slow vision or language model may help research,
label examples, explain a run or propose a future pack change; it must not stall
the reflex loop or dispatch an expired suggestion. A fallback is game-specific:
holding still can be unsafe in Dodge, so there is no universal safe action.

### Reactive and strategic specialists

The shared system can also support a turn-based game such as chess, without
putting a chess search engine into the live Dodge loop. Both use a versioned
observation, action vocabulary, goal, feedback, evidence and evaluation boundary.
Dodge's specialist consumes recent visual motion and responds within a short
deadline; a chess specialist needs a verified board, legal-move rules, clock state
and a bounded search budget. The action adapter for a visual board would still
need to verify the selected move occurred. Chess positions branch too widely to
assume every continuation can be calculated; search prioritizes useful lines.
[AlphaZero](https://deepmind.google/blog/alphazero-shedding-new-light-on-chess-shogi-and-go/)
is an example combining self-play learning with guided tree search, at a scale
well beyond the current lightweight Maelstrom experiment. A future optional
chess adapter could reuse a verified existing engine rather than training or
shipping a large general model; that integration is not implemented here.

Knowledge can compound through reusable contracts, validated perception and
input components, training/evaluation tools and explicitly compatible model
representations. A good Dodge score does not itself improve chess, and vice
versa; actual transfer must be tested. Per-game packs and bounded optional
specialists keep unrelated models and search code out of the live path.

## How the learning loop should improve

1. **Collect varied complete episodes.** Capture observations, timing, selected
   actions, selection probabilities where relevant, action receipts and observed
   outcomes. Preserve unknown and interrupted runs rather than treating them as
   failures or successes. Keep resource limits and a stop path.
2. **Build a useful training signal.** Demonstrations teach what a player did;
   scored exploration can help estimate what worked. A short-term survival label
   also reflects later actions, so it is not a proven counterfactual for the first
   move. For a controlled test game, reset or branch experiments can compare
   alternatives more directly. Keep any privileged simulator data on the
   evaluation side unless the intended deployment genuinely provides it.
3. **Fit a compact candidate offline.** Begin with cheap visual features and
   small models or rules. More complex reinforcement learning, imitation,
   distillation or a shared specialist router are options if the data and measured
   task justify them. Training and any heavy teacher can be scheduled away from
   live play. New production dependencies require the normal project approval.
4. **Freeze and compare.** Save the pack, model, data and environment versions.
   Compare against a frozen baseline on fresh, varied complete games; distinguish
   tuning games from validation. Report the mean and distribution, including
   early losses and uncertainty. For Dodge, the present user target is an average
   in-game score of at least 50, not a presumed creature count.
5. **Promote deliberately.** Run the approved frozen specialist in Observe or
   Assist first, then bounded Play when the relevant target, timing, input and
   stop checks are qualified. Keep rollback and record which version acted.
   A self-contained executable can bundle the runtime and pack later; it is not
   necessary to recompile the host each time weights change.

## Where Maelstrom stands

The private Dodge system already collects randomized episodes, trains small
CPU-only models, packages draft policies and evaluates complete games against
reserved seeds. The newest action-contrast trainer directly models differences
between available moves instead of merely predicting scene danger. This is real
offline learning, but it is a short-horizon observational experiment, not yet a
long-horizon RL system. Its current player still relies heavily on a reference
rule and visual clearance checks; observed scores cannot be credited to the model
alone. Results and limits are in [Dodge offline learning](DODGE_OFFLINE_LEARNING.md).

The larger gap is productization and generality: one pack/session/action contract,
in-app training and evaluation controls, measured Windows reaction latency,
resource governance, robust recognition, a second interaction type and validation
outside the private simulator. [The game-player gap analysis](GAME_PLAYER_GAP_ANALYSIS.md)
sets out those gaps and an ordered path.
