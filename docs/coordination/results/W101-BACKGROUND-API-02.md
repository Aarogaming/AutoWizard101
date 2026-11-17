# W101-BACKGROUND-API-02 — background facts service (interim)

Status: **IN PROGRESS — store, activity flags and read-only pipe contract delivered; resident reader and MCP surface remain.**
Date: 2026-11-17 · owner: root · workstream: `W101-COMBAT`

## Delivered

- `W101FactsStore`: bounded (64), registered IDs → Observed, proposed/unavailable producers → Candidate only, TTL/target changes → Unknown with explicit reason; 7 tests.
- `W101ActivityFlags`: accepted Diego predicate (visible nodes, 3 samples/≥600 ms, target/image/loading/Idle gates, trigger-only); 9 tests.
- `JobDeckHost` `facts` command + `W101BackgroundFacts`: read-only snapshot over the existing native pipe; clients cannot publish or mutate facts. 1 test verifies pipe input cannot change a fact.
- Full native suite **3,099 passed / 0 failed** on the final increment; format/analyzers/encoding/board green.

## Remaining

1. ~~Resident reader contract~~ **DONE offline** (`W101BackgroundReader.cs`, 6 tests): identity-validated cycles, no foreground gate, invalidation on target change/recovery/fault/cancel. Remaining: wire the real pinned Python readers into `readCycle` and run a separately authorized live qualification pass.
2. MCP `maelstrom_get_w101_facts` tool routed through the same native owner.
3. Live verification that facts expiry/invalidation behave correctly against a real client; separate owner authorization.

No live work, input dispatch, network or game-file writes were performed.
