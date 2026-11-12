# Legacy review validation — 2026-11-08

Installed release: `0.2.0-preview.20261108-legacy-library`.
Open **Wizard101 → Legacy library** to filter 25 distinct source candidates by
capability and inspect their evidence. The library is a read-only integration
map; it does not install, launch or silently enable the old bots.

## Coverage and results

- All 25 supplied root archives passed container integrity checks. Original
  archives are unchanged; 2,682 selected source/asset files were copied into
  hash-named review directories. Generated releases and nested reference/dependency
  archives are separately classified in the earlier 88-item inventory.
- 238 Python/JSON/TOML/XML parses passed, with no failures. Compiler JSON-with-
  comments files are explicitly deferred to their own parser, not called corrupt.
- 383 image assets opened successfully. This verifies decoding, not recognition.
- 20 selected Python behavior probes: **14 passed, 6 failed**. Failures reproduce
  malformed deck handling, the same coordinate-sign bug in two Deimos revisions,
  interrupted gardening key release, wizAPI's non-square match center and
  wizwad's missing magic validation. Source execution is hash-pinned and excludes
  original entry points; image, input and clock doubles replace desktop activity.
- C library probe: plain-entry reading passes; compressed reading explicitly
  reports unimplemented. The unsafe magic-string initializer was identified in
  source and deliberately not exercised. The C library compiles with a warning
  for an unused read count, which also indicates incomplete read validation.
- Damage-library probe: compiled under net9; **2 checks passed, 4 failed**.
  Repeated calculations mutate damage, flat-spell enchant is ignored, zero-pierce
  resistance is omitted, and boss boost overwrites wizard damage. This is not a
  test of the original net5 desktop application or current-game formulas.
- JavaScript parsing found a stray `]` in GrubNinja's `validateSession.js`.
  Its JSX files need a JSX parser. Utilities' seven JavaScript files and the
  gallery's one copied JavaScript config parse; those are not complete app builds.
- Rust and Go builds were attempted offline with one worker: missing cached
  `roxmltree` and `w101-client-go` dependencies block them. Other application
  requirements and limitations are listed per source in the matrix.
- Earlier Pet Dance source rehearsal: 100/100 synthetic sequences with cue
  deduplication. Scale experiment: 47/48 accepted correctly, one uncertain,
  four negative controls rejected. No live recognition accuracy is claimed.

Full application/runtime qualification is **incomplete**. The native binaries,
memory-backed clients, database services and live account workflows were not run.
Passing a source parser, component fixture or build never grants live status.

## Maelstrom integration points

| Legacy contribution | Existing Maelstrom destination | Adaptation still needed |
| --- | --- | --- |
| Pet Dance, wizAPI, wizSDK, farming and Bazaar images | `VisualStateRecognizer`, teaching recordings, game-pack assets | Window-relative regions, scale calibration, negatives and independent current-game recordings |
| Deimos language, gardening waits, Automatus routes | `W101Routine`, `tools/game_packs.py`, replay and shared action registry | Translate reviewed conditions/proposals into bounded data; do not run an imported VM against the game |
| Legacy teacher actions | `GameplayInputRecorder`, `GameplayInputTimeline`, `PassiveTeachingSampler` | Instrument teacher identity and dispatch, distinguish pixels from privileged labels; share the input lease with Auto-Idle |
| Damage/gear/pet context and Wiki schemas | `GameResearch`, editable pack context | Repair evaluator defects; date and verify source claims before treating them as decision facts |
| Alternative policies and teachers | `GameExperiments`, pack comparison tools | Frozen variants, fresh trials, uncertainty and failure metrics; no training/evaluation leakage |
| WAD readers and Aurorium | Asset/version provenance tools | Strict format limits, source licenses and current-render matching; do not replace the client runtime |
| Navigation candidates | Planned destination layer over bounded actions | Local avoidance, progress/arrival verification, alternate routes and native Idle restoration |

These are binding destinations and adapter requirements, not claims that all
legacy behavior is already wired into the execution loop. Multiple candidates
remain visible under their own IDs instead of replacing one another.

## Application and release checks

309 application tests passed; 29 Python review/pack/Workshop tests passed.
Formatting, Python lint and C# analyzer build completed. Both portable and
installed offscreen renders contain all four Wizard101 tabs and 25 legacy entries.
Auto-Idle, home confirmation and Start are off; no session is active. These
checks did not show or activate a desktop window.

Installer lifecycle passed install/update/uninstall with preservation of pack,
report, recording, video, experiment, decision, footage, home report, routine and
Auto-Idle sentinels. The real update retained the existing desktop shortcut and
separate user-data directory. No existing saved routine/Auto-Idle files were found
by the optional real-update hash check; their preservation guarantee here is
backed by the lifecycle sentinels, not a claimed comparison of nonexistent files.

Evidence directory: `.local/legacy-validation/` (`audit.json`, `components.json`,
`builds.json`, language-specific logs, test TRX, `installer-lifecycle.json`,
`installed.json`, and `installed-ui/home-movement-ui.json`).

Portable: `.local/releases/Maelstrom-20261108-135135-7d0804.zip`.
Installer: `.local/releases/Setup-20261108-135226-eb6789/MaelstromSetup.exe`.

No game input, continuous recording or model inference was used in this review.
W101's user-set native Idle state was left untouched. The installed check detected
one W101 window; it did not inspect the Idle banner or prove an idle-timeout
duration. Future live tests must restore and verify Idle through the screen menu.
