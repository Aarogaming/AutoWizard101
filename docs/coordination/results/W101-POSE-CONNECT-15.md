# W101 optional pose connection — 2026-11-11

Status: source integrated, offline checks in progress; no live hook test yet.

One opt-in CLI UI watch can now collect a bounded pose candidate after restoring
its root hook. The existing scheduler retains ownership throughout. The new
player hook preserves RCX and RFLAGS, replays the exact five-byte movsd, and uses
the existing verified scratch restoration protocol with a configurable entry
length/export name. Root behavior defaults are unchanged. Dependency fingerprints
cover the worker, pose decoder and shared bounded reader. No production dependency
added; no network or installed-file writes.

The current on-disk client image has one matching PlayerHook signature (reported
in W101-ROUTE-GEOMETRY-14); this is compatibility evidence only. Stable parent,
actor marker and two matching pose reads yield a candidate, not proven identity.
No address is returned in the report. Native validation rejects stale, nonfinite,
oversized, unsolicited or self-authorizing candidates. Pose is not a live
navigation provider yet. A recorded heading/displacement check is required.

Checks: 90 focused native tests, 55 UI observer and 7 pose Python tests passed.
Full native suite, scoped formatting/analyzers and live qualification pending.

Full offline gate passed: 2371 native tests, scoped whitespace/analyzers, Python syntax and project-board validation. Live no-movement notification issued; awaiting Ready. No controller or hook active while waiting.

## Live evidence

- `f775f58f4f3b42c1928469bc3aca5a8e`: notified observation returned XYZ
  (888.2929,-277.8175,-2.6403), yaw5.522124. Native Idle true, cleanup and hook
  restoration confirmed. Six pose reads/72bytes. No input.
- `fe19a33c1c544790a99ab9b806b824e0`: notified W150ms visit measured planar
  displacement58.0694 units, unchanged yaw; native Idle restored. User reported
  blocked/deflected movement at the Commons lamppost. Do not use this as a
  free-space speed estimate or arrival qualification.
- User moved to dorm and authorized continuing without them for bedtime.
- `9a1b3d33d15e4ac59e06fbdfcc4a8bdf`: unattended dorm W100ms/40-unit waypoint
  trial rejected at control-scope admission: target-not-foreground. Zero inputs,
  no pose/movement stage, no waypoint outcome. No focus stealing or desktop use.
- `78eea6ce409549d1acd28c5bd7425847`: observation-only follow-up confirms awake,
  not loading, cleanup/restoration true. Cannot claim native Idle restored after
  the user changed location; input remains blocked while another window is front.

## Measured visit implementation

Optional pose reads24/25 surround the existing owned pulse. Missing start pose
prevents input; missing end pose cannot prove movement. Before-input plan and
separate movement report preserve actual displacement. Optional20–100-unit
relative waypoint uses archived -sin(yaw),-cos(yaw), ten-unit tolerance. The
heading relation remains a hypothesis to test in unobstructed dorm movement.
Misses fail, never rewrite the target after movement. Coordinate agreement remains
candidate-only, not live-qualified route arrival. Existing visit restores Idle
when current ownership/foreground permit; user takeover remains authoritative.

## User-directed next architecture

Real navigation must use a bounded continuously sampled coordinate/heading feed,
measured motion rate and interruptible held movement, not repeated multi-second
snapshot/pulse visits. Detect collisions from interrupted progress, loading from
the existing loading feed, and crossing arrival from world/zone/map confirmation.
Use one scheduler owner, fixed polling budgets, no backlog, stale-data release,
and clean session-end restoration. Current pose decoding demands matching
stationary snapshots; it is not yet a moving-player streaming feed. Zone identity
is not supplied by this implementation. Do not infer a destination from a loading
screen alone. No game files were changed, no network operations, models or volume
changes, no doors or interactions, no installed update.

Final checkpoint:2378 native tests pass; scoped formatting/analyzers and Python checks pass. Final source docstring/fingerprint synchronization rebuilt below. Live dorm continuation blocked by foreground; no input owner/capture/desktop control remains active.
