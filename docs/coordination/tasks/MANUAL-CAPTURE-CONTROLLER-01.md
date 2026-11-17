# MANUAL-CAPTURE-CONTROLLER-01 — design brief (PROPOSED, not a reservation for implementation)

Status: **DESIGN BRIEF — owner approval required before any code or rule change. Revised 2026-11-16 to make NORMAL the default per owner direction.**
Date: 2026-11-16 · owner: root · workstream: `CORE-DELIVERY`

## Owner decision log (2026-11-16)

The owner corrected the initial proposal: *"my PC doesn't start in safe mode every time. safe mode is meant for when you're using caution explicitly, and isn't meant to be the default."* NORMAL/fluid operation is the product — turn it on, let it run, watch for errors, review them. SAFE is an explicit caution mode for step-by-step diagnostics, not a boot default. The owner further specified the control surfaces: pause/stop hotkeys, overlay buttons, mouse interlock, and a hard-stop keypress that works in every mode. The system pauses at loading screens, focus loss and mouse movement, and resumes when conditions are met **unless** an explicit pause or stop is selected.

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
| **NORMAL (fluid)** | **YES — the product mode** | Loading screen, focus loss, or mouse movement latches an *implicit* pause | **Auto-resume when conditions are met**: ≥3 s quiet (or focus regained / loading cleared), visible pre-resume countdown, target/identity/ownership re-verified — **unless an explicit pause or stop latch is set** |
| **SAFE** | No — explicit opt-in for cautious, step-by-step diagnostics | Same pause triggers; additionally stricter bounds (smaller budgets, step gating) | **Never automatic** while in SAFE; resume by explicit hotkey/click after re-verification. Hard-stop keypress still works |

Hard invariants in both modes:
1. **Explicit pause or stop is a latch** — auto-resume can never clear it, in any mode. This is how the owner stays in control while watching for errors.
2. **Hard-stop keypress** works in every mode, including SAFE.
3. **Pause triggers**: loading screen, window focus loss, and mouse movement (existing takeover + mouse interlock). No dispatch while any trigger is active.
4. **No input is dispatched while paused**; pending work is drained and the shared lease/cleanup is verified before any resume.
5. If cleanup is uncertain, `GameInputLease.RequireRecovery` fires and the hold is honored; no resume until verified restore.
6. SAFE is an explicit, per-session caution mode, never a boot default; NORMAL is the default on every launch.

## 4. Controller design

- **Hotkeys:** explicit non-game combos (candidate `Ctrl+Alt+R` record/start, `Ctrl+Alt+P` pause, `Ctrl+Alt+S` stop), registered with the OS hotkey API, never global low-level key interception; documented so the owner can change them. Reject binding plain keys already used by W101.
- **Overlay buttons:** Play / Pause / Stop, nonactivating clicks (existing pattern), showing: mode (SAFE/NORMAL), state (Running / Implicitly paused / Explicitly paused / Stopping), 3-second quiet timer + countdown, target identity, session clock.
- **Headless host:** `Maelstrom.exe --cli deck-host`-style owner plus the overlay as the only UI; the game stays foreground and untouched. No second controller/scheduler.
- **Capture sync:** one session clock from `SessionTimeline`; front-end frames/inputs and back-end W101 telemetry/UI-context share `UtcTicks` per sample; a `cross-compare.json` index aligns them. Only **one** back-end observer runs inside the session (no concurrent UI-tree watcher).

## 5. Proposed narrow AGENTS.md amendment (owner must approve verbatim or edit)

> Runtime boundaries addition: "NORMAL (fluid) operation is the default mode. Loading screens, window focus loss and mouse movement pause dispatch; dispatch auto-resumes when those conditions clear (including ≥3 s of sustained quiet after mouse movement), provided (a) no explicit pause or stop latch is set, (b) a visible pre-resume countdown has completed, and (c) target identity, ownership, focus and cleanup are freshly re-verified. SAFE is an explicit per-session caution mode in which resume is never automatic. Explicit pause/stop and the hard-stop keypress are never auto-cleared and take precedence in all modes. No resume may bypass a recovery hold or verified-cleanup requirement."

This is an *addition*, not a deletion of existing takeover rules. It changes only the default and the resume behavior after an **implicit** pause; it does not allow forcing input through an explicit stop, a recovery hold, or uncertain cleanup. If the owner does not approve it, implementation falls back to the earlier SAFE-default proposal.

## 6. Scope split and acceptance gates

**Implementation claim(s) — to be registered only after approval:**
- `W101RunningNotice.cs` / tests — persistent overlay host and pre-resume countdown (requires resolving `W101-OVERLAY-CONTROLS-35`).
- `ActionStartNotice.cs` — reusable countdown for resume.
- `TeachingRecordingCli.cs` / `SessionTimelineForm.cs` — session clock, cross-compare index, explicit pause latch.
- `MaelstromCli.cs` / `Program.cs` — headless `--manual-capture` entry and hotkey registration.
- `OperationScheduler.cs` / `GameInputLease.cs` — only if a true pause/resume contract is required; otherwise reuse drain + re-admission.

Gates: focused + full native suite with `-warnaserror`; scoped format/analyzers; encoding/board checks; offline simulated activity/quiet/takeover/timer tests proving (a) **NORMAL auto-resumes after the quiet+countdown conditions are met**, (b) explicit pause/stop never auto-resumes in either mode, (c) SAFE never auto-resumes, (d) no dispatch during any pause trigger (loading, focus loss, mouse movement), (e) target change/recovery hold/uncertain cleanup block resume, and (f) hard-stop keypress works in all modes. A live rehearsal needs **fresh owner scope** and is not part of implementation.

## 7. Boundaries and non-goals

No global key interception; no bypass of Escape/takeover; no auto-resume from an explicit latch; no concurrent capture jobs; no network manipulation; no game-file writes; no Crowns/snacks/volume/restart; no changes to the input owner model. This brief itself makes no code or rule changes.
