# RESOLUTION-1440P-01 — report-only feasibility map for 1440p / window-resolution support

Status: **FEASIBILITY MAP — no implementation, no live interaction.**
Date: 2026-11-16 · owner: root · claim: `RESOLUTION-1440P-01` · workstream: `W101-DANCE`

## 1. Scope and what was actually measured

Owner asked whether 1440p support can be added while they play the fire wizard. This report is **static source analysis only**. The only live values collected were read-only Win32 geometry/DPI queries on the selected game window — **no hook, no capture, no input, no focus change, no desktop control**.

| Read-only observation | Value |
| --- | --- |
| Game window (outer) | **2048 × 1104** |
| Game client area | **2033 × 1066** |
| Window DPI | **120** (125% display scale) |
| Qualified Dance geometry in code | **1282 × 760** (`PetDanceLevelSelectionVision.QualifiedGeometry`) |

The current session is therefore already far outside the qualified Dance geometry. The production runtime correctly refuses it (fail-closed `geometry-unqualified`) — that is expected behavior, not a defect.

## 2. Why this is not "add 1440p" as a flag

`docs/PET_DANCE_RESOLUTION_PLAN.md` is explicit:
- encoded pixel dimensions alone do **not** establish a capture transform;
- a one-pixel height change previously turned recognizable calibration frames into `Unknown` (`PET_DANCE_CALIBRATION_DIAGNOSTIC.md`);
- border/title-bar heights must be **measured**, never inferred from 100/125/150% scale;
- a new profile must declare canonical panel, client/viewport policy, UI-scale hypotheses, acquisition budget, sampling method, qualification evidence and supported resolution/UI/DPI combinations;
- resolution support must be an explicit capability of a **qualified pack version**, "not a global unchecked 'any size' option."

## 3. Resolution-dependent sites (static inventory)

| Site | Current dependency | What a 1440p profile needs |
| --- | --- | --- |
| `PetDancePanelVision` | Already **profile-driven**: `DancePanelCaptureGeometry(Width, Height, Transform, Evidence)`, canonical size from profile, `ValidateCaptureGeometry`, `WindowObserver.CheckSize` | Reuse this abstraction: a new `profile.json` with measured transform + evidence hashes. This is the right seam |
| `PetDanceLevelSelectionVision` | Hardcoded `QualifiedGeometry = 1282×760`, `QualifiedControlBounds = 452,485,59,55` (also mirrored in Rust parity fixtures) | Generalize to profile-provided geometry/bounds; preserve exact qualified default; add parity rows for the new profile |
| `PetDanceProductionRuntime` | Refuses any geometry ≠ qualified and arena ≠ WizardCity | Keep fail-closed; accept a *qualified profile identity*, not arbitrary sizes |
| `PetDanceEntryVision` / `PetDancePlayButtonVision` / `PetDanceMenuOcr` | Bounds/ROIs validated against frame size (`visual-evidence-frame-mismatch`) | New ROIs and templates per profile with provenance |
| UI tree native reader | Two pinned client-image hashes and layout image SHA-256 (`CURRENT_IMAGE_SHA256`, `ARCHIVED_LAYOUT_IMAGE_SHA256`), layout profiles in `tools/w101_ui_tree.py` | Layout/image qualification is **separate from resolution**; new DPI/layout may need new pins |
| Templates / fixtures | `Resources/{1024x768,1280x720,2256x1504}`, `Resources/PetDance/default-v1` (13 assets + provenance), `TestData/PetDance/*`, RustParity dance 405 rows @ 1280×720 | Real 1440p crops with provenance; new fixture rows; replay validation |
| Capture path | `WindowObserver` produces client-area frames with DPI awareness; `BoundedWindowCapture` gates freshness | No fundamental blocker, but transform must be **measured** for the new window/DPI combination |

## 4. Recommended staged path (not yet authorized)

1. **Owner-provided evidence (during play, zero agent interaction):** save 3–5 screenshots with the OS screenshot tool into `.local/1440p-captures/` — dance sigil/entry screen, level selection, one combat screen with spell cards, and native Idle — plus a small text note of display scale, windowed/fullscreen mode, and the client dimensions. No agent hook, capture or input while they play.
2. **Offline profile engineering (needs its own claim with source/build authority):** derive ROIs/templates from those frames; add a **default-off** 1440p profile with provenance and measured transform; generalize the profile-driven code paths; keep 1282×760 as the qualified default; add fixtures and parity rows.
3. **Offline verification:** focused + full native suite, scoped format/analyzers, replay of the new fixture set.
4. **Separately authorized live qualification:** bounded observation at the new profile, then (much later) a gameplay routine. No live step is authorized here.

## 5. Relation to the owner's play session

The fire-wizard session is **not** to be disturbed. No observer, hook, capture API, focus change or input will be used by the agent during play. Saved screenshots from the owner are the only intended evidence channel; they also give us real combat-HUD/card-layout data that could later feed a wrapped combat teacher under the "wrap first, then reverse-engineer from the working wrapper" principle — but that is a separate proposal.

## 6. Limits

No 1440p profile exists today; no 1440p capture, fixture, template or parity evidence was produced by this report. No implementation or live test was performed. The current client's 2048×1104/DPI-120 window is a *different* combination from 2560×1440 at 100% — the target resolution must be confirmed by the owner before any profile work starts.
