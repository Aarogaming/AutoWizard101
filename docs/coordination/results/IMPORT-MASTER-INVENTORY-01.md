# IMPORT-MASTER-INVENTORY-01 — complete imported/reference project inventory

Status: **inventory only — no new imports, code, build, live or network work.**
Date: 2026-11-17 · owner: root · workstream: `CORE-REFERENCES`

## 1. What this repository is

The repo is a reconstruction of **Aarogaming/AutoWizard101**, now Maelstrom. First-party code lives in `ProjectMaelstrom/` (native C# app/tests), `tools/` (Python workbench), `crates/` (Rust workspace), `Installer/`, and the docs/coordination system. A stale first-party portable build is also archived as `ProjectMaelstrom_win-x64_portable.zip`. Everything below is third-party or older first-party source brought in for reuse/study.

## 2. Layer A — 25 tracked root archives (~330 MB)

| Archive | Upstream | Disposition |
|---|---|---|
| `afk-wizard-main.zip` | cadekenyon/afk-wizard | reference; cue concepts rejected as flawed |
| `Aurorium-main.zip` | Revive101/Aurorium | reference; manifest/provenance concepts |
| `Automatus-v2-master.zip` | Drayux/Automatus-v2 | partially imported: zone adjacency + candidate waypoint importer |
| `cheat-engine-master.zip` | cheat-engine/cheat-engine | UX/diagnostic reference only |
| `Deimos-Wizard101-main.zip`, `Deimos-Wizard101-master.zip` | Deimos-Wizard101 forks | two versions; combat/navigation reference; teleport excluded |
| `gardening-bot-main.zip` | Adarsh-S-Nair/gardening-bot | reference; timed loops not qualified |
| `GrubNinja-master.zip` | ashah360/GrubNinja | vocabulary only; reward endpoints excluded |
| `libkiwad-master.zip` | (manifest URL wrong — `Joshsora/libki`) | WAD reader reference; unsafe magic initialiser |
| `On-Wiz-master.zip` | latelylk/On-Wiz | docs only |
| `petdance.zip` | kennyhngo/Wizard101_DanceBot | reused: 8 arrow images + sequence teacher |
| `ProjectMaelstrom_win-x64_portable.zip` | first-party stale build | comparison only; should not be tracked long-term |
| `raid-utility-tools-main.zip` | lxghtend/raid-utility-tools | role/location data candidate |
| `w101-proto-go-master.zip` | cedws (manifest points at client-go) | schema vocabulary only; no protocol execution |
| `wiz-packet-map-main.zip` | xgladius/wiz-packet-map | **excluded** injection/decryption; separable data only |
| `wizAPI-master.zip` | GloXFX/wizAPI | visual teaching reference; defects reproduced |
| `Wizard101-Bazaar-Tc-Farming-master.zip` | NicolasMaskal/... | search-only reference; purchases policy-gated |
| `Wizard101-Damage-Calculator-master.zip` | Deimos-Wizard101/... | calculator reference; tests failed |
| `Wizard101-Farming-main.zip` | Dj-Liquid/Wizard101-Farming | combat teacher reference |
| `Wizard101-Utilities-master.zip` | taSilver/Wizard101-Utilities | knowledge importer candidate |
| `WizGallery-dev.zip` | andrearcaina/WizGallery | provenance/indexing reference |
| `wizSDK-master.zip` | UnderpaidDev1/wizSDK | geometry/wait helper reference; launcher gated |
| `wizwad-main.zip` | wizspoil/wizwad | read-only WAD candidate; invalid-magic defect |
| `wizwalker-master.zip` | wizwalker/wizwalker | **the one wired reuse** — pinned read-only adapter |
| `WizWikiAPI-main.zip` | R-unic/WizWikiAPI | historical world records imported to cache |

## 3. Layer B — `Scripts/Library` extracted imports (33 dirs, 35 manifests, 28 unique upstreams)

Tracked source copies that ship with the app (`ProjectMaelstrom.csproj:281` copies `Scripts\**` as content; excluded from C# compilation at line 52). These include the archive extractions plus bundled-only projects not in the 25-archive catalog:

- From the archives: afk-wizard, Automatus-v2, BazaarTCBot, Deimos-Wizard101, gardening-bot, GrubNinja, libkiwad, On-Wiz, raid-utility-tools (+ four role wrappers), w101-proto-go, wiz-packet-map, wizAPI, Wizard101-Damage-Calculator, Wizard101-Farming, Wizard101-Utilities, wizSDK, wizwad, wizwalker, WizGallery, WizWikiAPI, Aurorium.
- Bundled-only (no root zip): **Ambrose** (MIT trivia extension), **Arcane** (CC BY-NC-SA asset tool), **wizproxy** (ISC MITM proxy — reference-only/excluded), **wizard101-wad-reader**, **Wizard101Trivia**, and stubs **CrownsTriviaBot**, **SeleniumCaptchaBot** (no source, "needs manual clone"), plus first-party `SampleEcho`.
- Manifest URL defects: `libkiwad` → `Joshsora/libki` (wrong repo name); `w101-proto-go` → dependency repo, not itself.

## 4. Layer C — `.local/upstream` study clones (16, git-ignored)

`Wizard101_DanceBot` (petdance source), `moonlight` (offline packet decoder), `wizard101-companion`, `wizdiff`, `wiztype`, plus the eight approved batch-01 clones: `spiraldb`, `katsuba`, `wizard101-chat-translator`, `manual_wizard101_wizapelago`, `WizLaunch`, `Hivemind`, `wizbot`, and four batch-03 study clones `kinif`, `zhol`, `PAT.py`, `W101-PD-Script`. All are untracked references; HEADs and licences are recorded in `APPROVABLE-IMPORT-BATCH-01.md`; follow-up batch dispositions in `EXTERNAL-LIST-03-DISPOSITION-01.md`.

## 5. Layer D — `.local/reference-repos` peer/history clones (18, git-ignored)

Aarogaming repos: `AaroneousAutomationSuite`, `AndroidApp`, `Guild`, `Library`, `Maelstrom` (older), `Merlin`, `MyFortress`, `Workbench`, `aaroneous`, `aaroneous-devtools`, `opencode`, `chatterbox`, `alexa-skills-kit-sdk-for-java`, `circuit-simulator`, `Aarogaming.github.io`, `Wizard101_DanceBot` (owner copy) — plus third-party comparison engines `Airtest` and `MaaFramework`. Pattern-level reuse only (Steward receipts → scheduler; Workbench hashes → fixture checker). No verbatim copies or build dependencies.

## 6. Totals and hygiene

- **25 tracked root archives** (~330 MB; biggest: petdance 167 MB, stale portable 90 MB, two Deimos zips 42 MB)
- **28 unique library upstreams** (35 manifest records, 33 dirs, 4 wrappers around one repo)
- **16 study clones** + **18 reference clones** (git-ignored, not in git)
- **62 unique third-party repository identities** across layers B–D (URL-canonical union), with overlaps in the archive layer
- licence gaps: many bundled imports have no licence file; `Arcane` and `spiraldb` are CC BY-NC-SA; Catsuba/zhol/wizbot/etc MIT or GPL-3 as recorded
- policy exclusions: wizproxy, wiz-packet-map, w101-proto-go execution, Wizard101LuaCommandExecution, wiz_packets/o101-style server work, Crowns/CAPTCHA bots
- first-party stale portable zip and root logs (`bot_log.txt`, `tmp_ext.txt`) remain repo-bloat candidates awaiting owner cleanup direction

## 7. Interpretation

The imported ecosystem is intentionally **reference-heavy, integration-light**: one adapter (wizwalker) is genuinely wired, several pure algorithms were reimplemented natively after review, and the rest are retained for provenance/comparison. That is the correct posture for a system whose rules require qualification before trust. Full dispositions remain in `W101_LEGACY_WORK_PACKAGES.md`, `W101_LEGACY_QUALIFICATION.md`, `POST-PATCH-DOCTRINE-01.md`, `MINIGAME-ECOSYSTEM-DISPOSITION-01.md`, and the batch reports.
