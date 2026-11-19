<!-- response-format: 5w1h-bda -->
# Capability composition: gap map (2026-11-19)

**Status: draft design note, docs only.** On 2026-11-19 the owner pasted a Copilot suggestion that Maelstrom, Merlin,
Library, Guild and Fortress be unified through a shared "capability composition" layer (capability + condition +
authority + constraints, job cards as ladder logic, CLI/API/MCP as one declared interface, context loading before
execution). This note records what the repo already has, the two real gaps, and the parts that are not adopted.
The suggestion is a proposal, not evidence. No code was changed and nothing was run against a game.

## What already exists

| Copilot's idea | Existing anchor in this repo |
| --- | --- |
| Job card with intent, inputs, constraints, chain, success, failure handling | Common card contract in `docs/ROUTINE_EXECUTION_MODEL.md` (Identity, Configuration, Requirements, Limits, Actions, Completion, Interruption, Recovery, Record); scene-triggered edges with expected outcomes and deadlines in `docs/ROUTINE_GRAPHS.md` |
| CLI, API and MCP are one declared interface with different transports | Rule and six-point operation contract in `docs/BACKEND_CONTROL_PLANE.md`; the capability catalog is the discovery contract for CLI, API, MCP, cards and UI, with receipts that separate requested, admitted, dispatched, observed and verified |
| Capability declarations | `research/maelstrom-capabilities.json`: 26 entries, explicitly non-executable and not exhaustive |
| Context loaded before execution | Card Requirements (needed facts and their freshness); the facts store with a freshness TTL (`ProjectMaelstrom/ProjectMaelstrom/Utilities/W101FactsStore.cs`) |
| Authority separate from facts and actions | Core invariant Facts != Authority != Actions in `docs/architecture/ONTOLOGY_NORMALIZATION.md` |
| Convergence without merging repositories | `docs/ADR_MAELSTROM_INDEPENDENT.md`: Maelstrom stays independent; broader responsibilities are possible future adapter destinations, not available services |

Calling this "the next major architectural discovery" overstates it: most of the structure is already specified. The
open work is conformance (does each surface really follow the contract) and live qualification, not a new layer.

## R1. Per-capability authority and trust declarations (real gap)

**Who.** Owner approves any schema change; an implementing agent drafts it under its own claim; the integration agent
reviews. **What.** The 26 capability entries carry id, title, scope, source references, callable surfaces, limits, deck
binding, verification, evidence, next gap and related items. They carry no inputs and outputs, no required-authority
field and no required-trust field. The idea to declare these per capability is sound. **When.** Not before the
patch-resilience and live-qualification work in `docs/OPEN_ISSUES_REPORT_AND_PLAN_20261119.md` is under way; no
dates are promised. **Where.** `research/maelstrom-capabilities.json` and the board or schema check that reads it, with
a short update to `docs/BACKEND_CONTROL_PLANE.md`. **Why.** Authority and trust requirements are enforced in code and
in card Requirements, but a reader of the inventory cannot see them. **How.** Optional `inputs`, `outputs`,
`authority_required` and `trust_required` fields, filled only for entries that have evidence, and a check that a filled
trust value uses the real tiers. The tier set is `Observed`, `Candidate` and `Unknown` (see the trust note in
`docs/architecture/ONTOLOGY_NORMALIZATION.md`); a numeric rule such as "confidence above 0.8" would be an invented,
uncalibrated number and is not adopted. **Before.** Approval of the field names, and a count of which entries
have evidence. **During.** Offline schema and tests only; no capability is promoted, and `executable` stays false.
**After.** Done means the inventory validates with the new optional fields and the existing 26 entries are unchanged.
Still unproven: that declared requirements match what each surface actually enforces; that is a conformance audit.

## R2. A shared contract across Maelstrom, Merlin, Library, Guild and Fortress (deferred)

**Who.** Owner decides whether those projects are in scope; their own maintainers would have to adopt anything.
**What.** One declared contract, governance model and evidence model across repositories. **When.** Deferred until the
owner names it a goal and the in-repo contract (R1) has been exercised. **Where.** Not in this repo alone; the other
projects are not inspectable from here. **Why.** The ADR treats Merlin, Library, Guild and similar projects as optional
future adapters, and `docs/ASSISTANT_CONTEXT.md` calls Merlin an optional source, not a required host. Fortress is not
mentioned anywhere in the docs outside Gemini's drafts and result files, so it cannot be assessed. **How.** Not started.
If pursued: write the contract inside Maelstrom first, then test it by writing one adapter. **Before.** Owner
decision, and a check of each other repository's current contract. **During.** No work in this repo beyond reading.
**After.** The ADR rule stays: names are not recreated locally to match; nothing is claimed as available.

## R3. Parts of the suggestion that are not adopted

**Who.** Owner can overrule; agents follow it until then. **What.** (1) A second execution engine or "any implementation
can execute the graph", including models and external tools. (2) Numeric trust thresholds. (3) Treating the other
projects as domains of one composition system. **When.** From this note onward. **Where.** `docs/ROUTINE_EXECUTION_MODEL.md`
(no independent controller beside the existing job-card, routine and scheduler types), `AGENTS.md` runtime boundaries.
**Why.** One input owner, one scheduler, cancellation and takeover are safety properties, and models are optional slow
planners behind local rules. A second executor would bypass them. Numeric thresholds need calibration evidence this
repo does not have. **How.** Extend the existing card, graph and scheduler types where needed; keep declarations
descriptive, not executable. **Before.** Reread the runtime boundaries. **During.** Reject patches that add a parallel
controller. **After.** Revisit only with an owner decision and evidence; this note is not a decision record for other
repositories.
