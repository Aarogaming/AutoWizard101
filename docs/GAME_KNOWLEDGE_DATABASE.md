# Local game knowledge database

The proposed store gives Maelstrom one place for accumulated game facts,
relationships, references and qualification evidence. It keeps contradictory and
historical information instead of flattening the newest search result into truth.
It is general to games; Wizard101 supplies the first import and research tasks.

Implemented in this slice: [version-1 SQLite schema](../research/game_knowledge_schema.sql)
and [in-memory integrity tests](../tools/tests/test_game_knowledge_schema.py).
No production dependency, database server or model was installed. The schema
tests use synthetic facts explicitly labeled as examples, not new Wizard101
research. The 2026-11-09 [first bounded importer](W101_KNOWLEDGE_IMPORT.md) now
adds a separate offline pet-snack research bundle: 8 sources, 9 claims and no
runtime qualifications. A native application database service and knowledge UI
are still pending.

## Data model

| Record | Purpose and boundary |
| --- | --- |
| Games and domains | Stable local game identity and research areas such as quests, navigation, spells, pets and minigames. |
| Entities | Quests, areas, routes, spells, enemies, items, pets, NPCs, resources, mechanics and minigames. Initial display names identify records; later name corrections can be sourced claims. |
| Typed relationships | Connections, locations, requirements, rewards, containment and use. An edge names a proposition; its existence alone does not establish a working route or currently available mechanic. |
| Sources and source revisions | Locators and bibliography, with publication/modification dates separate from retrieval date/timezone. Unknown dates stay null. Optional content hashes identify the bytes actually retrieved. |
| Claim series and immutable revisions | Subject/predicate plus a typed value, statement, uncertainty and applicable version/region/platform/Live/Test/preview scope. Revisions correct or retract without deleting earlier assertions. |
| Additional evidence and claim links | Supporting/refuting/context citations; contradictions, refinements, scope differences and later resolutions. Competing assertions can coexist without an automatic winner. |
| Observations | Actual current-client, recorded-client, synthetic or manually reported evidence; distinguishes human inputs from Maelstrom inputs. Artifact references/hashes stay local. |
| Qualifications | Scoped support, contradiction or inconclusive evidence about a claim. Current-client qualification requires a current-client observation with version, region, platform, realm and a hashed artifact. It is not permission to execute a routine. |
| Runtime candidates | Disabled references to an action registry entry, routine, recipe or recognizer in a named/versioned game pack. The database contains no executable candidate scripts or enabled candidates. |

`maelstrom.game.*`, `maelstrom.entity.*` and the other prefixes are **Maelstrom
identifiers**, not asserted official game IDs. Importers must maintain a stable
mapping for each imported identity; do not regenerate IDs from display names on
every import. An official ID, when actually discovered, is a separate sourced
claim with its official namespace. Changing a public name does not change the
local entity identity.

The schema permits multiple claim series for the same subject and predicate.
Each series has sequential revisions and one recorded leaf. `claim_leaves` is a
view of latest revisions, including retractions; it is **not** a truth-selection
view. Readers must inspect disposition, scope, evidence and competing series.
Qualifying an observation from a new build or realm cannot silently corroborate
or refute a claim explicitly limited to another build/realm; record that mismatch
as inconclusive or a different-scope link. Opaque version strings are not ordered
lexicographically to infer compatibility.

## Integrity and history

Every connection must enable `PRAGMA foreign_keys=ON` before a transaction and
check it succeeded. The reviewed schema enables it for the creating connection.
Foreign keys prevent missing sources and cross-game relationship/qualification
bindings. Typed values, date bounds, SHA-256 shapes and revision predecessors
are constrained. Supporting or contradicting client qualifications must match
the explicit claim's version, region, platform, realm and effective-date scope.

Updates and deletes of knowledge rows are rejected. Corrections append a source
revision, claim revision, observation, qualification or explanatory link. Duplicate
key inserts are rejected before `INSERT OR REPLACE` can erase history, including
with SQLite recursive triggers disabled. Importers should query and compare an
existing immutable record before attempting an idempotent insert; do not use
`REPLACE`, silent `IGNORE` or unchecked upserts to conceal differing content.

These are integrity guarantees under the application's connection contract, not
a security boundary against the database owner. The owner can delete the database
or change its schema. Future migrations should build and validate a new database
from the previous version, preserve the previous file/backup and only then switch
the app's active file. This version-1 script creates a new database and is not an
in-place migration or a command to recreate an existing user's library.

## Local-first adapters

Python research/import tools can use the standard-library `sqlite3` module. The
smallest first adapter is a bounded importer with parameterized SQL, one writer
transaction, an import receipt, and a read-only JSON query/export interface.
Each receipt should retain source-ledger digest, mapping/version, inserted IDs,
unchanged IDs, conflicts, rejected records and qualification limitations.

The C# app can initially consume bounded versioned JSON exports using its
existing JSON support. Native SQLite reads/writes require a separately reviewed
adapter: either the available Windows SQLite interface or a managed SQLite
provider after any required dependency approval. This schema does not silently
add either provider, a Python runtime distribution requirement, a background
server or a C#→Python process bridge. Wire the existing scheduler to bounded
knowledge import/query operations after their implementation is tested.

For concurrent research, keep one committing writer and give each agent a
separate staged import bundle. Readers should use short read-only transactions.
WAL, busy timeouts, cancellation, recovery and backup behavior need adapter tests
before being presented as application features. There is no reason to run a
model or keep a game capture active just to query this database.

Database routine references are inert data. Resolve them only through the
application's reviewed action registry and explicit game-pack qualification.
Never execute SQL, Python, shell commands, macros or prompts taken from a guide,
database note or downloaded file. Enabling a routine remains outside this store
and still requires the normal selected target, input owner and user controls.

## Staged import and research plan

| Stage | Work and scope | Completion evidence |
| --- | --- | --- |
| K1 | Implement the importer and read-only query/export adapter; create a fresh local database. | Atomic rollback on errors; foreign-key/integrity checks; repeat import is unchanged; modified ledger appends history; bounded reports. |
| K2 | Import [existing W101 source/observation ledger](../research/w101-context-20261108.json). | Stable IDs, each claim's source revisions and chronology, observed-local records separated from documentation; historical statements retained with their original observation date. |
| K3 | Import [pet-snack research ledger](../research/w101-pet-snacks-20261108.json). | Preserve source publication vs retrieval dates, uncertainty and disabled feeding policy. Separate training snacks, Happiness feeding and ordinary inventory-item feeding. |
| K4 | Add the [recorded Commons/Dance study](W101_DEMONSTRATION_20261108.md) as observed evidence. | Video/report hashes, human-play provenance and calibration limits retained. Human success never becomes an autonomous-player qualification. Old “no live Dance yet” observations remain dated history. |
| K5 | Research a bounded navigation slice: Commons, Pavilion, transport types and their preconditions. | Versioned area/route entities, sourced edges, normal transport distinct from memory teleport, observed landmarks and traversal checks; no automatic route enablement. |
| K6 | Research a bounded quest slice: prerequisites, objectives, NPC/area links and reward references. | Documented quest graph with uncertain/optional/branching conditions; user progress is a separate local observation, not a public fact. |
| K7 | Research a bounded spell/combat slice: costs, school, targets, effect types and versioned interactions. | Source-specific values and conditions; historical balance changes coexist; rules are evaluated separately before any combat routine uses them. |
| K8 | Expand items/enemies/pets/minigames and other games by independent domain tasks. | Source coverage and unresolved gaps reported per slice; reusable schema, distinct game IDs and independent runtime qualification. |

Do not treat this as a request to scrape or mirror all game pages. Save locators,
bibliographic facts, concise paraphrased claims, reviewed identifiers and hashes.
Retain full local evidence only when needed and permitted; do not copy complete
guides into database notes. No credentials, private clipboard contents, model
prompts or account secrets belong in import bundles. An allowlist-based importer
must enforce that policy; SQL cannot determine whether arbitrary prose contains
a secret. Shared exports should omit private local observations/artifact paths
unless explicitly selected by the user.

For project management, each domain task should name its entity set, source
selection, publication/effective/retrieval dates, unknowns, reviewed claim IDs,
pending current-client checks and immutable import receipt. This allows research
to proceed in parallel while one primary session owns live game testing. The
[Pet Dance tracker](PET_DANCE_DELIVERY_TRACKER_20261108.md) is the first workflow
example; snack research remains an independent optional subsystem.

## Validation and remaining limits

Run the small checks without starting the app or game:

```text
python -m unittest tools.tests.test_game_knowledge_schema -v
```

Twelve in-memory tests passed during this implementation. They cover mandatory
source provenance; publication/retrieval separation; invalid dates; source and
claim revision lineage; retraction/history; replacement prevention; contradiction
coexistence; typed payloads; cross-game foreign keys; current-client qualification
scope; historical realm/version differences; and permanently disabled runtime
references. They do not prove the stored facts are true.

Remaining work includes an importer, database file lifecycle/migrations, native
UI/provider integration, canonical identity/alias resolution, richer multi-effect
spell schemas, route/quest graph validation, source refresh policy, source-access
failure handling, export/privacy controls, content licensing decisions, disk
budgets and a query UX. Optional confidence numbers are stored evidence metadata,
not calibrated probabilities or an automatic source-ranking algorithm. Current
schema qualifications concern claims; controller/package qualification and live
execution still belong to Maelstrom's experiment and action systems.
