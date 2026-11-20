# Action countdown and foreground recovery — 2026-11-12

Source implementation, not installed or live-qualified.

A reusable nonactivating, click-through notice is connected to W101ControlScope after foreground admission and before caller actions. It appears near the top center of the game window for five seconds and closes before gameplay. Input activity, loss of focus/ownership, changed target, minimization or cancellation prevents completion. No keys/clicks are dispatched by the notice. This covers shared Idle, navigation visit/probe and routine Idle scopes; it is not yet a universal notice for every legacy controller. Existing total operation deadlines are unchanged.

Settings includes an optional countdown sound (default on, following the user's request); no volume changes. Older settings retain defaults, and saves preserve existing preference fields. System sound availability/mute can make the audible cue unavailable; the visual cue remains primary. Exclusive fullscreen overlays remain unqualified; windowed/borderless behavior needs live inspection.

Foreground recovery now offers a ten-second clickable verification window when Windows denies the single normal activation request. A real user click can grant foreground; no synthetic click, input-queue attachment, repeated focus forcing or global Windows setting change. The existing fresh cleanup checks still decide recovery. The window closes at the bounded end, and no routine starts automatically. Unattended foreground admission is still not guaranteed or live-qualified.

Source: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow . An unlocked desktop alone does not grant a background process foreground rights; SetForegroundWindow can be denied. This is not evidence of W101 anti-cheat.

Focused13 tests passed (countdown boundaries, preference migration/round-trip, existing foreground guards). Final full-suite and formatting results follow. No live input, volume adjustment or installation performed in this task. Recovery/new-profile/dorm waypoint remains pending.

Final verification:2392 native tests passed; scoped formatting/analyzers passed; board valid (30 workstreams).
