# Maelstrom roadmap

Current course: **2026-11-12**. See the [all-front progress snapshot](coordination/results/PROJECT-STATUS-20261112.md) and [Travel card evidence](coordination/results/W101-TRAVEL-30.md). The architecture ordering below comes from CORE-ARCH-AUDIT-01; checkpoint rows retain historical scope.
[Product](PRODUCT.md) defines scope; [the board](coordination/workstreams.json)
owns workstream status and claims; [the architecture audit](ARCHITECTURE_AUDIT_20261111.md)
records the gaps and acceptance tests. Dated reports are historical evidence,
not competing current plans.

Maelstrom is a lightweight, reusable desktop game-player and experiment workbench.
The [execution model](ROUTINE_EXECUTION_MODEL.md) defines Action Cards, Routine
Cards and the Routine Deck in application terms, using the existing runtime.
Games are workspaces; packs provide rules, recognition and bindings; cards choose
bounded routines under one scheduler/input owner. It should operate repeated runs
without an assistant driving every transition. Wizard101 is the first expansive
integration, not a replacement for the generic framework.

## Current checkpoints

| Checkpoint | What the evidence establishes | What it does not establish |
| --- | --- | --- |
| [Installed release](coordination/results/CORE-DECK-RELEASE-02.md) | Card fronts/settings, native deck owner, API/MCP and installed offline replay; 1,905 native, 94 Python and 14 installer tests at that release. | Full auto-questing, combat, all prepared card types or today's source installed. |
| [Later source](coordination/results/W101-COMBAT-CONNECT-06.md) | 2,047 native tests at that checkpoint; candidate combat observations, bounded input/battle components, secure credential setup; clean short live Idle observer. | Qualified battle decisions/input, automatic sign-in, new installation or a defeated Fire Elf. |
| [Architecture/knowledge audit](ARCHITECTURE_AUDIT_20261111.md) | Actual callable surfaces, card gates, imported-source roles, representative mechanics verification and ordered work. | Exhaustive function coverage, all game knowledge current or new gameplay qualification. |
| [Concrete module observation](coordination/results/W101-MODULE-INTEGRATION-05.md) | Real CLI probe combines optional UI reader and current screen OCR; both recognized native Idle and disconnected cleanly. Reviewed reader pins, exact prompt candidates and bounded X/Escape transport added in source. | Live input, route navigation, enabled production module cards or a new installed release. |
| [Reusable Idle transitions](coordination/results/W101-CYCLE-INTEGRATION-06.md) | Automatic wake → left/right turn → native Idle succeeded under one CLI owner, independently confirmed by the user; 2,315 native tests and scoped formatting/analyzers pass. Earlier 59 focused Python tests remain unchanged. | An arrival-verified route, new sigil/interaction outcome, controller hardware support, general resolution coverage or installed update. |
| [Routine Idle fail-safe](coordination/results/W101-IDLE-INTEGRATION-11.md) | Shared finalization connected to quest CLI/deck, admitted modules, Dance postgame and home movement; 2,366 offline tests pass. | Live qualification of these integrations; unavailable combat/farming routines. |
| Pet Dance / Dodge | Dance has observed PERFECT gameplay; Dodge has isolated evaluation/learning components. | Repeatable complete Dance visits across supported configurations; Dodge's required fresh-game average of 50. |

The installed directory recorded by the release is
`7e16d7c03fd4462aa3f1797e01281536`. Subsequent source edits need a matching package.
Earlier roadmap statements that the card/deck or all UI-tree work were uninstalled
were stale; later observer worker revisions remain source-only.

## Ordered work list

Only bounded slices are active. This is dependency order, not a promise to run
all workstreams at once. Task IDs below are backlog identifiers until claimed.

| Order / task | Deliverable | Acceptance gate |
| --- | --- | --- |
| 0 · CORE-CONTRACT-REPAIR-01 · source verified | Replaced the divergent MCP routine validator with native validation through the existing local API. | 2,059 native and 31 focused Python tests pass; actual built-app bridge agrees; unavailable owner stays unconfirmed. Not yet installed. |
| 1 · CORE-CAPABILITY-02 | Canonical runtime capability and typed fact catalog, using existing catalogs as inputs. | UI/CLI/MCP expose the same IDs/options/availability; unknown facts rejected during editing; each unavailable fact names its producer. Audit inventory is not the runtime registry. |
| 2 · CORE-OBSERVATION-02 | Bounded shared observation refresh and resource/context facts. | Waiting cards resume on fresh qualifying evidence without external repolling; stale/unknown/conflicting readings remain explicit; refresh has a budget and stops with the owner. |
| 3 · W101-COMBAT-QUALIFY-11 | Reader → qualified facts → planner → bounded battle → outcome → Quest. | Real card identity, geometry, selected state and input response verified; no experimental formula used as proof of victory; before/after objective progress; clean releases. |
| 4 · CORE-MODULE-DECK-02 | Bind existing sigil, menu, native Idle and navigation modules to cards. | Reuse one owner; verify loading/resume/takeover; automatic between-run Idle preserves dialogue and user changes; route arrival independently observed. |
| 5 · W101-DANCE-VISIT-02 | Repeated complete Dance visit through the deck. | Entry, level, loading, all rounds, result, no-feed exit and Idle succeed under declared UI/resolution variants without assistant menus. |
| 6 · CORE-KNOWLEDGE-02 | Read-only knowledge export and context/shortcut import. | Sources/dates/build/region, contradictions, freshness and unknowns survive export; no research claim silently becomes a permitted action. |
| 7 · CORE-PACK-CONTRACTS-02 | Versioned contract conformance and staged pack updates. | Saved fixtures pass across supported adapter versions; incompatible updates rejected; prior pack/report preserved; rollback tested. |
| 8 · CORE-AUTHORING-02 | Consistent card configuration and game-specific workspaces. | User can configure, validate, save/reopen, rehearse and inspect a run with clear blocked reasons; no editing JSON required. |
| 9 · CORE-RELEASE-03 | Package a qualified integrated checkpoint. | Format/lint/tests, installed API/CLI smoke, preserved user data, updater/rollback receipts. Do not ship speculative paths as enabled gameplay. |

## Parallel lanes

November 11 W101 binding follow-up: [sigil/menu/native Idle integration](coordination/results/W101-MODULE-INTEGRATION-04.md)
adds semantic bindings, trusted bounded runtime and card-adapter preparation.
2,127 native tests pass offline. Production transport/current geometry and prompt
identity still require qualification; live adapters remain disabled. Native Idle
entry is distinct from wake/resume and automatic between-run scheduling.

November 11 module follow-up: [four-module card integration](coordination/results/CORE-MODULE-INTEGRATION-03.md)
adds configurable offline navigation, sigil, menu and Auto-Idle rehearsal cards,
prepared jobs under the existing scheduler and an Add card picker for older
collections. 2,097 native tests pass. Step4 now has an offline integrated slice;
live game bindings and automatic between-run Idle remain unqualified.

November 11 source follow-up: [catalog/refresh integration](coordination/results/CORE-CATALOG-INTEGRATION-02.md)
implements typed standard/scoped facts, card validation/discovery and bounded
waiting-Quest refresh. 2,075 native and15 MCP tests pass offline. Steps1–2 now
have an integrated first slice; new resource producers, live qualification and
generalization beyond the current Quest observer remain open. No update installed.

- **Runtime integration:** catalog/facts/refresh and combat qualification. Shared
  contract edits and builds remain serialized by the coordinator.
- **Knowledge and reuse:** provenance/export, mechanics fixtures and archive
  disposition reconciliation. Imported calculators remain estimates until checked.
- **Other players:** Conjuration perception, Dodge offline learning, other pet and
  fairegrounds games. Each must reuse the same action/outcome contracts and provide
  independent evidence. Progress in W101 does not close Dodge's average-50 goal.

At most three disjoint agents plus the coordinator; claim exact files first.
Do not activate heavy models, parallel captures or competing controllers.
Unassigned work is a backlog, not a background worker.

## Later composition

Resource-driven switching (quest → inventory maintenance → snacks/pets → potion
refill → resume), collecting, farming, team functions and longer navigation need
qualified resource facts and individual outcomes first. A prepared category card
is not an implemented player. Purchases, feeding and selling require their own
configured policies, reserves and verified results; Crown Shop stays excluded.

Retain independent legacy alternatives for comparison; consolidate common
contracts and proven behavior rather than wholesale language rewrites. See
[reuse audit](coordination/results/CORE-REUSE-AUDIT-01.md) and
[legacy work packages](W101_LEGACY_WORK_PACKAGES.md).

## Evidence and runtime boundaries

Source exists, test exists, test passed, installed and live-qualified are separate
facts. Synthetic replay proves control logic on those inputs; saved pixels do not
prove arbitrary scales. A successful dispatch is not a successful action.

Default game interaction is UI-only. The [W101 exception](W101_CLIENT_SIDE_EXCEPTION_20261110.md)
permits optional reviewed client-side reads/hooks/writes through the shared owner;
network manipulation remains excluded and installed game files remain read-only.
Preserve no-feeding defaults, Crown Shop exclusion, volume and immediate release
on user takeover. Offline work remains the default; live work is separately scoped.

See [project organization](PROJECT_ORGANIZATION.md), [documentation map](INDEX.md)
and [external-agent guide](coordination/EXTERNAL_AGENT_GUIDE.md).





## November 12 attachment and character scheduling follow-up

Auto-attachment must invalidate process-bound observations on restart, inspect the
client image, select a reviewed compatible adapter profile, and qualify changed
bindings before enabling routines. Unknown builds must not inherit a previous
hook/layout qualification. Current source rejects unsupported UI builds before
control admission; automatic discovery/requalification and profile switching
remain work to implement. See `coordination/results/W101-FOREGROUND-16.md`.

Owner-requested backlog: optional character-selection/switching cards for
resource-aware scheduling. Verify wizard identity and maintain separate energy,
inventory/snack/spending policy, routine progress and resumable task state per
wizard. Switch to eligible configured wizards while others regenerate normally;
no timer manipulation, purchases or automatic character changes are implemented.
