# W101-COMBAT-API-MAP-01 — bounded battle observation (NO COMBAT OBSERVED)

Status: **ONE CLEAN READ-ONLY PASS, NO COMBAT DATA.** Not a fact catalog; no qualification of combat fields.
Date: 2026-11-16 · owner: root · claim: `W101-COMBAT-API-MAP-01` · workstream: `W101-COMBAT`

## 1. What was authorized and run

Owner authorized read-only capture during their battle/quest session. One bounded `ui-tree-watch` was run against the exact pinned target (handle `642779728`, PID `44468`, ticks `639304802391820609`, image `d410a246…`), with all four optional context reads enabled: `--include-combat-context --include-resource-context --include-pose-context --include-quest-context`, duration 45 s, fresh output path and stop-file.

- Exit **0**, receipt `b3640e76d79c45df834bba799ee82255`, state **Completed**
- Message: *"Observed 2 UI trees and 5 loading samples; root hook restored and disconnected. Candidate states only; no game inputs sent."*
- `ui-result.json`: `Status: observed`, `Code: bounded-ui-snapshot-finished`, `Loading: false`, `States: {interactionPrompt:false, dialogue:false, menu:false, nativeIdle:true}`, `CleanupConfirmed: true`, `HookRestored: true`, `TreeSamples: 2`, `TreeFailures: 0`, `DurationMs: 4468`
- Guard after: marker absent, `game-input.lock = 0`
- Artifacts: `ui-request.json`, `ui-result.json`, `ui-observations.jsonl`, `combat-readiness.json` under `%LOCALAPPDATA%\Maelstrom\operations\b3640e76d79c45df834bba799ee82255\`; run output `.local/agent-runs/combat-map-20261116-231325/`

Only 2 tree samples in 45 s — the session was dominated by loading/transition states, consistent with questing between encounters.

## 2. Combat readiness result (the decisive finding)

`combat-readiness.json` (verbatim):

```json
{
  "SchemaVersion": 1,
  "Ready": false,
  "CardCandidates": 0,
  "ParticipantCandidates": 0,
  "RejectedCandidates": 0,
  "Missing": ["fresh-combat-observation"],
  "Note": "No current, restored combat observation is available."
}
```

**No combat was observed.** This pass cannot name a single card, participant, pip count or combat fact. The observer behaved correctly: when no current restored combat observation exists, it reports `Ready: false` with an explicit missing-evidence reason rather than inventing candidate fields.

## 3. What this proves and what it does not

Proves: the all-context read path runs cleanly with verified hook restoration and no input; the combat-readiness gate fails closed when there is no combat; the owner's play session was not disturbed.

Does not prove: any combat fact, card identity, geometry, pip/castability reading, or duel structure. The "readable combat API" remains **unwritten** — this pass is a negative-evidence record, not the catalog.

## 4. Next gate

A future capture must be started **while a battle is actually in progress**, with the same exact target, opt-ins and bounded duration. The owner has indicated they can signal between battles/navigations; a short 20–30 s pass triggered on that signal should produce restored combat trees. That requires fresh owner scope and a new/successor claim; nothing in this record authorizes another live pass.

## Correction 2026-11-17

This report implied the read path required the game to be foreground. That was wrong: `GameWindows.IsCurrent` checks process identity (`GameWindows.cs:45`), not focus, and a sampled 12-tree pass with zero W101-foreground samples succeeded (receipt `d8c040db…`, see `W101-BG-QUAL-LIVE-01.md`). The "no combat observed" finding stands; the foreground explanation does not.

## 5. Limits

No output was fabricated to fill the catalog. No hyperlinks, hashes or counts from this report were copied from memory — all values are quoted from the receipt and artifacts above. No game input, focus change or cleanup action was performed.
