# Connecting the Wizard101 tools

Implementation and offline evaluation: October 30, 2026.

**Player interface added:** open `Launch Tools.cmd` or the trainer's **Tools & companions** entry. See [the user guide](TOOLS_USER_GUIDE.md). The in-app window owns process groups, exposes launch/stop and boss-library updates, and provides maintenance file pickers. The CLI details below remain useful for development; its standalone process ownership differs from the in-app supervisor.

The first integration uses separate processes and shared files. Each dependency stack lives in `.local/envs/`; downloaded upstream sources live in `.local/upstream/`. The existing app remains the owner of its native execution policy and OCR snapshot. No global Python environment was changed.

## What is connected now

| Connection | Implemented method | Verified result |
| --- | --- | --- |
| Companion → Maelstrom knowledge | Read-only SQLite export to `wizwiki_companion.json`; native loader merges names/locations/schools with its primary cache. | 1,851 active bosses exported from the supplied template database; primary entries win duplicate name/location matches. |
| Maelstrom → Python adapters | Atomic snapshot replacement and an environment-selectable Python reader with timestamp, focus, energy, potions and optional freshness validation. | Writer and reader tests pass. Existing third-party tools do not automatically consume this environment variable; their adapters must call the reader. |
| Isolated applications → common launcher | `tools/wizard101_hub.py launch`; argument arrays, explicit working directory, package metadata inspection, preview by default. | Companion/DanceBot command previews, offline Moonlight execution, and policy/locking tests pass. |
| Competing automation → one hub input owner | OS-backed file lease around hub-launched Deimos or DanceBot; live policy must explicitly allow automation in the experimental profile. | A second hub owner is rejected; releasing the lease permits a replacement. |
| wiztype → Moonlight | Generate format 1 type dumps; Moonlight accepts `--typedefs` pointing to that format. | A v1-shaped fixture is accepted by the installed `printrospector.TypeCache`; actual client dumps remain untested. |
| Saved wiztype outputs → maintenance report | `compare-types` normalizes v1/v2 by class hash and preserves property offsets for comparison. | Fixtures detect changed properties across dump formats. |
| wizdiff → maintenance report | `tools/wizdiff_adapter.py` calls `parse_records_from_xml` on local patch lists, then compares file records by `SrcFileName`. | A synthetic patch yields one added file, one removed file, and one CRC/size change. No patch-server traffic is required. |
| Moonlight → offline decoded JSON | Hub invokes a compatibility wrapper around the real upstream CLI. | A synthetic PCAP containing the upstream fake DML packet decodes to `MSG_PROTO1_FAKE`. No packets were captured or transmitted. |

The lease coordinates hub-managed automation only. Native Maelstrom bots, its legacy launch buttons, and independently started programs do not participate. Do not treat this as a global input scheduler. Ctrl+C attempts to stop the owned process tree; forcibly killing the hub itself is not a tested recovery path.

## Overlaps and recommended ownership

| Overlap | Programs | Recommended method |
| --- | --- | --- |
| Questing/farming | Deimos, wizSDK, Maelstrom native bots | Evaluate Deimos as the primary external backend. Retain native specialized bots as explicit alternatives. Reuse SDK ideas through adapters after resolving its API gaps. |
| Navigation/client state | Deimos's wizwalker fork, standalone wizwalker, wizSDK | One backend session should own each client. Do not share Python client objects across environments; exchange versioned JSON snapshots or task results. |
| Pet training | Deimos `auto_pet`, wizSDK routines, visual DanceBot | Choose one implementation per run. DanceBot is a visual alternative, not a second worker to run alongside Deimos's pet routine. |
| Screen reading | Maelstrom OCR, Companion EasyOCR, DanceBot image classification | Share general health/resource snapshots where possible. Preserve specialized boss OCR and arrow classification; they solve different recognition tasks. Companion OCR packages/models are not installed in this initial core environment. |
| Quest progress | Companion tracker, Deimos quest state | Treat Companion as user-facing planning/progress data. Deimos's active quest state needs an explicit adapter before it can update the tracker. Automatic progress synchronization is not implemented. |
| Boss/gear/deck information | Companion, Maelstrom cached knowledge | Use Companion as the detailed planning interface; export a read-only boss index for Maelstrom. Native code currently consumes name/location/school/type only; extra cheat/health/source details remain in exported JSON. |
| Client updates | wiztype, wizdiff, Moonlight | Compare patch files with wizdiff, compare type dumps with the hub, then decode suitable offline captures with Moonlight if needed. Their outputs complement each other. |

## Concrete upstream methods identified

These are integration seams found in source, not a promise that calling them is compatible with the current game.

| Source | Methods or interfaces | Use and limitation |
| --- | --- | --- |
| Deimos `src/questing.py` | `Quester.read_quest_txt`, `get_client_quests`, `auto_quest_solo`, `auto_quest` | Active quest state and orchestration. Requires attached clients and the Deimos fork stack. |
| Deimos `src/auto_pet.py` | `navigate_to_pavilion_from_commons`, `navigate_to_dance_game`, `nomnom`, `auto_pet` | Navigation and pet workflow. Alternative to the visual DanceBot. |
| wizwalker | `ClientHandler`, `Client`, `Client.activate_hooks`, `Client.teleport`, `Client.goto`, `Client.send_key` | Low-level backend seams; compatible symbol imports were checked for Deimos's installed fork. No client was attached. |
| wizSDK `wizsdk/client.py` | `Client.register`, `get_quest_xyz`, `teleport_to`, `face_quest_destination`, `get_battle` | Legacy facade. `teleport_to` expects backend `move_lock`; facing expects `set_yaw`. Those methods are absent from the installed wizwalker 1.9.0 class. Movement requires porting, not merely successful installation. |
| DanceBot `main.py` | `setup_game`, `play_game`, `finish_game`, `on_press` | UI/input workflow methods imported successfully without running them. Upstream starts its own keyboard listener and input loops during actual use. |
| Companion `database.py` | `get_connection`, boss table, `export_guide_code` / `import_guide_code`, counter share-code methods | The implemented bridge reads the boss table directly in SQLite read-only mode. Share codes are a possible future route for guides/counters. |
| Companion `database_gear.py` | `export_loadout_code`, `import_loadout_code` | Base64-encoded JSON share format; useful for future explicit loadout exchange. No gear data is written by this integration. |
| wiztype | `JsonTypeDumperV1`, `JsonTypeDumperV2`; CLI `--version` and positional output | Format 1 matches Moonlight's current type cache. Format 2 supports maintenance comparisons in the hub. |
| wizdiff | `parse_records_from_xml`; `UpdateNotifier.notify_revision_update`, `notify_all_file_update` | Offline file-list parsing is implemented. A future network monitor can subclass notification hooks; polling and automatic updates are not enabled. |
| Moonlight | `PacketReader.decode_ki_packet`; CLI `decode pcap`; `printrospector.TypeCache` | Tested on synthetic unencrypted data. Current game traffic decryption is outside the tool's capabilities. |

## Installed environments and fixes

Seven local Python 3.12 environments were created: `companion`, `dance`, `deimos`, `legacy`, `moonlight`, `wiztype`, and `wizdiff`. `legacy` is shared only by the standalone SDK evaluation and its own wizwalker distribution; it is separate from Deimos's fork.

- **Companion core:** PyQt5 5.15.11 plus local database and basic parsing dependencies. An offscreen UI was constructed and loaded 1,851 boss names. Global hotkeys, overlays, OCR and the delayed update check were disabled for that smoke test. EasyOCR/model downloads and wiki-fetch dependencies remain optional and untested.
- **DanceBot:** NumPy 1.26.4 and Pillow 10.4.0 replace its original Python-3.12-incompatible pins; OpenCV 4.8.0.76 is retained. Source workflow methods import successfully. The original full requirements list includes build-only packages and was not installed wholesale.
- **Deimos:** documented requirements installed, including wizwalker fork commit `df2283f146748d74b2229d32c08233c20ce8bb3d` and wizsprinter fork commit `222fc7a85d2a4b1578cc895bbf97af446c6dbce9`. Client, navigation and combat symbols import. Deimos's full GUI and active tasks are not validated.
- **wizSDK:** fixed bundled metadata from invalid `^1.x.x` to `>=1.8.1,<2` and corrected its author/source URL. Version 1.5.0 installs with standalone wizwalker 1.9.0 and imports, but its movement API gaps remain as described above. The bundled reference snapshot of wizwalker 1.8.1 was preserved.
- **Moonlight:** version 0.4.1 requires source dependencies unavailable in a normal package-only install. Installed `printrospector` from commit `89eb2e50e209bc247b7aed6163eb0923e59ca19c` and `katsuba` from its declared commit `36e296f6c6ab21459edbd61d8ba333c950e9ac44`. `moonlight_adapter.py` supplies Scapy's former `.next()` alias within the wrapper process, leaving upstream source unchanged. Offline PCAP decoding then passes. A live capture driver was not installed.
- **wiztype 0.1.13:** installed; CLI help works. No live type dump was attempted.
- **wizdiff 0.2.1:** installed; offline parsing and file comparison work.

Exact local package versions and containing repository commits are recorded by `tools/record_wizard101_environments.py` in `.local/artifacts/environment-snapshot.json`. This is an evaluation snapshot, not a portable installer lockfile. The bundled SDK metadata edit is a local change on top of the containing repository commit.

## Use the hub

From the repository root:

```powershell
python tools/wizard101_hub.py status
python tools/wizard101_hub.py overlaps

# Preview only; add --execute to open Companion.
python tools/wizard101_hub.py launch companion

# Export to a NEW output file. Existing outputs are never overwritten.
python tools/wizard101_hub.py export-companion .local/upstream/wizard101-companion/boss_wiki.db --output .local/boss-export-next.json

# Compare saved type dumps offline.
python tools/wizard101_hub.py compare-types old-types.json new-types.json --output type-changes.json

# Compare saved patch file lists with the isolated wizdiff parser.
.local/envs/wizdiff/Scripts/python.exe tools/wizdiff_adapter.py old-files.xml new-files.xml --output patch-changes.json

# Preview a format-1 dump for later Moonlight use.
python tools/wizard101_hub.py launch wiztype --output .local/artifacts/client-types-v1.json

# Decode a suitable LOCAL capture; preview unless --execute is supplied.
python tools/wizard101_hub.py launch moonlight --definitions path/to/dml --capture path/to/input.pcap --output .local/artifacts/decoded.json --typedefs .local/artifacts/client-types-v1.json
```

A Companion boss index has already been exported to `ProjectMaelstrom/ProjectMaelstrom/Scripts/Data/WizWiki/wizwiki_companion.json`. It is local generated data excluded from Git. A fresh build copies it with the `Scripts` content. For an existing installation, place an explicitly reviewed export at `Scripts/Data/WizWiki/wizwiki_companion.json` (the old `Scripts/Library/WizWikiAPI-main/` location is still read as a fallback), then use Settings → Refresh Wiki Cache or restart. Location text is retained as supplied; this export does not derive navigable world/area routes.

For Deimos/DanceBot execution, the hub additionally requires `--policy` pointing to the actual app installation's `Scripts/.cache/execution_policy.conf`. The hub does not edit that file. A disabled or academic policy blocks automation, even when `--execute` is given. Libraries (`wizwalker`, `wizsdk`, `wizdiff`) have no fabricated GUI launch command; wizdiff uses its explicit offline adapter.

If tools are installed elsewhere, `.local/wizard101-tools.json` may override a tool's `source` and `python` paths. It is a JSON object keyed by hub tool name, for example `{"companion":{"source":"D:/Tools/Companion","python":"D:/Tools/Companion/venv/Scripts/python.exe"}}`.

## Verification and remaining work

- Native app builds and all 43 .NET tests pass, including source merge and snapshot replacement tests.
- All 15 hub tests pass, covering read-only export, schema rejection, output preservation, dump normalization, patch changes, input ownership, policy, preview behavior, freshness and the Moonlight iterator shim.
- Changed C# files pass `dotnet format --verify-no-changes` including analyzers. Original unrelated formatting issues remain in the repository baseline.
- Offline smoke checks cover Companion UI/database construction, DanceBot workflow imports, Deimos backend symbols, legacy SDK imports/API-gap detection, and Moonlight synthetic packet decoding. Wiztype CLI help and the real wizdiff parser were exercised.
- No Wizard101 client process was found during evaluation. Active questing/farming, dance timing, memory offsets, game focus handling, resource guards and actual type dumps remain unverified.
- The hub remains available as a developer entry point. The player-facing Tools window now owns its launched process groups and pauses the trainer's task pump/input bridge while open. Guided questing, current-entrance farming and pet-dance adapters are now available; see [Guided activities](GUIDED_ACTIVITIES.md). Cross-instance native/hub scheduling, automatic Companion quest-progress exchange and additional minigames remain future work.

Primary source URLs and baseline findings are recorded in [the restoration assessment](WIZARD101_RESTORATION_ASSESSMENT.md). Local adapters and offline tests are in `tools/`; dependency/source copies stay in `.local/`.
