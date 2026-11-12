# Wizard101 workspace and Auto-Idle

Open **Wizard101 · routines and Auto-Idle**. Wizard101 is one game with
**Routine builder**, **Auto-Idle**, and **Observe & teach** pages. See
[the routine architecture](W101_ROUTINES.md) for the category and action model.

## Methods and controls

The **Auto-Idle** checkbox sits left of the method selector. Checking it permits
preparation; only **Start** begins a bounded session. Loading settings, selecting
a routine, launching the app or installing an update never starts input.

| Method | Behavior |
| --- | --- |
| Spin | A-only left pulses (default), D-only right pulses, or optional opposing A/D turns. One-direction choices never reverse automatically. |
| Step | W then S pulses, requiring a user-confirmed clear home area. This can drift. |
| Routine | A saved sequence of 1–8 shared motion actions, checked after every key. Translation requires a clear home area. |
| Native Idle | Instructions for W101’s Escape → Idle command. Maelstrom sends no input and installs no activity hooks. |

Choices and timing persist in `auto-idle-settings.json`. Session permission,
window identity and reference images do not restore permission to run. Methods
remain available rather than replacing one another. The native Idle banner was
observed, but its long-duration connection test was interrupted by manual input.

1. Choose method/details. For Routine, save a sequence in **Routine builder**
   and choose **Use for Auto-Idle**.
2. Select W101 at 1280 × 720 in a clear test area, with chat and menus closed.
   Walking requires your home. Capture and review the reference.
3. Confirm the area, check Auto-Idle, and Start. Switch to W101 and leave input
   quiet for five seconds. Maelstrom never activates or refocuses the game.
4. Keyboard/mouse activity (including remote input), Escape or focus loss pauses.
   Stop, unchecking Auto-Idle or closing Maelstrom cancels and releases keys.
   Resume is explicit; it starts a new bounded session.

Defaults: 60 ms pulses, 90 seconds between cycles, 15-minute session. Limits:
40–150 ms per key, 60–300 seconds between cycles, 1–30 minutes, 20 cycles.
A custom cycle may hold keys for at most 600 ms total. A failed check can stop
partway through a cycle. There is no continuous held-key option.

The foreground routine pauses while you use another app. Native Idle is the
in-game option for working elsewhere; isolated Dodge does not qualify background
W101 input.

## Evidence and limitations

Every key requires current process/handle/start identity, foreground ownership,
client size, quiet input and a fresh before-image (at most 200 ms old). The fresh
after-image must follow key release. Missing or uncertain evidence pauses.

A small 160 × 90 comparison retains original health/mana and spellbook anchors.
One-way spinning and custom sequences compare the scene to the previous verified
view, allowing accumulated rotation. Preset opposing turns and W/S steps also
compare to the initial scene. No confirmed scene change pauses the chain.

These are image-change guards, not semantic location, chat, menu or obstacle
recognition. Animated backgrounds can look like movement. The user confirms the
area; scene change proves neither character movement nor prevention of logout.
This is not a map navigator or home farmer.

There is no model, network request, continuous capture or hidden focus retry.
Capture resources are released between checks. The activity monitor counts events
without retaining text or pointer positions, and a per-user lease excludes
simultaneous movement sessions from other Maelstrom copies.

Reports in `home-movement/<session>/` distinguish input receipts from visual
measurements and retain bounded last before/after/changed images and small
reference descriptors. Reports, routines, packs and settings remain in user data
across updates and uninstall. No old reports are automatically deleted.

Tests cover choices, persistence without consent, action resolution, sequence
budgets, per-step outcome checks, takeover/cancellation, gradual rotation,
real compact-HUD images, stale frames and key release. Simulated tests are not
live W101 qualification. See the [live-test record](W101_KEEP_ACTIVE_LIVE_TEST.md).

Developer checks (no input or foreground changes):

```text
Maelstrom.exe --verify-home-movement-ui <data-root> <output-directory>
Maelstrom.exe --verify-home-movement-monitor <output-json>
Maelstrom.exe --sample-window-stills <HWND> <data-root> <workspace-name> <seconds> <fps>
```

The passive still sampler accepts 1–60 seconds and 1–5 samples/sec, uses the
existing reviewable teaching-recording format, and stops near 128 MiB (one final
frame may cross the threshold). Capture timestamps and a separate report preserve
its evidence. Actual sample rate can be lower; this is not real-time video.
There is intentionally no command-line auto-start for movement.
