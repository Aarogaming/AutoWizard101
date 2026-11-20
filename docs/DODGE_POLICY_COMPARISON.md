# Dodge avoidance comparison

The existing `motion-v1` policy remains the default. `resilient-v2`, `guarded-v3`, and `guarded-v4` are experimental pack versions; the available evidence does not establish that any survives longer. The tracker was also fixed to recognize the player near the top of the private viewport, without changing the legacy Windows capture crop. The fix has a saved-frame regression test.

The first candidate uses two-step lookahead, a longer horizon, and larger collision radii. Each test is a sequential private WSL visual run with a 20-action cap. Times below are seconds between the first recognized playing frame and the last observation. `limit` means the run was stopped while still playing; it is not a game-over time. Game over is recognized from the screen, not independently authenticated.

| Seed | Baseline `motion-v1` | Candidate `resilient-v2` |
| --- | --- | --- |
| 118 | 5.68, game over | 5.40, game over |
| 247 | 13.74, action limit | 7.25, game over |
| 781 | 11.59, action limit | 5.09, game over |
| 997 | 4.13, game over | 12.98, game over |

The later guarded revisions keep baseline constraints and use the existing policy unless lookahead has a clearance advantage. `guarded-v4` additionally permits up to two re-observations after an unknown frame without repeating input. In four paired checks, the guarded recovery candidate reached game over in all four; the baseline reached game over in two and hit the action limit in two. None of the candidate's dispatched actions in those checks differed from the baseline advice and none used re-observation. Those earlier reports did not log hold decisions, so they cannot establish that every decision was identical. A subsequent seed-247 pair recorded holds as well: no candidate decisions differed, yet one run hit the action limit and the other ended at game over. Real-time scheduling can change the game even with the same seed.

A separate installed-build smoke run on seed 1553 logged one decision override and no re-observations before game over. It confirms that the candidate path can execute and report an override; without a paired run it does not establish a survival benefit.

These are small, noisy observations. Different seeds do not hold enemy trajectories or timing fixed, and the first candidate changes both policy and constraints. Do not infer a survival win, loss rate, or causal effect from these runs. Earlier stops for uncertain tracking are censored, not deaths. `observed_play_seconds` excludes startup and starts at the first recognized playing frame; it is not the game's authoritative survival timer.

The local machine retains detailed comparison reports in `.local/dodge-policy-final-20261104/comparison.json`, `.local/dodge-guarded-comparison-20261104/comparison.json`, and `.local/dodge-recovery-comparison-20261104/comparison.json`. Reports and screenshots are user-controlled files, not authenticated evidence. Workshop's **Compare Dodge test runs** can compare one same-seed pair of private-display reports; `tools/compare_wsl_dodge_runs.py` compares multiple pairs and keeps censored results separate.

To evaluate future policy changes more reliably, the next test harness should make game stepping and input timing repeatable, then compare policies across more independent scenarios with a predeclared metric. Continue to keep the policy's observations visual, and reserve any game-state instrumentation for evaluation only.

## Controlled-step follow-up

The optional stepped copy now controls simulation ticks, seeded starting conditions and decision timing while leaving every move to the visual policy. Choose 25%, 50%, or 100% simulated speed, and compare only reports from the same speed and seed. The game advances between observations and actions; this is not a fixed script. The controller has no access to game objects or enemy positions except through rendered frames. Two short probes with the same seed, observation and input produced identical images and simulated timestamps, but full runs showed small pixel differences, so exact repeatability is not guaranteed.

The equal-constraints `dodge-context-predictive` pack isolates the existing two-step strategy from earlier changes to game constraints. The experimental `dodge-context-v5` candidate adds a penalty for near-term narrow clearance and uses enclosing radii of 34 pixels for the player and 50 for enemies. These reflect the half-heights of the capsule collision shapes in this local game's source, rather than asserting that all games have circular hitboxes. It remains a simplified conservative estimate. Both candidates remain experimental; changing collision estimates and scoring together means a comparison cannot attribute any difference to just one of them. The default remains `motion-v1`.

In five short paired scenarios (three seeds at 25% speed, two at 100%), `guarded-v4` made no different decisions and had the same observed outcomes as the baseline. The equal-constraints two-step candidate made different decisions, but the mixed results do not establish a survival improvement. The collision-aware candidate differed in two of those scenarios, both with seed 247; the following longer follow-ups used the higher 40-action and 180-request limits:

| Speed, seed 247 | Baseline `motion-v1` | Candidate `geometry-v5` |
| --- | --- | --- |
| 25% | 3.63 seconds, game over | 13.78 seconds, observation limit |
| 50% | 3.77 seconds, game over | 3.77 seconds, game over |
| 100% | 8.87 seconds, game over | 13.93 seconds, game over |

These durations measure simulated time from the first visually recognized playing frame. The 25% candidate result is censored, and the 50% run had no candidate decision overrides. This small set suggests that the candidate merits further varied testing, not that it is reliably better or ready to replace the default. Comparing different speeds is not a controlled pair: changing simulation granularity can change trajectories. Detailed local evidence is under `.local/dodge-stepped-comparison-20261104/` and `.local/wsl-dodge-runs/`.
