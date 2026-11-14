# W101-STATS-53 — local-file layout review and bounded live qualification

Status: current-value qualification and opt-in integration; maximum/potion qualification remains pending. No installer or gameplay actions in this task.

## Local evidence

Installed files were read only. No patcher settings, game assets, account logs or network traffic were changed/read for this review.

- Executable: `C:/ProgramData/KingsIsle Entertainment/Wizard101/Bin/WizardGraphicalClient.exe`, SHA-256 `d410a2463e7ff0e221d6d11ca10fa53de265abd17cacfba211f9eeca8ec8511a`.
- Named `WizGameStats` property registrations in this executable explain why the imported stats offsets no longer agree: new fields intervene before several resources. Existing local MinGW objdump inspected narrow code ranges; no new dependency.
- Root.wad `GUI/HUDWindow.gui`: decoded 550,717 bytes; SHA-256 `36cdbc187f677713ba220398b306751c4f53d8ec27e06105b6e7de41bccb2585`. The read-only archive review read 13,256,934 bytes total. Declared archive CRC was not equivalent to ordinary CRC32; bounds, decoded size and XML parsing were checked, not a claimed matching CRC.
- Exact current-value paths beneath Root/WorldView/windowHUD are `spriteHealthBkg/HealthGlobeRect/textHealth`, `spriteManaBkg/ManaGlobeRect/textMana`, and `spriteEnergyBkg/EnergyGlobeRect/textEnergy`. `btnPotions` is a PotionButton without a numeric text child. Asset default values are not runtime readings.

| Named property | Object-relative candidate offset | Qualification |
| --- | ---: | --- |
| base hitpoints | 80 | named registration only |
| base mana | 84 | named registration only |
| energy maximum | 108 | named registration only |
| current hitpoints | 112 | repeated HUD agreement |
| current mana | 136 | repeated HUD agreement |
| potion maximum | 168 | candidate; float |
| potion charge | 172 | candidate; float, preserve partial charges |
| bonus hitpoints | 224 | named registration only |
| bonus mana | 228 | named registration only |
| bonus energy | 244 | named registration only |

Property registration adds class-base metadata to these constants. Runtime class-name recovery did not establish object type. Therefore offsets alone do not qualify output: current HP and mana must independently match the visible HUD in each snapshot, on this exact executable hash. Energy current comes from its exact HUD path. Existing imported raid tools support base-plus-bonus energy as a hypothesis, not independent live maximum verification.

## Live evidence

All observations ran under Maelstrom's existing scheduler and input owner, with serialized scoped root/stats hooks and verified restoration. No keys, clicks, focus changes, volume changes or desktop-control sessions. Installed files stayed read-only; scoped process hooks use the existing optional W101 exception.

Immutable local receipts: `.local/stats-53/probe-5.json`, `probe-6.json`, `probe-7.json`; their artifacts point to full bounded results.

| Probe | HUD/current HP | HUD/current mana | HUD energy | Candidate maxima HP/mana/energy | Candidate potions |
| --- | ---: | ---: | ---: | --- | --- |
| 5 | 3244 | 460 | 25 | 6973 / 543 / 106 | 4 / 4 |
| 6 | 2757 | 456 | 25 | 4920 / 543 / 106 | 4 / 4 |
| 7 | 4559 | 446 | 26 | 6973 / 543 / 106 | 4 / 4 |

Probe 6 correctly withheld executable resources because UI-to-stats age was about 2.38 seconds. Stopping the UI phase after two samples removed unused watch delay; probe 7's age was 0.872 seconds and cleanup succeeded. Its initial output included layout-supported maxima. Subsequent review and the user's report of W101's own health display changing prompted stricter scope: final code publishes current HP/mana/energy only. Maxima, percentages and potion values remain candidate evidence and are unavailable to automatic card scans. These observations do not diagnose a stale client, prove a cause for health changes, or establish all equipment/buff/character cases.

## Runtime contract

- CLI `ui-tree-watch --include-resource-context` is explicit opt-in; still requires the existing client-observation/UI-tree opt-ins and exact process identity.
- API `connect` accepts optional `enableResourceObservation: true`, default false. Shared `observe` and card-demand refresh use the same scheduler; disconnected/default sessions do not attach the stats hook.
- Cards request `health`, `mana`, `energy` (or their Current aliases). Only opted-in demand or bounded learning enables the resource scan. No separate poller. Existing refresh limits remain five-second cooldown, at most 30 attempts; bounded learning is 60 seconds.
- Snapshot `resources.currentValues` holds qualified current fields; `resources.values` stays empty until pairs have independent qualification. Unknown maxima never become percentages. Candidate inventory is not executable fact data.
- At most two UI samples, a one-second stats-export wait, two 992-byte candidate-block reads 100 ms apart, and bounded post-read loading check. This is a lightweight qualification path, not a high-frequency stats feed.
- Freshness is at most two seconds between UI and stats; preserve acquisition timestamps. Wrong builds, disagreement, loading/unknown, target change, cancellation or cleanup failure cannot qualify resources. The native boundary separately validates opt-in, schema, allowed fields and timestamps.

## Verification

Final current-only live probe: `.local/stats-53/probe-8.json`, Completed; current health 838, mana 427, energy 27; no maximum/percentage/potion facts; hookRestored and cleanupConfirmed true. The exact HUD/current matching gate passed.

Python: 70 observer tests and 3 resource tests passed; Black and compileall passed. Native final verification recorded below.

## Remaining gates

Verify maximum values against independent UI evidence during normal user gameplay; investigate temporary max-HP variation without assuming client age is its cause. Qualify potion charge/capacity separately. Broader arbitrary-field scans, high-speed multirate polling, automatic offset learning and general client-object identity are not delivered. Packaging/installation is outside this read-only review task.

## Expanded inventory after owner feedback

The [current-build named map](../../../research/w101-current-stats-layout.json) records 132 compiled properties with offset/name/current-registration-call evidence: 105 scalar candidates, 19 vectors, eight unresolved shapes/types. Type evidence is recorded separately from offset evidence. This is broader than the imported old-layout assumptions.

The bounded reader now decodes 76 explicitly selected scalar candidates from two 992-byte reads (1,984 bytes total; a 1,216-byte increase over the initial collector). It does not follow the vectors or arbitrary pointers. Currency amounts/caps, reference/scaled level, school ID, critical/block/damage/healing modifiers, pip/archmastery values, gardening/fishing/cantrip levels and XP, inventory-space bonus, and selected cooldown data are included. Social/account preferences are not decoded. Main character current XP remains unlocated: xpPercentIncrease is an XP bonus, not accumulated experience.

Each named field has a type, stable candidate value or unknown reason, source, and executable=false. Changed fields are withheld individually rather than causing unrelated stable stats to disappear. `snapshot.resourceCandidates` exposes these research observations through the existing API; candidate fields cannot become card conditions automatically. This is explicit inspectable data, not a claim of 76 verified gameplay facts.

Additional local semantic evidence: `Scripts/Tutorials/TutorialHealthMana.lua` uses GetCurrentAmount/GetMaxAmount on health/mana/energy globes. This confirms separate current/max concepts but not the offsets or a base-plus-bonus formula. Binary MaxHealth/MaxMana/MaxEnergy effect rules exist; no formula was fabricated from their tokens. Future independent max qualification can target globe values rather than guessing.

Offline regression details: wrong-build stats attachment rejected; signed/unsigned/byte-sized fields decoded separately; changed candidate gold withheld without dropping stable health/mana; candidates cannot become executable facts. Scoped native whitespace/analyzer checks passed. An earlier full run hit one scheduler timing timeout; the isolated eight-test scheduler suite and subsequent 2,598-test full run passed before the final candidate-projection test was added.

## Final expanded live check and validation

`.local/stats-53/probe-9.json`: Completed, two UI trees, five loading samples, 76 named fields, cleanupConfirmed=true/hookRestored=true. Qualified current health4920/mana543/energy29. Candidate examples: gold106452, referenceLevel106, gardeningLevel10/gardeningXP32700, fishingLevel4, cantripLevel1, schoolID2343174. These are captured observations, not independently confirmed field semantics.

Historical probe comparison exposes the maximum-HP ambiguity: probes5/7/8 have baseHitpoints4920 and bonusHitpoints2053 (sum6973); probes6/9 have baseHitpoints2867 and the same bonus2053 (sum4920). Thus base-plus-bonus is not yet a reliable cross-state maximum formula. No causal claim about client age, game display glitches or hook effects is established. The final production projection correctly withholds maxima.

Final checks: 2,599 native tests passed, warnings treated as errors; 70 observer Python tests and three resource-helper tests passed; scoped native whitespace/analyzer checks, Black, compileall and project-board validation passed. No packaging/installation, desktop control, game input, volume changes or game-file mutation. No live observer remains running.
