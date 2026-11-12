# Dodge: local machine-learning research

## 2026-11-08: native experiment workflow and a negative comparison

The [experiment workbench](EXPERIMENT_WORKBENCH.md) now freezes candidates,
reserves development/validation openings, runs bounded private games, compares
complete-game distributions, and preserves reports, audit hashes and decisions.
Its first real paired comparison is in
`.local/experiment-workbench-data/experiments/20261108-055448-5aba7db633e4/`.
The reference scored **20, 7, 11, 124** (mean **40.5**); the episode-return model
scored **20, 7, 11, 26** (mean **16**). All eight games ended in game over. The
candidate failed development, and both reserved ten-game sets remained unused.
This is not repeatable average-50 performance; no player was promoted.

In the fourth opening, the first differing recorded advice appeared at 7966.7 ms:
the reference chose Down-Left and the episode candidate Down. Across that
candidate game, the reference action was retained 269 times, the model chose
among passing clearance options six times, and fallback handled 38 decisions.
The reference's longer game had 1316, 51 and 59 respectively. These are behavior
counts, not proof of which action caused the outcome; different actions can
change subsequent encounters. See `development-diagnostic.json` for the exact
records. The next learning iteration must account for weak observed action
support and this failed full-game comparison, rather than assume episode
returns automatically make a better player.

The older comparison under
`.local/dodge-curricula/fallback-8ce2fde595724131b502e26837d6cac9/` was interrupted
after three recorded baseline games. Its stale status is documented separately
in `lifecycle-review.json`; no missing game was silently replaced.

Gameplay footage can now be attached as verified snapshots with quality
assessments. A real import of the existing Dodge user-round recording produced
241 images at 4 samples/second, 214 distinct image hashes, consistent dimensions
and a 250 ms median sample gap. These are unreviewed visual examples, not verified
action or reward labels. The packaged importer also extracted 600 images from a
20-second clip at 30 samples/second: 479 distinct hashes, consistent dimensions
and a 33.33 ms median sample gap. Neither run invoked a model or inferred keys.
Video-only action learning remains a separate gap.

## Existing offline trainers

Maelstrom can now train small action models from **completed, previously recorded** private Dodge games. This training reads local reports only: it does not start the game, capture the desktop, send inputs, call an online service, or host a large model. The learned policy is an experimental game-pack component for the private stepped environment; it is not a qualified desktop player.

The current research pipeline is:

1. Verify the original benchmark manifest, completed game reports and game-over evidence. Separate training and evaluation by whole game seed, not by frames from the same game.
2. Extract the player position and up to three visible tracked threats. Add bounded, action-relative clearance and screen-edge features. These are game-specific features derived from visual observations, not hidden game-state inputs. The model does not process raw video or learn visual detection itself.
3. Train a small multiclass linear behavioral-cloning model and a bounded nearest-demonstration comparison. Limit the amount sampled from each game and cap the total examples; training uses the Python standard library and CPU only.
4. Report agreement with the recorded actions, alongside a single-action reference. The linear model and training report have matching hashes; both the training data reports and model are fingerprinted. A separate replay mode rejects seeds used in fitting and checks that the teacher pack matches.
5. Build an immutable draft pack that embeds the linear model and its provenance. The private player validates the model dimensions and action names, computes the same features, and applies the existing movement boundary checks. Cross-language regression tests compare its features and raw predictions with the offline trainer. A boundary check can change the action actually sent.

This is **behavioral cloning**, not reinforcement learning or an automatic discovery of game rules. Movement training uses actions whose movement was observed; unverified actions are excluded, and verified actions can still be bad decisions. The first teacher itself failed the independent 50-point target. Agreement with recorded actions is not evidence that the learned policy survives, nor that different actions would be worse. Samples from the same game are correlated, and recordings used during earlier development are not an independent final test even if their seeds were held out from model fitting.

The initial model contains only movement labels; the private player therefore holds when it sees no threats, rather than inventing movement. Future private runs record fresh visual state for `Hold` decisions, and the trainer can include those labels with `--include-holds`. It refuses that option when the training games do not contain fresh recorded holds. A hold label is a teacher decision, not proof that waiting was safe.

An initial model fitted on **913 decisions from six recorded games** matched **34 of 96 decisions (35.4%)** from two other recorded games. The largest single action in those two recordings accounted for **19.8%** of decisions. A subsequent replay of six additional, previously exposed recordings gave **119 of 300 (39.7%)** agreement for the linear model and **112 of 300 (37.3%)** for nearest demonstrations. These are small, development-stage imitation measurements. The model was not selected or qualified using game survival in those evaluations.

The model and report are in `.local/dodge-offline-models/77fa09786e0f49719e44a5fbabb4831c/`; the later replay report is `.local/dodge-offline-evaluations/b95119c7405545bda006c2108c2894a7/`. The draft pack is `.local/dodge-learned-packs/dodge-context/0.11.0-b5bba46991ea/`. These local artifacts are not installed or promoted.

In its first closed-loop development trial, this model controlled four complete private, 25%-speed stepped games and scored **7, 6, 16, and 10**, for a mean of **9.75**. The saved reports show actual model-selected actions and fresh hold observations; this was not just a replay of recorded actions. The openings had already been exposed during project development, so the results are not independent validation. This experiment demonstrates that the training-to-game loop runs, not that the player is ready or meets the mean-50 objective. The reports are in `.local/dodge-benchmarks/96549cc0bba848e8b9e6b4d6772c8301/`.

A subsequent baseline comparison stopped incomplete when a single antialiased score pixel was mistaken for a second player landmark. The saved frame is now a recognition regression case; the tracker still withholds movement for multiple plausible landmarks. That interrupted game is not counted as a completed score. Comparisons following the tracking repair must use matching application builds for both policies.

With the repair in both builds, a matched four-game development comparison produced baseline scores **30, 49, 17, 14** (mean **27.5**) and learned-policy scores **7, 6, 16, 10** (mean **9.75**). The learned policy lost every paired comparison and neither policy met the target. These are previously exposed development openings, not a final holdout. The complete comparison is `.local/dodge-ml-comparison.json`; the benchmark directories are `.local/dodge-benchmarks/de8be74372fa4c69a2be81433fd930e7/` and `.local/dodge-benchmarks/5f5b89b9dd86456aae5ddf77f1cb2195/`.

A new nine-action model was trained offline from two baseline games, with two other complete games reserved for evaluating agreement. It matched **164 of 300 decisions (54.7%)**, versus **52.3%** for always choosing the most common action; most examples were holds. The draft model and pack are `.local/dodge-offline-models/d42eab4cf7dd4a598a584d9c0bb61196/` and `.local/dodge-learned-packs/dodge-context/0.11.0-0d4822401064/`.

In the same four private development games, that model scored **25, 4, 10, and 5**, averaging **11**, versus the baseline's **27.5**. The first two openings were also in its training data, so even these poor results are not independent. The last two were held out from fitting, though all four were previously exposed during project development. The model selected many holds and is not qualified for promotion. The comparison is `.local/dodge-ml-hold-comparison.json` and the reports are `.local/dodge-benchmarks/95368a1435e847dd97c6b5f716e7f462/`.

`tools/train_dodge_survival.py` is a separate outcome-learning experiment. It uses the visual state and the action actually taken as features, and labels whether the player remained alive for another five simulated seconds. The private game's saved audit supplies only `ticks` and `alive` for offline labels; labels whose horizon falls between the last-alive and first-dead captures are excluded. Game positions from the audit are not model inputs. The resulting logistic model is observational: it does not establish what would have happened for a different action and is not connected to game control.

On the same two recorded test games, the first outcome model had a Brier score of **0.565**, worse than **0.466** for a constant prediction based on the training rate. Giving each training game equal weight produced **0.540**, still worse than its constant baseline of **0.296**. Lower is better; the test has only **95 labeled observations from two games**, and those games were already used during project development. These poor results are evidence against using either model as a gameplay value signal. Reports are `.local/dodge-survival-models/f813e78568bb437fbce7c2a786c4ac16/report.json` and `.local/dodge-survival-models/43ab3d4c111b40d787ca438b534e2ce1/report.json`.

Outcome training can also combine up to eight complete benchmark sets from different policy packs with `--also-benchmark <directory>`. It verifies matching seeds, timing, limits, and game/application/bridge fingerprints, deduplicates reused reports, and keeps every occurrence of a seed in the same split. On two policies and **245 held-out observations from four recorded games**, the unbalanced model's Brier score was **0.327** versus **0.248** for its constant baseline; the equal-game-weight model scored **0.368** versus **0.231**. Neither is suitable for control. These are not directly comparable to the earlier two-game figures because the evaluation data changed. The reports are `.local/dodge-survival-models/34380362711a44c18aa093603c96e102/report.json` and `.local/dodge-survival-models/eeb71782408645fcb44d69b83f164ee5/report.json`.

Using both policies from the repaired matched comparison produced another outcome model with **0.362** held-out Brier score versus **0.241** for its constant baseline, on **300 observations from four games**. It also remains unsuitable for control. Its report is `.local/dodge-survival-models/f4bb0090f910418dbc522fb9beb99531/report.json`.

Adding the nine-action policy to that outcome dataset produced a held-out Brier score of **0.358** against a constant baseline of **0.252** on **235 observations from six games**. It also remains unsuitable for control; see `.local/dodge-survival-models/db39b7308ed74a1da68f53bcc4daf199/report.json`. Its test sample differs from the two-policy experiment, so the scores are not a direct comparison between models.

To train from a completed local benchmark, choose held-out seeds from that benchmark:

```powershell
python tools/train_dodge_imitation.py . <benchmark-directory> --test-seeds <seed> <seed>
```

For newer recordings that contain fresh hold-decision state, add `--include-holds` to train a nine-action model. Models without those records continue to use the eight-action format.

To evaluate the resulting model against a different completed benchmark containing recorded seeds that were not used to fit it:

```powershell
python tools/train_dodge_imitation.py . <other-benchmark-directory> --test-seeds <seed> <seed> --evaluate-model <model-directory>
```

For the observational outcome experiment, run `python tools/train_dodge_survival.py . <benchmark-directory> --test-seeds <seed> <seed>`. Add `--also-benchmark <directory>` to combine a compatible policy, and `--balance-games` to give each training game equal total weight. These options only analyze completed local evidence and do not start a game.

To run the offline experiments as one bounded, sequential study without an assistant in the loop:

```powershell
python tools/run_dodge_learning_study.py . <benchmark-directory> --test-seeds <seed> <seed> --include-holds --also-benchmark <other-benchmark-directory>
```

Omit `--include-holds` for older recordings without fresh hold labels, and omit or repeat `--also-benchmark` to choose compatible additional outcome data. The study runs movement imitation, optional hold imitation, and both ordinary and game-balanced outcome training, each with a 90-second limit. It writes a consolidated report under `.local/dodge-learning-studies/`, including explicit failures. It does not run games, choose a winning policy, install a model, or turn imitation accuracy into a gameplay claim. The first complete study is `.local/dodge-learning-studies/1e092a1b6db741ae8d1acd18fcc1e072/`.

Create a draft pack with `tools/build_dodge_learned_pack.py`; the model's training report must match the model bytes and source teacher pack. The training and pack-building tools never start games. A model pack must first be exercised in bounded, isolated, slow games and compared with the baseline across complete varied runs. Any reported mean of 50 must come from those actual complete games and a fresh held-out set, not from imitation accuracy. Consistent success at the slow stage is required before real-time or desktop testing.

Further research needs broader and better demonstrations, uncertainty and out-of-distribution handling, a learned value or reward signal that can improve rather than merely copy the teacher, and carefully separated development and final evaluation. Offline data alone cannot reveal the outcomes of actions that were never taken; those hypotheses require controlled additional experience. The current training workflow is a developer tool, not yet a finished in-app training screen.

## Outcome-learning loop and varied experience

The outcome trainer supports `--contextual --include-holds`, adding action-relative features and time-aligned, fresh hold observations. The older additive model could not change its ranking of actions based on the scene; contextual features remove that specific limitation. Several compatible complete benchmark sets may have different seed lists, and all instances of a held-out seed stay out of fitting. The environment fingerprints, timing, complete-game evidence and resource caps still apply.

The private-only `directional-explore-v35` pack follows the directional controller most of the time and chooses uniformly among boundary-allowed actions on 20% of eligible observations. Every choice records whether it came from exploration, its unconditional selection probability, and the eligible action count. An independently generated per-game random seed is recorded so the random sequence can be examined. These runs collect diverse experience; they are not a performance benchmark for the player. `--exploration-only` trains on recorded randomized choices with valid metadata, while rejecting ambiguous timestamps and preserving complete-game evidence. This reduces a source of selection bias, but an observed five-second outcome still depends on later actions and is not proof of the counterfactual outcome for a different action.

The labeler also retains a terminal input as a negative example when its before-frame aligns to an alive audit and its after-frame aligns to a recorded game over. In that case movement may not have been observable; the example means the input was issued immediately before death, not that the input's movement or causal responsibility was verified.

The original clearance features assumed 190 pixels per simulated second. In a completed exploratory run, 165 observed movements had a median of **398.7 pixels per simulated second**, consistent with the pack's 400-pixel speed. The outcome trainer supports `--player-speed 400` and writes a distinct feature version; its private-test player validates the version and context. On the same two recorded development games, an exploration-trained model's held-out Brier score improved from **0.227** with the old features to **0.199** with the calibrated features, but remained worse than the **0.173** constant baseline. These are observational prediction results, not game performance. The corrected model is `.local/dodge-survival-models/449a27fa064b4c22b2428929de0fe78e/`.

Outcome experiments can choose a one-, two-, or five-second horizon using `--horizon-ms`. The shorter target reduces, but does not eliminate, the influence of later decisions. A one-second model trained on the randomized choices from 16 games had held-out Brier score **0.049** versus **0.073** for a constant reference, on **189 decisions from two games**. Only **15** held-out decisions were followed by death within the horizon, so this is a small correlated sample. Scores for different horizons have different targets and cannot be directly ranked against one another. This model remains unqualified until it is tested through complete games; its report is `.local/dodge-survival-models/ac451dc9b39b4dba80c07a41ea47350b/report.json`.

At a threshold of less than 50% predicted survival, that same model flagged only **1 of the 15** imminent deaths and raised no false danger flags. Brier score alone therefore hid poor detection of the minority failure cases. New outcome reports include death counts, flags and recall at that threshold; the threshold is a diagnostic and is not a gameplay safety guarantee.

As a ranking diagnostic, **13 of the 15** deaths occurred among the **38** held-out decisions with the lowest predicted survival (the lowest fifth). New reports include this measure as well. The two diagnostics use correlated decisions from just two games, and ranking observed actions does not establish which alternative would have survived.

New reports also include balanced Brier error, which averages the error on survival cases and the error on death cases instead of allowing the more common class to dominate. `--selection balanced-brier` lets a new curriculum select among its models using that held-out metric. It is a development heuristic, not evidence that a policy survives in the game, and requires both outcome classes in the held-out sample.

`--balance-outcomes` is an optional offline experiment that upweights the rarer outcome class and adjusts the intercept for the observed class prior. On the same development recordings it improved detection on training examples, but its held-out Brier score was **0.054** and it still flagged only **1 of 15** imminent deaths at the 50% threshold. It was not selected for gameplay. The prior adjustment is approximate and does not prove calibration; the report is `.local/dodge-survival-models/6efbd221c0214b029c2bb5ad2dfc8296/report.json`.

The first **32 completed exploration games** contain **5,399** eligible time-aligned one-second examples, of which **993** came from recorded randomized choices. These counts precede the trainer's per-game and total sampling caps; they are correlated decisions, not 5,399 independent games.

An experimental `directional-outcome-v34` pack turns the observational predictor into a private, slow-test candidate with the usual boundary and observation checks. This is deliberately unqualified: maximizing an observational predictor is extrapolation. The first candidate scored **1, 2, 1, 4** (mean **2.0**) on four development games, versus **24, 6, 17, 48** (mean **23.75**) for the matched directional controller. Neither reaches the goal. The paired report is `.local/dodge-outcome-comparison.json`.

The next test build adds a short visual clearance check when choosing among outcome-model actions. It filters predicted immediate collisions, accounts for room near screen edges, and falls back to the most open predicted direction if no option clears the threshold. This is a bounded heuristic, not a collision guarantee; its effect must be measured in new complete games. The earlier 2.0-score result used the older unguarded build.

The next private build also records the model's preference before the clearance filter, whether that filter changed the choice, the number of options meeting its clearance threshold, and the selected predicted clearance. These are diagnostic estimates from visual tracking, not proof that a move was safe.

A separate `directional-outcome-turn-v36` experiment uses the existing two-phase forecast only when no immediate option clears the guard. It responds to observed end-of-game traces in which the clearance-only fallback repeatedly moved toward a boundary. `--turn-aware` creates that distinct experimental pack; it does not replace the active v34 policy. The small completed comparison below is development evidence, not validation.

`--compare-fallbacks` builds both policies from the identical trained model, verifies their packs differ only in the fallback, and plays them on the same predeclared development openings before selecting the higher mean. It records a paired comparison and keeps a separate final set reserved. A selected development winner is not, by itself, a validated player.

In the first completed paired test, the standard fallback scored **9, 37, 7, 53** (mean **26.5**) and the turn-aware fallback scored **56, 37, 61, 15** (mean **42.25**), using identical model weights and opening seeds. The turn-aware option improved two games, tied one, and worsened one; neither met the target. This is a four-game development comparison, not final validation. Evidence is `.local/dodge-curricula/outcome-auto-348d20d3b1eb4404a7eba5253b5dbc6d/round-1/fallback-comparison.json`.

After collecting eight more games, the next turn-aware candidate scored **16, 32, 16, 7** on fresh development openings, averaging **17.75**; the baseline replay was skipped under the predeclared screening rule. The candidate's 40 experience seeds had no overlap with its development or reserved final seeds. More recorded games alone did not establish improvement. Evidence is `.local/dodge-curricula/outcome-auto-ddf24a7399554f8c86111dae13eb4f11/round-1/candidate/summary.json`.

In the first complete game using this diagnostic build, the experimental player scored **41**. Of **422 decisions with visible threats**, the clearance filter changed **107**; in **27** it found no option meeting the clearance threshold and used its fallback. This is one development game, not evidence of a successful average, nor a causal breakdown of the model's and guard's contributions. The report is `.local/wsl-dodge-runs/wsl-dodge-3e76b2152dc3448db8f2380dcfbf970c/report.json`.

The first four-game development set with the diagnostic build completed at **41, 14, 34, 91** (mean **45**, median **37.5**, minimum **14**; one game at or above 50). The optional matched baseline was skipped because the complete candidate mean was already below 50, so this is not a paired comparison. It falls short of the goal and leaves the reserved final set untouched. Evidence is `.local/dodge-curricula/outcome-auto-d7158a57b85f4083988a1174c2285e0b/round-1/candidate/summary.json`.

The first calibrated one-second outcome candidate with the clearance and edge checks completed four newly reserved development games at **8, 59, 6, 15** (mean **22**). It is still below the 50-point goal; the optional paired baseline was skipped under the predeclared screening rule. Different seeds and policy changes prevent attributing the difference from earlier candidates to one change. Evidence is `.local/dodge-curricula/outcome-auto-f176d986313842a6ac7d1b3741f375fc/round-1/candidate/summary.json`.

In the second automated development round, the unguarded outcome policy scored **8, 4, 2, 2** (mean **4.0**). The matched directional baseline scored **34, 56, 63, 130** (mean **70.75**, median **59.5**, minimum **34**). This four-game development result meets the mean target for the baseline, not the outcome policy, and does not establish repeatability. The paired evidence is `.local/dodge-curricula/outcome-auto-210f9b41f43b48989bc9a13a71eaa880/round-2/comparison.json`; a separate ten-seed validation is required for the baseline.

The unchanged directional baseline subsequently completed ten freshly reserved validation games at **4, 35, 6, 7, 64, 19, 90, 19, 122, 38**: mean **40.4**, median **27**, minimum **4**, with **3 of 10** at or above 50. It failed the requested target and does not justify increasing speed. The complete evidence is `.local/dodge-benchmarks/18ea6abc1a5245b38e33a3031c2bd372/summary.json`. One of these losses was inspected while developing the separate outcome candidate's edge guard, so these seeds are exposed for that candidate even though the baseline stayed unchanged during validation.

The ten baseline-validation seeds were also checked against the seed fields in **498 other available local run reports**; no collisions were found. This check covers the available reports, not undocumented play outside the workspace.

`tools/run_dodge_outcome_curriculum.py` provides a bounded, sequential collect/train/compare workflow. It reserves separate experience, development and ten-game final seed sets before training; it fits outcome models offline, tests an experimental candidate against the baseline, and touches the final set only if the four-game development mean is at least 50 and the matched baseline completes. It compares a model fitted on all decisions with one fitted only on random exploration; both are scored on the exact same held-out decisions, verified by hash. Training is capped at 5,000 observations, held-out evaluation at 600, and input-audit bytes at 64 MB. It records failed/incomplete stages and installs nothing. A final ten-game mean of 50 is evidence for this slow private environment, not a guarantee of real-time or arbitrary-game performance. A local `STOP` file in the printed curriculum directory stops it between games. The 45–300 minute budget and maximum of seven rounds are explicit; at most one private game runs at a time. The full bounded runtime for all ten final games must fit before final testing starts.

The curriculum takes the workspace, test executable, exploration pack and directional baseline pack, plus `--minutes` and `--rounds`. `--initial <completed-benchmark-directory>` reuses completed, verified exploration evidence in the first round. Each later round collects a new batch. Run `python tools/run_dodge_outcome_curriculum.py --help` for the arguments. The initial benchmark and later collections are still validated by the outcome trainer, including seed separation, complete-game evidence and matching environment fingerprints.

The newer curriculum also checks a configurable evidence disk budget before each game (2 GiB by default) and stops when less than 2 GiB remains free. It does not delete existing recordings. `--player-speed 400` selects the calibrated model feature version and requires a compatible private test executable.

When starting a subsequent curriculum, `--prior <completed-benchmark-directory>` can be repeated to include earlier compatible experience in training. The newest seven prior sets are used along with the current set; older evidence remains on disk. Different game, application or bridge fingerprints are rejected rather than silently combined.

`--experience-app <collector-executable>` optionally keeps collection on a fixed, compatible build while candidate and baseline games both use the comparison executable. Each set records its own application fingerprint; outcome training still refuses to combine collector sets with mismatched environments, and candidate comparisons require matching fingerprints.

For a cheaper screening round, `--skip-baseline-below-target` skips the matched baseline only after every candidate development game is complete and its mean is already below 50. That saves additional games but means that round has no paired baseline comparison. A candidate at or above 50 still requires a matched baseline before the final set can be used.

If a qualified outcome curriculum reaches its time limit before the reserved final set starts, `tools/resume_dodge_outcome_validation.py` can resume that untouched set with a fresh 210–300 minute budget. It rechecks the development games, application and game fingerprints, model and report hashes, seed separation, and holdout registry before starting. It refuses to rerun a final set that has already started or whose evidence changed.

The first failure-balanced candidate flagged 12 of 16 imminent losses in two held-out recorded games, but scored **5, 3, 10, 7** in four fresh development games (mean **6.25**). The held-out decisions are correlated within games; prediction accuracy alone does not validate action choices. The completed games are in `.local/dodge-curricula/outcome-auto-212b27d36c5f4a5c8e2e11dd7fef7fd3/round-1/candidate/summary.json`.

After eight further complete experience games, the next failure-balanced model scored **29, 117, 18, 10** (mean **43.5**, median **23.5**) on four new development games. Three of four scores were below 50. This round skipped the matched baseline under its predeclared screening rule and did not reach final validation. The results are in the same curriculum's `round-2/candidate/summary.json`.

`--retain-reference` creates a separate `directional-outcome-reference-v37` research candidate. It retains the existing directional move whenever that move clears the same short-term visual estimate, and considers the learned preference otherwise; when no option clears it, the existing two-phase fallback applies. The decision log records whether the reference was retained. Clearance is only an estimate, and this candidate is restricted to private 25% stepped endurance tests. The curriculum can compare it with the directional baseline on identical fresh seeds, with final games still reserved. It has not been established to improve gameplay; only completed paired games and a separate validation can do that.

The first v37 development games scored **46, 11, 8, 169** (mean **58.5**, median **28.5**, minimum **8**). The matched directional baseline scored **14, 31, 8, 210** (mean **65.75**, median **22.5**). The hybrid won one pair, lost two and tied one; both means were driven by the same long opening, and three games for each policy were below 50. Across 2,663 hybrid threat-visible decisions the reference was retained 2,514 times, the learned model selected a bounded choice 70 times after the reference failed the clearance check, and the two-phase fallback handled 79 cases with no option clearing it. These are decision counts, not proof of causality or safety. Evidence is `.local/dodge-curricula/outcome-auto-f9ef3900263e484a938a0a0ed372b917/round-1/comparison.json`. Independent final validation is needed; this development comparison does not demonstrate reliable improvement.

`python tools/analyze_dodge_distribution.py <completed-benchmark-directory>` rechecks the complete benchmark and reports its scores, median, minimum, sample spread and a reproducible bootstrap interval for the mean. The four-game development set above yields an interval of approximately **9.5–129.5**, illustrating how little four uneven games establish. The interval assumes independent, representative games and is not a guarantee, particularly for such a small sample; incomplete benchmarks are rejected.

New curriculum runs also save these distributions with completed candidate, baseline and final results. A preliminary mean of 50 still only opens the separately reserved final evaluation; it is not itself a reliability claim.

The first independent ten-game v37 validation scored **37, 168, 2, 29, 6, 31, 140, 44, 23, 18** (mean **49.8**, median **30**, minimum **2**, two games at or above 50). Its bootstrap interval for the mean is **20.6–87.3** under the stated assumptions. It fell short of the requested mean and does not demonstrate reliable play. Evidence is `.local/dodge-curricula/outcome-auto-f9ef3900263e484a938a0a0ed372b917/round-1/final/summary.json`. These validation openings are now exposed and must not be reused to develop or validate a later policy.

After validation, offline audits of two earlier **development** failures found that all 92 requested movement pairs moved at least five pixels in the requested direction. Approximate proximity matching found 347 of 350 and 257 of 265 visible enemy samples, with 0 of 77 and 2 of 53 nearby samples unmatched, respectively; some nearby velocity estimates still differed by more than 100 pixels per second. These comparisons use offline native audit data solely for diagnostics, not policy input, and cannot prove the cause of either collision.

The separate `directional-outcome-escape-v38` candidate tests an earlier response when three or fewer actions meet the immediate visual clearance check. It considers the existing two-phase forecast and changes the chosen move only if the forecast's first move also meets that check. A regression test exercises the behavior on saved **development** observations; it does not establish gameplay improvement. `tools/run_dodge_fallback_study.py` can compare v37 and v38 with identical model weights on newly reserved matching openings. `--candidate-only` prevents revalidating the previous version when the new one fails to improve its development mean. Only private 25% stepped testing is supported.

The first v37/v38 paired development test scored **24, 24, 43, 4** for v37 (mean **23.75**) and **24, 24, 42, 4** for v38 (mean **23.5**). The candidate did not improve and was rejected without using its reserved final set. Evidence is `.local/dodge-curricula/fallback-847d19f19319499b96689840664c44b5/comparison.json`.

`python tools/analyze_dodge_action_support.py <workspace> <outcome-model-directory>` is a separate offline diagnostic for the randomized collection branch. It checks whole-game holdout separation, model and recording hashes, and the exact collector geometry and timing, then reports how often the recorded random action matched the model's unguarded preferred action. It uses inverse random-action probabilities for a short-term survival estimate and reports match and effective-sample counts. This is evidence about a single randomized action followed by the collector's later behavior—not the guarded player, complete-game performance, or a reliable counterfactual for an entire playthrough. Neighboring observations are correlated and a small match count is inconclusive. The diagnostic takes the same study lock and refuses to read audits while a private study is running; no audit state is supplied to the player.

After eight more randomized experience games, the next v37 candidate scored **30, 37, 14, 7** on fresh paired development openings (mean **22**). The directional baseline scored **53, 38, 9, 16** on those same openings (mean **29**). Both remain below the requested target; the matched evidence is `.local/dodge-curricula/outcome-auto-ee2195ae96df45ac9bf458cb15606cca/round-1/comparison.json`.

For that model, the action-support diagnostic found **31 sampled randomized observations across two held-out games, with only three matching the model's raw preferred move**. All three had observed one-second survival, but the support is too small and correlated to establish a useful action advantage. The curriculum now accepts `--experience-games` (4–32) and `--heldout-games` (at least two and fewer than the experience games), reserving the requested distinct seeds before collection. This permits larger whole-game model holdouts without changing the ten-game final validation or letting the model see evaluation games during fitting.

The outcome trainer also reports a per-game balanced prediction error: it gives each held-out game equal weight and, within each game, weights survival and loss cases equally. `--selection game-balanced-brier` can select using this error only when every held-out game has examples of both outcomes; otherwise no model is selected under that criterion. This is still an observational prediction metric and must not be interpreted as the quality of the model's unobserved action choices. Existing runs using `--selection balanced-brier` retain their original selection rule.

When the trainer caps a long game's labeled observations, its chronological sampling now preserves both endpoints (or the final decision if the limit is one). Previously the last labeled decisions could be dropped entirely, hiding short-term losses. Sampling still limits data and does not guarantee that every game contains both classes. New models record the trainer's hash, so this sampling change remains distinguishable from earlier training runs.

`--compare-balanced-training` adds two bounded CPU-only profiles that reweight observed survival and loss examples during fitting, then apply the trainer's approximate prior correction. They are scored on the same hashed held-out decisions as the other profiles. Reweighting can change calibration and does not establish better action choices; a selected candidate still needs fresh complete-game evaluation.

`tools/train_dodge_action_advantage.py` adds an experimental action-contrast trainer. It uses randomized recorded choices and centers each action's features against the actions available in the same visual state before fitting a small regularized linear model. The resulting scores rank choices; they are not probabilities. This is a one-action observational experiment under the collector's later behavior, not long-term reinforcement learning or proof of causal gameplay improvement. Training is CPU-only, bounded to the existing local evidence limits, and uses the study lock. It emits a draft pack-compatible model with held-out action-support counts; the guarded player must still be compared on fresh complete games. `tools/run_dodge_fallback_study.py --compare-models --candidate-only` can compare a calibrated survival model and action-contrast model under otherwise identical reference-guarded rules.

The curriculum's `--action-advantage --retain-reference --player-speed 400` mode can run that trainer automatically between private games, while the curriculum owns the study lock. It records the held-out action-support diagnostic and applies the usual fresh-game screening and final-validation gates. It does not rank this model against survival predictors using a probability error, since its action scores are not probabilities.

New private-test builds record wall time spent waiting for an observation, performing visual/decision work and waiting for the action response, plus whether the observation was reused. `tools/analyze_dodge_timing.py` summarizes completed reports and distinguishes missing timing fields from zero. These measurements exclude hold-only decisions and later reobservations, and describe the private stepped bridge—not desktop capture or real-time latency. They are intended to guide a separately validated throughput experiment; the bridge's fixed waits have not been removed.

In the first fresh screening game after the crop-edge tracker fix, 169 action records had median observation wait **142.4 ms**, decision work **2.1 ms**, and action wait **326.1 ms**. The game ended at score 41. These are wall-clock measurements from one private slow-step game, not end-to-end reaction latency or a performance guarantee. The bridge is a stronger throughput target than the current decision calculation; changing its waits requires separate checks that key delivery, step acknowledgement, and rendered-frame freshness still hold. Evidence: `.local/wsl-dodge-runs/wsl-dodge-57e4eab0b4a44b7aa571426b36804c32/report.json`.

That initial tracker revision was stopped at a game boundary after three screening scores (**41, 51, 96**) when a synthetic regression showed it could overlook a genuinely clipped second eye. Across those three games and 853 action records, the median waits were **159.2 ms** for observation and **329.5 ms** for action, versus **2.2 ms** of decision work; their 95th-percentile values were **208.9 ms**, **399.8 ms**, and **4.2 ms** respectively. The same private-bridge limitations apply. Those results do not qualify the later tracker revision. A separate study using the strengthened tracker, new screening and validation seeds, one private game at a time, a 300-minute limit and a 2 GiB new-evidence limit was started at `.local/dodge-curricula/outcome-auto-44e5de6cb23a4d00ade014a5ada8e844/`. Its status and completed results must be read from its `curriculum.json` and benchmark summaries; starting the study is not a success claim.

The new study fits a five-second action-contrast signal from compatible existing collector recordings. Its held-out diagnostic sampled 246 random decisions across eight games; 32 decisions matched the model's raw choice across seven games. Observed five-second survival was **68.75%** among matched choices versus **69.51%** among the sampled random choices overall. The sample is small and correlated, the collector's subsequent actions affect the outcome, and these are not full-player or causal comparisons. The study's full-game screening and fresh validation evaluate the guarded player, not the model in isolation.

The strengthened tracker's four fresh screening games completed at **214, 63, 30, 11** (mean **79.5**), with substantial spread and two early losses. The matched baseline scored **102, 24, 17, 28** (mean **42.75**); the candidate won three paired openings and lost one. In the 214-point game, the reference rule was retained for **2,195 of 2,334** decisions with visible threats; the model selected 58 moves after the reference was rejected, and fallback handled 81. Those counts show substantial rule reliance, not causal proof of any component's contribution. Evidence: `.local/dodge-curricula/outcome-auto-44e5de6cb23a4d00ade014a5ada8e844/round-1/comparison.json` and `.local/wsl-dodge-runs/wsl-dodge-9ea14c4abbf849558ec41da78957e1a1/report.json`.

The separate ten-game validation finished at **24, 130, 34, 32, 28, 27, 8, 14, 35, 218**: mean **55**, median **30**, minimum **8**, and **2 of 10** at or above 50. The descriptive 95% bootstrap interval for the mean is **23.2–101.2**, assuming independent representative games. All ten reached game-over without a tracking stop, but this small, highly variable set does not establish reliable survival; the two long games carry the average. `final_target_met` in the curriculum status denotes only that this set's arithmetic mean met its threshold, not qualification or promotion. Evidence is `.local/dodge-curricula/outcome-auto-44e5de6cb23a4d00ade014a5ada8e844/round-1/final/summary.json`. These seeds are reserved and must not be used to tune the next candidate.

A further bounded study started at `.local/dodge-curricula/outcome-auto-a004f089c2d34bd7a87b5df5f6541fc1/` to collect 16 fresh randomized experience games with the repaired tracker before fitting a new action-contrast model. Eight whole games are held out from fitting. It uses at most one private game at a time, a 300-minute budget and a 4 GiB new-evidence budget; the current result is in its `curriculum.json`. It remains a short-horizon observational learning experiment, not full-episode reinforcement learning.

`tools/dodge_episode_transitions.py` is a separate, experimental in-memory
building block for full-episode reward learning. It constructs visual-feature,
action, next-observation and elapsed-time rows; survival earns a time-scaled
positive reward, and an audit-bracketed game-over receives one configurable
negative terminal penalty. Missing observations, incomplete runs and unobserved
gaps are not silently converted into deaths or bridged. It verifies decision and
action receipts match and uses audit data only for alive/dead timestamps, never
as visual features. The caller is responsible for validating provenance, whole
game splits and bounded file inputs first. Synthetic tests cover reward signs,
terminal evidence, interruption, gaps and unmatched decisions. A completed
screening report produced 334 structurally valid transitions using **synthetic**
audit times derived from its recorded decisions and final frame; this checks the
report shape, not real audit alignment or outcome truth. The helper has now
also loaded the completed new experience set with its recorded game audits and
feeds the research trainer below. It can
calculate discounted returns along verified linked transitions; returns for
segments ending at an interruption or gap remain unknown rather than assuming
the game ended. These returns describe recorded behavior, not counterfactuals. It is
not an `.si` format, a qualified policy, or a claim of causal action values.

`tools/train_dodge_episode_advantage.py` fits a separate research model from
those complete returns. It uses logged randomized exploratory decisions,
centered action features and equal per-game weighting to fit 19 small linear
coefficients. An isolated toy test verifies that different recorded survival
returns can change its ranking, and tests reject censored-only data and invalid
randomization probabilities. The fit is observational: later behavior and
environmental variation affect the return. A draft-only encoding can now be
loaded by the private Dodge outcome-policy path and compared against a frozen
baseline; it is not an installed or qualified player. Do not treat its synthetic
result as proof of learning or a completed reinforcement-learning loop.

`tools/dodge_episode_dataset.py` bridges a completed private benchmark to those
episode transitions using the existing report and whole-game split verifier. It
takes the same exclusive study lock before reading audits, checks reports again,
limits audit sizes and records audit hashes at load. Existing audit files were
not sealed into older reports, so those hashes identify what was read but do not
prove the files were unchanged since capture. New source records a digest after
the private game, and both offline outcome and episode loaders check it when
present. Older reports with no digest field are marked unsealed; an explicitly
unavailable digest in a new report is rejected. The current
comparison runs an older build, and the new capture-time path still needs an
end-to-end game test. Synthetic tests check split separation, changed-report and
changed-audit rejection, and lock exclusion. The loader processed
the completed 16-game experience set as eight training and eight held-out games:
**1,143** and **1,711** visual transitions respectively. The episode model fit
**222** randomized training decisions. Its held-out diagnostic found **37** raw
model-choice matches across seven games among **284** randomized decisions;
the weighted mean clipped return was **10.78** for matches versus **11.27** for
all sampled random choices. These small, correlated observations do not prove
an advantage or causal effect. Model and evidence: `.local/dodge-episode-models/e8573571ed9c461e8005b7e72a11e3c1/`.

A separate bounded private comparison, `.local/dodge-curricula/fallback-8ce2fde595724131b502e26837d6cac9/`,
tests the episode model against the prior short-horizon model under identical
guarded policy and game settings. Its study status and reports are authoritative;
the comparison being launched does not imply that the episode model improves play.

The first action-contrast model used **1,816 randomized training observations**. Its eight-game held-out action diagnostic found **28 matching moves across seven games**, with observed weighted one-second survival of approximately **89.3%**, compared with **92.7%** across the sampled random moves. These samples are small and correlated; neither number estimates complete-game performance, and the diagnostic does not use the runtime guard. The model and report are in `.local/dodge-survival-models/50059a1d541c4309b9d157a7ffc12309/`.

The first automated action-contrast cycle fit a new model on **2,077 randomized observations**; its eight held-out games supplied 33 matches across four games. Its first four fresh development scores were **67, 61, 46, 55** (mean **57.25**, median **58**, minimum **46**). Three of the four exceeded 50. The matched directional baseline scored **124, 22, 63, 55** (mean **66**); the action candidate won one pair, lost two and tied one. In the action games the reference was retained on 2,340 of 2,495 threat-visible decisions, the model selected 80, and fallback handled 75. These four results do not establish reliability or attribute success to the model. Evidence is `.local/dodge-curricula/outcome-auto-ccdc113f21824c1f98784eb4779cb05b/round-1/comparison.json`; independent validation is needed.

That cycle's ten-game reserved validation stopped incomplete after six complete scores: **16, 26, 6, 93, 144, 13**. The seventh game stopped alive at score **59** with `player-not-recognized`; the last three did not run. The incomplete score is censored, not a completed game, and the predeclared ten-game mean is unavailable. The search crop cut a nearby dark enemy into small fragments, which were incorrectly treated as extra possible eye landmarks. The tracker now searches a wider bounded area, and unresolved clipped candidates retain ambiguity; recorded-frame and synthetic clipped-fragment and genuine-ambiguity regressions cover this case. Evidence is `.local/dodge-curricula/outcome-auto-ccdc113f21824c1f98784eb4779cb05b/round-1/final/summary.json` and the seventh game's `last-unrecognized.bmp`. The exposed validation set must not be reused to tune and then claim an independent validation; a modified tracker or policy needs a fresh reserved set.

In its first fresh paired development comparison, the action-contrast model scored **14, 65, 80, 84** and the survival model scored **52, 27, 80, 84**: both means were **60.75**, with one win apiece and two ties. The new model did not improve the mean and its reserved final set remained unused. Across 2,780 action-model decisions with visible threats, the reference was retained 2,625 times, the learned model selected a move 77 times after the reference failed the clearance check, and two-phase fallback handled 78 cases with no option clearing it. These counts are not causal attribution. The full comparison is `.local/dodge-curricula/fallback-6da761913ccb4650b14ea85070622893/comparison.json`.

The first 16-game experience batch supplied eight whole held-out games. All four unbalanced profiles had worse balanced prediction error than a neutral 0.5 probability on that held-out sample; the selected profile's error was **0.2724** versus **0.25** for the neutral predictor. Its subsequent independent development scores were **3, 80, 4, 28** (mean **28.75**). The optional matched baseline was skipped under the predeclared below-target rule. This is evidence that the predictor and player still need work, not a causal assessment of individual actions. Evidence is `.local/dodge-curricula/outcome-auto-10c31525a57342efb66bbf81df0ca5cc/round-1/candidate/summary.json`.

The action-support check for that model found **37 matching random moves across seven of eight held-out games**, from 286 sampled randomized observations. Its weighted observed one-second survival was approximately **91.7%**, versus **92.7%** for sampled randomized moves overall. This small, correlated sample does not establish an action advantage or evaluate the guarded player's full-game performance. The reproducible diagnostic output is `.local/dodge-survival-models/8dbfbe19e4d7475fae9762c967cf50b8/action-support.json`.

The two loss-balanced profiles had higher per-game balanced prediction errors (**0.4215** and **0.3624**) than the best unbalanced profile (**0.2669**) on the same eight held-out games. The selected unbalanced weights were byte-identical to the previous candidate. A second fresh development set for those weights scored **34, 82, 27, 12** (mean **38.75**), again below target. Evidence is `.local/dodge-curricula/outcome-auto-88bafe8516c545aa8a789abd5043b2dd/round-1/candidate/summary.json`. This does not rule out other training methods.
