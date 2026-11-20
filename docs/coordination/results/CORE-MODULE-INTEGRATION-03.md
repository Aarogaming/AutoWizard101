# Shared module cards integration

2026-11-11. Offline source integration; no gameplay or installation.

Three parallel tasks implemented prepared adapters, configurable cards and an
independent source review. Four game-neutral cards (`module.navigation`,
`module.sigil`, `module.menu`, `module.auto-idle`) reuse the existing module
compilers, runner and shared scheduler. They support labelled saved-session
replay and the separately limited W101 saved-screen review pipeline.

The cards explicitly rehearse recorded evidence. They do not grant live input,
claim current workspace localization or qualify arbitrary game recognition.
The existing live W101 Auto-Idle preparation card retains its identity/status.
An explicit Add card picker makes new registered cards available to older saved
collections, adding undealt defaults without resetting history.

Prepared jobs avoid nested scheduler submission. Replay freezes/pins bundle,
manifest and event bytes, stages inputs separately from scheduler artifacts,
checks module kind and internal game coherence, and supports per-event cancellation.
Stopped evidence returns Failed; incomplete evidence remains Unknown. Trusted
owned preparation preserves the scheduler-held lease checks but is not enabled
by any imported card or bundle.

Validation: **2,097 native tests passed**, zero failed/skipped, warnings-as-errors.
Scoped formatting and analyzer checks passed; project-board and advisory
capability-map checks passed. Initial test-only analyzer/accessibility errors
were repaired before the final passing run.
Receipt: `.local/module-cards-20261111/module-cards-final.trx`.

Tests cover all four module cards through the real deck/scheduler with synthetic
independent-game evidence; missing/rejected receipts; queued source mutation;
queued cancellation; kind/timeout checks; and undealt card addition to an older
collection. No actual desktop input, game connection, models, new dependencies,
packaging or installation occurred.

Next: qualify game-specific observations/control bindings for trusted live module
cards, including loading, takeover, entry/exit and independent outcomes. Automatic
between-run native Idle, autonomous routes and live sigil/menu dispatch remain
separate integration work. Saved evidence completion is not live-game completion.
