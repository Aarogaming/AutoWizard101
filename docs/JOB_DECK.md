# Card collection, Routine Deck and local API

Release checkpoint: 2026-11-11. This is a native Maelstrom feature, not a second
game controller. Open **Wizard101 · Card collection & Routine Deck** from Home.
The card/deck update is installed. See [installation evidence and combat work](coordination/results/CORE-DECK-RELEASE-02.md)
and [source validation and remaining work](coordination/results/CORE-DECK-01.md).

## Using cards

Source follow-up: four **Shared Modules** cards now expose the existing
navigation, sigil/interaction, menu and Auto-Idle modules for offline rehearsal.
IDs are `module.navigation`, `module.sigil`, `module.menu`, `module.auto-idle`.
For an existing saved deck, choose **Add card** to add one to the collection.
It starts undealt; existing cards and run history are preserved. Loading an
update does not automatically change the saved deck.
They are game-neutral and use the ordinary card/deck API and shared scheduler.
Offline evidence intentionally uses the bundle's recorded game identity, which
may differ from the workspace game; report identity describes the recording,
not a connection to the active game. No live target is inferred from that choice.
They do not send game input or enable live traversal, menus or between-run Idle.
The earlier `w101.auto-idle` preparation card keeps its identity and remains
unavailable for live dispatch; existing saved cards are not silently reinterpreted.

Choose **offline-replay** with a module bundle and labelled session manifest,
or **screen-review** with its pinned saved-image manifest (leave the separate
bundle field empty in this mode). A bundle must contain only the selected kind;
it may contain several sequential modules of that kind. The session points to
its local event file. Set a review timeout of 1–300 seconds; deck limits can
shorten it. Saved evidence is pinned and frozen before ordinary admission.

Completion means that the saved observations and receipts satisfy the module
contracts. Missing outcome evidence cannot count as a successful live action.
Navigation, sigil and menus use the same replay runner as Auto-Idle; no second
controller or nested scheduler is introduced. Screen review retains its existing
W101 menu-OCR recognizer limitations; screen-review is not a general recognizer
for navigation, sigils or other games. Labelled replay supports other game profiles.
These additions are source-only until a matching release is installed.

The collection contains Quest, separate Pet Games, all eight Fairgrounds
minigames, maintenance/farming/battle preparation cards, and Offline Replay.
Collection groups are binders, not commands to play every card in that group.
Only cards explicitly dealt into the **Routine Deck** participate in a run.

- Click a front to flip to its settings. The bottom button deals/returns it.
- Set copies to complete, maximum attempts, priority and cooldown. Duplicate a
  configured card to make an independent variant. Saving never starts work.
- Add exact include/exclude values for a fact; exclusions win. Missing or stale
  facts wait. The current editor shows one scope filter and preserves additional
  filters imported through the API. Conditions use `fact eq/gte/lte JSON-value`;
  a more guided condition editor is a follow-up, not yet implemented.
- **Play one** admits at most one eligible card. **Play deck** evaluates the
  collection once per second, runs one operation at a time, and waits without
  owning game input when conditions are unmet.
- **Stop** cancels pending observation, pending starts and active deck work;
  cleanup drains before ownership is released. Closing the card window also
  stops/drains its work. There is no automatic Idle fallback keypress.
- **New run** explicitly resets counters after a stop. Ordinary edits, removal,
  reinsertion, reopening, or request retries do not replenish used attempts.
- Import/export saves configuration only, not live targets, permissions or
  successful outcomes. Imported cards never automatically start.

The art uses Gemini's original vector icons with purple, parchment and gold
styling. An installed serif font is selected with a Georgia fallback. This does
**not** claim to bundle the Wizard101 font or authentic game icons. The exact
game typeface remains unverified. [CARD-ART-01](coordination/results/CARD-ART-01.md)
preserves artwork provenance; integration also bounds drawing and releases
enumerated font resources.

## Actual capability boundaries

| Card | Current behavior |
| --- | --- |
| Offline Replay | Freezes a recorded routine, manifest and events; submits to the ordinary scheduler; succeeds only if the saved replay completes. No game inputs. |
| Quest | Generic objective auto-fill and scope filters. Can complete a currently open, explicitly enabled conversation through the existing verified conversation adapter. It does not yet walk, fight or gather. |
| Other game cards | Customizable preparation entries. Clearly marked **Preparation only**, unavailable to dispatch. Standalone Dance evidence does not automatically qualify a deck visit. |

The optional W101 connection selects exact window/process/start-time identity
and separately enables reviewed UI-tree observation and quest actions. Play can
collect one bounded fresh reading first, with the start request reserved durably
against its exact deck revision. An observation is not a completed action.
Stop, expiry, changed configuration or failed cleanup cancels that continuation.

The generic Quest card recognizes `Go To`, `Explore`, `Talk To`, `Defeat`,
`Collect`, and `Defeat … and Collect …`, with explicit unknown results for
unsupported grammar. Targets and areas are text, not guessed game entity IDs.
Count suffixes such as `(0 of 3)` are progress observations, not instructions to
send three inputs. No quest name is an execution branch.

Auto-fill derives an immutable selected-conversation snapshot from fresh,
stable objective/dialogue text. A changed objective invalidates an old plan;
scope is rechecked before every meaningful dialogue advance. A saved selection
file remains an alternative. The lower of card and file page limits applies.
Older cards with a selection file but no source mode keep their explicit file
selection. Dialogue completion still requires a changed objective and closure.

Useful facts currently include `dialogue`, `menu`, `nativeIdle`,
`quest.objective`, and objective-derived `quest.action`, `quest.target`,
`quest.area`. `quest.area` is the **destination in the objective**, not the
wizard's current zone. Health, mana, inventory, energy, current area, quest IDs
and main/side quest classifications are **unknown** until qualified readers
provide them. `quest.action` values include `Destination`, `Talk`, and
`DefeatAndCollect`; they do not imply an implemented adapter.

For navigation, the user's map is the primary route/road reference, with scale
calibration when zoom changes. The quest arrow is a coarse hint. Distance is a
progress signal which may increase on a legitimate detour. The old Python
walking prototypes are not admitted controllers. See
[navigation preflight](coordination/results/W101-DESTINATION-PREFLIGHT-01.md).

## Contracts and evidence

`JobDeckDocument` version 1 has a name, game ID, up to 64 configured cards,
attempt cap (1–1000) and wall-time cap (1–3600 seconds). Each card selects a
registered routine and settings, inclusion/exclusion filters, scalar conditions,
priority 0–999, copies 1–100, attempts up to 100, and cooldown 1–3600 seconds.
Cards cannot supply executable code, arbitrary keys or a new input transport.

Successful copies count only terminal `Completed` receipts from the trusted
adapter. Failures spend an attempt and pause the continuous run; deferred cards
spend nothing. Copies and attempts are separate. A deck deadline includes queue
and automatic-observation time. Cleanup may continue after cancellation; expiry
does not abandon a held input lease.

State lives under `<data-root>/job-decks/active.json`, with an exclusive owner
lock. Removed-card progress is retained for the current run's quota. Up to 128
card histories and 1024 run request IDs are kept; state and summary sizes are
bounded. An interrupted session does not resume. Storage/uncertain-outcome holds
survive ordinary Stop/reopen. Do not clear them by editing files: preserve the
receipt evidence and use the existing recovery review before a new session.
Deck-specific guided recovery of ambiguous outcome holds remains follow-up work.

Each scheduled run preserves `job-card.json` (configuration, revision, observed
facts, target, qualification) alongside the normal scheduler receipt and adapter
reports. Full errors stay in receipts; the deck stores bounded summaries.

## Local API and MCP

Open the native Card Deck to host the API, or run the same executable headlessly:

```text
Maelstrom.exe --deck-host <absolute-data-root> <stop-file>
Maelstrom.exe --deck-api <absolute-data-root> <request.json> <new-response.json>
```

Creating the stop file stops/drains the headless owner. Launch hidden and wait
for exit. A GUI and headless host cannot own the same data root simultaneously.
The API uses a current-user-only named pipe. It is not an internet listener.
Requests/responses have a 256 KiB limit, finite read/connect timeouts, and a
versioned `{ok, result, error, schemaVersion}` envelope.

```json
{"command":"step","requestId":"d17e28261df14a7292b899d413c0f47d2","arguments":{}}
```

Commands: `catalog`, `state`, `snapshot`, `replace`, `deal`, `preview`, `step`,
`start`, `pause`, `stop`, `reset`, `windows`, `connect`, `disconnect`, `observe`,
`validate-routine`.
`replace` takes `{deck, revision}`; `deal` takes `{card, revision}` and updates
one configuration without running it. `reset` requires the current revision.
`preview` may accept synthetic facts; it cannot replace live observations.
Use a fresh GUID-N request ID for a new `start`/`step`; reuse it after a lost
response. Retries never create additional actions. Poll `state` for outcomes.

`validate-routine` takes `{graph: <native routine graph object>}` and calls the
same `RoutineGraphs.Read` contract used by replay. It accepts the native schema-1
shape (`id`, `game_id`, `start_node`, limits, pause scenes and nodes), not the old
MCP-only `routine_id`/schema-2 shape. The graph is limited to 64K characters and
the existing transport limits still apply. It requires no game connection and
does not change the deck, acquire input or submit an operation. Structural
acceptance does not establish trusted action bindings, replay success or live
qualification. The reply's result contains `valid`, `confirmed`, `errors`,
`authority: "native-routine-graphs-v1"`, and `liveQualified: false`.

The existing MCP server now delegates to this owner through
`tools/maelstrom_deck_client.py`. Configure absolute `MAELSTROM_EXE` and
`MAELSTROM_DATA_ROOT`. It does not launch a game or a second controller, retry a
timeout, or return invented vitals. The generic MCP deck command exposes catalog,
preview, editing and bounded run controls; game connection is explicit through
the native UI/API rather than silently granted by a tool call.

The dedicated MCP `validate_routine_graph` tool also delegates to the native
owner. A normal rejection has `valid: false, confirmed: true`; an unavailable
or older owner, malformed reply, or locally rejected oversized/non-JSON input
has `valid: false, confirmed: false`. It has no independent Python validator or
success fallback. Open the updated Card Deck or headless owner to obtain an
authoritative answer; validation does not start one automatically. This is a
source contract repair and requires a build/update before an older installed
host can serve the new command.

The MCP `get_w101_policy_state` tool describes the Python generic legacy
launcher only. It does not observe or enable the native client's current
permissions (`native_opt_in_state: "not_observed"`). The documented
[reviewed W101 exception](W101_CLIENT_SIDE_EXCEPTION_20261110.md) and a running
adapter's explicit opt-ins are separate from those legacy-launcher restrictions.

The existing `MaelstromBot.Server` deck endpoints are authenticated proxies to
the same pipe. Mutations require its admin API key; start/step also require the
`Idempotency-Key` header. `/session/snapshot` is observed state only; the old
mutable simulated snapshot was removed. An absent native owner returns an
unavailable error, not a fabricated completion. Running this optional web server
is not required for the app or MCP.

## Scan-cycle and card metaphors

The core card/document/rule/service contracts are game-independent. W101's
catalog, objective parser, UI adapter and skin are the first substantial built-in
composition. A future game pack adds registered observations/actions/outcomes
and game knowledge; it does not add another scheduler or input owner. No
Minecraft/Combat Arms player is implemented by this checkpoint.

The owner's supplied PLC/ladder-logic comparison is a useful design analogy:
observe, evaluate prerequisites, choose one eligible operation, dispatch through
the control owner, verify the outcome, and update the deck. The current deck
scan is one second; fast reflexes remain inside bounded specialists. A large
vision/language model is not required per scan or input.

An eventual **hand** should expose ready/waiting work rather than randomly hide
required maintenance. **Discard** must distinguish completed, failed, deferred
and cancelled work. **Reshuffle** is an explicit repeat allocation with global
time/resource/attempt limits; it cannot silently reset budgets. **Treasure**
overrides remain subject to current-target, ownership and stop guards. **Wards**
are persistent monitors with bounded pause/recovery policies; detection does
not authorize killing the game or bypassing permissions. These additional UI
metaphors are design direction, not already implemented engine features.

Conditions reduce macro desynchronization; they do not eliminate stale reads,
ambiguous screens or delayed input effects. Retain freshness, target binding,
observed outcomes and cleanup checks at every tier. No Rust rewrite, new input
library, random draw engine, automatic client termination or unbounded repeat
loop is authorized or introduced by the supplied conceptual description.

Gemini's subsequent `GenericMacroDeck<TContext>` is preserved as an offline
experiment. Its source is explicitly excluded from the shipped app and linked
only into the existing test assembly, where its synthetic tests still run.
It has direct delegates and no shared ownership, budgets or durable outcome
contract; it is not the runtime behind the card UI. Its hand/reshuffle concepts
can inform future data-only selection policies without creating a second player.

## Combat integration checkpoint — November 11

The source now has a bounded whole-battle wrapper over the existing turn runner,
and a target-addressed mouse dispatcher under the same input lease. Each turn
gets distinct proposal/evidence identities, fresh observations and planning,
while global time/read/input limits remain in force. Explicit observed victory
is required; it does not imply a collected quest item.

The CLI's optional combat observation now includes bounded source-layout card,
participant and duel candidates, plus an explicit readiness report. These new
fields have offline tests, not live battle qualification. The Quest card still
cannot dispatch combat, and encounter navigation is not connected. No Fire Elf
was defeated by this checkpoint. Gemini's four observation-bridge files remain
reserved and were not present at integration. See the
[combined report](coordination/results/W101-COMBAT-CONNECT-06.md).

Optional [game sign-in storage](W101_SIGN_IN.md) is reachable through Settings
in the source build. Credentials stay in Windows Credential Manager, outside
cards, decks, recordings and exports. Saving a credential does not enable login
or a gameplay routine. Launcher submission remains unconnected.

## Shared fact discovery

Card settings now offer a registered string-fact picker for scope filters and a
fact reference showing type, units, producer support and description. Condition
expressions remain editable; native validation owns type/operator checks. A
supported producer does not imply a connected game or fresh reading. Unsupported
resource producers are explicit and cannot be replaced with invented defaults.

MCP `maelstrom_get_capability_catalog` delegates to the native read-only
`capabilities` command. It takes no arguments and does not connect, observe,
refresh or start work. An unavailable/older owner remains an explicit error;
there is no fabricated fallback catalog. The current source requires a matching
native owner. This is source integration, not new installed/live qualification.
The card reference and scope picker include trusted per-routine extension facts from the same native catalog used by validation.

## W101 semantic module cards — source checkpoint

`w101.interact` configures an exact expected prompt and bounded interaction;
`w101.menu` supports only opening the verified game menu; `w101.auto-idle`
retains its existing identity and method options. Only native Idle entry has a
new binding seam; saved spin/step/routine choices remain preparation-only.
Wake/resume and automatic between-run scheduling are separate work.

These cards accept a code-owned `W101ModuleCardAdapter` with prepare and readiness
callbacks. The ordinary host does not supply that adapter, so default live
availability stays false. Registration is not current readiness: the callback
must enforce the selected target, freshness, semantic context, prompt identity,
geometry and permission before a prepared operation is created. Saved card JSON
cannot supply callbacks. The timeout setting is bounded to1–60seconds.

Tree booleans alone do not identify a game menu, sigil or clickable Idle control.
The shared module bindings require independently verified context; saved replay
remains available through the separate `module.*` cards. No live qualification,
automatic dialogue progression, shop access or arbitrary menu commands are added.
