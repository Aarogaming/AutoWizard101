# MaaFramework and Airtest reuse review

Reviewed 2026-10-30. Decision: keep Maelstrom's workspace, sampler and data-only
packs; reuse recognition primitives behind a read-only adapter. Do not add another
capture/input owner or import executable task scripts into packs. This review
changes no application dependencies or live behavior.

## Evidence

Source-only shallow snapshots in `.local/reference-repos/`:

| Project | Reviewed revision | Work performed |
| --- | --- | --- |
| MaaFramework | `04418969ff14e3c25b8c70bfa683d44462e38802` | Python/C++ recognition API, matcher, pipeline docs, dependencies and license inspected. Native runtime not installed/executed. |
| Airtest | `d729c631d2032521be1e2168a249a2a89d79af2a` | Source reports 1.4.3. CV implementation, wrapper, requirements and license inspected; low-level matcher executed on synthetic images. |

These revisions are source snapshots, not assertions about published binary APIs.
Neither repository's setup scripts were run. No SDK was added to the app.

## Fit against Maelstrom

| Area | Current implementation | Decision |
| --- | --- | --- |
| Capture | Explicit HWND/PID/start identity and Windows Graphics Capture | Keep. Both upstream recognition entry points accept supplied images. |
| Sampler | Goal, teaching/check purpose, 1fps PNGs, manual labels, independent checks | Keep; add region/template annotations here. Framework recognition does not replace our evidence workflow. |
| Recognition | Whole-frame 32×24 RGB exemplar distance | Retain as baseline; benchmark established regional matching before extending it. |
| Authoring | Immutable packs with labeled images and hashes | Borrow Airtest's reference-resolution/cropped-template approach and Maa's separation of recognition regions, results and actions. |
| Masks | Not implemented | Maa supports ROI and green masks. Airtest's inspected TemplateMatching has no mask argument. |
| OCR | Existing Tesseract dependency, not wired into Observe | Compare it with Maa OCR on actual text crops before adding another runtime. The tested Airtest matcher does not provide OCR. |
| Decisions | Exact label rules produce offline key/click/wait proposals | Borrow Maa's recognition-driven branching/recovery concepts later; keep execution under Maelstrom ownership. |
| Evaluation | Human check labels; exact training duplicates excluded | Keep, adding localization, negative cases, latency and session-separated checks. Scores are not interchangeable probabilities. |

## MaaFramework findings

The reviewed Python `Tasker.post_recognition(reco_type, reco_param, image)` accepts
a NumPy image and returns a task job. It serializes typed parameters using
`dataclasses.asdict`, not arbitrary pipeline JSON. C++ constructs a RecognitionTask
separately from post_action/full pipeline tasks. Native resource initialization,
result extraction and operation without an input-capable controller still need a
runtime smoke test before adoption.

The pipeline supports template, feature, color, OCR, neural and combined
recognition. TemplateMatch exposes ROI, thresholds, OpenCV methods and green_mask.
RGB (0,255,0) marks ignored template pixels and the C++ implementation passes a
mask to OpenCV. Store original pixels and separate masks in our packs; translate
to this convention inside the adapter so real green objects are not lost.

Documentation describes templates prepared at 720p. Our raw captures include
window chrome; the adapter must explicitly manage resolution/coordinates. Do not
assume standalone supplied-image recognition normalizes like the controller path.
Set score ordering explicitly: the documented default is horizontal ordering.

Pinned sources: [binding](https://github.com/MaaXYZ/MaaFramework/blob/04418969ff14e3c25b8c70bfa683d44462e38802/source/binding/Python/maa/tasker.py),
[matcher](https://github.com/MaaXYZ/MaaFramework/blob/04418969ff14e3c25b8c70bfa683d44462e38802/source/MaaFramework/Vision/TemplateMatcher.cpp),
[protocol](https://github.com/MaaXYZ/MaaFramework/blob/04418969ff14e3c25b8c70bfa683d44462e38802/docs/en_us/3.1-PipelineProtocol.md).

## Airtest findings and executed probe

`airtest.aircv.template_matching.TemplateMatching(template, frame, threshold,
rgb).find_best_result()` returns center, rectangle and score. The tested path
uses grayscale normalized correlation; optional RGB checks change score semantics.
The higher-level Template wrapper also supports recorded position/resolution and
multiscale/feature strategies, but uses global device/logging infrastructure.
Prefer the supplied-image CV layer, not touch/wait/device connections or scripts.

Run `tools/review_airtest_matching.py SOURCE REPORT` in an existing NumPy/OpenCV
environment. It imports the reference CV module only; deterministic synthetic
images, no device APIs. Evidence: `.local/reports/airtest-source-probe.json`.

| Case | Fixed-scale result at threshold 0.8 |
| --- | --- |
| Original target | Correct location; 1.0 |
| Moved target on changed background | Correct location; approximately 1.0 |
| Brightness-shifted target | Correct location; 1.0 |
| Absent target | No match |
| Target enlarged to 125% | No match |
| Blank constant template | False match; 1.0 |

Reject low-information templates and test negatives; a perfect score alone is
not evidence of recognition quality. This was not a native-baseline head-to-head
trial or Wizard101 accuracy test. No baseline scores were approximated and Maa
was not executed. The full Airtest stack was not qualified.

Airtest requirements pin NumPy below 2 and OpenCV-contrib to 4.4–4.6, plus mobile,
capture and desktop-control dependencies. The narrow probe ran using existing
OpenCV 4.8.0 / NumPy 1.26.4, outside the declared OpenCV range. Do not install the
whole stack into our bundled Workshop runtime. Maelstrom already references
Emgu/OpenCV 4.9 and Tesseract: straightforward regional matching through those
existing libraries may be a smaller production integration.

Pinned sources: [matcher](https://github.com/AirtestProject/Airtest/blob/d729c631d2032521be1e2168a249a2a89d79af2a/airtest/aircv/template_matching.py),
[wrapper](https://github.com/AirtestProject/Airtest/blob/d729c631d2032521be1e2168a249a2a89d79af2a/airtest/core/cv.py),
[requirements](https://github.com/AirtestProject/Airtest/blob/d729c631d2032521be1e2168a249a2a89d79af2a/requirements.txt).

## Sampler and pack integration proposal

`WGC frame → region/template adapter → recognition evidence → state resolution → Observe`

Adapter inputs: frame ID/time, target generation, raw dimensions, normalized
search ROI, hashed template/mask assets, method and versioned parameters. Outputs:
candidate state, raw score and score kind, raw-frame bounding box, duration,
backend/version, evidence hashes and unknown/error reason. No input API. Use a
bounded latest-frame queue and worker timeout. Reject stale results after target
or geometry changes.

Add a separately versioned recognition manifest with frame space, reference size,
backend/version and detectors (state, search ROI, template/mask hashes, method,
scale policy, threshold, ambiguity policy, source frame/session and split role).
This is a proposed contract, not an accepted current pack schema.

Current v1 validators do not validate detector semantics, and asset copying follows
session image references, not arbitrary detector/template/mask fields. Implement
validation, path/bounds/hash checks and asset preservation before adding fields.
Preserve existing packs. Reject upstream scripts, commands and unrestricted action
nodes. Bind sampler checks to the recognition bundle, not just pack.json's hash.

Sampler should let users select a region, crop a representative template and
preview ignored areas. Differences may suggest masks, but require user confirmation:
moving scenery can be irrelevant and static objects can matter. Compare backends
on the same independent check sessions, with detector settings and evidence hashes
in each report. No automatic controller qualification.

## Implementation order

1. Region/template/mask authoring and a shared read-only recognition contract.
2. Native OpenCV template baseline through existing Emgu, with low-variance guard;
   retain our whole-frame baseline and the Airtest probe for comparison.
3. Pin a Maa release in an isolated research environment; test supplied-image
   recognition, masks and OCR without capture/input ownership.
4. Benchmark actual Wizard101 rounds with absent targets, distractors, animation,
   background changes, scale/DPI, target loss and stale frames. Record per-state
   correct/incorrect/unknown, false positives, localization and latency. Choose
   thresholds before inspecting check results.
5. Ship an optional Maa recognition worker only if its gains justify runtime and
   packaging complexity; otherwise keep the smaller native stack. No production
   dependency was approved or added by this review. Input control remains separate.

## Licenses and packaging

Inspected licenses: Airtest Apache-2.0; MaaFramework LGPL-3.0. Preserve notices and
modification provenance for reused code. Review actual binaries, OCR models and
dependencies before distribution; a worker boundary does not automatically remove
license obligations. These source downloads and a local probe are not a shipping
decision.

[Airtest license](https://github.com/AirtestProject/Airtest/blob/d729c631d2032521be1e2168a249a2a89d79af2a/LICENSE),
[Maa license](https://github.com/MaaXYZ/MaaFramework/blob/04418969ff14e3c25b8c70bfa683d44462e38802/LICENSE.md).
