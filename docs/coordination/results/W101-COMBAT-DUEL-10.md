# W101-COMBAT-DUEL-10 — Bounded manager/duel correlation

Date: 2026-11-11. Owner: `combat_reader`. Status: needs integration review;
offline implementation verified, current-client manager/layout **not qualified**.
The coordinator explicitly added the three files in this task after READ-08.

## Source finding

The installed development source at
`.local/envs/deimos/Lib/site-packages/wizwalker/memory/memory_objects/duel.py`
resolves `CurrentDuel` using a RIP-relative global manager and its duel map.
It does not require the archived DuelHook for this path. Its actual source
commit is `df2283f146748d74b2229d32c08233c20ce8bb3d` according to the local
distribution's `direct_url.json`.

However, upstream `read_base_address` retries indefinitely on selected read
failures and matches a participant against a client-object global ID;
`duel_phase` turns `MemoryReadError` into `ended`. Those behaviors were not
reused. The new module neither imports upstream code nor adds a ClientObject
hook. It matches all freshly observed rooted combatant owner IDs instead.

The coordinator owns signature discovery and current-image validation. Its
candidate must validate the actual `48 8B 0D` MOV prefix, RIP calculation and
mapped executable range; wildcard-pattern agreement alone is insufficient. This
module accepts only an already vetted manager address and injected async byte
reader. It has no process, scanner, memory-write, input or hook API.

## API and semantics

`await read_duel_context(read_bytes, manager_address, observed_owner_ids,
layout_id="deimos-development-df2283f")` reads a bounded candidate. The layout ID
is a required keyword; no historical/default-layout probing exists. Owner IDs
must be one through eight unique canonical decimal uint64 strings from the same
current root-bound participant observation. Missing identity cannot be filled by
an enemy name or screen position.

The result has `schemaVersion: 1`, `layoutId`,
`qualification: "source-layout-only"`, `metadataQualified: false`, `status`
(`matched` or `unknown`), a fixed-code `reason`, nullable `duelId`, `phaseCode`,
`phase`, `round`, `participantOwnerIds`, and byte/read counters. It emits no
addresses or external exception messages. Exactly one duel must contain **all**
observed owners. Ambiguous/no matches, malformed/changing state, empty map or
read failure remain unknown. No error fallback reports `ended` or victory.

Source phase enumeration: starting=0, pre-planning=1, planning=2,
pre-execution=3, execution=4, resolution=5, victory=6, ended=7. The source's
sentinel `max=10` is not a phase. Unknown enum values and out-of-range rounds are
rejected. `outcome` and `winnerTeamId` always remain null: even an observed
phase named victory does not establish that the selected player won. Combat
completion, survival, quest item drops and quest completion need independent
evidence. Source-consistent phase/round readings alone do not authorize input.

## Exact bounded layout

- `ClientDuelManager` exact PropertyClass type; map sentinel pointer at +8.
- Sentinel parent/root at +8 and is-leaf flag +25. One sentinel; traversed nodes
  have left +0/right +16, is-leaf +25, uint64 key +32 and duel pointer +40.
- `Duel` exact type; duel ID +72, shared participant vector +80/+88, round +192,
  phase +196. The map key must equal the read duel ID. This interpretation is
  a source-layout candidate and requires current-client evidence.
- `CombatParticipant` exact type; owner ID +112. Shared-vector entries are 16
  bytes; no participant fields beyond this identity are needed here.
- Maximum 16 map nodes/duels, eight participants per duel, 16 KiB read bytes,
  256 reads, shared two-second cooperative scan deadline, 250 ms per read.
- Cycles, duplicate keys/duel pointers/participants/owner IDs, invalid flags,
  invalid pointers, malformed/alignment/oversize vectors and unsupported types
  reject the observation. Stored map headers/nodes, participant vectors/IDs and
  duel identity/phase/round bytes are reread before publishing any match.

This is a consistency check, not an atomic process snapshot. The coordinator
must still invalidate on target change, loading, stale read time, incomplete
root sampling or failed cleanup. Its hard worker deadline handles an injected
reader that ignores cooperative cancellation.

## Evidence and checks

Local source SHA-256:

- duel.py: `4d2d5279e0290455c0adccb94077ccb37ec8fc54e072450d7304530ff36a571d`
- client_duel_manager.py: `1e48ef5075b0c5b727bf45319ae24bafe4c94e82fbbbc8b8d8bb8e09644a422e`
- memory_object.py: `78d21f4ef1117ae1bb2f0fd7e0bccb0d20e72a53b63b398364e7380b746dafb2`
- enums.py: `321d7ef6f7a3c9ae3e78e1a2ff5a541477767f766c277b746122e01bb784fd2c`

Fourteen synthetic tests pass: select the unique matching duel rather than the
first duel, all-owner requirement, ambiguous/no/empty matches, victory versus
player outcome, enum/round validation, malformed/cyclic/duplicate maps, changing
manager/phase, participant bounds/duplicate IDs, wrong type, budgets, sanitized
read errors, cancellation and invalid input without reads. A two-duel/five-owner
fixture uses under 4 KiB and under 100 reads. No current combat capture or memory
read was performed.

- `python -m unittest discover -s tools/tests -p test_w101_duel_reader.py` — 14 passed in 0.351 seconds.
- `python -m black --workers 1 tools/w101_duel_reader.py tools/tests/test_w101_duel_reader.py` — completed.
- `python -m black --check --workers 1 tools/w101_duel_reader.py tools/tests/test_w101_duel_reader.py` — passed.
- `python -m compileall -q tools/w101_duel_reader.py tools/tests/test_w101_duel_reader.py` — passed.

No separate Python lint tool was invoked. Module SHA-256 at freeze:
`632a826d1aee0757a01dbe442d61b3045eecc0f274d5a94650d6f0048c060165`.

Remaining: coordinator signature/worker/import/pin integration, exact live
manager/map/type/layout qualification, fresh root identity correlation and
phase/round comparison against observed gameplay. No live capability or release
qualification is claimed by these synthetic tests.

## Coordinator-assigned static executable review

After implementation freeze the coordinator explicitly assigned read-only
review of `C:/ProgramData/KingsIsle Entertainment/Wizard101/Bin/WizardGraphicalClient.exe`.
One bounded Python process read that file, verified PE32+ headers/sections,
calculated SHA-256, searched the maintained CurrentDuel signature and checked
RIP-relative mapping. It completed in approximately 0.53 seconds. No process was
opened, memory read, hook installed, input sent or installed file edited.

- File SHA-256: `544ab726ef7b920a5ccb67aa3d06b245db7e0012317d2fe7856d58c17c31cf7e`,
  identical to the previously reviewed executable identity.
- File size: 56,433,288 bytes; PE `SizeOfImage`: 57,729,024 bytes; six sections.
- Maintained full signature:
  `.......\xE8....\x90.......\x48\x85\xC9\x74.\x0F\x28\x45`
  has exactly one match mapped into an executable section.
- Match file offset: 14,473,298; instruction RVA: 14,476,370, in executable `.text`.
- Actual first seven instruction bytes: `48 8b 0d 47 63 55 02`.
  The first three bytes are the required MOV prefix, independently of the
  wildcard pattern. Signed displacement plus instruction RVA plus seven gives
  pointer-slot RVA 53,626,784.
- The entire eight-byte pointer slot is within `SizeOfImage` and mapped into
  readable/writable `.data`.

These are image-relative static facts, not a current manager value or game
observation. The runtime must remain bound to this exact reviewed image, verify
the instruction bytes and module mappings again, validate the pointer it reads,
and qualify actual manager/map/duel types and fields. The audit does not endorse
automatic signature adaptation to another executable build.
