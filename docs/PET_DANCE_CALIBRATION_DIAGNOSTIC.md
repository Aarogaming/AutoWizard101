# Pet Dance calibration geometry diagnostic

November 9, 2026. Offline saved-frame study; no game, desktop control, model,
installation changes or application builds.

**Confirmed:** the implicit 759→760 height normalization can turn recognizable
calibration images into entirely Unknown observations. **Not established:** that
this specific pixel mapping caused live teacher 04's failure. The missing live
gameplay frames prevent a complete causal diagnosis.

## Evidence

Generated evidence is under
[calibration-diagnostic-01](../.local/w101-route-study/human-route-01/calibration-diagnostic-01/diagnostic.json):

- `gdi-panels.json`: source/profile/image hashes and 32 saved panels.
- `diagnostic.json`: per-frame cue/phase errors, classifications and offset probes.
- `geometry-contract.json`: capture/encoder metadata, source audit and live summary.

The [PowerShell diagnostic](../tools/diagnose_pet_dance_gdi.ps1) uses actual
`System.Drawing.Graphics.DrawImage` with the default settings and operation order
from the pre-correction `PetDancePanelVision.Extract`. It loads the drawing
assembly; it does not compile C# or run the application. Thus these resizing
results use GDI+, not a PIL approximation.

The [Python diagnostic](../tools/diagnose_pet_dance_calibration.py) reproduces the
native per-frame sampled pixels, circular cue mask, opacity fit and thresholds.
PIL only reads/crops PNGs. It runs with numerical worker counts limited to one.
The actual native sequence/outcome gate was **not** executed by this diagnostic;
root-coordinated C# fixtures remain the integration check.

Seven actual human demonstration frames and one reference reconstruction were
tested. All images are from the existing calibration study, not held-out accuracy
data. The original GDI+ panel crops matched direct saved-image crops exactly in
all seven real-frame cases.

| Geometry variant | Correct phase and cue | Unknown phase/cue | Maximum phase RMS | Maximum cue residual |
| --- | --- | --- | --- | --- |
| Original 1282×760 | 8/8 | 0/8 | 3.52 | 3.72 |
| Remove bottom row → 1282×759 → current GDI+ normalization | **0/8** | **8/8, both Unknown** | 29.42 | 36.72 |
| Remove top row → 1282×759 → normalization | 8/8 | 0/8 | 4.76 | 4.21 |
| GDI+ shrink to 759 → normalize to 760 | 8/8 | 0/8 | 5.19 | 4.08 |

For `dance-60.png` specifically, original Watch/Neutral has zero phase/cue error.
The bottom-row-removal case has phase RMS **29.32**, cue residual **19.86** and
blank difference **20.00**, exceeding the current phase/neutral limits of 12.
It becomes Unknown/Unknown. Removing one row outside every configured panel
region should not require resampling the interior unless the profile explicitly
defines that coordinate transform.

The alternatives demonstrate why a dimension ratio cannot choose the mapping:
the same 1282×759 dimensions represent different pixel relationships. A blanket
switch to unscaled cropping would likewise be wrong for a genuinely scaled source.

## Minimal correction

The primary implementation should remove implicit live one-pixel normalization
and make the mapping an explicit, pinned calibration property:

1. No mapping declaration means exact source geometry only.
2. A pixel-preserving source mapping may extract unchanged coordinates when all
   regions fit, but requires stated source dimensions and evidence.
3. A legacy one-pixel scaling mapping remains a separate explicitly qualified
   option; it must not be inferred merely because the heights differ by one.
4. Keep existing profiles unchanged and unqualified for mismatched live geometry.
   Do not silently choose transforms per frame or increase thresholds to hide
   calibration disagreement.

Required native fixtures: exact geometry passes; undeclared mismatch rejects;
explicit pixel-preserving bottom-row-removal retains Watch/Neutral; explicit
scaled geometry retains its independently generated fixture; mapping/profile
changes invalidate pins. The two source relationships must stay separate.
This agent changed no application files; the primary agent owns that correction.

## Encoder contract audit

The human recording's `video/result.json` records source **1282×759**, encoded
**1282×760**, and `source_to_video.scale_y = 760/759`. Source inspection shows
`GpuSurfaceVideoProbe` provides uncompressed source dimensions and requests
different `MediaTranscoder` output dimensions. It does not specify or measure the
interior pixel mapping. The ratio is computed metadata, not measured registration;
`VideoGeometry`'s comment that Windows scales does not establish that contract.

A bounded `ffprobe` metadata inspection found one H.264 video stream:
1282×760 coded/display dimensions, square pixels, progressive yuv420p, 60/1 fps.
There was no reported transform side data and no audio stream. These container
properties do not recover the raw-capture-to-encoded pixel transform.

The saved Watch frame and saved live loading frame have their sampled central
titlebar boundary at row 37. The decoded Watch frame's central bottom rows 758
and 759 are black; the live frame's last row 758 is gray window border. This shows
different edge handling, but the scenes differ and it does not prove the interior
was padded, clipped or scaled. A same-frame raw/encoded pair is still required.

## Other assumptions and gaps

- The frozen profile uses full-window coordinates, including window chrome:
  phase `(525,657,232,28)`, cue `(610,680,64,64)`, feedback `(550,100,180,40)`.
  Cue-mask center is `(641,710)`, radius 19. There is no visual-anchor registration.
  On the known Watch/Neutral frame, shifting either sampled region axis by one
  pixel in either direction already produces Unknown/Unknown. UI origin changes
  therefore matter independently of resolution.
- The profile does not record/enforce game level, UI scale or client-content
  rectangle. Tiny UI is study context, not a validated profile field. Level/UI
  differences need evidence; they are not diagnosed causes here.
- The loading profile used for live teacher 04 has identical geometry and the
  same ten gameplay reference hashes as the frozen study, plus Loading. A swapped
  gameplay reference set is therefore excluded for this comparison.
- Live teacher 04 recorded 93 Loading and 642 Unknown classifications; all 735
  cues were Unknown. There were 598 content changes, zero input receipts and zero
  outcomes. Median observed-frame gap was 61.11 ms; maximum was 150.00 ms rounded.
  A temporal sequence threshold cannot explain a per-frame matcher returning
  Unknown on every cue by itself. This is not evidence that 60 fps capture was
  achieved or that raising the sample rate fixes the geometry.
- The two saved post-start panels show a loading transition and a later scene,
  not a full saved set of Watch/Go gameplay. Without those intermediate full
  frames, we cannot reconstruct whether UI geometry, scene selection, rendering
  or another mismatch dominated the live result.
- The completed scheduler receipt separately confirms supervised native Idle
  restoration. That cleanup success is independent of gameplay recognition.

Next useful evidence is a small, timestamped, game-window-only frame set spanning
Watch, clear directions, Go and feedback, plus paired raw/decoded images when
qualifying the recorder transform. Preserve unknown frames and their geometry;
do not run more blind input trials merely to select a transform that appears to
work.

## Fixture list and reproduction

Paths below are relative to `.local/w101-route-study/human-route-01`.

| Input | Expected per-frame phase/cue | Notes |
| --- | --- | --- |
| `dance-60.png` | Watch / Neutral | Human video, 60 s |
| `right-61_1.png` | Watch / Right | 61.1 s |
| `calibration-frames/frame-0002.png` | Watch / Left | 61.6 s |
| `up-62.png` | Watch / Up | 62 s |
| `calibration-frames/frame-0005.png` | Watch / Down | 68.5 s |
| `feedback-65.png` | Resolving / Neutral | Success feedback; temporal latch excluded |
| `feedback-90.png` | Resolving / Neutral | Failure feedback; temporal latch excluded |
| `fade-study/Go.png` | Go / Neutral | Reconstructed full image from panel; synthetic surrounding pixels |

```powershell
tools/diagnose_pet_dance_gdi.ps1 `
  -ProfilePath .local/w101-route-study/human-route-01/fade-study/profile.json `
  -StudyRoot .local/w101-route-study/human-route-01 `
  -Output .local/w101-route-study/human-route-01/calibration-diagnostic-new

.local/envs/dance/Scripts/python.exe tools/diagnose_pet_dance_calibration.py `
  .local/w101-route-study/human-route-01/calibration-diagnostic-new/gdi-panels.json `
  --output .local/w101-route-study/human-route-01/calibration-diagnostic-new/diagnostic.json
```

Both steps require new output paths and preserve earlier evidence. No video frames
were newly decoded for this study. The Python scoring/PNG pass took under one
second in the tool run; that is diagnostic cost, not a gameplay throughput claim.
