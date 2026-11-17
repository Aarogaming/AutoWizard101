# MANUAL-CAPTURE-CONTROLLER-02 — implementation progress (interim)

Status: **IN PROGRESS — core gate and persistent overlay host delivered; CLI wiring and capture sync remain.**
Date: 2026-11-16 · owner: root · workstream: `CORE-DELIVERY`

## 1. Delivered so far

- `Utilities/ManualCaptureController.cs` — pure dispatch gate with NORMAL (default) and SAFE modes. Loading/focus/mouse triggers close the gate; NORMAL reopens only after ≥3 s quiet → visible pre-resume countdown → fresh verification; explicit pause/stop and hard stop are latches nothing auto-clears; recovery holds and cleanup uncertainty block resume.
- `Utilities/ManualCaptureHost.cs` — persistent, nonactivating overlay (`WS_EX_NOACTIVATE`) with Play/Pause/Stop buttons and four explicit hotkeys (`Ctrl+Alt+P/S/R/Q`, no low-level key interception). Target identity and `GameInputLease` recovery checks feed the controller; activity revisions drive the mouse interlock; loss of foreground closes the gate. The host never dispatches input or activates a window.
- Tests: `ManualCaptureControllerTests.cs` — 9 focused tests, including the bug they caught (fresh activity during the pre-resume countdown was ignored; fixed).

## 2. Verification (this increment)

| Check | Result |
|---|---|
| Focused controller tests | 9/9 passed |
| Production build `-warnaserror` | Build succeeded |
| Full native suite | **3,077 passed / 0 failed** on the final run; two earlier runs showed transient `NamedPipeClientStream.Connect` timeouts in `JobDeckHostTests` (those tests pass in isolation) — recorded honestly, not hidden |
| Scoped whitespace / analyzers | exit 0 / 0 (WFO1000 form-property attributes applied) |
| Encoding / board | 0 violations / 30 workstreams valid |

## 3. Remaining work (claim stays active)

1. Headless `--manual-capture` CLI entry via `MaelstromCli.cs` / `Program.cs` (paths owned by this claim).
2. Front/back capture on one `SessionTimeline` clock (reuse `TeachingRecordingCli`).
3. Host-level tests where headless-safe (hotkey mapping, target invalidation) without launching UI in CI.
4. Live rehearsal — separate owner authorization; not part of implementation.

## 4. Known integration note

The existing `W101RunningNotice` click handler requires the game to be foreground. The new persistent host deliberately does **not** — pause/stop clicks and hotkeys work when focus has moved, while still never granting input ownership. This is intentional and must be preserved.
