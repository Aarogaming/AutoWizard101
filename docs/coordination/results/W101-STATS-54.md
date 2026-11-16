# W101-STATS-54 — observed resource maxima, potion correlation and optional visual profile evidence

Bounded continuation of [STATS-53](W101-STATS-53.md). Exact executable d410a2463e7ff0e221d6d11ca10fa53de265abd17cacfba211f9eeca8ec8511a. Installation reads only; scoped existing observation hooks are optional. No game input, focus change, desktop control, volume change, install or network manipulation.

## Health maximum resolution

The legacy base-plus-bonus expression is no longer used to publish resource maxima. BaseHP changed between2867 and4920 with bonus2053 in prior probes; adding bonus can yield6973. This remains a transient representation/object-identity hypothesis, not a diagnosis of the game's health-display behavior or client staleness.

The current executable registers StatGlobe.GetCurrentAmount/GetMaxAmount as direct DWORD reads at object+0x3a0/+0x3a4. HealthGlobe, ManaGlobe and EnergyGlobe inherit this class. The existing bounded type decoder identifies each exact visible HUD object. The reader now observes the pair from that object and checks current against HUD text plus the existing stats correlation. Maxima therefore describe the displayed globe, independent of the changing stats-base formula. No game function is executed.

[Static layout proof](../../../research/w101-ui-resource-layout.json) preserves the local registration/getter evidence. Offsets remain pinned to one executable hash; they are not inferred for future updates. Probe1 withheld the pair because the new code incorrectly gated on the old internal current_layout profile name; the running d410 build is internally archived_profile. The corrected gate enables only the reviewed d410 layout. The control-type decoder was not widened.

## Potion qualification

The PotionButton rendering code uses float32 maximum at+0x440 and charge at+0x444. A traced UI update copies GameStats potionMax(+0xa8) and potionCharge(+0xac) into those UI fields. The optional reader observes the exact visible Root/WorldView/windowHUD/btnPotions object, validates its PotionButton class, reads eight bytes, and requires agreement with the independently sampled stats pair. Fractional charge is preserved. No rounding to usable full potions, consumption or spending is introduced.

## Live checks

- `.local/stats-54/probe-1.json`: current values matched; no globe maxima published; clean disconnect.
- `probe-2.json`: UI globes4920/4920 health,543/543 mana,33/106 energy; current text agreed; clean disconnect.
- `probe-3.json`: UI globes4920/4920,543/543,34/106. Native resource-facts.json contained nine current/max/percentage facts from the same shared transform used by cards. PotionButton4/4 independently matched stats4/4. No inputs; cleanup verified.

Final combined probe and test results are recorded below when complete.

## Registry/card behavior

Qualified health, mana, energy and potion pairs supply current, max and percent through the existing catalog, alias register, demand scanner and scheduler. Resource collection stays off by default (`enableResourceObservation` for API connect; `--include-resource-context` for CLI ui-tree-watch). Canonical facts are not generated from the76 candidate inventory. Gold, reference/scaled level, school and skill stats remain research-only until independently verified.

The native boundary rejects unrequested, stale/future, unknown/loading, unclean, unsupported-key and contradictory pair/current evidence. Projection rejects conflicting current-only and paired readings. Resource-facts.json is now a CLI evidence artifact with the same canonical values and aliases used by the deck host; generating it does not execute a card. Tests cover percentage conditions without legacy base/bonus arithmetic.

## Optional character-tab/OCR profile verification

`tools/w101_stats_profile.py` is an offline CLI comparing a saved ui-result.json with explicit numeric OCR/manual reference data. Its docstring specifies the schema. It supports named candidate properties, health/mana/energy globe current/maximum, and potions.current/maximum. References bind exact process/start/window/executable identity and use the screenshot/frame acquisition timestamp, not the later human-review time. Low-confidence/ambiguous, nonfinite, stale (>2seconds), duplicate, mismatched or missing evidence is unknown/rejected. Optional images are bounded and SHA256 checked. Reports preserve input hashes and never promote live permissions or facts automatically.

This component does not capture screenshots, perform OCR, open menus, or add an application settings screen. Existing capture/OCR can supply reference data later; actual character-tab evidence is still needed. All42 installed Root GUI definitions were reviewed; character tab selectors were found but no safe numeric inner-page paths for gold/skills/modifiers. Generic labels are not treated as the active player's stats.

Example: `python tools/w101_stats_profile.py --observation <ui-result.json> --reference <reference.json> --output <new-comparison.json>`.

## Remaining qualification and reliability work

The maximum source is resolved for the current UI build, but why the raw baseHP changes remains unproven. No claim of all equipment/buff/character conditions or future builds. Scalar candidates,19vectors and8unresolved properties remain separate. Main character accumulatedXP is still unlocated; XP bonus is not currentXP. No user panels were opened to accelerate qualification.

Hook arithmetic review found no direct stats-object write explaining the health anomaly. Existing patch lifecycle still lacks proven thread quiescence/atomic seven-byte patching; byte restoration is verified, not mathematical proof of race-free execution. That debt is recorded for a separately reviewed lifecycle task; no risky hook redesign occurred here.

## Final result

`.local/stats-54/probe-4.json` completed with two UI trees and five loading samples. All four UI-correlated pairs reached the native resource-facts.json projection: health4920/4920(100%), mana543/543(100%), energy34/106(32.0754716981%), potions4/4(100%). Twelve canonical facts and aliases were generated by the same transform used by cards. cleanupConfirmed/hookRestored were true. No live observer remains running.

Validation: 2,601 native tests with warnings-as-errors;72 observer tests,14 profile-comparison tests,3 resource-helper tests; scoped native whitespace/analyzers, Black, compileall and project-board validation. A test assertion analyzer warning was repaired and rerun before live checks. CLI help was updated, followed by a clean build. No installer/package was produced.

Remaining: independent character-tab/OCR evidence for currencies, levels/school/modifiers and skills; broader state/character qualification and vectors. The profile comparator is an offline backend tool, not an automatic OCR capture workflow or finished settings UI. Current resource pairs are qualified for the observed exact-build HUD path, not a claim that every one of the76 candidates is ready for cards.
