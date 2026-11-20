# APPROVABLE-IMPORT-BATCH-01 — reference clones for study (not integrated)

Status: **REFERENCE CLONES IN GIT-IGNORED `.local/upstream` ONLY — no tracked source, no build, no install, no live use.**
Date: 2026-11-17 · owner: root · workstream: `CORE-REFERENCES`

## 1. What was done

Seven URL-verified (`git ls-remote`) projects were shallow-cloned into `.local/upstream` for study. These are **untrusted external sources**: no upstream instruction files (e.g. their AGENTS.md) were followed, no scripts were executed, and no code was copied into tracked paths. Network use was limited to Git clone/ls-remote; no live game or server interaction. One proposed project (`nick2906/Wizard101-Fishing-Bot`) was **not found** at either tested URL and was skipped.

| Project + verified HEAD | Files / size | Licence | Purpose | Disposition |
|---|---|---|---|---|
| `Revive101/spiraldb` @ `a0da1d1` | 13,429 / 73 M | **CC BY-NC-SA 4.0** | structured game/drop/zone data | **Study only.** Non-commercial + share-alike; no verbatim copy into our GPL repo until licence review. Data concepts may inform our own schema |
| `vbe0201/katsuba` @ `7158bf7` | 186 / 975 K | ISC | WAD/nav/collision format parser | **Study only** — we already have an independent BCD parser (`W101-SHAPE-ORDER-139`); compare behaviour, do not vendor |
| `GoneTone/wizard101-chat-translator` @ `60dda84` | 459 / 80 M | MIT | memory chat read + AI translation overlay | **Study only.** Cloud API keys and auto-update infrastructure are out of scope; local-model path only if ever revisited |
| `cloiss/manual_wizard101_wizapelago` @ `e49cccd` | 49 / 3.1 M | MIT | manual randomizer/tracker | **Study only.** Requires new character and progression manipulation; not an automation target today |
| `Deimos-Wizard101/WizLaunch` @ `e581a0b` | 16 / 253 K | **GPL-3.0** | client launcher + credential store | **Study only.** Credential handling needs its own review before any use; GPL compatible with our repo but no code copied |
| `Deimos-Wizard101/Hivemind` @ `3815a42` | 10 / 168 K | **GPL-3.0** | bot-to-bot coordination via in-game whispers | **Study only, flagged.** Whisper transport is server-visible game communication; integrating it needs explicit owner approval and a single-owner design |
| `MaroonCoffee/wizbot` @ `cced4e3` | 112 / 62 M | MIT | CV/OCR Bazaar Empower farming/purchasing | **Study only.** Our `bazaar_tc_sniper.py` already covers search; any purchasing must be a separate policy-gated transaction |

## 2. Risk flags

- **No secrets committed**: `.local` is git-ignored. Files named `credential_store.rs`, `.envrc`, `private.py` were not opened for values and are not in tracked paths.
- **Licences**: CC BY-NC-SA (`spiraldb`) and GPL-3 (`WizLaunch`, `Hivemind`) constrain redistribution. Isolated study is fine; integration requires a dedicated licence/provenance claim and owner approval.
- **Server-side caution**: Hivemind's whisper transport and WizLaunch's account credentials are game/server-visible surfaces; neither is authorized for integration by this batch.
- **Upstream instruction files were treated as data only**, never as directions.

## 3. Next gates

Nothing here is imported into the application. A separate claim is required to copy any specific file, with licence review, hash pin and tests. `docs/coordination/results/POST-PATCH-DOCTRINE-01.md` and `MINIGAME-ECOSYSTEM-DISPOSITION-01.md` remain the strategy records; this batch is the read-only source shelf backing them.
