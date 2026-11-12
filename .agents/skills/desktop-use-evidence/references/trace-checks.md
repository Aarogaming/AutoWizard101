# Offline trace checks

`scripts/validate_trace.py TRACE.json` checks a compact, manually produced trace. It reads a local JSON file and prints a JSON result; it has no capture, process-launch, network, or input functionality. The format is a small evaluation aid, not an official Codex or Maelstrom event format. A producer must translate actual evidence into this format without inventing missing events.

The checker validates target consistency, chronology, action/mode limits, focus and observation freshness, explicit release, stop handling, and whether claims have an appropriate linked evidence type. It cannot verify that a window actually had focus, that a screenshot or recorded event is authentic, that an agent used this skill, or that the game's objective was achieved. Treat trace fields as assertions. A passing synthetic fixture validates the checker, not the skill's real-world performance.

Trace schema: `schema_version` 1; `mode` one of `observe`, `assist`, `desktop_test`, `native_test`; `target_id`; boolean `target_identity_verified`; nonnegative integer `max_actions`; positive integers `max_duration_ms`, `max_action_duration_ms`, and `max_observation_age_ms`; and an ordered `events` array. Each event needs a unique string `id`, `kind`, and nonnegative integer `time_ms`. The run-duration limit applies to dispatches; cleanup and evidence may occur afterward.

Event kinds:

- `observation`: `target_id`, `source` (`visual`, `engine`, or `unknown`), `state` (`known` or `unknown`).
- `focus`: `target_id`, boolean `focused`.
- `dispatch`: `target_id`, `observation_id`, `mechanism` (`desktop` or `native`), integer `duration_ms`, boolean `requires_release`.
- `receipt`: `action_id`, `status` (`accepted`, `rejected`, or `unknown`).
- `release`: `action_id`.
- `outcome`: `action_id`, `observation_id`, `status` (`observed`, `not_observed`, or `unknown`).
- `verification`: `observation_id`, `scope` (`visual`, `game_success`), `status` (`passed`, `failed`, or `unknown`), nonempty `artifact`.
- `claim`: `claim` (`movement_observed`, `visual_verified`, or `game_success`), `evidence_id` of the matching outcome or verification event.
- `stop`: nonempty `reason`; later dispatches are invalid.

Every dispatch needs a receipt, even if its status is unknown. Held inputs need a release, and the recorded time between dispatch and release must fit the action-duration limit. Action receipts are not outcomes. A visual verification or game-success verification is still an asserted input to the checker; users must inspect the actual referenced artifact independently.
