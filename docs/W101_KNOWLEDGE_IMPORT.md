# First offline Wizard101 knowledge import

Implemented **2026-11-09**: a bounded standard-library Python importer for exactly
the existing [pet-snack research ledger](../research/w101-pet-snacks-20261108.json).
It creates a **new** SQLite database using the
[version-1 knowledge schema](../research/game_knowledge_schema.sql), plus a receipt
for the exact committed database. It does not fetch URLs, inspect a running game,
read installation files, load a model, execute legacy code, enable feeding or
connect the C# app to SQLite. No dependency was installed.

The [database design](GAME_KNOWLEDGE_DATABASE.md) remains the broader contract.
This importer implements its first new-database slice; merge/update, additional
ledger formats, query/export UI and application integration remain pending.

## Run and output

```powershell
python tools/import_w101_knowledge.py --ledger 'F:/Modlists/AutoWizard101/research/w101-pet-snacks-20261108.json' --output-root 'F:/Modlists/AutoWizard101/.local/w101-knowledge-import-check' --name pet-snacks-new-review
```

Both ledger and output-root paths must be absolute and remain inside this
checkout. The output name is a simple lowercase name; existing bundles are never
updated or overwritten. The default ledger is the reviewed pet-snack file, so
`--ledger` may be omitted. No arbitrary SQL/schema path is accepted.

The result is a new directory under the selected output root containing:

- `knowledge.sqlite`: schema, source revisions and mapped research claims.
- `import-receipt.json`: ledger/schema/database SHA-256 hashes, mapping version,
  import identity, inserted IDs/counts, research date/timezone, scope and explicit
  qualification/runtime limitations.

A staging directory is created beside the final destination. The schema's own
creation transaction runs there; all mapped records are then inserted in one
transaction with foreign keys enabled and integrity checks. Database commit and
close happen **before** the receipt is generated, so its database hash identifies
the actual committed file outside the SQLite write lock. After checking that the
ledger/schema bytes have not changed, a same-parent directory rename publishes
the database and receipt together.

An exclusive per-name filesystem lock prevents two importer instances publishing
the same bundle. Controlled failures remove only the invocation's staging
directory and lock, leaving existing output intact. Windows rename refuses an
existing destination; a second existence check also precedes publication. This
is atomic publication on the intended local filesystem, not a power-loss
durability guarantee or a sandbox against unrelated programs changing the output
directory. A hard process termination may leave a staging directory/lock for
manual inspection; there is no automatic stale-lock takeover or overwrite.

## Explicit mapping

Only `schema_version=1`, `kind=game-research-ledger`, `game_id=wizard101`,
`subsystem=pet-snacks` with the eight reviewed source IDs and nine reviewed claim
IDs is supported. Source/claim record fields are allowlisted. Missing provenance,
unknown IDs/fields, duplicate JSON keys, invalid dates, failed source retrievals,
credential-bearing locators or runtime-enabled claims are rejected. Input JSON
is bounded to 256 KiB; the fixed repository SQL schema is bounded to 1 MiB.
Linked paths are rejected. Recorded HTTPS locators are stored as data, never
fetched.

| Ledger data | Database representation |
| --- | --- |
| Wizard101 | `maelstrom.game.wizard101`; explicitly a local Maelstrom identity, not an official game ID. |
| Pet-snack subsystem | `pets` domain; eight local mechanic entities: training snacks, pet resources, preferences, Happiness feeding, ordinary-item feeding, mastery skipping, reward modifiers and supply. |
| Eight sources | Eight stable `maelstrom.source.w101.pet-snacks.*` IDs and initial source revisions. |
| Source kind | Official guide stays `official-guide`; secondary guide maps to `community`; publisher-administrator announcement maps to `other` with its original kind retained. |
| Bibliography | Publication/modification dates stay distinct from retrieval date/timezone. Undated values remain null. Titles are explicitly `Ledger source: <id>` labels; actual page titles/publishers were not recorded and are not invented. |
| Source scope | Original region, platform, release-scope, limitations, URLs and HTTP result are preserved in structured JSON within `source_revisions.revision_note`, with the ledger hash and original source ID. |
| Nine claims | Nine stable assertion-series IDs and nine immutable initial claims; statement stored verbatim as text data. |
| Uncertainty | The ledger's explicit evidence classification is copied without promoting multiple citations to corroboration or assigning confidence scores. |
| Source references | Twelve evidence edges. The first listed source is primary for storage; that ordering is not an authority ranking. All listed references remain attached. |
| Version/current applicability | Claim version, region, platform and effective dates remain null; realm stays unknown. The Happiness source's historical Test Realm caveat remains in its source revision, not a new claim of current Live applicability. |
| Provenance/lineage | Original claim IDs appear in assertion IDs; source locations point to the ledger hash and JSON claim index. Initial revision IDs include that ledger hash, with revision number 1 and no invented predecessor. |

The web pages' bytes were not supplied to this importer. Their `content_sha256`
therefore remains **null**; the ledger digest must not masquerade as a webpage
content hash. Retrieval dates and local research dates do not establish game
feature release dates or client observations.

The ledger's proposed configuration/contracts, failed retrieval, work milestones,
qualification cases and unresolved questions are explicitly listed as not
imported in the receipt. They remain in the original ledger; the importer does
not turn design notes into executable routines. The global research scope/date
are retained in the receipt. No observations, qualifications, runtime candidates,
candidate-claim links or account/pet/inventory facts are created.

## History and limitations

Reimporting to an existing bundle fails and leaves its database/receipt byte-for-
byte unchanged. Importing revised research to a *different* new bundle preserves
the older bundle, uses the same stable assertion IDs and produces different
revision identities. This is **not a cross-database history merge**: each fresh
store begins at revision 1. A future merger must compare provenance, assign
actual predecessor/revision numbers and preserve contradictions; it must not
blindly insert these independent initial snapshots into a shared store.

The schema's immutability and foreign-key guards remain installed. No claim is
automatically current, true or suitable for gameplay merely because it imported
successfully. There is no native UI/provider, scheduled import, background
service, URL retriever or automatic runtime import in this slice.

## Verified output and tests

The small [verified bundle](../.local/w101-knowledge-import-check/pet-snacks-v1-20261109/import-receipt.json)
contains a 323,584-byte database with 1 game, 1 domain, 8 mechanic entities,
8 sources/source revisions, 9 assertion series/claims and 12 evidence edges.
Foreign-key and integrity checks pass; qualifications and runtime candidates
both contain zero records. The exact database hash matches its receipt. Local
`.local` artifacts are not assumed to exist in a clean checkout.

[Twelve importer tests](../tools/tests/test_import_w101_knowledge.py) pass using
small workspace-local temporary databases:

```text
python -m unittest tools.tests.test_import_w101_knowledge -v
```

They cover the actual ledger mapping, dates/scope/uncertainty, missing or unknown
provenance, runtime flags, invalid/oversized data, source changes during staging,
immutable history, duplicate import, competing lock preservation, transactional
failure, receipt/publication failure and SQL-looking statements remaining plain
data. No test launches an application or accesses the game/network. Both Python
files were formatted using the already installed Black; combined validation is
owned by the primary session.

Next: add a bounded read-only query/export operation, review a second explicit
ledger mapping, then design an append-only merge transaction with conflict and
revision accounting. Application SQLite provider selection remains a separate
decision; this importer adds no C# dependency.
