# W101-COMBAT-CONNECT-06 — observation and combat integration checkpoint

Date: 2026-11-11. Owner: coordinator, with parallel INPUT-07, READ-08,
BATTLE-09, DUEL-10 and SIGNIN-01 components. Source changes, not a newly
installed release. This does **not** complete the Fire Elf quest or enable the
Quest card's battle execution.

## Connected source behavior

- Optional CLI combat observation now invokes the reviewed, bounded card and
  participant decoder through visible, exact-type controls reached from the
  existing root hook. No stored raw pointers or chat/account content are emitted.
- Typed candidates include current spell effects and full pip categories, using
  separate explicit source layouts instead of silently reusing archived offsets.
  Unsupported derived effects and inconsistent reads stay rejected.
- A read-only duel-manager binding uses the exact image's independently audited
  instruction and mapped slot. Unique owner-ID correlation yields source-only
  phase/round/encounter candidates. No new hook, endless retry or read-error-as-win.
- Native validation checks bounds, control bindings, identities and the explicit
  lack of live qualification. `combat-readiness.json` lists missing connections.
- Acquisition-start timestamps now survive both the final result and saved tree
  traces. Slow reads/disconnect cannot renew old evidence. Helper execution uses
  the exact bytes checked by its digest, and validation precedes opening a client.
- The addressed mouse dispatcher and bounded whole-battle operation reuse the
  current scheduler/input lease. Per-turn proposal IDs differ; global budgets,
  activity checks, release recovery, fresh re-planning and explicit outcomes remain.

The complete reader → qualified turn facts → planner → actual game input →
verified outcome route is **not yet live qualified or registered with Quest**.
Do not interpret an offline synthetic battle success as game progress. Gemini's
reserved OBS-02 bridge files were not present; their reservation is retained.

## Verification and the failed live setup

Evidence root: `.local/combat-connect-06-evidence/`.

- 111 focused native input/battle/turn cases passed.
- 223 integrated native combat/UI/sign-in/CLI cases passed.
- 43 Python UI observer tests passed; 15 combat-field and 14 duel-reader tests
  passed in the assigned source-only tasks. Black and syntax checks passed.
- The first restricted test runner could not connect to its test host and aborted;
  the bounded host retry passed. The failed runner receipt remains preserved.
- A first live attempt rejected an enlarged result cap **before the worker was
  constructed or launched**. The caller conservatively latched recovery. No hook
  or input was sent. Its failed receipt is preserved in `live-observation.json`.
- Repaired the worker's matching result bound and parameter diagnostics; added
  admission boundary tests. 43 relevant native tests passed. The current result
  cap is 256 KiB and is exercised by a maximum-field Unicode serialization case.
- Used the installed application's existing Stop → Check → Restore flow.
  The first computer-use app approval timed out; the user then approved access.
  The screen confirmed **Game controls restored**. Nothing resumed automatically.
  Closed the app afterward; no recovery file was manually removed or rewritten.
- Corrected three-second live observation completed: 3 UI trees, 12 loading
  samples, 0 tree failures, 4,016 ms total including setup/cleanup. Native Idle
  true; objective `Defeat Any Fire Elf and Collect Arrows in Firecat Alley (1 of 3)`.
  No visible combat hand/participants, no combat metadata reads, no game inputs.
  Root hook restored and disconnected. Report:
  `live-data/operations/c8abff825cf248d6953a6412b53b516a/ui-result.json`.
- Full native regression suite passed **2,047/2,047**, warnings-as-errors;
  `full-native.trx`. Scoped formatting/analyzers passed before the final small
  worker-bound/help changes; final checks are recorded by the coordinator.

## Sign-in request

The source Settings → Game sign-in dialog stores/removes a Wizard101 credential
in Windows Credential Manager for this Windows user. No `.env`, pack, API/MCP,
log or report receives a secret. Tests use synthetic buffers and do not touch the
real vault. Existing credentials are not filled into the settings form.

Automatic launcher submission is **not connected**. Credentials have not been
requested in chat, read from the clipboard, entered or saved by this session.
See [SIGNIN-01](W101-SIGNIN-01.md) and [the storage contract](../../W101_SIGN_IN.md).

## Remaining work in dependency order

1. Qualify the new fields against a bounded real battle, including dynamic effect
   variants, unique hand instances, self/enemy identity and selected-card meaning.
2. Qualify current client-pixel geometry and actual addressed-click response.
   A successful window message is not proof of a card selection.
3. Connect the qualified facts and planner to the turn/battle operation, then to
   generic Quest execution with fresh independent objective/count verification.
4. Qualify encounter approach, between-run native Idle and resource limits.
5. Connect explicit, single-attempt launcher login using the saved credential's
   tightly scoped lifetime, with no recording and no automatic retry loop.

No model/GPU workload, volume adjustment, purchases, snacks, game restart or
network manipulation occurred. Game files were inspected read-only for the
duel signature audit. W101 stayed in native Idle throughout.
