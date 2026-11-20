# Shared catalog and bounded refresh integration

2026-11-11. Source-only, offline integration; no installation or gameplay.

Three parallel tasks added typed facts/card validation, bounded observation
refresh, and card UI/API/MCP discovery. Sixteen standard descriptors distinguish
types, units, producers and unavailable readings. Trusted routines can add frozen
scoped facts; imported cards cannot create capabilities. Unknown names and wrong
types are rejected before dispatch. Missing readings remain unknown.

Discovery exposes the existing routine catalog and per-routine fact definitions.
The card editor uses those same definitions. Availability is not session readiness
or permission. Existing prepared cards remain unavailable until their adapters
are connected and qualified.

Waiting opted-in Quest decks can refresh via the existing owner: one-second
admission check, five-second minimum interval,30 attempts per run, and at least
five seconds remaining. No overlapping observer/job is admitted. Stop/drain and
deadlines invalidate late observations. This is a low-rate quest-context producer,
not a general high-rate minigame feed or an implementation of resource readers.

Validation: **2,075 native tests passed**, zero failures/skips, warnings-as-errors;
**15 Python MCP tests passed**. Scoped native formatting/analyzers and Python
format/syntax checks passed. Project board and capability inventory checks passed.
Native receipt: `.local/catalog-refresh-20261111/catalog-refresh-final.trx`.

Synthetic tests exercise actual deck/scheduler waiting→fresh facts→one completion
and Stop suppression, plus pure refresh budgets and late-result rejection.
They do not qualify the W101 reader live. No desktop, game, model, credentials,
installation or new dependencies were used.

Next: qualify integrated observation/card behavior separately; add actual resource
producers and connect shared module adapters. Full combat and autonomous navigation
remain unfinished. These changes are not installed.
