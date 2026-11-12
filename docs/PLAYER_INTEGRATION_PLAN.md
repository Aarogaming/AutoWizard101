# Player integration course

Maelstrom is the local Windows workspace for building, evaluating and running
game players. It owns the selected target, capture, user experience, run limits,
evidence and (when qualified) input. Game packs contain game-specific knowledge.
Third-party projects supply replaceable capabilities through narrow adapters or
act as development references; their applications need not all run together.

## Existing starting point

- The workspace can select a window and observe/record it; capture throughput and
  frame-to-input latency remain unqualified for real games.
- The sampler, recordings and pack workshop exist. `tools/game_packs.py`
  validates draft state-to-proposal rules; `tools/evaluate_game_pack.py` replays
  them without input. These are a basis for the shared harness, not a live player.
- The new experimental Dodge advice in Observe detects colors on individual
  frames, sends no input, and has only a small saved-frame check. It is not a
  temporal tracker, complete game pack, or gameplay qualification.
- Schema 2 packs can now declare named key actions and map state rules to those
  bindings. A completed Dodge recording can be exported as a read-only session
  and evaluated against the draft Dodge sample in Workshop. It still does not
  execute actions or verify the outcome of a proposed action.
- MaaFramework, Airtest, and the owner's earlier repositories already have local
  source snapshots in `.local/reference-repos/`. A local source snapshot does
  not imply an installed or tested integration.

## Capability boundaries

| Capability | First implementation / candidate | Boundary |
| --- | --- | --- |
| Window and recording | Existing Maelstrom workspace | Target identity, geometry, timestamps and bounded queues belong to Maelstrom. |
| Visual observation | Existing matching and Dodge baseline; evaluate Maa/Airtest/OpenCV | Accept a supplied frame; return timestamped observations and explicit unknowns. No independent input owner. |
| Decisions | Extend existing draft game-pack rules | Data-only conditions and bounded proposals, with source observation and expiry. |
| Execution | One Maelstrom-owned input dispatcher, still to qualify | Target/focus/geometry checks, stop and release, bounded input; Observe and replay never dispatch. |
| Evidence and evaluation | Existing sessions/replay; Gymnasium and GameWorld as references | Distinguish proposal, dispatched input and observed outcome. A game test oracle is not player perception. |
| Build and improve | Workshop and optional model provider; Game2Player and ScreenQuest as references | Propose changes offline; compare with baseline on held-out cases before promotion. No requirement for a model during play. |
| Dataset tooling | Sampler; optional CVAT/FiftyOne export | Use separate optional tools only when justified; do not start server stacks by default. |

## Smallest end-to-end milestone

1. Define and validate a versioned observation/action/outcome contract on top of
   existing pack and session formats. Preserve old packs; unknown or stale data
   cannot produce a live action.
2. Have the Dodge specialist produce observations for the pack engine. Save a
   draft Dodge pack and replay saved, separate sessions through it. Include
   negative screens, ambiguous objects, missed detections and timing.
3. Compare a baseline and a candidate rule pack using a repeatable report.
   Outcomes are unknown unless independently observed; comparing proposed keys
   with recorded keys alone does not prove a successful action.
4. Add one explicitly enabled, time-limited Dodge test through the shared input
   dispatcher. Revalidate target, freshness and focus; release keys on stop,
   loss of focus or failure. Record actual outcomes and resource usage.
5. Present attach → observe → build/check → bounded test → review in the app.
   Confirm the installed application preserves packs and evidence.

The milestone does not depend on a model, a browser-based environment, or a
second process taking over the game. Evaluate one narrowly scoped adapter only
after the existing path has a measurable gap. Model-assisted rule authoring and
actual model training are separate later stages.

## Resource and test policy

Start no model or external service on opening Observe. Limit concurrent jobs,
queue depth, rate and run duration; show which process is active. Allow the user
to cancel and retain partial results. Protect desktop responsiveness by testing
in small bounded runs and recording CPU, memory, GPU, capture and decision
latency where measurable. Never treat synthetic game loop ticks or an encoder's
nominal rate as delivered game-frame throughput. A global system-resource
governor is still to be implemented; the current AI request limit is per run.

## Research and sourcing

See [Open-source player research](OPEN_SOURCE_PLAYER_RESEARCH.md) and the earlier
[Maa/Airtest review](VISUAL_AUTOMATION_REUSE_REVIEW.md). Source snapshots should
record upstream revision, license and local changes, and be kept out of the app
runtime unless selected and approved. Check code, models and game assets
separately. Do not install all frameworks or launch their UIs to orchestrate them.
