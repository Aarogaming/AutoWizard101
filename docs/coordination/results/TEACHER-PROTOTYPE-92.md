# Teacher-to-native-prototype bridge92

2026-11-14. Source only; no live game interaction, training run or installation.

TeacherActionReplay.Export now emits prototype.json after its existing verified teacher/input/video checks and successful source replay. It uses CardPrototype.Create/Save and the existing review.routine catalog action, with exact absolute graph/session paths. The draft is undealt and has no qualification history. Existing CLI --teaching-link-action automatically receives this additional output; the native Workshop can load it.

Validation: 27 focused TeacherActionReplay/CardPrototype tests passed with warnings-as-errors compilation. Extended integration test loads the generated native prototype, verifies undealt state, exact paths, empty qualification history and valid replay binding. Scoped whitespace and analyzer checks passed. Board check separately recorded by coordinator.

This is one attributable teacher action, not repetition discovery or autonomous improvement. Discovery-trace replay is reconstruction, not independent validation. No action authority, live card insertion, model training or repeatability qualification is created. Next: bounded multi-action extraction with deduplication, contrary outcomes and separate holdout-session studies.

Follow-up: LinkAll now extracts up to64 non-overlapping attributable actions from at most4096 events, deduplicates identical proposal evidence, and rejects conflicting reuse of an identity. Legacy Link selects the first from this validated collection; Export still creates one prototype, not a batch. Three added cases cover multiple distinct actions, duplicate logs and conflicting identities.30 focused tests pass; scoped format/analyzers clean. Cross-session grouping, batch export and independent validation remain pending.
