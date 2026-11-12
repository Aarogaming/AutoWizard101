# CORE-CATALOG-SURFACE-02

2026-11-11 · CORE-DELIVERY · source integration only.

- Card settings expose a registered string-fact picker for scope filters and a
  shared fact reference with type, units, producer and availability explanation.
  Condition text remains editable and the native rules remain authoritative.
- MCP exposes `maelstrom_get_capability_catalog` through the existing native
  `capabilities` command. No arguments, connection, observation, startup or
  fabricated fallback; older/unavailable owners report their real failure.
- The CLI advertised tool list includes the new discovery operation.
- JOB_DECK documents source-only integration and definitions versus live facts.

Verification: 15 MCP Python tests passed; Black check and compileall passed.
Native formatting, build and tests belong to the coordinator's serialized slot.
No desktop, game, model, dependency or installation work was performed.
Follow-up: UI consumes Deck.FactDefinitions per routine, including frozen trusted extensions; no second global-only fact list is maintained.
