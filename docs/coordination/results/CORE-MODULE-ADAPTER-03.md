# Module prepared-job adapters

2026-11-11. Source implementation; coordinator owns native build/test/format.

`ModuleOperation` now exposes `PrepareOffline` and trusted `PrepareOwned` returning
the existing `PreparedDeckJob`. Its legacy Submit methods prepare and submit once.
Cards can therefore use the same operation without submitting a nested scheduler
job or acquiring another input owner.

`ModuleJobAdapter.PrepareReplay` supports the four known module kinds through
the existing generic labelled-session replay. Preparation validates homogeneous
module kind, optional game identity, manifest/binding, finite timeout and filenames.
Bundle/manifest/events are frozen and source-pinned. Execution stages those bytes
in an operation-local input directory, checks that no input is held, and preserves
admission and replay evidence. Completed, stopped and incomplete map respectively
to Completed, Failed and Unknown. Cancellation retains staged evidence and remains
Cancelled; no successful game outcome is inferred.

The existing saved-image review is available through `PrepareOffline`; it retains
its current W101 OCR limitations. Trusted owned preparation remains code-only and
requires the actual scheduler lease at execution. A callback still owns current
control qualification and outcome verification; preparation does not qualify it.

`ModuleReplay.Run` accepts an optional cancellation token checked before reads,
per event and before report creation. Existing callers remain compatible.

New adapter tests cover preparation without dispatch, timeout/kind/game rejection
and cancellation before source access. Follow-up tests also cover each frozen
source changing while queued, queued cancellation before execution, and distinct
missing-evidence Unknown versus rejected-receipt Failed outcomes. Existing ModuleOperation tests cover source
mutation, queued cancellation and exact scheduler ownership; card agent supplies
four-kind deck completion fixtures. No live game, desktop, dependency or build was
used by this task. Root records integrated verification separately.
