# Optional Node-RED workbench

Maelstrom remains the execution owner. This integration is an optional local Windows authoring client; it is not bundled into the installed app. Node-RED 5.0.9 with Node 24.20.0 was tested in an isolated development directory. No global install or game connection was used.

## Editable example
Import `examples/offline-card.json` through Node-RED Import. It uses standard Inject, Switch, Change, Catch and Debug nodes around two existing maelstrom-local nodes. No Function node or model is needed.

Set MAELSTROM_TEST_DATA_ROOT to an absolute **isolated test** data folder before starting Node-RED, or edit both Maelstrom nodes' Data folder fields to that folder. Start the installed executable with `--deck-host <isolated-folder> <new-stop-file>`. Do not use your active gameplay data root for this rehearsal.

The manual Test card once button reads owner state, requires a successful response and status Stopped, then calls the existing Stop action. Maelstrom reports Paused after stopping, so another click does not repeatedly fire. The card's condition, action mapping, labels and connections are editable. This is a visual control-card example, not an imported native JobCardSpec or live game routine. Stop is intentionally the only action exercised. The example's fixed request ID is for this Stop-only rehearsal; new start/step operations need fresh GUID-N IDs, reused only when reconciling the same uncertain request.

Double-click a Maelstrom node to configure its path, subscription channel/rate and Allow actions. Read nodes leave actions disabled. Never connect subscription heartbeats directly to start/step: use an edge/one-shot gate, current evidence and native run limits. Unknown game facts do not satisfy conditions. An API acknowledgement is not a verified game outcome.

## Local development setup
Install Node-RED into a separate development folder (task43 uses `.local/node-red-43`, version pinned in its package-lock), then either install this local package into that runtime's userDir or point Node-RED settings.nodesDir at this folder. Bind uiHost to 127.0.0.1; disable dynamic module installation. Our test settings and runtime are local artifacts, not a production service. No startup task is installed.

Example task43 startup from repository root (PowerShell):

```powershell
$env:MAELSTROM_TEST_DATA_ROOT = 'F:\Modlists\AutoWizard101\.local\node-red-43\data'
node --max-old-space-size=256 .local/node-red-43/node_modules/node-red/red.js --userDir .local/node-red-43/user --settings .local/node-red-43/settings.js
```

Editor: http://127.0.0.1:18843 . The deck host must be started separately and stopped via its stop file. The saved userDir retains the edited flow. All task43 test processes were stopped after verification.

Tests: `node --test integrations/node-red/bridge.test.js`. The editor integration test is opt-in (`MAELSTROM_EDITOR_SMOKE=1`), assumes the isolated task43 host/editor and deployed example, modifies only that test flow, restores it, and verifies both false and true conditions. Syntax: `node --check integrations/node-red/editor-smoke.test.js`. No separate JS linter/formatter is configured.

Sources: https://nodered.org/docs/creating-nodes/first-node and https://nodered.org/docs/user-guide/runtime/configuration .

## Reusable native cards (task44)

Import `examples/replay-card.json`. The new Maelstrom card node has a configuration back (owner folder, library folder, load ID and native card fields). Set MAELSTROM_TEST_DATA_ROOT and MAELSTROM_CARD_LIBRARY, or replace those fields with absolute paths. Replace the sample graph/session placeholders with saved evidence paths. Use a fresh 32-hex GUID for each new card version.

Preview emits `front` (title, catalog description, requirements, filters, qualification and limits), `back` (catalog options/settings), and `card` (the actual native JobCardSpec). This is the basis for front-facing play requirements and back-facing editing; no flip animation or graphical condition picker is implemented yet. Custom cards compose existing registered routines and facts; arbitrary executable actions are not accepted.

Save creates `<library>/<id>.json` exclusively. Load revalidates against the current catalog. Neither operation executes. To edit a loaded card in a flow, modify `msg.payload.card`, change topic to card/preview or card/deal and return it to the author node; catalog and native validations apply again. Change ID before saving another version. Deal fetches the current deck revision and submits the card through the existing API; native rejection remains authoritative. No automatic retry or auto-start.

Replay uses the existing `review.routine` catalog entry with graphFile and sessionFile settings. Once dealt, trigger the owner's existing step command separately, then inspect native state/progress and the operation's replay.json. Use an isolated deck with only the intended Replay card selected. The example deliberately does not wire Save or subscription heartbeats to execution. The stored routine graph and captured receipts are unchanged; success establishes recorded-evidence replay, not live qualification.

Catalog facts are typed; conditions support the existing eq/gte/lte vocabulary. Unavailable facts remain requirements and block native eligibility. No new condition evaluator or copied game controller was introduced. Node-RED flow JSON itself is not compiled into routine graphs: arbitrary flow-to-graph conversion is a separate task.

Tests: `node --test integrations/node-red/card-library.test.js`; two native integration cases require MAELSTROM_CARD_SMOKE=1 and the isolated .local/card-author-44/data host plus saved action39/release40 fixtures. Full integration checks save/load, unknown-resource blocking, native deal, scheduler replay, and author-node handlers. No production dependency added.

## Graphical card workshop (task45)

Open `/maelstrom-cards` on the optional local Node-RED editor (the Maelstrom card node links to it). Configure these server environment variables before starting Node-RED:

- MAELSTROM_CARD_EDITOR_DATA_ROOT: absolute folder of the existing disconnected owner.
- MAELSTROM_CARD_LIBRARY: absolute saved-card folder.
- MAELSTROM_CARD_EDITOR_EXE: absolute trusted installed Maelstrom executable used for offline replay only.

The front displays the native description, requirements, preserved scope filters, qualification and run limits. Edit back reveals catalog routine selection, named options, typed requirements, run/attempt/cooldown/priority fields and selection state. Boolean requirements use Yes/No; numeric comparisons offer eq/gte/lte. Unavailable producers are marked. Scope filters loaded from prior cards are preserved and displayed; graphical filter editing is not implemented yet.

Save creates a card without overwriting. Save new version assigns a new ID and preserves the original. Load restores the fields. No deployment or gameplay occurs from this editor. Changes remain local until saved; there is no autosave or unsaved-change navigation guard yet.

Test recorded replay accepts only review.routine and absolute saved graph/session paths. It calls the existing --replay-routine command, hidden, with a60-second bound and no shell. Reports are new files under the card library/replay-reports. It checks the zero-desktop-dispatch/non-live qualification contract. This tests the recorded graph, not card eligibility: cards with extra conditions/filters are explicitly refused rather than silently ignoring them. Use a separate unconditional Replay card for the evidence test; full condition+recorded-fact rehearsal is still pending.

Admin routes use Node-RED flows.write permission when authentication is configured. Keep this development editor loopback-only; this task did not configure remote access. No new dependencies. Tests: node --test integrations/node-red/card-editor.test.js (service tests), plus existing library/bridge tests. The actual browser save/load/version/replay workflow passed against an isolated installed owner; the new editor has not been packaged into Maelstrom's native window.
