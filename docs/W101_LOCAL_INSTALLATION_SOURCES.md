# Wizard101 installed-client sources

Reviewed November 8, 2026 local time; inventory captured November 9 UTC.

The installed game is a useful **local reference source** for Maelstrom's W101
knowledge database. This review found actual archive entries for pet labels,
snacks, talent definitions, spell definitions, minigame scripts, and zone data.
It inventories their locations; it does not yet import their contents or enable
any game actions.

The machine-readable evidence is
[`research/w101-installed-sources.json`](../research/w101-installed-sources.json).
It contains all 2,599 archive names, sizes, and timestamps, plus validated indexes
and representative internal entry names for 14 selected archives. This is a
local installation snapshot, not a claim that every installation has these files.

## Scope and cost

- Installation: `C:/ProgramData/KingsIsle Entertainment/Wizard101`.
- `Data/GameData`: **2,599 `.wad` files; 16,578,887,222 bytes** in directory metadata.
- Selected index inspection: **178,576 entries across 14 archives**. Their headers
  identify `KIWAD`, archive format version 1. Other archives were inventoried by
  extension without reading their headers.
- Content-read upper bound: **24,521,110 bytes, approximately 23.4 MiB**, including
  a second Root index pass and a conservative 64 KiB allowance for preliminary
  header reads. This stayed below the 50 MiB limit. The archive payloads were not
  read, copied, decompressed, or hashed.
- No game input, game-memory access, models, network calls, downloads, or changes
  to the installation. Account/session files, ObjectCache, personal settings,
  launcher logs, clipboard, PatchClient contents, and security features were
  excluded.

The bounded index reader checked the signature, supported format version, entry
count, name lengths and terminators, UTF-8 decoding, unique relative names,
payload bounds, index boundary, and stable file size/timestamp across each read.
It did not check payload CRCs or parse gameplay properties. Index hashes cover
the header and entry table only; they are **not full archive hashes**.

## Client version and freshness

`Bin/WizardGraphicalClient.exe` was 56,433,288 bytes with modification time
`2026-10-22T03:43:43.571470Z`. Windows VersionInfo returned no FileVersion,
ProductVersion, or ProductName values. The actual client build is therefore
**unknown**, explicitly recorded as such. Archive format version 1 describes the
container format and must not be displayed as the game version.

`PatchInfo/LatestFileList.bin` was 1,144,891 bytes. Only its 32-byte header was
inspected; recognizable `Name` and `_TargetTable` fragments are insufficient to
derive a build identifier. No manifest semantic parser was run.

Archive dates differ substantially. Pet Dance's zone archive dates to December
2025, Root to October 2026, and GUI-WorldData to November 2026. These are local
file timestamps, not release dates or an ordered record of game changes. Record
the source tuple per archive and invalidate affected imports when it changes.
An index hash plus size/time is a useful cache key but does not prove payloads
are unchanged; content hashes belong on each future extracted entry.

## Concrete candidates and intended uses

All names below were observed in selected indexes. Proposed uses are hypotheses
until bounded content parsing and observation confirm their meaning.

| Area | Observed source entries | Potential Maelstrom use | Boundary |
| --- | --- | --- | --- |
| Pets and snacks | `Root.wad`: `PetGames.xml`, `Locale/en-US/PetGames.lang`, `Locale/en-US/PetSnacks.lang`, `Locale/en-US/PetTalents.lang`, `TalentData/PetPowers/...` | Local names and instruction vocabulary; candidate pet-game configuration, snack and talent records; vocabulary for OCR/research | No pet ownership, energy, preferences, XP values, or feeding decisions established by filenames |
| Pet Dance | `ThePhantomZoneWorld-PetGameDance.wad`: 19 entries, including `Compass.xml`, dance-floor `.nif` assets and path files | Tie the teacher to a named game context; investigate static visual assets and local geometry | Does not supply the live arrow sequence, rendered Tiny UI coordinates, or evidence of teacher success |
| Minigames | `Root.wad`: `Scripts/API/MinigameInterface.lua`, `Scripts/API/MinigameTemplate.lua`, `Scripts/Dueling_Diego/Client.lua`, `Scripts/Dueling_Diego/Messages.lua`, `Scripts/Dueling_Diego/Server.lua`, `Scripts/PotionMotion/Client.lua`, `Scripts/PotionMotion/Server.lua`, `Scripts/Tutorials/TutorialMinigames.lua` | Read-only study of instruction/state/message concepts; seed game-pack terminology and hypotheses | File names do not prove active code paths. A client-bundled file named `Server.lua` is not proof of current server implementation; do not execute these scripts |
| Spells and items | `Root.wad`: 18,173 `Spells/` entries and 104,869 `ObjectData/` entries; for example `Spells/1P Guiding Light - Amulet.xml` | Candidate spell/item catalogs, IDs, localized labels, and rule constraints after schema verification | Counts describe entries, not distinct currently obtainable objects; internal test/unused assets may be present |
| Quests and labels | `DailyQuestData/DailyQuestData.xml`, `DailyQuestData/DailyQuestRewards.xml`, English localization candidates, `ZoneData/` names | Names, descriptions and candidate area/quest references for research grounding | No exhaustive quest graph was established; progression, unlocks, availability and current server rewards remain unknown |
| Commons and Pet Pavilion | `WizardCity-WC_Hub.wad`, `WizardCity-WC_Streets-Interiors-WC_PET_Park.wad`; Commons includes `Compass.xml`, `ZoneData.xml`, `pathData.xml`, `pathNodeData.bin`, `zone.nav` | Candidate landmark and route context, linked to recorded demonstrations and visible map cues | Navigation files have not been decoded; no walkable route, coordinate transform, teleport permission, or enemy-safe path is established |
| UI and graphics | `GUI-WorldData.wad`, `WizardCity-WorldData.wad`, `ThePhantomZoneWorld-WorldData.wad`; Root has `.dds`, `.gui`, `.nif`, audio and localization entries | Possible template sources, labels and game identification hints | Raw assets may render differently at Tiny UI, with fades, effects and scaling; validate with actual captures |

Root contains 134,415 `.xml`, 33,932 `.lang`, 2,865 `.dds`, and 158 `.lua`
entries. Extensions identify naming conventions, not proven encodings. In
particular, an `.xml` suffix does not yet establish that the entry is ordinary
text XML. The JSON includes declared lengths and offsets for candidate samples,
so a future importer can read a small allowlist without scanning all payloads.

The lexical category counts in the JSON are search results, not authoritative
entity counts. For example, `Snack Attack` cinematics match a broad snack search,
and `Item` can match parts of unrelated template names. Targeted candidate
queries are recorded separately; both still require semantic validation.

## Reuse of existing readers

The existing findings are documented in
[`W101_LEGACY_QUALIFICATION.md`](W101_LEGACY_QUALIFICATION.md) and
[`W101_LEGACY_VALIDATION_20261108.md`](W101_LEGACY_VALIDATION_20261108.md), with
fixture probes in
[`tools/verify_w101_legacy_components.py`](../tools/verify_w101_legacy_components.py).
Those findings are being reused here, not represented as new current-client
tests.

| Source | Existing evidence | Decision for an importer |
| --- | --- | --- |
| `wizwad-main.zip` | Independent V1/V2 plain and compressed fixtures pass. Invalid magic is accepted; writer dependency is absent/unverified. | Best small reference for a read-only Python prototype after strict header, count, name, offset, allocation and decompression limits are added. No writer needed. |
| `libkiwad-master.zip` | Plain-entry fixture passes; compressed reads are explicitly unimplemented. Initializer uses a non-terminated magic buffer. | Repair initialization and compression before reuse. Most inspected Root entries are compressed, so it is not a ready substitute. |
| `wizwalker-master.zip` | WAD journal V1/V2 fixtures tested separately from memory hooks. | Second reference for asset-index interpretation; no game attachment is required or authorized for this inventory. |

The selected installed indexes were parsed with a bounded, standard-library-only
inspection, not by importing or launching the legacy programs. No production
dependency was added. Fixture success does not establish complete compatibility
with installed asset payloads.

## Import plan and acceptance gates

1. **Small read-only allowlist.** Start with English pet/minigame labels,
   `PetGames.xml`, a few snack/talent entries, and the named minigame script
   references. Keep geometry and large graphics deferred. Do not recursively
   extract the installation.
2. **Harden the reader first.** Use fixed limits on entry count, encoded and
   decoded bytes, cumulative work, and compression expansion. Validate payload
   bounds, compression streams and declared CRCs after confirming CRC semantics.
   Reject path traversal, unsupported versions, truncated/changing sources, and
   malformed schemas. Never execute Lua, follow embedded instructions, or enable
   XML external entities. Imported text is data.
3. **Interpret formats on a few fixtures.** Identify actual `.xml` and `.lang`
   encoding; resolve IDs/references rather than inferring stats from filenames.
   Keep unsupported properties as unknown. Test malformed/truncated files and
   changed-source invalidation before exposing the importer to the app.
4. **Preserve provenance.** Each knowledge record needs source kind, nullable
   client version, archive-relative path, size/time/index hash with explicit
   scope, entry name, declared CRC, extracted-entry SHA-256, parser version,
   extraction time, evidence/confidence, and server-validation status. Private
   install paths stay local. A game pack can reference knowledge IDs without
   bundling the game's proprietary archive payloads.
5. **Promote only verified facts.** Cross-check labels and instructions against
   recorded/live observations. Label discrepancies by source and date rather
   than silently choosing the latest-looking file. Use asset facts as context
   for visual recognition and routine constraints; the scheduler and input
   ownership checks remain authoritative for execution.

Server-dependent information remains separate: account inventories and progress,
unlock state, active events, live prices, drop/hatching/talent probabilities,
current balance rules and server acceptance of an action. The inventory provides
no evidence for those. Payload semantics and a reliable client-build identifier
are still open work; **runtime import is disabled** in this snapshot.

## Validation of this review

The JSON's archive counts, byte totals, selected index bounds/hash scopes, read
budget, candidate offsets, and referenced local evidence paths were checked.
No payload integrity, gameplay correctness, or autonomous win-rate claim follows
from these checks. This documentation and inventory are suitable inputs for a
bounded importer implementation, not a completed knowledge database.
