# Teacher action baseline qualification

Status: offline source correction; not installed.

Review found that teacher linking rejected an Unknown/Loading outcome but accepted those scenes as the before-state. A loading completion could therefore be presented as a changed keyboard-action outcome despite lacking a recognizable baseline. Both before and after scenes must now be outside Unknown/Loading. This does not prove causation: resulting cards remain editable proposals and not live-qualified.

Added two baseline regressions. Fourteen teacher-export and condition-aware replay tests passed, including accepted export and recorded-fact conditions. Warnings-as-errors test build passed. Scoped formatting/analyzer checks run at integration. Initial restricted test runner stalled before results; only its two identified owned dotnet processes were stopped, then the same offline checks passed in the normal user environment. No pre-fix failing test result is claimed.

No game access, desktop activity, host launch, installation or new dependency. Next: preserve this change for a future authorized release and expand fresh-session teaching evidence independently.
