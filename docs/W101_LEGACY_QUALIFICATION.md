# Wizard101 legacy qualification matrix

Generated from local integrity, component, build and data checks. This is a
review baseline, not a claim that 25 complete applications work with current W101.
No upstream bot, game hook, account service or reward endpoint was started.
W101 was left in the user's native Idle mode throughout offline testing.

Each source remains a distinct candidate. Catalogue entries do not execute code
or make game packs playable. Live qualification and redistribution review remain
separate gates. Pet Dance's earlier synthetic rehearsal is documented in
[W101_LEGACY_TEACHERS.md](W101_LEGACY_TEACHERS.md).

| Project | Maelstrom areas | Qualification |
| --- | --- | --- |
| `afk-wizard-main.zip` | Auto-Idle, Combat | Offline components tested |
| `Aurorium-main.zip` | Assets, Versioning | Reference reviewed |
| `Automatus-v2-master.zip` | Navigation, Experiments | Offline components tested |
| `cheat-engine-master.zip` | Developer tools | Reference reviewed |
| `Deimos-Wizard101-main.zip` | Pets, Quests, Combat, Navigation, Routine language | Repair required |
| `Deimos-Wizard101-master.zip` | Pets, Quests, Combat, Navigation | Repair required |
| `gardening-bot-main.zip` | Farming, Navigation, Routine language | Repair required |
| `GrubNinja-master.zip` | Pets, Game context | Repair required |
| `libkiwad-master.zip` | Assets | Offline components tested |
| `On-Wiz-master.zip` | Developer tools, Game context | Reference reviewed |
| `petdance.zip` | Pets, Visual recognition | Offline components tested |
| `ProjectMaelstrom_win-x64_portable.zip` | Application UX | Reference reviewed |
| `raid-utility-tools-main.zip` | Quests, Navigation, Game context | Reference reviewed |
| `w101-proto-go-master.zip` | Developer tools, Game context | Reference reviewed |
| `wiz-packet-map-main.zip` | Developer tools | Reference reviewed |
| `wizAPI-master.zip` | Combat, Farming, Navigation, Visual recognition | Repair required |
| `Wizard101-Bazaar-Tc-Farming-master.zip` | Farming, Visual recognition | Offline components tested |
| `Wizard101-Damage-Calculator-master.zip` | Combat, Game context | Repair required |
| `Wizard101-Farming-main.zip` | Farming, Combat, Visual recognition | Offline components tested |
| `Wizard101-Utilities-master.zip` | Game context, Pets, Combat | Reference reviewed |
| `WizGallery-dev.zip` | Observations, Game context | Reference reviewed |
| `wizSDK-master.zip` | Visual recognition, Combat, Navigation | Offline components tested |
| `wizwad-main.zip` | Assets | Repair required |
| `wizwalker-master.zip` | Navigation, Combat, Pets, Assets | Offline components tested |
| `WizWikiAPI-main.zip` | Game research, Game context | Reference reviewed |

## Overlapping approaches

- **Assets**: `aurorium-assets`, `libkiwad-reader`, `wizwad-reader`, `wizwalker-client`.
- **Combat**: `afk-visual`, `deimos-main`, `deimos-master`, `wizapi-visual`, `damage-evaluator`, `farming-opencv`, `utilities-context`, `wizsdk-visual`, `wizwalker-client`.
- **Developer tools**: `cheat-engine-reference`, `on-wiz-reference`, `protocol-go`, `packet-map-reference`.
- **Farming**: `gardening-visual`, `wizapi-visual`, `bazaar-visual`, `farming-opencv`.
- **Game context**: `grubninja-reference`, `on-wiz-reference`, `raid-specialists`, `protocol-go`, `damage-evaluator`, `utilities-context`, `gallery-context`, `wiki-context`.
- **Navigation**: `automatus-v2`, `deimos-main`, `deimos-master`, `gardening-visual`, `raid-specialists`, `wizapi-visual`, `wizsdk-visual`, `wizwalker-client`.
- **Pets**: `deimos-main`, `deimos-master`, `grubninja-reference`, `petdance-visual`, `utilities-context`, `wizwalker-client`.
- **Quests**: `deimos-main`, `deimos-master`, `raid-specialists`.
- **Routine language**: `deimos-main`, `gardening-visual`.
- **Visual recognition**: `petdance-visual`, `wizapi-visual`, `bazaar-visual`, `farming-opencv`, `wizsdk-visual`.

## afk-wizard-main.zip

Candidate ID: `afk-visual`. SHA-256: `73eb31ecf475b775b2450fed22e72d4523f524671214dbca44808f5f23f1f1a8`.

Conditional battle cues and motion proposals.

Missing book is treated as battle; obscured frames can trigger false decisions. Unbounded loops and global input need replacement.

- Archive integrity: passed; parsed files: 1; parse failures: 0
- Image assets decode: Passed — 3 images opened; recognition accuracy not measured
- Battle state truth table: Passed — Logic only; an obscured/missing book still falsely implies battle

## Aurorium-main.zip

Candidate ID: `aurorium-assets`. SHA-256: `588d8669b43954f80a3478bf0d3dd36e2f084379b14b6c54daeda0c00c3022f7`.

Client asset/version provenance.

Downloader/server, not a game player. Offline build lacks roxmltree and potentially further dependencies.

- Archive integrity: passed; parsed files: 2; parse failures: 0
- Image assets decode: Passed — 0 images opened; recognition accuracy not measured
- Aurorium offline build: Build blocked

## Automatus-v2-master.zip

Candidate ID: `automatus-v2`. SHA-256: `e87cca6a6b0237ed56cb18c00c5acc1b7652435ad37f77a8c236ccd12e4bbb97`.

Waypoint route adapter and typed run statistics.

Project marked discontinued. Route following is not enemy avoidance. append_point/remove_point reference os without importing it.

- Archive integrity: passed; parsed files: 12; parse failures: 0
- Image assets decode: Passed — 0 images opened; recognition accuracy not measured
- Typed statistics: Passed — Counter accumulates; invalid category rejected

## cheat-engine-master.zip

Candidate ID: `cheat-engine-reference`. SHA-256: `58515a31b4f14fac8a92481180cfe69aa6d5ab4ea00e8a348ad4cb3b519af9ff`.

Inspection UX and diagnostic plugin patterns.

Lazarus/FPC build unavailable; drivers and invasive components are not part of Maelstrom's gameplay loop.

- Archive integrity: passed; parsed files: 7; parse failures: 0
- Image assets decode: Passed — 166 images opened; recognition accuracy not measured
- Application readiness: Not qualified — Lazarus/FPC toolchain unavailable; Pascal application and drivers not built or launched.

## Deimos-Wizard101-main.zip

Candidate ID: `deimos-main`. SHA-256: `0e4ce956b878a97c109dc950b6218f444584e7b0ff4d8e85cb418cf77ef73a89`.

Branching routine compiler and optional privileged teacher adapter.

Memory-backed dance labels must be tagged separately from pixel observations. Coordinate proximity and malformed deck handling have reproducible defects; client hooks unqualified.

- Archive integrity: passed; parsed files: 35; parse failures: 0
- Image assets decode: Passed — 1 images opened; recognition accuracy not measured
- Deck roundtrip: Passed — Two decks preserve names, sections and counts
- Deck malformed section count: Failed — IndexError: list index out of range
- Routine language compile: Passed — 3 programs compiled; no gameplay instructions executed
- Navigation geometry: Passed — Distance and a two-vertex graph fixture
- Navigation opposite coordinates: Failed — AssertionError: Opposite coordinates incorrectly treated as nearby

## Deimos-Wizard101-master.zip

Candidate ID: `deimos-master`. SHA-256: `47db55d7249edf3aa6f6a3cc31452503acfab8eef99aff045e4cdd3ff35365c6`.

Independent older workflow/navigation comparison.

Keep separate from main: dependencies, code and behavior differ. Coordinate proximity defect reproduced. Current-client hooks unqualified.

- Archive integrity: passed; parsed files: 27; parse failures: 0
- Image assets decode: Passed — 1 images opened; recognition accuracy not measured
- Navigation geometry: Passed — Distance and a two-vertex graph fixture
- Navigation opposite coordinates: Failed — AssertionError: Opposite coordinates incorrectly treated as nearby

## gardening-bot-main.zip

Candidate ID: `gardening-visual`. SHA-256: `ff395a786a86419614ac2c8f34ee322eefd6b4cdae9483a8bca2544cd904937a`.

Plant states, visual waits and home movement chains.

Use Maelstrom's action owner for execution: interrupted legacy key presses can remain held. Energy, planting and harvesting outcomes need separate qualification.

- Archive integrity: passed; parsed files: 14; parse failures: 0
- Image assets decode: Passed — 23 images opened; recognition accuracy not measured
- Movement order: Passed
- Interrupted key release: Failed — AssertionError: Interrupted legacy action left a key held
- Visibility and timeout: Passed

## GrubNinja-master.zip

Candidate ID: `grubninja-reference`. SHA-256: `1d4e5e7017e6a1ba79c3b11927476efb5a8a182bd422b057283993eccc2bc542`.

Grub Guardian maps and UI/state vocabulary.

validateSession.js contains a syntax error (stray closing bracket). Reviewed reward path generates scores and submits service calls; it is not a visual gameplay demonstration. Do not use reward responses as training outcomes.

- Archive integrity: passed; parsed files: 3; parse failures: 0
- Image assets decode: Passed — 27 images opened; recognition accuracy not measured
- Application readiness: Not qualified — Electron/React dependencies absent; lifecycle includes a global npm link. Reward endpoint calls are not gameplay evidence.
- Javascript parse: Failed

## libkiwad-master.zip

Candidate ID: `libkiwad-reader`. SHA-256: `e18f0dca7318f801ecf327d72b5645964422be0a363480af3078eeafd6442756`.

Alternative native asset reader.

Plain-entry read tested; compressed reads explicitly unimplemented. Initializer compares a non-terminated magic buffer and requires repair before use.

- Archive integrity: passed; parsed files: 0; parse failures: 0
- Image assets decode: Passed — 0 images opened; recognition accuracy not measured
- Kiwad compile: Passed
- Kiwad probe: Passed

## On-Wiz-master.zip

Candidate ID: `on-wiz-reference`. SHA-256: `06a1d5bd9cac380ee22cdec14ee2dfd1122b9c8f7a60ad29033ae69c27bc3802`.

Client/protocol research notes.

Documentation only. Historical command-line/service notes are not current compatibility evidence.

- Archive integrity: passed; parsed files: 0; parse failures: 0
- Image assets decode: Passed — 0 images opened; recognition accuracy not measured
- Application readiness: Not applicable — Documentation project; no application entry point to test.

## petdance.zip

Candidate ID: `petdance-visual`. SHA-256: `f705a4330b8a2cb0f862b48847d9c0db8a00781441f40dd29d1f5f84668b0911`.

Calibrated visual Pet Dance teacher and draft action pack.

Pet Dance teacher is available under Observe & teach: imports eight pinned images, calibrates scale and reviews recordings into proposal packs. Archive is TAR despite its name. Current W101 Watch/Go/results and live input remain unqualified.

- Archive integrity: passed; parsed files: 0; parse failures: 0
- Image assets decode: Passed — 8 images opened; recognition accuracy not measured
- Reviewed source synthetic sequences: Passed — 100/100 with cue deduplication; earlier source rehearsal, no live frames or bundled EXE execution
- Native app teacher pipeline: Passed — 40/40 synthetic scaled cues; 3 exact varied round proposals; 0 proposals on sparse footage; no inputs, model, or live-game accuracy claim
- Application readiness: Not qualified — Binary teacher not launched; source-equivalent images and reviewed source components tested separately.

## ProjectMaelstrom_win-x64_portable.zip

Candidate ID: `maelstrom-historical`. SHA-256: `04849b55f98bf79d9187ecd21f33e27070307c25ec1a139ae7bb09cd85e5b02d`.

Historical portable application comparison.

Preserved binary distribution; not executed or substituted for the current app.

- Archive integrity: passed; parsed files: 0; parse failures: 0
- Image assets decode: Passed — 0 images opened; recognition accuracy not measured
- Application readiness: Not qualified — Historical binary preserved; current app is tested separately. Historical application not launched.

## raid-utility-tools-main.zip

Candidate ID: `raid-specialists`. SHA-256: `b51e892c611f193048a171e36282338bf1ce8424cc72dccca2c0923afde56d0d`.

Raid activity vocabulary, location context and themes.

Four specialist variants retained. Qt/memobj/fork dependencies and client hooks not qualified; no raid operation or teleport tested.

- Archive integrity: passed; parsed files: 16; parse failures: 0
- Image assets decode: Passed — 4 images opened; recognition accuracy not measured
- Application readiness: Not qualified — Matching Qt, memobj and forked wizwalker environment and live client compatibility unqualified.
- Literal theme data: Passed — 4 dictionaries; no raid gameplay tested

## w101-proto-go-master.zip

Candidate ID: `protocol-go`. SHA-256: `8e73e6990cb8025ae1afce8edef9013ad2730900c2cec89e97b7888d85605c86`.

Typed protocol vocabulary, including minigame messages.

Build requires unavailable w101-client-go module. No traffic replay, login or reward calls run; schemas are not visual players.

- Archive integrity: passed; parsed files: 0; parse failures: 0
- Image assets decode: Passed — 0 images opened; recognition accuracy not measured
- Protocol go offline build: Build blocked

## wiz-packet-map-main.zip

Candidate ID: `packet-map-reference`. SHA-256: `686a277ccb2307527aaca37cb5655cd83204e4e1cc545baf23b89ccfed6546fb`.

Historical packet schema research.

Legacy x86 injection and client-specific offsets; MSVC/Crypto++ setup unavailable. No interception or game attachment performed.

- Archive integrity: passed; parsed files: 0; parse failures: 0
- Image assets decode: Passed — 0 images opened; recognition accuracy not measured
- Application readiness: Not qualified — Legacy x86 injection project requires its MSVC/Crypto++ build setup and client offsets; not attached to W101.

## wizAPI-master.zip

Candidate ID: `wizapi-visual`. SHA-256: `b2853c3b4197a334177c01277fbaba7204162173a2ef79e74f2083ad0c5d65fe`.

Visual spell and UI action alternative.

Non-square template center is wrong. Fixed 800x600/Regular UI/DPI assumptions, global activation and unbounded waits require adaptation.

- Archive integrity: passed; parsed files: 3; parse failures: 0
- Image assets decode: Passed — 14 images opened; recognition accuracy not measured
- Nonsquare match center: Failed — AssertionError: Template center swaps width and height
- Absent template: Passed

## Wizard101-Bazaar-Tc-Farming-master.zip

Candidate ID: `bazaar-visual`. SHA-256: `7115291770885b73be4c192516553e1ca24c118a8eb95ed4990a639edab92a22`.

Item/menu templates and conditional page-search proposals.

Image matching tested on fixtures only. Purchase loops are not enabled; missing-image coordinates and RGB/BGR conversion need adapter checks.

- Archive integrity: passed; parsed files: 2; parse failures: 0
- Image assets decode: Passed — 78 images opened; recognition accuracy not measured
- Image match and absent: Passed

## Wizard101-Damage-Calculator-master.zip

Candidate ID: `damage-evaluator`. SHA-256: `d0bea30c9d88bb3548c1fbd81b4931543a1f1b1361b75686260ef4081ab7d02e`.

Candidate consequence evaluator.

Pure library compiles under net9, but repeated calculations mutate damage; enchant/resistance/boost checks fail. Do not use it as decision truth yet.

- Archive integrity: passed; parsed files: 8; parse failures: 0
- Image assets decode: Passed — 1 images opened; recognition accuracy not measured
- Damage library probe: Passed
- Base damage: Passed
- Blade and shield: Passed
- Repeatability: Failed
- Flat spell enchant applies: Failed
- Zero pierce still applies resist: Failed
- Boost does not erase wizard damage: Failed

## Wizard101-Farming-main.zip

Candidate ID: `farming-opencv`. SHA-256: `71848d31dc7e0fa1730ec824f1e3bdd4a440e7500ede86085c80e7e561302415`.

Card matcher and enchant/attack proposal sequence.

Basic visual fixtures pass. Matching returns window-relative coordinates while input expects screen coordinates; fixed deck/order and indefinite loops remain.

- Archive integrity: passed; parsed files: 1; parse failures: 0
- Image assets decode: Passed — 6 images opened; recognition accuracy not measured
- Card match and absent: Passed — Fake activation/capture/output; no real window touched

## Wizard101-Utilities-master.zip

Candidate ID: `utilities-context`. SHA-256: `7fa75c58dc216f180dd40a7b17a81d4273a8b3d84303b6aa76910281343a77af`.

Gear/pet data and research schema helpers.

Historical data requires freshness checks. Scrapers and database population scripts not run against external services.

- Archive integrity: passed; parsed files: 19; parse failures: 0
- Image assets decode: Passed — 42 images opened; recognition accuracy not measured
- Javascript parse: Passed

## WizGallery-dev.zip

Candidate ID: `gallery-context`. SHA-256: `ffb7a6fe245ce640f24b38d01d2a742c9ee7f91e205608743181be93d8b53ba9`.

Screenshot library and location metadata patterns.

Database-backed web app. Dependencies/services absent; import-time database connections and migration scripts not executed.

- Archive integrity: passed; parsed files: 7; parse failures: 0
- Image assets decode: Passed — 4 images opened; recognition accuracy not measured
- Application readiness: Not qualified — Next.js dependencies and database credentials/services absent; server/migration scripts not started.
- Javascript parse: Passed

## wizSDK-master.zip

Candidate ID: `wizsdk-visual`. SHA-256: `f88867bc0962b895099a0183b3c594265709c7abe2a99b4a8ebbaa5d6a4fa783`.

Visual utility alternative and client control vocabulary.

Non-square visual match and blank fixture pass. Capture/input integration, resolution assumptions and client dependencies remain unqualified.

- Archive integrity: passed; parsed files: 13; parse failures: 0
- Image assets decode: Passed — 5 images opened; recognition accuracy not measured
- Nonsquare match and absent: Passed

## wizwad-main.zip

Candidate ID: `wizwad-reader`. SHA-256: `f4e9103b9e68421c581f9d1b3bc0bd1704e7b6849339d3eedc9735c275e79c8e`.

Python asset indexing alternative.

V1/V2 plain/compressed reads pass independent fixtures. Invalid magic is accepted; add strict header/size checks. Writer dependency absent; writer not tested.

- Archive integrity: passed; parsed files: 6; parse failures: 0
- Image assets decode: Passed — 0 images opened; recognition accuracy not measured
- Read v1 v2 plain compressed: Passed — Four independent format fixtures, both versions and compression modes; writer not tested
- Invalid signature rejected: Failed — AssertionError: Non-KIWAD signature accepted

## wizwalker-master.zip

Candidate ID: `wizwalker-client`. SHA-256: `265c50b940b937dc165aeeeb14f329a8eaba799cd006a000473900c0fea70568`.

Optional client adapter and WAD metadata reader.

V1/V2 journals tested. Client memory offsets and live hooks are a separate compatibility gate; no attachment performed.

- Archive integrity: passed; parsed files: 58; parse failures: 0
- Image assets decode: Passed — 0 images opened; recognition accuracy not measured
- Wad journal v1 v2: Passed — Journal parsing only; client-memory hooks and async extraction not exercised

## WizWikiAPI-main.zip

Candidate ID: `wiki-context`. SHA-256: `5d4635312eea86c4d30e1a48c19d9a0e8c2a85fbe35019edbb6b1253d7df7464`.

Game categories, world context and guide schema.

Historical world data parsed; README says API no longer works. TypeScript/compiler dependencies absent. No access-control workarounds attempted.

- Archive integrity: passed; parsed files: 4; parse failures: 0
- Image assets decode: Passed — 0 images opened; recognition accuracy not measured
- Application readiness: Not qualified — TypeScript dependencies absent; bundled README reports upstream API no longer works. Current service not queried.
- World context schema: Passed — 16 historical world records; freshness not established

## Reproduction and limits

Run `tools/audit_w101_legacy.py`, then `tools/verify_w101_legacy_components.py`,
`tools/verify_w101_legacy_builds.py`, and `tools/map_w101_legacy.py` using the
existing isolated Dance environment. Reports live in `.local/legacy-validation`.
Sources remain in hash-named directories; originals are untouched. Function
probes enforce `research/w101-legacy-reviewed-files.json` hashes. Build probes
operate on the reviewed local source copies; review changes before rerunning.

C/C# probes use constructed data, not the upstream desktop entry points. The C#
library is built under net9; this does not qualify its original net5 UI. A passing
probe process can report failing behavioral assertions; those are included above.
The Python WAD writer and full client integration are not qualified. Historical
documents/data have no gameplay runtime to test. JSX/TypeScript need their own
compiler dependencies; Node syntax checks are not web-app builds.
