# W101-SNACK-DATA-01 — preserve snack-claim provenance in a read-only view

| Field | Value |
| --- | --- |
| Parent workstream | `W101-SNACKS` |
| Task status | Proposed; unclaimed |
| Owner | Unassigned |
| Mode | Offline data view and validation; no feeding or web retrieval |

Follow [the collaboration contract](../EXTERNAL_AGENT_GUIDE.md). Coordinate with
the current snack/preferences owner before claiming these new paths.

## Outcome and inputs

Produce a small normalized claim view that makes chronology and uncertainty
visible without creating a second knowledge importer. Reuse
`validate_ledger` from [the existing importer](../../../tools/import_w101_knowledge.py)
and read [its mapping contract](../../W101_KNOWLEDGE_IMPORT.md), the
[knowledge schema](../../../research/game_knowledge_schema.sql), and the
[existing snack ledger](../../../research/w101-pet-snacks-20261108.json).

Also read [snack research](../../W101_PET_SNACK_RESEARCH_20261108.md),
[configuration](../../W101_PET_SNACK_CONFIGURATION.md),
[planner behavior](../../PET_SNACK_PLANNER.md), and the latest
[arena/snack evidence](../../PET_TRAINING_LEVELS_AND_SNACKS.md). These documents
may be receiving concurrent updates; note the exact input hashes you used.

## Proposed ownership

- `tools/w101_snack_claim_view.py`
- `tools/tests/test_w101_snack_claim_view.py`
- `research/w101-snacks/claim-view.schema.json`
- `docs/coordination/results/W101-SNACK-DATA-01.md`

Do not edit the existing importer/schema/ledger, `PetTrainingPreferences.cs`,
`PetSnackPlanner.cs`, `PetSnackRecommendations.cs`, their tests, or their existing
docs. Do not alter central workstream or release status.

## Scope

Accept exactly the current snack-ledger format through its existing validator.
Export a bounded read-only view with original source/claim IDs, literal statements,
evidence categories, source links, publication/update/retrieval dates, region,
platform, Live/Test scope, uncertainties and ledger hash. Keep unknown values
explicit. No invented official pet/snack IDs, quantities, inventory, preferences
or confidence percentages. Multiple citations are not automatic corroboration.

This view neither merges revisions nor writes an existing SQLite database. If
existing importer output already provides a requested field, reuse it or document
the mapping rather than implementing another import pipeline. Bound ledger input
to the existing validator's limit. Write a new output only; never overwrite a
source ledger or previous derived result.

Non-goals: additional browsing, complete snack catalogs, current reward tables,
feeding, purchases, Happiness automation, planner policy changes or runtime enablement.

## Acceptance

- The known ledger's sources and claims retain original IDs and evidence links.
- Retrieval dates do not become release dates; historical/Test Realm material
  does not become current Live qualification.
- The ledger hash does not masquerade as an unavailable webpage-content hash.
- Missing sources, malformed chronology and unsupported schemas fail before
  publishing partial output; valid null/unknown dates remain allowed.
- Every output is explicitly nonexecutable and feeding/runtime qualification
  remains false; no account or inventory facts are synthesized.

Return focused tests and a small sample view with provenance preserved. Name any
source inconsistency for a separate research task instead of silently repairing it.
