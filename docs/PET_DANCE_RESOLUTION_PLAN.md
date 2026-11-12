# Pet Dance at native game resolution

Status: offline geometry and bounded native-pixel extraction implemented and tested,
November 9, 2026. No live resolution qualification is claimed by this document.
Keep the current **1280 × 720 client-area gate** until
the replacement path passes the checks below. The desired outcome is that the
user keeps Wizard101 at a comfortable native resolution while Maelstrom analyzes
small, normalized regions internally. It should not require shrinking the game
window or changing Windows display scale.

## What the current implementation already provides

| Component | Reusable behavior | Limitation to address |
| --- | --- | --- |
| `WindowObserver` | Captures only the selected HWND, with source timestamps and identity checks. | Bitmap reads currently copy the entire captured surface to CPU memory; a larger game increases that cost even if recognition uses a small panel. |
| `PetDancePanelVision` | Pinned references, explicit capture mappings, separate cue/phase/feedback regions, arrow fade handling; new offline measured-client overload. | Existing runtime `Extract` still requires the exact profile size or one specifically declared one-pixel height mapping. New overload is not live-qualified. |
| `PetDanceUiLayout` | Finds a phase plate and nearby arrow ring in the bottom-center 1/12, with a 1/6 fallback; new offline overload refines bounded grid candidates at native pixels. | Existing runtime entry point still requires exact profile dimensions. UI scale remains explicit and independent of resolution. New path measures the client-top anchor instead of scaling `TopAnchorY=38`. |
| `PetDanceArrowShapeMatcher` | Recognizes direction from normalized arrow shape rather than one exact raw template. | Still depends on valid layout acquisition and an adequately sampled arrow. |
| `PetDancePostGameClassifier` | Uses relative regions and actual OCR word bounds for reward, snack, settings, and Idle menus. | Saved-fixture scaling tests are geometry checks, not evidence that native rendering at another resolution produces equivalent OCR. |
| `PetDanceMenuInput.MapPoint` | Maps observed button bounds through the actual captured window and client rectangle. | The capture rectangle, image dimensions, and client geometry must agree; an analysis-only coordinate must never be passed as a raw capture coordinate. |

The bundled profile currently uses a **1282 × 759 outer capture** and a
**256 × 140 analysis panel**. Earlier encoded recordings contain 1282 × 760
frames. These are different spaces from a 1280 × 720 game client. See
[the geometry diagnostic](PET_DANCE_CALIBRATION_DIAGNOSTIC.md): a seemingly minor
one-pixel height resize changed recognizable calibration frames into Unknown.
The current explicit mapping rules are therefore a requirement to preserve.

## Coordinate contract

Define and record these spaces separately for each accepted frame:

1. **Desktop pixels:** physical screen coordinates used only by the final UI
   dispatcher. Retain monitor origin, including negative origins.
2. **Captured window pixels:** the actual WGC image and its dimensions. Window
   decorations are included when the capture includes them.
3. **Game client pixels:** the measured client rectangle inside that same capture.
   Obtain window/client geometry through normal desktop APIs, not game memory.
4. **Search pixels:** a bounded image used to locate the phase plate and ring.
   Retain its transform and any padding; it is not an input coordinate space.
5. **Canonical panel pixels:** the normalized cue/phase/feedback panel consumed
   by the existing sequence recognizer. Retain the source rectangles and scale
   that produced it.

A frame's geometry record should include selected process identity, capture
timestamp, capture dimensions, measured outer bounds, client bounds in capture,
window DPI, mode/viewport evidence, transform version, and profile hash. Geometry
must be measured consistently with the application's DPI-awareness mode. Compare
the physical outer dimensions with WGC before accepting an inverse mapping.
Window border/title-bar heights are measured values, never constants inferred
from 100%, 125%, or 150% display scale.

For a client rectangle `(cx, cy, cw, ch)` in captured pixels, client-to-capture
translation is `(x + cx, y + cy)`. A search image can use one uniform scale
`s = min(searchWidth/cw, searchHeight/ch)` with explicit padding offsets. Its
inverse removes padding, divides by `s`, and adds the client origin. Never use
separate horizontal and vertical scales to force an unfamiliar aspect ratio
into the existing panel. Every mapped rectangle must remain entirely within its
verified source/client region.

The geometry record is immutable for the frame. At dispatch, verify that target,
client size, outer mapping, and DPI still match. A resize, DPI transition, mode
change, or conflicting measurement invalidates queued actions and pending cue
sequences; reacquire a complete Watch sequence after the new layout is qualified.
Moving the unchanged window can update desktop translation, but still requires
a fresh target/mapping check immediately before a click.

## Recognition path

Do not resize the entire outer window to 1282 × 759. That also scales decorations
and moves the in-game anchors. Do not equate a larger screen with a larger UI:
game UI settings and game resolution can affect icon sizes independently.

Use two levels of analysis:

1. **Bounded acquisition:** find the phase plate in a uniformly downsampled
   bottom-center client region. Keep the existing search pixel and component
   bounds; a 3840 × 2160 image's 1/12 already exceeds 250,000 pixels. Inspect a
   1/6 fallback only when the smaller search cannot qualify one layout.
2. **Source refinement:** map the candidate back into the original client frame,
   inspect its native pixels, and verify plate dimensions, text, and ring support.
   Estimate the local UI scale from those actual anchors. Reject ambiguous
   candidates instead of choosing a different transform for each direction.
3. **Panel extraction:** sample only the qualified source regions into the
   canonical panel, once. Use the same transform for all arrow hypotheses, retain
   interpolation settings in the profile, and preserve fade/neutral sequencing.
   The top feedback region needs an independently measured top-client anchor;
   the present chrome-relative `TopAnchorY` must not be scaled with resolution.
4. **Temporal tracking:** retain a qualified layout for bounded tracking, but
   validate the plate/ring on every consumed frame. Reacquire when the geometry
   or evidence changes. Cached coordinates are not enough to authorize input.
5. **Menu OCR:** continue recognizing whole-game results and native Idle separately
   from rapid arrow analysis. Use native menu crops, cap the enlarged crop to
   Windows OCR's actual maximum image dimension, and translate word bounds back
   to captured pixels. The current fixed 3× crop can exceed that limit at 4K.

Aspect ratio changes need separate treatment. First qualify ordinary 16:9 client
sizes. Later qualify 16:10, ultrawide, or observed letterboxing with explicit
viewport evidence. Dark game artwork alone is not proof of a black border, and
matching outer-window aspect ratios is not proof of matching client viewports.
Do not silently crop an ultrawide client to 16:9.

## Versioning and compatibility

Keep version-1 profiles, hashes, source rectangles, and replay results unchanged.
Introduce a versioned client-space geometry contract for the new path. A profile
migration must read actual calibration metadata or a reviewed calibration frame
to establish the client origin; dimensions alone cannot supply it.

The new profile should declare the canonical panel, client/viewport policy,
bounded UI-scale hypotheses, acquisition/search budget, source sampling method,
qualification evidence, and supported resolution/UI/DPI combinations. A new
coordinate contract or interpolation method changes the profile hash and requires
replay validation. Resolution support should be an explicit capability of a
qualified pack version, not a global unchecked “any size” option.

Keep training labels and original recordings in source coordinates, with source
timestamps. Derived normalized panels refer back to their original frame and
transform. A resize cannot turn one source observation into several independent
training examples, and encoded 60 fps does not prove 60 unique captures per second.

## Work order and acceptance evidence

| Stage | Concrete change | Required evidence before progressing |
| --- | --- | --- |
| 1. Geometry only | Add a pure, versioned capture/client/search transform and inverse; no input or new live sizes. | Bounds and inverse tests for negative monitor origins, client borders, fractional DPI, aspect mismatch, out-of-bounds points, malformed geometry, resize/DPI transitions, and round-trip error no greater than one physical pixel. |
| 2. Offline panel path | Refactor layout acquisition to accept measured client geometry, then refine source crops and normalize the existing panel. | Frozen baseline recordings still produce the same accepted sequences. Synthetic transformations test only mapping invariants; report them separately from actual native-resolution recordings. |
| 3. Native capture evidence | Record bounded Observe-only samples at 1920 × 1080 and the user's actual native client resolution without changing display scale. | Captured-window/client alignment, all four arrows, repeated arrows/fades, Watch/Go/feedback, loading, reward, snack screen, graphics menu, and Idle banner labeled in original pixels. Include the chosen UI size and actual window DPI. |
| 4. Held-out replay | Evaluate new recordings with a frozen profile and source timestamps. | Full-sequence agreement against reviewed labels, phase timing error, unknown-frame rate, missed/duplicate cues, false response phases, and cleanup classification. Report per configuration; do not pool unsupported configurations into one accuracy figure. |
| 5. Resource qualification | Measure capture, acquisition, panel extraction, OCR, and policy separately. | CPU/GPU use, working set, unique frame cadence, capture gaps, p50/p95/p99 analysis age and dropped stale frames. No model host, no unbounded queue, no full-resolution RGB conversion solely to analyze a tiny ROI when a measured cheaper path is available. |
| 6. Bounded UI trial | Enable one qualified native-resolution configuration in the player. | Several complete games with varied sequences and reviewed outcomes, prompt input release on user activity, no wrong-window clicks, and visually confirmed native Idle cleanup. Preserve failed games. |
| 7. Wider support | Add further UI sizes, DPI values, arenas, or aspect ratios one at a time. | The same replay and bounded live checks for each newly declared capability. Existing packs remain usable. |

Do not broaden the player launch gate merely because the geometry helper passes
unit tests. Native-resolution rendering, input mapping, and cleanup need evidence
from that configuration. A perfect game at the existing setting does not qualify
all resolutions or all arenas.

## Lightweight defaults

Keep the game and optional recording at the user's chosen resolution. Recognition
can consume the newest bounded panel at its measured required cadence; do not
queue stale frames to “catch up.” Maintain the slow postgame OCR budget separately
from the rapid arrow loop. Perform full search only for acquisition/recovery and
validate small tracked regions during stable gameplay.

Start with the existing capture path and measure it before choosing a new backend.
`WindowObserver` already exposes a surface path that could support bounded GPU
crop/downscale before CPU readback; qualify its timestamps and geometry before
using it for control. Retain original-source recording as an optional separate
consumer, not a prerequisite for every live observation. No model dependency is
needed to implement this resolution work.

## Implemented offline API and results

The new `PetDanceResolutionMap` accepts two `PetDanceCaptureGeometry` records:
calibration and source. Each contains capture dimensions, measured client bounds
within that capture, DPI, and evidence provenance. It validates containment,
bounded dimensions, positive client area, exact client aspect agreement, and
finite mapped points. No border, viewport, UI scale, or DPI relationship is guessed.

`PetDancePanelVision.Extract(source, map, sourceUiScaleMultiplier: 1)` and the
corresponding `PetDanceUiLayout.TryExtract` overload are offline entry points.
The multiplier is an explicit source UI-pixel hypothesis, not an automatically
derived resolution ratio. The existing runtime overload is unchanged. The new
overload requires a layout-aware profile and returns Unknown when acquisition
fails; an unrecognized or transparent frame cannot become a loading pause.

Broad search uses the measured client grid. An integer sampling step keeps the
search at or below 250,000 pixels while preserving one uniform scale. Candidates
are checked against explicit UI-size hypotheses and refined using original
source pixels, with a separate 250,000-pixel total refinement allowance per grid.
Only bounded top/bottom panel crops are normalized; the full game image is never
stretched to fit the old outer-frame dimensions. Native crops with nonopaque
pixels are rejected, including hidden RGB beneath alpha zero.

New tests are in `PetDanceResolutionTests.cs`. They cover 1280 × 720,
1920 × 1080, and 2560 × 1440 clients; distinct borders and DPI; independent UI
scales; bounded 4K grids; incorrect aspects/bounds; missing rings; ambiguous or
unsupported layouts; transparent frames; stale-layout rejection; and unchanged
behavior of the original exact-size entry point.

The final focused run passed **63/63 tests**, including these new cases, the
existing layout/loading regressions, and bounded menu-OCR raster tests. Compilation
and tests used `DOTNET_PROCESSOR_COUNT=2`, one build worker, and no shared compiler.
Root review then ran the complete native suite: **966/966 passed, none skipped**.
Scoped formatting and analyzers passed. These source changes are not in the frozen
21:24 workspace installer awaiting installation confirmation.

Four fixtures under `ProjectMaelstrom.Tests/TestData/PetDance/Resolution` retain
unscaled broad-grid and top-panel crops from the existing recorded Watch/Down
and Go frames. Their `provenance.json` records original frame hashes, crop
rectangles, and fixture hashes. Six tests transplant those real pixels into
explicitly declared synthetic client transforms at the three client sizes.
All six preserve the expected phase; Watch retains Down. This is a regression
test using real game pixels, **not newly captured native-resolution gameplay**.

Measured extraction plus recognition timings from the focused test run follow.
Each case has three warm-up frames and twelve timed repetitions. Capturing a
window, constructing/resizing the synthetic source, recording, and sending input
are excluded. These numbers therefore do not establish live frame throughput or
reaction latency.

| Saved pixels / synthetic client | Mean | p95 | Approx. managed allocation per frame |
| --- | ---: | ---: | ---: |
| Go / 1280 × 720 | 9.178 ms | 9.827 ms | 491 kB |
| Watch/Down / 1280 × 720 | 8.957 ms | 9.514 ms | 492 kB |
| Go / 1920 × 1080 | 10.097 ms | 10.926 ms | 677 kB |
| Watch/Down / 1920 × 1080 | 10.289 ms | 11.041 ms | 676 kB |
| Go / 2560 × 1440 | 11.459 ms | 12.685 ms | 815 kB |
| Watch/Down / 2560 × 1440 | 11.740 ms | 14.278 ms | 814 kB |

The allocation figures include the recognizer and reused search buffers'
initial growth averaged over all fifteen iterations; they are not steady-state
capture allocations. Further buffer reuse and cached-layout tracking remain
possible optimizations, to be justified by native capture measurements.

No live gates, game settings, capture implementation, or input behavior were
changed. The next qualification remains bounded Observe-only recordings at the
user's chosen native resolution, followed by frozen replay and a separate
bounded live test. Ultrawide and different-aspect viewport mapping remain pending.
