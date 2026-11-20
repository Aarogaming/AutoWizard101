# Pet Dance transitions and Conjuration recognition — offline checkpoint

Date: 2026-11-09, America/New_York. Three subagents worked on Dance, Conjuration and independent evidence/review; the coordinator integrated the adapters and kept builds serialized. No W101 interaction, live capture, model hosting, volume change, feeding, installation or new dependency was used.

## Delivered

**Dance:** a receipt-aware full-visit adapter over the shared routine runner, with data-only `pet-dance-visit.json`. Entry can begin at a qualified sigil or instructions, including an already-selected level. It checks target/frame/arena/cost evidence, waits through loading, requires explicit completed/released specialist evidence, then observes result → Finish without feeding → outside → menu → native Idle. Initial Idle never wakes or completes a visit. This adapter remains offline; the live launcher is unchanged.

Saved OCR distinguishes instructions from other screens while preserving uncertainty about selected arena. A separate bounded matcher uses two actual enabled/disabled Play crops, anchored to OCR bounds, with a winning-margin and absolute-quality threshold. Readable Play text alone cannot authorize starting. Current saved imagery does not qualify automatic selected-arena recognition.

A final bounded review extracted eight frames from the existing local recording, with one encoder thread and no live capture. A frame at encoded time 18.466667 seconds shows a persistent bright rim on the first arena after its tooltip has disappeared and the pointer has moved to Play; the instructions close by 18.60 seconds. This is stronger selection-candidate evidence than a hover-only screenshot. The input/video timing and client-to-recorded-image registration remain unqualified, so it does not supply a verified selection or a run-bound action receipt. Exact source details and hashes are preserved in the independent audit linked below.

**Conjuration:** a normalized-region/frozen-gallery recognizer produces the existing `Board`/`Card` records, with per-card confidence and temporal evidence. A bounded image replay feeds those observations to the memory helper, preserving explicit receipts and outcomes. No LLM or online service participates. Phase, group-size and current-client reference qualification remain separate inputs.

**Reusable workflow:** strict observation fixtures, original source hashes, immutable image snapshots, bounded replay reports and task/evidence updates. New recorded-image checks bind PNG header dimensions and hashes to Dance OCR observations without decompressing untrusted full images. That verifies metadata/provenance, not independent OCR or full raster integrity.

## Integrated checks

| Check | Result |
| --- | --- |
| Complete native suite | **1,063 passed**, zero skipped, warnings-as-errors; two CPU workers |
| Scoped native whitespace/analyzers | Passed |
| All Conjuration Python suites | **60 passed**, zero skipped; existing isolated environment, one numeric-library thread |
| Python Black/syntax | Passed on changed files; formatter run one file at a time |
| Project board | 30 workstreams validated; 22 validator tests passed |
| Native synthetic full visit CLI | Completed contract; 6 proposals and 6 supplied simulated receipts; 0 inputs |
| Native recorded-entry OCR CLI | Correctly incomplete; instructions observed but no qualified level selection/control; 0 proposals and 0 inputs |
| Conjuration image CLI | 16 synthetic frames; 4 proposals, 4 supplied simulated receipts, 2 observed matched groups; 0 inputs |

Full native test duration was about 7 seconds; combined Conjuration tests took about 1.4 seconds on this development machine. Neither is a real-time gameplay or capture-throughput measurement. The initial focused Dance run found transparent edges introduced by synthetic GDI resizing; the fixture was corrected to use an opaque backing, and production opacity/quality checks were retained.

Independent review also found and repaired retired Conjuration board IDs accepting old revisions, image hashes describing bytes other than those decoded, strict JSON/profile issues, disabled-visit status inconsistency, receipt-ID schema length, and recorded-source geometry mismatches. These have targeted tests.

Evidence is under `.local/parallel-recognition-20261109/`:

- `tests/parallel-recognition.trx` — complete native test receipt.
- `dance-synthetic/visit.json` and `report.json` — explicitly synthetic complete-visit trace.
- `dance-recorded/visit.json` and `report.json` — two unchanged recorded instruction images and supplied saved OCR. Timestamps are synthetic replay offsets, not reconstructed original capture times.
- `conjuration-pixels/manifest.json` and `report.json` — self-contained generated image episode and source hashes.
- `selection-seek/` — eight bounded frame extracts from existing Dance video; candidate evidence only.

The [independent audit](coordination/results/PARALLEL-TRANSITIONS-RECOGNITION-20261109.md) preserves source hashes and qualifications. The [Conjuration result](coordination/results/CONJ-PERCEPTION-01.md) documents contracts, commands and resource budgets. The [Dance entry document](PET_DANCE_ENTRY_TRANSITIONS.md) documents measured crops, gates and remaining selection evidence.

## Readiness

The code can rehearse these workflows without the assistant operating menus. It is not yet a qualified full live player. We still need independently distinguishable Dance arena-selection evidence and actual Conjuration board/card examples before treating those paths as ready for live control. Existing automatic Idle freshness/cleanup repairs also still need a fresh authorized live qualification.

These source changes were not packaged or installed. The board tracks them as integrated offline with the remaining gates explicit; earlier installed results and synthetic tests remain separate.
