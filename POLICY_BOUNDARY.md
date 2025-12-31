# Policy Boundary (Aaroneous Automation Suite)

## What the app can do
- Run as an offline-first trainer with capability-driven plugins (overlay widgets, replay analyzers, minigame catalog).
- Dispatch automation through executors that are selected by policy (simulation vs live).
- Load plugins based on profile (Public/Experimental) and declared capabilities.

## What the app cannot do
- UI, plugins, and manifests cannot override execution policy or executor selection; changes require code modification and rebuild.
- Capability negotiation does not grant new privileges beyond the current policy snapshot.
- No automatic policy escalation: changing profiles or manifests cannot flip live/blocked state.

## Where enforcement happens (current runtime)
- Policy load: `ExecutionPolicyManager` reads `execution_policy.conf`; defaults are safe (`ALLOW_LIVE_AUTOMATION=false`, `EXECUTION_PROFILE=AcademicSimulation`).
- Executor selection: `ExecutorFactory.FromPolicy` and `LiveExecutor` block when `AllowLiveAutomation` is false (returns blocked result, logs).
- Plugin gating: `Plugins.Evaluate` blocks `LiveIntegration` capability when live is disabled and when profile is AcademicSimulation.
- Input/Macro bridges: `InputBridge` and `MacroPlayer` respect `AllowLiveAutomation` and block commands when live is disabled.
- Settings UI surfaces the loaded policy snapshot (Allow, Mode, Path, Backend).

## Practical implication
- Live automation is OFF by default. Enabling it requires editing `execution_policy.conf` or otherwise changing the snapshot, then restarting the app.
- UI/plugins/manifests cannot bypass or strengthen policy at runtime; altering policy behavior requires source changes and rebuild.

## Planned/aspirational (not yet enforced)
- Additional sandboxing or stricter executor separation would require future code changes and is not active today.
