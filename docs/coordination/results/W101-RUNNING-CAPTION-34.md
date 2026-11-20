# Optional W101 running caption - 2026-11-12

Task W101-RUNNING-CAPTION-34, CORE-DELIVERY. Owner root/running_caption. Source implementation ready; offline checks pending coordinator build slot. Root owns settings/control-scope integration and live qualification.

## Contract

W101RunningNotice.StartAsync(GameWindow, Func<string>, CancellationToken) returns Task<W101RunningNotice>; dispose asynchronously before releasing the owning scope. The status callback accepts Running, Paused or Stopping, optionally prefixed with Maelstrom; unknown values display Paused. Caption never grants control or sends inputs.

A dedicated STA loop updates at5Hz. The borderless layered caption is nonactivating, click-through and absent from the taskbar. It uses target client coordinates translated to screen space, per-monitor thread awareness, target DPI-scaled small font/size, and a two-pixel-at96DPI bottom inset. It hides on focus loss/minimization and exits on target replacement, cancellation or disposal. No desktop capture, model, audio, window activation or game-process injection. Startup/shutdown waits are bounded to3seconds. The parent controls opt-in preference and lifetime; there is no global persistent service.

## Owned files

- ProjectMaelstrom/ProjectMaelstrom/Utilities/W101RunningNotice.cs
- ProjectMaelstrom/ProjectMaelstrom.Tests/W101RunningNoticeTests.cs
- This report

Tests cover physical placement across positive/negative monitor origins, DPI scales and tiny clients, foreground/current-target visibility policy, and bounded recognizable status text. These are pure offline tests; they do not prove OS click-through behavior, overlay visibility on W101, lifecycle timing, exclusive fullscreen rendering or live focus retention.

## Validation

Pending coordinator build slot. No new dependencies, settings edits or runtime control by this worker. Parent should record live screenshot/visibility and owner cleanup separately before claiming live qualification.

Coordinator checkpoint: integrated native suite2513 passed, zero failed/skipped; scoped whitespace format/verify and analyzers passed. Warnings-as-errors compilation passed. Board30workstreams valid. No installation. Live evidence and limits are in W101-CONTROL-34; deliberate focus-switch recovery is offline-tested, not yet live-qualified.
