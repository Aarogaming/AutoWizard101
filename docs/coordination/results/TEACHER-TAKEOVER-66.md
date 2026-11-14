# Teacher action attribution after takeover

Status: offline source repair; not installed.

Two synthetic regressions demonstrated that the teacher linker skipped an intervening UserActive or Cancelled observation and accepted a later quiet outcome for the earlier action. Both tests failed before the repair. The candidate action search now stops at takeover/cancellation; a later independent action can still supply its own evidence. This preserves user control as a causal boundary rather than merely filtering individual samples.

Sixteen focused teacher/export/condition replay tests passed after repair. Warnings-as-errors build passed. Scoped formatting and analyzers checked at integration. No live game, desktop, host, installation or dependency changes. This repairs proposal evidence attribution, not proof of causation or live gameplay.

Next: qualify other interruptions (target/context/ownership changes) as explicit causal boundaries, retaining separate action candidates and raw evidence.
