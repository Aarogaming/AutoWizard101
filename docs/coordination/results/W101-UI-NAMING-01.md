# W101-UI-NAMING-01 — candidate UI node catalog from a 25-tree read-only pass

Status: **candidate names only — no geometry, no qualification, no combat facts.**
Date: 2026-11-16 · owner: root · claim: `W101-UI-NAMING-01` · workstream: `W101-COMBAT`

## Correction log

An earlier draft of §4 interpreted the hidden `PlanningPhase`, `DiegoMeter` and `DuelTimerWindow` nodes as evidence that the owner was in Dueling Diego. **The owner corrected this: they were not in that minigame.** Those nodes were `visible:false` in all 25 samples; they are dormant entries in the client's global layout and prove nothing about current activity. The interpretation has been removed. This was the same candidate→conclusion promotion the repo forbids — structural nodes are not activity evidence.

## 1. Provenance

Owner-authorized read-only pass fired automatically by a local foreground waiter when W101 gained focus. Exact target (handle `642779728`, PID `44468`, ticks `639304802391820609`, image `d410a246…`), `ui-tree-watch` with `--include-combat-context --include-pose-context --include-quest-context`, **no resource context**, 25 s duration.

- Receipt `cc76a2990cc447f8978bb9266d461ccb`, exit 0, `Completed`, 25 trees / 100 loading samples / 0 failures, `CleanupConfirmed: true`, `HookRestored: true`, `DurationMs: 27327`, no game input.
- Artifacts: `.local/agent-runs/auto-combat-nors-20261116-232238/`, durable `ui-observations.jsonl` under `%LOCALAPPDATA%\Maelstrom\operations\cc76a2990cc447f8978bb9266d461ccb\`.
- Zone candidate: `Khrysalis/KR_Z10_KondhaDesert`, `layoutId: archived-wizwalker-client-zone`, `status: candidate`, `liveQualified: false`, `ownerBinding: registry-owner-endpoints-matched`.

## 2. Operational finding (corrects my earlier diagnosis)

Every earlier all-context pass produced exactly 2 trees. I incorrectly attributed this to a C# hard cap (`W101UiTreeOperation.cs:274`); that line belongs to the **pose-stream read path**, not `ui-tree-watch`. The actual cause is `tools/w101_ui_tree.py:2019-2026`:

> if `stopAfterTwoTrees` **or `includeResourceContext`** and `treeSamples >= 2`: finish as `bounded-ui-snapshot-finished`.

Requesting resource context silently turned the watch into a 2-tree owned snapshot. **Dropping resource context yielded 25 trees in 25 s.** Future long observations must not combine resource context with extended tree sampling, or must add an explicit way to opt out of that snapshot behavior.

## 3. What was observed (94 distinct node names across 25 trees)

Persistent, visible controls (25/25 samples unless noted): `Root`, `WorldView`, `windowHUD`, `windowForTopRowButtons`, `windowForBtns`, `CompassWindow`, `ArrowWindow`, `ElementWindow`, `NotificationWindow`, `TipWindow`, `QuestHelperHud`, `XPBar`, `SocialSystemsContainer`, `TradeRequestWindow`, `WizardPatchingWindow`, `BallonLayer`, `CrownShopButtonsWindow`, `btnSpellbook`, `btnPotions`, `btnCompass`, `btnQuestHelper`, `spriteHealthBkg`, `spriteManaBkg`, `spriteEnergyBkg`, `wndElixirContainer`, `compassAndTeleporterButtons`, `GoHomeButton`, `GotoDormButton`, `MarkButton` (28 appearances), `RecallButton`, `OpenCantripsButton`, `PetSystemButton`, `txtGoalName`.

Hidden / inactive in all samples: `PlanningPhase`, `DiegoMeter`, `DuelTimerWindow`, `AddToFavoriteButton`, `CantripButtonCooldownText`, `ClassEventGlow`, `CloseFishingButton`, `CloseGardening`, `CloseObjectPlacement`, `DecoratorBuddyButton`, `GroupGlow`, `GroupSystemButton`, `OpenCameraButton`, `OpenCastleRatingWindow`, `OpenFishingButton`, `OpenGardening`, `OpenItemIdentification(2)`, `OpenObjectPlacement`, `ResumeInstanceButton`, `ResumeInstanceGlow`, `SpectatorUIButtons`, `TeleportToStartButton`, `VisitAnotherCastleButton`, `txtTestRealmText`, `txtRecallTimer`.

Occasional: `txtGoHomeTimer` and `txtGotoDormTimer` visible in 3/25 samples; `[excluded]` privacy-filtered nodes 100 appearances (expected).

## 4. Interpretation and limits

- `PlanningPhase`, `DiegoMeter`, `DuelTimerWindow` were `visible:false` in **all 25 samples**. They are dormant nodes of the client's global layout; their presence says nothing about what the owner was doing. The owner explicitly confirmed they were **not** in Dueling Diego during this run.
- `combat-readiness.json` reported `Ready:false`, 0 card/participant candidates, `Missing:["fresh-combat-observation"]`. Separately, the owner reports having been in combat at the time. These two facts are not reconciled by this capture — either the pass missed the combat window, or the combat UI lives outside the tree this reader exposes. **This is an open gap, not evidence that combat did not occur.**
- Node names are **candidate structure from an archived layout reader**, not screen coordinates. There are no `RawRectangle` values in this report; the convo's rule stands that raw layout rectangles are not client-pixel click targets.
- No combat card, pip, castability, health/mana value, or alive/enemy state was named. `CombatContext` and `QuestContext` were `null` in the final result.
- No installed game files were written; no network; no input; no focus change by the agent (the waiter only observed foreground).

## 5. Next gate

A pass started while actual combat is active, with no resource context if extended tree sampling is wanted, is still required before any combat fact catalog can be written. Dueling Diego has its own dormant UI nodes; a separate, later task could name those when the minigame is actually open. Neither is authorized by this report.
