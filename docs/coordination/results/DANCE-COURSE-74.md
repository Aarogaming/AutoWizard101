# Dance course and contributor queues — 2026-11-14

Completed slice: current roadmap reconciled, Codex work doc created, Gemini queue preclaimed and delegated through LIA-0033. No additional cron created. Gemini can ACK71 then work, hand back for review, and continue72/73 sequentially. Source/core/build/live ownership stays with root. Handback is not acceptance.

## Evidence

- Installed native/Rust read-only readiness probe succeeded: `.local/dance-readiness-74-verification.json`, raw evidence `.local/dance-readiness-74-e287294008034bd49d0c1674aaf8aa15`.
- Current client stats1022/1022 health,76mana,55energy,1136gold,level10; Idle/menu/dialogue/prompt all false. Location and Dance proximity were not established.
- Hook restored, cleanup confirmed, facts expired/disconnected, host exit0. No keys, clicks, focus changes, capture, feeding or spending. Observation-only leaves state unchanged; no claim of returning to Idle.
- 69 focused PetDanceVisit/PetDanceLifecycle/PetDanceEntry tests passed on current source with warnings-as-errors. No source behavior changed in this slice.

## Concrete missing connection

`JobDeckRoutines` registers w101.pet.dance through Planned, with Executable=false. `PetDanceVisit` documents itself as offline-only, and current instantiation is in `PetDanceVisitReplay`; it does not dispatch live inputs. Existing visual/sound trial launchers retain a visible Start/Stop host. Completing a replay or a standalone perfect game therefore cannot qualify the whole deck routine.

The next root task is to adapt those existing observation/action/specialist receipts through the shared scheduler, with explicit entry energy, arena, no-feed and Idle constraints, rather than adding another controller. Gemini71 maps exact binding gaps;72 supplies independent contract tests;73 reviews legacy reuse. Route qualification remains separate from the at-sigil visit.

## Documentation authority

Current execution lane: `docs/coordination/CODEX_WORK.md`. Gemini assignments: `docs/coordination/gemini/QUEUE.md`. Roadmap front matter and board next tasks now point to this product milestone; older dated sections retain historical evidence. Existing shared deleted legacy Rust files remain untouched.
