# W101-IDENTITY-56 — wizard ownership, gold/level and Idle failure handling

## Delivered scope

The optional resource scan now verifies that the sampled stats object belongs to the current wizard. It uses the existing serialized observation owner and stats hook; identity itself uses bounded read-only registry lookup, with no new hook, client input, network manipulation or game-file changes.

Exact executable: d410a2463e7ff0e221d6d11ca10fa53de265abd17cacfba211f9eeca8ec8511a. Other builds do not inherit these offsets.

`gold.current` (`Gold`, `Gold.Current`) and `wizard.level` (`Wizard.Level`) are DINT facts in the shared catalog. Cards request them through the existing optional resource scan; no second poller/model/service. API/MCP use the same host facts, aliases and scheduler; CLI emits the same projection in resource-facts.json. Disabled resource observation, unknown identity, changed ownership, stale data, loading or unconfirmed cleanup withhold them.

## Current-build evidence and safeguards

- ClientObject registers m_characterId at +0x1c0 as eight-byte opaque gid; legacy +0x1b8 is stale. Registration1416c05b1/helper14043a891.
- WizClientObject inherits ClientObject and registers m_gameStats at +0x230. Getter142078b00 reads precisely this pointer.
- Legacy ClientHook signature still matches, but its hardcoded replay instruction uses the wrong offset. It was NOT activated or copied into the runtime.
- The reviewed current-object lookup uses singleton RVA3762040, key at gameClient+213a8, registry sentinel37eba38/buckets37eba48/mask37eba60. The lookup key is distinct from characterId. Exact key equality and expected stats-pointer ownership are required.
- tools/w101_identity.py checks seven instruction signatures and image size0x3b69000. Each pass allows at most16 nodes,64 reads,2048 bytes and one second; it rechecks root/key/bucket/nodes/owner/ID. The worker performs two agreeing passes around the stats read. Observed one-node runs cost58 reads/596 bytes combined.
- Only a domain-separated SHA256 character identifier is published. It is an opaque identifier, not encryption. Raw addresses and character IDs are not emitted. The native subject binds this ID, school and process-start epoch; connect/disconnect also advance the host observation generation. Queued writes reject a changed/missing subject as well as a changed process target.
- Names are not inferred from IDs: the backend subject label is explicitly opaque. This does not rename or activate a saved wizard/house profile. Character-name/profile display mapping remains separate.
- Read-only checks cannot pin client object lifetime or exclude ABA changes. Short deadlines, repeated reads and unknown results bound this limitation; no refcount/lock writes are made.

Gold116 and referenceLevel324 are signed32 named properties from the existing read block. Their semantics were independently compared with the Death character sheet in STATS55; owner verification now removes the unresolved player/stats association. School328 is the existing uint32 field, decoded against imported school IDs and the observed Death sheet. Level is mutable and never part of wizard identity.

The offline visual comparator now optionally requires matching subject/profile/name/school/generation on both sources. Missing counterpart, wrong school, different generation and stale subject timing reject. Historical subjectless comparisons remain usable as non-promotable evidence. They do not grant live authority.

## Idle default

Complete W101 world routines should finish through verified native Idle, including recoverable failures. This is not an unconditional key sequence: cancellation, keyboard takeover, lost ownership or uncertain input cleanup must not be overridden to force Idle. Passive observations do not alter the player; nested primitives leave finalization to their parent. Character selection has no native Idle and is an explicit unavailable terminal context.

The existing shared finalizers already cover admitted modules, quest conversations, navigation/travel, home movement and Dance lifecycle paths. This change closes known character-world early-return gaps using the existing scope and input lease, a20-second linked budget, fresh world evidence and explicit input-release guards. Failure-finalization reports preserve the original failure and escalate unconfirmed cleanup. No second control scope or reacquired input baseline is introduced. Character identity matching now requires one unambiguous name and school across all seven schools; cached level differences no longer reject a level-up.

This does not certify every unfinished combat/minigame operation or automatically wrap arbitrary future routines. Future W101 encounter routines must call the shared finalizer at the complete encounter boundary, never between battle turns.

## Live evidence

`.local/stats-56/probe-1.json` and `probe-2.json`: two separate no-input CLI observations of restarted PID62076. Both completed with two trees, clean restoration/disconnect, nativeIdle=true and the same opaque wizard identity. Both projected14 facts: health1022/1022, mana76/76, energy55/55, potions1/1, their percentages, gold1136 and level10. School was Death. No constant running observer/controller remains.

Final built-binary probe `.local/stats-56/probe-final.json` completed cleanly with the same identity, nativeIdle=true and fresh DINT aliases Gold.Current=1136 and Wizard.Level=10. No inputs were needed because all three probes independently observed native Idle. Different-character switching was tested offline, not performed live in this task.

Validation:2,616 native tests;74 observer,13 identity-reader and20 profile-comparator Python tests. Scoped native whitespace/analyzers, Black, compileall and diff checks passed. A final Python formatting change refreshed the worker hash and54 affected native tests passed again. No installer has been produced or run in this task.

## Remaining useful data

Prioritize bounded school damage/resistance/accuracy vectors, effective gold capacity, inventory usage and item identities, main character XP/training points, pet/snack details and gardening state. Mapped vector headers and coincident scalar values are not yet verified gameplay facts. Keep build qualification, stable wizard identity, fresh values and saved profile preferences separate.

## Git checkpoint review

Current branch: maelstrom/rust-architecture-v1. Latest inspected commit a617cdf. The working tree had147 changed/untracked entries at the user's Git question, including accumulated earlier work and other contributors' changes. No reset, branch switch or push was performed. The owner subsequently authorized reviewed local checkpoints. Proposed review groups: native app/cards/recording plus dependent adapters; W101 field qualification; Rust parity/migration. These overlap and need dependency review before splitting. Generated builds, recordings and private local evidence remain excluded. Node-RED integration checkpoint9d6f596 passed9offline tests (3live opt-in tests skipped). The native/adapters/documentation checkpoint follows. Concurrent Rust commit65935fd appeared during review; active Rust migration deletions are excluded from this checkpoint.
