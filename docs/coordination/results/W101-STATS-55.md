# W101-STATS-55 — character-sheet evidence on restarted Death wizard

## Scope and result

The owner clarified that they were away and not playing. Two bounded existing `character-verify` CLI operations used the shared scheduler/input owner, countdown, C to open/close the character sheet, selected-window capture, local Windows OCR, and automatic native Idle. No network manipulation, account change, spending, volume change, desktop-use session, installation or game-file modification.

Target: PID99680, start639301914346328534, HWND174457832; executable SHA256 d410a2463e7ff0e221d6d11ca10fa53de265abd17cacfba211f9eeca8ec8511a. Screenshot identifies Luke SkyWalker, level10 Necromancer. Previous Fire-wizard values were not reused.

## Implementation

The existing character verification now saves raw bounded Windows OCR with image hash, target identity, frame acquisition time and separate OCR completion time. Resource context uses the existing owned UI-reader overload. OCR failure is recorded without preventing normal sheet-close/Idle cleanup; cancellation still propagates. Capture timestamp validation rejects future, nonfinite and older-than-three-second frames.

An attempted expansion into character-panel internals encountered UnicodeDecodeError. It was removed rather than silently accepting partial trees. A regression test ensures resource collection does not traverse unqualified character-panel fields. Synthetic test names are not claims about actual client control names. The reviewed worker hash was updated after narrowing traversal.

## Evidence

- `.local/stats-55/initial.json`: restarted client observation completed, cleaned up, no inputs.
- `character-1.json`, operation82fa6ab8f6d8402f86387d35aad21f77: image/OCR saved; stats tree unavailable twice, no resource publication. Native Idle independently observed.
- `character-2.json`, operationb472463c84a3494eb20f502ceb2d1f47: two complete trees, no tree failures, cleanup and hook restoration confirmed. Sheet closed and native Idle independently observed.
- `reference-2.json` and `comparison-2.json`: manual annotation of the saved image, not invented OCR confidence. Eight numeric agreements, zero disagreements/unknowns; target/image hashes and two-second source alignment checked. Raw OCR supports level, mana, energy and split gold text but misses health/XP. OCR therefore remains supporting candidate evidence.

Character-sheet and reader agree: health1022/1022, mana76/76, energy55/55, gold1136, level10. Reader also publishes potions1/1 through the existing resource registry. Visible character XP1001/1665, trainingpoints0 and gold capacity300000 are useful candidate references, not newly qualified memory fields. The baseGoldPouch value happens to match capacity; bonuses require separate semantic verification.

The CLI receipt remains Unknown because automatic post-load wizard-profile identity qualification is unfinished; `entry-result.json` independently records idleVerified=true. This is not a claim that character switching or profile activation was qualified.

## Remaining gates and next priorities

Resource pairs already use the shared registry/card transform; no new gold/level card aliases were enabled by a single comparison. Add wizard subject/generation binding before using character-specific verification across same-process switches. Level is mutable metadata, never identity. Keep executable-layout qualification separate from stored wizard values.

Next client sources: bounded school-specific modifier vectors (19 mapped headers, payload/order unqualified); inventory usage/items; current character XP/training points; active quest/goal IDs; pet/snack and gardening detail. Existing quest text, loading, position and travel readers should be reused. No blanket memory dump or network work is needed. Imported legacy offsets remain candidates until current-build proof exists.

Validation:2,602 native tests with warnings-as-errors;73 observer and14 profile-comparison tests; scoped native whitespace/analyzer verification; Python Black and compileall passed. Restricted Python runner stalled and was stopped; bounded elevated rerun passed. No observer/controller is left running.
