<!-- response-format: 5w1h-bda -->
# Capability composition: gap map (2026-11-19)

**Status: draft design note, docs only.** On 2026-11-19 the owner pasted a Copilot suggestion that Maelstrom, Merlin,
Library, Guild and Fortress be unified through a shared "capability composition" layer (capability + condition +
authority + constraints, job cards as ladder logic, CLI/API/MCP as one declared interface, context loading before
execution). This note records what the repo already has, the two real gaps, and the parts that are not adopted.
The suggestions are proposals, not evidence; a second paste (API, MCP, CLI, LSP and event-stream surfaces,
with Aaroneous and Devtools context) is assessed in R4, and a third (capability genome, intent preservation, lineage and decay) in R5. The owner states the Omni, Library, Chimera and Marionette names come from the
`Aarogaming/aaroneous` and `Aarogaming/aaroneous-devtools` repositories; both are public and were inspected read-only
on 2026-11-19 (R2, R4). No code was changed and nothing was run against a game.

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
owner names it a goal and the in-repo contract (R1) has been exercised. **Where.** Not in this repo alone. Aaroneous and aaroneous-devtools are public and were inspected read-only;
Merlin, Library, Guild, Workbench, MyFortress and Maelstrom are private repositories whose names are visible but
whose contents were not read. **Why.** The ADR treats Merlin, Library, Guild and similar projects as optional
future adapters, and `docs/ASSISTANT_CONTEXT.md` calls Merlin an optional source, not a required host. The owner states the Omni, Library, Chimera and Marionette names come from the Aaroneous repositories. Aaroneous holds
real counterparts to several proposed layers (R4), but no file path in either public repository contains those names
except Omni, so the others may live in file contents or in the private repositories, which were not read. A private
repository named MyFortress exists; Fortress cannot be assessed from its name alone. **How.** Not started.
If pursued: write the contract inside Maelstrom first, then test it by writing one adapter, using Aaroneous's
`UniversalTool` descriptor and event types as the comparison point rather than inventing a third scheme. **Before.** Owner
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

## R4. Surfaces: facts, capabilities, operations, understanding, transitions (second Copilot paste)

The paste splits interfaces into API = facts, MCP = capabilities, CLI = operations, LSP = understanding, plus an
event stream = transitions, and ends by saying transport should be free to vary. As vocabulary this is useful; as an
assignment of content to protocols it does not match the repo.

| Copilot layer | What the repo has | Verdict |
| --- | --- | --- |
| Facts | A read-only facts view served to API, MCP and CLI consumers (`ProjectMaelstrom/ProjectMaelstrom/Utilities/W101FactsStore.cs`); native `state` and snapshot calls | Exists, on every transport. The Diego observer report has no schema-version, session, sequence or digest field (`docs/AARONEOUS_COMPATIBILITY.md`), so facts exchanged at that seam are not yet self-describing |
| Capabilities | Catalog as the discovery contract for all surfaces (`docs/BACKEND_CONTROL_PLANE.md`); `research/maelstrom-capabilities.json` (non-executable); MCP server `tools/maelstrom_mcp_server.py` delegates catalog calls to the native owner | Exists; the gap is R1. Aaroneous has its own counterpart (`UniversalTool` trait and `core/hypervisor/src/capability_registry.rs`, see below) |
| Operations | `MaelstromCli.cs` commands and named-pipe operations through one `OperationScheduler`; MCP observe calls go to the same owner | Exists; coverage is partial and the audit says so |
| Understanding (LSP, Omni, Atlas) | Nothing in this repo. In Aaroneous, `crates/omni` has `query_engine.rs`, node descriptors, spatial layout and a protocol bridge (by file name; contents not reviewed), and its navigation spec sits under `docs/archive/stale_sprawl/`, so its current status is unknown. No file path in either public repo contains lsp, atlas, chimera, marionette, library or fortress | Not adopted here; the route, if wanted, is an adapter to Aaroneous's Omni, not a new local surface |
| Transitions | Progress/state events and a receipt that separates requested, admitted, dispatched, observed, completed, stopped and verified (contract item 5 in `docs/BACKEND_CONTROL_PLANE.md`); no event-stream transport is documented here. Aaroneous has an event log with Intent, Proposal, Execution, Checkpoint, Consensus, Error, System and Mutation event types (`core/hypervisor/src/event_log/types.rs`) | Real gap here, but the planned fix is smaller than a message bus; Aaroneous's event types are the comparison point if a shared vocabulary is ever wanted |

**Who.** Owner decides whether to claim the receipt lookup; an implementing agent builds it; the integration agent
holds the build slot. **What.** Keep one operation contract across the pipe, CLI, MCP and UI, as the control-plane rule
already says, and close the transitions gap with the planned bounded owner-side receipt lookup keyed by operation ID
(terminal status, target binding, cleanup confirmation, result availability) exposed identically on all three
surfaces. Not adopted: NATS, Kafka, ring buffers or any other event bus, an LSP-style understanding surface, and the
rule that a transport carries only one class of content. **When.** The receipt lookup is the next backend slice named in
`docs/BACKEND_CONTROL_PLANE.md`, after the patch-resilience and live-qualification work; no dates are promised.
**Where.** `docs/BACKEND_CONTROL_PLANE.md` (contract and delivery order), `JobDeckHost`, `MaelstromCli.cs`,
`tools/maelstrom_mcp_server.py` and `tools/maelstrom_deck_client.py`, plus `docs/coordination/results/BACKEND-MCP-SESSION-154.md`
for the observation flow. **Why.** Today completion cannot be told from the disappearance of an observer field or from
non-empty facts, and a later snapshot is not tied to the operation that produced it. A message bus would add a
production dependency and a second state path to a single-machine desktop tool whose boundaries are one scheduler,
one input owner and bounded resources; `AGENTS.md` requires approval for any new production dependency.
**How.** Same request and receipt through each adapter, tested identically; polling plus receipts, no new transport.
Aaroneous and Devtools (read-only inspection on 2026-11-19 through GitHub's API: `Aarogaming/aaroneous` main at
`cc794cb3` dated 2026-11-09 and `Aarogaming/aaroneous-devtools` main at `a87ec8f2` dated 2026-11-13; file lists plus
the heads of three source files). The Aaroneous `UniversalTool` trait gives each capability a name, an opcode, a
category, a description and a parameter schema, with one JSON invocation face documented as serving cloud REST,
LLM MCP and CLI together. That is one capability behind several transports, which matches the control-plane rule
and contradicts splitting content by protocol. Adopting it here would mean assigning opcodes, which
`docs/AARONEOUS_COMPATIBILITY.md` declines to do, so it stays a comparison point, not a dependency.
`crates/adaptation_plane/src/capability_spec.rs` is a model-profile (genetics) module despite its name, not a
capability declaration. aaroneous-devtools keeps versioned contracts with schemas and fixtures under `contracts/`
(for example `guild-operation-v1.md`), a convention this repo could mirror; the ADR keeps Devtools independent.
The phrase "one capability, many routes" was not found in this repo's Aaroneous documents; the `UniversalTool`
source comment describes one capability with two invocation faces, which is close but not that phrase. **Before.** Owner claim for the
receipt lookup; confirm the receipt fields against `BACKEND-MCP-SESSION-154.md`. **During.** Offline and mocked tests
only; one owner, no second desktop controller; no live run implied. **After.** Done means the same receipt is returned
for the same operation ID through pipe, CLI and MCP, with an offline test per adapter. Still unproven: parity of
the other operations (the audit marks CLI and MCP coverage as partial) and that any real game outcome matches a receipt;
the Rust-to-C# bridge was not separately checked for event paths; the contents of the private repositories
(Merlin, Library, Guild, Workbench, MyFortress, Maelstrom) were not read, only their names; and whether
Aaroneous's registry and event log are used outside tests was not checked.

## R5. Capability genome: intent preservation, lineage and decay (third Copilot paste)

The paste argues that the unit worth evolving is the capability with its intent, contract, evidence and history, not the
file; that changes should be recorded as batches; that evidence should carry an age so stale capabilities become
measurable; and that a biology mapping (gene = capability, protein = implementation, HOX genes = architectural laws,
epigenetics = per-environment expression, Chimera = mutation and selection) is a useful design frame. The problem is
real. The mapping is an analogy, and most of it is not grounded in this repo.

| Copilot's idea | What the repo has today (checked 2026-11-19) | Verdict |
| --- | --- | --- |
| Capability with intent, contract, evidence, history | `research/maelstrom-capabilities.json`: 26 entries with id, scope, source references, callable surfaces, limits, deck binding, evidence paths, a source/installed/live verification state, next gap and related items. `docs/coordination/claims.json`: task, workstream, owner, state, owned paths, notes, release note | The pieces exist as separate records. They are not linked: the inventory references one claim or task id in total, and claims carry no capability link or evidence link |
| Batch with intent, evidence consumed, changes, expected outcome, verification, lineage | A claim plus its task brief, result document and conventional commit | The pieces exist; a new batch framework is not needed. Linking them is the gap |
| Decay: last verified, evidence age | Per-entry verification state without dates; one file-level audit date (2026-11-11); claims have no date field | Real gap; the same idea as the drift ledger in `docs/OPEN_ISSUES_REPORT_AND_PLAN_20261119.md` (R1, phase P4) |
| Known failure modes | Free text in `limits` and `next_gap`; patch tiers in the open-issues report | Real gap, small |
| Architectural laws (HOX genes) | Recorded: Facts != Authority != Actions (`docs/architecture/ONTOLOGY_NORMALIZATION.md`). The phrases "No Ambient Authority", "Verify Before Promotion" and "Evidence Before Trust" do not appear in the docs, though related substance exists in the evidence contract (`docs/coordination/EXTERNAL_AGENT_GUIDE.md`) and the replay promotion filter | Candidate laws for the owner to approve; none are recorded today |
| Epigenetics: same capability, different expression per environment | `AGENTS.md` mission: a game pack supplies game-specific knowledge while capture, action ownership, transitions and replay stay shared; `deck_binding` on capability entries | Already the design |
| Chimera as mutation and selection | Only a provenance-envelope schema for compile/decompile work in Aaroneous, reviewed at `9ee5442d` and marked "adapt selectively" in `docs/REPOSITORY_REUSE_REVIEW.md` | Not supported here; the private repositories were not read |
| Automatic mutation, repair and promotion by an agent | `AGENTS.md`: smallest safe change, claims on a shared checkout, owner approval for dependencies, no discarding other work | Not adopted |

**Who.** Owner approves field names and the candidate laws; an implementing agent drafts under its own claim; the
integration agent reviews. **What.** Three small, optional additions: (1) on capability entries, a `last_verified` value
(date plus the command or build it came from), links to the claims and result documents that produced the evidence, and a
`known_failure_modes` list; (2) date and link fields on claim records, with an advisory check first, like
`--strict-claims`; (3) a short list of candidate architectural laws, split into recorded and proposed. Not adopted:
automatic mutation, repair or promotion, and the biology table as a specification. **When.** After the patch-resilience
phases that build the drift ledger (R1 in the open-issues report) and after R1 of this note; no dates are promised.
**Where.** `research/maelstrom-capabilities.json`, `docs/coordination/claims.json`, `tools/check_project_board.py` for the
advisory check, and `docs/architecture/` for the laws list. **Why.** Lineage and age cannot be checked by a tool today. This
session's audit of `W101-QUEST-INTERPRETER-CORE-02` found a result marked Completed for a module with a no-op accept policy,
no timing code and no callers, and no gate could tell. **How.** Optional fields first; advisory warnings that list gaps;
backfill only where evidence exists; report coverage counts before anything becomes mandatory; reuse the patch ledger's
source tuple (file, size, time, hash where cheap) for data-derived entries; keep declarations descriptive, not executable.
**Before.** Owner approval of the field names; a baseline count of how many entries have each field (today: 26 entries,
one id reference); a decision on who maintains the records, given that the claims file names the coordinator role, which
is an agent role here. **During.** Offline schema and tests only; no capability is promoted and `executable` stays false;
no claim owned by someone else is edited outside the claim process. **After.** Done means the new optional fields
validate, coverage is reported, and the advisory check lists remaining gaps. Still unproven: that the records would be
maintained; that a `last_verified` date means something, since an agent can write today's date without running anything,
so each date must name the command that produced it (the evidence contract already requires numbers from output you ran);
and Chimera's real design, which sits in repositories that were not read.
