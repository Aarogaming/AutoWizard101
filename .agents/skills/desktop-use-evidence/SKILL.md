---
name: desktop-use-evidence
description: Test or improve desktop and game automation using bounded actions and evidence-backed outcomes.
---

# Desktop Use Evidence

Use this companion when building or testing a desktop-use workflow, visual game player, or automation adapter. It supplements the available computer-use skill and its tool instructions; it does not replace them, grant permissions, or change their retry/stop rules. Prefer a purpose-built connector or app interface when it fits the request.

1. Identify the requested outcome, allowed target, interaction mode, and existing authorization. Distinguish observing, suggesting actions, and sending inputs. Reuse the selected application's capabilities before adding dependencies or requiring the user to operate it.
2. Choose an appropriate path: native/game test interface when explicitly available, or the supported desktop/window tool for visual interaction. Bind actions to a current target identity and fresh observation. Treat unknown, lost focus, changed target, cancellation, and a failed safety check as reasons to stop or withhold input under the applicable tool rules.
3. Set a task-appropriate budget for duration, input count, work concurrency, and expensive processing; cap and supervise only processes created for the test. Prefer small sequential offline checks and user-authorized owned test apps when performance matters. Never infer authorization to stop unrelated applications.
4. Keep observation, proposed action, dispatch/receipt, and independently observed outcome separate. An accepted key event is not proof the game reacted. Native game state is not visual recognition, and a simulated replay is not live play. Reobserve after ambiguous tool errors rather than assuming a state change or repeating a possibly completed action.
5. Record enough local evidence to reproduce the result, including source, target, timing, limits, stop reason, and relevant artifact/pack version. Distinguish observed facts, hypotheses, and untested claims. Respect user data and disclose the scope of any metrics.

For Maelstrom-specific paths and known test boundaries, read [references/maelstrom.md](references/maelstrom.md) only when working in that repository. For the optional offline trace checker and its limits, read [references/trace-checks.md](references/trace-checks.md). Run it only on explicit local traces; it validates structure and consistency, not whether the trace is true or whether an agent followed this skill.
