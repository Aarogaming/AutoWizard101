# Navigation feedback contract

Status: implemented and offline verified, not live route recognition.

The existing navigation module accepts up to four distinct correction scenes per
segment, each mapped to a registered action. Every correction uses the same runner,
input owner and action/time budgets. Fresh receipt-bound observations select the
next action; a correction cannot directly prove destination arrival.

Eight added synthetic checks exercise adaptive left/right/forward sequences,
stale receipts, bounded repeated correction and ambiguous scene rejection.
The optional field is omitted when null so older serialized packs remain unchanged.
Full native suite at integration: 2,341 passed. Scoped formatting/analyzers passed.
No new W101 route, live recognition, arrival or controller support is claimed.
Next: qualify a trusted provider of route-relative alignment/progress/arrival cues.
