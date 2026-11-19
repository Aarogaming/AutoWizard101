# CORE-DECK-RELEASE-02 — Installed cards and parallel combat foundation

Date: 2026-11-11. Root coordinates builds, release and integration. The user
authorized packaging/installing the tested deck checkpoint and continuing combat
work with subagents alongside Gemini. No new dependencies or models were added.

## Installed deck checkpoint

- Portable: `.local/releases/Maelstrom-20261111-014821-9e8f8c.zip`.
- Installer: `.local/releases/Setup-20261111-014952-1a746b/MaelstromSetup.exe`.
- Installed version directory:
  `%LOCALAPPDATA%/Programs/Maelstrom/versions/08bcce2a90584742a8862c923865e174`.
- Source gate: 1,859 native tests and 24 Python bridge tests passed in the
  [prior source checkpoint](CORE-DECK-01.md). Installer tests: 14 passed.
- The first no-restore publish lacked a Windows runtime asset target. Restoring
  the existing dependency cache against an empty local feed resolved it without
  downloading or changing package versions. Failed output/logs were preserved.
- Published setup's isolated install/update/uninstall diagnostic passed every
  data-preservation sentinel before the actual update.
- The installer displayed successful completion and was closed. No game inputs,
  game activation, volume changes, purchases, models or capture jobs ran.
- Seven installed executable/bridge/document files matched the frozen payload's
  hashes. All 17,111 pre-existing data files (1,868,597,144 bytes) remained present
  with identical sizes and modification times; 1,080 small configuration/text
  files also retained identical hashes. Large recordings were not rehashed.
- The actual installed EXE and bundled Python completed one synthetic offline
  replay through MCP → CLI → named pipe → shared deck owner. Duplicate request
  suppression passed; initially unknown game facts stayed unknown. The host
  drained after its stop file. This is not a live-game or Fire Elf result.

Local evidence is under `.local/deck-release-20261111/`: installer tests/build
logs, `installer-lifecycle.json`, before/after installation receipts,
`installed-verification.json`, `headless-installed-app.json` and the independent
operation report. Private data inventories stay local.

## Parallel combat work

- [Planner repairs](W101-COMBAT-PLAN-03.md): reuse the existing pure planner;
  explicit input facts, power-pip arithmetic, supported effects and bounded
  proposals. These are proposed actions, not input or observed damage.
- [Owned turn loop](W101-COMBAT-LOOP-04.md): one turn over the existing scheduler,
  input owner and routine runner, fresh semantic control binding, selection and
  outcome checks. No production live runtime is registered yet.
- [Structural observer](W101-COMBAT-SOURCE-05.md): separately opted-in combat
  control candidates and raw layout rectangles using the existing reviewed
  root hook/header reads. Geometry, spell metadata, turn identity, target health
  and castability remain unqualified. No additional hooks are introduced.
- Gemini retains [W101-COMBAT-OBS-02](../tasks/W101-COMBAT-OBS-02.md). Its four
  bridge files were not present at the integration check; assignment is not
  evidence that an external agent has completed or tested them.

Combat changes began after the deck payload was frozen. Native and Python
integration checks and subsequent release status are recorded below when done.
The optional observer's source hash changes with this work; older pinned
executables must refuse that new worker until the matching build is used.

## Combined verification

- Full native suite: **1,905 passed, zero failed or skipped**, including 32
  synthetic combat-turn cases. Scoped formatting/analyzers and warnings-as-errors
  build passed. Report: `combat-integrated-final.trx` in the evidence directory.
- Python: **45 combat consumer tests, 39 UI observer tests and 10 MCP tests**
  passed. Black single-worker and syntax checks passed. No model was used.
- Peer review repaired stale-plan reuse before input, unsupported hybrid spell
  effects, strict seven-slot pip admission and explicit effect application order.
  Fresh target evidence must explicitly confirm that a selected combatant is alive.
- The UI result cap now allows the existing worker maximum of 128 KiB. A Unicode
  boundary regression covers a full control list plus quest text; the earlier
  32 KiB cap could reject a clean result and incorrectly require recovery.
- One actual three-second CLI UI observation collected **3 UI trees and 12 loading
  samples, zero tree failures**, confirmed root restoration/disconnect, and
  observed stable native Idle. No worker remained running. Combat planning/hand
  were absent and candidate controls empty: this is **not** combat recognition
  qualification. No game inputs, foreground changes or volume changes occurred.
  Local evidence: `combat-structure-live.json` and its linked operation files.
  Worker pin: `58fbee909a67145d400b848b037acde1db843013d2662384321d7cc2c3129b19`.

## Final matching installation

The combined, tested source was subsequently packaged and installed so the
optional reader pin matches this checkout. This supersedes the first installed
directory above; both evidence sets are retained.

- Portable: `.local/releases/Maelstrom-20261111-021041-84c650.zip`.
- Setup: `.local/releases/Setup-20261111-021218-8d09f3/MaelstromSetup.exe`.
- Final installed directory:
  `%LOCALAPPDATA%/Programs/Maelstrom/versions/7e16d7c03fd4462aa3f1797e01281536`.
- The embedded-payload lifecycle test passed again. Eight installed files,
  including the offline combat planner, matched this payload's hashes.
- The final installed EXE/bundled Python again completed one replay with exactly
  one attempt/completion, duplicate suppression and orderly host exit. A separate
  bundled planner check proposed the expected action for explicit synthetic facts
  and rejected missing facts. Neither check sent game inputs.
- Installed front/back card rendering completed without showing a window;
  `final/installed-card-settings.png` preserves the output.
- The final comparison preserved all 17,110 non-lock pre-existing files with the
  original sizes/timestamps and small-file hashes. The sole difference was the
  timestamp of `session-locks/game-input.lock`, written by the authorized live
  observer between installations. It retained one byte, ASCII `0`, with no
  recovery notice. The initial comparison failure and reviewed explanation are
  both preserved; no user data was removed or silently exempted.
- One installer click returned unavailable geometry; fresh observation showed
  the update had not started. One retry after refreshing succeeded. Setup was
  closed after confirmed completion. No task-owned app or worker remained.

Final installed evidence lives under `.local/deck-release-20261111/final/`.
The installed combat foundation is deliberately not an enabled live combat
adapter. No Fire Elf was fought and no quest progress is claimed.

## Remaining outcome gate

The Quest card cannot yet autonomously defeat the two user-reported Fire Elves.
We still need qualified live hand/spell/target/turn facts, an owned input binding,
independent round/victory and quest-count observations, and encounter navigation.
No artificial HP, pip counts, spell effects, target positions or quest progress
may be substituted to claim a working battle. UI candidates alone are not those
facts. The combat schema and owner must remain reusable for other encounters.
