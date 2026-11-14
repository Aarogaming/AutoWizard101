# Typed aliases and shared execution surfaces

2026-11-12 — CORE-DELIVERY — source verified offline, not installed.

## Register contract

JobFactDefinition accepts optional DataType and Aliases. Supported storage types: BOOL, signed 16-bit INT, signed 32-bit DINT, finite double REAL, STRING. Existing boolean/string/number kinds remain compatible and default to BOOL/STRING/REAL. Trusted routine definitions may supply aliases and integer types. Freeze validates types, bounded names, uniqueness across canonical and alias names, and copies supplied aliases. Alias chains, type coercion and overrides of standard facts are not supported.

Built-in aliases include Idle → nativeIdle, MenuOpen → menu, DialogueOpen → dialogue, CanInteract → interactionPrompt, and Health/Mana/Energy/Potions. Names are case-sensitive. Values remain stored under canonical names only. Conditions and scope filters resolve aliases through the same JobFactCatalog and JobDeckRules used by cards and replay; aliases do not duplicate observation storage.

Snapshots expose aliases with id, aliases, dataType, unit, value, quality, source, observedUtc, gameId and targetId. Quality is fresh/stale/unknown/invalid; only fresh, correctly typed values are exposed as usable values. Missing, future and invalid observations never become zero or false. Existing raw facts remain available with their timestamps for compatibility. A declaration neither starts observation nor supplies an unavailable producer.

## Shared execution

Native snapshot/capabilities replies include the register. Existing Node-RED snapshot subscriptions receive it without a second polling loop. MCP maelstrom_get_alias_register forwards to snapshot; CLI --deck-api forwards the same JobDeckRequest over the current-user named pipe. MCP deal/step/start and native cards already use JobDeckHost → JobDeckService → OperationScheduler. This change preserves that single path; it adds no direct input dispatch. Clients must deal a configured routine card and explicitly step/start with the existing revision/request-ID gates. Discovery is not authority to execute.

Read-only Python knowledge/calculation/planning helpers remain pure helper tools, not gameplay executors. They are not newly registered action cards. This does not claim every legacy standalone CLI operation has become a deck capability; only the existing registered routine catalog is shared across these execution surfaces. No scheduler bypass or generic arbitrary-function invocation was added.

## Evidence and limits

2,581 native tests passed, including named-pipe host read-only discovery, offline routine scheduling, rejection/ownership checks and alias range/condition tests. MCP and CLI bridge Python suites: 30 tests passed. Scoped formatting/analyzers and Python formatting/syntax checked. No new dependencies, client attachment, focus, desktop input, capture, model, installation or W101 activity.

Custom aliases currently belong to trusted routine fact definitions; a user-facing alias-editing screen and persisted user override registry are not supplied. Global snapshot exposes standard aliases; routine-specific definitions remain in capabilities.routineFacts and are used by their own condition/replay evaluation. Hour-long recording qualification and live capabilities remain separate.
