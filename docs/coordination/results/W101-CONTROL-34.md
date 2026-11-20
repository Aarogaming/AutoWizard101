# Focus-aware control and running caption - 2026-11-12

W101 scopes now count physical/remote keyboard takeover only while the exact game window is foreground. Mouse events are ignored for these scopes. Other DesktopActivityMonitor callers retain default global semantics. Input dispatch still requires target focus; cancellation, changed target, stale monitor and uncertain cleanup remain stop conditions.

Released navigation checkpoints can wait up to60seconds (within parent/finalizer budgets) for focus, without activating a window or resetting keyboard takeover. Held-key Ready checks remain immediate, causing release on focus loss. Feedback reobserves pose against its frozen destination; fresh Idle attempts reconcile state. This is NOT universal resumable travel/Dance/menu execution. Countdown interruption still cancels startup. Minimized/changed-size/other invalid states remain blocking. No consequential action is blindly replayed.

Optional setting W101RunningCaption defaults false; exposed in Settings. Enabled in the isolated live-test data root. Tiny click-through/nonactivating caption at physical client bottom center, scales with DPI, updates5Hz, hides when focus leaves/minimized, closes with scope. No volume change. Existing legacy keyboard-only preference is retained for compatibility, but W101 scope enforces focused keyboard-only policy. Countdown wording updated.

80 focused checks passed with warnings-as-errors. Live waypoint2b865349063b4280a38dc5f0c92b8257 completed:60-unit relative target, two40ms corrections, two arrival observations, final error5.28units, native Idle independently verified. Caption lifetime executed; physical visibility not independently user-confirmed. No deliberate live focus interruption tested yet. General named-route repeatability remains unqualified.

Next route: owner-taught center-path approach to house door, followed by arrival evidence. Long-term chain: World Gate/Wizard City -> Commons/Pet Pavilion -> Dance -> Idle. Existing landmarks are not sufficient to enable arbitrary navigation.

Long center-path live approach fcf523e0828e490f819e2b8fb76d90fb completed loading-triggered release and native Idle. Final saved pixels show equipped-house interior. This is one successful forward edge, not repeatable whole-route proof. Reverse-loading direction S now uses the same guarded transport as W;16 input tests pass. Reverse attempt304c503748a34e0b8f9a50ed5b676a17 stopped beforeinput on foreground admission. Startup now has the same bounded focus-only wait; this later change awaits final live qualification.

Coordinator checkpoint: integrated native suite2513 passed, zero failed/skipped; scoped whitespace format/verify and analyzers passed. Warnings-as-errors compilation passed. Board30workstreams valid. No installation. Live evidence and limits are in W101-CONTROL-34; deliberate focus-switch recovery is offline-tested, not yet live-qualified.
