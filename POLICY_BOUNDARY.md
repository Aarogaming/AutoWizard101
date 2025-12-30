# Policy Boundary (Project Maelstrom)

## What the app can do
- Run as an offline-first trainer with capability-driven plugins (overlay widgets, replay analyzers, minigame catalog).
- Dispatch automation commands through executors; policy currently defaults to live-allowed (`ALLOW_LIVE_AUTOMATION=true`).
- Load allowed plugins based on profile (Public/Experimental) and declared capabilities.

## What the app cannot do
- UI, plugins, and manifests cannot override execution policy or executor selection; changes require code-level modification and rebuild.
- Plugin capabilities are declarative; capability negotiation does not grant new privileges beyond the configured policy snapshot.
- No automatic policy escalation: changing profiles or manifests does not force live blocking/allow beyond the code-defined policy.

## Where enforcement happens
- Policy load and snapshot: `ExecutionPolicyManager` (`execution_policy.conf`, defaults to `ALLOW_LIVE_AUTOMATION=true`, `EXECUTION_PROFILE=AcademicSimulation`).
- Executor selection: `ExecutorFactory` chooses Simulation or Live executors based on the policy snapshot.
- Live dispatch path: `LiveExecutor` uses the current policy snapshot; if policy were set to block, it would return a blocked result instead of throwing.
- Plugin gating: `PluginGate` evaluates capabilities (e.g., LiveIntegration) against the policy snapshot; gating follows the current policy and cannot be overridden from UI or manifests.

## Practical implication
- Live automation is permitted by default in the current build; setting `ALLOW_LIVE_AUTOMATION=false` is an opt-out marker for external tooling but runtime enforcement is relaxed.
- No UI, plugin, or manifest can bypass or strengthen this policy at runtime; altering policy behavior requires source changes and a rebuild.
