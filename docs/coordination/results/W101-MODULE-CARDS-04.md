# W101 semantic module card integration

2026-11-11. Source-only, offline task W101-MODULE-CARDS-04.

Added optional code-owned W101ModuleCardAdapter to the existing catalog factory,
with prepare and readiness callbacks. New w101.interact and w101.menu cards are
explicit and bounded; existing w101.auto-idle identity/options are preserved,
with non-native movement methods still blocked. Default factory absence means
live availability remains false; no observer, game or input is started.

Tests cover default rejection, trusted readiness before preparation, exact mode
routing, invalid time limits, empty prompts/arbitrary menu actions and older
Auto-Idle settings. Native build/test/format belongs to coordinator. No tests or
builds were run by this agent, and no live capability is claimed.

Review findings sent to binding/runtime agents: semantic context must distinguish
live OCR from saved/manual annotations, actual prompt identity is not present in
aggregate UI booleans, and a concrete trusted card-to-runtime factory must enforce
configuration rather than silently ignore it. Generic X can perform quest effects,
so the present sigil binding completes only on verified interaction menu, not
spoken dialogue. Root coordinates final runtime factory and full-suite validation.
