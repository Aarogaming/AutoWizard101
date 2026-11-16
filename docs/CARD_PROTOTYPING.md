# Card Workshop: native prototype authoring

The Card Workshop is an offline authoring surface over Maelstrom's existing
catalog, card editor, routine replay and qualification ledger. Open it from a
game's Routine Deck. It does not connect a game, acquire input, start a scheduler
job or load a model. New and reopened versions initially display **Unqualified**.

## User workflow

1. Choose a catalog action and select **New card**. This creates an undealt draft.
2. Flip the card. Give it a name, configure its existing action options, set run
   and attempt limits, and edit requirements and scope filters. **Save & flip**
   applies those changes to the draft.
3. Select **Save new version**. The original version remains intact; choose a new
   filename for each save. **Load prototype** reopens a version in its game
   workspace. Saving or loading does not qualify it or add it to a running deck.
4. For the **Replay** action, choose the routine and recorded-session files in
   the card's settings, then select **Test configured action**. The test uses the
   exact configured graph and checks recorded requirements, receipts and outcomes.
5. Inspect the status and saved report location. Select an older attempt and
   **Verify selected evidence for this version** to reproduce its evidence against
   the current configuration. Historical labels never silently promote a card.

The editor reuses `JobCardView` in prototype mode and `CardRequirementsForm`.
It hides dealing and the older arbitrary-graph review controls in this mode.
The new form does not introduce another card engine or a scripting language.
The workshop sizes its startup window to the owner window's display working
area (or the active display when rendered headlessly) so the default layout can fit 720p desktops; this source change is pending build,
focused test and refreshed offscreen-render verification (see task 81 report).

## Association and qualification boundary

`CardPrototype.ReplayBinding` currently recognizes only `review.routine`. Its
code-owned association is that action's required `graphFile` setting, together
with the selected game workspace. The test cannot accept a separate unrelated
graph as proof of another action's behavior. Other catalog actions can be
configured and saved, but remain unqualified until a trusted adapter supplies
their exact test association and verifier.

Passing Replay means that the configured recorded routine completed under the
card's conditions. It does not demonstrate live gameplay, repeatability,
perception accuracy, or that run counts/cooldowns were exercised. Run limits
remain validated configuration for the existing scheduler. Only one replay is
tested per request.

Qualification binds the entire card, graph bytes, prototype adapter version,
workspace scope and fact schema. Changes invalidate the current assessment
without removing older evidence. `CardQualification.RecordReplay` reproduces
the canonical runner's report and freezes its source material; `Assess`
reproduces the frozen replay again. A saved success flag is not sufficient.
See [Card qualification](CARD_QUALIFICATION.md) for the ledger's complete limits.
No stage grants execution permission and this workflow cannot promote to
`LivePassed` or `Repeatable`.

## Storage and resource limits

- Default prototype library: `<data-root>/card-prototypes/`; users can select
  another local save location. Schema 1 contains game ID and the existing card
  configuration only, bounded to 64 KiB. Unknown properties and duplicate JSON
  fields are rejected. Cards must remain undealt.
- Attempts: `<data-root>/card-prototype-tests/<card-id>/<attempt-id>/`, with
  `attempt.json`, the canonical report and a qualification snapshot on success.
  IDs are local GUIDs. Historical outcomes have a fixed vocabulary and do not
  establish current validity.
- The existing replay limits apply: 64 KiB graph/manifest, 16 MiB event stream,
  20,000 events, no wall-clock playback. The UI allows one review at a time on a
  worker task, and shows at most 256 locally enumerated attempts per card.
- Drafts can contain private local paths. Import/export does not sanitize these
  paths or authenticate who recorded the observations. These are local evidence
  checks, not cryptographic provenance or evidence of live success.

## Current delivery slice

This is a native catalog-backed prototype builder, not a general visual graph
editor. It can edit existing action options and conditions, preserve versions,
and test the Replay action with exact evidence. Building arbitrary action chains,
importing a qualified prototype into the Routine Deck, interactive recording,
and supervised/live qualification remain separate integration work.

`CardPrototypeTests` covers configuration round trips, undealt/catalog/schema
boundaries, exact replay and reopen, changed binding invalidation, unsupported
action association, missing paths/workspace mismatch, absent requirements and
receipts, history tampering and zero input ownership. These fixtures are
synthetic labelled observations. They do not constitute a native-form visual
check, installed-app verification or game qualification. Shared build, formatting
and test evidence is recorded by the coordinator after integration.
