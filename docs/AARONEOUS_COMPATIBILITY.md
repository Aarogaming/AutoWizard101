> Historical coordination material. The current direction and plan are in [the standalone profile](MAELSTROM_STANDALONE_PROFILE.md). Aaroneous integration and further handoffs are not prerequisites.

# AutoWizard101 producer seam for Aaroneous

Coordination revision 1, October 30, 2026. **Producer-side documentation and fixtures; host contract adoption pending.** This note is the AutoWizard101 home for this first offline seam. A transferred copy should be pinned by source revision and file digest. Aaroneous retains ownership of its own admission, capability registration, presentation and effect interfaces.

## Return mapping received

The user supplied `MAELSTROM_RETURN_HANDOFF.md` from the Aaroneous window. It reports host inspection at `a3383f2feeeee32a47bcd11e34698cfc0d138131`. Its structure proposal identifies candidate UniversalTool and UiCartridge interfaces, a HUD registry without an established production mount, and future Marionette/Platform Bridge, Merlin, Library and Guild/Steward responsibilities. This workspace has not independently reviewed those host sources. Their interfaces and qualification remain attributed to that return handoff, not newly adopted product policy.

A verbatim local copy is retained at `docs/coordination/MAELSTROM_RETURN_HANDOFF.md` so the proposal does not depend on the original temporary visualization path.

We align with the proposed **saved JSON → read-only Rust importer → presentation** slice. We do not allocate an opcode, add a host envelope, call the existing autonomous game policy, or migrate the working .NET/Python backends. Future control must establish actual ownership of every input sender. The host's luminance grid is not a replacement for the rich observer report.

## Deliverables in this repository

- `tools/diego_report.py`: headless producer using existing OpenCV/NumPy/Pillow dependencies. It does not import tkinter or the desktop UI, capture a screen, or send input. The original `diego_observer.py` entry point remains unchanged for existing consumers.
- `tools/fixtures/diego_observer/v1/`: synthetic frames, stable example pack, valid/invalid reports and SHA-256 manifest. It includes a standalone single-frame report and a three-frame tracking sequence. Nothing in this set is historical video footage or a private recording.
- `tools/check_diego_report.py`: dependency-free local fixture checker. Its bounds and unknown-field behavior are explicit producer test choices; the Aaroneous consumer must review and enforce its own admission rules.
- `tools/build_diego_fixtures.py`: rebuilds deterministic fixture artifacts into an empty folder. The fixture version changes when pinned bytes/expectations change. OpenCV or image-encoding changes may require comparing results rather than assuming bytes remain stable across dependency versions.
- `tools/export_coordination_source.py`: preserves tracked changes, present changed/untracked source files and their hashes under a new `.local/` destination. It inventories selected required artifacts without bundling ignored environments or tutorial media.

## Existing report shape — preserved, not silently versioned

The observer payload currently has **no schema-version, session, sequence, digest or clock field**. `v1` names the fixture set and this shape snapshot, not a runtime field. The JSON `version` inside `pack/examples.json` versions only that example-pack format. Do not treat it as a report or host-envelope version.

| Field | Existing meaning |
| --- | --- |
| `mode` | Literal `offline-observer` |
| `inputSent` | Boolean false; no execution receipt or grant is implied |
| `sourceSize` | `[width, height]` integer image dimensions |
| `playArea` | `[x, y, width, height]`, source-image pixel coordinates; origin is top-left, x right, y down |
| `exampleCount` | Count of loaded reference examples |
| `similarityThreshold` | Matcher threshold; currently default 0.94 |
| `detections` | Objects containing `label`, `box` in the same pixel coordinates, and `similarity` |
| `tracks` | Objects containing `label`, `box`, `consecutiveFrames`, and `relativeMotion`: `unknown`, `approaching`, `receding` or `steady` |
| `suggestions` | Human-review strings, never executable HID commands |
| `spawnSequenceAssumed` | Boolean false |
| `limitations` | Explicit explanatory text; retain and display it |
| `syntheticDemo` | Optional CLI/demo indicator. It is true for bundled synthetic fixtures; false for ordinary CLI file input, which cannot itself verify provenance |

Rectangles cover `[x, x + width)` and `[y, y + height)`. Detection and track boxes must lie within the play area, which must lie within the source image. Similarity is a finite matching score, not a probability. Detection ordering is not stable semantic identity. Tracks have no persistent IDs, velocities or timestamps.

Single-frame CLI reports have empty tracks and no temporal corroboration. Their suggestions remain unconfirmed candidates. An empty detection list means nothing recognized, not a scene known to be clear. Resource state and verified gameplay outcomes are absent and must not be inferred as zero, healthy or successful.

The producer checker accepts additional fields and returns them unchanged, but assigns them no authority. It rejects missing required fields, live/input-enabled modes, invalid geometry, non-finite or out-of-range numbers, malformed JSON, duplicate keys and reports over 256 KiB. It caps image dimensions at 16,384 and list/string sizes. These are local checker bounds, not independently agreed host limits.

## Fixture manifest and import expectations

`manifest.json` has `fixtureSetVersion: 1`, `syntheticOnly: true`, `payloadVersionField: null`, per-file byte counts/SHA-256 digests and cases with `accept`/`reject` expectations. Verify this identity before using pinned fixtures. Missing/unsupported **fixture-set** versions should be rejected by the bundle loader. No runtime payload version can be checked until a reviewed mapping/envelope is agreed.

Valid cases cover synthetic subjects, current detections across changed positions, explicit unknown, a single-frame report, and preservation of an extra read-only field. Invalid cases cover input-enabled/wrong mode, missing limitations, boolean dimensions, boxes outside the play area, invalid similarity/track counts, malformed JSON, duplicate keys and non-finite JSON numbers. The size-boundary case is generated in tests rather than checking in a large dummy file.

The existing vision tests exercise randomized enemy order, disappearance, ambiguity, repeated inspections and skipped/backward frames. Session/sequence admission, clock domains, cancellation of host import, rendering escape rules and effect idempotency are **host/future-envelope work**, not capabilities established by this fixture set.

## Reproduce the first slice

From the repository root on the prepared Windows checkout:

```powershell
.local/envs/dance/Scripts/python.exe tools/diego_report.py --pack tools/fixtures/diego_observer/v1/pack --frame tools/fixtures/diego_observer/v1/frames/frame-0000.png --output .local/coordination/report.json
.local/envs/dance/Scripts/python.exe tools/check_diego_report.py .local/coordination/report.json
.local/envs/dance/Scripts/python.exe -m unittest discover -s tools/tests -p test_diego_coordination.py -v
```

The ordinary file-input CLI sets `syntheticDemo` false because it cannot establish the image's origin. Preserve the fixture manifest's independent synthetic provenance; do not let that CLI indicator override it. `--demo` marks the producer's own generated demonstration true. A future provenance system should verify bytes against a trusted artifact record rather than trust a boolean.

The pure JSON checker needs only Python's standard library. Producing reports needs the existing isolated vision stack; this change installs no packages and embeds no Python in Cargo. It has not been tested on Linux here. Source images, pack, script/interpreter and output path should remain explicit arguments when a future supervised sidecar is qualified.

## Dirty-source preservation and artifact transfer

The repository's base commit remains the one recorded in the incoming handoff; substantial work is uncommitted. Generate a local transfer snapshot after edits settle:

```powershell
.local/envs/dance/Scripts/python.exe tools/export_coordination_source.py --output .local/coordination/source-snapshot-001
```

This creates `tracked-changes.patch`, `working-tree-files.zip` and `inventory.json`, with source hashes and the base revision. It refuses an existing destination and detects concurrent path/content changes. Inspect its deletion/unavailable records before reconstruction. The ZIP contains local source changes, not a full Git checkout or historical commit chain. Do not blindly apply a patch over unrelated work; review it against the recorded base.

Ignored packages, executables, private data and historical tutorial media are not silently transferred. Local tutorial `provenance.json`, the environment snapshot and fixture-manifest hashes are inventoried when available. A consumer can start from the checked-in synthetic fixtures without any ignored tutorial assets. Separately qualify package versions/licenses and required ignored artifacts before a runnable-backend transfer.

## Requested Aaroneous follow-up

Use the synthetic fixture bundle to implement and independently test one read-only importer in a separately claimed host scope. Preserve the full report and limitations, identify its source digest, and render the disposition as offline observation/review required. No action conversion or autonomous game policy should run in this slice.

Return the exact host importer/presentation paths, pinned fixture digest, validation/unknown-field choices, test evidence and any required changes to this producer note. Then agree on an authoritative envelope/schema home, stable activity identity and versioning. Numeric opcode allocation, Studio HUD mounting and model/control execution remain separate reviewed work.

## Producer verification for this revision

All 39 Python tests passed, including four coordination tests. The headless CLI produced and the standard-library checker accepted a real synthetic-frame report. Importing the headless producer was checked not to import tkinter. Regeneration into a separate empty directory reproduced all 24 pinned fixture files byte-for-byte. Fixture manifest SHA-256: `aa3e173239a971a0bb171fa71519a03d6aad36d6d3e1cc4b9affd479c0617470`.

The prior 63 .NET tests/build results remain historical evidence; no C# behavior was changed or .NET suite rerun for this producer-only seam. Host import, rendering and admission are not implemented or independently qualified in this repository.
