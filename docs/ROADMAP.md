# Maelstrom roadmap

Current course: **2026-11-14**. Maelstrom remains a lightweight game-neutral player and experiment workbench. Wizard101 is the first extensive game pack; shared observations, cards, scheduler, ownership and replay remain reusable.

## Execution ownership

- [Codex work queue](coordination/CODEX_WORK.md): main integration lane, live qualification and release ownership.
- [Gemini work queue](coordination/gemini/QUEUE.md): bounded preassigned offline contributions, one at a time.
- [Workstream board](coordination/workstreams.json) and [claims](coordination/claims.json): evidence and exact file ownership. Queue completion is not automatic acceptance.

## Next product milestones

1. **Complete Pet Dance visit:** approach/sigil → arena → Play/loading → specialist → results → no-feed exit → verified Idle. Connect the existing visit state machine and specialist through the shared scheduler; qualify repeat visits. The Dance deck entry is currently preparation-only.
2. **Usable teaching loop:** long-session recording/telemetry/input → activity review → editable routine → condition/outcome replay → separately verified fresh session. Keep model-free operation and explicit uncertainty.
3. **Generic quest/combat loop:** navigation + verified dialogue/battle/collection + objective progress. No claim of autonomous Fire Elf completion or general farming.
4. **Expand packs:** other minigames, snacks, resource routines and additional games through the same contracts; do not create separate controllers.

## Current qualified checkpoint

[Release62](coordination/results/RUST-LIVE-RELEASE-62.md) is installed at version `a534bf82cce04f6dba3448b77cf4df79`. Installed Rust bridge and native scheduler read live W101 facts; expiry/disconnect/hook cleanup passed without game inputs. This is telemetry qualification, not automated gameplay.

Source-only follow-ups63–70 repair disconnected context, blank host roots, ambiguous/interrupted teaching attribution, cross-focus input scoring and bounded log ingestion. Their focused tests passed; they are not included in release62. Earlier all-front snapshot remains historical. Broad navigation, repeatable complete Dance visits, full combat and unattended wizard switching still require integration/qualification.

Live testing is now owner-authorized for the root's Dance work while away, subject to the current target, resource limits, countdown, takeover and cleanup controls. Gemini's queue remains offline. No new cron is needed: existing Codex hourly and Gemini ten-minute checks consume these queues.

## Architecture backlog (historical ordering; reconcile against current evidence)

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
