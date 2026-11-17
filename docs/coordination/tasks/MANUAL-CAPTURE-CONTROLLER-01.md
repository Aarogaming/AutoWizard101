# MANUAL-CAPTURE-CONTROLLER-01 — design brief (PROPOSED, not a reservation for implementation)

Status: **DESIGN BRIEF — owner approval required before any code or rule change.**
Date: 2026-11-16 · owner: root · workstream: `CORE-DELIVERY`

## 1. Goal

Run Maelstrom headless with a persistent, on-screen **manual-control capture controller** so the owner can play the game in the foreground while Maelstrom records front end and back end on one timestamped clock. The controller must feel immediate (hotkeys + overlay buttons) while preserving the safety rule the owner acknowledged: *to take permanent control, the owner explicitly pauses or stops Maelstrom*.

## 2. What already exists (reuse, do not rewrite)

| Asset | Current reality | Reuse |
| --- | --- | --- |
| `W101RunningNotice.cs` (282 lines) | Play/Pause/Stop glyph buttons, `ControlButton` enum, nonactivating clicks, status callback (`Running`/`Paused`/`Stopping`), 200 ms timer, caption window | Extend into a **persistent** overlay host; keep nonactivating clicks and status semantics |
| `ActionStartNotice.cs` | 5-second start countdown, target-change/focus/ownership cancellation | Reuse for start and for **pre-resume countdown** |
| `TeachingRecordingCli.cs` + `SessionTimelineForm.RecordHeadlessAsync` | `RecordInputs`, `EnableW101Telemetry`, `StopFile`, headless host with `ShowWithoutActivation`, bounded minutes | The capture session itself; do not rebuild recording |
| `JobDeckHost` / `OperationScheduler` | `pause` currently maps to `Stop()`; cancel/drain/recovery holds exist | Add a distinct explicit-pause latch; do not conflate with stop |
| `OverlayHudForm.cs` | TopMost, manual-position form with labels | Optional host shell; keep click-only, no key interception |

Note: `W101RunningNotice.cs` is under **active claim `W101-OVERLAY-CONTROLS-35`** (worker stopped at usage limit; coordinator completing validation). Resolve that claim (release after validation or transfer paths) before implementation. This brief only references it.

## 3. Operating modes (owner-directed)

| Mode | Default? | Activity response | Resume rule |
| --- | --- | --- | --- |
| **SAFE** | **YES** | Any desktop activity pauses dispatch immediately (existing takeover) | **Never automatic.** Step-by-step diagnostics only; resume by explicit hotkey/click after re-verification |
| **NORMAL (fluid)** | No — explicit per-session opt-in | Activity latches an *implicit* pause | Auto-resume allowed **only** after ≥3 s of no detected activity **and** no explicit pause/stop latch, with a visible countdown and full re-verification before dispatch |

Hard invariants in both modes:
1. **Explicit pause or stop is a latch** — auto-resume can never clear it, in any mode.
2. **Escape / exit / target change / focus loss during an action routine** remains immediate takeover — unchanged.
3. **No input is dispatched while paused**; pending work is drained and the shared lease/cleanup is verified before any resume.
4. If cleanup is uncertain, `GameInputLease.RequireRecovery` fires and the hold is honored; no resume until verified restore.
5. NORMAL mode is **per-session opt-in**, never sticky across sessions; SAFE is the default on every launch.

## 4. Controller design

- **Hotkeys:** explicit non-game combos (candidate `Ctrl+Alt+R` record/start, `Ctrl+Alt+P` pause, `Ctrl+Alt+S` stop), registered with the OS hotkey API, never global low-level key interception; documented so the owner can change them. Reject binding plain keys already used by W101.
- **Overlay buttons:** Play / Pause / Stop, nonactivating clicks (existing pattern), showing: mode (SAFE/NORMAL), state (Running / Implicitly paused / Explicitly paused / Stopping), 3-second quiet timer + countdown, target identity, session clock.
- **Headless host:** `Maelstrom.exe --cli deck-host`-style owner plus the overlay as the only UI; the game stays foreground and untouched. No second controller/scheduler.
- **Capture sync:** one session clock from `SessionTimeline`; front-end frames/inputs and back-end W101 telemetry/UI-context share `UtcTicks` per sample; a `cross-compare.json` index aligns them. Only **one** back-end observer runs inside the session (no concurrent UI-tree watcher).

## 5. Proposed narrow AGENTS.md amendment (owner must approve verbatim or edit)

> Runtime boundaries addition: "A per-session, explicitly opted-in NORMAL capture mode permits implicit pause on desktop activity and automatic resume after ≥3 s of sustained quiet, provided (a) no explicit pause or stop latch is set, (b) a visible pre-resume countdown has completed, (c) target identity, ownership, focus and cleanup are freshly re-verified, and (d) SAFE mode remains the default with no automatic resume. This does not waive takeover for action routines outside the opted-in session, and never permits evading a recovery hold or user takeover to perform cleanup."

This is an *addition*, not a deletion of existing takeover rules. If the owner does not approve it, implementation is SAFE-mode-only.

## 6. Scope split and acceptance gates

**Implementation claim(s) — to be registered only after approval:**
- `W101RunningNotice.cs` / tests — persistent overlay host and pre-resume countdown (requires resolving `W101-OVERLAY-CONTROLS-35`).
- `ActionStartNotice.cs` — reusable countdown for resume.
- `TeachingRecordingCli.cs` / `SessionTimelineForm.cs` — session clock, cross-compare index, explicit pause latch.
- `MaelstromCli.cs` / `Program.cs` — headless `--manual-capture` entry and hotkey registration.
- `OperationScheduler.cs` / `GameInputLease.cs` — only if a true pause/resume contract is required; otherwise reuse drain + re-admission.

Gates: focused + full native suite with `-warnaserror`; scoped format/analyzers; encoding/board checks; offline simulated activity/quiet/takeover/timer tests proving (a) explicit pause never auto-resumes, (b) SAFE never auto-resumes, (c) no dispatch during pause, (d) target change/recovery hold blocks resume. A live rehearsal needs **fresh owner scope** and is not part of implementation.

## 7. Boundaries and non-goals

No global key interception; no bypass of Escape/takeover; no auto-resume from an explicit latch; no concurrent capture jobs; no network manipulation; no game-file writes; no Crowns/snacks/volume/restart; no changes to the input owner model. This brief itself makes no code or rule changes.
