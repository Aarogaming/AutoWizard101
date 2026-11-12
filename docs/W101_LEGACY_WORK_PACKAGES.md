# Legacy collection: implementation work plan

**2026-11-10 scope update:** [reviewed W101 client-side functionality](W101_CLIENT_SIDE_EXCEPTION_20261110.md)
is now authorized, including necessary hooks, injection and memory reads/writes;
network manipulation remains excluded. Deimos/wizwalker/wizSDK may be integrated
as actual backends and teachers. Their existing runtime blocks indicate pending
adapter/session qualification, not a permanent method prohibition. All 25 sources
retain their original evidence and acceptance tests.

Reviewed November 9, 2026. **All 25 supplied root archives are mapped exactly
once.** The existing recursive inventory has 88 compressed items because it also
includes releases, dependencies and prior downloads. No count mismatch was found.
This review used existing local inventories, source findings and current Maelstrom
bindings; it did not extract/run old applications or access the game installation.

The [structured work packages](../research/w101-legacy-work-packages.json) give
each project its source hash/copy, original qualification, languages, proposed
actions, exact existing binding paths, missing connections, dependencies and
acceptance tests. This is a **non-executable plan**, not another launcher or a
claim that the collection is already integrated.

## What to consolidate

Keep C# as the native application/runtime and Python for bounded offline imports,
teacher comparisons and experiments. Preserve the Rust, C, Go, Pascal and web
sources as references or independent test implementations where useful. Translating
every repository would repeat work without establishing better gameplay.

Consolidate **contracts and execution**, while retaining alternative algorithms:

| Shared function | Sources worth comparing | Common destination |
| --- | --- | --- |
| Visual matching, state and waits | Pet Dance, wizAPI, wizSDK, Farming, Bazaar, gardening, afk-wizard | Window-local observations with timestamps, confidence, unknown and timeout; one native input boundary |
| Conditions, routes and subgoals | Both Deimos revisions, Automatus, gardening, raid tools | Typed conditions/actions, bounded repeats, arrival/outcome checks; independent source/version identities |
| Combat decisions | Damage Calculator, Farming, wizAPI, Deimos, wizSDK, afk-wizard | Pure estimates plus verified hand/turn context; estimates remain distinct from observed damage |
| Asset/version parsing | wizwad, libkiwad, wizwalker, Aurorium, packet-map utilities | One bounded read-only import contract with multiple differential readers |
| Knowledge and observations | Utilities, WizWikiAPI, Gallery, On-Wiz, raid tools, GrubNinja | Local sourced claims, frame references, chronology and contradictions |
| Application UX | Historical Maelstrom, Gallery, Cheat Engine | Individual useful patterns inside the current application |

The public operation/action schema is the common interface; MCP is not required.
An implementation produces observations/proposals and receives controlled action
receipts. The existing scheduler owns admission, cancellation and evidence. Packs
remain declarative and cannot grant new execution capabilities.

## Current bindings, without overstating readiness

- The [shared scheduler and input owner](SHARED_OPERATIONS.md) exist. Extend them;
  do not rebuild them for each source. Native/Python ownership is already shared.
- `w101.pet-dance-review` is a recorded teacher operation. The bounded
  `w101.pet-dance-live-trial` exists, but independent live qualification and the
  complete user-facing workflow remain incomplete. Consult the
  [delivery tracker](PET_DANCE_DELIVERY_TRACKER_20261108.md), not the older catalog,
  for current replay/live evidence.
- W101's routine categories and motion primitives exist. A category named Farming
  or Quests does not mean its player is implemented. Branching conditions and
  verified outcome composition are still work.
- The [snack planner](PET_SNACK_PLANNER.md) is a pure suggestion function. Inventory
  recognition, settings persistence, consumption accounting and feeding dispatch
  are not supplied by that function.
- Knowledge schema, cache loading and the
  [installed-file inventory](W101_LOCAL_INSTALLATION_SOURCES.md) exist. They do not
  imply a populated, maintained knowledge database or a hardened payload importer.
- Historical managed Deimos/wizwalker/wizSDK launcher paths exist, but the newer
  [native interaction policy](../ProjectMaelstrom/ProjectMaelstrom/Utilities/W101InteractionPolicy.cs)
  and [Python policy](../tools/w101_interaction_policy.py) block their current
  client-internal execution. The older integration map's optional-teacher wording
  must not be read as permission to bypass those current gates.

**UI-only remains the general default.** W101's expanded exception permits
reviewed client-side functionality as an optional mode, with visual operation
retained. Process reads, hooks, writes, installation-file reads and network access
remain distinct methods in provenance; authorization does not make them equivalent.
The client-side methods are now eligible for integration. Network manipulation
remains excluded, and installation files remain read-only. This plan itself runs
or enables none of these methods.

## Every supplied project

All rows remain incomplete at the target-integration level. “Native” below means
the recommended destination, not that the missing binding already exists. Exact
paths and per-project tests are in the JSON; original defects/evidence are in the
[qualification matrix](W101_LEGACY_QUALIFICATION.md).

| Supplied archive | Role and runtime recommendation | Next concrete repair or binding | Packages |
| --- | --- | --- | --- |
| `afk-wizard-main.zip` | Python detector baseline → native C# states | Positive battle evidence; absent/obscured book becomes unknown. Reuse existing Auto-Idle instead of its global loop. | LP05 |
| `Aurorium-main.zip` | Rust manifest concepts → reference/offline import | Fill a measured version-provenance gap; bind changed sources to affected qualification without requiring its server. | LP04, LP10 |
| `Automatus-v2-master.zip` | Python route/statistics teacher → native route consumer | Repair missing imports; import one landmark route with arrival predicates, heading and obstruction tests. | LP06, LP09 |
| `cheat-engine-master.zip` | Pascal/C/C++/Lua diagnostic UX reference | Target identity, freshness and value-origin presentation; no driver or application integration. | LP10, LP11 |
| `Deimos-Wizard101-main.zip` | Python compiler/decision fixtures → native declarative routines | Fix signed-coordinate and malformed-deck defects; translate bounded condition/action IR. Current internal launcher stays blocked. | LP06, LP08, LP09 |
| `Deimos-Wizard101-master.zip` | Separate Python alternative | Repair sign defect independently; compare same inputs with main, preserving disagreements and versions. | LP06, LP08, LP09 |
| `gardening-bot-main.zip` | Python visual teacher → native plant/wait/action states | Fix interrupted key release; one-plant dry run with verified need, energy and outcome. | LP05, LP06, LP07 |
| `GrubNinja-master.zip` | JS/React/Electron map/state reference | Import vocabulary only; generated scores/reward submissions are not demonstrations. No app lifecycle or endpoints. | LP03, LP10 |
| `libkiwad-master.zip` | C reader as independent baseline | Repair unsafe initializer and absent compressed reads before comparing against selected importer. | LP04 |
| `On-Wiz-master.zip` | Dated documentation reference | Normalize cited claims with revision/uncertainty; no executable gameplay binding. | LP03, LP10 |
| `petdance.zip` | Native C# teacher + offline Python comparison | Freeze Tiny UI/fade/loading calibration; fresh variable-round evidence; then profile/Start/Stop/report workflow. The archive is TAR despite its suffix. | LP02, LP11 |
| `ProjectMaelstrom_win-x64_portable.zip` | Historical binary/UX reference | Compare documented flows; do not replace current runtime or launch a nested old app. | LP11 |
| `raid-utility-tools-main.zip` | Four Python/Qt specialist references | Import four separate role/location records; compose later after solo route/combat contracts qualify. | LP03, LP09 |
| `w101-proto-go-master.zip` | Go schema vocabulary → offline reference | Normalize reviewed type names without needing its missing client module or executing protocol calls. | LP10 |
| `wiz-packet-map-main.zip` | C++ schema/asset reference | Separate useful data utilities from old x86 injection; saved-file comparisons only. | LP04, LP10 |
| `wizAPI-master.zip` | Python matcher → native C# visual primitives | Fix nonsquare center, scale and coordinate assumptions; bounded waits; compare with wizSDK on identical crops. | LP05, LP08 |
| `Wizard101-Bazaar-Tc-Farming-master.zip` | Python matcher → native bounded search | Fix no-match/color semantics; report item/price within page/time caps. Purchasing requires a separate transaction operation. | LP05, LP07 |
| `Wizard101-Damage-Calculator-master.zip` | C# pure consequence component | Repair mutation/enchant/resistance/boost against independent fixtures; bind only after repeatable results. | LP08 |
| `Wizard101-Farming-main.zip` | Python/OpenCV teacher → native turn policy | Separate recognition/input, normalize coordinates once, test shuffled hands and verify turn outcome. | LP05, LP08 |
| `Wizard101-Utilities-master.zip` | Python/JS/data → offline knowledge importer | Import a small gear/pet batch with source/conflicts; connect facts to cache/snack dry runs without inventing stock or benefits. | LP03 |
| `WizGallery-dev.zip` | TS/JS/Python indexing patterns → native recording library | Bind saved annotations to frame/source/zone evidence; omit separate website, database and migrations. | LP03, LP11 |
| `wizSDK-master.zip` | Python matching/wait baseline → native helpers | Preserve tested matcher; compare saved crops and bound waits. Internal client launcher remains blocked. | LP05, LP06 |
| `wizwad-main.zip` | Python read-only importer candidate | Repair signatures, bounds, decompression limits and worker deadlines. Writer deferred. | LP04 |
| `wizwalker-master.zip` | Python WAD journal and observation vocabulary | Isolate journal parsing from client startup; reviewed client observation/control adapter is eligible, with runtime integration pending. | LP04, LP10 |
| `WizWikiAPI-main.zip` | TypeScript/JSON context → existing native cache | Import 16 historical world records with dates/conflicts; archived API is not required or presumed working. | LP03, LP09 |

The previously connected companion, moonlight, wiztype and wizdiff are accounted
for in the [earlier integration map](W101_CAPABILITY_INTEGRATION_MAP.md). They are
not additional members of the 25-archive inventory. Saved-file decoding and patch
comparison can support this plan; current live-access policy still applies.

## Ordered work and acceptance

These LP packages refine the existing W01–W09 plan. The JSON preserves those
links. They do not reset completed foundation work, and none is marked complete.

| Order | Package | Dependencies and smallest useful deliverable | Acceptance gate |
| --- | --- | --- | --- |
| 0 | LP01 contracts/evidence | Extend current scheduler/owner with implementation and evidence coverage | Existing duplicate/cancel/crash guarantees hold; imported content cannot grant capabilities |
| 1, parallel | LP03 knowledge | LP01; one bounded saved-data batch | Idempotent import, contradictions retained, unknowns stay unknown |
| 1, parallel | LP04 assets/version | LP01; one repaired bounded reader | Plain/compressed differential fixtures plus malformed/expansion/change failures |
| 1, parallel | LP05 vision | LP01; same small crop set across alternatives | Correct local coordinates, stale/absent/occluded/scale rejection, no input |
| 1, parallel | LP10 references | LP01; dated schema/diagnostic records | No process/traffic/account access; methods and revisions remain distinct |
| 2 | LP02 Pet Dance | LP01 + LP05; frozen profile and fresh episode comparison | Actual keys, result and cleanup independently verified before live qualification |
| 2 | LP06 conditions/routines | LP01 + LP05; three bounded branching examples | Different events produce different branches; failed prerequisites stop actions |
| 3 | LP07 gardening/search | LP03 + LP05 + LP06; one plant and finite search | Resource/freshness/cancel checks; no purchase hidden inside search |
| 3 | LP08 evaluator/combat | LP03 + LP05 + LP06; pure evaluator first, verified turn later | Independent repeatability/modifier tests, then shuffled-hand and outcome checks |
| 4 | LP09 routes/quests/farming | LP03 + LP06 + LP08; verified subgoals | Arrival/progress evidence; obstruction, wrong heading and unknowns cannot pass |
| 3, first vertical release | LP11 workspace/delivery | LP01 + LP02 + LP03 | Visible method qualification, complete installed workflow and preserved reports/packs |

Pure damage repair can start in parallel now; its later combat integration depends
on LP08's other prerequisites. Route research can also continue earlier, but full
route/combat/quest composition must wait for its postconditions. LP11 does not
require every later gameplay feature to ship a completed first workflow.

## Agent-sized work and evidence

Useful independent assignments now: one saved-data importer, one WAD fixture
repair, one visual-matcher comparison, the pure damage evaluator, and dated schema
mapping. Give each an exclusive file scope and a small frozen input set. Return
source pins, tests, a result report with failures, exact remaining gates and the
files to merge. Source ownership does not require parallel agents to compete for
build outputs or desktop input.

Serialize shared contract/policy edits, builds, packaging and all live desktop
tests. Keep one analysis worker and no loaded model by default. A background task
must not launch an old bot just because its package is under review.

For each overlapping implementation: preserve it → repair deterministic defects →
compare identical saved evidence → freeze a candidate → evaluate held-out states →
qualify bounded live behavior where relevant → expose the method in the workspace.
Retain failed fixtures, old source hashes and rollback choices. A teacher is useful
evidence, not automatic ground truth. Review redistribution rights separately
before bundling copied code/assets.

This review's validation checks catalog/inventory coverage, source hashes,
referenced paths, dependency order and explicit incomplete/non-executable status.
It does not establish new runtime compatibility, gameplay success or permission
to enable a different observation method.

Validation passed: 25 projects, 11 acyclic work packages, eight overlap groups,
234 existing local paths, all document links, canonical JSON formatting and the
scoped whitespace check. No builds or gameplay tests were needed for this plan.
