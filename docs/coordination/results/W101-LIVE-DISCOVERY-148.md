# W101 live window discovery — 148

2026-11-15. Current-window discovery and Observe-path check. No game movement, hook, game-file access, or installation. Maelstrom and W101 focus were changed during the desktop check; see the unexpected UI change below.

## Target found by the desktop window observer

The supported desktop window listing returned exactly one Wizard101 target:

- Window handle: `1681917634`
- App identity: `process:C:\ProgramData\KingsIsle Entertainment\Wizard101\Bin\WizardGraphicalClient.exe`
- Window title: `Wizard101`

A window-bound screenshot was captured without activating the window. The screen showed the native `YOU ARE IDLE` banner, the location labels `Ravenwood` and `Golem Court`, and an active Firecat Alley quest at `2 of 3`. These are visual observations only; no backend wizard identity or zone string was obtained in this pass.

## Controller identity handoff gap

The same live window could not be enumerated from the repository execution shell:

- Maelstrom `--cli windows` returned an empty `windows` array.
- The shell's visible-window process listing was also empty.
- `sky` exposes the selected HWND, app path, and title, but not the PID or process creation ticks required by Maelstrom's target fence.
- Desktop Commander reports the user's device offline, so it cannot provide a same-session terminal fallback.

Opened the existing Maelstrom application in the interactive desktop and navigated to its Observe workspace. Its game-window selector found `Wizard101 — WizardGraphicalClient (PID 120576)`, matching the PID in the recent reattach record. The selector does not expose process-start ticks in its accessible text, and the `Observe selected window` button could not be invoked: the desktop tool rejected its computed point `(-172, 91)` as outside the Maelstrom window bounds. A fresh read afterward showed Observe had not started and its preview/record controls remained disabled. No game-directed input was intentionally issued.

During focus restoration, a fresh W101 screenshot showed the Monstrology Tome open and the earlier native-Idle banner absent. A Tab key had been targeted to the Maelstrom window while its accessible focus still reported the workspace-name field; the tool reported the key call completed, but it did not establish where Windows delivered it. The Monstrology Tome may reflect that focus-routing attempt or unrelated concurrent desktop activity. The cause and Idle state are unknown. No further input or cleanup was attempted.

This is consistent with the CLI process running outside the interactive desktop/window session visible to the desktop observer; the exact platform boundary is not proven. The GUI selector can discover the target PID, but the observation action still needs an accessible, in-bounds invocation. Maelstrom's `windows` implementation uses `EnumWindows` and `GetWindowThreadProcessId`, then reads the PID's process start time. Its empty result therefore cannot be replaced safely by guessing a PID or passing only the HWND.

## Outcome and next gate

The live app was visually idle at initial discovery. The Maelstrom GUI discovered the W101 PID, but its Observe action did not run and this execution context still lacks the complete verified `(HWND, PID, process-start-ticks)` handoff. A later screenshot showed Monstrology Tome open; the cause is unknown and native Idle is not currently verified. No navigation experiment was dispatched. Next, fix/qualify the Maelstrom Observe control's window geometry and focus routing before any live action. Keep direct desktop input out of the loop so Maelstrom remains the single input owner.

The last authorized Maelstrom movement evidence remains the local coordinate-waypoint result in [W101-REATTACH-147](W101-REATTACH-147.md); it is not a route or arrival qualification. This observation did not recheck the present backend Idle state after the screenshot.
