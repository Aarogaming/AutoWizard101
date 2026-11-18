# CI-PARITY-01 — Python/Rust/Node CI lanes and clean-checkout defects

Status: **COMPLETE — all lanes green locally and in a clean clone; CI not yet run on GitHub.**
Date: 2026-11-18 · owner: root · workstream: `CORE-DELIVERY`

## Delivered

- `.github/workflows/ci.yml` gained three jobs alongside the existing .NET lane, all on
  `windows-latest` (toolchain already shipped by the runner: Python 3.12, Rust/Cargo, Node 22 —
  no new dependencies, no setup actions):
  - `python-tests`: board gate → encoding gate → `python -m unittest discover -s tools/tests -p "test_*.py"`
  - `rust-tests`: `cargo test --workspace --locked`
  - `node-tests`: `node --test` in `integrations/node-red`
- Eight clean-checkout defects fixed (each would have failed CI on a fresh clone):

  | # | Defect | Fix |
  |---|--------|-----|
  | 1 | `test_analyze_pet_dance_audio` hard-imported NumPy | `skipIf` guard + decorator |
  | 2 | `test_detect_pet_dance_audio` hard-imported NumPy | `skipIf` guard + decorator |
  | 3 | `test_diego_vision` hard-imported NumPy/Pillow/cv2 | `skipIf` guard + decorator |
  | 4 | `test_diego_observer_ui` hard-imported Pillow/tkinter/cv2 | `skipIf` guard + decorator |
  | 5 | `test_route_landmarks` hard-imported OpenCV | `skipIf` guard + decorator |
  | 6 | `test_diego_coordination` subprocess ran `diego_report.py` (needs Pillow) | `skipUnless` on the one CLI test |
  | 7 | `test_boss_wiki_resolver` needed gitignored `wizwiki_companion.json` | `skipTest` when the data file is absent |
  | 8 | SHA-pinned readers broke under `core.autocrlf=true` | `.gitattributes`: `w101_zone_reader.py eol=lf`, `w101_moving_pose.py eol=crlf` |

- `tools/check_project_board.py` gained `--allow-missing-local`: a clean checkout may lack
  gitignored `.local/` evidence. Strict mode remains the default, so full-workspace runs still
  fail on missing evidence. Two tests cover both the strict default and the flag's scope
  (non-`.local` paths stay required even when set).

## Evidence

Commits: `cbcf790` (claim), `ff6f53d` (fixes).

| Check | Full workspace | Clean clone (`ff6f53d`, bare venv) |
|-------|----------------|-------------------------------------|
| Python suite | 906 passed / 8 skipped | 906 passed / **100 skipped** / 0 failed |
| Native suite (`-warnaserror`) | 3,117 passed / 0 failed | not re-run (lane unchanged) |
| `cargo test --workspace --locked` | 78 passed / 0 failed | 78 passed / 0 failed |
| `node --test` | 9 passed / 3 skipped | 9 passed / 3 skipped |
| `check_project_board.py` | valid (30 workstreams) | `--allow-missing-local` → valid; strict → exit 1 (expected, `.local/` absent) |
| `check_text_encoding.py --quiet` | 0 violations | 0 violations |
| Byte pins `w101_zone_reader` / `w101_moving_pose` | `1e7ddcfa…` / `785c4a62…` | **PASS / PASS** (was FAIL before the fix) |
| `black --check` (changed paths) | 9 files unchanged | — |
| `compileall` (changed paths) | clean | — |

Baseline before fixes, same clean clone: 849 ran / 1 failure / 7 errors / 38 skipped.

Notes:

- The bare venv has no NumPy/Pillow/OpenCV, so 100 tests skip instead of erroring; the full
  workspace still runs all 906 with only 8 skips. Nothing was deleted or weakened — the guards
  fire only on `ImportError`/missing data.
- Workflow YAML parsed and all four jobs enumerated; `pyyaml` used for the check.
- CI has **not** run on GitHub yet (history shows only dependabot/dependency-graph). First push
  to a `main`-targeting branch is the real confirmation.

## Not claimed

- No live game work, no input dispatch, no network calls, no new dependencies.
- `codex_convo.md` (user-owned, untracked) untouched.
